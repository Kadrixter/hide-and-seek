using System.Collections.Generic;
using HideAndSeek.Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HideAndSeek.Game.UI
{
    /// <summary>
    /// Small icon in the bottom-right corner of the main menu: which weapon the menu character holds. Shows the
    /// current choice ("?" = random); a click opens a row of icons to the left (?, knife, chainsaw, Glock).
    /// White icons, green on hover, a short tooltip above. The choice is saved in the player settings.
    /// </summary>
    public class MenuWeaponPicker : MonoBehaviour
    {
        private const float Size = 52f, Gap = 10f;
        private MainMenu _menu;
        private RectTransform _row;
        private Text _tip;
        private PickerIcon _current;
        private readonly List<PickerIcon> _options = new();
        private float _open, _openTarget;

        public bool IsOpen => _openTarget > 0.5f;

        private static readonly (MainMenu.MenuWeapon w, HotbarItem icon, string name)[] Choices =
        {
            (MainMenu.MenuWeapon.Random, HotbarItem.None, "losowo"),
            (MainMenu.MenuWeapon.Knife, HotbarItem.Knife, "nóż"),
            (MainMenu.MenuWeapon.Chainsaw, HotbarItem.Chainsaw, "piła"),
            (MainMenu.MenuWeapon.Glock, HotbarItem.Glock, "glock"),
        };

        public static MenuWeaponPicker Create(Transform root, MainMenu menu)
        {
            var rt = UiKit.Rect("WeaponPicker", root);
            rt.Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-44, 40), new Vector2(Size, Size));
            var p = rt.gameObject.AddComponent<MenuWeaponPicker>();
            p._menu = menu;
            p.Build(rt);
            return p;
        }

        private void Build(RectTransform rt)
        {
            _tip = UiKit.Label("Tip", rt, "", 16, TextAnchor.LowerRight, new Color(1f, 1f, 1f, 0f));
            _tip.font = UiFonts.SemiBold;
            _tip.horizontalOverflow = HorizontalWrapMode.Overflow;
            _tip.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 0), new Vector2(0, 12), new Vector2(420, 24));

            _row = UiKit.Rect("Options", rt);
            _row.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(1, 0), new Vector2(-Gap - 6, 0), new Vector2(Choices.Length * (Size + Gap) - Gap, Size));
            for (int i = 0; i < Choices.Length; i++)
            {
                var c = Choices[i];
                var icon = PickerIcon.Create(_row, c.icon, Size - 6f);
                ((RectTransform)icon.transform).Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(i * (Size + Gap), 3), new Vector2(Size - 6, Size - 6));
                icon.Tip = "Broń w menu: " + c.name;
                icon.Owner = this;
                var w = c.w;
                icon.Click = () => Choose(w);
                _options.Add(icon);
            }

            _current = PickerIcon.Create(rt, HotbarItem.None, Size);
            ((RectTransform)_current.transform).Stretch();
            _current.Owner = this;
            _current.Ring = true;
            _current.Click = () => _openTarget = IsOpen ? 0f : 1f;
            Refresh();
        }

        private void Choose(MainMenu.MenuWeapon w)
        {
            if (_menu != null) _menu.SetWeapon(w);
            else MainMenu.SavedWeapon = w;
            _openTarget = 0f;
            Refresh();
        }

        private MainMenu.MenuWeapon _shown = (MainMenu.MenuWeapon)(-1);

        private void Refresh()
        {
            var w = MainMenu.SavedWeapon;
            _shown = w;
            foreach (var c in Choices)
                if (c.w == w)
                {
                    _current.SetIcon(c.icon);
                    _current.Tip = "Broń w menu: " + c.name;
                }
            for (int i = 0; i < _options.Count; i++) _options[i].Selected = Choices[i].w == w;
        }

        /// <summary>Tests: open / close without a click.</summary>
        public void SetOpen(bool open) => _openTarget = open ? 1f : 0f;

        internal void ShowTip(string tip) => _hoverTip = tip;
        private string _hoverTip;

        private void Update()
        {
            if (MainMenu.SavedWeapon != _shown) Refresh(); // changed from code (tests)
            _open = Mathf.MoveTowards(_open, _openTarget, Time.unscaledDeltaTime / 0.16f);
            float e = _open * _open * (3f - 2f * _open);
            _row.gameObject.SetActive(_open > 0.001f);
            for (int i = 0; i < _options.Count; i++)
            {
                // slide out to the left one after another
                float k = Mathf.Clamp01(e * 1.6f - (_options.Count - 1 - i) * 0.15f);
                _options[i].Alpha = k;
                var r = (RectTransform)_options[i].transform;
                r.anchoredPosition = new Vector2(i * (Size + Gap) + (1f - k) * 30f, 3f);
            }
            string tip = _hoverTip ?? (IsOpen ? "Wybierz broń postaci w menu" : null);
            if (tip != null) _tip.text = tip;
            var tc = _tip.color;
            tc.a = Mathf.MoveTowards(tc.a, tip != null ? 0.6f : 0f, Time.unscaledDeltaTime * 5f);
            _tip.color = tc;
            // a click anywhere else closes the row
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (IsOpen && mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                var pos = mouse.position.ReadValue();
                if (!RectTransformUtility.RectangleContainsScreenPoint((RectTransform)transform, pos)
                    && !RectTransformUtility.RectangleContainsScreenPoint(_row, pos)) _openTarget = 0f;
            }
        }
    }

    /// <summary>One icon of the picker: white silhouette of the hotbar icon (or "?"), green on hover.</summary>
    public class PickerIcon : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public System.Action Click;
        public string Tip;
        public MenuWeaponPicker Owner;
        public bool Ring, Selected, ForceHover;
        public float Alpha = 1f;
        private Image _hit, _ring, _icon, _dot;
        private Text _q;
        private bool _hover;
        private float _h;

        private static readonly Dictionary<HotbarItem, Sprite> Silhouettes = new();

        public static PickerIcon Create(Transform parent, HotbarItem item, float size)
        {
            var hit = UiKit.Image("Icon", parent, new Color(0f, 0f, 0f, 0.35f));
            hit.sprite = UiStyle.Circle;
            var p = hit.gameObject.AddComponent<PickerIcon>();
            p._hit = hit;
            p._ring = UiKit.Image("Ring", hit.transform, new Color(1f, 1f, 1f, 0.35f));
            p._ring.sprite = RingSprite();
            p._ring.raycastTarget = false;
            p._ring.rectTransform.Stretch();
            p._icon = UiKit.Image("Glyph", hit.transform, Color.white);
            p._icon.raycastTarget = false;
            p._icon.preserveAspect = true;
            p._icon.rectTransform.Stretch(size * 0.2f);
            p._q = UiKit.Label("Q", hit.transform, "?", Mathf.RoundToInt(size * 0.55f), TextAnchor.MiddleCenter);
            p._q.font = UiFonts.Bold;
            p._q.rectTransform.Stretch();
            p._dot = UiKit.Image("Dot", hit.transform, UiKit.ButtonHover);
            p._dot.sprite = UiStyle.Circle;
            p._dot.raycastTarget = false;
            p._dot.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, -8f), new Vector2(6f, 6f));
            p.SetIcon(item);
            return p;
        }

        public void SetIcon(HotbarItem item)
        {
            bool q = item == HotbarItem.None;
            _q.gameObject.SetActive(q);
            _icon.gameObject.SetActive(!q);
            if (!q) _icon.sprite = Silhouette(item);
        }

        private static Sprite _ringSprite;

        private static Sprite RingSprite()
        {
            if (_ringSprite != null) return _ringSprite;
            const int n = 96;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var px = new Color32[n * n];
            float r = n / 2f;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = new Vector2(x + 0.5f - r, y + 0.5f - r).magnitude;
                float a = Mathf.Clamp01(r - d) * Mathf.Clamp01(d - (r - 2.5f));
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return _ringSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        }

        private static Sprite Silhouette(HotbarItem item)
        {
            if (Silhouettes.TryGetValue(item, out var s) && s != null) return s;
            var src = HotbarIcons.Get(item);
            if (src == null) return null;
            var t = src.texture;
            var px = t.GetPixels32();
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, px[i].a > 40 ? (byte)255 : (byte)0);
            var tex = new Texture2D(t.width, t.height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            tex.SetPixels32(px);
            tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 32f);
            Silhouettes[item] = s;
            return s;
        }

        public void OnPointerEnter(PointerEventData e)
        {
            _hover = true;
            GameAudio.Play2D(GameSfx.UiHover, 0.3f);
        }

        public void OnPointerExit(PointerEventData e)
        {
            _hover = false;
            if (Owner != null) Owner.ShowTip(null);
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (Alpha < 0.5f) return;
            GameAudio.Play2D(GameSfx.UiClick, 0.7f);
            Click?.Invoke();
        }

        private void Update()
        {
            bool on = (_hover || ForceHover) && Alpha > 0.5f;
            if (on && Owner != null) Owner.ShowTip(Tip);
            _h = Mathf.Lerp(_h, on ? 1f : 0f, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 16f));
            var fg = Color.Lerp(Color.white, UiKit.ButtonHover, _h);
            fg.a = Alpha;
            _icon.color = fg;
            _q.color = fg;
            _hit.color = new Color(0f, 0f, 0f, 0.38f * Alpha);
            var rc = Color.Lerp(new Color(1f, 1f, 1f, Ring ? 0.45f : 0.18f), UiKit.ButtonHover, _h);
            rc.a *= Alpha;
            _ring.color = rc;
            _dot.color = new Color(UiKit.ButtonHover.r, UiKit.ButtonHover.g, UiKit.ButtonHover.b, Selected ? Alpha : 0f);
            _hit.raycastTarget = Alpha > 0.5f;
            float s = 1f + 0.06f * _h;
            transform.localScale = new Vector3(s, s, 1f);
        }
    }
}
