using System;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames.Pool
{
    public static partial class CueArt
    {
        /// <summary>Circumference of the cue round the wrap (metres), used to keep the weave's pixels square-ish.</summary>
        const float WrapCirc = 2f * Mathf.PI * 0.0137f;

        /// <summary>Irish linen: one turn of cord per <see cref="LinenPitch"/>, <see cref="LinenTurns"/> turns per texture tile.</summary>
        public const float LinenPitch = 0.00115f;
        public const int LinenTurns = 24;
        public const float LinenTile = LinenPitch * LinenTurns;
        /// <summary>Leather wrap: one spiral seam per tile.</summary>
        public const float LeatherTile = 0.045f;

        /// <summary>Height 0..1 of the linen cord (helix, three twisted plies) and which ply the point lies on.</summary>
        static float LinenHeight(float u, float z, out float along, out int ply, out float c)
        {
            float cp = z / LinenPitch - u;
            c = Frac(cp);
            float cord = Mathf.Sqrt(Mathf.Max(0f, Mathf.Sin(Mathf.PI * c)));
            const int strands = 108; // diagonal ply ridges round the cue (multiple of 3: the u seam stays invisible)
            float sp = u * strands + c * 1.3f;
            float s = Frac(sp);
            ply = ((Mathf.FloorToInt(sp) % 3) + 3) % 3;
            along = Mathf.Floor(cp) + u;
            float strand = Mathf.Pow(Mathf.Sin(Mathf.PI * s), 0.6f);
            return cord * (0.68f + 0.32f * strand);
        }

        /// <summary>Linen albedo: <paramref name="main"/> fibres with <paramref name="fleck"/> flecks (share 0..1), shaded by the weave.</summary>
        public static Texture2D Linen(string key, Color main, Color fleck, float share) => Get("linen_" + key, () =>
            Fill(1024, 512, false, (x, y) =>
            {
                float u = (x + 0.5f) / 1024f, z = (y + 0.5f) / 512f * LinenTile;
                float h = LinenHeight(u, z, out float along, out int ply, out float c);
                const int perTurn = 150;
                float fib = Noise(along * perTurn, ply * 5.3f + c * 1.7f, perTurn * LinenTurns, 61 + ply);
                float fib2 = Noise(along * perTurn * 3f, ply * 2.1f, perTurn * LinenTurns * 3, 67);
                Color col = fib > 1f - share ? Color.Lerp(main, fleck, Mathf.Clamp01((fib - (1f - share)) * 6f)) : main;
                col *= 0.85f + 0.3f * fib2;
                col *= 0.5f + 0.5f * h;
                col.a = 1f;
                return col;
            }));

        public static Texture2D LinenNormal => Get("linen_n", () => NormalMap(1024, 512, LinenTile, 0.00028f, (u, z) => LinenHeight(u, z, out _, out _, out _)));

        // ================================================================ leather

        static float LeatherHeight(float u, float z)
        {
            float t = z / LeatherTile;
            float p = Noise(u * 150f, t * 76f, 150, 81, 76) * 0.65f + Noise(u * 330f, t * 171f, 330, 83, 171) * 0.35f;
            float pebble = 1f - Mathf.Pow(Mathf.Abs(p - 0.5f) * 2f, 1.5f);
            // Spiral seam of the wound leather strip.
            float f = Frac(z / LeatherTile - u);
            float dz = Mathf.Min(f, 1f - f) * LeatherTile;
            float seam = Mathf.SmoothStep(0f, 1f, dz / 0.0007f);
            return (0.55f + 0.45f * pebble) * (0.25f + 0.75f * seam);
        }

        public static Texture2D Leather(string key, Color main) => Get("leather_" + key, () =>
            Fill(1024, 512, false, (x, y) =>
            {
                float u = (x + 0.5f) / 1024f, z = (y + 0.5f) / 512f * LeatherTile;
                float h = LeatherHeight(u, z);
                float mottle = Noise(u * 5f, z / LeatherTile * 3f, 5, 87, 3) * 0.6f + Noise(u * 11f, z / LeatherTile * 7f, 11, 88, 7) * 0.4f;
                Color col = main * (0.7f + 0.3f * h) * (0.88f + 0.24f * mottle);
                col.a = 1f;
                return col;
            }));

        public static Texture2D LeatherNormal => Get("leather_n", () => NormalMap(1024, 512, LeatherTile, 0.00012f, LeatherHeight));

        /// <summary>
        /// Tangent-space normal map (linear, RGB = normal * 0.5 + 0.5, A = 1) of a height field over u = 0..1 round the
        /// wrap and z = 0..<paramref name="tile"/> metres; <paramref name="depth"/> = metres of relief for height 0..1.
        /// </summary>
        static Texture2D NormalMap(int w, int h, float tile, float depth, Func<float, float, float> height) =>
            Fill(w, h, true, (x, y) =>
            {
                float du = 1f / w, dz = tile / h;
                float u = (x + 0.5f) / w, z = (y + 0.5f) / h * tile;
                float hx = (height(u + du, z) - height(u - du, z)) * depth / (2f * du * WrapCirc);
                float hy = (height(u, z + dz) - height(u, z - dz)) * depth / (2f * dz);
                var n = new Vector3(-hx, -hy, 1f).normalized;
                return new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
            });
    }
}
