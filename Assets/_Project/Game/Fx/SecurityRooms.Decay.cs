using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Game
{
    /// <summary>
    /// The room has stood abandoned for years: grime and dust (one multiply-decal mesh per level: blotches, coffee
    /// rings, grit, damp streaks down the walls), cobwebs in the corners and under the desk, two knocked-over bins with
    /// the papers spilled out, crushed cans, old wrappers and loose crumpled papers on the floor, sheets spilled from
    /// the open filing drawer, a fallen binder, a toppled stacking chair by the break table, a dead plant by the south wall
    /// and a poster peeling off the wall. Nothing here has a collider except the toppled chair's seat, and nothing
    /// stands near the swivel chairs, the monitors, the level buttons or the doorway / crate.
    /// </summary>
    public partial class SecurityRooms
    {
        private static Texture2D _grimeAtlas, _webTex;
        private static Material _grimeMat;
        private static readonly Mesh[] _grimeMeshes = new Mesh[Levels];

        private void BuildDecay(Transform root, int lv, float cx, float cz, float y0)
        {
            float fy = y0 + 0.04f;
            BuildGrime(root, lv, cx, cz, y0);
            BuildCobwebs(root, cx, y0);

            // Knocked-over bins: one by the west chair (spilling south-west, away from the chair), one at the counter end.
            TrashBinProp.Create(root, new Vector3(cx - 4.05f, fy, Z1 - 1.5f), 200f, 20 + lv, 6, false, false, true);
            TrashBinProp.Create(root, new Vector3(X0 + 3.85f, fy, Z0 + 0.42f), 25f + lv * 12f, 31 + lv, 5, false, false, true);
            // Loose crumpled papers on the floor.
            TrashBinProp.CreateLitter(root, new Vector3(cx + 0.9f, fy, Z1 - 1.35f), 0f, 40 + lv, 3, 0.3f, false);
            TrashBinProp.CreateLitter(root, new Vector3(X0 + 0.95f, fy, Z0 + 3.9f), 0f, 47 + lv, 2, 0.25f, false);
            TrashBinProp.CreateLitter(root, new Vector3(X0 + 2.0f, fy, Z0 + 1.35f), 0f, 53 + lv, 3, 0.35f, false);

            // Crushed cans on the floor (under the desk, between the chairs, by the bins, in the open).
            CrushedCan(root, new Vector3(cx - 2.55f, fy, Z1 - 0.55f), 30f + lv * 50f, Crush.Stomped, 0, lv);
            CrushedCan(root, new Vector3(cx + 0.65f, fy, Z1 - 1.8f), 75f + lv * 33f, Crush.Squeezed, 1, lv, true);
            CrushedCan(root, new Vector3(cx - 4.4f, fy, Z1 - 2.15f), 140f + lv * 20f, Crush.Flattened, 2, lv);
            CrushedCan(root, new Vector3(X0 + 3.55f, fy, Z0 + 1.15f), 10f + lv * 70f, Crush.Stomped, 1, lv + 1);
            CrushedCan(root, new Vector3(cx + 2.0f, fy, cz - 1.45f), 200f + lv * 15f, Crush.Flattened, 0, lv + 1);
            // Old wrappers.
            Wrapper(root, new Vector3(cx - 1.25f, fy, Z1 - 1.95f), 25f + lv * 40f, new Color(0.7f, 0.15f, 0.1f), 0);
            Wrapper(root, new Vector3(X0 + 2.25f, fy, Z0 + 1.6f), 110f, new Color(0.2f, 0.35f, 0.7f), 1);
            Wrapper(root, new Vector3(cx + 3.35f, fy, Z1 - 1.3f), -40f + lv * 25f, new Color(0.8f, 0.65f, 0.15f), 2);

            // Sheets spilled from the open filing drawer, a fallen binder below the shelf, lying open.
            var paper = new Color(0.78f, 0.75f, 0.64f);
            for (int i = 0; i < 6; i++)
            {
                Vector3 sp = new Vector3(X0 + 0.85f + (i % 3) * 0.17f + (i * 0.37f % 0.1f), fy + 0.001f + i * 0.0012f, Z1 - 2.35f + (i / 3) * 0.3f + (i * 0.23f % 0.12f));
                Box(root, "OldSheet", sp, new Vector3(0.21f, 0.0012f, 0.297f), paper * (0.9f + (i % 2) * 0.08f), false, new Vector3(0f, i * 47f + lv * 20f, i % 2 == 0 ? 0.8f : -0.6f));
            }
            Vector3 fb = new Vector3(X0 + 0.65f, fy, Z1 - 2.95f);
            var binder = new Color(0.45f, 0.12f, 0.1f);
            Box(root, "FallenBinderCover", fb + new Vector3(-0.15f, 0.035f, 0f), new Vector3(0.3f, 0.006f, 0.29f), binder, false, new Vector3(0f, 12f, 14f), Surf.Shiny(0.25f));
            Box(root, "FallenBinderCover", fb + new Vector3(0.15f, 0.035f, 0.01f), new Vector3(0.3f, 0.006f, 0.29f), binder, false, new Vector3(0f, 12f, -14f), Surf.Shiny(0.25f));
            Box(root, "FallenBinderPages", fb + new Vector3(0.12f, 0.03f, 0.02f), new Vector3(0.26f, 0.025f, 0.27f), paper, false, new Vector3(0f, 12f, -10f));

            BuildToppledChair(root, new Vector3(X0 + 3.3f, fy, Z0 + 2.5f), 160f + lv * 9f);
            BuildDeadPlant(root, lv, new Vector3(X0 + 8.7f, fy, Z0 + 0.52f));   // south wall, between the cork board and the coat rack
            BuildPeelingPoster(root, y0);
        }

        // ================================================================ grime decals

        /// <summary>One multiply decal: centre, normal, in-plane "up" (texture v), size, atlas cell (0 blotch, 1 ring, 2 grit, 3 streak), strength.</summary>
        private readonly struct Decal
        {
            public readonly Vector3 C, N, W;
            public readonly Vector2 S;
            public readonly int Cell;
            public readonly float K;
            public Decal(Vector3 c, Vector3 n, Vector3 w, Vector2 s, int cell, float k) { C = c; N = n; W = w; S = s; Cell = cell; K = k; }
        }

        /// <summary>Grime of one level: a single mesh (room-local, y relative to the floor), the contact-shadow multiply shader.</summary>
        private void BuildGrime(Transform root, int lv, float cx, float cz, float y0)
        {
            if (_grimeMat == null)
            {
                var sh = Resources.Load<Shader>("Shaders/HnsContactShadow");
                if (sh == null || !sh.isSupported) return;
                if (_grimeAtlas == null) _grimeAtlas = MakeGrimeAtlas();
                _grimeMat = Keep(new Material(sh) { name = "Sec_Grime", mainTexture = _grimeAtlas });
            }
            if (_grimeMeshes[lv] == null) _grimeMeshes[lv] = Keep(MakeGrimeMesh(lv, cx, cz));
            var g = new GameObject("Grime");
            g.transform.SetParent(root, false);
            g.transform.position = new Vector3(0f, y0, 0f);
            g.AddComponent<MeshFilter>().sharedMesh = _grimeMeshes[lv];
            var r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = _grimeMat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private static Mesh MakeGrimeMesh(int lv, float cx, float cz)
        {
            var d = new List<Decal>();
            var rnd = new System.Random(71 + lv * 13);
            float Rf() => (float)rnd.NextDouble();
            Vector3 Flat(float deg) => Quaternion.Euler(0f, deg, 0f) * Vector3.forward;
            void Floor(float x, float z, float size, int cell, float k, float y = 0.0425f) =>
                d.Add(new Decal(new Vector3(x, y, z), Vector3.up, Flat(Rf() * 360f), new Vector2(size, size * (0.75f + Rf() * 0.5f)), cell, k));

            // Floor: dirt along the walls and under the desk, the worn path from the door, chair scuffs, grit, a dried puddle.
            for (int i = 0; i < 7; i++) Floor(X0 + 1.3f + i * 1.45f + Rf() * 0.4f, Z1 - 0.35f - Rf() * 0.15f, 0.9f + Rf() * 0.5f, 0, 0.42f);
            for (int i = 0; i < 4; i++) Floor(X0 + 0.35f + Rf() * 0.2f, Z0 + 1.6f + i * 1.3f, 0.6f + Rf() * 0.3f, 0, 0.38f);
            for (int i = 0; i < 3; i++) Floor(X1 - 0.35f - Rf() * 0.2f, Z0 + 2.3f + i * 1.4f, 0.6f + Rf() * 0.3f, 0, 0.36f);
            for (int i = 0; i < 6; i++)
            {
                float t = i / 5f;
                Vector3 p = t < 0.4f ? Vector3.Lerp(new Vector3((DoorA + DoorB) * 0.5f, 0f, Z0 + 0.3f), new Vector3((DoorA + DoorB) * 0.5f - 0.2f, 0f, Z0 + 2.6f), t / 0.4f)
                                     : Vector3.Lerp(new Vector3((DoorA + DoorB) * 0.5f - 0.2f, 0f, Z0 + 2.6f), new Vector3(cx + 0.5f, 0f, Z1 - 2.2f), (t - 0.4f) / 0.6f);
                Floor(p.x + (Rf() - 0.5f) * 0.3f, p.z + (Rf() - 0.5f) * 0.3f, 0.75f + Rf() * 0.4f, 0, 0.3f);
            }
            foreach (float sx in new[] { -2.4f, 2.25f }) Floor(cx + sx, Z1 - 2.35f, 1.1f, 0, 0.3f);
            for (int i = 0; i < 9; i++) Floor(X0 + 0.8f + Rf() * 10.4f, Z0 + 0.8f + Rf() * 5.9f, 0.5f + Rf() * 0.5f, 2, 0.6f);
            Floor(X0 + 0.8f, Z0 + 2.2f, 0.75f, 0, 0.5f);                                           // under the water cooler tap
            Floor(X0 + 2.6f, Z0 + 2.15f, 0.9f, 0, 0.35f);                                          // under the break table

            // Desk top: dust, coffee rings (mugs stood there for years), a sticky spill. Kept off the battery spot behind the panel.
            float desk = 0.7715f;
            foreach (float x in new[] { -3.9f, -2.2f, 1.9f, 3.6f }) Floor(cx + x + (Rf() - 0.5f) * 0.4f, Z1 - 0.6f, 0.9f, 0, 0.32f, desk);
            foreach (var (x, z) in new[] { (-3.18f, Z1 - 0.9f), (-3.4f, Z1 - 0.72f), (1.62f, Z1 - 0.48f), (-1.7f, Z1 - 0.88f), (2.7f, Z1 - 0.62f), (3.15f, Z1 - 0.95f) })
                Floor(cx + x, z, 0.1f + Rf() * 0.02f, 1, 0.75f, desk);
            Floor(cx - 1.45f, Z1 - 0.62f, 0.3f, 0, 0.55f, desk);
            // Break table and the counter top.
            Floor(X0 + 2.5f, Z0 + 2.05f, 0.1f, 1, 0.75f, 0.7815f);
            Floor(X0 + 2.75f, Z0 + 2.3f, 0.1f, 1, 0.7f, 0.7815f);
            Floor(X0 + 2.6f, Z0 + 2.15f, 0.6f, 0, 0.35f, 0.7815f);
            for (int i = 0; i < 3; i++) Floor(X0 + 1.2f + i * 0.9f, Z0 + 0.42f, 0.6f, 0, 0.4f, 0.9515f);
            Floor(X0 + 2.9f, Z0 + 0.5f, 0.09f, 1, 0.7f, 0.9515f);

            // Damp streaks running down the walls from the top.
            void WallStreak(Vector3 c, Vector3 n, float w, float h, float k) => d.Add(new Decal(c, n, Vector3.up, new Vector2(w, h), 3, k));
            float nFace = Z1 - 0.1f - 0.0015f, sFace = Z0 + Wall * 0.5f + 0.0015f, wFace = X0 + Wall * 0.5f + 0.013f, eFace = X1 - Wall * 0.5f - 0.0015f;
            WallStreak(new Vector3(X0 + 1.55f, 2.25f, nFace), Vector3.back, 0.55f, 1.3f, 0.55f);
            WallStreak(new Vector3(X1 - 0.75f, 2.2f, nFace), Vector3.back, 0.7f, 1.4f, 0.5f);
            WallStreak(new Vector3(X0 + 4.0f, 2.25f, sFace), Vector3.forward, 0.6f, 1.3f, 0.5f);
            WallStreak(new Vector3(X0 + 6.3f, 2.35f, sFace), Vector3.forward, 0.45f, 1.1f, 0.45f);
            WallStreak(new Vector3(wFace, 2.25f, Z0 + 2.25f), Vector3.right, 0.5f, 1.2f, 0.5f);
            WallStreak(new Vector3(eFace, 2.3f, Z0 + 4.4f), Vector3.left, 0.55f, 1.2f, 0.45f);
            // A grubby splashback above the counter.
            d.Add(new Decal(new Vector3(X0 + 2.1f, 1.15f, sFace), Vector3.forward, Vector3.up, new Vector2(1.6f, 0.5f), 0, 0.4f));

            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var col = new List<Color>();
            var t2 = new List<int>();
            foreach (var q in d)
            {
                Vector3 u = Vector3.Cross(q.N, q.W).normalized * (q.S.x * 0.5f), w = q.W.normalized * (q.S.y * 0.5f);
                float u0 = (q.Cell % 2) * 0.5f + 0.004f, v0 = (q.Cell / 2) * 0.5f + 0.004f, du = 0.492f;
                int b = v.Count;
                v.Add(q.C - u - w); v.Add(q.C + u - w); v.Add(q.C + u + w); v.Add(q.C - u + w);
                uv.Add(new Vector2(u0, v0)); uv.Add(new Vector2(u0 + du, v0)); uv.Add(new Vector2(u0 + du, v0 + du)); uv.Add(new Vector2(u0, v0 + du));
                var c = new Color(1f, 1f, 1f, q.K);
                col.Add(c); col.Add(c); col.Add(c); col.Add(c);
                // Both windings: whichever side faces the viewer is drawn (the other one is culled).
                t2.Add(b); t2.Add(b + 1); t2.Add(b + 2); t2.Add(b); t2.Add(b + 2); t2.Add(b + 3);
                t2.Add(b); t2.Add(b + 2); t2.Add(b + 1); t2.Add(b); t2.Add(b + 3); t2.Add(b + 2);
            }
            var m = new Mesh { name = "Sec_Grime" + lv };
            m.SetVertices(v);
            m.SetUVs(0, uv);
            m.SetColors(col);
            m.SetTriangles(t2, 0);
            m.RecalculateBounds();
            return m;
        }

        /// <summary>2x2 atlas of multiply masks (alpha): soft blotch, coffee ring, grit, a damp streak (strongest at the top).</summary>
        private static Texture2D MakeGrimeAtlas()
        {
            const int n = 256, h = n / 2;
            var px = new Color32[n * n];
            var rnd = new System.Random(5);
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int cell = (x / h) + (y / h) * 2;
                float lx = (x % h + 0.5f) / h * 2f - 1f, ly = (y % h + 0.5f) / h * 2f - 1f;    // -1..1 inside the cell
                float d = Mathf.Sqrt(lx * lx + ly * ly), ang = Mathf.Atan2(ly, lx);
                float edge = Mathf.Clamp01((1f - Mathf.Max(Mathf.Abs(lx), Mathf.Abs(ly))) * 12f);   // fade at the cell border
                float a;
                switch (cell)
                {
                    case 0:
                    {
                        float nz = SecurityTextures.Fbm(x * 0.045f + 3f, y * 0.045f, 4);
                        a = Mathf.Clamp01((1f - d) * 1.8f) * Mathf.Clamp01(nz * 2.2f - 0.45f);
                        break;
                    }
                    case 1:
                    {
                        float rr = 0.72f + 0.03f * Mathf.Sin(3f * ang + 1f) + 0.02f * Mathf.Sin(7f * ang);
                        float ring = Mathf.Exp(-Mathf.Pow((d - rr) / (0.035f + 0.02f * Mathf.Sin(ang * 2f + 0.5f)), 2f));
                        float rr2 = 0.66f, d2 = Mathf.Sqrt((lx - 0.12f) * (lx - 0.12f) + (ly + 0.08f) * (ly + 0.08f));
                        float ring2 = Mathf.Exp(-Mathf.Pow((d2 - rr2) / 0.025f, 2f)) * Mathf.Clamp01(Mathf.Sin(ang * 1.3f + 2f));
                        a = ring * (0.65f + 0.35f * SecurityTextures.Noise(ang * 6f + 10f, 1f)) + ring2 * 0.5f + (d < rr ? 0.12f : 0f);
                        break;
                    }
                    case 2:
                    {
                        float falloff = Mathf.Clamp01(1f - d);
                        float speck = rnd.NextDouble() > 0.965 ? 0.55f + (float)rnd.NextDouble() * 0.45f : 0f;
                        a = falloff * (speck + 0.22f * Mathf.Clamp01(SecurityTextures.Fbm(x * 0.08f, y * 0.08f + 7f, 3) * 2f - 0.7f));
                        break;
                    }
                    default:
                    {
                        // ly = +1 at the top. Wavy column, wider and stronger at the top, a tide line where it stops.
                        float top = (ly + 1f) * 0.5f;
                        float cxw = 0.15f * Mathf.Sin(ly * 4f + 1f) + 0.1f * (SecurityTextures.Noise(ly * 3f, 4f) - 0.5f);
                        float wdt = 0.35f + 0.45f * top;
                        float across = Mathf.Clamp01(1f - Mathf.Abs(lx - cxw) / wdt);
                        float runs = 0.6f + 0.4f * SecurityTextures.Fbm(lx * 9f + 2f, ly * 1.2f, 3);
                        float stop = 0.25f + 0.35f * SecurityTextures.Noise(lx * 5f + 9f, 2f);
                        float body = Mathf.Clamp01((top - stop) * 4f);
                        float tide = Mathf.Exp(-Mathf.Pow((top - stop) / 0.03f, 2f)) * 0.5f;
                        a = (across * across * runs * (0.35f + 0.65f * top) * body + tide * across) * Mathf.Clamp01(1.4f - Mathf.Abs(lx) * 1.2f);
                        break;
                    }
                }
                px[y * n + x] = new Color32(0, 0, 0, (byte)(Mathf.Clamp01(a * edge) * 255f));
            }
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "SecGrime", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return Keep(tex);
        }

        // ================================================================ cobwebs

        private void BuildCobwebs(Transform root, float cx, float y0)
        {
            if (_webTex == null) _webTex = MakeCobwebTexture();
            float top = y0 + Height - 0.04f;
            // Ceiling corners (the north-west one is taken by the cable tray), then two low ones under the desk.
            CornerWeb(root, new Vector3(X1 - Wall * 0.5f, top, Z1 - 0.1f), Vector3.left, Vector3.back, 0.75f, 0.6f);
            CornerWeb(root, new Vector3(X0 + Wall * 0.5f, top, Z0 + Wall * 0.5f), Vector3.forward, Vector3.right, 0.65f, 0.55f);
            CornerWeb(root, new Vector3(X1 - Wall * 0.5f, top, Z0 + Wall * 0.5f), Vector3.left, Vector3.forward, 0.6f, 0.5f);
            CornerWeb(root, new Vector3(cx + 3.74f, y0 + 0.715f, Z1 - 0.2f), Vector3.left, Vector3.back, 0.42f, 0.38f);
            CornerWeb(root, new Vector3(cx - 3.74f, y0 + 0.715f, Z1 - 0.2f), Vector3.back, Vector3.right, 0.36f, 0.32f);
        }

        /// <summary>Web spanning a corner diagonally: its edges on the two faces (directions <paramref name="a"/>, <paramref name="b"/> from the corner), top at <paramref name="corner"/>.</summary>
        private void CornerWeb(Transform root, Vector3 corner, Vector3 a, Vector3 b, float width, float height)
        {
            float k = width / Mathf.Sqrt(2f);
            Vector3 c = corner + (a + b) * (k * 0.5f) + Vector3.down * (height * 0.5f);
            // Facing the room (away from the corner); the quad's right runs from face b to face a.
            Sign(root, "Cobweb", c, new Vector2(width, height), (a + b).normalized, _webTex, 0.05f, true);
        }

        /// <summary>Hanging web (alpha-tested): spokes from a hub to anchors on the top and both sides, a sagging spiral, a few broken threads, dust clumps.</summary>
        private static Texture2D MakeCobwebTexture()
        {
            const int n = 256;
            var px = new Color32[n * n];
            var silk = new Color32(196, 194, 188, 255);
            var rnd = new System.Random(9);
            float Rf() => (float)rnd.NextDouble();
            void Dot(float x, float y, float r)
            {
                int x0 = Mathf.FloorToInt((x - r) * n), x1 = Mathf.CeilToInt((x + r) * n), y0 = Mathf.FloorToInt((y - r) * n), y1 = Mathf.CeilToInt((y + r) * n);
                for (int yy = Mathf.Max(0, y0); yy <= Mathf.Min(n - 1, y1); yy++)
                for (int xx = Mathf.Max(0, x0); xx <= Mathf.Min(n - 1, x1); xx++)
                {
                    float dx = (xx + 0.5f) / n - x, dy = (yy + 0.5f) / n - y;
                    if (dx * dx + dy * dy <= r * r) px[yy * n + xx] = silk;
                }
            }
            void Line(Vector2 p, Vector2 q, float r, float sag = 0f)
            {
                int steps = Mathf.CeilToInt((q - p).magnitude * n * 1.5f) + 1;
                for (int i = 0; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    Vector2 s = Vector2.Lerp(p, q, t) + Vector2.down * (sag * 4f * t * (1f - t));
                    Dot(s.x, s.y, r);
                }
            }
            Vector2 hub = new Vector2(0.5f, 0.6f);
            var anchors = new List<Vector2>();
            for (int i = 0; i < 4; i++) anchors.Add(new Vector2(0.995f, 0.3f + i * 0.2f + Rf() * 0.05f));      // right side, bottom to top
            for (int i = 0; i < 5; i++) anchors.Add(new Vector2(0.85f - i * 0.175f + Rf() * 0.04f, 0.995f));   // top, right to left
            for (int i = 0; i < 4; i++) anchors.Add(new Vector2(0.005f, 0.9f - i * 0.2f - Rf() * 0.05f));      // left side, top to bottom
            anchors.Add(new Vector2(0.3f, 0.04f));                                                             // a long thread down
            anchors.Add(new Vector2(0.72f, 0.08f));
            foreach (var an in anchors) Line(hub, an, 0.0045f);
            // Spiral: consecutive spokes joined at growing distances, sagging, a gap where it tore.
            for (int ring = 1; ring <= 8; ring++)
            {
                float f = ring / 9f;
                for (int s = 0; s < anchors.Count - 1; s++)
                {
                    if ((ring == 6 && s > 3 && s < 7) || (ring == 3 && s == 10)) continue;
                    Vector2 p = Vector2.Lerp(hub, anchors[s], f), q = Vector2.Lerp(hub, anchors[s + 1], f);
                    Line(p, q, 0.0032f, 0.008f * f);
                }
            }
            // Broken threads hanging down, dust clumps caught in the web.
            for (int i = 0; i < 4; i++)
            {
                Vector2 p = Vector2.Lerp(hub, anchors[3 + i * 2], 0.5f + Rf() * 0.3f);
                Line(p, p + new Vector2((Rf() - 0.5f) * 0.08f, -0.15f - Rf() * 0.2f), 0.003f);
            }
            for (int i = 0; i < 9; i++)
            {
                Vector2 p = Vector2.Lerp(hub, anchors[rnd.Next(anchors.Count)], 0.2f + Rf() * 0.7f);
                Dot(p.x, p.y, 0.008f + Rf() * 0.01f);
            }
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "SecCobweb", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return Keep(tex);
        }

        // ================================================================ furniture wrecks

        /// <summary>Plastic stacking chair lying on its side (legs pointing along local -x before the yaw). The seat has a collider.</summary>
        private void BuildToppledChair(Transform root, Vector3 floor, float yaw)
        {
            var q = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(0f, 0f, 90f);
            Vector3 o = floor + Vector3.up * 0.212f;
            Vector3 P(Vector3 local) => o + q * local;
            Vector3 E(Vector3 euler) => (q * Quaternion.Euler(euler)).eulerAngles;
            var plastic = new Color(0.22f, 0.27f, 0.33f);
            var steel = new Color(0.32f, 0.32f, 0.31f);
            Box(root, "ToppledSeat", P(new Vector3(0f, 0.45f, 0.01f)), new Vector3(0.42f, 0.03f, 0.4f), plastic, true, E(Vector3.zero), Surf.Shiny(0.3f));
            Box(root, "ToppledBack", P(new Vector3(0f, 0.74f, -0.2f)), new Vector3(0.42f, 0.28f, 0.025f), plastic, false, E(new Vector3(-8f, 0f, 0f)), Surf.Shiny(0.3f));
            foreach (float sx in new[] { -0.18f, 0.18f })
            {
                foreach (float sz in new[] { -0.17f, 0.17f })
                    Prim(root, PrimitiveType.Cylinder, "ToppledLeg", P(new Vector3(sx, 0.22f, sz)), new Vector3(0.022f, 0.22f, 0.022f), steel, E(Vector3.zero), false, Surf.Shiny(0.4f, 0.7f));
                Prim(root, PrimitiveType.Cylinder, "ToppledUpright", P(new Vector3(sx, 0.62f, -0.19f)), new Vector3(0.02f, 0.17f, 0.02f), steel, E(new Vector3(-8f, 0f, 0f)), false, Surf.Shiny(0.4f, 0.7f));
            }
        }

        /// <summary>Dead potted plant: dry soil, bare grey-brown stems drooping, a few curled leaves left, more on the floor.</summary>
        private void BuildDeadPlant(Transform root, int lv, Vector3 floor)
        {
            var pot = CachedMesh("PlantPot", p => p.Lathe(new[] { new Vector2(0f, 0f), new Vector2(0.11f, 0f), new Vector2(0.15f, 0.3f), new Vector2(0.162f, 0.31f), new Vector2(0.162f, 0.34f),
                new Vector2(0.15f, 0.34f), new Vector2(0.142f, 0.32f) }, 22, 40f));
            MeshObj(root, "DeadPlantPot", pot, floor, Quaternion.identity, new Color(0.5f, 0.32f, 0.22f), Surf.Shiny(0.1f));
            Prim(root, PrimitiveType.Cylinder, "DrySoil", floor + Vector3.up * 0.3f, new Vector3(0.28f, 0.004f, 0.28f), new Color(0.22f, 0.18f, 0.14f));
            var stem = new Color(0.36f, 0.3f, 0.22f);
            var leaf = new Color(0.42f, 0.32f, 0.17f);
            var rnd = new System.Random(17 + lv);
            float Rf() => (float)rnd.NextDouble();
            var leafMesh = CachedMesh("DryLeaf", p => p.At(Vector3.zero, Quaternion.identity, new Vector3(1f, 0.15f, 2f)).RoundBox(new Vector3(0.04f, 0.04f, 0.04f), 0.019f, 3));
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f + Rf() * 30f, lean = 15f + Rf() * 30f, len = 0.35f + Rf() * 0.35f;
                var dir = Quaternion.Euler(0f, a, 0f);
                // Rising, then bending over and drooping at the tip.
                var pts = new[]
                {
                    floor + Vector3.up * 0.3f + dir * new Vector3(0.02f, 0f, 0f),
                    floor + Vector3.up * (0.3f + len * 0.5f) + dir * new Vector3(len * 0.25f * Mathf.Sin(lean * Mathf.Deg2Rad), 0f, 0f),
                    floor + Vector3.up * (0.3f + len * 0.85f) + dir * new Vector3(len * 0.55f * Mathf.Sin(lean * Mathf.Deg2Rad) + 0.05f, 0f, 0f),
                    floor + Vector3.up * (0.3f + len * 0.7f) + dir * new Vector3(len * 0.6f * Mathf.Sin(lean * Mathf.Deg2Rad) + 0.14f, 0f, 0f),
                };
                var key = "DeadStem" + lv + "_" + i;
                MeshObj(root, "DeadStem", CachedMesh(key, p => p.Tube(pts, 0.0045f, 5)), Vector3.zero, Quaternion.identity, stem, Surf.Shiny(0.1f), false, false);
                if (i % 2 == 0)
                    MeshObj(root, "DryLeaf", leafMesh, pts[3] + Vector3.down * 0.015f, Quaternion.Euler(50f + Rf() * 30f, a, Rf() * 40f), leaf, Surf.Shiny(0.1f), false, false);
            }
            for (int i = 0; i < 5; i++)
                MeshObj(root, "FallenLeaf", leafMesh, floor + new Vector3((Rf() - 0.5f) * 0.7f, 0.004f, 0.12f + Rf() * 0.4f), Quaternion.Euler(Rf() * 10f, Rf() * 360f, Rf() * 10f),
                    leaf * (0.85f + Rf() * 0.2f), Surf.Shiny(0.1f), false, false);
        }

        private static Mesh _posterFlap, _posterGap;

        /// <summary>The safety poster on the south wall has come loose: the bottom right corner folded off the wall (yellowed back out), bare wall under it, old tape.</summary>
        private void BuildPeelingPoster(Transform root, float y0)
        {
            float sIn = Z0 + Wall * 0.5f + 0.006f;
            Vector3 corner = new Vector3(X0 + 2.0f + 0.21f, y0 + 1.55f - 0.295f, sIn + 0.001f);   // bottom right of PosterBhp (0.42 x 0.59)
            if (_posterFlap == null)
            {
                // Hinge A-B across the corner; the triangle swung ~120 degrees off the wall into the room (+z).
                Vector3 a = new Vector3(-0.11f, 0f, 0f), b = new Vector3(0f, 0.11f, 0f), mid = (a + b) * 0.5f;
                Vector3 tip = mid + Quaternion.AngleAxis(-120f, (b - a).normalized) * (Vector3.zero - mid);
                _posterFlap = Keep(Triangle("Sec_PosterFlap", a, b, tip));
                _posterGap = Keep(Triangle("Sec_PosterGap", a, b, Vector3.zero));
            }
            MeshObj(root, "PosterFlap", _posterFlap, corner, Quaternion.identity, new Color(0.72f, 0.68f, 0.56f), Surf.Shiny(0.05f), false, false);
            MeshObj(root, "PosterGap", _posterGap, corner, Quaternion.identity, WallPaint * 1.04f, Surf.T(SecurityTextures.Kind.Concrete, 2.4f, 0.05f), false, false);
            Box(root, "PosterTape", new Vector3(X0 + 2.0f - 0.16f, y0 + 1.55f + 0.285f, sIn + 0.002f), new Vector3(0.07f, 0.025f, 0.002f), new Color(0.7f, 0.6f, 0.35f), false, new Vector3(0f, 0f, 20f));
        }

        /// <summary>Single triangle visible from both sides.</summary>
        private static Mesh Triangle(string name, Vector3 a, Vector3 b, Vector3 c)
        {
            var m = new Mesh { name = name };
            m.SetVertices(new List<Vector3> { a, b, c, a, b, c });
            m.SetUVs(0, new List<Vector2> { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f) });
            m.SetTriangles(new[] { 0, 1, 2, 3, 5, 4 }, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }
}
