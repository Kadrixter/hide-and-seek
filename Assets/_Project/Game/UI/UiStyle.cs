using System;
using HideAndSeek.Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HideAndSeek.Game.UI
{
    /// <summary>
    /// Procedural UI sprites (rounded rectangle, circle) and the richer widgets used by the main menu and the
    /// in-game Esc menu: toggle switch rows, option pickers ("&lt; value &gt;"), slider rows and rounded panels.
    /// </summary>
    public static class UiStyle
    {
        public static readonly Color Row = new Color(0.12f, 0.13f, 0.15f, 0.95f);
        public static readonly Color RowHover = new Color(0.17f, 0.185f, 0.21f, 1f);
        public static readonly Color TrackOff = new Color(0.25f, 0.26f, 0.29f, 1f);
        public static readonly Color Border = new Color(1f, 1f, 1f, 0.1f);

        // Settings widgets (rows, switches, pickers, sliders): flat, square, sea-green accent like the text buttons.
        public static Color WidgetAccent => UiKit.ButtonHover;
        public static readonly Color SqRow = new Color(1f, 1f, 1f, 0.045f);
        public static readonly Color SqRowHover = new Color(1f, 1f, 1f, 0.085f);
        public static readonly Color SqBorder = new Color(1f, 1f, 1f, 0.12f);
        public static readonly Color SqTrack = new Color(1f, 1f, 1f, 0.16f);
        public static Color WidgetHoverBorder => new Color(WidgetAccent.r, WidgetAccent.g, WidgetAccent.b, 0.6f);

        private static Sprite _sqRing;

        /// <summary>1 px square outline (9-sliced, any size).</summary>
        public static Sprite SquareRing
        {
            get
            {
                if (_sqRing != null) return _sqRing;
                const int n = 8;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Point, hideFlags = HideFlags.DontSave };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    px[y * n + x] = (x == 0 || y == 0 || x == n - 1 || y == n - 1) ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
                tex.SetPixels32(px);
                tex.Apply();
                return _sqRing = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(2, 2, 2, 2));
            }
        }

        /// <summary>Square 1 px outline stretched over <paramref name="parent"/>.</summary>
        public static Image SquareBorder(string name, Transform parent, Color color)
        {
            var r = UiKit.Image(name, parent, color);
            r.sprite = SquareRing;
            r.type = Image.Type.Sliced;
            r.raycastTarget = false;
            r.rectTransform.Stretch();
            return r;
        }

        private static Sprite _round, _round6, _circle, _grad, _ring, _ringSmall;

        /// <summary>Rounded rectangle with a vertical gradient baked in (top lighter): button base.</summary>
        public static Sprite RoundGradient => _grad != null ? _grad : (_grad = MakeRound(64, 16f, 0f, true));

        /// <summary>Rounded outline only (border overlay).</summary>
        public static Sprite RingSprite => _ring != null ? _ring : (_ring = MakeRound(64, 16f, 2.2f, false));
        public static Sprite RingSmall => _ringSmall != null ? _ringSmall : (_ringSmall = MakeRound(32, 7f, 1.6f, false));

        /// <summary>Stretched rounded border on top of <paramref name="parent"/>.</summary>
        public static Image Ring(string name, Transform parent, Color color, bool small = false)
        {
            var r = UiKit.Image(name, parent, color);
            r.sprite = small ? RingSmall : RingSprite;
            r.type = Image.Type.Sliced;
            r.raycastTarget = false;
            r.rectTransform.Stretch();
            return r;
        }

        /// <summary>9-sliced rounded rectangle (radius ~14 px).</summary>
        public static Sprite Round => _round != null ? _round : (_round = MakeRound(64, 16f, 0f, false));

        /// <summary>Small radius variant (tracks, inputs).</summary>
        public static Sprite RoundSmall => _round6 != null ? _round6 : (_round6 = MakeRound(32, 7f, 0f, false));

        public static Sprite Circle => _circle != null ? _circle : (_circle = MakeCircle(64));

        private static Sprite MakeRound(int size, float radius, float ring, bool gradient)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, radius, size - radius), cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                float d = new Vector2(x + 0.5f - cx, y + 0.5f - cy).magnitude;
                float a = Mathf.Clamp01(radius - d + 0.5f);
                if (ring > 0f)
                {
                    // Distance to the rounded edge from inside (also along the straight sides).
                    float edge = Mathf.Min(Mathf.Min(x + 0.5f, size - x - 0.5f), Mathf.Min(y + 0.5f, size - y - 0.5f));
                    float inside = (cx != x + 0.5f || cy != y + 0.5f) ? radius - d : edge;
                    a *= Mathf.Clamp01(ring - inside + 0.5f);
                }
                byte g = gradient ? (byte)(255 * Mathf.Lerp(0.72f, 1f, y / (float)(size - 1))) : (byte)255;
                px[y * size + x] = new Color32(g, g, g, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            float b = radius + 1f;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        private static Sprite MakeCircle(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
            var px = new Color32[size * size];
            float r = size / 2f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = new Vector2(x + 0.5f - r, y + 0.5f - r).magnitude;
                px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(r - d) * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        /// <summary>Makes an Image a rounded, sliced shape.</summary>
        public static Image Rounded(this Image img, bool small = false)
        {
            img.sprite = small ? RoundSmall : Round;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f;
            return img;
        }

        public static Shadow AddDrop(this Graphic g, float d = 4f, float alpha = 0.45f)
        {
            var s = g.gameObject.AddComponent<Shadow>();
            s.effectColor = new Color(0f, 0f, 0f, alpha);
            s.effectDistance = new Vector2(0f, -d);
            return s;
        }

        /// <summary>Rounded panel with a drop shadow and a thin accent line on top.</summary>
        public static Image Panel(string name, Transform parent, Color color)
        {
            var img = UiKit.Image(name, parent, color).Rounded();
            img.AddDrop(8f, 0.5f);
            var accent = UiKit.Image("Accent", img.transform, UiKit.Accent).Rounded(true);
            accent.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -10), new Vector2(120, 5));
            accent.raycastTarget = false;
            accent.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            return img;
        }

        // ------------------------------------------------------------------ rows

        private static RectTransform RowBase(string name, Transform parent, string label, int fontSize, out Image bg, out Text text)
        {
            bg = UiKit.Image(name, parent, SqRow);
            var rt = bg.rectTransform;
            SquareBorder("Border", rt, SqBorder);
            text = UiKit.Label("Label", rt, label, fontSize);
            text.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 0.5f), new Vector2(20, 0), new Vector2(-200, 0));
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            return rt;
        }

        /// <summary>Label on the left, animated on/off switch on the right; the whole row is clickable.</summary>
        public static RectTransform Switch(string name, Transform parent, string label, Func<bool> get, Action<bool> set, int fontSize = 24)
        {
            var rt = RowBase(name, parent, label, fontSize, out var bg, out _);
            var track = UiKit.Image("Track", rt, SqTrack);
            track.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-20, 0), new Vector2(UiSwitch.TrackW, UiSwitch.TrackH));
            track.raycastTarget = false;
            var knob = UiKit.Image("Knob", track.transform, Color.white);
            knob.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(UiSwitch.KnobOff, 0), new Vector2(UiSwitch.Knob, UiSwitch.Knob));
            knob.raycastTarget = false;
            var state = UiKit.Label("State", rt, "", fontSize - 4, TextAnchor.MiddleRight, UiKit.TextDim);
            state.rectTransform.Place(new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-86, 0), new Vector2(110, 0));

            var sw = rt.gameObject.AddComponent<UiSwitch>();
            sw.Init(bg, track, knob.rectTransform, state, get, set);
            return rt;
        }

        private static Sprite _pill;

        private static Sprite MakePill()
        {
            if (_pill != null) return _pill;
            // Circle sliced through the middle = capsule at any width.
            var c = Circle;
            _pill = Sprite.Create(c.texture, c.rect, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(31, 31, 31, 31));
            return _pill;
        }

        /// <summary>Label on the left, "&lt; value &gt;" on the right. Clicking the row steps forward.</summary>
        public static RectTransform Choice(string name, Transform parent, string label, Func<string> value, Action<int> step, int fontSize = 24)
        {
            var rt = RowBase(name, parent, label, fontSize, out var bg, out var labelText);
            labelText.rectTransform.sizeDelta = new Vector2(-330, 0);
            var val = UiKit.Label("Value", rt, value(), fontSize, TextAnchor.MiddleCenter, WidgetAccent, FontStyle.Bold);
            val.rectTransform.Place(new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-66, 0), new Vector2(240, 0));
            val.horizontalOverflow = HorizontalWrapMode.Overflow;
            Arrow(rt, "<", new Vector2(-300, 0), () => { step(-1); val.text = value(); });
            Arrow(rt, ">", new Vector2(-16, 0), () => { step(1); val.text = value(); });
            var row = rt.gameObject.AddComponent<UiRowFx>();
            row.Init(bg, () => { step(1); val.text = value(); });
            return rt;
        }

        private static void Arrow(RectTransform row, string glyph, Vector2 pos, Action click)
        {
            // dark square like the row, light arrow; hover: sea-green outline and arrow (UiArrowFx)
            var b = UiKit.Image("Arrow" + glyph, row, new Color(0f, 0f, 0f, 0.32f));
            var ring = SquareBorder("Ring", b.transform, SqBorder);
            b.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), pos, new Vector2(36, 36));
            var t = UiKit.Label("T", b.transform, glyph, 24, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.85f), FontStyle.Bold);
            t.rectTransform.Stretch();
            var btn = b.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            var nav = btn.navigation;
            nav.mode = Navigation.Mode.None;
            btn.navigation = nav;
            var fx = b.gameObject.AddComponent<UiArrowFx>();
            fx.Ring = ring;
            fx.Glyph = t;
            btn.onClick.AddListener(() =>
            {
                GameAudio.Play2D(GameSfx.UiClick, 0.7f);
                click();
            });
        }

        private static Sprite _circleRing;

        private static Sprite MakeCircleRing()
        {
            if (_circleRing != null) return _circleRing;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var px = new Color32[size * size];
            float r = size / 2f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = new Vector2(x + 0.5f - r, y + 0.5f - r).magnitude;
                float a = Mathf.Clamp01(r - d) * Mathf.Clamp01(d - (r - 3f));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return _circleRing = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        /// <summary>Label on the left, a small button on the right (e.g. friends list "Zaproś").</summary>
        public static RectTransform ActionRow(string name, Transform parent, string label, string button, Action click, int fontSize = 22)
        {
            var rt = RowBase(name, parent, label, fontSize, out var bg, out var text);
            text.rectTransform.sizeDelta = new Vector2(-190, 0);
            var b = UiKit.BoxButton("Action", rt, button, click, 20, new Color(0.13f, 0.32f, 0.16f));
            ((RectTransform)b.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-8, 0), new Vector2(150, 38));
            return rt;
        }

        /// <summary>Label + value on one line, a thin square slider below, all inside one flat row card.</summary>
        public static Slider SliderRow(Transform parent, string label, float min, float max, float value, Action<float> set, Func<float, string> fmt, int fontSize = 22)
        {
            var card = UiKit.Image("SliderRow", parent, SqRow);
            card.Height(68);
            SquareBorder("Border", card.transform, SqBorder);
            var l = UiKit.Label("Label", card.transform, label, fontSize);
            l.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(20, -3), new Vector2(-140, 28));
            var v = UiKit.Label("Value", card.transform, fmt(value), fontSize, TextAnchor.MiddleRight, WidgetAccent, FontStyle.Bold);
            v.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-20, -3), new Vector2(120, 28));
            var s = UiKit.Slider("S", card.transform, min, max, value, false, x =>
            {
                set(x);
                v.text = fmt(x);
            });
            ((RectTransform)s.transform).Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 3), new Vector2(-44, 24));
            return s;
        }
    }

    /// <summary>Hover / press feedback for buttons: lighter colour, accent bar, slight scale.</summary>
    public class UiButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public Image Accent, Border;
        public Shadow Drop;
        public RectTransform Label;
        public Text Chevron;
        private bool _hover, _down;
        private float _h;
        private Button _btn;

        private void Awake() => _btn = GetComponent<Button>();
        public void OnPointerEnter(PointerEventData e) => _hover = true;
        public void OnPointerExit(PointerEventData e) { _hover = false; _down = false; }
        public void OnPointerDown(PointerEventData e) => _down = true;
        public void OnPointerUp(PointerEventData e) => _down = false;

        private void OnDisable()
        {
            _hover = _down = false;
            _h = 0f;
            transform.localScale = Vector3.one;
            if (Accent != null) Accent.color = new Color(Accent.color.r, Accent.color.g, Accent.color.b, 0f);
            if (Chevron != null) Chevron.color = new Color(Chevron.color.r, Chevron.color.g, Chevron.color.b, 0f);
            if (Border != null) Border.color = UiStyle.Border;
        }

        private void Update()
        {
            bool on = _hover && (_btn == null || _btn.interactable);
            // Ease towards the target (fast in, a bit slower out) - no scaling: it blurred the text.
            _h = Mathf.Lerp(_h, on ? 1f : 0f, 1f - Mathf.Exp(-Time.unscaledDeltaTime * (on ? 16f : 9f)));
            if (transform.localScale != Vector3.one) transform.localScale = Vector3.one;
            bool wide = ((RectTransform)transform).rect.width >= 220f;
            float e = _h * _h * (3f - 2f * _h);
            if (Accent != null)
            {
                var c = Accent.color;
                c.a = wide ? e : 0f;
                Accent.color = c;
                Accent.rectTransform.sizeDelta = new Vector2(5f, -20f + 8f * e);
            }
            if (Border != null) Border.color = Color.Lerp(UiStyle.SqBorder, UiStyle.WidgetHoverBorder, e);
            if (Drop != null)
            {
                Drop.effectColor = Color.Lerp(new Color(0f, 0f, 0f, 0.3f), new Color(UiKit.ButtonHover.r * 0.4f, UiKit.ButtonHover.g * 0.4f, UiKit.ButtonHover.b * 0.4f, 0.45f), e);
                Drop.effectDistance = new Vector2(0f, -(3f + 2f * e) + (_down ? 2f : 0f));
            }
            if (Label != null) Label.anchoredPosition = new Vector2(wide ? 5f * e : 0f, _down ? -2f : 0f);
            if (Chevron != null)
            {
                // Only on wide buttons (on small ones it covered the text).
                var c = Chevron.color;
                c.a = wide ? e : 0f;
                Chevron.color = c;
                Chevron.rectTransform.anchoredPosition = new Vector2(-30f + 12f * e, 0f);
            }
        }
    }

    /// <summary>Clickable row with hover highlight.</summary>
    public class UiRowFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        private Image _bg;
        private Action _click;
        private bool _hover;

        private Image _border;

        public void Init(Image bg, Action click)
        {
            _bg = bg;
            var b = bg.transform.Find("Border");
            if (b != null) _border = b.GetComponent<Image>();
            _click = click;
            bg.raycastTarget = true;
        }

        public void OnPointerEnter(PointerEventData e)
        {
            _hover = true;
            GameAudio.Play2D(GameSfx.UiHover, 0.3f);
        }

        public void OnPointerExit(PointerEventData e) => _hover = false;

        public void OnPointerClick(PointerEventData e)
        {
            if (e.pointerPress != gameObject && e.pointerPress != null && e.pointerPress.GetComponent<Button>() != null) return;
            GameAudio.Play2D(GameSfx.UiClick, 0.7f);
            _click?.Invoke();
        }

        private void Update()
        {
            if (_bg != null) _bg.color = Color.Lerp(_bg.color, _hover ? UiStyle.SqRowHover : UiStyle.SqRow, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 14f));
            if (_border != null) _border.color = Color.Lerp(_border.color, _hover ? UiStyle.WidgetHoverBorder : UiStyle.SqBorder, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 14f));
        }
    }

    /// <summary>Hover of the square "&lt;" / "&gt;" arrows: outline and arrow ease to the sea-green accent.</summary>
    public class UiArrowFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Image Ring;
        public Text Glyph;
        private bool _hover;
        private float _h;

        public void OnPointerEnter(PointerEventData e) => _hover = true;
        public void OnPointerExit(PointerEventData e) => _hover = false;

        private void OnDisable()
        {
            _hover = false;
            _h = 0f;
            Apply();
        }

        private void Update()
        {
            _h = Mathf.Lerp(_h, _hover ? 1f : 0f, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 16f));
            Apply();
        }

        private void Apply()
        {
            if (Ring != null) Ring.color = Color.Lerp(UiStyle.SqBorder, UiStyle.WidgetHoverBorder, _h);
            if (Glyph != null) Glyph.color = Color.Lerp(new Color(1f, 1f, 1f, 0.85f), UiStyle.WidgetAccent, _h);
        }
    }

    /// <summary>Animated on/off switch row.</summary>
    public class UiSwitch : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        // Rectangular track with a square knob (3 px inset).
        public const float TrackW = 52f, TrackH = 24f, Knob = 18f, KnobOff = 3f + Knob * 0.5f;
        private Image _bg, _track, _border;
        private RectTransform _knob;
        private Text _state;
        private Func<bool> _get;
        private Action<bool> _set;
        private float _t;
        private bool _hover;

        public void Init(Image bg, Image track, RectTransform knob, Text state, Func<bool> get, Action<bool> set)
        {
            _bg = bg;
            _track = track;
            _knob = knob;
            _state = state;
            var bd = bg.transform.Find("Border");
            if (bd != null) _border = bd.GetComponent<Image>();
            _get = get;
            _set = set;
            bg.raycastTarget = true;
            _t = get() ? 1f : 0f;
            Apply();
        }

        public void OnPointerEnter(PointerEventData e)
        {
            _hover = true;
            GameAudio.Play2D(GameSfx.UiHover, 0.3f);
        }

        public void OnPointerExit(PointerEventData e) => _hover = false;

        public void OnPointerClick(PointerEventData e)
        {
            _set(!_get());
            GameAudio.Play2D(GameSfx.UiClick, 0.8f);
        }

        private void Update()
        {
            _t = Mathf.MoveTowards(_t, _get() ? 1f : 0f, Time.unscaledDeltaTime * 7f);
            Apply();
        }

        private void Apply()
        {
            float e = _t * _t * (3f - 2f * _t);
            _knob.anchoredPosition = new Vector2(Mathf.Lerp(KnobOff, TrackW - KnobOff, e), 0f);
            _track.color = Color.Lerp(UiStyle.SqTrack, UiStyle.WidgetAccent, e);
            _state.text = _get() ? "włączone" : "wyłączone";
            _state.color = _get() ? UiStyle.WidgetAccent : UiKit.TextDim;
            if (_bg != null) _bg.color = Color.Lerp(_bg.color, _hover ? UiStyle.SqRowHover : UiStyle.SqRow, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 14f));
            if (_border != null) _border.color = Color.Lerp(_border.color, _hover ? UiStyle.WidgetHoverBorder : UiStyle.SqBorder, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 14f));
        }
    }
}
