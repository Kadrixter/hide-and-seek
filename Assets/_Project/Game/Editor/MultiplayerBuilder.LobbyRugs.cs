using System.Collections.Generic;
using HideAndSeek.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Game.Editor
{
    /// <summary>
    /// Rugs: Persian rug under the lounge (border bands, medallion, wool), round braided rug at the chess spot, kilim
    /// runner by the exit, 80s arcade carpet in front of the SNAKE cabinets. 12 mm thick with rounded edges, lifted
    /// half a millimetre off the floor (no shared faces), cotton fringes on the short ends; carpet footsteps.
    /// </summary>
    public static partial class MultiplayerBuilder
    {
        private static void BuildLobbyRugs(Transform root)
        {
            var t = new GameObject("Rugs").transform;
            t.SetParent(root, false);
            Vector3 lounge = new Vector3(-4.15f, 0f, 3.1f);
            Rug(t, "RugLounge", lounge, new Vector2(3.9f, 3.0f), 0f, Mat("LobbyRugPersian", Color.white, PersianTex(), 0.02f, Vector2.one), true);
            RoundRug(t, "RugChess", LobbyLayout.ChessSpot, 1.0f);
            Rug(t, "RugRunner", new Vector3(LobbyLayout.ExitDoorX, 0f, -LobbyLayout.HalfD + 2.0f), new Vector2(0.9f, 1.8f), 0f, Mat("LobbyRugKilim", Color.white, KilimTex(), 0.02f, Vector2.one), true);
            float ax = (LobbyLayout.Arcades[0].pos.x + LobbyLayout.Arcades[1].pos.x) * 0.5f;
            Rug(t, "RugArcade", new Vector3(ax, 0f, LobbyLayout.Arcades[0].pos.z - 0.95f), new Vector2(2.2f, 1.0f), 0f, Mat("LobbyRugArcade", Color.white, ArcadeCarpetTex(), 0.02f, new Vector2(2.2f, 1f)), false);
        }

        private const float RugH = 0.012f, RugLift = 0.0005f;

        private static void Rug(Transform parent, string name, Vector3 centre, Vector2 size, float yaw, Material m, bool fringe)
        {
            var g = LocalBox(parent, name, Vector3.zero, new Vector3(size.x, RugH, size.y), m);
            g.transform.SetPositionAndRotation(centre + Vector3.up * (RugLift + RugH * 0.5f), Quaternion.Euler(0f, yaw, 0f));
            RoundCube(g.transform, 0.005f, 0f);
            g.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            var zone = g.AddComponent<FootstepSurface>();
            zone.Type = SurfaceType.Carpet;
            zone.ZoneSize = new Vector3(size.x, 0.6f, size.y);
            zone.Priority = 5;
            if (!fringe) return;
            var cotton = Mat("LobbyRugFringe", new Color(0.88f, 0.84f, 0.74f), null, 0.05f, Vector2.one);
            var mesh = LobbyMeshSaved($"Fringe_{size.x:0.00}".Replace(',', '.'), () => FringeMesh(size.x));
            foreach (float s in new[] { -1f, 1f })
            {
                var f = MeshObj("Fringe", g.transform, mesh, cotton, new Vector3(0f, -RugH * 0.5f + 0.0015f, s * size.y * 0.5f), Quaternion.Euler(0f, s > 0 ? 0f : 180f, 0f), false);
            }
        }

        /// <summary>Row of cotton strands along X, hanging out towards +Z, slightly wavy, lying on the floor.</summary>
        private static Mesh FringeMesh(float width)
        {
            var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var parts = new List<CombineInstance>();
            int n = Mathf.RoundToInt(width / 0.014f);
            for (int i = 0; i < n; i++)
            {
                float x = -width * 0.5f + 0.01f + i * (width - 0.02f) / (n - 1);
                float len = 0.06f + 0.012f * Hash01(i * 7 + 3);
                float yaw = (Hash01(i * 13 + 5) - 0.5f) * 14f;
                parts.Add(new CombineInstance
                {
                    mesh = cube,
                    transform = Matrix4x4.TRS(new Vector3(x, 0f, len * 0.5f - 0.004f), Quaternion.Euler(0f, yaw, 0f), new Vector3(0.0045f, 0.0025f, len)),
                });
            }
            var m = new Mesh();
            m.CombineMeshes(parts.ToArray(), true, true);
            return m;
        }

        private static void RoundRug(Transform parent, string name, Vector3 centre, float r)
        {
            var mat = Mat("LobbyRugBraid", Color.white, SmoothTex("lobby2_rug_braid", 256, 256, (x, y) =>
            {
                float u = x / 128f - 1f, v = y / 128f - 1f, d = Mathf.Sqrt(u * u + v * v);
                int band = (int)(d * 11f);
                Color[] cols = { new(0.72f, 0.36f, 0.2f), new(0.86f, 0.8f, 0.64f), new(0.2f, 0.32f, 0.42f), new(0.55f, 0.5f, 0.32f) };
                var c = cols[band % cols.Length];
                // braid: diagonal ridges inside each band
                float a = Mathf.Atan2(v, u);
                float ridge = 0.85f + 0.15f * Mathf.Sin((a * 60f + Mathf.Repeat(d * 11f, 1f) * 6f) * (band % 2 == 0 ? 1f : -1f));
                float edge = 1f - 0.3f * Mathf.Pow(Mathf.Abs(Mathf.Repeat(d * 11f, 1f) - 0.5f) * 2f, 6f);
                return c * ridge * edge;
            }), 0.02f, Vector2.one);
            var mesh = LobbyMeshSaved($"RugDisc_{r:0.00}".Replace(',', '.'), () => LobbyMesh.Lathe(new[]
            {
                new Vector2(r - 0.006f, 0f), new Vector2(r, 0.004f), new Vector2(r, RugH - 0.004f), new Vector2(r - 0.006f, RugH),
            }, 64, true, true));
            var g = MeshObj(name, parent, mesh, mat, centre + Vector3.up * RugLift, null, false);
            // lathe caps map u/v around the centre: good for a round rug
            var zone = g.AddComponent<FootstepSurface>();
            zone.Type = SurfaceType.Carpet;
            zone.ZoneSize = new Vector3(r * 1.6f, 0.6f, r * 1.6f);
            zone.Priority = 5;
        }

        private static Texture2D PersianTex() => SmoothTex("lobby2_rug_persian", 512, 384, (x, y) =>
        {
            float u = x / 511f, v = y / 383f;
            // distance to the edge in "rug units" (the long side = 1)
            float ex = Mathf.Min(u, 1f - u) * 1.3f, ey = Mathf.Min(v, 1f - v);
            float e = Mathf.Min(ex, ey);
            Color red = new(0.46f, 0.08f, 0.07f), navy = new(0.1f, 0.14f, 0.26f), cream = new(0.86f, 0.78f, 0.6f), gold = new(0.75f, 0.55f, 0.22f), rust = new(0.62f, 0.25f, 0.12f);
            Color c;
            if (e < 0.012f) c = navy;
            else if (e < 0.02f) c = cream;
            else if (e < 0.085f)
            {
                // main border: navy with a running vine of cream / gold
                float along = ex < ey ? v : u;
                float s = Mathf.Sin(along * 90f) * 0.02f + 0.0525f;
                c = Mathf.Abs(e - s) < 0.008f ? cream : (Mathf.Repeat(along * 45f, 1f) < 0.25f && Mathf.Abs(e - 0.052f) < 0.018f ? gold : navy);
            }
            else if (e < 0.093f) c = cream;
            else if (e < 0.1f) c = rust;
            else
            {
                // field: red with a lattice of small motifs, central medallion
                float fu = (u - 0.5f) * 1.3f, fv = v - 0.5f;
                float md = Mathf.Abs(fu) / 0.32f + Mathf.Abs(fv) / 0.26f; // diamond medallion
                float lattice = Mathf.Abs(Mathf.Sin(u * 52f) * Mathf.Sin(v * 40f));
                c = red;
                if (lattice > 0.86f) c = rust;
                if (Mathf.Abs(Mathf.Sin(u * 26f + v * 20f)) < 0.05f || Mathf.Abs(Mathf.Sin(u * 26f - v * 20f)) < 0.05f) c = Color.Lerp(c, gold, 0.5f);
                if (md < 1f) c = md < 0.25f ? cream : md < 0.32f ? gold : md < 0.9f ? (Mathf.Repeat(md * 8f, 1f) < 0.2f ? cream : navy) : gold;
                // corner spandrels
                float cd = Mathf.Min(Mathf.Min(u, 1f - u) * 1.3f + Mathf.Min(v, 1f - v), 9f);
                if (cd < 0.3f && cd > 0.2f) c = navy;
            }
            float wool = 0.88f + 0.12f * Mathf.PerlinNoise(x * 0.6f, y * 0.6f) + 0.04f * Hash01(x * 31 + y * 17);
            float wear = 0.94f + 0.06f * Mathf.PerlinNoise(u * 4f, v * 4f);
            return c * wool * wear;
        });

        private static Texture2D KilimTex() => SmoothTex("lobby2_rug_kilim", 128, 256, (x, y) =>
        {
            float u = x / 127f, v = y / 255f;
            Color sand = new(0.8f, 0.68f, 0.48f), brick = new(0.6f, 0.2f, 0.12f), teal = new(0.12f, 0.36f, 0.36f), dark = new(0.18f, 0.12f, 0.1f);
            float e = Mathf.Min(Mathf.Min(u, 1f - u) * 0.5f, Mathf.Min(v, 1f - v));
            if (e < 0.02f) return dark;
            if (e < 0.04f) return brick;
            // stepped diamonds along the runner
            float cy = Mathf.Repeat(v * 5f, 1f) - 0.5f, cx = u - 0.5f;
            float dmd = Mathf.Abs(cx) * 1.6f + Mathf.Abs(cy);
            float step = Mathf.Floor(dmd * 10f) / 10f;
            var c = step < 0.15f ? teal : step < 0.25f ? sand : step < 0.4f ? brick : step < 0.5f ? dark : sand;
            return c * (0.9f + 0.1f * Mathf.PerlinNoise(x * 0.8f, y * 0.8f));
        });

        private static Texture2D ArcadeCarpetTex() => SmoothTex("lobby2_rug_arcade", 256, 256, (x, y) =>
        {
            float u = x / 256f, v = y / 256f;
            var c = new Color(0.05f, 0.04f, 0.09f);
            // neon squiggles, triangles and dots on black (classic arcade carpet)
            if (Mathf.Abs(Mathf.Sin(u * 18f + Mathf.Sin(v * 9f) * 2f) - (v * 2f - 1f)) < 0.05f) c = new Color(0.95f, 0.2f, 0.7f);
            float tu = Mathf.Repeat(u * 5f + 0.3f, 1f), tv = Mathf.Repeat(v * 4f + 0.6f, 1f);
            if (tv < 0.35f && Mathf.Abs(tu - 0.5f) < tv * 0.45f && tv > 0.05f) c = new Color(0.2f, 0.85f, 1f);
            float du = Mathf.Repeat(u * 9f, 1f) - 0.5f, dv = Mathf.Repeat(v * 9f + Mathf.Floor(u * 9f) * 0.5f, 1f) - 0.5f;
            if (du * du + dv * dv < 0.012f) c = new Color(1f, 0.85f, 0.2f);
            if (Mathf.Abs(Mathf.Repeat(u * 3f + v * 3f, 1f) - 0.5f) < 0.02f && Mathf.Repeat(v * 12f, 1f) < 0.5f) c = new Color(0.4f, 1f, 0.35f);
            return c * (0.85f + 0.15f * Hash01(x * 13 + y * 29));
        });
    }
}
