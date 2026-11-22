using HideAndSeek.Characters;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Office bin with crumpled papers, built at runtime (lobby: placed by the lobby builder; security rooms: created by
    /// SecurityRooms). Perforated anthracite bin with a liner bag (<see cref="TrashBinMesh"/>), a pile of crumpled balls
    /// inside with a few sticking out over the rim, missed ones on the floor in front (+Z), optionally a crushed soda can.
    /// All papers are one combined mesh; three shared materials (<c>HideAndSeek/TrashProp</c>); no lights.
    /// </summary>
    public sealed class TrashBinProp : MonoBehaviour
    {
        [Tooltip("Varies the folds, papers and their placement.")] public int seed = 11;
        [Tooltip("Missed papers on the floor in front of the bin (+Z).")] public int floorPapers = 4;
        [Tooltip("Crushed soda can lying next to the bin.")] public bool can = true;
        [Tooltip("Off in the garage: no light / reflection probes, like the rest of the level.")] public bool probes = true;

        static Material _body, _bag, _paper, _shadow;
        bool _built;
        readonly System.Collections.Generic.List<(Vector3 at, float r, float k)> _blobs = new();

        /// <summary>Places a bin at <paramref name="floorPos"/> (built in its Start); papers spill towards local +Z (rotated by <paramref name="yaw"/>).</summary>
        public static TrashBinProp Create(Transform parent, Vector3 floorPos, float yaw, int seed, int floorPapers, bool can, bool probes)
        {
            var g = new GameObject("TrashBin");
            g.transform.SetParent(parent, false);
            g.transform.SetPositionAndRotation(floorPos, Quaternion.Euler(0f, yaw, 0f));
            var b = g.AddComponent<TrashBinProp>();
            b.seed = seed; b.floorPapers = floorPapers; b.can = can; b.probes = probes;
            return b;
        }

        System.Collections.IEnumerator Start()
        {
            // Textures are computed on worker threads since the game started; normally ready long before this.
            while (!TrashArt.Ready) yield return null;
            if (!_built) Build();
        }

        void Build()
        {
            _built = true;
            EnsureMaterials();
            Part("BinBody", TrashBinMesh.Body(), _body, true);
            float slip = (40f + (seed * 37) % 50) * Mathf.Deg2Rad; // slipped inside on the front right
            Part("BinBag", TrashBinMesh.Bag(slip, seed), _bag, true);
            Part("BinPapers", Papers(), _paper, true);
            _blobs.Add((Vector3.zero, TrashBinMesh.Rb * 1.9f, 0.75f));
            if (can) Can();
            if (_shadow != null) Part("BinContactShadow", Shadows(), _shadow, false);
        }

        void Part(string n, Mesh mesh, Material m, bool shadows)
        {
            var g = new GameObject(n);
            g.transform.SetParent(transform, false);
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            if (!probes)
            {
                r.lightProbeUsage = LightProbeUsage.Off;
                r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
        }

        static void EnsureMaterials()
        {
            if (_body != null) return;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var sh = Resources.Load<Shader>("Shaders/HnsTrash");
            if (sh == null || !sh.isSupported) sh = Shader.Find("Standard");
            Material M(string n, Texture2D a, Texture2D nrm, float bump, float smooth, float metal)
            {
                var m = new Material(sh) { name = n, mainTexture = a };
                m.SetTexture("_BumpMap", nrm);
                m.SetFloat("_BumpScale", bump);
                m.SetFloat("_Glossiness", smooth);
                m.SetFloat("_Metallic", metal);
                return m;
            }
            _body = M("TrashBinBody", TrashArt.Metal, TrashArt.MetalNormal, 1f, 0.68f, 0.45f);
            _bag = M("TrashBinBag", TrashArt.Bag, TrashArt.BagNormal, 0.8f, 0.62f, 0f);
            _paper = M("TrashPaper", TrashArt.Paper, TrashArt.PaperNormal, 0.6f, 0.12f, 0f);
            var cs = Resources.Load<Shader>("Shaders/HnsContactShadow");
            if (cs != null && cs.isSupported) _shadow = new Material(cs) { name = "TrashContactShadow", mainTexture = TrashArt.Blob };
            Debug.Log($"[TrashBin] textures uploaded, materials made in {sw.ElapsedMilliseconds} ms");
        }

        // ------------------------------------------------------------------ papers

        Mesh Papers()
        {
            var m = new TrashMesh();
            var rnd = new System.Random(seed * 31 + 5);
            float Rf() => (float)rnd.NextDouble();
            int ball = 0;
            TrashArt.PaperKind Kind(int i) => (TrashArt.PaperKind)(new[] { 0, 1, 3, 0, 2, 1, 0, 3, 2, 0, 1, 3, 2, 0, 3, 1, 0, 1 }[(i + seed) % 18]);

            void Ball(Vector3 at, float r, Vector3 squash, Quaternion rot, bool onFloor)
            {
                int s = seed * 100 + ball;
                var kind = Kind(ball++);
                if (kind == TrashArt.PaperKind.Note) r = Mathf.Min(r, 0.03f); // a sticky note makes a small ball
                int first = m.V.Count;
                if (!onFloor) CrumpledPaper.Add(m, s, r, squash, kind, p => at + rot * p);
                else
                {
                    // Floor: build once to find the lowest point, then rebuild resting on the floor (2 mm sunk in).
                    var probe = new TrashMesh();
                    float low = CrumpledPaper.Add(probe, s, r, squash, kind, p => rot * p);
                    CrumpledPaper.Add(m, s, r, squash, kind, p => at + rot * p + Vector3.up * (-low - 0.002f));
                    _blobs.Add((at, r * 1.5f * Mathf.Max(squash.x, squash.z), 0.7f));
                }
                // Occlusion by height: the underside on the floor, deeper inside the bin.
                for (int i = first; i < m.V.Count; i++)
                {
                    float y = m.V[i].y;
                    float k = onFloor ? Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(y / (r * 1.1f)))
                                      : Mathf.Lerp(0.4f, 1f, Mathf.Clamp01((y - TrashBinMesh.FillY + 0.01f) / (TrashBinMesh.H + 0.03f - TrashBinMesh.FillY)));
                    var c = m.C[i];
                    m.C[i] = new Color(c.r * k, c.g * k, c.b * k, c.a * k);
                }
            }
            Quaternion RandRot() => Quaternion.Euler(Rf() * 360f, Rf() * 360f, Rf() * 360f);
            Vector3 Squash() => new Vector3(0.85f + Rf() * 0.3f, 0.75f + Rf() * 0.3f, 0.85f + Rf() * 0.3f);

            // Inside: a ring of balls of mixed sizes on the bag bottom, a second layer, three on top above the rim.
            float y0 = TrashBinMesh.FillY;
            for (int i = 0; i < 5; i++)
            {
                float a = i * 72f * Mathf.Deg2Rad + Rf() * 0.4f;
                float r = 0.034f + Rf() * 0.02f;
                Ball(new Vector3(Mathf.Cos(a) * 0.08f, y0 + 0.015f, Mathf.Sin(a) * 0.08f), r, Squash(), RandRot(), false);
            }
            Ball(new Vector3(0f, y0 + 0.01f, 0f), 0.04f, Squash(), RandRot(), false);
            for (int i = 0; i < 3; i++)
            {
                float a = (i * 120f + 30f) * Mathf.Deg2Rad + Rf() * 0.5f;
                Ball(new Vector3(Mathf.Cos(a) * 0.06f, y0 + 0.06f, Mathf.Sin(a) * 0.06f), 0.03f + Rf() * 0.018f, Squash(), RandRot(), false);
            }
            Ball(new Vector3(-0.03f, TrashBinMesh.H + 0.01f, 0.035f), 0.05f, Squash(), RandRot(), false);
            Ball(new Vector3(0.06f, TrashBinMesh.H - 0.008f, -0.03f), 0.034f, Squash(), RandRot(), false);
            Ball(new Vector3(-0.05f, TrashBinMesh.H - 0.016f, -0.065f), 0.04f, new Vector3(1.3f, 0.5f, 1.1f), RandRot(), false);

            // Missed throws on the floor in front: mostly balls, one loosely crumpled flat sheet.
            for (int i = 0; i < floorPapers; i++)
            {
                float a = Mathf.Lerp(-75f, 75f, floorPapers == 1 ? 0.5f : i / (float)(floorPapers - 1)) + (Rf() - 0.5f) * 25f;
                float d = 0.26f + Rf() * 0.34f;
                var at = Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0f, d);
                bool flat = i == 1;
                Ball(at, flat ? 0.055f : 0.032f + Rf() * 0.02f, flat ? new Vector3(1.5f, 0.42f, 1.2f) : Squash(),
                    flat ? Quaternion.Euler(Rf() * 10f, Rf() * 360f, Rf() * 10f) : RandRot(), true);
            }
            return m.ToMesh("TrashPapers");
        }

        /// <summary>Soft blobs on the floor under the bin, the papers on the floor and the can (one mesh).</summary>
        Mesh Shadows()
        {
            var m = new TrashMesh();
            foreach (var (at, r, k) in _blobs)
            {
                Vector3 a = at + new Vector3(-r, 0.0015f, -r), b = at + new Vector3(r, 0.0015f, -r), c = at + new Vector3(-r, 0.0015f, r), d = at + new Vector3(r, 0.0015f, r);
                var col = new Color(1f, 1f, 1f, k);
                m.Tri(a, c, b, new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 0), col, col, col);
                m.Tri(b, c, d, new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1), col, col, col);
            }
            return m.ToMesh("TrashContactShadow");
        }

        void Can()
        {
            var rnd = new System.Random(seed * 17 + 3);
            var c = SodaCan.Create(transform, rnd.Next(SodaCanArt.BrandCount), gameObject.layer);
            c.name = "CrushedCan";
            // Lying on its side next to the bin (right side), flattened like someone stepped on it.
            float a = (105f + (float)rnd.NextDouble() * 20f) * Mathf.Deg2Rad;
            c.transform.localPosition = new Vector3(Mathf.Sin(a) * 0.3f, SodaCan.Radius * 0.98f, Mathf.Cos(a) * 0.3f);
            Vector3 along = new Vector3(Mathf.Cos(a + 0.6f), 0f, -Mathf.Sin(a + 0.6f));
            c.transform.localRotation = Quaternion.LookRotation(Vector3.up, along);
            c.SetCrush(0.9f);
            _blobs.Add((c.transform.localPosition - Vector3.up * c.transform.localPosition.y, 0.085f, 0.5f));
            foreach (var r in c.GetComponentsInChildren<Renderer>())
            {
                if (probes) continue;
                r.lightProbeUsage = LightProbeUsage.Off;
                r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
        }
    }
}
