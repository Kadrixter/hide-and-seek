using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Map.Editor
{
    internal enum TarpKind { Cover, BlueTarp, Foil, Canvas }

    /// <summary>
    /// Soft cover draped over a real car mesh. The car is probed with rays from a point below its middle (in a space
    /// squashed to the car's proportions), one vertical slice per column: the cloth wraps the outline of each slice
    /// (convex hull = taut cloth, partly sagging into hollows like the windscreen foot), is relaxed across slices
    /// (bridging like a tent around the mirrors on loose tarps, mirror pockets on the fitted cover), hangs straight
    /// down from the widest point with folds that grow towards the hem, and a long tarp lies on the floor at the
    /// corners. Columns are resampled by arc length so the rows run parallel to the hem. The canvas is cut off at
    /// the front (pulled back) - <see cref="TarpCut"/> collapses the cut-off part onto the cut line.
    /// </summary>
    internal sealed partial class TarpDrape
    {
        private const int Cols = 128, Dense = 150, Rows = 46;
        private const float FloorRest = 0.012f;

        private struct Spec
        {
            public float Clear, Sag, Hem, HemCorner, HemNoise, FoldAmp, FoldWave, Wrinkle, Flare;
            public int BlurCols;
        }

        private static Spec SpecOf(TarpKind k)
        {
            switch (k)
            {
                case TarpKind.Cover: return new Spec { Clear = 0.012f, Sag = 0.6f, Hem = 0.24f, HemCorner = 0.03f, HemNoise = 0.01f, FoldAmp = 0.01f, FoldWave = 0.17f, Wrinkle = 0.004f, Flare = -0.025f, BlurCols = 1 };
                case TarpKind.BlueTarp: return new Spec { Clear = 0.025f, Sag = 0.12f, Hem = 0.1f, HemCorner = 0.3f, HemNoise = 0.05f, FoldAmp = 0.045f, FoldWave = 0.34f, Wrinkle = 0.009f, Flare = 0.05f, BlurCols = 7 };
                case TarpKind.Foil: return new Spec { Clear = 0.015f, Sag = 0.35f, Hem = 0.2f, HemCorner = 0.12f, HemNoise = 0.1f, FoldAmp = 0.03f, FoldWave = 0.21f, Wrinkle = 0.007f, Flare = 0.025f, BlurCols = 3 };
                default: return new Spec { Clear = 0.03f, Sag = 0.22f, Hem = 0.12f, HemCorner = 0.26f, HemNoise = 0.04f, FoldAmp = 0.06f, FoldWave = 0.42f, Wrinkle = 0.006f, Flare = 0.06f, BlurCols = 5 };
            }
        }

        private readonly MeshCollider _car;
        private readonly TarpKind _kind;
        private readonly Spec _s;
        private readonly System.Random _rnd;
        private readonly float _noiseSeed;
        private Vector3 _o;
        private float _ax, _ay, _az;

        /// <summary>Cloth vertices [row, col] (col Cols = seam copy of col 0) in car space.</summary>
        public Vector3[,] P;
        /// <summary>Optional front cut (canvas pulled back): z of the cut at x = 0 and its slope dz/dx.</summary>
        public float CutZ = float.NaN, CutSlope;
        /// <summary>Points of the cut line (one per column that crosses it), from the left side over the top to the right.</summary>
        public readonly List<Vector3> CutLine = new List<Vector3>();

        /// <summary>Where the probe colliders sit (far below the map, so they never touch the build's physics checks).</summary>
        public static readonly Vector3 ProbeOffset = new Vector3(0f, -500f, 0f);

        public TarpDrape(MeshCollider car, TarpKind kind, int seed)
        {
            _car = car;
            _kind = kind;
            _s = SpecOf(kind);
            _rnd = new System.Random(seed);
            _noiseSeed = (float)_rnd.NextDouble() * 100f;
        }

        public void Drape()
        {
            var b = _car.sharedMesh.bounds;
            _o = new Vector3(b.center.x, -0.55f, b.center.z);
            _ax = b.extents.x + 0.15f;
            _az = b.extents.z + 0.15f;
            _ay = b.max.y - _o.y + 0.2f;

            var dirs = new Vector3[Dense, Cols];
            var sup = new float[Dense, Cols];     // support: car + clearance, or the hanging line
            var r = new float[Dense, Cols];
            var edgeIdx = new int[Cols];
            var edgeY = new float[Cols];
            for (int j = 0; j < Cols; j++)
            {
                float phi = j * Mathf.PI * 2f / Cols;
                var ys = new float[Dense];
                var rho = new float[Dense];
                var car = new float[Dense];
                float rhoEdge = 0.05f, yEdge = 0.3f;
                int iEdge = Dense - 1;
                for (int i = 0; i < Dense; i++)
                {
                    float th = Mathf.Lerp(89.6f, 1f, i / (Dense - 1f)) * Mathf.Deg2Rad;
                    var ds = new Vector3(Mathf.Cos(th) * Mathf.Cos(phi), Mathf.Sin(th), Mathf.Cos(th) * Mathf.Sin(phi));
                    var dw = new Vector3(ds.x * _ax, ds.y * _ay, ds.z * _az).normalized;
                    dirs[i, j] = dw;
                    float h = new Vector2(dw.x, dw.z).magnitude;
                    car[i] = _car.Raycast(new Ray(ProbeOffset + _o + dw * 14f, -dw), out var hit, 14f) ? 14f - hit.distance : 0f;
                    if (car[i] > 0f)
                    {
                        rho[i] = car[i] * h;
                        ys[i] = _o.y + car[i] * dw.y;
                        if (rho[i] > rhoEdge) { rhoEdge = rho[i]; yEdge = ys[i]; iEdge = i; }
                    }
                }
                edgeIdx[j] = iEdge;
                edgeY[j] = yEdge;
                for (int i = 0; i < Dense; i++)
                {
                    var dw = dirs[i, j];
                    float h = Mathf.Max(1e-3f, new Vector2(dw.x, dw.z).magnitude);
                    float s = car[i] > 0f ? car[i] + _s.Clear : 0f;
                    if (i > iEdge) s = Mathf.Max(s, (rhoEdge + _s.Clear) / h);
                    sup[i, j] = s;
                }
                // taut cloth: convex hull of the slice outline, partly sagging back into the hollows
                var hull = HullRadii(j, dirs, sup);
                for (int i = 0; i < Dense; i++)
                    r[i, j] = sup[i, j] > 0f ? Mathf.Lerp(hull[i], sup[i, j], _s.Sag) : hull[i];
            }
            // thin parts (mirrors, aerials) caught by one slice only: the neighbouring slices must clear them too
            var dil = (float[,])sup.Clone();
            for (int i = 0; i < Dense; i++)
            for (int j = 0; j < Cols; j++)
                sup[i, j] = Mathf.Max(dil[i, j], Mathf.Max(dil[i, (j + 1) % Cols], dil[i, (j + Cols - 1) % Cols]) - 0.01f);
            for (int i = 0; i < Dense; i++)
            for (int j = 0; j < Cols; j++)
                r[i, j] = Mathf.Max(r[i, j], sup[i, j]);
            // relax across slices (tenting round mirrors on loose tarps) and a little along each slice
            for (int it = 0; it < _s.BlurCols; it++)
            {
                var copy = (float[,])r.Clone();
                for (int i = 0; i < Dense; i++)
                for (int j = 0; j < Cols; j++)
                {
                    float avg = 0.25f * copy[i, (j + Cols - 1) % Cols] + 0.5f * copy[i, j] + 0.25f * copy[i, (j + 1) % Cols];
                    r[i, j] = Mathf.Max(sup[i, j], avg);
                }
            }
            for (int it = 0; it < 2; it++)
            {
                var copy = (float[,])r.Clone();
                for (int i = 1; i < Dense - 1; i++)
                for (int j = 0; j < Cols; j++)
                    r[i, j] = Mathf.Max(sup[i, j], 0.25f * copy[i - 1, j] + 0.5f * copy[i, j] + 0.25f * copy[i + 1, j]);
            }

            // dense points + folds below the widest point
            var dense = new Vector3[Dense, Cols];
            var edgeArc = EdgeArc(dirs, r, edgeIdx);
            for (int j = 0; j < Cols; j++)
            {
                var hd = new Vector3(dirs[0, j].x, 0f, dirs[0, j].z);
                hd = hd.sqrMagnitude > 1e-6f ? hd.normalized : Vector3.right;
                float phase = (float)(_noiseSeed + j * 0.0f);
                for (int i = 0; i < Dense; i++)
                {
                    Vector3 p = _o + dirs[i, j] * r[i, j];
                    float below = edgeY[j] - p.y;
                    if (i > edgeIdx[j] && below > 0f)
                    {
                        float k = Mathf.Clamp01(below / 0.55f);
                        float wave = Mathf.Sin((edgeArc[j] / _s.FoldWave + Noise(edgeArc[j] * 1.7f, 3.1f) * 1.6f) * Mathf.PI * 2f);
                        float amp = _s.FoldAmp * Mathf.Pow(k, 1.2f) * (0.6f + 0.8f * Noise(edgeArc[j] * 0.9f, 7.7f + phase));
                        p += hd * (amp * wave + _s.Flare * k);
                    }
                    dense[i, j] = p;
                }
            }
            Resample(dense, edgeArc);
            Wrinkle();
            if (!float.IsNaN(CutZ)) TarpCut();
        }

        /// <summary>Distance along every ray of slice <paramref name="j"/> to the convex hull of the support points.</summary>
        private float[] HullRadii(int j, Vector3[,] dirs, float[,] sup)
        {
            // 2D slice: x = horizontal distance from the axis, y = height relative to the probe point.
            var pts = new List<Vector2>();
            var idx = new List<int>();
            for (int i = 0; i < Dense; i++)
            {
                if (sup[i, j] <= 0f) continue;
                var dw = dirs[i, j];
                pts.Add(new Vector2(new Vector2(dw.x, dw.z).magnitude, dw.y) * sup[i, j]);
                idx.Add(i);
            }
            var res = new float[Dense];
            if (pts.Count < 2)
            {
                for (int i = 0; i < Dense; i++) res[i] = Mathf.Max(0.3f, sup[i, j]);
                return res;
            }
            // points are ordered by falling elevation (clockwise round the probe point): keep right turns only
            var hull = new List<int>();
            for (int k = 0; k < pts.Count; k++)
            {
                while (hull.Count >= 2)
                {
                    Vector2 a = pts[hull[hull.Count - 2]], b = pts[hull[hull.Count - 1]], c = pts[k];
                    float cross = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
                    if (cross >= 0f) hull.RemoveAt(hull.Count - 1); // left turn or straight: b is inside
                    else break;
                }
                hull.Add(k);
            }
            for (int i = 0; i < Dense; i++)
            {
                var dw = dirs[i, j];
                var d = new Vector2(new Vector2(dw.x, dw.z).magnitude, dw.y);
                float best = sup[i, j];
                for (int k = 0; k + 1 < hull.Count; k++)
                {
                    Vector2 a = pts[hull[k]], c = pts[hull[k + 1]];
                    if (RaySeg(d, a, c, out float t)) { best = Mathf.Max(best, t); break; }
                }
                res[i] = best > 0f ? best : 0.3f;
            }
            return res;
        }

        private static bool RaySeg(Vector2 d, Vector2 a, Vector2 b, out float t)
        {
            t = 0f;
            Vector2 e = b - a;
            float den = d.x * e.y - d.y * e.x;
            if (Mathf.Abs(den) < 1e-7f) return false;
            t = (a.x * e.y - a.y * e.x) / den;
            float u = (a.x * d.y - a.y * d.x) / den;
            return t > 0f && u >= -1e-4f && u <= 1f + 1e-4f;
        }

        /// <summary>Arc length round the car at the widest point of every slice (fold phase).</summary>
        private float[] EdgeArc(Vector3[,] dirs, float[,] r, int[] edgeIdx)
        {
            var arc = new float[Cols];
            Vector3 prev = _o + dirs[edgeIdx[0], 0] * r[edgeIdx[0], 0];
            for (int j = 1; j < Cols; j++)
            {
                Vector3 p = _o + dirs[edgeIdx[j], j] * r[edgeIdx[j], j];
                p.y = 0f;
                prev.y = 0f;
                arc[j] = arc[j - 1] + Vector3.Distance(prev, p);
                prev = p;
            }
            return arc;
        }

        private float Noise(float x, float y) => Mathf.PerlinNoise(x + _noiseSeed, y + _noiseSeed * 0.37f);

        private float HemAt(Vector3 p)
        {
            // lower at the four corners of the sheet (diagonals of the car), a little ragged
            float cx = Mathf.Abs(p.x - _o.x) / _ax, cz = Mathf.Abs(p.z - _o.z) / _az;
            float corner = Mathf.Pow(Mathf.Clamp01(Mathf.Min(cx, cz) * 1.35f), 2.5f);
            float ang = Mathf.Atan2(p.z - _o.z, p.x - _o.x);
            return _s.Hem - _s.HemCorner * corner + (Noise(ang * 2.3f, 1.3f) - 0.5f) * 2f * _s.HemNoise;
        }

        /// <summary>Cuts every slice at its hem (cloth beyond the floor lies flat outwards) and resamples by arc length.</summary>
        private void Resample(Vector3[,] dense, float[] edgeArc)
        {
            P = new Vector3[Rows, Cols + 1];
            for (int j = 0; j < Cols; j++)
            {
                var line = new List<Vector3>();
                Vector3 hd = dense[Dense - 1, j] - _o;
                hd.y = 0f;
                hd = hd.sqrMagnitude > 1e-6f ? hd.normalized : Vector3.right;
                float hem = HemAt(dense[Dense / 2, j]);
                for (int i = 0; i < Dense; i++)
                {
                    Vector3 p = dense[i, j];
                    float stop = Mathf.Max(hem, FloorRest);
                    if (p.y > stop) { line.Add(p); continue; }
                    // crossing point
                    Vector3 a = line.Count > 0 ? line[line.Count - 1] : p;
                    float t = Mathf.Abs(a.y - p.y) > 1e-5f ? (a.y - stop) / (a.y - p.y) : 0f;
                    Vector3 c = Vector3.Lerp(a, p, Mathf.Clamp01(t));
                    line.Add(c);
                    if (hem < FloorRest)
                    {
                        // the rest of the sheet lies on the floor, pushed outwards, edge slightly wavy
                        float lie = FloorRest - hem;
                        Vector3 f = new Vector3(c.x, FloorRest, c.z);
                        line.Add(f + hd * (lie * 0.4f));
                        line.Add(f + hd * lie + new Vector3(0f, 0.004f, 0f));
                    }
                    break;
                }
                float total = 0f;
                var cum = new float[line.Count];
                for (int k = 1; k < line.Count; k++) { total += Vector3.Distance(line[k - 1], line[k]); cum[k] = total; }
                int seg = 1;
                for (int row = 0; row < Rows; row++)
                {
                    // denser rows near the hem (folds) and the top
                    float u = row / (Rows - 1f);
                    float s = total * u;
                    while (seg < line.Count - 1 && cum[seg] < s) seg++;
                    float len = cum[seg] - cum[seg - 1];
                    float t = len > 1e-6f ? (s - cum[seg - 1]) / len : 0f;
                    P[row, j] = line.Count > 1 ? Vector3.Lerp(line[seg - 1], line[seg], Mathf.Clamp01(t)) : line[0];
                }
            }
            for (int row = 0; row < Rows; row++) P[row, Cols] = P[row, 0];
        }

        /// <summary>Small creases over the whole sheet (along the normal).</summary>
        private void Wrinkle()
        {
            if (_s.Wrinkle <= 0f) return;
            var n = Normals();
            for (int i = 1; i < Rows; i++)
            for (int j = 0; j < Cols; j++)
            {
                Vector3 p = P[i, j];
                float w = Mathf.PerlinNoise(p.x * 6.3f + _noiseSeed, p.z * 5.1f + p.y * 3.7f) - 0.5f;
                float w2 = Mathf.PerlinNoise(p.z * 14f + _noiseSeed * 2f, p.y * 13f + p.x * 9f) - 0.5f;
                float ridge = 1f - Mathf.Abs(w) * 2f;
                P[i, j] = p + n[i, j] * _s.Wrinkle * (ridge * ridge * 1.4f - 0.5f + w2 * 0.8f);
                if (P[i, j].y < FloorRest) P[i, j].y = FloorRest;
            }
            for (int i = 0; i < Rows; i++) P[i, Cols] = P[i, 0];
        }

        /// <summary>Outward vertex normals of the grid (seam column copies column 0).</summary>
        public Vector3[,] Normals()
        {
            var n = new Vector3[Rows, Cols + 1];
            for (int i = 0; i < Rows; i++)
            for (int j = 0; j < Cols; j++)
            {
                Vector3 du = P[i, (j + 1) % Cols] - P[i, (j + Cols - 1) % Cols];
                Vector3 dv = P[Mathf.Min(Rows - 1, i + 1), j] - P[Mathf.Max(0, i - 1), j];
                Vector3 c = Vector3.Cross(du, dv);
                if (c.sqrMagnitude < 1e-10f) c = Vector3.up;
                c.Normalize();
                Vector3 outw = P[i, j] - new Vector3(_o.x, Mathf.Min(P[i, j].y, 0.4f), _o.z);
                if (i == 0) c = Vector3.up;
                else if (Vector3.Dot(c, outw) < 0f) c = -c;
                n[i, j] = c;
            }
            for (int i = 0; i < Rows; i++) n[i, Cols] = n[i, 0];
            return n;
        }

        /// <summary>
        /// Grid mesh: main cloth (rows 0..Rows-3) and the hem band (last two rows) as two sub-meshes; UV = arc
        /// lengths in metres / <paramref name="tile"/>.
        /// </summary>
        public Mesh ToMesh(string name, float tile)
        {
            var n = Normals();
            var v = new List<Vector3>();
            var nn = new List<Vector3>();
            var uv = new List<Vector2>();
            for (int i = 0; i < Rows; i++)
            {
                float u = 0f;
                for (int j = 0; j <= Cols; j++)
                {
                    if (j > 0) u += Vector3.Distance(P[i, j - 1], P[i, j]);
                    float vv = 0f;
                    for (int k = 1; k <= i; k++) vv += Vector3.Distance(P[k - 1, j], P[k, j]);
                    v.Add(P[i, j]);
                    nn.Add(n[i, j]);
                    uv.Add(new Vector2(u, vv) / tile);
                }
            }
            var main = new List<int>();
            var hem = new List<int>();
            int w = Cols + 1;
            for (int i = 0; i < Rows - 1; i++)
            for (int j = 0; j < Cols; j++)
            {
                int a = i * w + j, b = i * w + j + 1, c = (i + 1) * w + j + 1, d = (i + 1) * w + j;
                var list = i >= Rows - 3 ? hem : main;
                // front face (Unity: cross(b - a, c - a)) towards the outside; diagonals also work when a row is a point
                Vector3 fn = Vector3.Cross(v[c] - v[a], v[d] - v[b]);
                if (Vector3.Dot(fn, nn[a] + nn[b] + nn[c] + nn[d]) > 0f) { list.Add(a); list.Add(b); list.Add(c); list.Add(a); list.Add(c); list.Add(d); }
                else { list.Add(a); list.Add(c); list.Add(b); list.Add(a); list.Add(d); list.Add(c); }
            }
            var mesh = new Mesh { name = name };
            mesh.indexFormat = v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(v);
            mesh.SetNormals(nn);
            mesh.SetUVs(0, uv);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(main, 0, false);
            mesh.SetTriangles(hem, 1, false);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }
    }
}
