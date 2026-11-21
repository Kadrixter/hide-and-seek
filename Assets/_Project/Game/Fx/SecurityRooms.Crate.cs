using HideAndSeek.Audio;
using HideAndSeek.Characters;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>State of the crate in front of a security room door (2 bits per level in <see cref="MatchManager.SecurityCrates"/>).</summary>
    public enum CrateState : byte { Closed = 0, Open = 1, Broken = 2 }

    /// <summary>What a player does with the crate.</summary>
    public enum CrateAction : byte
    {
        OpenOutside = 0,   // hider outside: pushes it aside (walks along, west)
        CloseOutside = 1,  // hider outside: pushes it back in front of the door (walks along, east)
        OpenInside = 2,    // hider inside the doorway: shoves it away
        CloseInside = 3,   // hider inside the doorway: drags it back in front of the door
        Saw = 4,           // seeker with the chainsaw: cuts it apart (the door stays open until the round ends)
    }

    /// <summary>
    /// The crate blocking a security room door: closed / pushed aside / cut to pieces, synced by the server. Every client
    /// animates the moves from <see cref="BlockyCharacterActions.CrateProgress"/> (same timing as the pusher's body);
    /// a client that joins later (or missed the animation) just snaps to the synced state, debris already on the floor.
    /// </summary>
    public partial class SecurityRooms
    {
        public const float CratePushTime = BlockyCharacterActions.CratePushTime;
        public const float CrateShift = 1.65f;
        public const float PushYaw = 270f;
        private static readonly Vector3 CrateSize = new Vector3(1.45f, 1.5f, 1.05f);

        private readonly float[] _crateVis = new float[Levels];          // 0 closed position .. 1 pushed aside
        private readonly bool[] _crateBroken = new bool[Levels];
        private readonly float[] _animStart = { -1f, -1f, -1f };
        private readonly CrateAction[] _animAct = new CrateAction[Levels];
        private readonly float[] _animFrom = new float[Levels];
        private readonly int[] _seenState = { -1, -1, -1 };
        private readonly float[] _stateSince = new float[Levels];

        public static CrateState StateOf(byte mask, int lv) => (CrateState)((mask >> (lv * 2)) & 3);
        public static byte WithState(byte mask, int lv, CrateState s) => (byte)((mask & ~(3 << (lv * 2))) | ((int)s << (lv * 2)));
        private static CrateState SyncedState(int lv) => MatchManager.Instance != null ? StateOf(MatchManager.Instance.SecurityCrates.Value, lv) : CrateState.Closed;

        // ================================================================ geometry

        private static Vector3 CrateClosed(int lv) => new Vector3((DoorA + DoorB) * 0.5f, FloorY(lv) + CrateSize.y * 0.5f, Z0 - Wall * 0.5f - CrateSize.z * 0.5f - 0.03f);
        private static Vector3 CrateOpen(int lv) => CrateClosed(lv) + Vector3.left * CrateShift;
        private static Vector3 Feet(Vector3 crateCentre) => crateCentre + Vector3.down * CrateSize.y * 0.5f;

        /// <summary>Where the actor stands at the start of <paramref name="act"/>, and his yaw.</summary>
        public static Vector3 ActionStand(int lv, CrateAction act, out float yaw)
        {
            float doorX = (DoorA + DoorB) * 0.5f;
            switch (act)
            {
                case CrateAction.CloseOutside: yaw = 90f; return Feet(CrateOpen(lv)) + new Vector3(-(CrateSize.x * 0.5f + 0.42f), 0f, 0f);
                case CrateAction.OpenInside: yaw = 180f; return new Vector3(doorX, FloorY(lv), Z0 + 0.48f);
                case CrateAction.CloseInside: yaw = 238f; return new Vector3(doorX - 0.22f, FloorY(lv), Z0 + 0.42f);
                case CrateAction.Saw: yaw = 0f; return SawStand(lv, SyncedState(lv) == CrateState.Open);
                default: yaw = PushYaw; return Feet(CrateClosed(lv)) + new Vector3(CrateSize.x * 0.5f + 0.42f, 0f, 0f);
            }
        }

        /// <summary>Seeker's stand in front of the crate (south side, facing it) wherever the crate is now.</summary>
        public static Vector3 SawStand(int lv, bool open) => Feet(open ? CrateOpen(lv) : CrateClosed(lv)) + new Vector3(0.05f, 0f, -(CrateSize.z * 0.5f + 0.78f));

        /// <summary>Old API (tests): stand and end of the outside push.</summary>
        public static Vector3 PushStand(int lv) => ActionStand(lv, CrateAction.OpenOutside, out _);
        public static Vector3 PushEnd(int lv) => PushStand(lv) + Vector3.left * CrateShift;

        /// <summary>Does the actor walk along with the crate (outside pushes), and where to.</summary>
        public static bool WalksAlong(CrateAction act, int lv, out Vector3 from, out Vector3 to)
        {
            from = ActionStand(lv, act, out _);
            to = from + (act == CrateAction.OpenOutside ? Vector3.left : Vector3.right) * CrateShift;
            return act == CrateAction.OpenOutside || act == CrateAction.CloseOutside;
        }

        public static PushMode ModeOf(CrateAction act) =>
            act == CrateAction.OpenInside ? PushMode.Brace : act == CrateAction.CloseInside ? PushMode.Pull : PushMode.Walk;

        public static bool IsInside(Vector3 feet) => feet.x > X0 && feet.x < X1 && feet.z > Z0 && feet.z < Z1;

        public bool CrateOpenNow(int lv) => _crateVis[lv] >= 0.999f || _crateBroken[lv];
        public bool CrateBrokenNow(int lv) => _crateBroken[lv];

        /// <summary>Test helper: forget what this client showed (like a player whose room was just built); the synced state is applied again.</summary>
        public void TestForgetCrate(int lv)
        {
            if (_crateBroken[lv]) RestoreCrate(lv);
            _crateVis[lv] = 0f;
            PlaceCrate(lv, 0f, 0f);
            _seenState[lv] = -1;
        }

        public Vector3 CratePosition(int lv) => _crates[lv] != null ? _crates[lv].position : default;

        // ================================================================ prompt / server checks

        /// <summary>
        /// The crate action the local player can do now (prompt): hiders push / pull, the seeker with the chainsaw cuts.
        /// </summary>
        public bool TryFindCrateAction(Vector3 feet, Vector3 eye, Vector3 fwd, bool seeker, bool sawOut, out CrateAction act, out int level, out Vector3 world, out float dist)
        {
            act = default; level = -1; world = default; dist = float.MaxValue;
            for (int lv = 0; lv < Levels; lv++)
            {
                if (_crates[lv] == null || Mathf.Abs(feet.y - FloorY(lv)) > 1.2f || _animStart[lv] >= 0f) continue;
                var st = SyncedState(lv);
                if (st == CrateState.Broken || !Allowed(lv, st, feet, seeker, sawOut, out var a)) continue;
                Vector3 c = _crates[lv].position;
                Vector3 look = IsInside(feet) ? new Vector3((DoorA + DoorB) * 0.5f, FloorY(lv) + 1.0f, Z0) : c;
                float d = Vector3.Distance(eye, look);
                if (d >= dist || Vector3.Angle(fwd, look - eye) > (IsInside(feet) ? 55f : 42f)) continue;
                act = a; level = lv; world = c + Vector3.up * 0.3f; dist = d;
            }
            return level >= 0;
        }

        private bool Allowed(int lv, CrateState st, Vector3 feet, bool seeker, bool sawOut, out CrateAction act)
        {
            act = CrateAction.OpenOutside;
            bool inside = IsInside(feet);
            Vector3 c = Feet(st == CrateState.Open ? CrateOpen(lv) : CrateClosed(lv));
            if (seeker)
            {
                act = CrateAction.Saw;
                return sawOut && !inside && Flat(feet, c) < 2.6f && feet.z < c.z;
            }
            if (inside)
            {
                act = st == CrateState.Closed ? CrateAction.OpenInside : CrateAction.CloseInside;
                return Flat(feet, ActionStand(lv, act, out _)) < 1.5f;
            }
            act = st == CrateState.Closed ? CrateAction.OpenOutside : CrateAction.CloseOutside;
            return Flat(feet, c) < 2.5f;
        }

        private static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        /// <summary>Server: <paramref name="act"/> on level <paramref name="lv"/> is allowed for a player standing at <paramref name="feet"/>.</summary>
        public bool ServerCanAct(int lv, CrateAction act, Vector3 feet, bool seeker, bool sawOut)
        {
            if (lv < 0 || lv >= Levels || Mathf.Abs(feet.y - FloorY(lv)) > 1.3f) return false;
            var st = SyncedState(lv);
            if (st == CrateState.Broken) return false;
            if (!Allowed(lv, st, feet + Vector3.zero, seeker, sawOut, out var a)) return false;
            return a == act;
        }

        /// <summary>Server: somebody other than <paramref name="actor"/> stands where the crate will slide.</summary>
        public static bool PathBlocked(int lv, CrateAction act, NetPlayer actor)
        {
            bool closing = act == CrateAction.CloseOutside || act == CrateAction.CloseInside;
            Vector3 a = closing ? CrateOpen(lv) : CrateClosed(lv), b = closing ? CrateClosed(lv) : CrateOpen(lv);
            float minX = Mathf.Min(a.x, b.x) - CrateSize.x * 0.5f - 0.3f, maxX = Mathf.Max(a.x, b.x) + CrateSize.x * 0.5f + 0.3f;
            float minZ = a.z - CrateSize.z * 0.5f - 0.3f, maxZ = a.z + CrateSize.z * 0.5f + 0.3f;
            foreach (var p in NetPlayer.All)
            {
                if (p == null || p == actor || !p.IsSpawned) continue;
                Vector3 v = p.transform.position;
                if (Mathf.Abs(v.y - FloorY(lv)) > 1.2f) continue;
                if (v.x > minX && v.x < maxX && v.z > minZ && v.z < maxZ) return true;
            }
            return false;
        }

        // ================================================================ animation (every client)

        /// <summary>Every client: <paramref name="act"/> starts now on level <paramref name="lv"/>.</summary>
        public void BeginCrateAction(int lv, CrateAction act)
        {
            if (lv < 0 || lv >= Levels || _crates[lv] == null || _crateBroken[lv]) return;
            _animStart[lv] = Time.time;
            _animAct[lv] = act;
            _animFrom[lv] = _crateVis[lv];
            if (act == CrateAction.Saw) BeginSawFx(lv);
            else if (_scrapeClip != null) GameAudio.PlayClipAt(_scrapeClip, _crates[lv].position, 1f, 26f);
        }

        private void UpdateCrates()
        {
            for (int lv = 0; lv < Levels; lv++)
            {
                if (_crates[lv] == null) continue;
                var st = SyncedState(lv);
                if ((int)st != _seenState[lv]) { _seenState[lv] = (int)st; _stateSince[lv] = Time.time; }

                if (_animStart[lv] >= 0f)
                {
                    float t = Time.time - _animStart[lv];
                    if (_animAct[lv] == CrateAction.Saw)
                    {
                        UpdateSawFx(lv, t);
                        if (t >= BlockyCharacterActions.CrateSawBreakTime && !_crateBroken[lv]) BreakCrate(lv, true);
                        if (t >= BlockyCharacterActions.CrateSawTime) _animStart[lv] = -1f;
                        continue;
                    }
                    bool opening = _animAct[lv] == CrateAction.OpenOutside || _animAct[lv] == CrateAction.OpenInside;
                    float k = BlockyCharacterActions.CrateProgress(t);
                    float slide = BlockyCharacterActions.CrateSlide(t);
                    _crateVis[lv] = Mathf.Lerp(_animFrom[lv], opening ? 1f : 0f, k);
                    PlaceCrate(lv, slide, t);
                    if (t >= CratePushTime) _animStart[lv] = -1f;
                    continue;
                }

                // No animation running: follow the synced state (after a short grace, so a state that arrives just
                // before the animation's RPC does not make the crate jump).
                if (Time.time - _stateSince[lv] < 0.6f) continue;
                if (st == CrateState.Broken && !_crateBroken[lv]) BreakCrate(lv, false);
                else if (st != CrateState.Broken && _crateBroken[lv]) RestoreCrate(lv);
                float target = st == CrateState.Open ? 1f : 0f;
                if (!_crateBroken[lv] && Mathf.Abs(_crateVis[lv] - target) > 1e-4f)
                {
                    _crateVis[lv] = target;
                    PlaceCrate(lv, 0f, 0f);
                }
            }
        }

        /// <summary>Crate transform from its progress; while sliding it yaws and tips a little (one edge catches the floor).</summary>
        private void PlaceCrate(int lv, float slide, float t)
        {
            Vector3 p = Vector3.Lerp(CrateClosed(lv), CrateOpen(lv), _crateVis[lv]);
            float yaw = slide * (1.8f * Mathf.Sin(t * 7.3f) + 0.9f * Mathf.Sin(t * 17f));
            float tip = slide * 1.2f * (0.6f + 0.4f * Mathf.Sin(t * 23f));
            float hop = slide * 0.006f * Mathf.Abs(Mathf.Sin(t * 31f));
            _crates[lv].SetPositionAndRotation(p + Vector3.up * hop, Quaternion.Euler(0f, yaw, tip));
        }
    }
}
