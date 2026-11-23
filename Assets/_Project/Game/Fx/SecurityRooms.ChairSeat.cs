using HideAndSeek.Characters;
using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Sitting on the swivel chairs, on every client (state: <see cref="MatchManager.SecurityChairs"/>). The seated
    /// pose of the regular seats (SeatPoses, "Prosto") hangs on the chair's seat anchor, so the body sits down from where
    /// the player stood and stands up the same way, and turns with the chair. The sitter's look yaw turns the chair
    /// (mouse = swivel): the owner's root yaw is the chair yaw, the others follow it from the synced transform. The
    /// gas lift gives a little when somebody drops onto the seat. Runs before the character animator (100).
    /// </summary>
    [DefaultExecutionOrder(90)]
    public class SecurityChairDriver : MonoBehaviour
    {
        private SecurityRooms _rooms;
        private readonly NetPlayer[] _shown = new NetPlayer[SecurityRooms.ChairCount];
        private readonly bool[] _settle = new bool[SecurityRooms.ChairCount];

        /// <summary>Chair the local player sits on (-1 = none).</summary>
        public static int LocalChair { get; private set; } = -1;
        private static int _leftChair = -1;
        private static float _leftUntil;

        public static Vector3 StandOffset(SecurityRooms.Chair c) => c.Anchor.position + Flat(c.Anchor.forward) * 0.5f;

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward; }

        private void Awake() => _rooms = GetComponent<SecurityRooms>();

        private void OnDestroy()
        {
            // Map unloaded while somebody sat (round over): nobody may stay glued to a seat that is gone.
            for (int i = 0; i < _shown.Length; i++)
            {
                var p = _shown[i];
                _shown[i] = null;
                if (p == null) continue;
                if (p.Seat.Value < 0) (p.Actions as BlockyCharacterActions)?.SetSitting(false);
                if (!p.IsOwner) continue;
                p.Motor.Seated = false;
                var cc = p.GetComponent<CharacterController>();
                if (cc != null && p.Life.Value != LifeState.Dead) cc.enabled = true;
            }
            LocalChair = -1;
        }

        /// <summary>Owner: get up now (the server is told; the chair keeps the current turn).</summary>
        public static void LeaveLocal()
        {
            var me = NetPlayer.Local;
            var mm = MatchManager.Instance;
            var sr = SecurityRooms.Instance;
            if (LocalChair < 0 || me == null || sr == null) return;
            int i = LocalChair;
            mm?.ChairRequestRpc((byte)i, false, (short)Mathf.RoundToInt(Mathf.Repeat(me.transform.eulerAngles.y, 360f)));
            _leftChair = i;
            _leftUntil = Time.time + 1.5f;
            sr.GetComponent<SecurityChairDriver>()?.Release(i, me, true);
        }

        private void LateUpdate()
        {
            var mm = MatchManager.Instance;
            if (mm == null || _rooms == null) return;
            if (mm.IsServer) mm.ServerChairTick();
            var st = mm.SecurityChairs.Value;
            float dt = Time.deltaTime;
            var chairs = _rooms.Chairs;
            for (int i = 0; i < chairs.Count && i < SecurityRooms.ChairCount; i++)
            {
                var c = chairs[i];
                ulong id = st.Sitter(i);
                var p = id != ChairsState.Free ? NetPlayer.Find(id) : null;
                if (p != null && p.IsOwner && i == _leftChair && Time.time < _leftUntil) p = null;   // already got up, server not caught up yet
                if (p != null && !p.IsSpawned) p = null;
                if (p != _shown[i])
                {
                    if (_shown[i] != null) Release(i, _shown[i], false);
                    if (p != null) Engage(i, p);
                }

                float target = p != null ? p.transform.eulerAngles.y : st.Yaw(i) != short.MinValue ? st.Yaw(i) : c.Yaw;
                float rate = p == null ? 5f : _settle[i] ? 7f : 22f;
                c.ShownYaw = Mathf.LerpAngle(c.ShownYaw, target, 1f - Mathf.Exp(-rate * dt));
                if (_settle[i] && Mathf.Abs(Mathf.DeltaAngle(c.ShownYaw, target)) < 2f) _settle[i] = false;
                c.Upper.rotation = Quaternion.Euler(0f, c.ShownYaw, 0f);

                // Gas lift: a damped spring, pressed a little while somebody sits.
                float rest = p != null ? -0.012f : 0f;
                // Fixed small steps (a long frame, e.g. right after loading, must not blow the spring up).
                for (float left = Mathf.Min(dt, 0.1f); left > 1e-5f; left -= 0.01f)
                {
                    float h = Mathf.Min(0.01f, left);
                    c.DipVel += (-(c.Dip - rest) * 260f - c.DipVel * 16f) * h;
                    c.Dip = Mathf.Clamp(c.Dip + c.DipVel * h, -0.04f, 0.02f);
                }
                c.Upper.localPosition = new Vector3(0f, c.Dip - SecurityRooms.SeatDrop, 0f);

                if (p != null && p.IsOwner) OwnerTick(c, p);
            }
        }

        private void Engage(int i, NetPlayer p)
        {
            var c = _rooms.Chairs[i];
            _shown[i] = p;
            _settle[i] = true;
            c.DipVel = -0.45f;
            var act = p.Actions as BlockyCharacterActions;
            Vector3 from = p.Character != null ? p.Character.transform.position : p.transform.position;
            Quaternion fromRot = p.Character != null ? p.Character.transform.rotation : p.transform.rotation;
            if (p.IsOwner)
            {
                LocalChair = i;
                if (p.SitPoseVar.Value != 0) p.SitPoseVar.Value = 0;
                var cc = p.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                p.Motor.Seated = true;
                p.TeleportLocal(c.Anchor.position, p.transform.eulerAngles.y);
            }
            if (act != null)
            {
                act.SetSitSeat(c.Anchor, SecurityRooms.ChairShape);
                act.BeginSitFrom(from, fromRot);
                act.SetSitting(true);
            }
            Debug.Log($"[Chair] {p.Nick} sits on chair {i} (level {SecurityRooms.LevelName(c.Level)})");
        }

        private void Release(int i, NetPlayer p, bool local)
        {
            if (_shown[i] != p) return;
            _shown[i] = null;
            var c = _rooms.Chairs[i];
            c.DipVel = 0.3f;
            if (p == null) return;
            if (p.Seat.Value < 0) (p.Actions as BlockyCharacterActions)?.SetSitting(false);
            if (!p.IsOwner) return;
            if (LocalChair == i) LocalChair = -1;
            p.Motor.Seated = false;
            // Step off forward (the body stands up from the seat, the camera follows its eyes: no jump). Not when the
            // player was knocked down / killed or taken by an animation: those place the body themselves.
            if (p.Life.Value == LifeState.Alive && !p.Busy.Value)
            {
                var cc = p.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = true;
                p.TeleportLocal(StandOffset(c), p.transform.eulerAngles.y);
            }
            if (local) Debug.Log($"[Chair] {p.Nick} gets up from chair {i}");
        }

        /// <summary>Owner while seated: the body stays on the seat, the look pitch stays out of the own chest.</summary>
        private static void OwnerTick(SecurityRooms.Chair c, NetPlayer p)
        {
            Vector3 a = c.Anchor.position;
            if ((p.transform.position - a).sqrMagnitude > 0.0004f) p.Motor.Teleport(new Vector3(a.x, a.y, a.z), p.transform.eulerAngles.y);
            float pitch = Mathf.Clamp(p.Motor.LookPitch, -50f, 70f);
            if (!Mathf.Approximately(pitch, p.Motor.LookPitch)) p.Motor.SetLook(p.transform.eulerAngles.y, pitch);
        }
    }
}
