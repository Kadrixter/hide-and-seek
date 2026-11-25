using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames.Pool
{
    /// <summary>
    /// Cube of billiard chalk (22 mm) in its paper wrapper. The chalk: a fine height-field top with softly rounded
    /// edges and, with wear, the dish ground in by the cue tip (<see cref="PoolChalkArt.Dish"/>); flat sides and
    /// bottom. The wrapper: four printed sides a hair outside the chalk, wrapped under the bottom, its top edge just
    /// under the chalk's when new and torn lower and ragged with wear, with a thin rim showing the paper's thickness.
    /// Origin: the middle of the bottom (resting surface), +Y up. Meshes and textures are cached per wear tenth.
    /// </summary>
    public static class PoolChalkModel
    {
        public const float Size = 0.022f, Bevel = 0.0006f, Gap = 0.00025f, PaperT = 0.0002f;

        static readonly Dictionary<int, Mesh> Bodies = new(), Wraps = new();

        public static Transform Build(Transform parent, Material template, float wear01)
        {
            int step = PoolChalkArt.Step(wear01);
            var root = new GameObject("PoolChalk").transform;
            if (parent != null)
            {
                root.gameObject.layer = parent.gameObject.layer;
                root.SetParent(parent, false);
            }
            Part("Chalk", root, Body(step), Mat(template, PoolChalkArt.Top(step / 10f), 0.07f));
            Part("Wrapper", root, Wrapper(step), Mat(template, PoolChalkArt.Wrapper(step / 10f), 0.32f));
            return root;
        }

        // ================================================================ chalk body

        /// <summary>Height of the chalk top at (x, z) from the cube's middle.</summary>
        static float TopHeight(float x, float z, float wear)
        {
            const float half = Size * 0.5f, b = Bevel;
            float qx = Mathf.Max(Mathf.Abs(x) - (half - b), 0f), qz = Mathf.Max(Mathf.Abs(z) - (half - b), 0f);
            float q = Mathf.Min(Mathf.Sqrt(qx * qx + qz * qz), b);
            float drop = b - Mathf.Sqrt(Mathf.Max(0f, b * b - q * q));
            var (c, r, depth) = PoolChalkArt.Dish(wear);
            float dip = 0f;
            if (depth > 0f)
            {
                float t = (new Vector2(x, z) - c).magnitude / r;
                if (t < 1f) dip = depth * Mathf.Pow(1f - t * t, 1.3f);
            }
            return Size - drop - dip;
        }

        /// <summary>Grid stations across the top: dense near the edges (for the rounding), even in between.</summary>
        static float[] Stations()
        {
            const float half = Size * 0.5f;
            var edge = new List<float> { 0f, 0.00006f, 0.00016f, 0.0003f, 0.00045f, 0.0006f, 0.0008f };
            var list = new List<float>();
            foreach (float e in edge) list.Add(-half + e);
            int inner = Mathf.CeilToInt((Size - 2f * 0.0008f) / 0.0004f);
            for (int i = 1; i < inner; i++) list.Add(Mathf.Lerp(-half + 0.0008f, half - 0.0008f, i / (float)inner));
            for (int i = edge.Count - 1; i >= 0; i--) list.Add(half - edge[i]);
            return list.ToArray();
        }

        static Mesh Body(int step)
        {
            if (Bodies.TryGetValue(step, out var cached) && cached != null) return cached;
            float wear = step / 10f;
            var xs = Stations();
            int n = xs.Length;
            var v = new List<Vector3>();
            var nn = new List<Vector3>();
            var uv = new List<Vector2>();
            var tri = new List<int>();
            const float eps = 0.00003f, half = Size * 0.5f;
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    float x = xs[i], z = xs[j];
                    v.Add(new Vector3(x, TopHeight(x, z, wear), z));
                    // Normal from the height gradient (one-sided at the border, where the rounding turns vertical).
                    float x0 = Mathf.Max(x - eps, -half), x1 = Mathf.Min(x + eps, half), z0 = Mathf.Max(z - eps, -half), z1 = Mathf.Min(z + eps, half);
                    float gx = (TopHeight(x1, z, wear) - TopHeight(x0, z, wear)) / (x1 - x0);
                    float gz = (TopHeight(x, z1, wear) - TopHeight(x, z0, wear)) / (z1 - z0);
                    nn.Add(new Vector3(-gx, 1f, -gz).normalized);
                    uv.Add(new Vector2(x / Size + 0.5f, z / Size + 0.5f));
                }
            for (int j = 0; j < n - 1; j++)
                for (int i = 0; i < n - 1; i++)
                {
                    int a = j * n + i, b = a + 1, c = a + n, d = c + 1;
                    tri.Add(a); tri.Add(c); tri.Add(b);
                    tri.Add(b); tri.Add(c); tri.Add(d);
                }
            // Sides: from the bottom (on the wrapper's floor) up to the rounded top edge, following the grid stations.
            float top = Size - Bevel;
            Vector3[] dirs = { Vector3.back, Vector3.right, Vector3.forward, Vector3.left };
            for (int s = 0; s < 4; s++)
            {
                Vector3 nrm = dirs[s], along = Vector3.Cross(nrm, Vector3.up);
                int start = v.Count;
                for (int i = 0; i < n; i++)
                {
                    float a = xs[i];
                    Vector3 basePos = nrm * half + along * a;
                    v.Add(new Vector3(basePos.x, PaperT, basePos.z)); nn.Add(nrm); uv.Add(new Vector2(a / Size + 0.5f, PaperT / Size));
                    v.Add(new Vector3(basePos.x, top, basePos.z)); nn.Add(nrm); uv.Add(new Vector2(a / Size + 0.5f, top / Size));
                }
                for (int i = 0; i < n - 1; i++)
                {
                    int a = start + i * 2;
                    tri.Add(a); tri.Add(a + 1); tri.Add(a + 2);
                    tri.Add(a + 1); tri.Add(a + 3); tri.Add(a + 2);
                }
            }
            var m = new Mesh { name = "PoolChalk" + step, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m.SetVertices(v); m.SetNormals(nn); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
            BoardMeshes.FixWinding(m);
            m.RecalculateTangents();
            m.RecalculateBounds();
            return Bodies[step] = m;
        }

        // ================================================================ wrapper

        /// <summary>Height of the wrapper's top edge at perimeter position <paramref name="u"/> (0..1 round the cube).</summary>
        static float PaperTop(float u, float wear)
        {
            float a = u * Mathf.PI * 2f;
            // Periodic noise (whole frequencies) so the edge closes on itself; torn lower and more ragged with wear.
            float rag = 0.5f + 0.18f * Mathf.Sin(a * 3f + 1.3f) + 0.14f * Mathf.Sin(a * 7f + 0.4f) + 0.1f * Mathf.Sin(a * 17f + 2.1f)
                        + 0.06f * Mathf.Sin(a * 41f + 0.9f) + 0.04f * Mathf.Sin(a * 97f);
            return Size - 0.0005f - wear * (0.0012f + 0.0042f * rag);
        }

        static Mesh Wrapper(int step)
        {
            if (Wraps.TryGetValue(step, out var cached) && cached != null) return cached;
            float wear = step / 10f;
            const int cols = 40;
            const float half = Size * 0.5f, o = half + Gap;
            var v = new List<Vector3>();
            var nn = new List<Vector3>();
            var uv = new List<Vector2>();
            var tri = new List<int>();
            Vector3[] dirs = { Vector3.back, Vector3.right, Vector3.forward, Vector3.left };
            for (int s = 0; s < 4; s++)
            {
                Vector3 nrm = dirs[s], along = Vector3.Cross(nrm, Vector3.up);
                int start = v.Count;
                for (int i = 0; i <= cols; i++)
                {
                    float f = i / (float)cols, u = (s + f) / 4f;
                    float ht = PaperTop(u, wear);
                    Vector3 p = nrm * o + along * Mathf.Lerp(-o, o, f);
                    Vector3 pin = nrm * half + along * Mathf.Lerp(-o, o, f);
                    // Outer face (bottom, top), then the rim across the paper's thickness (outer, inner).
                    v.Add(new Vector3(p.x, 0f, p.z)); nn.Add(nrm); uv.Add(new Vector2(u, 0f));
                    v.Add(new Vector3(p.x, ht, p.z)); nn.Add(nrm); uv.Add(new Vector2(u, ht / Size));
                    v.Add(new Vector3(p.x, ht, p.z)); nn.Add(Vector3.up); uv.Add(new Vector2(u, ht / Size));
                    v.Add(new Vector3(pin.x, ht, pin.z)); nn.Add(Vector3.up); uv.Add(new Vector2(u, ht / Size));
                }
                for (int i = 0; i < cols; i++)
                {
                    int a = start + i * 4, b = a + 4;
                    tri.Add(a); tri.Add(a + 1); tri.Add(b);
                    tri.Add(a + 1); tri.Add(b + 1); tri.Add(b);
                    tri.Add(a + 2); tri.Add(a + 3); tri.Add(b + 2);
                    tri.Add(a + 3); tri.Add(b + 3); tri.Add(b + 2);
                }
            }
            // The fold under the bottom.
            int q = v.Count;
            foreach (var (x, z) in new[] { (-o, -o), (o, -o), (o, o), (-o, o) })
            {
                v.Add(new Vector3(x, 0f, z)); nn.Add(Vector3.down); uv.Add(new Vector2(0.02f, 0.02f));
            }
            tri.Add(q); tri.Add(q + 1); tri.Add(q + 2);
            tri.Add(q); tri.Add(q + 2); tri.Add(q + 3);
            var m = new Mesh { name = "PoolChalkWrapper" + step };
            m.SetVertices(v); m.SetNormals(nn); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
            BoardMeshes.FixWinding(m);
            m.RecalculateTangents();
            m.RecalculateBounds();
            return Wraps[step] = m;
        }

        // ================================================================ helpers

        static void Part(string name, Transform parent, Mesh mesh, Material mat)
        {
            var g = new GameObject(name) { layer = parent.gameObject.layer };
            g.transform.SetParent(parent, false);
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            g.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }

        static Material Mat(Material template, Texture tex, float gloss)
        {
            var m = template != null ? new Material(template) : new Material(Shader.Find("Standard"));
            m.color = Color.white;
            m.mainTexture = tex;
            m.mainTextureScale = Vector2.one;
            m.DisableKeyword("_EMISSION");
            m.SetTexture("_BumpMap", null);
            m.DisableKeyword("_NORMALMAP");
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Glossiness", gloss);
            return m;
        }
    }
}
