using HideAndSeek.Game.Cinematics;
using HideAndSeek.Game.Net;
using HideAndSeek.Game.UI;
using HideAndSeek.Map;
using UnityEngine;
using UnityEngine.UI;

namespace HideAndSeek.Game
{
    /// <summary>
    /// The hiders' objective line under the round timer (own canvas), by <see cref="MatchManager.Escape"/>:
    /// Fuses - find the fuses and the right box (+ how many the player carries), DotLink - go to the broken lever at the
    /// exit gate, GateOpening - the gate is opening, GateOpen - run out through the gate. From DotLink on an arrow turned
    /// by the view and the distance (and the level, when another one) point to the gate. Hiders only; hidden during the
    /// intro / outro (HudHider) and outside a hide-and-seek round. Other parts of the objective show only their own toasts.
    /// </summary>
    public sealed class EscapeHud : MonoBehaviour
    {
        public static EscapeHud Instance { get; private set; }
        /// <summary>Current line (tests).</summary>
        public static string LineText { get; private set; } = "";

        private Canvas _canvas;
        private RectTransform _row, _arrowRt;
        private Image _bg, _arrow;
        private Text _line;

        public static void Ensure()
        {
            if (Instance != null) return;
            var go = new GameObject("[EscapeHud]");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<EscapeHud>();
        }

        private void Awake()
        {
            _canvas = UiKit.CreateCanvas("EscapeHudCanvas", 8, transform);
            _row = UiKit.Rect("EscapeRow", _canvas.transform);
            _row.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -122f), new Vector2(700f, 34f));
            _bg = UiKit.Image("Bg", _row, new Color(0.02f, 0.025f, 0.03f, 0.55f));
            _bg.rectTransform.Stretch();
            _line = UiKit.Label("Objective", _row, "", 21, TextAnchor.MiddleCenter, UiKit.Text);
            _line.rectTransform.Stretch();
            _line.supportRichText = true;
            _line.AddOutline(1.5f, 0.8f);
            _arrow = UiKit.Image("Arrow", _row, Color.white);
            _arrow.sprite = ArrowSprite();
            _arrowRt = _arrow.rectTransform;
            _arrowRt.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(20f, 0f), new Vector2(22f, 22f));
            _row.gameObject.SetActive(false);
        }

        private void Update()
        {
            string text = Compose(out bool arrow, out Vector3 target, out Color color);
            bool show = !string.IsNullOrEmpty(text) && !HideAndSeek.Game.Cinematics.HudHider.Active
                && !HideAndSeek.Game.DotLink.DotLinkLocal.Active; // solving the panel: the row would cover the puzzle's top row
            if (_row.gameObject.activeSelf != show) _row.gameObject.SetActive(show);
            LineText = show ? text : "";
            if (!show) return;
            if (_line.text != text) _line.text = text;
            _line.color = color;
            float w = _line.preferredWidth + (arrow ? 64f : 36f);
            _row.sizeDelta = new Vector2(Mathf.Max(260f, w), 34f);
            _line.rectTransform.offsetMin = new Vector2(arrow ? 30f : 0f, 0f);
            _arrow.enabled = arrow;
            if (!arrow) return;
            var me = NetPlayer.Local;
            var cam = me != null && me.Camera != null ? me.Camera.transform : Camera.main != null ? Camera.main.transform : null;
            if (cam == null) { _arrow.enabled = false; return; }
            Vector3 to = target - cam.position, f = cam.forward;
            to.y = 0f;
            f.y = 0f;
            float ang = to.sqrMagnitude > 0.01f && f.sqrMagnitude > 0.001f ? Vector3.SignedAngle(f, to, Vector3.up) : 0f;
            _arrowRt.localRotation = Quaternion.Euler(0f, 0f, -ang);
            _arrow.color = color;
        }

        private static string Compose(out bool arrow, out Vector3 target, out Color color)
        {
            arrow = false;
            target = default;
            color = UiKit.Text;
            var mm = MatchManager.Instance;
            var me = NetPlayer.Local;
            if (mm == null || me == null || !me.IsHider || mm.RaceMode) return "";
            var ph = mm.Phase.Value;
            if (ph != MatchPhase.Hiding && ph != MatchPhase.Seeking) return "";
            if (!mm.FuseBoxes.Value.Placed) return "";
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 5f);
            switch (mm.Escape.Value)
            {
                case EscapeStage.Fuses:
                {
                    color = new Color(1f, 0.86f, 0.45f);
                    int n = me.FuseCount;
                    return n > 0 ? $"Znajdź bezpieczniki i właściwą skrzynkę   <color=#d9d4c0>(masz: {n})</color>" : "Znajdź bezpieczniki i właściwą skrzynkę";
                }
                case EscapeStage.DotLink:
                    color = new Color(0.55f, 0.9f, 1f);
                    target = GateTarget(false);
                    arrow = true;
                    return "Idź do skrzynki z urwaną dźwignią przy bramie WYJŚCIE" + Where(me, target);
                case EscapeStage.GateOpening:
                    color = new Color(pulse, 0.7f * pulse, 0.3f * pulse);
                    target = GateTarget(true);
                    arrow = true;
                    return "Brama się otwiera!" + Where(me, target);
                default:
                    color = new Color(0.45f * pulse, pulse, 0.5f * pulse);
                    target = GateTarget(true);
                    arrow = true;
                    return "UCIEKAJ za bramę!" + Where(me, target);
            }
        }

        private static Vector3 GateTarget(bool opening)
        {
            var g = ExitGate.Instance;
            if (NetPlayer.Local != null)
            {
                // The nearer of the two gates (both boxes have the panel, both gates open together).
                var near = ExitGate.Get(HideAndSeek.Game.Escape.EscapeTimeline.Nearest(NetPlayer.Local.transform.position));
                if (near != null) g = near;
                // Each gate opens on its own: towards an open one when running out, else to a box whose panel is still to do.
                var mm = MatchManager.Instance;
                Vector3 me = NetPlayer.Local.transform.position;
                float best = float.MaxValue;
                for (int i = 0; mm != null && i < ExitGate.Count; i++)
                {
                    var gi = ExitGate.Get(i);
                    if (gi == null || (mm.EscapeGateAtOf(i).Value > 0d) != opening) continue;
                    float d = ((opening ? gi.SillWorld : gi.BoxWorld) - me).sqrMagnitude;
                    if (d < best) { best = d; g = gi; }
                }
            }
            if (g == null) return ExitGate.DefaultPos + ExitGate.BoxPos;
            return opening ? g.SillWorld : g.BoxWorld;
        }

        private static string Where(NetPlayer me, Vector3 target)
        {
            Vector3 d = target - me.transform.position;
            float m = new Vector2(d.x, d.z).magnitude;
            int lv = MapInfo.LevelOf(target.y), mine = MapInfo.LevelOf(me.transform.position.y);
            string level = lv != mine ? $", poziom {(lv == 0 ? "0" : "-" + lv)}" : "";
            return $"   <color=#c8d2d8>{m:0} m{level}</color>";
        }

        private static Sprite ArrowSprite()
        {
            const int n = 32;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                // Arrow pointing up: a head and a short shaft.
                float u = (x + 0.5f) / n - 0.5f, v = (y + 0.5f) / n;
                bool head = v > 0.45f && v < 0.95f && Mathf.Abs(u) < (0.95f - v) * 0.85f;
                bool shaft = v > 0.08f && v <= 0.5f && Mathf.Abs(u) < 0.11f;
                px[y * n + x] = head || shaft ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            }
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 32f);
        }
    }
}
