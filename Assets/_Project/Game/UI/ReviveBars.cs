using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using UnityEngine;
using UnityEngine.UI;

namespace HideAndSeek.Game.UI
{
    /// <summary>
    /// Progress rings for lifting a downed player (and healing): a large filling circle under the crosshair for the
    /// helper ("Podnosisz X", seconds in the middle) and for the downed player ("X cię podnosi"), plus small rings above
    /// every downed player someone else is lifting - hidden from the seeker (he must not see who is being lifted).
    /// Progress comes from <see cref="NetPlayer.ReviveTarget"/> / <see cref="NetPlayer.ReviveProgress"/>.
    /// </summary>
    public class ReviveBars : MonoBehaviour
    {
        private const int WorldBars = 4;

        private RectTransform _canvasRt;
        private RectTransform _center;
        private Image _centerFill;
        private Text _centerText, _centerSecs;
        private readonly RectTransform[] _world = new RectTransform[WorldBars];
        private readonly Image[] _worldFill = new Image[WorldBars];
        private readonly Text[] _worldText = new Text[WorldBars];

        public static ReviveBars Create(Transform canvasRoot)
        {
            var go = new GameObject("ReviveBars", typeof(RectTransform));
            go.transform.SetParent(canvasRoot, false);
            ((RectTransform)go.transform).Stretch();
            var bars = go.AddComponent<ReviveBars>();
            bars.Build((RectTransform)canvasRoot);
            return bars;
        }

        private void Build(RectTransform canvasRt)
        {
            _canvasRt = canvasRt;
            _center = Ring("Center", 112f, 11f, out _centerFill, out _centerText, 26);
            _center.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -265), new Vector2(112, 112));
            _centerSecs = UiKit.Label("Secs", _center, "", 30, TextAnchor.MiddleCenter);
            _centerSecs.rectTransform.Stretch();
            _centerSecs.AddOutline();
            _center.gameObject.SetActive(false);
            for (int i = 0; i < WorldBars; i++)
            {
                _world[i] = Ring("World" + i, 44f, 6f, out _worldFill[i], out _worldText[i], 18);
                _world[i].anchorMin = _world[i].anchorMax = new Vector2(0.5f, 0.5f);
                _world[i].gameObject.SetActive(false);
            }
        }

        private RectTransform Bar(string name, Vector2 size, out Image fill, out Text label, int font)
        {
            var bg = UiKit.Image(name, transform, new Color(0f, 0f, 0f, 0.6f));
            var rt = bg.rectTransform;
            rt.sizeDelta = size;
            bg.AddOutline(2f, 0.6f);
            fill = UiKit.Image("Fill", rt, new Color(0.35f, 0.85f, 0.4f, 0.95f));
            fill.rectTransform.Stretch(3f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.fillAmount = 0f;
            // Filled images need a sprite to fill; use the built-in UI sprite.
            fill.sprite = UiSprite;
            label = UiKit.Label("Text", rt, "", font, TextAnchor.LowerCenter);
            label.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 0), new Vector2(0, 4), new Vector2(260, font + 12));
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.AddOutline();
            return rt;
        }

        private static Sprite _sprite;
        private static readonly System.Collections.Generic.Dictionary<int, Sprite> RingSprites = new();

        private static Sprite UiSprite
        {
            get
            {
                if (_sprite != null) return _sprite;
                var tex = Texture2D.whiteTexture;
                _sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                return _sprite;
            }
        }

        /// <summary>Anti-aliased ring (thickness relative to the diameter), generated once per thickness.</summary>
        private static Sprite RingSprite(float thickness01)
        {
            int key = Mathf.RoundToInt(thickness01 * 1000f);
            if (RingSprites.TryGetValue(key, out var sp) && sp != null) return sp;
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[n * n];
            float outer = n * 0.5f - 1f, inner = outer - thickness01 * n;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x + 0.5f - n * 0.5f) * (x + 0.5f - n * 0.5f) + (y + 0.5f - n * 0.5f) * (y + 0.5f - n * 0.5f));
                float a = Mathf.Clamp01(outer - d + 0.5f) * Mathf.Clamp01(d - inner + 0.5f);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            sp = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
            RingSprites[key] = sp;
            return sp;
        }

        /// <summary>Dark track ring + a filling ring (clockwise from the top) + label above.</summary>
        private RectTransform Ring(string name, float size, float thickness, out Image fill, out Text label, int font)
        {
            var root = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(transform, false);
            root.sizeDelta = new Vector2(size, size);
            var disc = UiKit.Image("Disc", root, new Color(0f, 0f, 0f, 0.45f));
            disc.sprite = RingSprite(0.5f);
            disc.rectTransform.Stretch(thickness * 0.5f);
            var track = UiKit.Image("Track", root, new Color(1f, 1f, 1f, 0.18f));
            track.sprite = RingSprite(thickness / size);
            track.rectTransform.Stretch();
            fill = UiKit.Image("Fill", root, new Color(0.35f, 0.85f, 0.4f, 0.95f));
            fill.sprite = RingSprite(thickness / size);
            fill.rectTransform.Stretch();
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Radial360;
            fill.fillOrigin = (int)Image.Origin360.Top;
            fill.fillClockwise = true;
            fill.fillAmount = 0f;
            label = UiKit.Label("Text", root, "", font, TextAnchor.UpperCenter);
            label.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 1), new Vector2(0, -6), new Vector2(360, font + 12));
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.AddOutline();
            return root;
        }

        private void LateUpdate()
        {
            var me = NetPlayer.Local;
            var lpc = LocalPlayerController.Instance;
            var cam = Camera.main;

            // ---- center bar: my own hold, or somebody lifting me
            bool show = false;
            if (lpc != null && lpc.HoldKind != PromptKind.None && lpc.HoldTarget != null)
            {
                float p = Mathf.Clamp01(lpc.HoldProgress);
                float left = Mathf.Max(0f, lpc.HoldDurationNow * (1f - p));
                string what = lpc.HoldKind == PromptKind.Rescue ? $"Podnosisz: {lpc.HoldTarget.Nick}"
                    : lpc.HoldKind == PromptKind.SelfRevive ? "Opatrujesz się i wstajesz"
                    : lpc.HoldKind == PromptKind.Resurrect ? $"Wskrzeszasz: {lpc.HoldTarget.Nick}"
                    : lpc.HoldTarget == me ? "Opatrujesz rany" : $"Opatrujesz: {lpc.HoldTarget.Nick}";
                SetBar(_center, _centerFill, _centerText, p, what, true);
                _centerSecs.text = $"{left:0.0}";
                show = true;
            }
            else if (lpc != null && lpc.WirePulling)
            {
                // tearing a piece of wire off a fence (LocalPlayerController.RcWire.cs)
                float p = lpc.WireProgress;
                SetBar(_center, _centerFill, _centerText, p, "Urywasz kawałek drutu", true);
                _centerSecs.text = $"{Mathf.Max(0f, HideAndSeek.Characters.WirePullTimeline.Duration * (1f - p)):0.0}";
                show = true;
            }
            else if (me != null && me.Life.Value == LifeState.Downed)
            {
                var helper = HelperOf(me, out float p);
                if (helper != null)
                {
                    float left = Mathf.Max(0f, Tuning.RescueHoldTime * (1f - p));
                    SetBar(_center, _centerFill, _centerText, p, $"{helper.Nick} cię podnosi", true);
                    _centerSecs.text = $"{left:0.0}";
                    show = true;
                }
            }
            if (!show && _center.gameObject.activeSelf) _center.gameObject.SetActive(false);

            // ---- small rings above downed players lifted by somebody else (never for the seeker)
            int used = 0;
            if (cam != null && me != null && !me.IsSeeker)
            {
                foreach (var helper in NetPlayer.All)
                {
                    if (used >= WorldBars) break;
                    if (helper == null || helper == me || !helper.Reviving.Value) continue;
                    var target = NetPlayer.Find(helper.ReviveTarget.Value);
                    if (target == null || target == me || target.Life.Value != LifeState.Downed) continue;
                    Vector3 wp = target.ChestPosition + Vector3.up * 0.55f;
                    Vector3 sp = cam.WorldToScreenPoint(wp);
                    if (sp.z <= 0f || sp.z > 40f) continue;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRt, sp, null, out Vector2 local);
                    _world[used].anchoredPosition = local;
                    float p = helper.ReviveProgress.Value / 255f;
                    SetBar(_world[used], _worldFill[used], _worldText[used], p, $"{p * 100f:0}%", true);
                    used++;
                }
            }
            for (int i = used; i < WorldBars; i++)
                if (_world[i].gameObject.activeSelf) _world[i].gameObject.SetActive(false);
        }

        private static NetPlayer HelperOf(NetPlayer downed, out float progress)
        {
            progress = 0f;
            NetPlayer best = null;
            foreach (var pl in NetPlayer.All)
            {
                if (pl == null || pl == downed || !pl.Reviving.Value || pl.ReviveTarget.Value != downed.NetworkObjectId) continue;
                float p = pl.ReviveProgress.Value / 255f;
                if (best == null || p > progress)
                {
                    best = pl;
                    progress = p;
                }
            }
            return best;
        }

        private static void SetBar(RectTransform root, Image fill, Text text, float p, string label, bool on)
        {
            if (root.gameObject.activeSelf != on) root.gameObject.SetActive(on);
            fill.fillAmount = p;
            fill.color = Color.Lerp(new Color(0.95f, 0.75f, 0.25f, 0.95f), new Color(0.35f, 0.9f, 0.4f, 0.95f), p);
            text.text = label;
        }
    }
}
