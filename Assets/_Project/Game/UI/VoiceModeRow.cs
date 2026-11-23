using System;
using HideAndSeek.Game.Net;
using HideAndSeek.Game.Voice;
using UnityEngine;
using UnityEngine.UI;

namespace HideAndSeek.Game.UI
{
    /// <summary>
    /// "Ustawienia rundy" panel: one row with the voice chat mode toggle (klasyczny / niskie opóźnienie, see
    /// <see cref="VoiceMode"/>) and the voice delay slider in steps (<see cref="VoiceMode.Steps"/>). Switching the mode
    /// puts the delay on that mode's default. Only the host changes it; it is sent with the other round settings.
    /// </summary>
    public static class VoiceModeRow
    {
        /// <summary>Height the row adds to the panel (row + spacing).</summary>
        public const float Height = 76f;

        /// <summary>
        /// Adds the row to <paramref name="panel"/>. <paramref name="get"/> reads the edited settings,
        /// <paramref name="set"/> stores mode + delay and pushes them (null = view only, not the host).
        /// </summary>
        public static void Build(Transform panel, Func<NetSettings> get, Action<byte, ushort> set)
        {
            var row = UiKit.Rect("VoiceRow", panel);
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 10f;
            h.childControlHeight = true;
            h.childControlWidth = true;
            h.childForceExpandHeight = true;
            h.childForceExpandWidth = false;
            row.Height(68);

            var s0 = get();
            byte mode = s0.VoiceMode == VoiceMode.LowLatency ? VoiceMode.LowLatency : VoiceMode.Classic;
            int delay = VoiceMode.ClampDelay(s0.VoiceDelayMs, mode);

            Button modeB = null;
            Slider slider = null;
            Text sliderLabel = null, sliderValue = null;

            string ModeText(byte m) => "Czat głosowy:\n<color=#3fb8a8>" + VoiceMode.ModeName(m) + "</color>";
            string LabelText(byte m) => "Opóźnienie głosu (" + VoiceMode.DelayMeaning(m) + ")";

            modeB = UiKit.BoxButton("VoiceMode", row, ModeText(mode), () =>
            {
                if (set == null) return;
                mode = mode == VoiceMode.LowLatency ? VoiceMode.Classic : VoiceMode.LowLatency;
                delay = VoiceMode.DefaultFor(mode);
                modeB.GetComponentInChildren<Text>().text = ModeText(mode);
                sliderLabel.text = LabelText(mode);
                slider.SetValueWithoutNotify(VoiceMode.StepIndex(delay));
                sliderValue.text = delay + " ms";
                set(mode, (ushort)delay);
            }, 22);
            var le = modeB.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 270f;
            le.minWidth = 270f;
            le.flexibleWidth = 0f;
            var modeLabel = modeB.GetComponentInChildren<Text>();
            modeLabel.supportRichText = true;
            modeLabel.lineSpacing = 0.9f;

            slider = UiStyle.SliderRow(row, LabelText(mode), 0, VoiceMode.Steps.Length - 1, VoiceMode.StepIndex(delay), v =>
            {
                if (set == null) return;
                delay = VoiceMode.Steps[Mathf.Clamp(Mathf.RoundToInt(v), 0, VoiceMode.Steps.Length - 1)];
                set(mode, (ushort)delay);
            }, v => VoiceMode.Steps[Mathf.Clamp(Mathf.RoundToInt(v), 0, VoiceMode.Steps.Length - 1)] + " ms", 21);
            slider.wholeNumbers = true;
            var card = slider.transform.parent;
            var cardLe = card.GetComponent<LayoutElement>() ?? card.gameObject.AddComponent<LayoutElement>();
            cardLe.flexibleWidth = 1f;
            sliderLabel = card.Find("Label").GetComponent<Text>();
            sliderValue = card.Find("Value").GetComponent<Text>();

            modeB.interactable = set != null;
            slider.interactable = set != null;
            if (set != null) return;
            // Not the host: follow the live setting (the host may change it while this panel is open).
            var view = row.gameObject.AddComponent<VoiceModeRowView>();
            view.Show = (m, ms) =>
            {
                modeLabel.text = ModeText(m);
                sliderLabel.text = LabelText(m);
                slider.SetValueWithoutNotify(VoiceMode.StepIndex(ms));
                sliderValue.text = ms + " ms";
            };
        }
    }

    /// <summary>View-only voice row: refreshes from the session's settings every 0.25 s.</summary>
    public class VoiceModeRowView : MonoBehaviour
    {
        public Action<byte, int> Show;
        private float _next;
        private int _shown = -1;

        private void Update()
        {
            if (Time.unscaledTime < _next || MatchManager.Instance == null) return;
            _next = Time.unscaledTime + 0.25f;
            var s = MatchManager.Instance.Settings.Value;
            byte m = s.VoiceMode == VoiceMode.LowLatency ? VoiceMode.LowLatency : VoiceMode.Classic;
            int ms = VoiceMode.ClampDelay(s.VoiceDelayMs, m);
            if (m * 10000 + ms == _shown) return;
            _shown = m * 10000 + ms;
            Show?.Invoke(m, ms);
        }
    }
}
