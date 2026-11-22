using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Crumpled paper ball: an icosphere (320 faces) cut flat by random planes (panels with sharp ridges), pushed in
    /// along fold lines and jittered, flat-shaded so every facet catches the light differently. Folds that sink in get baked
    /// occlusion (darker). Uv: each facet is projected onto the paper atlas along its main axis, so printed lines run
    /// on across neighbouring facets and break at the creases, like a real crumpled sheet.
    /// </summary>
    internal static class CrumpledPaper
    {
        static Vector3[] _ico;
        static int[] _tris;
        static List<int>[] _adj;

        /// <summary>Adds one ball (radius ~<paramref name="radius"/>, squash per axis) to <paramref name="mesh"/>.
        /// <paramref name="place"/> maps ball-local points into the mesh space. Returns the lowest y in mesh space.</summary>
        public static float Add(TrashMesh mesh, int seed, float radius, Vector3 squash, TrashArt.PaperKind kind, System.Func<Vector3, Vector3> place)
        {
            Ensure();
            var rnd = new System.Random(seed * 7919 + 13);
            float Rf() => (float)rnd.NextDouble();

            // Flat panels: the ball is cut by random planes (a lumpy polyhedron with sharp ridges), then pushed in
            // along fold lines (valleys, a few ridges) and jittered a little.
            const int panels = 22, folds = 9;
            var pn = new Vector3[panels];
            var ph = new float[panels];
            for (int k = 0; k < panels; k++) { pn[k] = Random3(rnd).normalized; ph[k] = 0.78f + Rf() * 0.22f; }
            var n = new Vector3[folds];
            var o = new float[folds];
            var a = new float[folds];
            for (int k = 0; k < folds; k++)
            {
                n[k] = Random3(rnd).normalized;
                o[k] = (Rf() - 0.5f) * 1.2f;
                a[k] = (0.08f + Rf() * 0.16f) * (Rf() < 0.75f ? -1f : 1f);
            }
            var r = new float[_ico.Length];
            for (int i = 0; i < _ico.Length; i++)
            {
                Vector3 d = _ico[i];
                float v = 1.3f;
                for (int k = 0; k < panels; k++)
                {
                    float c = Vector3.Dot(d, pn[k]);
                    if (c > 0.15f) v = Mathf.Min(v, ph[k] / c);
                }
                for (int k = 0; k < folds; k++) v += a[k] * Mathf.Max(0f, 0.35f - Mathf.Abs(Vector3.Dot(d, n[k]) - o[k]));
                v += (Rf() - 0.5f) * 0.1f;
                r[i] = Mathf.Max(0.45f, v);
            }
            float mean = 0f;
            foreach (float v in r) mean += v;
            mean /= r.Length;
            for (int i = 0; i < r.Length; i++) r[i] /= mean;
            // Occlusion from concavity (neighbours further out than the vertex).
            var ao = new float[_ico.Length];
            for (int i = 0; i < _ico.Length; i++)
            {
                float avg = 0f;
                foreach (int j in _adj[i]) avg += r[j];
                avg /= _adj[i].Count;
                ao[i] = Mathf.Clamp(1f - (avg - r[i]) * 4f, 0.6f, 1f) * Mathf.Clamp(0.85f + 0.15f * r[i], 0.8f, 1.05f);
            }
            var pos = new Vector3[_ico.Length];
            for (int i = 0; i < _ico.Length; i++) pos[i] = Vector3.Scale(_ico[i] * r[i], squash);

            // Uv: the ball's projection fits inside its quadrant of the atlas (flat sheets are wider).
            float ext = 0.01f;
            foreach (var p in pos) ext = Mathf.Max(ext, Mathf.Abs(p.x), Mathf.Abs(p.y), Mathf.Abs(p.z));
            Vector2 q0 = TrashArt.PaperCentre(kind) - new Vector2(0.25f, 0.25f);
            Vector2 centre = TrashArt.PaperCentre(kind) + new Vector2(Rf() - 0.5f, Rf() - 0.5f) * 0.04f;
            float rot = Rf() * Mathf.PI * 2f, cs = Mathf.Cos(rot), sn = Mathf.Sin(rot);
            float uvScale = Mathf.Min(0.17f, 0.2f / ext);
            Vector2 Uv(Vector3 p, int axis)
            {
                Vector2 q = axis == 0 ? new Vector2(p.z, p.y) : axis == 1 ? new Vector2(p.x, p.z) : new Vector2(p.x, p.y);
                q = centre + new Vector2(q.x * cs - q.y * sn, q.x * sn + q.y * cs) * uvScale;
                return new Vector2(Mathf.Clamp(q.x, q0.x + 0.01f, q0.x + 0.49f), Mathf.Clamp(q.y, q0.y + 0.01f, q0.y + 0.49f));
            }

            float minY = float.MaxValue;
            for (int t = 0; t < _tris.Length; t += 3)
            {
                int i0 = _tris[t], i1 = _tris[t + 1], i2 = _tris[t + 2];
                Vector3 p0 = pos[i0], p1 = pos[i1], p2 = pos[i2];
                Vector3 fn = Vector3.Cross(p1 - p0, p2 - p0);
                float ax = Mathf.Abs(fn.x), ay = Mathf.Abs(fn.y), az = Mathf.Abs(fn.z);
                int axis = ax >= ay && ax >= az ? 0 : ay >= az ? 1 : 2;
                Vector3 w0 = place(p0 * radius), w1 = place(p1 * radius), w2 = place(p2 * radius);
                minY = Mathf.Min(minY, Mathf.Min(w0.y, Mathf.Min(w1.y, w2.y)));
                mesh.Tri(w0, w1, w2, Uv(p0, axis), Uv(p1, axis), Uv(p2, axis), TrashMesh.Ao(ao[i0]), TrashMesh.Ao(ao[i1]), TrashMesh.Ao(ao[i2]));
            }
            return minY;
        }

        static Vector3 Random3(System.Random rnd) =>
            new Vector3((float)rnd.NextDouble() - 0.5f, (float)rnd.NextDouble() - 0.5f, (float)rnd.NextDouble() - 0.5f) + Vector3.one * 1e-4f;

        /// <summary>Unit icosphere, 2 subdivisions (162 vertices, 320 faces), and its vertex neighbours.</summary>
        static void Ensure()
        {
            if (_ico != null) return;
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var v = new List<Vector3>
            {
                new(-1, t, 0), new(1, t, 0), new(-1, -t, 0), new(1, -t, 0), new(0, -1, t), new(0, 1, t),
                new(0, -1, -t), new(0, 1, -t), new(t, 0, -1), new(t, 0, 1), new(-t, 0, -1), new(-t, 0, 1),
            };
            for (int i = 0; i < v.Count; i++) v[i] = v[i].normalized;
            var f = new List<int>
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            for (int s = 0; s < 2; s++)
            {
                var mid = new Dictionary<long, int>();
                int Mid(int a, int b)
                {
                    long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    if (mid.TryGetValue(key, out int m)) return m;
                    v.Add(((v[a] + v[b]) * 0.5f).normalized);
                    return mid[key] = v.Count - 1;
                }
                var nf = new List<int>();
                for (int i = 0; i < f.Count; i += 3)
                {
                    int a = f[i], b = f[i + 1], c = f[i + 2];
                    int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                    nf.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                f = nf;
            }
            _ico = v.ToArray();
            _tris = f.ToArray();
            _adj = new List<int>[_ico.Length];
            for (int i = 0; i < _adj.Length; i++) _adj[i] = new List<int>();
            for (int i = 0; i < _tris.Length; i += 3)
            for (int e = 0; e < 3; e++)
            {
                int p = _tris[i + e], q = _tris[i + (e + 1) % 3];
                if (!_adj[p].Contains(q)) _adj[p].Add(q);
                if (!_adj[q].Contains(p)) _adj[q].Add(p);
            }
        }
    }
}
