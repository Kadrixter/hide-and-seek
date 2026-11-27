using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames.Pool
{
    /// <summary>
    /// The sixteen pool balls (0 = cue ball, 1-7 solids, 8 black, 9-15 stripes): one shared smooth UV sphere (radius
    /// <see cref="PoolDims.BallR"/>, centre at the origin, poles on local ±Z) and a high-gloss phenolic material per
    /// ball with the texture from <see cref="PoolBallArt"/>. With no rotation a number faces up (+Y) and reads upright
    /// towards +Z. Also the 8-ball rack layout on the foot spot. Textures / materials are made once and kept.
    /// </summary>
    public static class PoolBallModels
    {
        const int Segments = 144, Rings = 72;
        static Mesh _mesh;
        static readonly Texture2D[] Textures = new Texture2D[16];
        static readonly Dictionary<Material, Material[]> Materials = new();
        static readonly Material[] NoTemplate = new Material[16];

        /// <summary>Ball <paramref name="number"/> (0-15) under <paramref name="parent"/>; its centre is the local origin. No collider.</summary>
        public static GameObject Build(Transform parent, Material template, int number) => Build(parent, template, number, 0);

        /// <summary>Like <see cref="Build(Transform, Material, int)"/> with the <paramref name="texW"/> x texW/2 texture (0 = full size).</summary>
        public static GameObject Build(Transform parent, Material template, int number, int texW)
        {
            number = Mathf.Clamp(number, 0, 15);
            var g = new GameObject("PoolBall_" + number);
            if (parent != null)
            {
                g.layer = parent.gameObject.layer;
                g.transform.SetParent(parent, false);
            }
            g.AddComponent<MeshFilter>().sharedMesh = SphereMesh;
            g.AddComponent<MeshRenderer>().sharedMaterial = BallMaterial(template, number, texW);
            return g;
        }

        public static Texture2D Texture(int number)
        {
            number = Mathf.Clamp(number, 0, 15);
            return Textures[number] != null ? Textures[number] : (Textures[number] = PoolBallArt.Make(number));
        }

        // ---- smaller textures (lobby: 1024 x 512), cached per width; pixels can be computed on worker threads first

        static readonly Dictionary<int, Texture2D[]> SizedTextures = new();
        static readonly Dictionary<int, Color32[][]> SizedPixels = new();
        static readonly Dictionary<int, int[]> SizedLeft = new();

        /// <summary>Ball texture <paramref name="texW"/> x texW/2 (the full size when texW is 0 or <see cref="PoolBallArt.W"/>).</summary>
        public static Texture2D Texture(int number, int texW)
        {
            if (texW <= 0 || texW >= PoolBallArt.W) return Texture(number);
            number = Mathf.Clamp(number, 0, 15);
            if (!SizedTextures.TryGetValue(texW, out var set)) SizedTextures[texW] = set = new Texture2D[16];
            if (set[number] != null) return set[number];
            Color32[] px = null;
            if (SizedPixels.TryGetValue(texW, out var pending))
                lock (pending) { px = pending[number]; pending[number] = null; }
            return set[number] = px != null ? PoolBallArt.ToTexture(number, px, texW, texW / 2) : PoolBallArt.Make(number, texW, texW / 2);
        }

        /// <summary>
        /// Starts computing the sixteen textures of width <paramref name="texW"/> on worker threads (no hitch on the main
        /// thread); <see cref="PixelsReady"/> tells when <see cref="Build(Transform, Material, int, int)"/> only has to upload them.
        /// </summary>
        public static void Prewarm(int texW)
        {
            if (texW <= 0 || texW >= PoolBallArt.W || SizedPixels.ContainsKey(texW)) return;
            var pending = new Color32[16][];
            SizedPixels[texW] = pending;
            var left = new[] { 16 };
            SizedLeft[texW] = left;
            for (int i = 0; i < 16; i++)
            {
                int n = i;
                System.Threading.Tasks.Task.Run(() =>
                {
                    Color32[] px = null;
                    try { px = PoolBallArt.Pixels(n, texW, texW / 2); }
                    catch (System.Exception e) { Debug.LogWarning($"[PoolBalls] texture {n} failed on a worker: {e.Message}"); }
                    lock (pending) pending[n] = px;
                    System.Threading.Interlocked.Decrement(ref left[0]);
                });
            }
        }

        /// <summary>All worker jobs of <see cref="Prewarm"/> are done (true as well when it was never started).</summary>
        public static bool PixelsReady(int texW)
        {
            return !SizedLeft.TryGetValue(texW, out var left) || System.Threading.Volatile.Read(ref left[0]) <= 0;
        }

        static readonly Dictionary<(Material, int), Material[]> SizedMaterials = new();

        /// <summary><see cref="BallMaterial(Material, int)"/> with the smaller texture (cached per template and size).</summary>
        public static Material BallMaterial(Material template, int number, int texW)
        {
            if (texW <= 0 || texW >= PoolBallArt.W) return BallMaterial(template, number);
            number = Mathf.Clamp(number, 0, 15);
            if (!SizedMaterials.TryGetValue((template, texW), out var set)) SizedMaterials[(template, texW)] = set = new Material[16];
            if (set[number] != null) return set[number];
            return set[number] = NewMaterial(template, number, Texture(number, texW), $"PoolBall{number}_{texW}");
        }

        public static Color BallColor(int number) => PoolBallArt.BallColor(number);

        /// <summary>Glossy phenolic-resin material of ball <paramref name="number"/> made from <paramref name="template"/> (Standard).</summary>
        public static Material BallMaterial(Material template, int number)
        {
            number = Mathf.Clamp(number, 0, 15);
            Material[] set = NoTemplate;
            if (template != null && !Materials.TryGetValue(template, out set)) Materials[template] = set = new Material[16];
            if (set[number] != null) return set[number];
            return set[number] = NewMaterial(template, number, Texture(number), "PoolBall" + number);
        }

        static Material NewMaterial(Material template, int number, Texture2D tex, string name)
        {
            var m = template != null ? new Material(template) : new Material(Shader.Find("Standard"));
            m.name = name;
            // 2026-11-27: the albedo is dimmed a bit - under the 3 lamp spots the cream caps of the stripes clipped to pure
            // white (a stripe looked like the cue ball from above, its colour band washed out).
            m.color = new Color(0.82f, 0.82f, 0.82f, 1f);
            m.mainTexture = tex;
            m.mainTextureScale = Vector2.one;
            m.mainTextureOffset = Vector2.zero;
            m.DisableKeyword("_EMISSION");
            m.SetTexture("_BumpMap", null);
            m.DisableKeyword("_NORMALMAP");
            m.SetFloat("_Metallic", 0f);
            // Satin rather than mirror: at 0.93 the lamp spots and the room showed up as big white blots on every ball
            // (a solid then looked like a stripe and the colours washed out). Softer, dimmer highlight, blurred reflection.
            // (No _GLOSSYREFLECTIONS_OFF keyword: runtime-only Standard variants may be stripped from the build.)
            // 2026-11-27: 0.62 still threw the lamp spots back as bright blots (hard to tell the balls apart) -> 0.40.
            m.SetFloat("_Glossiness", number == 8 ? 0.45f : 0.40f);
            m.SetFloat("_SpecularHighlights", 1f);
            m.SetFloat("_GlossyReflections", 1f);
            return m;
        }

        /// <summary>The shared ball sphere: latitude rings round local Z, u = longitude, v = latitude (matches <see cref="PoolBallArt"/>).</summary>
        public static Mesh SphereMesh
        {
            get
            {
                if (_mesh != null) return _mesh;
                int cols = Segments + 1, rows = Rings + 1;
                var v = new Vector3[cols * rows];
                var n = new Vector3[v.Length];
                var uv = new Vector2[v.Length];
                for (int r = 0; r < rows; r++)
                {
                    float fv = r / (float)Rings;
                    float lat = (fv - 0.5f) * Mathf.PI, cl = Mathf.Cos(lat), sl = Mathf.Sin(lat);
                    for (int s = 0; s < cols; s++)
                    {
                        float fu = s / (float)Segments;
                        // At the poles the texel column is taken half a segment over, so each pole triangle samples its own middle.
                        if (r == 0) fu -= 0.5f / Segments;
                        if (r == Rings) fu += 0.5f / Segments;
                        float lon = fu * Mathf.PI * 2f;
                        var d = new Vector3(cl * Mathf.Cos(lon), cl * Mathf.Sin(lon), sl);
                        if (r == 0) d = Vector3.back;
                        if (r == Rings) d = Vector3.forward;
                        int i = r * cols + s;
                        v[i] = d * PoolDims.BallR;
                        n[i] = d;
                        uv[i] = new Vector2(fu, fv);
                    }
                }
                var tri = new List<int>(Segments * Rings * 6);
                for (int r = 0; r < Rings; r++)
                    for (int s = 0; s < Segments; s++)
                    {
                        int a = r * cols + s, b = a + 1, c = a + cols, d = c + 1;
                        if (r > 0) { tri.Add(a); tri.Add(c); tri.Add(b); }
                        if (r < Rings - 1) { tri.Add(b); tri.Add(c); tri.Add(d); }
                    }
                var m = new Mesh { name = "PoolBall" };
                m.SetVertices(v);
                m.SetNormals(n);
                m.SetUVs(0, uv);
                m.SetTriangles(tri, 0);
                BoardMeshes.FixWinding(m);
                m.RecalculateTangents();
                m.RecalculateBounds();
                return _mesh = m;
            }
        }

        // ================================================================ rack

        static readonly int[] Order = { 1, 9, 2, 10, 8, 3, 11, 7, 14, 4, 5, 13, 15, 6, 12 };

        /// <summary>Distance between two rows of a tight rack (along X).</summary>
        public const float RowStep = PoolDims.BallD * 0.8660254f;

        /// <summary>
        /// Centres (table space) of the 15 racked balls: the apex ball on the foot spot, the triangle widening towards
        /// +X, rows listed from -Z to +Z, balls touching. Matches <see cref="RackOrder"/>.
        /// </summary>
        public static Vector3[] RackPositions()
        {
            var p = new Vector3[15];
            int i = 0;
            for (int row = 0; row < 5; row++)
                for (int k = 0; k <= row; k++)
                    p[i++] = PoolDims.BallOnCloth(PoolDims.FootSpot.x + row * RowStep, (k - row * 0.5f) * PoolDims.BallD);
            return p;
        }

        /// <summary>
        /// Ball numbers for <see cref="RackPositions"/>: 1 on the apex, 8 in the middle of the third row, the back corners
        /// one solid (5) and one stripe (12), solids and stripes alternating round the edge.
        /// </summary>
        public static int[] RackOrder() => (int[])Order.Clone();
    }
}
