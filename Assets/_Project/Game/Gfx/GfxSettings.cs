using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game.Gfx
{
    /// <summary>Edge smoothing: post filters (FXAA / SMAA, <see cref="GfxPost"/>), hardware MSAA, or both.</summary>
    public enum AaMode : byte { Off, Fxaa, Smaa, Msaa2, Msaa4, Msaa8, Msaa4Smaa, Msaa8Smaa }

    /// <summary>
    /// "Grafika" settings (saved in Documents\HideAndSeek\ustawienia.txt, keys hns.gfx.*) and applying them:
    /// anti-aliasing, anisotropic filtering, texture resolution, shadows (quality + distance), bloom, vertical sync,
    /// frame cap, plus the per-profile lights / LOD values. Applied once before the first scene and on every change.
    /// Profiles Niska / Średnia / Wysoka / Ultra set everything at once; any change afterwards shows "własny".
    /// Default (nothing saved): Wysoka.
    /// </summary>
    public static class GfxSettings
    {
        public struct Preset
        {
            public string Name;
            public AaMode Aa;
            public int Aniso, Tex, Shadows, Lights;
            public float ShadowDist, Lod;
            public bool Bloom;
        }

        public static readonly Preset[] Presets =
        {
            new Preset { Name = "Niska", Aa = AaMode.Fxaa, Aniso = 2, Tex = 1, Shadows = 1, ShadowDist = 35f, Bloom = false, Lights = 2, Lod = 1f },
            new Preset { Name = "Średnia", Aa = AaMode.Smaa, Aniso = 4, Tex = 0, Shadows = 2, ShadowDist = 60f, Bloom = true, Lights = 3, Lod = 1.5f },
            new Preset { Name = "Wysoka", Aa = AaMode.Msaa4Smaa, Aniso = 8, Tex = 0, Shadows = 3, ShadowDist = 100f, Bloom = true, Lights = 4, Lod = 2f },
            new Preset { Name = "Ultra", Aa = AaMode.Msaa8Smaa, Aniso = 16, Tex = 0, Shadows = 4, ShadowDist = 150f, Bloom = true, Lights = 6, Lod = 2.5f },
        };

        public const int DefaultPreset = 2;
        public static readonly int[] AnisoLevels = { 0, 2, 4, 8, 16 };
        public static readonly int[] FpsCaps = { 0, 30, 60, 75, 90, 120, 144, 165, 240 };

        /// <summary>Raised after anything was applied (the post-effect driver re-checks its cameras).</summary>
        public static event System.Action Changed;

        private static Preset Def => Presets[DefaultPreset];

        // ---------------------------------------------------------------- stored values

        public static AaMode Aa { get => (AaMode)Mathf.Clamp(Prefs.GetInt("hns.gfx.aa", (int)Def.Aa), 0, 7); set => SetI("hns.gfx.aa", (int)value); }
        public static int Aniso { get => Prefs.GetInt("hns.gfx.aniso", Def.Aniso); set => SetI("hns.gfx.aniso", value); }
        /// <summary>0 = full, 1 = half, 2 = quarter resolution textures.</summary>
        public static int TextureLevel { get => Mathf.Clamp(Prefs.GetInt("hns.gfx.tex", Def.Tex), 0, 2); set => SetI("hns.gfx.tex", value); }
        /// <summary>0 off, 1 low (hard, 512), 2 medium, 3 high, 4 very high (soft).</summary>
        public static int Shadows { get => Mathf.Clamp(Prefs.GetInt("hns.gfx.shadows", Def.Shadows), 0, 4); set => SetI("hns.gfx.shadows", value); }
        public static float ShadowDistance { get => Prefs.GetFloat("hns.gfx.shadowDist", Def.ShadowDist); set { Prefs.SetFloat("hns.gfx.shadowDist", value); Apply(); } }
        public static bool Bloom { get => Prefs.GetInt("hns.gfx.bloom", Def.Bloom ? 1 : 0) != 0; set => SetI("hns.gfx.bloom", value ? 1 : 0); }
        public static bool VSync { get => Prefs.GetInt("hns.gfx.vsync", 1) != 0; set => SetI("hns.gfx.vsync", value ? 1 : 0); }
        /// <summary>Frame cap without vsync (0 = none).</summary>
        public static int FpsCap { get => Prefs.GetInt("hns.gfx.fps", 0); set => SetI("hns.gfx.fps", value); }
        public static int PixelLights { get => Prefs.GetInt("hns.gfx.lights", Def.Lights); set => SetI("hns.gfx.lights", value); }
        public static float LodBias { get => Prefs.GetFloat("hns.gfx.lod", Def.Lod); set { Prefs.SetFloat("hns.gfx.lod", value); Apply(); } }
        /// <summary>Experimental reflections in the garage (<see cref="GarageReflections"/>), off by default and not part of the profiles.</summary>
        public static bool Reflections { get => Prefs.GetInt("hns.gfx.refl", 0) != 0; set => SetI("hns.gfx.refl", value ? 1 : 0); }

        private static void SetI(string key, int v)
        {
            Prefs.SetInt(key, v);
            Apply();
        }

        // ---------------------------------------------------------------- derived

        public static int MsaaSamples(AaMode m) => m switch
        {
            AaMode.Msaa2 => 2,
            AaMode.Msaa4 or AaMode.Msaa4Smaa => 4,
            AaMode.Msaa8 or AaMode.Msaa8Smaa => 8,
            _ => 1,
        };

        /// <summary>Post-process filter that runs on the final image (0 none, 1 FXAA, 2 SMAA).</summary>
        public static int PostFilter(AaMode m) => m switch
        {
            AaMode.Fxaa => 1,
            AaMode.Smaa or AaMode.Msaa4Smaa or AaMode.Msaa8Smaa => 2,
            _ => 0,
        };

        public static string AaName(AaMode m) => m switch
        {
            AaMode.Off => "wyłączone",
            AaMode.Fxaa => "FXAA",
            AaMode.Smaa => "SMAA",
            AaMode.Msaa2 => "MSAA 2x",
            AaMode.Msaa4 => "MSAA 4x",
            AaMode.Msaa8 => "MSAA 8x",
            AaMode.Msaa4Smaa => "MSAA 4x + SMAA",
            _ => "MSAA 8x + SMAA",
        };

        public static string ShadowName(int q) => q switch
        {
            0 => "wyłączone",
            1 => "niska",
            2 => "średnia",
            3 => "wysoka",
            _ => "bardzo wysoka",
        };

        public static string TextureName(int t) => t switch { 0 => "pełna", 1 => "połowa", _ => "ćwierć" };

        /// <summary>Index of the profile matching the current values, -1 = custom.</summary>
        public static int CurrentPreset()
        {
            for (int i = 0; i < Presets.Length; i++)
            {
                var p = Presets[i];
                if (p.Aa == Aa && p.Aniso == Aniso && p.Tex == TextureLevel && p.Shadows == Shadows && Mathf.Abs(p.ShadowDist - ShadowDistance) < 0.5f &&
                    p.Bloom == Bloom && p.Lights == PixelLights && Mathf.Abs(p.Lod - LodBias) < 0.01f) return i;
            }
            return -1;
        }

        public static void ApplyPreset(int i)
        {
            var p = Presets[Mathf.Clamp(i, 0, Presets.Length - 1)];
            Prefs.SetInt("hns.gfx.aa", (int)p.Aa);
            Prefs.SetInt("hns.gfx.aniso", p.Aniso);
            Prefs.SetInt("hns.gfx.tex", p.Tex);
            Prefs.SetInt("hns.gfx.shadows", p.Shadows);
            Prefs.SetFloat("hns.gfx.shadowDist", p.ShadowDist);
            Prefs.SetInt("hns.gfx.bloom", p.Bloom ? 1 : 0);
            Prefs.SetInt("hns.gfx.lights", p.Lights);
            Prefs.SetFloat("hns.gfx.lod", p.Lod);
            MirrorSettings.Quality = MirrorSettings.ForPreset(Mathf.Clamp(i, 0, Presets.Length - 1)); // the lobby mirror follows the profile
            Apply();
        }

        // ---------------------------------------------------------------- apply

        /// <summary>Test override (-testgfx): AA mode used instead of the saved one (null = saved).</summary>
        public static AaMode? TestAa;
        public static bool? TestBloom;
        public static bool? TestReflections;
        public static bool EffectiveReflections => TestReflections ?? Reflections;

        public static AaMode EffectiveAa => TestAa ?? Aa;
        public static bool EffectiveBloom => TestBloom ?? Bloom;

        public static void Apply()
        {
            var aa = EffectiveAa;
            int msaa = MsaaSamples(aa);
            QualitySettings.antiAliasing = msaa > 1 ? msaa : 0;

            int aniso = Aniso;
            if (aniso > 0)
            {
                QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
                Texture.SetGlobalAnisotropicFilteringLimits(aniso, aniso);
            }
            else QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;

            QualitySettings.globalTextureMipmapLimit = TextureLevel;

            int sh = Shadows;
            QualitySettings.shadows = sh == 0 ? ShadowQuality.Disable : sh == 1 ? ShadowQuality.HardOnly : ShadowQuality.All;
            QualitySettings.shadowResolution = sh <= 1 ? ShadowResolution.Low : sh == 2 ? ShadowResolution.Medium : sh == 3 ? ShadowResolution.High : ShadowResolution.VeryHigh;
            QualitySettings.shadowDistance = ShadowDistance;
            QualitySettings.shadowCascades = sh >= 3 ? 4 : 2;

            QualitySettings.pixelLightCount = PixelLights;
            QualitySettings.lodBias = LodBias;

            bool vs = VSync;
            QualitySettings.vSyncCount = vs ? 1 : 0;
            int cap = FpsCap;
            Application.targetFrameRate = vs || cap <= 0 ? -1 : cap;
            if (CmdFps > 0)
            {
                // -fps N on the command line (test windows): a hard cap over the player's own settings.
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = CmdFps;
            }

            Changed?.Invoke();
        }

        private static int _cmdFps = -1;

        /// <summary>-fps N from the command line (0 = none): caps test windows so several of them do not eat the PC.</summary>
        public static int CmdFps
        {
            get
            {
                if (_cmdFps >= 0) return _cmdFps;
                var a = System.Environment.GetCommandLineArgs();
                int i = System.Array.IndexOf(a, "-fps");
                _cmdFps = i >= 0 && i + 1 < a.Length && int.TryParse(a[i + 1], out int n) && n > 0 ? n : 0;
                return _cmdFps;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Startup()
        {
            if (Application.isBatchMode) return;
            Apply();
            GfxPostDriver.Ensure();
            GarageReflections.Ensure();
            Debug.Log($"[Gfx] profile {(CurrentPreset() is int p && p >= 0 ? Presets[p].Name : "własny")}: AA {AaName(Aa)}, aniso {Aniso}x, textures {TextureName(TextureLevel)}, " +
                      $"shadows {ShadowName(Shadows)} {ShadowDistance:0} m, bloom {Bloom}, vsync {VSync}, fps cap {FpsCap}, lights {PixelLights}, lod {LodBias}, reflections {Reflections}, mirror {MirrorSettings.Name(MirrorSettings.Quality)}");
        }
    }
}
