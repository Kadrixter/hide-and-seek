using System;
using System.Collections.Generic;
using HideAndSeek.Game.Gfx;
using UnityEngine;
using UnityEngine.UI;

namespace HideAndSeek.Game.UI
{
    /// <summary>"Grafika" settings page (main menu and Esc menu), see <see cref="GfxSettings"/>.</summary>
    public static class GraphicsSettingsUi
    {
        public const float PanelHeight = 990f;

        private static readonly float[] ShadowDists = { 25f, 35f, 50f, 60f, 75f, 100f, 125f, 150f };
        private static readonly int[] LightCounts = { 1, 2, 3, 4, 6, 8 };

        public static void Build(Transform parent)
        {
            var refresher = parent.gameObject.GetComponent<GfxRowRefresher>();
            if (refresher == null) refresher = parent.gameObject.AddComponent<GfxRowRefresher>();
            Row(refresher, parent, "GfxProfile", "Profil", () =>
            {
                int p = GfxSettings.CurrentPreset();
                return p >= 0 ? GfxSettings.Presets[p].Name : "własny";
            }, s =>
            {
                int p = GfxSettings.CurrentPreset();
                int n = GfxSettings.Presets.Length;
                p = p < 0 ? (s > 0 ? 0 : n - 1) : (p + s + n) % n;
                GfxSettings.ApplyPreset(p);
            });
            Row(refresher, parent, "GfxAa", "Wygładzanie", () => GfxSettings.AaName(GfxSettings.Aa),
                s => GfxSettings.Aa = (AaMode)Cycle((int)GfxSettings.Aa, s, 8));
            Row(refresher, parent, "GfxAniso", "Filtrowanie tekstur", () => GfxSettings.Aniso == 0 ? "wyłączone" : GfxSettings.Aniso + "x",
                s => GfxSettings.Aniso = Step(GfxSettings.AnisoLevels, GfxSettings.Aniso, s));
            Row(refresher, parent, "GfxTex", "Jakość tekstur", () => GfxSettings.TextureName(GfxSettings.TextureLevel),
                s => GfxSettings.TextureLevel = Cycle(GfxSettings.TextureLevel, -s, 3));
            Row(refresher, parent, "GfxShadows", "Cienie", () => GfxSettings.ShadowName(GfxSettings.Shadows),
                s => GfxSettings.Shadows = Cycle(GfxSettings.Shadows, s, 5));
            Row(refresher, parent, "GfxShadowDist", "Zasięg cieni", () => $"{GfxSettings.ShadowDistance:0} m",
                s => GfxSettings.ShadowDistance = Step(ShadowDists, GfxSettings.ShadowDistance, s));
            Row(refresher, parent, "GfxLights", "Świateł na obiekt", () => GfxSettings.PixelLights.ToString(),
                s => GfxSettings.PixelLights = Step(LightCounts, GfxSettings.PixelLights, s));
            Row(refresher, parent, "GfxBloom", "Poświata lamp", () => GfxSettings.Bloom ? "włączona" : "wyłączona",
                _ => GfxSettings.Bloom = !GfxSettings.Bloom);
            Row(refresher, parent, "GfxRefl", "Odbicia (eksperym.)", () => GfxSettings.Reflections ? "włączone" : "wyłączone",
                _ => GfxSettings.Reflections = !GfxSettings.Reflections);
            Row(refresher, parent, "GfxVsync", "VSync", () => GfxSettings.VSync ? "włączona" : "wyłączona",
                _ => GfxSettings.VSync = !GfxSettings.VSync);
            Row(refresher, parent, "GfxFps", "Limit FPS", () => GfxSettings.VSync ? "VSync" : GfxSettings.FpsCap <= 0 ? "bez limitu" : GfxSettings.FpsCap.ToString(),
                s => GfxSettings.FpsCap = Step(GfxSettings.FpsCaps, GfxSettings.FpsCap, s));
            var fps = UiKit.Label("Fps", parent, "", 20, TextAnchor.MiddleLeft, UiKit.TextDim);
            fps.Height(28);
            refresher.Fps = fps;
        }

        private static void Row(GfxRowRefresher r, Transform parent, string name, string label, Func<string> value, Action<int> step)
        {
            var rt = UiStyle.Choice(name, parent, label, value, step, 22);
            rt.Height(50);
            var t = rt.Find("Value")?.GetComponent<Text>();
            if (t != null) r.Rows.Add((t, value));
        }

        private static int Cycle(int v, int s, int n) => ((v + s) % n + n) % n;

        private static int Step(int[] list, int v, int s)
        {
            int i = Array.IndexOf(list, v);
            if (i < 0) i = 0;
            return list[Cycle(i, s, list.Length)];
        }

        private static float Step(float[] list, float v, int s)
        {
            int best = 0;
            for (int i = 1; i < list.Length; i++)
                if (Mathf.Abs(list[i] - v) < Mathf.Abs(list[best] - v)) best = i;
            return list[Cycle(best, s, list.Length)];
        }
    }

    /// <summary>Keeps every value on the "Grafika" page current (a profile changes several rows at once) and shows the frame rate.</summary>
    public class GfxRowRefresher : MonoBehaviour
    {
        public readonly List<(Text text, Func<string> value)> Rows = new();
        public Text Fps;
        private float _next, _acc;
        private int _frames;

        private void Update()
        {
            _acc += Time.unscaledDeltaTime;
            _frames++;
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.5f;
            foreach (var (t, v) in Rows)
                if (t != null) t.text = v();
            if (Fps != null && _frames > 0)
            {
                float ms = _acc / _frames * 1000f;
                Fps.text = $"Teraz: {1000f / Mathf.Max(ms, 0.01f):0} FPS ({ms:0.0} ms na klatkę)";
            }
            _acc = 0f;
            _frames = 0;
        }
    }
}
