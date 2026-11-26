using UnityEditor;
using UnityEngine;

namespace HideAndSeek.Game.Editor
{
    /// <summary>
    /// Greasy, torn upholstery of the garage lobby's sofas and poufs (<see cref="LobbyLayout.Garage"/>): the colour is
    /// baked into one 1024 px texture per piece (faded patches, soaked-in grease, hand smears, torn slits with yellow
    /// foam), the weave comes from a detail map, and the seat meshes get box-projected UVs so the stains and holes
    /// spread over the whole piece instead of repeating on every cushion.
    /// </summary>
    public static partial class MultiplayerBuilder
    {
        /// <summary>
        /// Worn upholstery with the colour baked in: faded patches, grease stains with a tide mark, smeared hand marks
        /// and torn holes showing yellow foam. <paramref name="holes"/> = how many holes (0 for piping).
        /// </summary>
        private static Texture2D GrimyFabricTex(string name, Color baseCol, int holes, int seed)
        {
            float Field(float u, float v) => Fbm(u * 4.2f + seed * 1.7f, v * 4.2f - seed * 0.9f, 5);
            // grease covers the top ~22 % of the noise field (its spread is narrow and depends on the octaves)
            float g0 = FieldPercentile(Field, 0.76f), g1 = FieldPercentile(Field, 0.95f);
            return SmoothTex(name, 1024, 1024, (x, y) =>
        {
            float u = x / 1024f, v = y / 1024f;
            float fade = Fbm(u * 3f + seed, v * 3f) - 0.5f;
            var c = Color.Lerp(baseCol, baseCol * 1.45f + new Color(0.05f, 0.05f, 0.05f), Mathf.Clamp01(fade * 1.6f));
            c *= 0.92f + Fbm(u * 25f, v * 25f, 2) * 0.16f;
            // grease: big irregular soaked-in patches (thresholded noise) with a darker tide line, plus small drops
            var grease = new Color(0.045f, 0.035f, 0.025f);
            float gf = Field(u, v);
            float gmask = Step01(g0, g1, gf);
            float gdens = 0.65f + Fbm(u * 30f + seed, v * 30f, 3) * 0.55f;
            c = Color.Lerp(c, grease, Mathf.Clamp01(gmask * 0.8f * gdens));
            float tide = 1f - Mathf.Abs(gf - g0 - (g1 - g0) * 0.08f) / ((g1 - g0) * 0.1f);
            if (tide > 0f) c = Color.Lerp(c, grease, tide * 0.22f);
            for (int i = 0; i < 14; i++)
            {
                float sx = Hash01(seed * 97 + i * 13 + 1), sy = Hash01(seed * 89 + i * 31 + 5), sr = 0.006f + Hash01(seed * 7 + i * 11) * 0.012f;
                float dx = Mathf.Repeat(u - sx + 0.5f, 1f) - 0.5f, dy = Mathf.Repeat(v - sy + 0.5f, 1f) - 0.5f;
                c = Color.Lerp(c, grease, Soak(dx, dy, sr, i * 2.3f + seed) * 0.8f);
            }
            // hand smears: short streaks of grease with finger lines
            for (int i = 0; i < 6; i++)
            {
                float sx = Hash01(seed * 53 + i * 19 + 2), sy = Hash01(seed * 61 + i * 23 + 9), ang = Hash01(i * 5 + seed) * Mathf.PI;
                float dx = Mathf.Repeat(u - sx + 0.5f, 1f) - 0.5f, dy = Mathf.Repeat(v - sy + 0.5f, 1f) - 0.5f;
                float along = dx * Mathf.Cos(ang) + dy * Mathf.Sin(ang), across = -dx * Mathf.Sin(ang) + dy * Mathf.Cos(ang);
                float s = Mathf.Clamp01(1f - Mathf.Abs(along) / 0.07f) * Mathf.Clamp01(1f - Mathf.Abs(across) / 0.025f);
                float fingers = Mathf.Clamp01(Mathf.Sin(across * 520f) * 0.8f + 0.4f) * (0.5f + Fbm(u * 60f, v * 60f, 2));
                c = Color.Lerp(c, new Color(0.05f, 0.04f, 0.03f), s * fingers * 0.8f);
            }
            // torn holes: ragged slits, a frayed lighter edge, yellow foam, a dark hollow
            for (int i = 0; i < holes; i++)
            {
                float sx = 0.08f + Hash01(seed * 37 + i * 41 + 3) * 0.84f, sy = 0.08f + Hash01(seed * 43 + i * 47 + 8) * 0.84f;
                float sr = 0.01f + Hash01(seed * 3 + i * 17) * 0.012f, ang = Hash01(seed * 5 + i * 29) * Mathf.PI, stretch = 1.6f + Hash01(i * 3 + seed) * 1.2f;
                float ex = u - sx, ey = v - sy;
                float dx = (ex * Mathf.Cos(ang) + ey * Mathf.Sin(ang)) / stretch, dy = -ex * Mathf.Sin(ang) + ey * Mathf.Cos(ang);
                float rimB = Blot(dx, dy, sr * 1.45f, i * 7.7f + seed, 0.35f);
                if (rimB <= 0f) continue;
                float hole = Blot(dx, dy, sr, i * 7.7f + seed, 0.3f);
                float deep = Blot(dx, dy, sr * 0.45f, i * 4.1f + seed, 0.3f);
                float thread = Hash01(x * 17 + y * 113) > 0.5f ? 1f : 0.55f;
                c = Color.Lerp(c, c * 1.6f * thread + new Color(0.07f, 0.06f, 0.05f), rimB * (1f - hole) * 0.85f);
                var foam = new Color(0.8f, 0.68f, 0.36f) * (0.7f + Fbm(u * 220f, v * 220f, 2) * 0.55f);
                c = Color.Lerp(c, foam, hole);
                c = Color.Lerp(c, new Color(0.14f, 0.1f, 0.05f), deep * 0.8f);
            }
            c.a = 1f;
            return c;
        });
        }

        /// <summary>Value of <paramref name="f"/> (over 0..1 x 0..1) below which fraction <paramref name="p"/> of 4096 samples lie.</summary>
        private static float FieldPercentile(System.Func<float, float, float> f, float p)
        {
            var a = new float[4096];
            for (int i = 0; i < a.Length; i++) a[i] = f(Hash01(i * 7 + 1), Hash01(i * 13 + 5));
            System.Array.Sort(a);
            return a[Mathf.Clamp(Mathf.RoundToInt(p * (a.Length - 1)), 0, a.Length - 1)];
        }

        /// <summary>Fine weave for the detail slot (mid grey, x2 multiply).</summary>
        private static Texture2D WeaveDetailTex() => SmoothTex("garage_weave_detail", 128, 128, (x, y) =>
        {
            float w = ((x / 2 + y / 2) % 2 == 0 ? 0.53f : 0.46f) + Hash01(x * 131 + y * 7919) * 0.04f;
            return new Color(w, w, w, 1f);
        });

        /// <summary>
        /// Box-projected UVs in object space (1 repeat = <paramref name="metres"/>), so one stained texture spreads over
        /// the whole piece of furniture instead of repeating on every cushion (the seat meshes map 0..1 per part).
        /// </summary>
        private static Mesh GarageBoxUv(Mesh m, float metres, Vector2 offset = default)
        {
            var v = m.vertices; var n = m.normals;
            var uv = new Vector2[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                Vector3 a = new Vector3(Mathf.Abs(n[i].x), Mathf.Abs(n[i].y), Mathf.Abs(n[i].z));
                Vector2 q = a.y >= a.x && a.y >= a.z ? new Vector2(v[i].x, v[i].z + 0.37f) : a.x >= a.z ? new Vector2(v[i].z + 0.71f, v[i].y) : new Vector2(v[i].x, v[i].y + 0.53f);
                uv[i] = q / metres + offset;
            }
            m.uv = uv;
            m.RecalculateTangents();
            return m;
        }

        /// <summary>Upholstery for the garage look: <paramref name="tiling"/> maps the mesh UVs to one repeat of the stained texture.</summary>
        private static Material GrimyFabric(string name, Color baseCol, int holes, int seed, Vector2 tiling, float weave = 18f)
        {
            var m = Mat(name, Color.white, GrimyFabricTex("garage_" + name.ToLowerInvariant(), baseCol, holes, seed), 0.16f, tiling);
            m.SetTexture("_DetailAlbedoMap", WeaveDetailTex());
            m.SetTextureScale("_DetailAlbedoMap", tiling * weave);
            m.EnableKeyword("_DETAIL_MULX2");
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
