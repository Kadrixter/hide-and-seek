using UnityEngine;
using UnityEngine.UI;

namespace HideAndSeek.Game.UI
{
    /// <summary>
    /// Shared look of the text-button menus: left shade gradient (text stays readable over the game / 3D scene),
    /// soft vignette, a transparent left column for the menu contents and the small spaced headings.
    /// </summary>
    public static class MenuChrome
    {
        /// <summary>Left edge of the menu column (reference 1920 x 1080).</summary>
        public const float ColumnX = 120f;

        private static Sprite _shade, _vignette;

        /// <summary>Horizontal gradient: dark on the left, almost clear on the right.</summary>
        public static Sprite LeftShade => _shade != null ? _shade : (_shade = MakeShade());

        /// <summary>Radial vignette (clear centre, dark corners).</summary>
        public static Sprite Vignette => _vignette != null ? _vignette : (_vignette = MakeVignette());

        public static Image Shade(string name, Transform parent, float strength = 1f)
        {
            var img = UiKit.Image(name, parent, new Color(0f, 0f, 0f, strength));
            img.sprite = LeftShade;
            img.rectTransform.Stretch();
            return img;
        }

        public static Image VignetteImage(Transform parent, float strength = 0.6f)
        {
            var img = UiKit.Image("Vignette", parent, new Color(0f, 0f, 0f, strength));
            img.sprite = Vignette;
            img.raycastTarget = false;
            img.rectTransform.Stretch();
            return img;
        }

        /// <summary>Transparent vertical column on the left, vertically centred; taller ones are scaled to fit 1080.</summary>
        public static RectTransform Column(string name, Transform parent, Vector2 size)
        {
            var rt = UiKit.Rect(name, parent);
            rt.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(ColumnX, 0f), size);
            const float maxH = 1030f;
            if (size.y > maxH) rt.localScale = Vector3.one * (maxH / size.y);
            UiKit.VStack(rt, 8, 0);
            // height = content (no spare height handed to flexible rows), still centred on the pivot
            rt.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return rt;
        }

        /// <summary>
        /// Labels named "Title" / "T" become the small spaced heading (left aligned, dim) with a short accent line;
        /// centred plain labels that are direct children of a menu column are left aligned.
        /// </summary>
        public static void StyleHeadings(Transform root)
        {
            foreach (var t in root.GetComponentsInChildren<Text>(true))
            {
                var tr = t.transform;
                bool inColumn = tr.parent != null && tr.parent.GetComponent<VerticalLayoutGroup>() != null;
                if ((t.name == "Title" || t.name == "T") && inColumn && tr.Find("HeadLine") == null)
                {
                    t.text = UiFonts.Spaced(t.text);
                    t.font = UiFonts.Bold;
                    t.fontStyle = FontStyle.Normal;
                    t.fontSize = 26;
                    t.alignment = TextAnchor.MiddleLeft;
                    t.color = new Color(1f, 1f, 1f, 0.6f);
                    t.horizontalOverflow = HorizontalWrapMode.Overflow;
                    var line = UiKit.Image("HeadLine", tr, UiKit.ButtonHover);
                    line.raycastTarget = false;
                    line.rectTransform.Place(new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0.5f), new Vector2(2f, 4f), new Vector2(40f, 3f));
                    continue;
                }
                if (t.alignment == TextAnchor.MiddleCenter && tr.parent != null && tr.parent.GetComponent<VerticalLayoutGroup>() != null
                    && tr.parent.name is "Panel" or "Main" or "Browser" or "Options" or "Audio" or "Voice" or "Character")
                    t.alignment = TextAnchor.MiddleLeft;
            }
        }

        private static Sprite MakeShade()
        {
            const int w = 256;
            var tex = new Texture2D(w, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
            var px = new Color32[w];
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)(w - 1);
                // 0.72 at the left edge -> 0.28 at 55 % -> 0.1 on the right (as in the accepted mock-up)
                float a = u < 0.55f ? Mathf.Lerp(0.72f, 0.28f, Mathf.SmoothStep(0f, 1f, u / 0.55f)) : Mathf.Lerp(0.28f, 0.1f, (u - 0.55f) / 0.45f);
                px[x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, 1), new Vector2(0.5f, 0.5f));
        }

        private static Sprite MakeVignette()
        {
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx * 0.8f + dy * dy);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1.35f, d));
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        }
    }
}

namespace HideAndSeek.Game.UI
{
    /// <summary>Fades its menu out while a <see cref="ConfirmDialog"/> is open (the question stands alone on the blur).</summary>
    public class HideUnderDialog : MonoBehaviour
    {
        private CanvasGroup _group;

        private void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
        }

        private void Update()
        {
            float target = ConfirmDialog.IsOpen ? 0f : 1f;
            _group.alpha = Mathf.MoveTowards(_group.alpha, target, Time.unscaledDeltaTime / 0.15f);
        }
    }
}

namespace HideAndSeek.Game.UI
{
    /// <summary>
    /// While this menu canvas lives, every other screen canvas below it (HUD, key hints, player list, minimap...)
    /// fades out, so only the menu stands on the blurred game. Uses CanvasGroups (the HUD toggles Canvas.enabled itself).
    /// </summary>
    public class HudHider : MonoBehaviour
    {
        private static int _active;
        private static readonly System.Collections.Generic.List<CanvasGroup> Hidden = new();
        private Canvas _own;

        private void OnEnable()
        {
            _own = GetComponentInParent<Canvas>();
            _active++;
        }

        private void OnDisable()
        {
            _active = Mathf.Max(0, _active - 1);
            if (_active == 0) Restore();
        }

        private void Update()
        {
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
            {
                if (!c.isRootCanvas || c == _own || c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                if (c.sortingOrder >= (_own != null ? _own.sortingOrder : 400)) continue; // confirm dialog etc.
                var g = c.GetComponent<CanvasGroup>();
                if (g == null) g = c.gameObject.AddComponent<CanvasGroup>();
                if (!Hidden.Contains(g)) Hidden.Add(g);
                g.alpha = Mathf.MoveTowards(g.alpha, 0f, Time.unscaledDeltaTime / 0.15f);
                g.blocksRaycasts = false;
            }
        }

        private static void Restore()
        {
            foreach (var g in Hidden)
                if (g != null) { g.alpha = 1f; g.blocksRaycasts = true; }
            Hidden.Clear();
        }
    }
}
