using UnityEditor;
using UnityEngine;

namespace HideAndSeek.Game.Editor
{
    /// <summary>
    /// Garage lobby textures and materials (<see cref="LobbyLayout.Garage"/>): the floor: LobbyGarageFloor; painted concrete block walls (1 repeat = 1.6 m x the full 3.6 m height: dark lower
    /// band, yellow stripe, light upper part, dirt at the bottom, streaks from the top) with a mortar normal map, the
    /// sectional door panel, hazard stripes and the pegboard. The upholstery: LobbyGarageFabric.
    /// </summary>
    public static partial class MultiplayerBuilder
    {
        public const float GarageWallRepeat = 1.6f;

        // ================================================================ textures

        /// <summary>GLSL smoothstep: 0 below <paramref name="e0"/>, 1 above <paramref name="e1"/> (Mathf.SmoothStep is an interpolation, not this).</summary>
        private static float Step01(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        /// <summary>Layered value noise 0..1.</summary>
        private static float Fbm(float x, float y, int oct = 4)
        {
            float a = 0.5f, s = 0f, n = 0f;
            for (int i = 0; i < oct; i++) { s += Mathf.PerlinNoise(x + i * 17.3f, y + i * 9.1f) * a; n += a; x *= 2.03f; y *= 2.03f; a *= 0.5f; }
            return s / n;
        }

        /// <summary>Irregular blot: 1 inside, 0 outside, soft noisy edge (d = distance / radius).</summary>
        private static float Blot(float dx, float dy, float r, float seed, float rough = 0.45f)
        {
            float lim = r * (1f + rough);
            if (dx * dx + dy * dy >= lim * lim) return 0f;
            float a = Mathf.Atan2(dy, dx);
            float wob = 1f + rough * (Mathf.PerlinNoise(Mathf.Cos(a) * 1.7f + seed, Mathf.Sin(a) * 1.7f + seed * 0.7f) - 0.5f) * 2f;
            float d = Mathf.Sqrt(dx * dx + dy * dy) / (r * wob);
            return Mathf.Clamp01((1f - d) * 4f);
        }

        /// <summary>Soft blot with a feathered, noisy edge (oil soaked into concrete): 0..1.</summary>
        private static float Soak(float dx, float dy, float r, float seed)
        {
            float lim = r * 1.5f;
            if (dx * dx + dy * dy >= lim * lim) return 0f;
            float a = Mathf.Atan2(dy, dx);
            float wob = 1f + 0.35f * (Mathf.PerlinNoise(Mathf.Cos(a) * 1.3f + seed, Mathf.Sin(a) * 1.3f + seed * 0.7f) - 0.5f) * 2f;
            float d = Mathf.Sqrt(dx * dx + dy * dy) / (r * wob);
            return Mathf.Clamp01(1f - Step01(0.55f, 1.15f, d));
        }

        // wall: 512 px = 1.6 m, 1152 px = 3.6 m (320 px/m); blocks 0.4 x 0.2 m = 128 x 64 px
        private const int GwW = 512, GwH = 1152;
        private const float GwPaintTop = 1.2f, GwStripe = 0.08f;

        private static float GarageWallHeight(int x, int y)
        {
            int row = y / 64;
            int lx = (x + (row % 2) * 64) % 128, ly = y % 64;
            float edge = Mathf.Min(Mathf.Min(lx, 127 - lx), Mathf.Min(ly, 63 - ly));
            return Mathf.Clamp01((edge - 1f) / 3f); // 0 in the mortar joint, 1 on the block face
        }

        private static Texture2D GarageWallTex() => SmoothTex("garage_wall", GwW, GwH, (x, y) =>
        {
            float u = x / (float)GwW, h = y / 320f; // h = height in metres
            int row = y / 64, bx = (x + (row % 2) * 64) / 128;
            float face = GarageWallHeight(x, y);
            float block = 0.9f + 0.1f * Hash01(bx * 31 + row * 17 + 5);
            float pore = Hash01(x * 7919 + y * 6271) > 0.93f ? 0.86f : 1f;
            float noise = 0.84f + Fbm(u * 14f, h * 5f) * 0.3f;
            Color paint;
            if (h < GwPaintTop) paint = new Color(0.2f, 0.25f, 0.29f);
            else if (h < GwPaintTop + GwStripe) paint = new Color(0.86f, 0.64f, 0.08f);
            else paint = new Color(0.58f, 0.58f, 0.55f);
            var c = paint * block * pore * noise * Mathf.Lerp(0.74f, 1f, face);
            // grimy patches on the upper paint
            float patch = Fbm(u * 4f + 21f, h * 1.5f, 3);
            if (h >= GwPaintTop + GwStripe) c = Color.Lerp(c, c * 0.72f, Mathf.Clamp01((patch - 0.5f) * 3f));
            // dirt and scuffs low down, darker right at the floor
            float dirt = Mathf.Clamp01(1f - h / 0.45f) * (0.45f + Fbm(u * 9f, h * 9f) * 0.6f);
            c = Color.Lerp(c, new Color(0.08f, 0.07f, 0.06f), dirt * 0.55f);
            float scuff = Fbm(u * 22f + 7f, h * 3f + 1f, 3);
            if (h < GwPaintTop && scuff > 0.66f) c = Color.Lerp(c, new Color(0.42f, 0.42f, 0.4f), (scuff - 0.66f) * 2.2f);
            // water / soot streaks from the ceiling
            float streak = Mathf.Clamp01((h - 2.2f) / 1.4f) * Mathf.Pow(Mathf.PerlinNoise(u * 18f, 0.3f), 3f) * Fbm(u * 40f, h * 2f, 2);
            c = Color.Lerp(c, new Color(0.2f, 0.19f, 0.16f), streak * 0.9f);
            // paint chipped off the stripe edges
            if (Mathf.Abs(h - GwPaintTop) < 0.02f || Mathf.Abs(h - GwPaintTop - GwStripe) < 0.02f)
                if (Hash01(x * 131 + y * 17) > 0.8f) c *= 0.75f;
            c.a = 1f;
            return c;
        });

        private static Texture2D GarageWallNormal() => SmoothTex("garage_wall_n", GwW, GwH, (x, y) =>
        {
            float hl = GarageWallHeight(Mathf.Max(0, x - 1), y), hr = GarageWallHeight(Mathf.Min(GwW - 1, x + 1), y);
            float hd = GarageWallHeight(x, Mathf.Max(0, y - 1)), hu = GarageWallHeight(x, Mathf.Min(GwH - 1, y + 1));
            float rough = (Hash01(x * 13 + y * 7) - 0.5f) * 0.12f;
            var n = new Vector3((hl - hr) * 1.4f + rough, (hd - hu) * 1.4f + rough, 1f).normalized;
            return new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
        }, true);

        /// <summary>One sectional door section seen from the front: two pressed grooves, a stucco grain, grime low down.</summary>
        private static Texture2D GarageDoorTex() => SmoothTex("garage_door_panel", 512, 128, (x, y) =>
        {
            float u = x / 512f, v = y / 128f;
            float g = 0.78f + Fbm(u * 60f, v * 15f, 2) * 0.12f;
            float groove = 1f;
            foreach (float gv in new[] { 0.33f, 0.66f })
            {
                float d = Mathf.Abs(v - gv) * 128f;
                if (d < 1.5f) groove = 0.55f;
                else if (d < 3f) groove = Mathf.Min(groove, v > gv ? 1.08f : 0.82f);
            }
            float edge = Mathf.Min(y, 127 - y) < 3 ? 0.6f : 1f;
            float grime = Fbm(u * 8f, v * 3f) * 0.25f + Mathf.Clamp01(0.35f - v) * 0.4f;
            var c = new Color(0.82f, 0.82f, 0.79f) * g * groove * edge * (1f - grime * 0.5f);
            c.a = 1f;
            return c;
        });

        /// <summary>Diagonal yellow / black hazard stripes (4 stripes per repeat), worn.</summary>
        private static Texture2D HazardTex() => SmoothTex("garage_hazard", 256, 256, (x, y) =>
        {
            bool yellow = ((x + y) / 64) % 2 == 0;
            var c = yellow ? new Color(0.9f, 0.68f, 0.06f) : new Color(0.06f, 0.06f, 0.06f);
            float wear = Fbm(x / 40f, y / 40f);
            if (wear > 0.62f) c = Color.Lerp(c, new Color(0.35f, 0.34f, 0.32f), (wear - 0.62f) * 2.5f);
            c *= 0.9f + Hash01(x * 31 + y * 7) * 0.1f;
            c.a = 1f;
            return c;
        });

        /// <summary>Pegboard: brown hardboard with a 25 mm hole grid (1 repeat = 0.4 m).</summary>
        private static Texture2D PegboardTex() => SmoothTex("garage_pegboard", 256, 256, (x, y) =>
        {
            int lx = x % 16, ly = y % 16;
            float d = new Vector2(lx - 7.5f, ly - 7.5f).magnitude;
            float g = 0.85f + Fbm(x / 30f, y / 30f) * 0.25f;
            var c = d < 2.2f ? new Color(0.05f, 0.04f, 0.03f) : new Color(0.52f, 0.38f, 0.24f) * g;
            c.a = 1f;
            return c;
        });

        // ================================================================ materials

        /// <summary>Block wall material tiled for a wall face <paramref name="length"/> m long.</summary>
        private static Material GarageWallMat(string name, float length)
        {
            var m = Mat(name, Color.white, GarageWallTex(), 0.12f, new Vector2(length / GarageWallRepeat, 1f));
            m.SetTexture("_BumpMap", GarageWallNormal());
            m.SetTextureScale("_BumpMap", m.mainTextureScale);
            m.SetFloat("_BumpScale", 1f);
            m.EnableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material HazardMat(string name, Vector2 tiling) => Mat(name, Color.white, HazardTex(), 0.3f, tiling);

    }
}
