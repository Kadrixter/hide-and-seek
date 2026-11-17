using System;
using HideAndSeek.Game.Net;
using UnityEngine;
using UnityEngine.UI;

namespace HideAndSeek.Game.UI
{
    /// <summary>Settings widgets shared by the main menu and the in-game Esc menu.</summary>
    public static class SettingsUi
    {
        /// <summary>Button that cycles borderless / fullscreen / windowed.</summary>
        public static Button DisplayModeButton(Transform parent)
        {
            Button b = null;
            b = UiKit.Button("Display", parent, Label(), () =>
            {
                var modes = LocalProfile.DisplayModes;
                int i = Array.IndexOf(modes, LocalProfile.DisplayMode);
                LocalProfile.DisplayMode = modes[(i + 1 + modes.Length) % modes.Length];
                b.GetComponentInChildren<Text>().text = Label();
            }, 26);
            b.Height(56);
            return b;

            static string Label() => "Tryb okna: " + LocalProfile.DisplayModeName(LocalProfile.DisplayMode);
        }

        public static void SensitivitySlider(Transform parent, Action<float> extra = null)
        {
            Row(parent, "Czułość myszy", 0.02f, 0.4f, LocalProfile.MouseSensitivity, v =>
            {
                LocalProfile.MouseSensitivity = v;
                extra?.Invoke(v);
            }, v => $"{v / 0.11f * 100f:0}%");
        }

        /// <summary>Full "Dźwięk" page: master, effects, steps, weapons, voices, ambient, music, UI, wall occlusion.</summary>
        public static void BuildAudio(Transform parent)
        {
            Row(parent, "Głośność ogólna", 0f, 1f, LocalProfile.MasterVolume, v => LocalProfile.MasterVolume = v, Pct);
            Row(parent, "Efekty (wszystkie)", 0f, 1f, LocalProfile.SfxVolume, v => LocalProfile.SfxVolume = v, Pct);
            Row(parent, "Kroki", 0f, 1.5f, LocalProfile.FootstepVolume, v => LocalProfile.FootstepVolume = v, Pct);
            Row(parent, "Broń (nóż, glock)", 0f, 1f, LocalProfile.WeaponVolume, v => LocalProfile.WeaponVolume = v, Pct);
            Row(parent, "Krzyki i oddech", 0f, 1f, LocalProfile.VoiceVolume, v => LocalProfile.VoiceVolume = v, Pct);
            Row(parent, "Otoczenie (wentylacja, kapanie, lampy)", 0f, 1.5f, LocalProfile.AmbientVolume, v => LocalProfile.AmbientVolume = v, Pct);
            Row(parent, "Muzyka i tło", 0f, 1f, LocalProfile.MusicVolume, v => LocalProfile.MusicVolume = v, Pct);
            Row(parent, "Interfejs", 0f, 1f, LocalProfile.UiVolume, v => LocalProfile.UiVolume = v, Pct);

            Button occ = null;
            occ = UiKit.Button("Occlusion", parent, OccLabel(), () =>
            {
                LocalProfile.Occlusion = !LocalProfile.Occlusion;
                occ.GetComponentInChildren<Text>().text = OccLabel();
            }, 24);
            occ.Height(50);

            static string OccLabel() => "Tłumienie dźwięku za ścianami: " + (LocalProfile.Occlusion ? "tak" : "nie");
        }

        /// <summary>"Czat głosowy" page: on/off, push-to-talk or open mic, device, gain, activation level, volume.</summary>
        public static void BuildVoice(Transform parent)
        {
            Toggle(parent, () => "Czat głosowy: " + (LocalProfile.VoiceEnabled ? "włączony" : "wyłączony"),
                () => { LocalProfile.VoiceEnabled = !LocalProfile.VoiceEnabled; Voice.VoiceChat.Instance?.Restart(); });
            Toggle(parent, () => "Tryb: " + (LocalProfile.VoiceOpenMic ? "otwarty mikrofon (aktywacja głosem)" : "naciśnij i mów (V)"),
                () => LocalProfile.VoiceOpenMic = !LocalProfile.VoiceOpenMic);
            Toggle(parent, () => "Mikrofon: " + MicName(),
                () =>
                {
                    var devs = Microphone.devices;
                    if (devs.Length == 0) return;
                    int i = Array.IndexOf(devs, LocalProfile.MicDevice);
                    LocalProfile.MicDevice = devs[(i + 1) % devs.Length];
                    Voice.VoiceChat.Instance?.Restart();
                });
            Row(parent, "Wzmocnienie mikrofonu", 0.5f, 4f, LocalProfile.MicGain, v => LocalProfile.MicGain = v, v => $"{v:0.0}x");
            Row(parent, "Próg aktywacji głosem", 0.02f, 0.5f, LocalProfile.MicThreshold, v => LocalProfile.MicThreshold = v, Pct);
            Row(parent, "Głośność innych graczy", 0f, 1.5f, LocalProfile.ChatVolume, v => LocalProfile.ChatVolume = v, Pct);

            var meter = UiKit.Label("Meter", parent, "", 22, TextAnchor.MiddleLeft, UiKit.TextDim);
            meter.Height(28);
            meter.gameObject.AddComponent<MicMeter>().Label = meter;

            static string MicName()
            {
                var devs = Microphone.devices;
                if (devs.Length == 0) return "brak";
                string d = LocalProfile.MicDevice;
                return Array.IndexOf(devs, d) >= 0 ? d : devs[0] + " (domyślny)";
            }
        }

        private static void Toggle(Transform parent, Func<string> label, Action click)
        {
            Button b = null;
            b = UiKit.Button("Toggle", parent, label(), () =>
            {
                click();
                b.GetComponentInChildren<Text>().text = label();
            }, 22);
            b.Height(46);
        }

        private static string Pct(float v) => $"{v * 100f:0}%";

        private static void Row(Transform parent, string label, float min, float max, float value, Action<float> set, Func<float, string> fmt)
        {
            var t = UiKit.Label("L", parent, "", 22);
            t.Height(26);
            void UpdateLabel(float v) => t.text = $"{label}: {fmt(v)}";
            UpdateLabel(value);
            UiKit.Slider("S", parent, min, max, value, false, v =>
            {
                set(v);
                UpdateLabel(v);
            }).Height(30);
        }
    }
}

namespace HideAndSeek.Game.UI
{
    /// <summary>Live microphone level in the voice settings.</summary>
    public class MicMeter : UnityEngine.MonoBehaviour
    {
        public UnityEngine.UI.Text Label;

        private void Update()
        {
            var vc = Voice.VoiceChat.Instance;
            if (Label == null) return;
            if (vc == null || !Net.LocalProfile.VoiceEnabled) { Label.text = "Mikrofon: wyłączony"; return; }
            if (!vc.MicAvailable) { Label.text = "Nie wykryto mikrofonu"; return; }
            int bars = UnityEngine.Mathf.RoundToInt(vc.MicLevel * 30f);
            int thr = UnityEngine.Mathf.RoundToInt(Net.LocalProfile.MicThreshold * 30f);
            var sb = new System.Text.StringBuilder("Poziom: ");
            for (int i = 0; i < 30; i++) sb.Append(i < bars ? '|' : (i == thr ? ':' : '.'));
            if (vc.Transmitting) sb.Append("  nadajesz");
            else if (!Net.LocalProfile.VoiceOpenMic) sb.Append("  (przytrzymaj V)");
            Label.text = sb.ToString();
        }
    }
}