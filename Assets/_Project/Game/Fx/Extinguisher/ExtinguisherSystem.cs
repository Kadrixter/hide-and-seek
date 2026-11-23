using HideAndSeek.Characters;
using HideAndSeek.Game.Net;
using HideAndSeek.Map;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Client side of the security-room extinguishers, built at runtime in the game scene (no map rebuild): the wall
    /// bracket in every room (where the old static extinguisher stood) and one world copy per extinguisher that sits
    /// on its bracket, lies on the floor, falls out of the hands (tips over and clanks) or flies back to the bracket
    /// when hung up. Every frame it drives each player's hands / first-person arms from the server state
    /// (<see cref="MatchManager.Extinguishers"/>), the jet and cloud of the one being sprayed, the loose safety pin,
    /// and the foamed seekers (wiping animation, weapon put away, foam on the face, coughs) - ExtinguisherSystem.Players.cs.
    /// </summary>
    [DefaultExecutionOrder(310)]
    public partial class ExtinguisherSystem : MonoBehaviour
    {
        public static ExtinguisherSystem Instance { get; private set; }

        private enum Mode { Hook, Floor, Hand, Falling, Flying }

        private sealed class Copy
        {
            public ExtinguisherModel.Parts Parts;
            public Transform Bracket;
            public Mode Mode = Mode.Hook;
            public byte Where = 255;
            public ulong Holder = MatchManager.NoExtHolder;
            public float T, HandSince;
            public Vector3 FromPos;
            public Quaternion FromRot = Quaternion.identity;
            public int Level = -1;
            public ExtinguisherSpray Spray;
            public bool WasSpraying;
            public byte LastCharge = ExtState.Full;
        }

        private readonly Copy[] _copies = new Copy[3];

        public static void Ensure()
        {
            if (Instance != null) return;
            if (SecurityRooms.Instance == null) return;
            var go = new GameObject("ExtinguisherSystem");
            Instance = go.AddComponent<ExtinguisherSystem>();
            Instance.Build();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            foreach (var p in NetPlayer.All)
            {
                if (p == null) continue;
                (p.Actions as BlockyCharacterActions)?.ClearExtinguisher();
                (p.Actions as BlockyCharacterActions)?.SetFoamed(false, 0f, 0f);
                if (p.IsOwner && p.ViewModel is FirstPersonViewModel fpv)
                {
                    fpv.SetExtinguisher(false, Vector3.zero, Quaternion.identity, true, 1f, false);
                    fpv.SetFoamed(false, 0f, 0f);
                }
            }
        }

        private void Build()
        {
            for (int lv = 0; lv < 3; lv++)
            {
                var c = new Copy();
                var root = new GameObject("ExtBracket_L" + SecurityRooms.LevelName(lv)).transform;
                root.SetParent(transform, false);
                Vector3 spot = SecurityRooms.ExtinguisherSpot(lv);
                root.SetPositionAndRotation(new Vector3(SecurityRooms.ExtinguisherWallX, spot.y + SecurityRooms.ExtinguisherHang, spot.z), Quaternion.LookRotation(SecurityRooms.ExtinguisherFacing));
                c.Bracket = ExtinguisherModel.CreateBracket(root, 0);
                LevelLighting.SetLevelLayer(root, lv);
                c.Parts = ExtinguisherModel.Create(transform, 0);
                c.Parts.Root.name = "Extinguisher_L" + SecurityRooms.LevelName(lv);
                c.Spray = ExtinguisherSpray.Create(transform);
                HookPose(lv, out var hp, out var hr);
                c.Parts.Root.SetPositionAndRotation(hp, hr);
                SetLevel(c, lv);
                _copies[lv] = c;
            }
            Debug.Log("[Ext] extinguishers built in the 3 security rooms");
        }

        /// <summary>Model pose on the bracket of level <paramref name="lv"/>.</summary>
        public static void HookPose(int lv, out Vector3 pos, out Quaternion rot)
        {
            Vector3 spot = SecurityRooms.ExtinguisherSpot(lv);
            rot = Quaternion.LookRotation(SecurityRooms.ExtinguisherFacing);
            var s = ExtinguisherModel.Styles[ExtinguisherModel.GameStyle];
            pos = new Vector3(SecurityRooms.ExtinguisherWallX, spot.y + SecurityRooms.ExtinguisherHang, spot.z) + rot * Vector3.forward * (ExtinguisherModel.BracketDepth + s.TankR);
        }

        /// <summary>Model pose lying on its side on the floor.</summary>
        public static void FloorPose(in ExtState st, out Vector3 pos, out Quaternion rot)
        {
            var s = ExtinguisherModel.Styles[ExtinguisherModel.GameStyle];
            rot = Quaternion.Euler(0f, st.Yaw, 0f) * Quaternion.Euler(0f, 0f, 90f);
            pos = st.Pos + Vector3.up * (s.TankR + 0.006f);
        }

        /// <summary>Server: a free floor spot in front of <paramref name="feet"/> (at the feet when a wall is in the way).</summary>
        public static Vector3 FloorSpot(Vector3 feet, Vector3 fwd)
        {
            fwd.y = 0f;
            fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;
            int mask = ~((1 << 8) | (1 << 9));
            Vector3 chest = feet + Vector3.up * 0.5f;
            float ahead = 0.55f;
            if (Physics.SphereCast(chest, 0.12f, fwd, out var w, 0.9f, mask, QueryTriggerInteraction.Ignore) && w.collider.GetComponentInParent<NetPlayer>() == null)
                ahead = Mathf.Max(0.12f, w.distance - 0.4f);
            Vector3 p = feet + fwd * ahead;
            var hits = Physics.RaycastAll(p + Vector3.up * 1.0f, Vector3.down, 2.2f, mask, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            Vector3 floor = new Vector3(p.x, feet.y, p.z);
            foreach (var h in hits)
            {
                if (h.collider.GetComponentInParent<NetPlayer>() != null || h.distance >= best) continue;
                best = h.distance;
                floor = h.point;
            }
            return floor;
        }

        private static void SetLevel(Copy c, int lv)
        {
            if (lv == c.Level) return;
            c.Level = lv;
            ExtinguisherModel.SetRender(c.Parts, 0, ShadowCastingMode.On);
            LevelLighting.SetLevelLayer(c.Parts.Root, lv);
        }

        private void Update()
        {
            var mm = MatchManager.Instance;
            var list = mm != null ? mm.Extinguishers : null;
            int n = list != null ? list.Count : 0;
            float dt = Time.deltaTime;
            for (int i = 0; i < 3; i++)
            {
                var c = _copies[i];
                if (c == null) continue;
                var st = i < n ? list[i] : new ExtState { Where = ExtState.OnWall, Holder = MatchManager.NoExtHolder, Charge = ExtState.Full };
                Transition(i, c, st);
                PoseCopy(i, c, st, dt);
                ExtinguisherModel.SetPinIn(c.Parts, !st.Unlocked);
                ExtinguisherModel.SetCharge(c.Parts, st.Charge01);
                ExtinguisherModel.SetSqueeze(c.Parts, 0f);
            }
            UpdatePlayers(list, n);
        }

        private void Transition(int i, Copy c, in ExtState st)
        {
            if (st.Where == c.Where && st.Holder == c.Holder) return;
            bool first = c.Where == 255;
            byte was = c.Where;
            var prevHolder = NetPlayer.Find(c.Holder);
            c.Where = st.Where;
            c.Holder = st.Holder;
            c.T = 0f;
            if (st.Where == ExtState.InHands) { c.Mode = Mode.Hand; c.HandSince = Time.time; return; }
            var bca = prevHolder != null ? prevHolder.Actions as BlockyCharacterActions : null;
            bool fromHands = !first && was == ExtState.InHands;
            if (fromHands && bca != null && bca.ExtReleasePos != Vector3.zero)
            {
                c.FromPos = bca.ExtReleasePos;
                c.FromRot = bca.ExtReleaseRot;
            }
            else { c.FromPos = c.Parts.Root.position; c.FromRot = c.Parts.Root.rotation; }
            c.Mode = first || !fromHands ? (st.Where == ExtState.OnWall ? Mode.Hook : Mode.Floor) : st.Where == ExtState.OnWall ? Mode.Flying : Mode.Falling;
        }

        private void PoseCopy(int i, Copy c, in ExtState st, float dt)
        {
            c.T += dt;
            var root = c.Parts.Root;
            bool show = true;
            Vector3 pos;
            Quaternion rot;
            switch (c.Mode)
            {
                case Mode.Hand:
                {
                    // Stays where it was until the hand has it (then the hand copy takes over).
                    var holder = NetPlayer.Find(c.Holder);
                    var bca = holder != null ? holder.Actions as BlockyCharacterActions : null;
                    show = (bca == null || !bca.ExtInHand) && Time.time - c.HandSince < 1.2f;
                    pos = root.position;
                    rot = root.rotation;
                    break;
                }
                case Mode.Flying:
                {
                    HookPose(i, out var hp, out var hr);
                    float k = ActionTimeline.SmoothStep01(Mathf.Clamp01(c.T / 0.32f));
                    pos = Vector3.Lerp(c.FromPos, hp, k) + Vector3.up * 0.06f * Mathf.Sin(Mathf.PI * k);
                    rot = Quaternion.Slerp(c.FromRot, hr, k);
                    if (c.T >= 0.32f) { c.Mode = Mode.Hook; ExtinguisherSounds.Click(hp); }
                    break;
                }
                case Mode.Falling:
                {
                    FloorPose(st, out var fp, out var fr);
                    pos = FallPose(c, fp, fr, out rot);
                    break;
                }
                case Mode.Floor:
                    FloorPose(st, out pos, out rot);
                    break;
                default:
                    HookPose(i, out pos, out rot);
                    break;
            }
            if (root.gameObject.activeSelf != show) root.gameObject.SetActive(show);
            if (!show) return;
            root.SetPositionAndRotation(pos, rot);
            ExtinguisherModel.RestHose(c.Parts);
            SetLevel(c, MapInfo.LevelOf(pos.y - 0.3f));
        }

        /// <summary>Out of the hands: drops (gravity), lands on its foot, tips over onto its side with a clank and a small bounce.</summary>
        private static Vector3 FallPose(Copy c, Vector3 lie, Quaternion lieRot, out Quaternion rot)
        {
            const float Drop = 0.3f, Tip = 0.32f;
            float t = c.T;
            Quaternion upright = Quaternion.Euler(0f, lieRot.eulerAngles.y, 0f);
            Vector3 stand = new Vector3(lie.x, lie.y - ExtinguisherModel.Styles[ExtinguisherModel.GameStyle].TankR - 0.006f, lie.z);
            if (t < Drop)
            {
                float k = t / Drop;
                rot = Quaternion.Slerp(c.FromRot, upright, k);
                return Vector3.Lerp(c.FromPos, stand, k * k);
            }
            if (c.T - Time.deltaTime < Drop) ExtinguisherSounds.Clank(stand, 0.6f);
            float u = Mathf.Clamp01((t - Drop) / Tip);
            float e = u * u;
            rot = Quaternion.Slerp(upright, lieRot, e);
            if (u >= 1f && c.T - Time.deltaTime < Drop + Tip)
            {
                ExtinguisherSounds.Clank(lie, 1f);
                FoamFlakes.Burst(lie, Vector3.up, 8, 0.8f, 0.07f);
            }
            float bounce = u >= 1f ? Mathf.Abs(Mathf.Sin((t - Drop - Tip) * 18f)) * Mathf.Exp(-(t - Drop - Tip) * 9f) * 0.025f : 0f;
            if (t > Drop + Tip + 0.6f) c.Mode = Mode.Floor;
            return Vector3.Lerp(stand, lie, e) + Vector3.up * bounce;
        }
    }
}
