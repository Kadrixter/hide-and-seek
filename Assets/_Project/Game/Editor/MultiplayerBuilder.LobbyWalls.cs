using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HideAndSeek.Game.Editor
{
    /// <summary>
    /// Lobby wall finish: damask wallpaper above, wooden wainscoting below (base board, raised panels, chair rail at
    /// 0.95 m), profiled skirting, crown moulding under the ceiling, wall sockets. Each wall gets a frame with local +X
    /// along the wall (left to right seen from the room), local -Z into the room; gaps leave room for flush objects
    /// (door, mirror, fireplace, settings board, lever).
    /// </summary>
    public static partial class MultiplayerBuilder
    {
        public const float WainscotTop = 0.95f, WainscotBase = 0.012f, PanelLow = 0.30f, PanelHigh = 0.82f;

        /// <summary>Wallpaper strip: 0.6 m wide, the full 3.6 m wall height (128 x 768 px), half-drop damask, 0.6 m motif.</summary>
        public const float WallpaperRepeat = 0.6f;

        private static Texture2D Wallpaper() => SmoothTex("lobby3_wallpaper", 128, 768, (x, y) =>
        {
            float u = x / 128f, v = (y % 128) / 128f;
            float m = Damask(Mathf.Abs(u - 0.5f) * 2f, v);
            float u2 = Mathf.Repeat(u + 0.5f, 1f), v2 = Mathf.Repeat(v + 0.5f, 1f);
            float m2 = Damask(Mathf.Abs(u2 - 0.5f) * 2f, v2) * 0.55f;
            float pat = Mathf.Max(m, m2);
            float stripe = 0.96f + 0.04f * Mathf.Sin(u * Mathf.PI * 16f);
            float paper = 0.95f + 0.05f * Mathf.PerlinNoise(x * 0.31f, y * 0.31f);
            Color bg = new Color(0.15f, 0.22f, 0.19f), fg = new Color(0.25f, 0.31f, 0.24f);
            var c = Color.Lerp(bg, fg, pat * 0.85f) * stripe * paper;
            c.a = 1f;
            return c;
        });

        /// <summary>Damask motif (cu = distance from the centre line 0..1, cv = 0..1 up), 0..1 coverage, soft edges.</summary>
        private static float Damask(float cu, float cv)
        {
            Vector2 p = new Vector2(cu * 0.5f, cv - 0.5f);
            float r = p.magnitude, a = Mathf.Atan2(p.y, p.x);
            // central lily: petals around the centre
            float petals = 0.16f + 0.07f * Mathf.Cos(a * 6f);
            float core = Mathf.Clamp01((petals - r) * 60f);
            // stem and side leaves
            float stem = Mathf.Clamp01((0.012f - Mathf.Abs(p.x)) * 120f) * Mathf.Clamp01((0.42f - Mathf.Abs(p.y)) * 40f);
            Vector2 l = new Vector2(p.x - 0.12f, Mathf.Abs(p.y) - 0.26f);
            float leaf = Mathf.Clamp01((0.06f - (new Vector2(l.x * 0.7f, l.y * 1.6f)).magnitude) * 80f);
            // ring outline around the lily
            float ring = Mathf.Clamp01((0.01f - Mathf.Abs(r - 0.29f)) * 120f) * (Mathf.Sin(a * 12f) > -0.2f ? 1f : 0f);
            return Mathf.Clamp01(core + stem + leaf + ring * 0.8f);
        }

        /// <summary>Procedural texture saved once as PNG, bilinear with mipmaps (walls, rugs, wood).</summary>
        private static Texture2D SmoothTex(string name, int w, int h, Func<int, int, Color> px, bool normal = false)
        {
            string path = $"{LobbyArtDir}/{name}.png";
            if (!File.Exists(path))
            {
                _artGenerated = true;
                var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
                var c = new Color[w * h];
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    c[y * w + x] = px(x, y);
                t.SetPixels(c);
                t.Apply();
                File.WriteAllBytes(path, t.EncodeToPNG());
                Object.DestroyImmediate(t);
                AssetDatabase.ImportAsset(path);
                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                imp.filterMode = FilterMode.Trilinear;
                imp.mipmapEnabled = true;
                imp.anisoLevel = 4;
                imp.wrapMode = TextureWrapMode.Repeat;
                if (normal) imp.textureType = TextureImporterType.NormalMap;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private struct WallSpan
        {
            public string Name;
            public Vector3 Origin;
            public float Yaw, Length;
            public (float a, float b)[] Gaps;     // wainscot / rail gaps (local x)
            public (float a, float b)[] SkirtGaps;
            public float SkirtInset;               // E / W skirting stops short of the N / S skirting
        }

        private static void BuildLobbyWalls(Transform root, Material darkWood, Material trim)
        {
            float hw = LobbyLayout.HalfW, hd = LobbyLayout.HalfD, H = LobbyLayout.H;
            var fin = new GameObject("WallFinish").transform;
            fin.SetParent(root, false);
            // E/W wallpaper gets its own tiling (12 m walls vs 16 m)
            var wallEW = Mat("LobbyWallEW", Color.white, Wallpaper(), 0.08f, new Vector2(LobbyLayout.D / WallpaperRepeat, 1f));
            foreach (var n in new[] { "WallE", "WallW" })
            {
                var w = root.Find(n);
                if (w != null) w.GetComponent<MeshRenderer>().sharedMaterial = wallEW;
            }
            var wains = Mat("LobbyWainscot", new Color(0.46f, 0.31f, 0.2f), Tex("lobby_wood", TexKind.Wood), 0.45f, new Vector2(2f, 0.5f));
            var panel = Mat("LobbyWainscotPanel", new Color(0.52f, 0.36f, 0.23f), Tex("lobby_wood", TexKind.Wood), 0.5f, new Vector2(0.6f, 0.4f));
            var moulding = Mat("LobbyMoulding", new Color(0.3f, 0.19f, 0.11f), null, 0.5f, Vector2.one);
            var crownMat = Mat("LobbyCrown", new Color(0.85f, 0.82f, 0.74f), null, 0.2f, Vector2.one);

            float ex = LobbyLayout.ExitDoorX, mx = LobbyLayout.Mirror.x, fx = LobbyLayout.Fireplace.x, lz = LobbyLayout.Lever.z;
            var spans = new[]
            {
                new WallSpan { Name = "N", Origin = new Vector3(-hw, 0f, hd), Yaw = 0f, Length = LobbyLayout.W,
                    Gaps = new[] { (fx + hw - 0.92f, fx + hw + 0.92f), (0.9f + hw - 1.98f, 0.9f + hw + 1.98f) } },
                new WallSpan { Name = "S", Origin = new Vector3(hw, 0f, -hd), Yaw = 180f, Length = LobbyLayout.W,
                    Gaps = new[] { (hw - ex - 0.62f, hw - ex + 0.62f), (hw - mx - 1.25f, hw - mx + 1.25f) },
                    SkirtGaps = new[] { (hw - ex - 0.57f, hw - ex + 0.57f) } },
                new WallSpan { Name = "E", Origin = new Vector3(hw, 0f, hd), Yaw = 90f, Length = LobbyLayout.D, SkirtInset = 0.03f,
                    Gaps = new[] { (hd - lz - 0.64f, hd - lz + 0.64f) } },
                new WallSpan { Name = "W", Origin = new Vector3(-hw, 0f, -hd), Yaw = -90f, Length = LobbyLayout.D, SkirtInset = 0.03f, Gaps = Array.Empty<(float, float)>() },
            };
            var skirtProf = LobbyMesh.MouldingProfile(0.022f, 0.14f, 0.008f);
            var railProf = RailProfile();
            var crownProf = CrownProfile();
            foreach (var s in spans)
            {
                var f = new GameObject("Wall" + s.Name + "Finish").transform;
                f.SetParent(fin, false);
                f.SetPositionAndRotation(s.Origin, Quaternion.Euler(0f, s.Yaw, 0f));
                float inset = s.SkirtInset;
                foreach (var (a, b) in Runs(inset, s.Length - inset, s.SkirtGaps))
                    Moulding(f, "Skirting", skirtProf, a, b, 0f, moulding);
                foreach (var (a, b) in Runs(inset, s.Length - inset, s.Gaps))
                {
                    // base board, panels between stiles, chair rail on top
                    LocalBox(f, "WainscotBoard", new Vector3((a + b) * 0.5f, WainscotTop * 0.5f, -WainscotBase * 0.5f), new Vector3(b - a, WainscotTop, WainscotBase), wains);
                    Moulding(f, "ChairRail", railProf, a, b, WainscotTop - 0.03f, moulding);
                    float len = b - a;
                    int count = Mathf.Max(1, Mathf.RoundToInt(len / 0.9f));
                    float step = len / count, stile = 0.12f;
                    if (step - stile < 0.25f) continue;
                    for (int i = 0; i < count; i++)
                    {
                        float c = a + step * (i + 0.5f);
                        var pg = LocalBox(f, "WainscotPanel", new Vector3(c, (PanelLow + PanelHigh) * 0.5f, -WainscotBase - 0.004f), new Vector3(step - stile, PanelHigh - PanelLow, 0.008f), panel);
                        RoundCube(pg.transform, 0.006f, 0f);
                    }
                }
                Moulding(f, "Crown", crownProf, inset, s.Length - inset, H - 0.11f, crownMat);
            }
            // wall sockets: on the wainscot base board
            var white = Mat("LobbyPlastic", new Color(0.9f, 0.9f, 0.86f), null, 0.35f, Vector2.one);
            var black = Mat("LobbyPlasticBlack", new Color(0.05f, 0.05f, 0.06f), null, 0.4f, Vector2.one);
            for (int i = 0; i < LobbyLayout.Outlets.Length; i++)
            {
                Vector3 n = LobbyLayout.OutletNormals[i];
                var o = Deco("Outlet", fin, LobbyLayout.Outlets[i], new Vector3(0.08f, 0.08f, 0.012f), white, Quaternion.LookRotation(-n));
                RoundCube(o.transform, 0.006f, 0f);
                foreach (int k in new[] { -1, 1 })
                {
                    var h = Deco("Hole", fin, LobbyLayout.Outlets[i] + n * 0.0062f + Quaternion.LookRotation(-n) * new Vector3(k * 0.014f, 0f, 0f), new Vector3(0.008f, 0.008f, 0.002f), black, Quaternion.LookRotation(-n));
                    h.name = "OutletHole";
                }
            }
        }

        /// <summary>Intervals of [from, to] not covered by the gaps.</summary>
        private static List<(float, float)> Runs(float from, float to, (float a, float b)[] gaps)
        {
            var list = new List<(float, float)>();
            float x = from;
            var sorted = new List<(float a, float b)>(gaps ?? Array.Empty<(float, float)>());
            sorted.Sort((p, q) => p.a.CompareTo(q.a));
            foreach (var (a, b) in sorted)
            {
                if (a > x + 0.05f) list.Add((x, Mathf.Min(a, to)));
                x = Mathf.Max(x, b);
            }
            if (to > x + 0.05f) list.Add((x, to));
            return list;
        }

        private static GameObject LocalBox(Transform parent, string name, Vector3 local, Vector3 size, Material m)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name;
            Object.DestroyImmediate(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = local;
            g.transform.localScale = size;
            g.GetComponent<MeshRenderer>().sharedMaterial = m;
            g.isStatic = true;
            return g;
        }

        private static void Moulding(Transform parent, string name, List<Vector2> prof, float a, float b, float y, Material m)
        {
            float len = b - a;
            var mesh = SaveMesh(LobbyMesh.Sweep(prof, len), $"Mould_{name}_{len:0.000}".Replace(',', '.'));
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = new Vector3(a, y, 0f);
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            g.AddComponent<MeshRenderer>().sharedMaterial = m;
            g.isStatic = true;
        }

        /// <summary>Chair rail: 5 cm high, 3.4 cm deep, rounded nose.</summary>
        private static List<Vector2> RailProfile()
        {
            var p = new List<Vector2> { new(0f, 0f), new(0.02f, 0f) };
            for (int i = 0; i <= 6; i++)
            {
                float a = -Mathf.PI * 0.5f + i / 6f * Mathf.PI;
                p.Add(new Vector2(0.02f + Mathf.Cos(a) * 0.014f, 0.025f + Mathf.Sin(a) * 0.014f));
            }
            p.Add(new Vector2(0.012f, 0.05f));
            p.Add(new Vector2(0f, 0.05f));
            return p;
        }

        /// <summary>Crown moulding: cove between wall and ceiling, 11 cm down, 8 cm out, small steps at both ends.</summary>
        private static List<Vector2> CrownProfile()
        {
            // starts in the wall / ceiling corner: the cove is concave, the end caps fan out from that corner
            var p = new List<Vector2> { new(0f, 0.11f), new(0f, 0f), new(0.012f, 0f), new(0.012f, 0.012f) };
            for (int i = 1; i <= 6; i++)
            {
                float a = i / 6f * Mathf.PI * 0.5f;
                p.Add(new Vector2(0.067f - Mathf.Cos(a) * 0.055f, 0.012f + Mathf.Sin(a) * 0.08f));
            }
            p.Add(new Vector2(0.08f, 0.098f));
            p.Add(new Vector2(0.08f, 0.11f));
            return p;
        }
    }
}
