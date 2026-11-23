using System.Collections.Generic;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Nobody may stand in front of the SNAKE player and cover the screen.
    /// <para>Other peers: while somebody plays, an invisible box (Ignore Raycast layer) in front of the cabinet blocks
    /// the local CharacterController (the zone covers the player's spot, 0.7 m to each side, 0.65 m behind and up
    /// to the cabinet). A local player who already stands inside when the game starts is slid out first.</para>
    /// <para>The player at the cabinet: other players whose capsule crosses any line from the camera to the screen
    /// are not drawn (renderers forced off) until they move away - the screen is always fully visible.</para>
    /// </summary>
    public sealed class ArcadeKeepOut : MonoBehaviour
    {
        /// <summary>Zone in the cabinet's frame around its stand spot (narrow enough not to reach the other cabinet's player).</summary>
        public const float HalfWidth = 0.45f, Behind = 0.65f, Front = 0.65f, Height = 2.2f;

        /// <summary>Tests: let the local player stand inside the zone (to check the hiding on the player's side).</summary>
        public static bool DisablePush;
        /// <summary>Tests: draw occluding players anyway (to compare with and without the hiding).</summary>
        public static bool DisableHide;

        /// <summary>Cabinet index (set by <see cref="LobbyArcade"/> right after AddComponent).</summary>
        public int cab;

        private static readonly ArcadeKeepOut[] All = new ArcadeKeepOut[ArcadeCabs.Count];
        public static ArcadeKeepOut Of(int i) => i >= 0 && i < All.Length ? All[i] : null;
        /// <summary>First cabinet (older tests).</summary>
        public static ArcadeKeepOut Instance => Of(0);
        /// <summary>Player <paramref name="p"/> is not drawn because it covers somebody's SNAKE screen.</summary>
        public static bool AnyHides(NetPlayer p)
        {
            foreach (var k in All) if (k != null && k.IsHidden(p)) return true;
            return false;
        }

        private BoxCollider _box;
        private bool _sliding;
        private Vector3 _slideLocal;
        private int _slideTries;
        private readonly Dictionary<NetPlayer, Renderer[]> _hidden = new();

        public bool BlockerOn => _box != null && _box.enabled;
        public bool IsHidden(NetPlayer p) => _hidden.ContainsKey(p);

        private void Start()
        {
            if (cab >= 0 && cab < All.Length) All[cab] = this;
            var go = new GameObject("ArcadeKeepOut");
            go.layer = 2; // Ignore Raycast: blocks walking, not prompts / shots / flashlight
            go.transform.SetParent(transform, true);
            Vector3 stand = ArcadeCabs.StandLocal;
            go.transform.SetPositionAndRotation(ArcadeCabs.World(cab, new Vector3(stand.x, Height * 0.5f, stand.z + (Front - Behind) * 0.5f)), ArcadeCabs.Rotation(cab));
            _box = go.AddComponent<BoxCollider>();
            Vector3 ls = go.transform.lossyScale;
            _box.size = new Vector3(HalfWidth * 2f / Mathf.Max(0.0001f, ls.x), Height / Mathf.Max(0.0001f, ls.y), (Behind + Front) / Mathf.Max(0.0001f, ls.z));
            _box.enabled = false;
        }

        private void OnDestroy()
        {
            ShowAll();
            if (cab >= 0 && cab < All.Length && All[cab] == this) All[cab] = null;
        }

        private void LateUpdate()
        {
            if (_box == null) return;
            var mm = MatchManager.Instance;
            var me = NetPlayer.Local;
            ulong player = mm != null ? mm.ArcadePlayerOf(cab) : ulong.MaxValue;
            bool someone = player != ulong.MaxValue && me != null;
            bool iPlay = someone && player == me.NetworkObjectId;
            bool iPlayOther = me != null && mm != null && !iPlay && mm.ArcadeCabOf(me.NetworkObjectId) >= 0;

            // ---- other peers: blocker + slide out (not the player at the other cabinet - it stands still next to us)
            bool block = someone && !iPlay && !iPlayOther;
            if (block && !DisablePush)
            {
                var cc = me.GetComponent<CharacterController>();
                Vector3 push = Vector3.zero;
                // Start sliding when overlapping, keep going until 6 cm clear (no flicker at the edge).
                bool inside = cc != null && cc.enabled && Overlaps(cab, me.transform.position, cc.radius + (_sliding ? 0.06f : 0f), out push);
                if (inside != _sliding)
                {
                    Debug.Log($"[ArcadeKeepOut] cab {cab}: {(inside ? "slide out from" : "clear at")} {me.transform.position}");
                    _slideLocal = Quaternion.Inverse(ArcadeCabs.Rotation(cab)) * push;
                    _slideTries = 0;
                }
                _sliding = inside;
                if (inside)
                {
                    block = false; // let the controller slide out of the box instead of being stuck in it
                    Vector3 before = me.transform.position;
                    float step = 5f * Time.deltaTime;
                    cc.Move(ArcadeCabs.Rotation(cab) * _slideLocal * step);
                    // Wedged against furniture (the other cabinet, a plant): try the next way out.
                    if (Vector3.Distance(before, me.transform.position) < step * 0.25f && ++_slideTries % 4 == 0)
                        _slideLocal = _slideLocal.x > 0.5f ? new Vector3(-0.7071f, 0f, -0.7071f)   // pushed right: back and away from it
                            : _slideLocal.x < -0.5f ? new Vector3(0.7071f, 0f, -0.7071f)
                            : _slideLocal == Vector3.back ? Vector3.left : Vector3.back;
                }
            }
            else _sliding = false;
            if (_box.enabled != block) _box.enabled = block;

            // ---- the player at the cabinet: nobody covers the screen
            if (iPlay && !DisableHide) HideOccluders(me);
            else if (_hidden.Count > 0) ShowAll();
        }

        /// <summary>First cabinet (older tests).</summary>
        public static bool Overlaps(Vector3 pos, float radius, out Vector3 push) => Overlaps(0, pos, radius, out push);

        /// <summary>True when a capsule of <paramref name="radius"/> at <paramref name="pos"/> overlaps the zone of cabinet
        /// <paramref name="cab"/>; push = way out (unit, world).</summary>
        public static bool Overlaps(int cab, Vector3 pos, float radius, out Vector3 push)
        {
            var rot = ArcadeCabs.Rotation(cab);
            Vector3 l = Quaternion.Inverse(rot) * (pos - ArcadeCabs.Origin(cab)) - ArcadeCabs.StandLocal;
            var zone = Rect.MinMaxRect(-HalfWidth, -Behind, HalfWidth, Front);
            push = Vector3.zero;
            var p = new Vector2(l.x, l.z);
            var q = new Vector2(Mathf.Clamp(p.x, zone.xMin, zone.xMax), Mathf.Clamp(p.y, zone.yMin, zone.yMax));
            Vector3 local;
            if (q != p)
            {
                var d = p - q;
                if (d.magnitude >= radius) return false;
                d.Normalize();
                local = new Vector3(d.x, 0f, d.y);
            }
            else
            {
                // Centre inside: leave by the nearest open side (never towards the cabinet).
                float le = p.x - zone.xMin, r = zone.xMax - p.x, b = p.y - zone.yMin;
                local = le <= r && le <= b ? Vector3.left : r <= b ? Vector3.right : Vector3.back;
            }
            push = rot * local;
            return true;
        }

        // ================================================================ hiding on the player's side

        private readonly Vector3[] _samples = new Vector3[20];

        private void HideOccluders(NetPlayer me)
        {
            var cam = me.Camera != null ? me.Camera : Camera.main;
            var arc = LobbyArcade.Of(cab);
            if (cam == null || arc == null || arc.screen == null) return;
            var st = arc.screen.transform;
            int n = 0;
            for (int iy = 0; iy < 4; iy++)
            for (int ix = 0; ix < 5; ix++)
                _samples[n++] = st.TransformPoint(new Vector3(-0.55f + 1.1f * ix / 4f, -0.55f + 1.1f * iy / 3f, 0f));
            Vector3 eye = cam.transform.position;
            foreach (var p in NetPlayer.All)
            {
                if (p == null || p == me) continue;
                bool cover = Covers(p, eye);
                if (cover && !_hidden.ContainsKey(p))
                {
                    var rs = p.GetComponentsInChildren<Renderer>(true);
                    foreach (var r in rs) if (r != null) r.forceRenderingOff = true;
                    _hidden[p] = rs;
                }
                else if (!cover && _hidden.TryGetValue(p, out var rs))
                {
                    foreach (var r in rs) if (r != null) r.forceRenderingOff = false;
                    _hidden.Remove(p);
                }
            }
            // players who left
            if (_hidden.Count > 0)
            {
                List<NetPlayer> gone = null;
                foreach (var k in _hidden.Keys) if (k == null) (gone ??= new List<NetPlayer>()).Add(k);
                if (gone != null) foreach (var k in gone) _hidden.Remove(k);
            }
        }

        private bool Covers(NetPlayer p, Vector3 eye)
        {
            Vector3 a = p.transform.position + Vector3.up * 0.05f, b = p.transform.position + Vector3.up * 2.0f;
            const float radius = 0.5f; // body + arms + held items
            foreach (var s in _samples)
                if (SegmentDistance(eye, s, a, b) < radius) return true;
            return false;
        }

        private void ShowAll()
        {
            foreach (var kv in _hidden)
                if (kv.Key != null)
                    foreach (var r in kv.Value) if (r != null) r.forceRenderingOff = false;
            _hidden.Clear();
        }

        /// <summary>Shortest distance between segments p1-q1 and p2-q2.</summary>
        private static float SegmentDistance(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2)
        {
            Vector3 d1 = q1 - p1, d2 = q2 - p2, r = p1 - p2;
            float a = Vector3.Dot(d1, d1), e = Vector3.Dot(d2, d2), f = Vector3.Dot(d2, r);
            float s, t;
            float c = Vector3.Dot(d1, r), bb = Vector3.Dot(d1, d2), denom = a * e - bb * bb;
            s = denom > 1e-6f ? Mathf.Clamp01((bb * f - c * e) / denom) : 0f;
            t = (bb * s + f) / e;
            if (t < 0f) { t = 0f; s = Mathf.Clamp01(-c / a); }
            else if (t > 1f) { t = 1f; s = Mathf.Clamp01((bb - c) / a); }
            return Vector3.Distance(p1 + d1 * s, p2 + d2 * t);
        }
    }
}
