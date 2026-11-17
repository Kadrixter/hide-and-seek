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
