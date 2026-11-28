using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HideAndSeek.Game.Editor
{
    /// <summary>
    /// Finds z-fighting in a scene: triangles of different renderers lying in the same plane (within 1.5 mm),
    /// facing the same way and overlapping. Batch: -executeMethod HideAndSeek.Game.Editor.ZFightCheck.RunLobby
    /// </summary>
    public static class ZFightCheck
    {
        struct Tri
        {
            public int R;
            public Vector3 A, B, C, N;
            public float D;
        }

        public static void RunLobby() => Run("Assets/_Project/Game/Scenes/Lobby.unity");

        public static void Run(string scenePath)
        {
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var rends = SceneRenderers();
            var pairs = Collect(rends);
            Debug.Log($"[ZFight] {rends.Count} renderers, {pairs.Count} overlapping pairs");
            foreach (var p in pairs.OrderByDescending(p => p.Value.area).Take(80))
                Debug.Log($"[ZFight] {Path(rends[p.Key.Item1].transform)}  <->  {Path(rends[p.Key.Item2].transform)}  area={p.Value.area:0.0000} at {p.Value.at} n={p.Value.n}");
        }

        static List<MeshRenderer> SceneRenderers() =>
            Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(r => r.enabled && r.GetComponent<MeshFilter>() != null && r.GetComponent<MeshFilter>().sharedMesh != null).ToList();

        /// <summary>
        /// Removes z-fighting in the open scene: of every coplanar overlapping pair, the smaller object is pushed
        /// 2 mm out along the shared face normal (faces looking down below 2.5 m are ignored: nobody sees them). Repeats until clean.
        /// </summary>
        public static int FixOpenScene()
        {
            int moved = 0;
            for (int pass = 0; pass < 4; pass++)
            {
                var rends = SceneRenderers();
                var pairs = Collect(rends);
                var done = new HashSet<MeshRenderer>();
                int movedNow = 0;
                foreach (var p in pairs.OrderByDescending(p => p.Value.area))
                {
                    if (p.Value.n.y < -0.9f && p.Value.at.y < 2.5f) continue; // undersides near the floor (unseen); ceilings stay in
                    var a = rends[p.Key.Item1];
                    var b = rends[p.Key.Item2];
                    float va = Volume(a.bounds), vb = Volume(b.bounds);
                    var small = va <= vb ? a : b;
                    if (!done.Add(small)) continue;
                    small.transform.position += p.Value.n * 0.002f;
                    movedNow++;
                }
                moved += movedNow;
                if (movedNow == 0) break;
            }
            Debug.Log($"[ZFight] fixed: {moved} nudges, remaining pairs: {Collect(SceneRenderers()).Count(p => p.Value.n.y >= -0.9f || p.Value.at.y >= 2.5f)}");
            return moved;
        }

        static float Volume(Bounds b) => Mathf.Max(b.size.x, 0.001f) * Mathf.Max(b.size.y, 0.001f) * Mathf.Max(b.size.z, 0.001f);

        static Dictionary<(int, int), (float area, Vector3 at, Vector3 n)> Collect(List<MeshRenderer> rends)
        {
            var tris = new List<Tri>();
            for (int ri = 0; ri < rends.Count; ri++)
            {
                var mf = rends[ri].GetComponent<MeshFilter>();
                var m = mf.sharedMesh;
                if (!m.isReadable) continue;
                var v = m.vertices;
                var idx = m.triangles;
                var tr = rends[ri].transform.localToWorldMatrix;
                for (int i = 0; i < idx.Length; i += 3)
                {
                    Vector3 a = tr.MultiplyPoint3x4(v[idx[i]]), b = tr.MultiplyPoint3x4(v[idx[i + 1]]), c = tr.MultiplyPoint3x4(v[idx[i + 2]]);
                    var n = Vector3.Cross(b - a, c - a);
                    if (n.sqrMagnitude < 1e-10f) continue;
                    n.Normalize();
                    tris.Add(new Tri { R = ri, A = a, B = b, C = c, N = n, D = Vector3.Dot(n, a) });
                }
            }

            // Bucket by quantized normal + plane distance (neighbour buckets checked too).
            var buckets = new Dictionary<(int, int, int, int), List<int>>();
            (int, int, int, int) Key(Tri t, int dd) => (Mathf.RoundToInt(t.N.x * 50), Mathf.RoundToInt(t.N.y * 50), Mathf.RoundToInt(t.N.z * 50), Mathf.FloorToInt(t.D / 0.003f) + dd);
            for (int i = 0; i < tris.Count; i++)
            {
                var k = Key(tris[i], 0);
                if (!buckets.TryGetValue(k, out var l)) buckets[k] = l = new List<int>();
                l.Add(i);
            }

            var pairs = new Dictionary<(int, int), (float area, Vector3 at, Vector3 n)>();
            foreach (var kv in buckets)
            {
                // each pair once: inside the bucket (b > a) and against the next plane bucket (no Contains: big lobby buckets)
                var list = kv.Value;
                buckets.TryGetValue((kv.Key.Item1, kv.Key.Item2, kv.Key.Item3, kv.Key.Item4 + 1), out var up);
                int n1 = list.Count, n2 = up?.Count ?? 0;
                for (int a = 0; a < n1; a++)
                for (int b = a + 1; b < n1 + n2; b++)
                {
                    var ti = tris[list[a]];
                    var tj = tris[b < n1 ? list[b] : up[b - n1]];
                    if (ti.R == tj.R) continue;
                    if (Vector3.Dot(ti.N, tj.N) < 0.995f || Mathf.Abs(ti.D - tj.D) > 0.0015f) continue;
                    float area = OverlapArea(ti, tj, out Vector3 at);
                    if (area < 2e-4f) continue;
                    var key = ti.R < tj.R ? (ti.R, tj.R) : (tj.R, ti.R);
                    pairs.TryGetValue(key, out var acc);
                    pairs[key] = (acc.area + area, acc.area > 0 ? acc.at : at, acc.area > 0 ? acc.n : ti.N);
                }
            }

            return pairs;
        }

        static string Path(Transform t)
        {
            string s = t.name;
            for (int i = 0; i < 2 && t.parent != null; i++) { t = t.parent; s = t.name + "/" + s; }
            return s;
        }

        /// <summary>Overlap of the two triangles' 2D bounding boxes in the plane (cheap approximation, clipped by the smaller triangle area).</summary>
        static float OverlapArea(Tri a, Tri b, out Vector3 at)
        {
            Vector3 n = a.N;
            Vector3 u = Vector3.Cross(n, Mathf.Abs(n.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 w = Vector3.Cross(n, u);
            Vector2 P(Vector3 p) => new Vector2(Vector3.Dot(p, u), Vector3.Dot(p, w));
            var a0 = P(a.A); var a1 = P(a.B); var a2 = P(a.C);
            var b0 = P(b.A); var b1 = P(b.B); var b2 = P(b.C);
            var amin = Vector2.Min(a0, Vector2.Min(a1, a2)); var amax = Vector2.Max(a0, Vector2.Max(a1, a2));
            var bmin = Vector2.Min(b0, Vector2.Min(b1, b2)); var bmax = Vector2.Max(b0, Vector2.Max(b1, b2));
            var lo = Vector2.Max(amin, bmin); var hi = Vector2.Min(amax, bmax);
            at = (a.A + a.B + a.C) / 3f;
            if (hi.x <= lo.x || hi.y <= lo.y) return 0f;
            // Sample a 4x4 grid inside the box overlap: fraction inside both triangles.
            int inside = 0;
            for (int ix = 0; ix < 4; ix++)
            for (int iy = 0; iy < 4; iy++)
            {
                var q = new Vector2(Mathf.Lerp(lo.x, hi.x, (ix + 0.5f) / 4f), Mathf.Lerp(lo.y, hi.y, (iy + 0.5f) / 4f));
                if (In(q, a0, a1, a2) && In(q, b0, b1, b2)) inside++;
            }
            return (hi.x - lo.x) * (hi.y - lo.y) * inside / 16f;
        }

        static bool In(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float s1 = Cross(b - a, p - a), s2 = Cross(c - b, p - b), s3 = Cross(a - c, p - c);
            return (s1 >= 0 && s2 >= 0 && s3 >= 0) || (s1 <= 0 && s2 <= 0 && s3 <= 0);
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    }
}
