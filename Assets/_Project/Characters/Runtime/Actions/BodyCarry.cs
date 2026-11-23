using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>Stage of carrying another player in the arms ("princess carry"). Values go over the network.</summary>
    public enum BodyCarryPhase : byte
    {
        None = 0,
        Lift = 1,     // crouch / kneel, arms under the knees and the back, up with effort
        Hold = 2,     // carrying (loop: weight, breathing, bounce, dangling legs)
        PutDown = 3,  // gently onto the floor (downed) or onto the feet (conscious)
        Drop = 4,     // the carrier got hit: the carried falls out of the arms
    }

    /// <summary>Timing and placement of the body carry, shared by the animation, the game and the server.</summary>
    public static class BodyCarryTimeline
    {
        // Real durations (calm, readable; the user asked for no rushed moves).
        public const float LiftFloor = 2.4f;       // downed player from the floor
        public const float LiftStanding = 1.9f;    // injured player who still stands
        public const float PutDownFloor = 2.1f;
        public const float PutDownStanding = 1.7f;
        public const float DropTime = 1.05f;
        // Key lengths the pose sequences are written in (BlockyCharacterActions.BodyCarry.cs); played stretched to the above.
        public const float KeyLiftFloor = 1.35f, KeyLiftStanding = 1.1f, KeyPutDownFloor = 1.1f, KeyPutDownStanding = 0.95f, KeyDropTime = 0.8f;
        /// <summary>First part of the lift in which the carrier steps to the right spot next to the body (real time).</summary>
        public const float ApproachTime = 0.6f;
        /// <summary>Carrier's turning speed while carrying (deg/s): the body turns slowly, the camera may look ahead.</summary>
        public const float TurnRate = 70f, TurnRateWalking = 95f, LookAheadMax = 100f;
        /// <summary>Third player treating the carried one with a medkit (H held), seconds.</summary>
        public const float HealTime = 5f;
        /// <summary>Carrier's speed while carrying (no running).</summary>
        public const float SpeedScale = 0.6f;
        /// <summary>Lying root frame: the pelvis lies this far behind the root (DownedPose / DeadPose).</summary>
        public const float LyingPelvisBack = 0.6f;
        /// <summary>Phase time mapped onto the key lengths of the pose sequences.</summary>
        public static float KeyTime(BodyCarryPhase phase, float t, bool fromFloor, bool standing)
        {
            float real = Duration(phase, fromFloor, standing);
            if (real <= 0f) return t;
            float key = phase switch
            {
                BodyCarryPhase.Lift => fromFloor ? KeyLiftFloor : KeyLiftStanding,
                BodyCarryPhase.PutDown => standing ? KeyPutDownStanding : KeyPutDownFloor,
                _ => KeyDropTime,
            };
            return t * key / real;
        }

        /// <summary>Effort grunt (key time into the lift).</summary>
        public const float LiftGruntTime = 0.62f;

        public static float Lift(bool fromFloor) => fromFloor ? LiftFloor : LiftStanding;
        public static float PutDown(bool standing) => standing ? PutDownStanding : PutDownFloor;

        public static float Duration(BodyCarryPhase phase, bool fromFloor, bool standing) => phase switch
        {
            BodyCarryPhase.Lift => Lift(fromFloor),
            BodyCarryPhase.PutDown => PutDown(standing),
            BodyCarryPhase.Drop => DropTime,
            _ => 0f,
        };

        // Canonical layout (carrier space, carried head on the carrier's LEFT; mirrored = head on the right).
        /// <summary>Where a downed body's pelvis should lie when the lift starts (carrier space).</summary>
        public static readonly Vector3 FloorPelvisLocal = new Vector3(0.05f, 0f, 0.5f);
        /// <summary>Where a standing player's root should be when the lift starts (carrier space).</summary>
        public static readonly Vector3 StandRootLocal = new Vector3(0.08f, 0f, 0.58f);
        /// <summary>Put down onto the floor: pelvis (carrier space).</summary>
        public static readonly Vector3 PutPelvisLocal = new Vector3(0.05f, 0f, 0.46f);
        /// <summary>Put down onto the feet: root (carrier space).</summary>
        public static readonly Vector3 PutStandLocal = new Vector3(0.12f, 0f, 0.62f);

        static Vector3 Side(Vector3 v, bool mirror) => mirror ? new Vector3(-v.x, v.y, v.z) : v;

        /// <summary>
        /// Carrier root (world) for a lift: next to the body so that it ends up in front of him, head on his left
        /// (or right when <paramref name="mirror"/>). Downed: the body lies prone, head along <paramref name="bodyRot"/> forward.
        /// Standing: the carried faces the carrier's right (left when mirrored).
        /// </summary>
        public static void CarrierSpot(Vector3 bodyRoot, Quaternion bodyRot, bool downed, bool mirror, out Vector3 pos, out Quaternion rot)
        {
            Vector3 f = bodyRot * Vector3.forward;
            f.y = 0f;
            if (f.sqrMagnitude < 1e-4f) f = Vector3.forward;
            f.Normalize();
            // Downed: head (root forward) to the carrier's left -> carrier forward = body right. Standing: body faces the carrier's right.
            Vector3 right = Vector3.Cross(Vector3.up, f);
            Vector3 cf = downed ? (mirror ? -right : right) : (mirror ? right : -right);
            rot = Quaternion.LookRotation(cf, Vector3.up);
            if (downed)
            {
                Vector3 pelvis = bodyRoot - f * LyingPelvisBack;
                pos = pelvis - rot * Side(FloorPelvisLocal, mirror);
            }
            else pos = bodyRoot - rot * Side(StandRootLocal, mirror);
            pos.y = bodyRoot.y;
        }

        /// <summary>
        /// Carrier root for lifting a body lying on the floor (downed or dead, any way round): <paramref name="pelvis"/> and the
        /// direction from it to the head (world). The body ends up in front of him, head on his left (right when mirrored).
        /// </summary>
        public static void CarrierSpotLying(Vector3 pelvis, Vector3 headDir, bool mirror, out Vector3 pos, out Quaternion rot)
        {
            headDir.y = 0f;
            if (headDir.sqrMagnitude < 1e-4f) headDir = Vector3.forward;
            headDir.Normalize();
            // Head to the carrier's left: his right = -head, forward = right x up... (left-handed: forward = Cross(right, up)).
            Vector3 right = mirror ? headDir : -headDir;
            Vector3 fwd = Vector3.Cross(right, Vector3.up);
            rot = Quaternion.LookRotation(fwd, Vector3.up);
            pos = pelvis - rot * Side(FloorPelvisLocal, mirror);
            pos.y = pelvis.y;
        }

        /// <summary>Side for a lying body: the spot closer to the carrier.</summary>
        public static bool PickMirrorLying(Vector3 carrierPos, Vector3 pelvis, Vector3 headDir)
        {
            CarrierSpotLying(pelvis, headDir, false, out var a, out _);
            CarrierSpotLying(pelvis, headDir, true, out var b, out _);
            Vector3 da = a - carrierPos, db = b - carrierPos;
            da.y = db.y = 0f;
            return db.sqrMagnitude < da.sqrMagnitude;
        }

        /// <summary>Which side the carrier would rather use (the spot closer to where he stands).</summary>
        public static bool PickMirror(Vector3 carrierPos, Vector3 bodyRoot, Quaternion bodyRot, bool downed)
        {
            CarrierSpot(bodyRoot, bodyRot, downed, false, out var a, out _);
            CarrierSpot(bodyRoot, bodyRot, downed, true, out var b, out _);
            Vector3 da = a - carrierPos, db = b - carrierPos;
            da.y = db.y = 0f;
            return db.sqrMagnitude < da.sqrMagnitude;
        }

        /// <summary>
        /// Root frame of the carried after the carry ends (put down / dropped), from the carrier's root.
        /// Lying: root = head end (pelvis <see cref="LyingPelvisBack"/> behind it); standing: facing the carrier's side.
        /// </summary>
        public static void EndFrame(Vector3 carrierPos, Quaternion carrierRot, bool mirror, bool standing, float forwardScale,
            out Vector3 pos, out Quaternion rot)
        {
            Vector3 side = carrierRot * (mirror ? Vector3.right : Vector3.left); // towards the carried's head
            if (standing)
            {
                Vector3 l = Side(PutStandLocal, mirror);
                l.z *= forwardScale;
                pos = carrierPos + carrierRot * l;
                rot = Quaternion.LookRotation(-side, Vector3.up);
            }
            else
            {
                Vector3 l = Side(PutPelvisLocal, mirror);
                l.z *= forwardScale;
                Vector3 pelvis = carrierPos + carrierRot * l;
                pos = pelvis + side * LyingPelvisBack;
                rot = Quaternion.LookRotation(side, Vector3.up);
            }
        }
    }

    /// <summary>
    /// One carry, shared by the carrier's and the carried's action layers on one machine. The game writes the phase,
    /// the time in it and the end frame every frame; the carrier's layer computes both bodies (the carried pose is
    /// expressed in the carrier's root space, so it rides on the carrier without any network jitter) and the
    /// carried's layer only maps that pose into its own root.
    /// </summary>
    public sealed class BodyCarryState
    {
        public BodyCarryPhase Phase;
        /// <summary>Seconds since the current phase began (server clock).</summary>
        public float Time;
        /// <summary>Head on the carrier's right instead of the left.</summary>
        public bool Mirror;
        /// <summary>The lift started from the floor (downed body) rather than standing.</summary>
        public bool FromFloor;
        /// <summary>The carried is awake (alive: hugs the neck) or barely (downed: limp arm, head lolls).</summary>
        public bool Conscious;
        /// <summary>Put down onto the feet (conscious) instead of the floor.</summary>
        public bool EndStanding;
        /// <summary>A dead player's body: completely limp, no look, no breathing; physics (ragdoll) once it is put down.</summary>
        public bool Dead;
        /// <summary>Root frame of the carried after the carry (world), see <see cref="BodyCarryTimeline.EndFrame"/>.</summary>
        public bool HasEnd;
        public Vector3 EndPos;
        public Quaternion EndRot = Quaternion.identity;
        /// <summary>Where the carried looks (world direction; zero = carrier's forward).</summary>
        public Vector3 CarriedLook;
        /// <summary>A third player treats the carried one: seconds into it (negative = nobody).</summary>
        public float HealT = -1f;

        // ---- written by the carrier's layer every frame
        internal BlockyCharacterActions Carrier, Carried;
        internal CharacterPose CarriedPose;   // carrier root space (final, mirrored)
        internal int CarriedFrame = -1;
        internal int TickFrame = -1;          // frame the carried was last ticked

        // ---- start snapshot of the carried (world root frame + its root-space pose)
        internal bool HasStart;
        internal Vector3 StartRootPos;
        internal Quaternion StartRootRot = Quaternion.identity;
        internal CharacterPose StartPose;

        public float Duration => BodyCarryTimeline.Duration(Phase, FromFloor, EndStanding);
        public bool Active => Phase != BodyCarryPhase.None;
    }
}
