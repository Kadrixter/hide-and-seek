using System;
using HideAndSeek.Game.Net;
using HideAndSeek.Player;
using UnityEngine;
using UnityEngine.UI;

namespace HideAndSeek.Game.UI
{
    /// <summary>
    /// "Pad" settings pages (main menu and Esc menu): connected pad, live test view (buttons light up, both sticks as
    /// circles with a dot, trigger bars, vibration test), stick dead zone, look speed X / Y, invert Y, vibration on / off,
    /// and "Przyciski pada": every <see cref="PadAction"/> with its button; click a row, then press the new button on the
    /// pad (L1 held = L1 layer; Esc / PS / 6 s cancel; a taken button is swapped). Plain uGUI widgets, so the pad
    /// cursor (<see cref="PadCursor"/>, D-pad jumps between rows, Cross clicks) operates the pages like every menu.
    /// </summary>
    public static class PadSettingsUi
    {
        public const float PanelHeight = 980f, BindPanelHeight = 940f;

        /// <summary>Main pad page: status, test view, sliders, switches, vibration test, "Przyciski pada" button.</summary>
        public static void Build(Transform parent, Action openBindings)
        {
            var status = UiKit.Label("PadStatus", parent, "", 22, TextAnchor.MiddleLeft, UiKit.TextDim);
            status.Height(30);
            status.gameObject.AddComponent<PadStatusText>().Label = status;

            PadTestView.Create(parent).Height(236);

            UiStyle.SliderRow(parent, "Martwa strefa gałek", 0.02f, 0.45f, PadInput.Deadzone, v => PadInput.Deadzone = v, v => $"{v * 100f:0}%");
            UiStyle.SliderRow(parent, "Czułość patrzenia - poziomo", 0.2f, 3f, PadInput.SensX, v => PadInput.SensX = v, v => $"{v * 100f:0}%");
            UiStyle.SliderRow(parent, "Czułość patrzenia - pionowo", 0.2f, 3f, PadInput.SensY, v => PadInput.SensY = v, v => $"{v * 100f:0}%");
            UiStyle.Switch("PadInvertY", parent, "Odwróć oś Y prawej gałki", () => PadInput.InvertY, v => PadInput.InvertY = v).Height(52);
            UiStyle.Switch("PadRumble", parent, "Wibracje", () => PadInput.RumbleEnabled, v => PadInput.RumbleEnabled = v).Height(52);
            UiStyle.ActionRow("PadRumbleTest", parent, "Test wibracji", "Wibracja", () =>
            {
                if (!PadInput.Connected) GameHud.Toast("Nie wykryto pada.", 2f);
                else if (!PadInput.RumbleEnabled) GameHud.Toast("Wibracje są wyłączone.", 2f);
                else PadInput.Rumble(0.55f, 0.9f, 0.6f);
            }).Height(52);
            UiKit.Button("PadBindings", parent, "Przyciski pada", openBindings, 30).Height(54);
        }

        /// <summary>"Przyciski pada" page: capture message, two columns of action rows, reset to defaults.</summary>
        public static void BuildBindings(Transform parent)
        {
            var msg = UiKit.Label("PadBindMsg", parent, "", 20, TextAnchor.MiddleLeft, UiKit.TextDim);
            msg.Height(52);
            msg.gameObject.AddComponent<PadCaptureText>().Label = msg;

            var cols = UiKit.Rect("PadBindCols", parent);
            int n = PadInput.ActionCount, half = (n + 1) / 2;
            const float rowH = 40f, gap = 4f;
            cols.Height(half * (rowH + gap));
            var h = cols.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 10f;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = h.childForceExpandHeight = true;
            for (int c = 0; c < 2; c++)
            {
                var col = UiKit.Rect("Col" + c, cols);
                UiKit.VStack(col, gap);
                for (int i = c * half; i < Mathf.Min(n, (c + 1) * half); i++)
                {
                    var a = (PadAction)i;
                    var b = UiKit.BoxButton("Bind" + a, col, "", () => PadBindRow.Click(a), 19);
                    b.Height(rowH);
                    var row = b.gameObject.AddComponent<PadBindRow>();
                    row.Action = a;
                    row.Label = b.transform.Find("Label")?.GetComponent<Text>();
                    if (row.Label != null) row.Label.alignment = TextAnchor.MiddleLeft;
                }
            }
            UiKit.Button("PadReset", parent, "Przywróć domyślne", () =>
            {
                PadInput.CancelCapture();
                PadInput.ResetBindings();
                Prefs.Save();
                GameHud.Toast("Przyciski pada: domyślne.", 2f);
            }, 28).Height(52);
        }
    }

    /// <summary>"Pad: DualSense (PS5)" / "nie wykryto pada".</summary>
    public sealed class PadStatusText : MonoBehaviour
    {
        public Text Label;

        private void Update()
        {
            if (Label == null) return;
            Label.text = PadInput.Connected
                ? $"Podłączony pad: {PadInput.Name}" + (PadInput.UsingPad ? "  (w użyciu)" : "")
                : "Nie wykryto pada. Podłącz DualShock 4 / DualSense przez USB lub Bluetooth.";
        }
    }

    /// <summary>Capture prompt / result above the rebind rows.</summary>
    public sealed class PadCaptureText : MonoBehaviour
    {
        public Text Label;

        private void Update()
        {
            if (Label == null) return;
            if (PadInput.Capturing is PadAction a)
            {
                float left = PadInput.CaptureTimeout - (Time.unscaledTime - PadInput.CaptureStarted);
                Label.text = $"{PadInput.ActionName(a)}: naciśnij przycisk na padzie... (L1 + przycisk = druga warstwa, Esc / PS = anuluj, {Mathf.CeilToInt(Mathf.Max(0f, left))} s)";
                Label.color = UiStyle.WidgetAccent;
                return;
            }
            Label.color = UiKit.TextDim;
            Label.text = !string.IsNullOrEmpty(PadInput.LastCaptureMessage) ? PadInput.LastCaptureMessage
                : PadInput.Connected ? "Kliknij akcję i naciśnij nowy przycisk. Zajęty przycisk zamieni się miejscami. L1 = przycisk drugiej warstwy (L1 + przycisk)."
                : "Nie wykryto pada - podłącz go, żeby zmienić przyciski.";
        }

        private void OnDisable() => PadInput.CancelCapture();
    }

    /// <summary>One rebind row: "Skok ........ ✕"; click starts the capture.</summary>
    public sealed class PadBindRow : MonoBehaviour
    {
        public PadAction Action;
        public Text Label;
        private static bool _froze;

        public static void Click(PadAction a)
        {
            if (PadInput.CaptureOwnsPad) return; // the press that just ended a capture must not start the next one
            if (!PadInput.Connected) { GameHud.Toast("Nie wykryto pada.", 2f); return; }
            PadInput.BeginCapture(a);
        }

        private void Update()
        {
            if (Label != null)
            {
                bool cap = PadInput.Capturing == Action;
                string btn = cap ? "<b>...</b>" : PadInput.Label(Action);
                Label.supportRichText = true;
                Label.text = $"  {PadInput.ActionName(Action)}<color=#9fe6a8>   {btn}</color>";
            }
            // While a capture runs the pad cursor stands still and does not click (the next press is the binding).
            bool capturing = PadInput.Capturing != null;
            if (capturing != _froze)
            {
                _froze = capturing;
                PadCursor.Frozen = capturing;
            }
        }

        private void OnDisable()
        {
            if (_froze) { _froze = false; PadCursor.Frozen = false; }
        }
    }

    /// <summary>
    /// Live pad test: face buttons, shoulders, D-pad, Options / Share / touchpad / PS light up when held; both sticks
    /// drawn as circles with a dot (raw position, dead zone ring), L2 / R2 bars.
    /// </summary>
    public sealed class PadTestView : MonoBehaviour
    {
        private static readonly Color Off = new Color(1f, 1f, 1f, 0.08f), On = new Color(0.45f, 0.95f, 0.55f, 0.95f);

        private struct Cap { public PadButton Button; public Image Img; public Text Text; }
        private readonly System.Collections.Generic.List<Cap> _caps = new();
        private RectTransform _lsDot, _rsDot, _lsDz, _rsDz;
        private Image _lsBg, _rsBg, _l2Fill, _r2Fill;
        private Text _l2Text, _r2Text;
        private PadKind _kindShown = (PadKind)(-1);

        public static RectTransform Create(Transform parent)
        {
            var bg = UiKit.Image("PadTest", parent, new Color(0f, 0f, 0f, 0.35f));
            UiStyle.SquareBorder("Border", bg.transform, UiStyle.SqBorder);
            var v = bg.gameObject.AddComponent<PadTestView>();
            v.Build(bg.rectTransform);
            return bg.rectTransform;
        }

        private void Build(RectTransform root)
        {
            // Layout in a 680 x 236 box (anchored at the centre, so it stays centred in wider columns).
            Stick(root, new Vector2(-250f, -28f), out _lsBg, out _lsDot, out _lsDz);
            Stick(root, new Vector2(250f, -28f), out _rsBg, out _rsDot, out _rsDz);
            Trigger(root, new Vector2(-250f, 92f), out _l2Fill, out _l2Text);
            Trigger(root, new Vector2(250f, 92f), out _r2Fill, out _r2Text);
            Button(root, PadButton.L1, new Vector2(-150f, 92f), 54f);
            Button(root, PadButton.R1, new Vector2(150f, 92f), 54f);
            Button(root, PadButton.Share, new Vector2(-60f, 92f), 74f);
            Button(root, PadButton.Options, new Vector2(60f, 92f), 74f);
            Button(root, PadButton.Touchpad, new Vector2(0f, 50f), 110f);
            Button(root, PadButton.Home, new Vector2(0f, 8f), 44f);
            // D-pad (left) and face buttons (right), between the sticks and the centre.
            Button(root, PadButton.DpadUp, new Vector2(-120f, 12f), 36f);
            Button(root, PadButton.DpadDown, new Vector2(-120f, -68f), 36f);
            Button(root, PadButton.DpadLeft, new Vector2(-160f, -28f), 36f);
            Button(root, PadButton.DpadRight, new Vector2(-80f, -28f), 36f);
            Button(root, PadButton.Triangle, new Vector2(120f, 12f), 36f);
            Button(root, PadButton.Cross, new Vector2(120f, -68f), 36f);
            Button(root, PadButton.Square, new Vector2(80f, -28f), 36f);
            Button(root, PadButton.Circle, new Vector2(160f, -28f), 36f);
            Button(root, PadButton.L3, new Vector2(-250f, -100f), 44f);
            Button(root, PadButton.R3, new Vector2(250f, -100f), 44f);
        }

        private static RectTransform At(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
            return rt;
        }

        private void Button(RectTransform root, PadButton b, Vector2 pos, float w)
        {
            var img = UiKit.Image("B" + b, root, Off).Rounded(true);
            img.raycastTarget = false;
            At(img.rectTransform, pos, new Vector2(w, 34f));
            var t = UiKit.Label("T", img.transform, "", 18, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            t.rectTransform.Stretch();
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            _caps.Add(new Cap { Button = b, Img = img, Text = t });
        }

        private static void Stick(RectTransform root, Vector2 pos, out Image bg, out RectTransform dot, out RectTransform dz)
        {
            bg = UiKit.Image("Stick", root, new Color(1f, 1f, 1f, 0.07f));
            bg.sprite = UiStyle.Circle;
            bg.raycastTarget = false;
            At(bg.rectTransform, pos, new Vector2(116f, 116f));
            var dzi = UiKit.Image("Deadzone", bg.transform, new Color(1f, 1f, 1f, 0.1f));
            dzi.sprite = UiStyle.Circle;
            dzi.raycastTarget = false;
            dz = At(dzi.rectTransform, Vector2.zero, new Vector2(20f, 20f));
            var d = UiKit.Image("Dot", bg.transform, On);
            d.sprite = UiStyle.Circle;
            d.raycastTarget = false;
            dot = At(d.rectTransform, Vector2.zero, new Vector2(22f, 22f));
        }

        private static void Trigger(RectTransform root, Vector2 pos, out Image fill, out Text text)
        {
            var bg = UiKit.Image("Trigger", root, Off);
            bg.raycastTarget = false;
            At(bg.rectTransform, pos, new Vector2(120f, 34f));
            fill = UiKit.Image("Fill", bg.transform, On);
            fill.raycastTarget = false;
            fill.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(0f, 0f));
            text = UiKit.Label("T", bg.transform, "", 18, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            text.rectTransform.Stretch();
        }

        private void Update()
        {
            if (PadInput.Kind != _kindShown)
            {
                _kindShown = PadInput.Kind;
                foreach (var c in _caps) c.Text.text = PadInput.ButtonLabel(c.Button);
            }
            bool any = PadInput.Connected;
            foreach (var c in _caps) c.Img.color = any && PadInput.ButtonHeld(c.Button) ? On : Off;
            Place(_lsDot, _lsDz, PadInput.LeftStickRaw, PadInput.ButtonHeld(PadButton.L3), _lsBg);
            Place(_rsDot, _rsDz, PadInput.RightStickRaw, PadInput.ButtonHeld(PadButton.R3), _rsBg);
            Bar(_l2Fill, _l2Text, PadInput.TriggerL, PadInput.Kind == PadKind.Other ? "LT" : "L2");
            Bar(_r2Fill, _r2Text, PadInput.TriggerR, PadInput.Kind == PadKind.Other ? "RT" : "R2");
        }

        private static void Place(RectTransform dot, RectTransform dz, Vector2 v, bool pressed, Image bg)
        {
            const float r = 47f;
            dot.anchoredPosition = Vector2.ClampMagnitude(v, 1f) * r;
            float d = PadInput.Deadzone * 2f * (r + 11f);
            dz.sizeDelta = new Vector2(d, d);
            bg.color = pressed ? new Color(0.45f, 0.95f, 0.55f, 0.3f) : new Color(1f, 1f, 1f, 0.07f);
        }

        private static void Bar(Image fill, Text text, float v, string name)
        {
            fill.rectTransform.sizeDelta = new Vector2(120f * Mathf.Clamp01(v), 0f);
            text.text = $"{name} {v * 100f:0}%";
        }
    }
}
