using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>Small generated textures / meshes of the fuse box: hazard stripes, the wiring sheet in the door, a label quad.</summary>
    public static class FuseBoxTextures
    {
        private static Texture2D _hazard, _sheet;

        /// <summary>Yellow / black diagonal stripes, a little worn (tiles along u).</summary>
        public static Texture2D Hazard()
        {
            if (_hazard != null) return _hazard;
            const int n = 64;
            var px = new Color32[n * n];
            var rnd = new System.Random(77);
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                bool yellow = ((x + y) / 16) % 2 == 0;
                float g = (float)rnd.NextDouble() * 0.08f;
                Color c = yellow ? new Color(0.95f - g, 0.75f - g, 0.1f) : new Color(0.06f + g * 0.3f, 0.06f, 0.06f);
                if (rnd.NextDouble() < 0.03) c = Color.Lerp(c, new Color(0.45f, 0.47f, 0.47f), 0.7f); // scratches to the metal
                px[y * n + x] = c;
            }
            _hazard = Make(px, n, n, TextureWrapMode.Repeat, "FuseHazard");
            return _hazard;
        }

        /// <summary>Yellowed wiring diagram glued inside the door: title bar, a few boxes and lines, a grid of text lines.</summary>
        public static Texture2D Sheet()
        {
            if (_sheet != null) return _sheet;
            const int w = 96, h = 128;
            var px = new Color32[w * h];
            var rnd = new System.Random(5);
            var paper = new Color(0.86f, 0.83f, 0.7f);
            var ink = new Color(0.18f, 0.2f, 0.28f);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float g = (float)rnd.NextDouble() * 0.05f;
                Color c = paper - new Color(g, g, g * 1.5f, 0f);
                bool line = false;
                if (y > h - 14 && y < h - 6 && x > 8 && x < w - 8) line = (x / 3 + y) % 4 != 0; // title
                if (y > 60 && y < 100 && (x == 12 || x == 84 || y == 61 || y == 99)) line = true; // frame
                for (int k = 0; k < 4; k++)
                {
                    int bx = 18 + k * 17;
                    if (y > 70 && y < 90 && (x == bx || x == bx + 10) && x > 0) line = true;
                    if ((y == 70 || y == 90) && x >= bx && x <= bx + 10) line = true;
                    if (x == bx + 5 && y >= 90 && y < 99) line = true;
                }
                if (y < 52 && y > 8 && y % 6 == 0 && x > 10 && x < w - 10 - (y * 7 % 23)) line = true; // text lines
                if (line) c = Color.Lerp(c, ink, 0.8f);
                if (x < 2 || y < 2 || x >= w - 2 || y >= h - 2) c *= 0.85f;
                c.a = 1f;
                px[y * w + x] = c;
            }
            _sheet = Make(px, w, h, TextureWrapMode.Clamp, "FuseSheet");
            return _sheet;
        }

        private static Texture2D Make(Color32[] px, int w, int h, TextureWrapMode wrap, string name)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = wrap, filterMode = FilterMode.Trilinear, anisoLevel = 4, name = name, hideFlags = HideFlags.DontSave };
            t.SetPixels32(px);
            t.Apply(true, false);
            return t;
        }

        /// <summary>Unit quad (1 x 1) facing +Z, readable from the front (u to the viewer's right).</summary>
        public static Mesh Quad()
        {
            var m = new Mesh { name = "FuseLabelQuad" };
            m.vertices = new[] { new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
            m.normals = new[] { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            m.RecalculateBounds();
            return m;
        }

        /// <summary>
        /// UVs of the door mesh (built without them): the hazard band tiles its stripes along its length, the sheet gets
        /// the whole texture, the rest a planar mapping. Every PaMesh face has its own vertices, so per-submesh is exact.
        /// </summary>
        public static void MapDoorUvs(Mesh door, float width)
        {
            var v = door.vertices;
            var uv = new Vector2[v.Length];
            for (int i = 0; i < v.Length; i++) uv[i] = new Vector2(-v[i].x / width, v[i].y + 0.5f);
            for (int sub = 1; sub <= 2; sub++)
            {
                var tri = door.GetTriangles(sub);
                if (tri.Length == 0) continue;
                Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = -min;
                foreach (int k in tri) { min = Vector2.Min(min, v[k]); max = Vector2.Max(max, v[k]); }
                Vector2 size = Vector2.Max(max - min, Vector2.one * 1e-4f);
                foreach (int k in tri)
                {
                    float u = (max.x - v[k].x) / size.x, w = (v[k].y - min.y) / size.y;
                    // Sheet is seen from inside the door (its back face): u runs the other way.
                    if (sub == 2) u = 1f - u;
                    uv[k] = sub == 1 ? new Vector2(u * size.x / size.y, w) : new Vector2(u, w);
                }
            }
            door.uv = uv;
        }
    }
}
