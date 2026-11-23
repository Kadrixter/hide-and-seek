using System;
using HideAndSeek.Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HideAndSeek.Game.UI
{
    public static partial class UiKit
    {
        /// <summary>Hover colour of the text buttons (one place to change the menu accent).</summary>
        public static readonly Color ButtonHover = new Color(0.247f, 0.722f, 0.659f, 1f); // #3fb8a8 (morski, wybór użytkownika 23.11)
        /// <summary>Hover colour of the "danger" buttons (quit, leave, close server).</summary>
        public static readonly Color ButtonDanger = new Color(1f, 0.353f, 0.31f, 1f);  // #ff5a4f

        /// <summary>
        /// Menu button: white upper-case text without a box; on hover it turns <see cref="ButtonHover"/> (red for
        /// danger buttons), slides right and, on wide buttons, a short accent dash slides in on the left.
        /// <paramref name="color"/> only picks the kind (a red-ish colour = danger) - kept for the old call sites.
        /// </summary>
        public static Button Button(string name, Transform parent, string text, Action onClick, int fontSize = 30, Color? color = null)
        {
            var hit = Image(name, parent, new Color(0f, 0f, 0f, 0f)); // invisible hit area
            var b = hit.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            var nav = b.navigation;
            nav.mode = Navigation.Mode.None;
            b.navigation = nav;

            var dash = Image("Dash", hit.transform, ButtonHover);
            dash.raycastTarget = false;
            dash.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0), new Vector2(0, 3));

            var label = Label("Label", hit.transform, text, fontSize, TextAnchor.MiddleLeft, Text);
            label.font = UiFonts.SemiBold;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.rectTransform.Stretch();
            var drop = label.gameObject.AddComponent<Shadow>();
            drop.effectColor = new Color(0f, 0f, 0f, 0.55f);
            drop.effectDistance = new Vector2(0f, -2f);

            var fx = hit.gameObject.AddComponent<UiTextButtonFx>();
            fx.Label = label;
            fx.Dash = dash;
            fx.Danger = color.HasValue && color.Value.r > 0.28f && color.Value.r > color.Value.g * 2.5f;

            b.onClick.AddListener(() =>
            {
                GameAudio.Play2D(GameSfx.UiClick, 0.8f);
                onClick?.Invoke();
            });
            return b;
        }

        /// <summary>Short grey description for the text button (shown by menus that listen to <see cref="UiTextButtonFx.Hovered"/>).</summary>
        public static Button Describe(this Button b, string description)
        {
            var fx = b.GetComponent<UiTextButtonFx>();
            if (fx != null) fx.Description = description;
            return b;
        }

        /// <summary>Forces the label alignment of a text button (default: left when wide, centred when narrow).</summary>
        public static Button Align(this Button b, TextAnchor anchor)
        {
            var fx = b.GetComponent<UiTextButtonFx>();
            if (fx != null) fx.ForcedAnchor = anchor;
            return b;
        }
    }

    /// <summary>Hover / press animation of <see cref="UiKit.Button"/> (unscaled time: works in menus).</summary>
    public class UiTextButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        /// <summary>Fires with the description of the hovered button (null when the pointer leaves).</summary>
        public static event Action<string> Hovered;

        public Text Label;
        public Image Dash;
        public bool Danger;
        public string Description;
        public TextAnchor? ForcedAnchor;
        /// <summary>Tests (-menushots): draws the button as hovered.</summary>
        public bool ForceHover;

        private const float DashLen = 26f, DashGap = 14f, WideFrom = 340f;
        private Button _btn;
        private bool _hover, _down;
        private float _h;
        private string _shown;

        private void Awake() => _btn = GetComponent<Button>();

        public void OnPointerEnter(PointerEventData e)
        {
            _hover = true;
            if (_btn != null && _btn.interactable)
            {
                GameAudio.Play2D(GameSfx.UiHover, 0.35f);
                Hovered?.Invoke(Description);
            }
        }

        public void OnPointerExit(PointerEventData e)
        {
            _hover = _down = false;
            Hovered?.Invoke(null);
        }

        public void OnPointerDown(PointerEventData e) => _down = true;
        public void OnPointerUp(PointerEventData e) => _down = false;

        private void OnDisable()
        {
            if (_hover) Hovered?.Invoke(null);
            _hover = _down = false;
            _h = 0f;
            Apply(0f);
        }

        private void Update()
        {
            bool live = _btn == null || _btn.interactable;
            bool on = (_hover || ForceHover) && live;
            _h = Mathf.Lerp(_h, on ? 1f : 0f, 1f - Mathf.Exp(-Time.unscaledDeltaTime * (on ? 18f : 10f)));
            Apply(_h);
        }

        private void Apply(float h)
        {
            if (Label == null) return;
            // Upper case also for texts changed later by the owner (e.g. "Tryb: x").
            if (Label.text != _shown)
            {
                Label.text = Label.text.ToUpperInvariant();
                _shown = Label.text;
            }
            bool live = _btn == null || _btn.interactable;
            float w = ((RectTransform)transform).rect.width;
            var anchor = ForcedAnchor ?? (w >= WideFrom ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter);
            bool left = anchor == TextAnchor.MiddleLeft;
            if (Label.alignment != anchor) Label.alignment = anchor;

            float e = h * h * (3f - 2f * h);
            var hoverC = Danger ? UiKit.ButtonDanger : UiKit.ButtonHover;
            var c = Color.Lerp(UiKit.Text, hoverC, e);
            if (_down && live) c *= 0.85f;
            c.a = live ? 1f : 0.32f;
            Label.color = c;
            float x = left ? (DashLen + DashGap) * e : 5f * e;
            Label.rectTransform.offsetMin = new Vector2(x, _down && live ? -1.5f : 0f);
            Label.rectTransform.offsetMax = new Vector2(x, _down && live ? -1.5f : 0f);
            if (Dash != null)
            {
                Dash.color = new Color(hoverC.r, hoverC.g, hoverC.b, left ? e : 0f);
                Dash.rectTransform.sizeDelta = new Vector2(left ? DashLen * e : 0f, 3f);
            }
        }
    }
}
