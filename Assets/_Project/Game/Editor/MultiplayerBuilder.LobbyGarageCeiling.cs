using UnityEditor;
using UnityEngine;

namespace HideAndSeek.Game.Editor
{
    /// <summary>
    /// Garage ceiling (<see cref="LobbyLayout.Garage"/>, 2026-11-28): ONE texture over the whole 16 x 12 m slab
    /// (128 px/m, the ceiling box gets world UVs like the floor), so nothing repeats. A slab poured on plywood
    /// formwork: staggered 2.44 x 1.22 m panel prints a shade apart with thin fins at the joints, a faint grain
    /// imprint, form-tie holes, bug holes, water stains with tide lines, soot halos over the shop lights, rust runs
    /// along the I-beams and darker, dusty corners along the walls. A normal map for the fins and holes.
    /// Generated only when the PNGs are missing (after a change delete garage_ceiling16*.png + .meta).
    /// </summary>
    public static partial class MultiplayerBuilder
    {
        private const int GcPpm = 128;
        private const float GcPanelL = 2.44f, GcPanelW = 1.22f;
        private static float[] _gcHeight;

        /// <summary>Shop light centres (x, z) - same list as BuildLobbyLights.</summary>
        private static readonly Vector2[] GcLamps = { new(-5.0f, -2.6f), new(0.5f, -2.6f), new(5.5f, -2.6f), new(1.2f, 2.6f), new(5.2f, 2.4f) };

        private static Material GarageCeilingMat()
        {
            var m = Mat("GarageCeiling", Color.white, GarageCeilingTex(), 0.06f, Vector2.one);
            m.SetTexture("_BumpMap", GarageCeilingNormal());
            m.SetTextureScale("_BumpMap", Vector2.one);
            m.SetFloat("_BumpScale", 1f);
            m.EnableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>World UVs on the ceiling box: u = x over the room width, v = z over its depth. <paramref name="shift"/>
        /// maps a box outside the room (the track passage) onto a patch of the room's ceiling.</summary>
        private static void GarageCeilingUv(Transform ceiling, Vector2 shift = default, string meshName = "GarageCeilingMesh")
        {
            var mf = ceiling.GetComponent<MeshFilter>();
            var m = Object.Instantiate(mf.sharedMesh);
            var v = m.vertices;
            var uv = new Vector2[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                Vector3 w = ceiling.TransformPoint(v[i]);
                uv[i] = new Vector2((w.x + shift.x + LobbyLayout.HalfW) / LobbyLayout.W, (w.z + shift.y + LobbyLayout.HalfD) / LobbyLayout.D);
            }
            m.uv = uv;
            m.RecalculateTangents();
            mf.sharedMesh = SaveMesh(m, meshName);
        }

        private static int GcW => Mathf.RoundToInt(LobbyLayout.W * GcPpm);
        private static int GcH => Mathf.RoundToInt(LobbyLayout.D * GcPpm);
        private static float GcX(int x) => (x + 0.5f) / GcPpm - LobbyLayout.HalfW;
        private static float GcZ(int y) => (y + 0.5f) / GcPpm - LobbyLayout.HalfD;

        /// <summary>Formwork panel of a point: row along z, staggered column along x; local coords in metres.</summary>
        private static (int id, float lx, float lz) GcPanel(float wx, float wz)
        {
            float rz = wz + LobbyLayout.HalfD;
            int row = Mathf.FloorToInt(rz / GcPanelW);
            float rx = wx + LobbyLayout.HalfW + (row % 2) * GcPanelL * 0.5f;
            int col = Mathf.FloorToInt(rx / GcPanelL);
            return (row * 31 + col * 7 + 3, rx - col * GcPanelL, rz - row * GcPanelW);
        }

        /// <summary>Relief (0 = slab plane): fins at the panel joints, tie holes and bug holes sunk in.</summary>
        private static float GcRelief(float wx, float wz)
        {
            var (id, lx, lz) = GcPanel(wx, wz);
            float edge = Mathf.Min(Mathf.Min(lx, GcPanelL - lx), Mathf.Min(lz, GcPanelW - lz));
            float h = edge < 0.006f ? 0.8f : edge < 0.012f ? 0.35f * (1f - (edge - 0.006f) / 0.006f) : 0f;
            // form ties: 2 x 4 grid per panel, 12 mm cones
            for (int i = 0; i < 4; i++)
            for (int j = 0; j < 2; j++)
            {
                float tx = (i + 0.5f) * GcPanelL / 4f, tz = (j + 0.5f) * GcPanelW / 2f;
                float d = Mathf.Sqrt((lx - tx) * (lx - tx) + (lz - tz) * (lz - tz));
                if (d < 0.012f) h -= 1f - d / 0.012f * 0.4f;
            }
            // bug holes (air bubbles): 4 cm cells, a few with a 2-4 mm pit
            int cx = Mathf.FloorToInt(wx * 25f), cz = Mathf.FloorToInt(wz * 25f);
            float hb = Hash01(cx * 7349 + cz * 1931 + 11);
            if (hb > 0.86f)
            {
                float px = (cx + 0.25f + 0.5f * Hash01(cx * 13 + cz * 71)) / 25f, pz = (cz + 0.25f + 0.5f * Hash01(cx * 91 + cz * 37)) / 25f;
                float r = 0.0015f + 0.0025f * Hash01(cx * 3 + cz * 5 + id);
                float d = Mathf.Sqrt((wx - px) * (wx - px) + (wz - pz) * (wz - pz));
                if (d < r) h -= 0.7f * (1f - d / r);
            }
            // plywood grain pressed into the concrete (along the panel length)
            h += (Mathf.PerlinNoise(lx * 1.5f + id, lz * 90f) - 0.5f) * 0.12f;
            return h;
        }

        private static float[] GcHeights()
        {
            if (_gcHeight != null) return _gcHeight;
            int w = GcW, hgt = GcH;
            var a = new float[w * hgt];
            for (int y = 0; y < hgt; y++)
            for (int x = 0; x < w; x++)
                a[y * w + x] = GcRelief(GcX(x), GcZ(y));
            return _gcHeight = a;
        }

        private static Texture2D GarageCeilingTex() => SmoothTex("garage_ceiling16", GcW, GcH, (x, y) =>
        {
            float wx = GcX(x), wz = GcZ(y);
            var (id, lx, lz) = GcPanel(wx, wz);
            float relief = GcHeights()[y * GcW + x];
            // base: light grey concrete, each panel print a shade apart, mottled cement
            float panel = 0.92f + 0.12f * Hash01(id * 17 + 5);
            float mottle = 0.82f + Fbm(wx * 1.3f + 40f, wz * 1.3f, 4) * 0.3f;
            float fine = 0.94f + Fbm(wx * 14f, wz * 14f, 2) * 0.1f;
            float tone = panel * mottle * fine;
            var c = new Color(0.6f, 0.595f, 0.575f) * tone;
            // panel edges: slightly darker seam lines (cement paste leaked), fins catch light (light line)
            float edge = Mathf.Min(Mathf.Min(lx, GcPanelL - lx), Mathf.Min(lz, GcPanelW - lz));
            if (edge < 0.016f) c *= Mathf.Lerp(0.88f, 1f, edge / 0.016f);
            if (relief > 0.5f) c *= 1.12f;
            if (relief < -0.15f) c *= Mathf.Lerp(1f, 0.35f, Mathf.Clamp01(-relief));
            // old water stains: soft blots with a darker tide line
            for (int i = 0; i < 7; i++)
            {
                float sx = -7f + Hash01(i * 97 + 1) * 14f, sz = -5f + Hash01(i * 53 + 7) * 10f, r = 0.35f + Hash01(i * 29 + 3) * 0.6f;
                float dx = wx - sx, dz = wz - sz;
                if (dx * dx + dz * dz > r * r * 3f) continue;
                float b = Soak(dx, dz, r, i * 3.7f);
                float tide = Mathf.Clamp01(1f - Mathf.Abs(b - 0.12f) / 0.08f);
                c = Color.Lerp(c, new Color(0.47f, 0.44f, 0.38f) * tone, b * 0.45f);
                c = Color.Lerp(c, new Color(0.36f, 0.31f, 0.24f), tide * 0.35f);
            }
            // soot halos over the shop lights (oval along the lamp)
            foreach (var l in GcLamps)
            {
                float dx = (wx - l.x) / 1.1f, dz = (wz - l.y) / 0.55f;
                float d2 = dx * dx + dz * dz;
                if (d2 < 4f) c = Color.Lerp(c, new Color(0.18f, 0.17f, 0.16f), Mathf.Exp(-d2 * 1.6f) * 0.55f * (0.75f + Fbm(wx * 3f, wz * 3f, 2) * 0.5f));
            }
            // rust runs along both flange edges of every I-beam
            foreach (float bz in GarageBeamZ)
            {
                float d = Mathf.Abs(Mathf.Abs(wz - bz) - 0.08f);
                if (d < 0.12f)
                {
                    float run = Mathf.Pow(Mathf.PerlinNoise(wx * 2.2f + bz * 5f, 3.1f), 2.5f) * Fbm(wx * 30f, wz * 12f, 2);
                    c = Color.Lerp(c, new Color(0.42f, 0.24f, 0.12f), (1f - d / 0.12f) * run * 1.4f);
                }
                if (Mathf.Abs(wz - bz) < 0.075f) c *= 0.7f; // shadowed strip right above the flange
            }
            // dusty, darker band along the walls and in the corners
            float wallD = Mathf.Min(LobbyLayout.HalfW - Mathf.Abs(wx), LobbyLayout.HalfD - Mathf.Abs(wz));
            c *= Mathf.Lerp(0.62f, 1f, Step01(0f, 0.9f, wallD) * 0.8f + Fbm(wx * 2f, wz * 2f, 2) * 0.2f);
            c.a = 1f;
            return c;
        });

        private static Texture2D GarageCeilingNormal() => SmoothTex("garage_ceiling16_n", GcW, GcH, (x, y) =>
        {
            var h = GcHeights();
            int w = GcW, hh = GcH;
            float hl = h[y * w + Mathf.Max(0, x - 1)], hr = h[y * w + Mathf.Min(w - 1, x + 1)];
            float hd = h[Mathf.Max(0, y - 1) * w + x], hu = h[Mathf.Min(hh - 1, y + 1) * w + x];
            var n = new Vector3((hl - hr) * 1.2f, (hd - hu) * 1.2f, 1f).normalized;
            return new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
        }, true);
    }
}
