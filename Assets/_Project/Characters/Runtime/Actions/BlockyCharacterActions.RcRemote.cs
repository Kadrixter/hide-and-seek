using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>Timings (s) of smashing the RC transmitter; shared by third person, first person, sounds and the server.</summary>
    public static class RcRemoteTimeline
    {
        /// <summary>Raised in both hands and shaken (0 .. WindStart), swung up over the right shoulder (.. ThrowStart),
        /// slammed down (.. Release), the transmitter leaves the hand at <see cref="Release"/> and hits the floor at <see cref="Land"/>.</summary>
        public const float WindStart = 0.3f, ThrowStart = 0.5f, Release = 0.62f, Land = 0.72f;
        public const float End = 1.45f;

        public static float Seg(float t, float a, float b) => ActionTimeline.SmoothStep01(ActionTimeline.Remap01(t, a, b));

        /// <summary>Rotation that lays the transmitter flat on the floor, face up, top edge along <paramref name="yaw"/>.</summary>
        public static Quaternion FlatRot(float yaw) => Quaternion.Euler(0f, yaw, 0f) * Quaternion.LookRotation(Vector3.up, Vector3.forward);
    }

    /// <summary>
    /// The "Pilot RC" in third person: held in both hands in front of the belly when its slot is selected (face up to
    /// the eyes, fists round the handles), and the angry smash when its car got destroyed - raised and shaken, swung up
    /// over the right shoulder, slammed at the floor in front of the feet; the left hand lets go for balance, the body
    /// follows through. The flying transmitter is a world object (seen by the owner in first person too) until it hits
    /// the floor - then the game shows the wreck (RcRemoteModel.CreateWreck, the same for everybody).
    /// </summary>
    public partial class BlockyCharacterActions
    {
        bool remoteHeld, remoteSelected;
        float remoteThrowT = -1f;
        Vector3 remoteLandLocal;
        float remoteLandYaw;
        RcRemoteModel.Parts remoteWorld;
        Vector3 remoteFlyFrom;
        Quaternion remoteFlyRot;

        /// <summary>
        /// <paramref name="held"/>: the transmitter is in both hands (selected, standing, not driving);
        /// <paramref name="selected"/>: its slot is selected at all - then it stays in the hands while sitting down to
        /// steer and while getting up again (no hiding it in the pocket in between).
        /// </summary>
        public void SetRcRemoteHeld(bool held, bool selected)
        {
            remoteHeld = held;
            remoteSelected = selected;
        }

        /// <summary>True while the smash runs.</summary>
        public bool RemoteThrowing => remoteThrowT >= 0f;

        /// <summary>Starts the smash; <paramref name="landWorld"/> = floor point where it breaks, <paramref name="yaw"/> = heading of the wreck.</summary>
        public void PlayRemoteThrow(Vector3 landWorld, float yaw)
        {
            EnsureInit();
            remoteThrowT = 0f;
            remoteLandLocal = transform.InverseTransformPoint(landWorld);
            remoteLandYaw = yaw;
            DestroyRemoteWorld();
        }

        Vector3 RemoteLandWorld => transform.TransformPoint(remoteLandLocal);

        /// <summary>Item shown by the transmitter layers (None = not involved).</summary>
        HeldItem RemoteShownItem()
        {
            if (remoteThrowT >= 0f) return remoteThrowT < RcRemoteTimeline.Release ? HeldItem.RcRemote : HeldItem.None;
            if (remoteHeld && action == ActionKind.None && VisualLifeState == LifeState.Alive && !pathActive && sitW < 0.01f && proneW < 0.3f)
                return HeldItem.RcRemote;
            return HeldItem.None;
        }

        /// <summary>Both fists round the handles in front of the belly, the face tilted up to the eyes (follows the look a little).</summary>
        static void RemoteHoldHands(ref CharacterPose p, float lookPitch, float shake)
        {
            float look = Mathf.Clamp(lookPitch, -45f, 45f) * 0.35f;
            Quaternion rot = p.TorsoRoot * Quaternion.Euler(-look, 0f, 0f)
                             * Quaternion.LookRotation(new Vector3(0f, 0.62f, -0.78f), new Vector3(0f, 0.78f, 0.62f))
                             * Quaternion.Euler(0f, 0f, shake);
            Vector3 centre = p.TorsoPoint(new Vector3(0f, 0.3f, 0.36f));
            Vector3 right = centre - rot * RcRemoteModel.CenterLocal;
            Vector3 left = right + rot * RcRemoteModel.LeftGrip;
            p.SolveArm(0, right, TorsoDir(p, 1f, -0.9f, -0.4f));
            p.SolveArm(1, left, TorsoDir(p, -1f, -0.9f, -0.4f));
            p.PropR = Quaternion.Inverse(p.ForearmRoot(0)) * rot;
        }

        /// <summary>Layer step after the phone smash: the throw pose + the flying transmitter. Runs every frame.</summary>
        void ApplyRemoteThrow(ref CharacterPose p, float dt, bool canPose, float lookPitch)
        {
            TickRemoteWorld();
            if (remoteThrowT < 0f) return;
            float prev = remoteThrowT;
            remoteThrowT += dt;
            float t = remoteThrowT;
            if (t >= RcRemoteTimeline.End || !canPose)
            {
                remoteThrowT = -1f;
                DestroyRemoteWorld();
                return;
            }

            // Released: the world transmitter takes over from the hand.
            if (prev < RcRemoteTimeline.Release && t >= RcRemoteTimeline.Release)
            {
                var prop = props[(int)HeldItem.RcRemote];
                remoteFlyFrom = prop != null ? prop.transform.position : socketR.position;
                remoteFlyRot = prop != null ? prop.transform.rotation : socketR.rotation;
                DestroyRemoteWorld();
                remoteWorld = RcRemoteModel.Create(null, 0);
                remoteWorld.Root.SetPositionAndRotation(remoteFlyFrom, remoteFlyRot);
            }
            // It hits the floor: the intact one goes, the wreck (game side, same spot) takes over.
            if (prev < RcRemoteTimeline.Land && t >= RcRemoteTimeline.Land) DestroyRemoteWorld();

            float w = RcRemoteTimeline.Seg(t, 0f, 0.12f) * (1f - RcRemoteTimeline.Seg(t, RcRemoteTimeline.End - 0.5f, RcRemoteTimeline.End));
            var s = RemoteThrowPose(p, t, lookPitch);
            p = CharacterPose.Lerp(p, s, w);
            if (t < RcRemoteTimeline.Release) p.PropR = s.PropR;
        }

        CharacterPose RemoteThrowPose(in CharacterPose current, float t, float lookPitch)
        {
            float S(float a, float b) => RcRemoteTimeline.Seg(t, a, b);
            var q = current;
            float raise = S(0.04f, 0.22f), wind = S(RcRemoteTimeline.WindStart, RcRemoteTimeline.ThrowStart);
            float thr = S(RcRemoteTimeline.ThrowStart, RcRemoteTimeline.Release + 0.02f), follow = S(RcRemoteTimeline.Release, 0.85f);

            // Body: a short lean back on the wind-up, then the whole torso goes into the throw and recovers.
            q.Torso = Quaternion.Euler(4f * raise - 12f * wind + 40f * thr - 14f * follow, -14f * wind + 12f * thr, 4f * wind - 4f * thr);
            q.Head = Quaternion.Euler(10f * raise - 14f * wind + 26f * thr, -6f * wind + 6f * thr, 0f);
            q.PelvisPos = current.PelvisPos + new Vector3(0f, -0.06f * thr + 0.03f * follow, 0.04f * thr - 0.02f * follow);

            // Hold in front, lifted and shaken in both hands.
            float shake = t > 0.08f && t < RcRemoteTimeline.WindStart ? 9f * Mathf.Sin(t * 46f) * (1f - S(0.22f, RcRemoteTimeline.WindStart)) : 0f;
            var hold = q;
            RemoteHoldHands(ref hold, lookPitch * (1f - raise), shake);
            Quaternion holdRot = hold.ForearmRoot(0) * hold.PropR;
            Vector3 holdHand = hold.HandPos(0) + q.TorsoRoot * new Vector3(0f, 0.12f * raise, 0.02f * raise);

            // Swing up over the right shoulder (top edge back), then down hard at the floor in front.
            Vector3 up = q.TorsoPoint(new Vector3(0.3f, 1.02f, -0.1f));
            Vector3 down = q.TorsoPoint(new Vector3(0.16f, 0.08f, 0.55f));
            Vector3 after = q.TorsoPoint(new Vector3(0.26f, 0.0f, 0.36f));
            Vector3 hand = Vector3.Lerp(Vector3.Lerp(Vector3.Lerp(holdHand, up, wind), down, thr), after, follow);
            Quaternion windRot = q.TorsoRoot * Quaternion.LookRotation(new Vector3(0f, 0.85f, 0.52f), new Vector3(0.1f, 0.5f, -0.86f));
            Quaternion throwRot = q.TorsoRoot * Quaternion.LookRotation(new Vector3(0f, 0.55f, 0.83f), new Vector3(0f, -0.83f, 0.55f));
            Quaternion rot = Quaternion.Slerp(Quaternion.Slerp(holdRot, windRot, wind), throwRot, thr);
            q.SolveArm(0, hand, TorsoDir(q, 1f, -0.4f + 0.6f * wind * (1f - thr), -0.6f));
            q.PropR = Quaternion.Inverse(q.ForearmRoot(0)) * rot;

            // Left hand: on the left handle, lets go on the wind-up and swings out for balance.
            Vector3 leftHold = hold.HandPos(0) + q.TorsoRoot * new Vector3(0f, 0.12f * raise, 0.02f * raise) + holdRot * RcRemoteModel.LeftGrip;
            Vector3 balance = q.TorsoPoint(new Vector3(-0.42f, 0.38f + 0.12f * thr, 0.22f + 0.1f * thr));
            Vector3 relax = q.TorsoPoint(new Vector3(-0.3f, 0.1f, 0.12f));
            Vector3 left = Vector3.Lerp(Vector3.Lerp(leftHold, balance, S(RcRemoteTimeline.WindStart - 0.04f, RcRemoteTimeline.ThrowStart - 0.04f)), relax, S(0.8f, 1.15f));
            q.SolveArm(1, left, TorsoDir(q, -1f, -0.5f, -0.5f));
            return q;
        }

        /// <summary>The thrown transmitter flies down and smacks into the floor (spinning over its top edge).</summary>
        void TickRemoteWorld()
        {
            if (remoteWorld == null) return;
            float t = remoteThrowT;
            if (t < 0f) { DestroyRemoteWorld(); return; }
            float k = Mathf.Clamp01((t - RcRemoteTimeline.Release) / (RcRemoteTimeline.Land - RcRemoteTimeline.Release));
            Quaternion landRot = RcRemoteTimeline.FlatRot(remoteLandYaw);
            Vector3 landPivot = RemoteLandWorld + Vector3.up * 0.03f - landRot * RcRemoteModel.CenterLocal;
            Vector3 pos = Vector3.Lerp(remoteFlyFrom, landPivot, k * k);
            Quaternion spin = Quaternion.AngleAxis(-160f * k, remoteFlyRot * Vector3.right);
            Quaternion rot = Quaternion.Slerp(spin * remoteFlyRot, landRot, k * k);
            remoteWorld.Root.SetPositionAndRotation(pos, rot);
        }

        void DestroyRemoteWorld()
        {
            if (remoteWorld != null && remoteWorld.Root != null) Destroy(remoteWorld.Root.gameObject);
            remoteWorld = null;
        }
    }
}
