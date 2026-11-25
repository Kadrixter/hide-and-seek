using HideAndSeek.Game.MiniGames.Pool;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// The players' cues at the pool table, on every peer (seat A: the "Points" cue, seat B: the ebony one). The player
    /// to shoot walks to the spot behind the cue ball, goes down into the stance and lays the cue on the shot line
    /// (tip a centimetre off the ball, butt raised just enough to clear the rail, pulled back with the power); the
    /// aim of the others comes smoothed from <see cref="NetPlayer.PoolAim"/>. A shot (<see cref="OnShotStarted"/>, or
    /// at once for the own release) drives the tip through the ball, holds the follow-through, then the player stands
    /// up. Everybody else holds the cue upright at the right side. Both fists follow the cue (TickSeat -> pose).
    /// </summary>
    public sealed partial class PoolTable
    {
        /// <summary>Tip to ball at address; the longest backswing (metres).</summary>
        const float TipGap = 0.012f, MaxPull = 0.22f;
        /// <summary>Where the right fist holds the cue (from the butt end): in the stance / upright at the side.</summary>
        const float GripZ = 0.5f, HoldZ = 1.0f;
        /// <summary>Stroke timeline: follow-through held until, stand up at, done at (seconds).</summary>
        const float StrokeHold = 0.8f, StrokeUp = 1.3f, StrokeEnd = 1.9f;

        sealed class SeatView
        {
            public ulong Player = ulong.MaxValue;
            public Transform Cue;
            // Shown aim (own: live, others: smoothed network values).
            public float Angle, Pull;
            public Vector2 Spin, Hand;
            public byte HandMode;
            public bool Fresh = true;
            public Vector2 AimCue;
            public bool AimValid;
            // Body.
            public float Bend, Walk;
            public Vector3 LastRoot;
            public bool HasRoot, PoseValid;
            public Vector3 Grip, Bridge, Look;
            // Stroke (StrokeT < 0 = none). Wait = own stroke sent, the server has not started the shot yet.
            public float StrokeT = -1f, StrokeAngle, StrokePull, StrokePower;
            public Vector2 StrokeCue, StrokeSpin;
            public bool StrokeLocal, StrokeWait;

            public void Reset(ulong player)
            {
                Player = player;
                Fresh = true;
                AimValid = HasRoot = PoseValid = false;
                Bend = Walk = Pull = 0f;
                Spin = Vector2.zero;
                HandMode = 0;
                StrokeT = -1f;
                StrokeLocal = StrokeWait = false;
            }
        }

        readonly SeatView[] _view = { new SeatView(), new SeatView() };
        /// <summary>Seat that made the shot being replayed (-1 = none yet).</summary>
        int _rollSeat = -1;

        partial void TickPlay(float dt)
        {
            TickLocalFallback();
            TickOwnRoot(dt);
            for (int s = 0; s < 2; s++) TickSeat(s, dt);
            HeldCuePos = HandBall(out var held) ? held : (Vector2?)null;
            TickLocalAim(dt);
            TickCursor();
            TickCamera(dt);
            TickGuide();
            TickHud();
        }

        partial void OnShotStarted(int seat, PoolShot shot)
        {
            if (seat < 0 || seat > 1) return;
            _rollSeat = seat;
            if (seat == LocalSeat) LocalShotStarted();
            var v = _view[seat];
            if (v.StrokeT >= 0f && v.StrokeLocal)
            {
                v.StrokeWait = false; // own stroke already on its way: the follow-through goes on now
                return;
            }
            v.StrokeT = 0f;
            v.StrokeLocal = v.StrokeWait = false;
            v.StrokeAngle = shot.Angle;
            v.StrokePower = Mathf.Clamp01(shot.Power);
            v.StrokeSpin = new Vector2(shot.SpinX, shot.SpinY);
            v.StrokePull = Mathf.Max(v.Pull, 0.2f + 0.5f * v.StrokePower);
            v.StrokeCue = ShownPos[0]; // the replay has just put the balls where the server struck them
        }

        /// <summary>Own release: the stroke starts at once (the tip waits on the ball until the server starts the shot).</summary>
        void StartLocalStroke(int seat, Vector2 cue, float angle, float pull, float power, Vector2 spin)
        {
            var v = _view[seat];
            v.StrokeT = 0f;
            v.StrokeLocal = v.StrokeWait = true;
            v.StrokeCue = cue;
            v.StrokeAngle = angle;
            v.StrokePull = pull;
            v.StrokePower = power;
            v.StrokeSpin = spin;
        }

        /// <summary>The own shot was refused (or never came): the cue goes back to the address.</summary>
        void CancelLocalStroke(int seat)
        {
            if (seat < 0) return;
            var v = _view[seat];
            if (v.StrokeLocal) v.StrokeT = -1f;
            v.StrokeLocal = v.StrokeWait = false;
        }

        /// <summary>Ball in hand is being used by the seat to shoot.</summary>
        bool HandActive(int seat) => Playing && State.BallInHand && State.Turn == seat;

        /// <summary>Cue ball a seat aims from (in hand: where the shooter holds / put it).</summary>
        Vector2 SeatCue(int seat, SeatView v) => HandActive(seat) && v.HandMode >= 1 ? v.Hand : State.Pos[0];

        void TickSeat(int s, float dt)
        {
            var v = _view[s];
            ulong id = SeatPlayer(s);
            if (id != v.Player) v.Reset(id);
            var pl = id == PoolState.Nobody ? null : NetPlayer.Find(id);
            if (pl == null)
            {
                if (v.Cue != null && v.Cue.gameObject.activeSelf) v.Cue.gameObject.SetActive(false);
                v.PoseValid = false;
                return;
            }
            if (v.Cue == null && Model != null && Model.Kit != null && Model.Kit.Template != null)
                v.Cue = CueModel.Build(transform, Model.Kit.Template, s == 0 ? 1 : 3);
            if (v.Cue != null && !v.Cue.gameObject.activeSelf) v.Cue.gameObject.SetActive(true);
            UpdateShownAim(s, v, pl, dt);

            if (v.StrokeT >= 0f)
            {
                v.StrokeT += dt;
                // Own stroke waiting for the server: over once the shot started or was given up (no endless hold).
                if (v.StrokeWait && v.StrokeLocal && !_awaitShot) v.StrokeWait = false;
                if (v.StrokeWait) v.StrokeT = Mathf.Min(v.StrokeT, StrokeContactT(v));
                if (v.StrokeT > StrokeEnd)
                {
                    v.StrokeT = -1f;
                    v.StrokeLocal = v.StrokeWait = false;
                    v.AimValid = false;
                }
            }
            bool stroking = v.StrokeT >= 0f;
            bool aiming = !stroking && Playing && State.Turn == s && !Rolling && !(HandActive(s) && v.HandMode == 1);

            Vector2 cue, spin;
            float angle, tip;
            if (stroking) { cue = v.StrokeCue; angle = v.StrokeAngle; tip = StrokeTip(v); spin = v.StrokeSpin; }
            else
            {
                cue = SeatCue(s, v);
                angle = v.Angle;
                tip = TipGap + Mathf.Clamp01(v.Pull) * MaxPull;
                spin = v.Spin;
                if (aiming) { v.AimCue = cue; v.AimValid = true; }
            }
            Address(cue, angle, out var stand, out float bridgeLen, out bool rail, out float elev);

            // Body: down into the stance once at the spot (or through the stroke), walking speed from the root.
            Transform pt = pl.transform;
            Vector3 rp = pt.position;
            float bendTo = stroking ? (v.StrokeT < StrokeUp ? 1f : 0f) : aiming && (ToTable(rp) - stand).sqrMagnitude < 0.09f ? 1f : 0f;
            v.Bend = Mathf.MoveTowards(v.Bend, bendTo, dt / (bendTo > v.Bend ? 0.5f : 0.6f));
            if (v.HasRoot && dt > 1e-5f)
            {
                Vector3 step = rp - v.LastRoot;
                step.y = 0f;
                float sp = step.magnitude / dt;
                if (sp > 6f) sp = 0f; // teleport
                v.Walk = Mathf.MoveTowards(v.Walk, Mathf.Clamp01(sp / 1.1f), dt * 5f);
            }
            v.LastRoot = rp;
            v.HasRoot = true;

            // Cue: upright at the right side <-> on the shot line, by the bend.
            float b = Mathf.SmoothStep(0f, 1f, v.Bend);
            Vector3 heldButt = transform.InverseTransformPoint(pt.TransformPoint(new Vector3(0.46f, 0.01f, 0.2f)));
            Vector3 heldAxis = transform.InverseTransformDirection(pt.TransformDirection(new Vector3(-0.04f, 1f, 0.03f).normalized));
            Quaternion heldRot = Quaternion.LookRotation(heldAxis, transform.InverseTransformDirection(pt.forward));
            CuePose(cue, angle, spin, elev, tip, out var aimButt, out var aimRot);
            Vector3 butt = Vector3.Lerp(heldButt, aimButt, b);
            Quaternion rot = Quaternion.Slerp(heldRot, aimRot, b);
            if (v.Cue != null) v.Cue.SetLocalPositionAndRotation(butt, rot);
            Vector3 along = rot * Vector3.forward;
            v.Grip = transform.TransformPoint(butt + along * Mathf.Lerp(HoldZ, GripZ, b));
            v.Bridge = transform.TransformPoint(BridgeFist(cue, angle, elev, bridgeLen, rail));
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            bool shooter = stroking || aiming;
            v.Look = shooter && !(stroking && v.StrokeT > 0.35f) ? ToWorld(cue + dir * 0.45f, 0f) : ToWorld(Rolling ? ShownPos[0] : SeatCue(State.Turn, _view[State.Turn & 1]));
            v.PoseValid = true;
        }

        void UpdateShownAim(int s, SeatView v, NetPlayer pl, float dt)
        {
            if (pl.IsOwner)
            {
                v.Angle = _aimAngle;
                v.Pull = _dragging ? _pull : 0f;
                v.Spin = _spin;
                v.Hand = _handPos;
                v.HandMode = _handMode;
                v.Fresh = false;
                return;
            }
            Vector4 a = pl.PoolAim.Value;
            Vector3 h = pl.PoolHand.Value;
            float k = v.Fresh ? 1f : 1f - Mathf.Exp(-dt * 12f);
            v.Angle += Mathf.DeltaAngle(v.Angle * Mathf.Rad2Deg, a.x * Mathf.Rad2Deg) * Mathf.Deg2Rad * k;
            v.Pull = Mathf.Lerp(v.Pull, Mathf.Clamp01(a.y), k);
            v.Spin = Vector2.Lerp(v.Spin, new Vector2(a.z, a.w), k);
            byte mode = (byte)Mathf.Clamp(Mathf.RoundToInt(h.z), 0, 2);
            var hp = new Vector2(h.x, h.y);
            v.Hand = v.Fresh || mode != v.HandMode || mode == 2 ? hp : Vector2.Lerp(v.Hand, hp, k);
            v.HandMode = mode;
            v.Fresh = false;
        }
    }
}
