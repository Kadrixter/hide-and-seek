using System;
using HideAndSeek.Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace HideAndSeek.Game.UI
{
    /// <summary>Tiny code-only uGUI toolkit: consistent dark style for menu, lobby and HUD.</summary>
    public static class UiKit
    {
        public static readonly Color Bg = new Color(0.04f, 0.045f, 0.05f, 0.92f);
        public static readonly Color Panel = new Color(0.08f, 0.09f, 0.1f, 0.94f);
        public static readonly Color ButtonColor = new Color(0.16f, 0.17f, 0.19f, 1f);
        public static readonly Color ButtonHoverColor = new Color(0.24f, 0.26f, 0.29f, 1f);
        public static readonly Color Accent = new Color(0.36f, 0.85f, 0.42f, 1f);
        public static readonly Color Danger = new Color(0.9f, 0.25f, 0.22f, 1f);
        public static readonly Color Text = new Color(0.9f, 0.9f, 0.88f, 1f);
        public static readonly Color TextDim = new Color(0.6f, 0.62f, 0.64f, 1f);
        public static readonly Color SeekerColor = new Color(0.95f, 0.3f, 0.25f, 1f);
        public static readonly Color HiderColor = new Color(0.45f, 0.8f, 1f, 1f);

        private static Font _font;
        public static Font Font => _font != null ? _font : (_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        public static Canvas CreateCanvas(string name, int sortOrder, Transform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null) go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            UnityEngine.Object.DontDestroyOnLoad(es);
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Place(this RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 pos, Vector2 size)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(this RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        public static Image Image(string name, Transform parent, Color color)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        public static Text Label(string name, Transform parent, string text, int size = 28, TextAnchor anchor = TextAnchor.MiddleLeft,
            Color? color = null, FontStyle style = FontStyle.Normal)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color ?? Text;
            t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static Outline AddOutline(this Graphic g, float d = 2f, float alpha = 0.85f)
        {
            var o = g.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, alpha);
            o.effectDistance = new Vector2(d, -d);
            return o;
        }

        public static Button Button(string name, Transform parent, string text, Action onClick, int fontSize = 30, Color? color = null)
        {
            var img = Image(name, parent, color ?? ButtonColor);
            var b = img.gameObject.AddComponent<Button>();
            var colors = b.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.35f, 1.35f, 1.35f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.selectedColor = Color.white;
            colors.colorMultiplier = 1.5f;
            b.colors = colors;
            var label = Label("Label", img.transform, text, fontSize, TextAnchor.MiddleCenter);
            label.rectTransform.Stretch(8f);
            b.onClick.AddListener(() =>
            {
                GameAudio.Play2D(GameSfx.UiClick, 0.8f);
                onClick?.Invoke();
            });
            var trigger = img.gameObject.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => { if (b.interactable) GameAudio.Play2D(GameSfx.UiHover, 0.35f); });
            trigger.triggers.Add(enter);
            return b;
        }

        public static InputField Input(string name, Transform parent, string value, string placeholder, int fontSize = 28)
        {
            var img = Image(name, parent, new Color(0.02f, 0.02f, 0.025f, 1f));
            var field = img.gameObject.AddComponent<InputField>();
            var text = Label("Text", img.transform, value, fontSize, TextAnchor.MiddleLeft);
            text.rectTransform.Stretch(12f);
            text.supportRichText = false;
            var ph = Label("Placeholder", img.transform, placeholder, fontSize, TextAnchor.MiddleLeft, TextDim, FontStyle.Italic);
            ph.rectTransform.Stretch(12f);
            field.textComponent = text;
            field.placeholder = ph;
            field.text = value;
            field.caretWidth = 2;
            return field;
        }

        public static Slider Slider(string name, Transform parent, float min, float max, float value, bool whole, Action<float> onChange)
        {
            var root = Rect(name, parent);
            var bg = Image("Bg", root, new Color(0.02f, 0.02f, 0.025f, 1f));
            bg.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0, 10));
            var fillArea = Rect("FillArea", root);
            fillArea.Place(new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0, 10));
            var fill = Image("Fill", fillArea, Accent);
            fill.rectTransform.Stretch();
            var handleArea = Rect("HandleArea", root);
            handleArea.Stretch();
            var handle = Image("Handle", handleArea, Text);
            handle.rectTransform.sizeDelta = new Vector2(18, 30);
            var s = root.gameObject.AddComponent<Slider>();
            s.fillRect = fill.rectTransform;
            s.handleRect = handle.rectTransform;
            s.targetGraphic = handle;
            s.minValue = min;
            s.maxValue = max;
            s.wholeNumbers = whole;
            s.value = value;
            s.onValueChanged.AddListener(v => onChange?.Invoke(v));
            return s;
        }

        public static VerticalLayoutGroup VStack(RectTransform rt, float spacing, int padding = 0)
        {
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = new RectOffset(padding, padding, padding, padding);
            v.childControlHeight = true;
            v.childControlWidth = true;
            v.childForceExpandHeight = false;
            v.childForceExpandWidth = true;
            return v;
        }

        public static LayoutElement Height(this Component c, float h)
        {
            var le = c.gameObject.GetComponent<LayoutElement>() ?? c.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = h;
            le.minHeight = h;
            return le;
        }

        public static string FormatTime(double seconds)
        {
            if (seconds < 0) seconds = 0;
            int s = Mathf.CeilToInt((float)seconds);
            return $"{s / 60}:{s % 60:00}";
        }
    }
}
