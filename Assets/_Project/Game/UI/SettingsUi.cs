using System;
using HideAndSeek.Game.Net;
using UnityEngine;
using UnityEngine.UI;

namespace HideAndSeek.Game.UI
{
    /// <summary>Settings widgets shared by the main menu and the in-game Esc menu.</summary>
    public static class SettingsUi
    {
        /// <summary>Window mode picker: borderless / fullscreen / windowed.</summary>
        public static RectTransform DisplayModeButton(Transform parent)
        {
            var rt = UiStyle.Choice("Display", parent, "Tryb okna", () => LocalProfile.DisplayModeName(LocalProfile.DisplayMode), step =>
            {
                var modes = LocalProfile.DisplayModes;
                int i = Array.IndexOf(modes, LocalProfile.DisplayMode);
                LocalProfile.DisplayMode = modes[(i + step + modes.Length * 2) % modes.Length];
            });
            rt.Height(58);
            return rt;
        }

        /// <summary>Resolution picker: "natywna (WxH)" (default) or one of the monitor's modes (<see cref="DisplayDefaults"/>).</summary>
        public static RectTransform ResolutionButton(Transform parent)
        {
            var rt = UiStyle.Choice("Resolution", parent, "Rozdzielczość", () => DisplayDefaults.Label(DisplayDefaults.Chosen), step =>
            {
                var opts = DisplayDefaults.Options();
                int i = Array.IndexOf(opts, DisplayDefaults.Chosen);
                if (i < 0) i = 0;
                DisplayDefaults.Chosen = opts[(i + step + opts.Length * 2) % opts.Length];
            }, 22);
            rt.Height(58);
            return rt;
        }

        /// <summary>Crouch key mode switch (on = press to toggle, off = hold).</summary>
        public static RectTransform CrouchModeButton(Transform parent, Action<bool> extra = null)
        {
            var rt = UiStyle.Switch("CrouchMode", parent, "Kucanie przełączane (Ctrl / C)", () => LocalProfile.CrouchToggle, v =>
            {
                LocalProfile.CrouchToggle = v;
                extra?.Invoke(v);
            });
            rt.Height(58);
            return rt;
        }

        /// <summary>Discord rich presence on / off.</summary>
        public static RectTransform DiscordSwitch(Transform parent)
        {
            var rt = UiStyle.Switch("Discord", parent, "Status gry w Discordzie", () => LocalProfile.DiscordStatus, v => LocalProfile.DiscordStatus = v);
            rt.Height(58);
            return rt;
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

            UiStyle.Switch("Occlusion", parent, "Tłumienie dźwięku za ścianami", () => LocalProfile.Occlusion, v => LocalProfile.Occlusion = v).Height(54);
        }

        /// <summary>"Czat głosowy" page: on/off, push-to-talk or open mic, device, gain, activation level, volume.</summary>
        public static void BuildVoice(Transform parent)
        {
            UiStyle.Switch("VoiceOn", parent, "Czat głosowy", () => LocalProfile.VoiceEnabled, v =>
            {
                LocalProfile.VoiceEnabled = v;
                Voice.VoiceChat.Instance?.Restart();
            }).Height(52);
            UiStyle.Choice("VoiceMode", parent, "Tryb", () => LocalProfile.VoiceOpenMic ? "aktywacja głosem" : "naciśnij i mów (V)",
                _ => LocalProfile.VoiceOpenMic = !LocalProfile.VoiceOpenMic, 22).Height(52);
            UiStyle.Choice("Mic", parent, "Mikrofon", MicName, step =>
            {
                var devs = Microphone.devices;
                if (devs.Length == 0) return;
                int i = Array.IndexOf(devs, LocalProfile.MicDevice);
                LocalProfile.MicDevice = devs[(i + step + devs.Length * 2) % devs.Length];
                Voice.VoiceChat.Instance?.Restart();
            }, 22).Height(52);
            Row(parent, "Wzmocnienie mikrofonu", 0.5f, 4f, LocalProfile.MicGain, v => LocalProfile.MicGain = v, v => $"{v:0.0}x");
            Row(parent, "Próg aktywacji głosem", 0.05f, 0.8f, LocalProfile.MicThreshold, v => LocalProfile.MicThreshold = v, Pct);
            Row(parent, "Głośność innych graczy", 0f, 2f, LocalProfile.ChatVolume, v => LocalProfile.ChatVolume = v, Pct);

            var meter = UiKit.Label("Meter", parent, "", 22, TextAnchor.MiddleLeft, UiKit.TextDim);
            meter.Height(28);
            meter.gameObject.AddComponent<MicMeter>().Label = meter;

            static string MicName()
            {
                var devs = Microphone.devices;
                if (devs.Length == 0) return "brak";
                string d = LocalProfile.MicDevice;
                string n = Array.IndexOf(devs, d) >= 0 ? d : devs[0];
                return n.Length > 17 ? n.Substring(0, 16).TrimEnd() + "..." : n;
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
            UiStyle.SliderRow(parent, label, min, max, value, set, fmt);
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
            Label.supportRichText = true;
            string meter = Game.GameHud.MeterText(vc.MicLevel, 30).Replace("MÓWISZ  ", "Poziom: ");
            if (vc.Transmitting) meter += "  nadajesz";
            else if (!Net.LocalProfile.VoiceOpenMic) meter += "  (przytrzymaj V)";
            Label.text = meter;
        }
    }
}