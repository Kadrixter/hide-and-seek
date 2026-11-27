using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HideAndSeek.Game.Editor
{
    /// <summary>
    /// 2026-11-27: worn upholstery on the lobby sofas and poufs - torn holes with the yellow foam showing (bulging a
    /// little out of the tear, a dark shadow line round it and the frayed fabric rim). The holes are put on the real
    /// meshes: rays from above / the front / the sides find the fabric (temporary mesh colliders on a spare layer), and
    /// every vertex of a hole is projected onto the surface again, so it hugs cushions and rolls. Seeded by the
    /// furniture's position: the same sofa gets the same holes on every build.
    /// </summary>
    public static partial class MultiplayerBuilder
    {
        private const int WearLayer = 30;
        private static readonly string[] FabricPieces = { "Frame", "Rolls", "Cushions", "Body", "Cushion" };

        private static Material _foamMat, _tearShadowMat;

        private static Material FoamMat() => _foamMat ??= SeatMat("SeatFoamYellow", new Color(1f, 0.9f, 0.45f), FoamTex(), 0.05f, new Vector2(6f, 6f));
        private static Material TearShadowMat() => _tearShadowMat ??= SeatMat("SeatTearShadow", new Color(0.09f, 0.06f, 0.04f), null, 0.05f, Vector2.one);

        /// <summary>Open-cell foam: bright yellow with small dark pores.</summary>
        private static Texture2D FoamTex() => SeatTex("seat_foam", 128, 128, (x, y) =>
        {
            float u = x / 128f, v = y / 128f;
            float cells = Mathf.PerlinNoise(u * 26f, v * 26f);
            float pore = Hash01(x * 613 + y * 1129) < 0.07f ? 0.55f : 1f;
            float n = (0.82f + cells * 0.2f) * pore * (0.95f + Mathf.PerlinNoise(u * 5f + 7f, v * 5f) * 0.08f);
            return new Color(n, n * 0.97f, n * 0.9f);
        });

        /// <summary>Adds the holes to the sofa / pouf <paramref name="t"/> (after its model was built).</summary>
        private static void AddWear(Transform t, bool sofa)
        {
            var pieces = new List<(GameObject go, int layer, MeshCollider col)>();
            foreach (var mf in t.GetComponentsInChildren<MeshFilter>())
            {
                if (System.Array.IndexOf(FabricPieces, mf.gameObject.name) < 0 || mf.sharedMesh == null) continue;
                var col = mf.gameObject.AddComponent<MeshCollider>();
                col.sharedMesh = mf.sharedMesh;
                pieces.Add((mf.gameObject, mf.gameObject.layer, col));
                mf.gameObject.layer = WearLayer;
            }
            if (pieces.Count == 0) return;
            Physics.SyncTransforms();
            int mask = 1 << WearLayer;
            var rnd = new System.Random(Mathf.RoundToInt(t.position.x * 97f + t.position.z * 131f) ^ (sofa ? 0x50FA : 0x90F));
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);

            var b = new Bounds(t.position, Vector3.zero);
            foreach (var r in t.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
            int want = sofa ? 6 : 3, made = 0;
            var spots = new List<Vector3>();
            var foam = new List<(Vector3 p, Vector3 n, Vector2 uv)>();
            var foamTri = new List<int>();
            var shadowV = new List<Vector3>();
            var shadowN = new List<Vector3>();
            var shadowTri = new List<int>();
            var rimV = new List<Vector3>();
            var rimN = new List<Vector3>();
            var rimUv = new List<Vector2>();
            var rimTri = new List<int>();
            Material rimMat = null;
            for (int attempt = 0; attempt < 80 && made < want; attempt++)
            {
                // direction: mostly from above (seats, tops, arm rolls), some from the front / sides
                float pick = R(0f, 1f);
                Vector3 dirLocal = pick < 0.6f ? Vector3.down : pick < 0.85f ? Vector3.forward * (sofa ? 1f : (R(0f, 1f) < 0.5f ? 1f : -1f)) : (R(0f, 1f) < 0.5f ? Vector3.right : Vector3.left);
                Vector3 dir = t.TransformDirection(dirLocal);
                Vector3 target = new Vector3(R(b.min.x, b.max.x), R(b.min.y + b.size.y * 0.3f, b.max.y), R(b.min.z, b.max.z));
                Vector3 from = target - dir * 3f;
                if (!Physics.Raycast(from, dir, out var hit, 6f, mask, QueryTriggerInteraction.Collide)) continue;
                if (Vector3.Dot(hit.normal, -dir) < 0.35f) continue; // grazing
                if (hit.point.y < t.position.y + 0.12f) continue;
                bool near = false;
                foreach (var sp in spots) if ((sp - hit.point).sqrMagnitude < 0.2f * 0.2f) near = true;
                if (near) continue;
                spots.Add(hit.point);
                rimMat ??= hit.collider.GetComponent<MeshRenderer>().sharedMaterial;
                Hole(t, hit.point, hit.normal, R(sofa ? 0.04f : 0.032f, sofa ? 0.078f : 0.052f), rnd, mask, foam, foamTri, shadowV, shadowN, shadowTri, rimV, rimN, rimUv, rimTri);
                made++;
            }
            foreach (var (go, layer, col) in pieces)
            {
                go.layer = layer;
                Object.DestroyImmediate(col);
            }
            if (made == 0) return;
            string key = (sofa ? "Sofa" : "Pouf") + "_Wear_" + Mathf.Abs(Mathf.RoundToInt(t.position.x * 10f)) + "_" + Mathf.Abs(Mathf.RoundToInt(t.position.z * 10f));
            var fm = new Mesh { name = key + "_Foam" };
            var fv = new List<Vector3>();
            var fn = new List<Vector3>();
            var fu = new List<Vector2>();
            foreach (var f in foam) { fv.Add(f.p); fn.Add(f.n); fu.Add(f.uv); }
            FixWinding(fv, fn, foamTri);
            FixWinding(shadowV, shadowN, shadowTri);
            FixWinding(rimV, rimN, rimTri);
            fm.SetVertices(fv); fm.SetNormals(fn); fm.SetUVs(0, fu); fm.SetTriangles(foamTri, 0); fm.RecalculateBounds();
            Piece(t, "WearFoam", fm, FoamMat(), key + "_Foam");
            var sm = new Mesh { name = key + "_Shadow" };
            sm.SetVertices(shadowV); sm.SetNormals(shadowN); sm.SetTriangles(shadowTri, 0); sm.RecalculateBounds();
            Piece(t, "WearShadow", sm, TearShadowMat(), key + "_Shadow");
            var rm = new Mesh { name = key + "_Rim" };
            rm.SetVertices(rimV); rm.SetNormals(rimN); rm.SetUVs(0, rimUv); rm.SetTriangles(rimTri, 0); rm.RecalculateBounds();
            Piece(t, "WearRim", rm, rimMat, key + "_Rim");
            Debug.Log($"[LobbySeats] {t.name} at {t.position}: {made} torn holes with foam");
        }

        /// <summary>Every triangle faces the way its vertex normals point (the rings are built in either order).</summary>
        private static void FixWinding(List<Vector3> v, List<Vector3> n, List<int> tri)
        {
            for (int i = 0; i + 2 < tri.Count; i += 3)
            {
                Vector3 a = v[tri[i]], b = v[tri[i + 1]], c = v[tri[i + 2]];
                Vector3 face = Vector3.Cross(b - a, c - a);
                if (Vector3.Dot(face, n[tri[i]] + n[tri[i + 1]] + n[tri[i + 2]]) < 0f) (tri[i + 1], tri[i + 2]) = (tri[i + 2], tri[i + 1]);
            }
        }

        /// <summary>One tear (local space of <paramref name="t"/>): foam bulging out of a ragged hole, its dark edge, the frayed fabric rim.</summary>
        private static void Hole(Transform t, Vector3 pWorld, Vector3 nWorld, float radius, System.Random rnd, int mask,
            List<(Vector3, Vector3, Vector2)> foam, List<int> foamTri, List<Vector3> shV, List<Vector3> shN, List<int> shTri,
            List<Vector3> rimV, List<Vector3> rimN, List<Vector2> rimUv, List<int> rimTri)
        {
            const int N = 18;
            Vector3 tx = Vector3.Cross(nWorld, Mathf.Abs(nWorld.y) < 0.9f ? Vector3.up : Vector3.forward).normalized;
            Vector3 ty = Vector3.Cross(nWorld, tx);
            float stretch = 0.7f + (float)rnd.NextDouble() * 0.6f, rot = (float)rnd.NextDouble() * Mathf.PI;
            var rr = new float[N];
            for (int i = 0; i < N; i++) rr[i] = radius * (0.72f + (float)rnd.NextDouble() * 0.42f);
            // surface point at a planar offset (projected back onto the cushion / roll)
            Vector3 Surf(Vector2 o, float lift, out Vector3 n)
            {
                Vector3 guess = pWorld + tx * o.x + ty * o.y;
                if (Physics.Raycast(guess + nWorld * 0.06f, -nWorld, out var h, 0.14f, mask, QueryTriggerInteraction.Collide))
                {
                    n = h.normal;
                    return h.point + h.normal * lift;
                }
                n = nWorld;
                return guess + nWorld * lift;
            }
            Vector2 Ring(int i, float k)
            {
                float a = i / (float)N * Mathf.PI * 2f + rot;
                return new Vector2(Mathf.Cos(a) * stretch, Mathf.Sin(a) / stretch) * rr[i] * k;
            }
            Vector3 L(Vector3 w) => t.InverseTransformPoint(w);
            Vector3 Ln(Vector3 w) => t.InverseTransformDirection(w).normalized;

            // foam: a domed fan (centre bulges ~7 mm out of the tear, little lumps)
            int f0 = foam.Count;
            var c = Surf(Vector2.zero, 0.013f, out var cn);
            foam.Add((L(c), Ln(cn), new Vector2(0.5f, 0.5f)));
            for (int i = 0; i < N; i++)
            {
                var o = Ring(i, 0.55f);
                var p = Surf(o, 0.008f + (float)rnd.NextDouble() * 0.005f, out var n1);
                foam.Add((L(p), Ln(n1), new Vector2(0.5f + o.x * 6f, 0.5f + o.y * 6f)));
            }
            for (int i = 0; i < N; i++)
            {
                var o = Ring(i, 0.93f);
                var p = Surf(o, 0.0032f, out var n2);
                foam.Add((L(p), Ln(n2), new Vector2(0.5f + o.x * 6f, 0.5f + o.y * 6f)));
            }
            for (int i = 0; i < N; i++)
            {
                int j = (i + 1) % N;
                foamTri.Add(f0); foamTri.Add(f0 + 1 + j); foamTri.Add(f0 + 1 + i);
                int a = f0 + 1 + i, bb = f0 + 1 + j, a2 = f0 + 1 + N + i, b2 = f0 + 1 + N + j;
                foamTri.Add(a); foamTri.Add(bb); foamTri.Add(b2);
                foamTri.Add(a); foamTri.Add(b2); foamTri.Add(a2);
            }
            // dark shadow line between the foam and the fabric (the cut edge, the hollow under it)
            int s0 = shV.Count;
            for (int i = 0; i < N; i++)
            {
                var pi = Surf(Ring(i, 0.86f), 0.003f, out var ni);
                var po = Surf(Ring(i, 1.06f), 0.0028f, out var no);
                shV.Add(L(pi)); shN.Add(Ln(ni));
                shV.Add(L(po)); shN.Add(Ln(no));
            }
            for (int i = 0; i < N; i++)
            {
                int j = (i + 1) % N;
                int a = s0 + i * 2, bb = s0 + j * 2;
                shTri.Add(a); shTri.Add(bb + 1); shTri.Add(bb);
                shTri.Add(a); shTri.Add(a + 1); shTri.Add(bb + 1);
            }
            // frayed fabric rim: a raised lip curling over the edge, ragged outside
            int r0 = rimV.Count;
            for (int i = 0; i < N; i++)
            {
                float fray = 1.16f + (float)rnd.NextDouble() * 0.32f;
                var pi = Surf(Ring(i, 1.0f), 0.0055f, out var ni);
                var po = Surf(Ring(i, fray), 0.0018f, out var no);
                rimV.Add(L(pi)); rimN.Add(Ln(ni)); rimUv.Add(new Vector2(i / (float)N, 0f));
                rimV.Add(L(po)); rimN.Add(Ln(no)); rimUv.Add(new Vector2(i / (float)N, 0.05f));
            }
            for (int i = 0; i < N; i++)
            {
                int j = (i + 1) % N;
                int a = r0 + i * 2, bb = r0 + j * 2;
                rimTri.Add(a); rimTri.Add(bb + 1); rimTri.Add(bb);
                rimTri.Add(a); rimTri.Add(a + 1); rimTri.Add(bb + 1);
            }
        }
    }
}
