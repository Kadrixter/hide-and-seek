using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HideAndSeek.Game.Editor
{
    /// <summary>
    /// Garage floor (<see cref="LobbyLayout.Garage"/>, 2026-11-26): ONE texture over the whole 16 x 12 m slab (128 px/m,
    /// the floor box gets world UVs), so nothing repeats - 4 x 4 m slabs poured a shade apart, saw-cut joints, a few
    /// hairline cracks, a newer repair patch, faint power-trowel swirls, grime along the walls, polished walking paths,
    /// oil stains where they belong (by the drum and the tyres, at the gate, a big old one with a tide line and drips),
    /// tyre tracks in from the gate, a burnout arc and paint splashes; smoothness in the alpha (oil and polished paths
    /// shine, the dusty edges do not), a normal map for the joints, cracks and the patch. Close up a 2 m detail map
    /// (aggregate, pores, fine grain) with its own normal map takes over.
    /// </summary>
    public static partial class MultiplayerBuilder
    {
        private const int GfPpm = 128, GdN = 512;
        private const float GfSlab = 4f;
        private static readonly Rect GfPatch = new(2.6f, -4.9f, 1.5f, 1.05f);

        private static Material GarageFloorMat()
        {
            var m = Mat("GarageFloor", Color.white, GarageFloorTex(), 0.2f, Vector2.one);
            m.SetTexture("_BumpMap", GarageFloorNormal());
            m.SetFloat("_BumpScale", 1f);
            m.EnableKeyword("_NORMALMAP");
            m.SetTexture("_DetailAlbedoMap", GarageFloorDetail());
            m.SetTextureScale("_DetailAlbedoMap", new Vector2(LobbyLayout.W / 2f, LobbyLayout.D / 2f));
            m.SetTexture("_DetailNormalMap", GarageFloorDetailNormal());
            m.SetFloat("_DetailNormalMapScale", 0.9f);
            m.EnableKeyword("_DETAIL_MULX2");
            // smoothness from the albedo's alpha
            m.SetFloat("_SmoothnessTextureChannel", 1f);
            m.SetFloat("_GlossMapScale", 1f);
            m.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>World UVs on the floor box: u = x over the room width, v = z over its depth (one texture, no repeats).</summary>
        private static void GarageFloorUv(Transform floor)
        {
            var mf = floor.GetComponent<MeshFilter>();
            var m = Object.Instantiate(mf.sharedMesh);
            var v = m.vertices;
            var uv = new Vector2[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                Vector3 w = floor.TransformPoint(v[i]);
                uv[i] = new Vector2((w.x + LobbyLayout.HalfW) / LobbyLayout.W, (w.z + LobbyLayout.HalfD) / LobbyLayout.D);
            }
            m.uv = uv;
            m.RecalculateTangents();
            mf.sharedMesh = SaveMesh(m, "GarageFloorMesh");
        }

        // ================================================================ big map (whole floor)

        private static float GfX(int x) => (x + 0.5f) / GfPpm - LobbyLayout.HalfW;
        private static float GfZ(int y) => (y + 0.5f) / GfPpm - LobbyLayout.HalfD;

        /// <summary>Distance (m) to the nearest saw cut.</summary>
        private static float GfJointDist(float wx, float wz)
        {
            float rx = Mathf.Repeat(wx + LobbyLayout.HalfW, GfSlab), rz = Mathf.Repeat(wz + LobbyLayout.HalfD, GfSlab);
            return Mathf.Min(Mathf.Min(rx, GfSlab - rx), Mathf.Min(rz, GfSlab - rz));
        }

        /// <summary>Hairline cracks: a ridge of warped noise, only in a few regions. 0..1.</summary>
        private static float GfCrack(float wx, float wz)
        {
            float mask = Step01(0.57f, 0.66f, Fbm(wx * 0.21f + 11f, wz * 0.21f + 5f, 2));
            if (mask <= 0f) return 0f;
            float warp = (Fbm(wx * 1.4f, wz * 1.4f + 4f, 2) - 0.5f) * 0.6f;
            float d = Mathf.Abs(Fbm(wx * 0.75f + warp + 3f, wz * 0.75f + 9f, 3) - 0.5f);
            return mask * (1f - Step01(0.0012f, 0.0035f, d));
        }

        /// <summary>Inside the repair patch (1) with a slightly ragged edge, 0 outside.</summary>
        private static float GfPatchIn(float wx, float wz)
        {
            float e = Mathf.Min(Mathf.Min(wx - GfPatch.xMin, GfPatch.xMax - wx), Mathf.Min(wz - GfPatch.yMin, GfPatch.yMax - wz));
            e += (Fbm(wx * 9f, wz * 9f, 2) - 0.5f) * 0.02f;
            return Step01(-0.004f, 0.004f, e);
        }

        private static float GfHeight(float wx, float wz)
        {
            float groove = 1f - Step01(0.004f, 0.01f, GfJointDist(wx, wz));
            float patch = GfPatchIn(wx, wz);
            return -groove - 0.6f * GfCrack(wx, wz) + 0.25f * patch;
        }

        private static Texture2D GarageFloorNormal()
        {
            int w = Mathf.RoundToInt(LobbyLayout.W * GfPpm), h = Mathf.RoundToInt(LobbyLayout.D * GfPpm);
            return SmoothTex("garage_floor16_n", w, h, (x, y) =>
            {
                float wx = GfX(x), wz = GfZ(y), s = 1f / GfPpm;
                float hl = GfHeight(wx - s, wz), hr = GfHeight(wx + s, wz), hd = GfHeight(wx, wz - s), hu = GfHeight(wx, wz + s);
                var n = new Vector3((hl - hr) * 1.6f, (hd - hu) * 1.6f, 1f).normalized;
                return new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
            }, true);
        }

        private static float SegDist(float px, float pz, Vector2 a, Vector2 b)
        {
            Vector2 p = new(px, pz), d = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, d) / d.sqrMagnitude);
            return Vector2.Distance(p, a + d * t);
        }

        private static Texture2D GarageFloorTex()
        {
            int w = Mathf.RoundToInt(LobbyLayout.W * GfPpm), h = Mathf.RoundToInt(LobbyLayout.D * GfPpm);
            float hw = LobbyLayout.HalfW, hd = LobbyLayout.HalfD;
            // oil: (x, z, radius) in metres - by the drum, by the tyres, at the gate, two old parking stains, a small one
            var oil = new List<(float x, float z, float r, float k)>
            {
                (7.1f, 4.95f, 0.42f, 1f), (7.55f, 4.35f, 0.15f, 0.9f), (6.85f, -4.85f, 0.28f, 0.8f), (-6.55f, 2.0f, 0.3f, 0.85f),
                (-6.95f, 2.75f, 0.18f, 0.8f), (-1.6f, -2.3f, 0.58f, 1f), (2.9f, 0.85f, 0.4f, 0.85f), (4.55f, -3.4f, 0.2f, 0.75f),
            };
            int big = oil.Count;
            for (int i = 0; i < big; i++)
            for (int k = 0; k < 5; k++)
            {
                float a = Hash01(i * 31 + k * 7) * 6.283f, dist = oil[i].r * (1.05f + Hash01(i * 17 + k * 3) * 0.7f);
                oil.Add((oil[i].x + Mathf.Cos(a) * dist, oil[i].z + Mathf.Sin(a) * dist, 0.015f + Hash01(i * 5 + k) * 0.035f, 0.85f));
            }
            // a leaking car's drip trail from the big stain to the exit door
            for (int k = 0; k < 22; k++)
            {
                float t = k / 21f;
                oil.Add((Mathf.Lerp(-1.1f, LobbyLayout.ExitDoorX - 0.3f, t) + (Hash01(k * 3 + 90) - 0.5f) * 0.08f,
                         Mathf.Lerp(-2.6f, -hd + 0.5f, t) + Mathf.Sin(t * 7f) * 0.12f, 0.012f + Hash01(k + 120) * 0.02f, 0.75f));
            }
            var paint = new (float x, float z, float r, Color c)[]
            {
                (-hw + 0.25f, -1.3f, 0.05f, new Color(0.85f, 0.65f, 0.1f)), (-hw + 0.38f, -1.05f, 0.022f, new Color(0.85f, 0.65f, 0.1f)),
                (-hw + 0.3f, -1.55f, 0.016f, new Color(0.85f, 0.65f, 0.1f)), (-hw + 0.45f, 0.75f, 0.035f, new Color(0.62f, 0.1f, 0.07f)),
                (6.1f, 3.85f, 0.055f, new Color(0.15f, 0.3f, 0.6f)), (6.22f, 3.7f, 0.02f, new Color(0.15f, 0.3f, 0.6f)), (1.2f, -5.5f, 0.03f, new Color(0.88f, 0.88f, 0.85f)),
            };
            Vector2 exit = new(LobbyLayout.ExitDoorX, -hd + 0.3f), gate = new(-hw + 0.2f, 2.25f), mid = new(0f, -0.2f), arc = new(4.6f, 3.4f), lounge = new(-3.4f, 0.4f);
            Color oilCol = new(0.11f, 0.095f, 0.08f), rubber = new(0.07f, 0.07f, 0.07f), dustCol = new(0.5f, 0.47f, 0.42f);

            return SmoothTex("garage_floor16", w, h, (x, y) =>
            {
                float wx = GfX(x), wz = GfZ(y);
                // ---- base concrete: big patches, slab-to-slab tone, mottling, warm / cool cast
                int sx = Mathf.FloorToInt((wx + hw) / GfSlab), sz = Mathf.FloorToInt((wz + hd) / GfSlab);
                float g = 0.43f + (Fbm(wx * 0.3f, wz * 0.3f) - 0.5f) * 0.14f + (Fbm(wx * 1.7f + 7f, wz * 1.7f) - 0.5f) * 0.07f
                          + (Hash01(sx * 13 + sz * 71 + 3) - 0.5f) * 0.05f + (Fbm(wx * 7f, wz * 7f, 3) - 0.5f) * 0.045f;
                float cast = Fbm(wx * 0.45f + 20f, wz * 0.45f);
                var c = new Color(g, g, g) * Color.Lerp(new Color(0.97f, 0.985f, 1f), new Color(1f, 0.98f, 0.935f), cast);
                float smooth = 0.17f + (Fbm(wx * 3f + 5f, wz * 3f) - 0.5f) * 0.08f;
                // ---- power-trowel swirls: faint arcs round jittered centres
                int tx = Mathf.FloorToInt(wx / 1.1f), tz = Mathf.FloorToInt(wz / 1.1f);
                float tcx = (tx + 0.3f + Hash01(tx * 7 + tz * 131) * 0.4f) * 1.1f, tcz = (tz + 0.3f + Hash01(tx * 19 + tz * 37) * 0.4f) * 1.1f;
                float td = Mathf.Sqrt((wx - tcx) * (wx - tcx) + (wz - tcz) * (wz - tcz));
                float swirl = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(td * 46f + Hash01(tx + tz * 5) * 6f), 10f) * Step01(0.45f, 0.7f, Fbm(wx * 1.2f, wz * 1.2f + 30f, 2));
                c += new Color(1f, 1f, 1f) * swirl * 0.03f;
                smooth += swirl * 0.06f;
                // ---- repair patch: newer, lighter, a dark seam
                float patch = GfPatchIn(wx, wz);
                c = Color.Lerp(c, new Color(0.5f, 0.495f, 0.48f) * (0.95f + 0.1f * Fbm(wx * 6f, wz * 6f, 2)), patch);
                float seam = patch * (1f - patch) * 4f;
                c *= 1f - 0.35f * seam;
                // ---- walking paths: a little darker and polished
                float path = Mathf.Min(Mathf.Min(SegDist(wx, wz, exit, mid), SegDist(wx, wz, gate, lounge)), Mathf.Min(SegDist(wx, wz, mid, arc), SegDist(wx, wz, mid, lounge)));
                float wear = (1f - Step01(0.15f, 0.85f, path)) * (0.6f + 0.8f * Fbm(wx * 2.2f, wz * 2.2f + 8f, 2));
                c *= 1f - 0.07f * wear;
                smooth += 0.2f * wear;
                // ---- tyre tracks in from the gate, fading out; a faint burnout arc in the open
                float fade = Mathf.Clamp01(1f - (wx + hw) / 2.6f);
                if (fade > 0f)
                    foreach (float side in new[] { -0.78f, 0.78f })
                    {
                        float dz = wz - (2.25f + side + (wx + hw) * 0.07f);
                        if (Mathf.Abs(dz) < 0.1f)
                        {
                            float tread = Mathf.Repeat(wx * 9f + Mathf.Abs(dz) * 25f, 1f) < 0.55f ? 1f : 0.55f;
                            c = Color.Lerp(c, rubber, 0.3f * fade * tread * (0.5f + Fbm(wx * 5f, wz * 5f, 2)) * (1f - Step01(0.07f, 0.1f, Mathf.Abs(dz))));
                        }
                    }
                float ad = Mathf.Abs(Mathf.Sqrt((wx - 1.1f) * (wx - 1.1f) + (wz + 3.4f) * (wz + 3.4f)) - 1.35f);
                float aa = Mathf.Atan2(wz + 3.4f, wx - 1.1f) * Mathf.Rad2Deg;
                if (ad < 0.09f && aa > -150f && aa < -20f)
                    c = Color.Lerp(c, rubber, 0.16f * Mathf.Sin((aa + 150f) / 130f * Mathf.PI) * (1f - ad / 0.09f) * (0.5f + Fbm(wx * 6f, wz * 6f, 2)));
                // ---- oil: soaked-in stains with a darker tide line, glossy
                for (int i = 0; i < oil.Count; i++)
                {
                    var o = oil[i];
                    float b = Soak(wx - o.x, wz - o.z, o.r, i * 3.7f);
                    if (b <= 0f) continue;
                    float inner = Soak(wx - o.x, wz - o.z, o.r * 0.8f, i * 3.7f);
                    float tide = Mathf.Clamp01(b - inner) * 1.6f;
                    float dens = 0.65f + 0.6f * Fbm(wx * 9f + i, wz * 9f, 3);
                    c = Color.Lerp(c, oilCol, Mathf.Clamp01((b * 0.62f * dens + tide * 0.3f) * o.k));
                    smooth += (b * 0.35f + tide * 0.1f) * o.k;
                }
                // ---- paint splashes (hard edged, satin)
                foreach (var p in paint)
                {
                    float b = Blot(wx - p.x, wz - p.z, p.r, p.x * 3f + p.z, 0.5f);
                    if (b <= 0f) continue;
                    c = Color.Lerp(c, p.c * (0.85f + 0.15f * Fbm(wx * 30f, wz * 30f, 2)), b * 0.92f);
                    smooth = Mathf.Lerp(smooth, 0.45f, b);
                }
                // ---- grime and dust along the walls
                float e = Mathf.Min(Mathf.Min(wx + hw, hw - wx), Mathf.Min(wz + hd, hd - wz));
                float dust = (1f - Step01(0.03f, 0.4f, e)) * (0.5f + 0.7f * Fbm(wx * 3f, wz * 3f + 50f, 2));
                c = Color.Lerp(c, dustCol, Mathf.Clamp01(dust * 0.3f));
                smooth -= dust * 0.1f;
                if (e < 0.03f) c *= 0.8f;
                // ---- saw cuts (dirt-filled) and cracks
                float jd = GfJointDist(wx, wz);
                if (jd < 0.008f) c *= 0.38f;
                else if (jd < 0.02f) c *= 0.88f + 0.08f * Hash01(x * 7 + y * 3);
                float crack = GfCrack(wx, wz);
                c *= 1f - 0.5f * crack;
                c.a = Mathf.Clamp(smooth, 0.05f, 0.7f);
                return c;
            });
        }

        // ================================================================ detail (2 m repeat)

        /// <summary>Periodic value noise (period in cells).</summary>
        private static float PNoise(float x, float y, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            float H(int i, int j) => Hash01(((i % period + period) % period) * 7919 + ((j % period + period) % period) * 104729 + seed * 1299709);
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            return Mathf.Lerp(Mathf.Lerp(H(x0, y0), H(x0 + 1, y0), fx), Mathf.Lerp(H(x0, y0 + 1), H(x0 + 1, y0 + 1), fx), fy);
        }

        /// <summary>Detail height (aggregate stones up, pores down) and the albedo tone of the spot (0.5 = neutral).</summary>
        private static float GdSample(int x, int y, out float tone)
        {
            const int cs = 8, cells = GdN / cs;
            int cx = Mathf.FloorToInt(x / (float)cs), cy = Mathf.FloorToInt(y / (float)cs);
            float hgt = 0f;
            tone = 0.5f + (PNoise(x / 4f, y / 4f, GdN / 4, 1) - 0.5f) * 0.07f + (PNoise(x / 16f, y / 16f, GdN / 16, 2) - 0.5f) * 0.06f;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int i = cx + dx, j = cy + dy;
                int seed = ((i % cells + cells) % cells) * 131 + ((j % cells + cells) % cells) * 7907;
                if (Hash01(seed) > 0.5f) continue;
                float px = i * cs + Hash01(seed + 1) * cs, py = j * cs + Hash01(seed + 2) * cs, r = 1.4f + Hash01(seed + 3) * 3.2f;
                float d = Mathf.Sqrt((x - px) * (x - px) + (y - py) * (y - py)) / r;
                if (d >= 1f) continue;
                float k = 1f - d * d;
                hgt = Mathf.Max(hgt, k * 0.7f);
                float stone = Hash01(seed + 4) < 0.55f ? 0.57f + Hash01(seed + 5) * 0.05f : 0.42f + Hash01(seed + 6) * 0.05f; // muted: strong contrast read as terrazzo
                tone = Mathf.Lerp(tone, stone, Mathf.Clamp01(k * 2f) * 0.3f);
            }
            int wxp = (x % GdN + GdN) % GdN, wyp = (y % GdN + GdN) % GdN;
            if (Hash01(wxp * 92821 + wyp * 68917 + 17) > 0.992f) { hgt -= 1f; tone *= 0.55f; }
            hgt += (PNoise(x / 2f, y / 2f, GdN / 2, 3) - 0.5f) * 0.25f;
            return hgt;
        }

        private static Texture2D GarageFloorDetail() => SmoothTex("garage_floor_detail", GdN, GdN, (x, y) =>
        {
            GdSample(x, y, out float tone);
            return new Color(tone, tone, tone, 1f);
        });

        private static Texture2D GarageFloorDetailNormal() => SmoothTex("garage_floor_detail_n", GdN, GdN, (x, y) =>
        {
            float hl = GdSample(x - 1, y, out _), hr = GdSample(x + 1, y, out _), hd = GdSample(x, y - 1, out _), hu = GdSample(x, y + 1, out _);
            var n = new Vector3((hl - hr) * 0.9f, (hd - hu) * 0.9f, 1f).normalized;
            return new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
        }, true);
    }
}
