using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game.Gfx
{
    /// <summary>
    /// "Lustro" graphics option for the lobby mirror (<see cref="PlanarMirror"/>), saved as hns.gfx.mirror next to the
    /// other graphics settings. 0 = off (no reflection camera, no render texture, the glass shows a static dark sheen),
    /// 1 = low, 2 = medium, 3 = high (the original look: full resolution, MSAA, refreshed every frame).
    /// The graphics profiles set it too (Niska / Średnia / Wysoka / Ultra = low / medium / high / high), but it is not
    /// compared when the profile row decides between a profile name and "własny". Read every frame, so a change
    /// applies immediately.
    /// </summary>
    public static class MirrorSettings
    {
        /// <summary>What one quality level costs: render-texture size, MSAA, refresh rate and the lighting kept in the reflection.</summary>
        public struct Level
        {
            /// <summary>Render-texture resolution relative to the screen (only the mirror's on-screen part is rendered).</summary>
            public float Scale;
            /// <summary>MSAA samples of the render texture (1 = none; 4 = at least 4, or the game's own MSAA if higher).</summary>
            public int Msaa;
            /// <summary>Render the reflection every N-th frame (frames in between reuse it, reprojected by the shader).</summary>
            public int Interval;
            /// <summary>Per-pixel lights per object in the reflection (0 = the game's own setting).</summary>
            public int PixelLights;
            /// <summary>Realtime shadows in the reflection (off = none).</summary>
            public bool Shadows;
            /// <summary>Shadow distance cap in the reflection, metres (0 = the game's own setting).</summary>
            public float ShadowDistance;
            /// <summary>Far clip of the reflection camera, metres (0 = the player's camera).</summary>
            public float FarClip;
            /// <summary>LOD bias multiplier in the reflection (lower = simpler meshes sooner).</summary>
            public float LodScale;
            /// <summary>HDR rendering (an extra float buffer; the texture itself is 8-bit).</summary>
            public bool Hdr;
            /// <summary>Layers left out of the reflection on top of the mirror's own mask.</summary>
            public int SkipLayers;
            /// <summary>The mirror is not refreshed farther away than this, metres.</summary>
            public float RenderDistance;
        }

        public const int Off = 0, Low = 1, Medium = 2, High = 3;
        public const int Default = High;
        private const string Key = "hns.gfx.mirror";

        public static readonly Level[] Levels =
        {
            default, // off
            new Level { Scale = 0.4f, Msaa = 1, Interval = 3, PixelLights = 1, Shadows = false, FarClip = 20f, LodScale = 0.5f, Hdr = false, SkipLayers = (1 << 1) | (1 << 4), RenderDistance = 22f },
            new Level { Scale = 0.65f, Msaa = 2, Interval = 2, PixelLights = 2, Shadows = true, ShadowDistance = 15f, FarClip = 30f, LodScale = 0.75f, Hdr = false, RenderDistance = 30f },
            new Level { Scale = 1f, Msaa = 4, Interval = 1, PixelLights = 0, Shadows = true, FarClip = 0f, LodScale = 1f, Hdr = true, RenderDistance = 40f },
        };

        private static int _cached = -1;

        /// <summary>Saved level 0..3 (default: high, the look the mirror always had).</summary>
        public static int Quality
        {
            get
            {
                if (_cached < 0) _cached = Mathf.Clamp(Prefs.GetInt(Key, Default), Off, High);
                return _cached;
            }
            set
            {
                _cached = Mathf.Clamp(value, Off, High);
                Prefs.SetInt(Key, _cached);
            }
        }

        /// <summary>Test override (-testmirrorq): used instead of the saved level, never written to the settings file.</summary>
        public static int? TestQuality;

        public static int Effective => TestQuality ?? Quality;

        /// <summary>Level matching a graphics profile (index into <see cref="GfxSettings.Presets"/>).</summary>
        public static int ForPreset(int preset) => Mathf.Clamp(preset + 1, Low, High);

        public static string Name(int q) => q switch
        {
            Off => "wyłączone",
            Low => "niska",
            Medium => "średnia",
            _ => "wysoka",
        };
    }
}
