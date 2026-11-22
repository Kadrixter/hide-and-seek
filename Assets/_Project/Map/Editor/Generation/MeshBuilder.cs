using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// Accumulates boxes, quads, cylinders and text into one mesh with a sub-mesh per material.
    /// UVs are projected in world (or builder-local) space using each material's tile size,
    /// so nothing stretches and adjacent pieces tile seamlessly.
    /// </summary>
    internal sealed class MeshBuilder
    {
        private readonly MaterialLibrary _lib;
        private readonly List<Vector3> _v = new List<Vector3>();
        private readonly List<Vector3> _n = new List<Vector3>();
        private readonly List<Vector2> _uv = new List<Vector2>();
        private readonly List<Material> _mats = new List<Material>();
        private readonly List<List<int>> _tris = new List<List<int>>();

        public MeshBuilder(MaterialLibrary lib) { _lib = lib; }

        public bool IsEmpty => _v.Count == 0;
        public Material[] Materials => _mats.ToArray();
        public MaterialLibrary Lib => _lib;

        /// <summary>Forces a material to occupy the next sub-mesh slot (e.g. car paint = slot 0).</summary>
        public void Reserve(Material m) => Tris(m);

        /// <summary>Optional material substitutions (fewer sub-meshes for cheap models).</summary>
        public Dictionary<Material, Material> Alias;

        private List<int> Tris(Material m)
        {
            if (Alias != null && m != null && Alias.TryGetValue(m, out var sub)) m = sub;
            int i = _mats.IndexOf(m);
            if (i < 0)
            {
                _mats.Add(m);
                _tris.Add(new List<int>());
                i = _mats.Count - 1;
            }
            return _tris[i];
        }

        // ------------------------------------------------------------------ primitives

        /// <summary>Quad centred at c spanning ±uh, ±vh. Front face normal = cross(vh, uh).</summary>
        public void Face(Vector3 c, Vector3 uh, Vector3 vh, Material m, float tileOverride = 0f)
        {
            float tile = tileOverride > 0f ? tileOverride : _lib.Tile(m);
            Vector3 ud = uh.normalized, vd = vh.normalized;
            Vector3 p0 = c - uh - vh, p1 = c - uh + vh, p2 = c + uh + vh, p3 = c + uh - vh;
            AddQuad(p0, p1, p2, p3, Vector3.Cross(vh, uh).normalized,
                Proj(p0, ud, vd, tile), Proj(p1, ud, vd, tile), Proj(p2, ud, vd, tile), Proj(p3, ud, vd, tile), m);
        }

        /// <summary>Quad with an explicit UV rectangle (min at -uh-vh, max at +uh+vh).</summary>
        public void FaceUV(Vector3 c, Vector3 uh, Vector3 vh, Material m, Vector2 uvMin, Vector2 uvMax)
        {
            Vector3 p0 = c - uh - vh, p1 = c - uh + vh, p2 = c + uh + vh, p3 = c + uh - vh;
            AddQuad(p0, p1, p2, p3, Vector3.Cross(vh, uh).normalized,
                new Vector2(uvMin.x, uvMin.y), new Vector2(uvMin.x, uvMax.y),
                new Vector2(uvMax.x, uvMax.y), new Vector2(uvMax.x, uvMin.y), m);
        }

        private static Vector2 Proj(Vector3 p, Vector3 ud, Vector3 vd, float tile) =>
            new Vector2(Vector3.Dot(p, ud) / tile, Vector3.Dot(p, vd) / tile);

        private void AddQuad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 n,
            Vector2 t0, Vector2 t1, Vector2 t2, Vector2 t3, Material m)
        {
            int b = _v.Count;
            _v.Add(p0); _v.Add(p1); _v.Add(p2); _v.Add(p3);
            _n.Add(n); _n.Add(n); _n.Add(n); _n.Add(n);
            _uv.Add(t0); _uv.Add(t1); _uv.Add(t2); _uv.Add(t3);
            var t = Tris(m);
            t.Add(b); t.Add(b + 1); t.Add(b + 2);
            t.Add(b); t.Add(b + 2); t.Add(b + 3);
        }

        /// <summary>
        /// Free quad a-b-c-d with per-corner normals (smooth shading). The front face is picked from the summed
        /// normals, so the corner order may be either winding. UVs are planar along the dominant normal axis.
        /// </summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 na, Vector3 nb, Vector3 nc, Vector3 nd, Material m)
        {
            Vector3 fn = na + nb + nc + nd;
            if (Vector3.Cross(c - a, d - b).sqrMagnitude < 1e-12f) return; // degenerate
            float tile = _lib.Tile(m);
            int i = _v.Count;
            AddV(a, na, fn, tile); AddV(b, nb, fn, tile); AddV(c, nc, fn, tile); AddV(d, nd, fn, tile);
            var t = Tris(m);
            AddTriOriented(t, i, i + 1, i + 2, fn);
            AddTriOriented(t, i, i + 2, i + 3, fn);
        }

        /// <summary>Free triangle with per-corner normals (see <see cref="Quad"/>).</summary>
        public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 na, Vector3 nb, Vector3 nc, Material m)
        {
            Vector3 fn = na + nb + nc;
            if (Vector3.Cross(b - a, c - a).sqrMagnitude < 1e-12f) return;
            float tile = _lib.Tile(m);
            int i = _v.Count;
            AddV(a, na, fn, tile); AddV(b, nb, fn, tile); AddV(c, nc, fn, tile);
            AddTriOriented(Tris(m), i, i + 1, i + 2, fn);
        }

        private void AddV(Vector3 p, Vector3 n, Vector3 fn, float tile)
        {
            _v.Add(p);
            _n.Add(n.sqrMagnitude > 1e-12f ? n.normalized : Vector3.up);
            float ax = Mathf.Abs(fn.x), ay = Mathf.Abs(fn.y), az = Mathf.Abs(fn.z);
            Vector2 uv = ax >= ay && ax >= az ? new Vector2(p.z, p.y) : ay >= az ? new Vector2(p.x, p.z) : new Vector2(p.x, p.y);
            _uv.Add(uv / tile);
        }

        [System.Flags]
        public enum Faces
        {
            None = 0, PosX = 1, NegX = 2, PosY = 4, NegY = 8, PosZ = 16, NegZ = 32,
            All = 63, NoBottom = All & ~NegY
        }

        public void Box(Vector3 center, Vector3 size, Material m, Quaternion? rotation = null,
            Faces faces = Faces.All, float tileOverride = 0f)
        {
            Quaternion r = rotation ?? Quaternion.identity;
            Vector3 hx = r * new Vector3(size.x * 0.5f, 0, 0);
            Vector3 hy = r * new Vector3(0, size.y * 0.5f, 0);
            Vector3 hz = r * new Vector3(0, 0, size.z * 0.5f);
            if ((faces & Faces.PosX) != 0) Face(center + hx, hz, hy, m, tileOverride);
            if ((faces & Faces.NegX) != 0) Face(center - hx, -hz, hy, m, tileOverride);
            if ((faces & Faces.PosY) != 0) Face(center + hy, hx, hz, m, tileOverride);
            if ((faces & Faces.NegY) != 0) Face(center - hy, hx, -hz, m, tileOverride);
            if ((faces & Faces.PosZ) != 0) Face(center + hz, -hx, hy, m, tileOverride);
            if ((faces & Faces.NegZ) != 0) Face(center - hz, hx, hy, m, tileOverride);
        }

        public void BoxMinMax(Vector3 min, Vector3 max, Material m, Faces faces = Faces.All)
        {
            Box((min + max) * 0.5f, max - min, m, null, faces);
        }

        /// <summary>Cylinder between a and b (smooth sides, flat caps).</summary>
        public void Cylinder(Vector3 a, Vector3 b, float radius, int sides, Material m, bool caps = true,
            float radiusB = -1f)
        {
            if (radiusB < 0f) radiusB = radius;
            float tile = _lib.Tile(m);
            Vector3 axis = b - a;
            float len = axis.magnitude;
            if (len < 1e-5f) return;
            Vector3 d = axis / len;
            Vector3 e1 = Vector3.Cross(d, Mathf.Abs(d.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 e2 = Vector3.Cross(d, e1).normalized;
            var t = Tris(m);
            int baseIdx = _v.Count;
            float circ = 2f * Mathf.PI * Mathf.Max(radius, radiusB);
            for (int i = 0; i <= sides; i++)
            {
                float ang = i * Mathf.PI * 2f / sides;
                Vector3 dir = Mathf.Cos(ang) * e1 + Mathf.Sin(ang) * e2;
                Vector3 normal = (dir + d * ((radius - radiusB) / len)).normalized;
                float u = (i / (float)sides) * circ / tile;
                _v.Add(a + dir * radius); _n.Add(normal); _uv.Add(new Vector2(u, 0));
                _v.Add(b + dir * radiusB); _n.Add(normal); _uv.Add(new Vector2(u, len / tile));
            }
            for (int i = 0; i < sides; i++)
            {
                int i0 = baseIdx + i * 2, i1 = i0 + 1, i2 = i0 + 2, i3 = i0 + 3;
                Vector3 outward = (_v[i0] + _v[i2]) * 0.5f - a;
                AddTriOriented(t, i0, i1, i3, outward);
                AddTriOriented(t, i0, i3, i2, outward);
            }
            if (!caps) return;
            Cap(a, -d, e1, e2, radius, sides, m, tile);
            Cap(b, d, e1, e2, radiusB, sides, m, tile);
        }

        private void Cap(Vector3 c, Vector3 n, Vector3 e1, Vector3 e2, float r, int sides, Material m, float tile)
        {
            if (r <= 0f) return;
            var t = Tris(m);
            int ci = _v.Count;
            _v.Add(c); _n.Add(n); _uv.Add(new Vector2(0.5f, 0.5f));
            for (int i = 0; i <= sides; i++)
            {
                float ang = i * Mathf.PI * 2f / sides;
                Vector3 p = c + (Mathf.Cos(ang) * e1 + Mathf.Sin(ang) * e2) * r;
                _v.Add(p); _n.Add(n);
                _uv.Add(new Vector2(Mathf.Cos(ang) * r / tile, Mathf.Sin(ang) * r / tile));
            }
            for (int i = 0; i < sides; i++)
                AddTriOriented(t, ci, ci + 1 + i, ci + 2 + i, n);
        }

        private void AddTriOriented(List<int> t, int a, int b, int c, Vector3 expected)
        {
            Vector3 fn = Vector3.Cross(_v[b] - _v[a], _v[c] - _v[a]);
            if (Vector3.Dot(fn, expected) < 0f) { t.Add(a); t.Add(c); t.Add(b); }
            else { t.Add(a); t.Add(b); t.Add(c); }
        }

        /// <summary>
        /// Text from the pixel-font atlas. <paramref name="normal"/> faces the reader, <paramref name="up"/> is
        /// the direction of the top of the letters. Anchored at the centre of the string.
        /// </summary>
        public void Text(string s, Vector3 center, Vector3 normal, Vector3 up, float height, Material m)
        {
            if (string.IsNullOrEmpty(s)) return;
            Vector3 right = Vector3.Cross(normal, up).normalized;
            up = up.normalized;
            float w = height * PixelFont.Aspect;
            float total = w * s.Length;
            Vector3 start = center - right * (total * 0.5f) + right * (w * 0.5f);
            for (int i = 0; i < s.Length; i++)
            {
                char ch = s[i];
                if (ch == ' ' || !PixelFont.Has(ch)) continue;
                PixelFont.GlyphUV(ch, out var mn, out var mx);
                FaceUV(start + right * (w * i), right * (w * 0.5f), up * (height * 0.5f), m, mn, mx);
            }
        }

        /// <summary>Horizontal up-facing quad (floor decal) with world-projected UVs.</summary>
        public void FloorQuad(float xMin, float xMax, float zMin, float zMax, float y, Material m)
        {
            Face(new Vector3((xMin + xMax) * 0.5f, y, (zMin + zMax) * 0.5f),
                new Vector3((xMax - xMin) * 0.5f, 0, 0), new Vector3(0, 0, (zMax - zMin) * 0.5f), m);
        }

        /// <summary>Up-facing decal quad with a full 0..1 UV rect, rotated so texture +V points along dir.</summary>
        public void FloorDecal(Vector3 center, Vector3 dir, float width, float length, Material m)
        {
            dir.y = 0;
            dir.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, dir);
            FaceUV(center, right * (width * 0.5f), dir * (length * 0.5f), m, Vector2.zero, Vector2.one);
        }

        // ------------------------------------------------------------------ output

        /// <summary>
        /// Merges vertices with the same position, normal and UV (quads are emitted with their own corners). Same
        /// look, a fraction of the vertices; used for the many parked cars.
        /// </summary>
        public void Weld()
        {
            var map = new Dictionary<(int, int, int, int, int, int, int, int), int>();
            var remap = new int[_v.Count];
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>();
            for (int i = 0; i < _v.Count; i++)
            {
                Vector3 p = _v[i], q = _n[i];
                var key = (Mathf.RoundToInt(p.x * 10000f), Mathf.RoundToInt(p.y * 10000f), Mathf.RoundToInt(p.z * 10000f),
                    Mathf.RoundToInt(q.x * 500f), Mathf.RoundToInt(q.y * 500f), Mathf.RoundToInt(q.z * 500f),
                    Mathf.RoundToInt(_uv[i].x * 2000f), Mathf.RoundToInt(_uv[i].y * 2000f));
                if (!map.TryGetValue(key, out int k))
                {
                    k = v.Count;
                    map[key] = k;
                    v.Add(p); n.Add(q); uv.Add(_uv[i]);
                }
                remap[i] = k;
            }
            foreach (var t in _tris)
                for (int i = 0; i < t.Count; i++) t[i] = remap[t[i]];
            _v.Clear(); _v.AddRange(v);
            _n.Clear(); _n.AddRange(n);
            _uv.Clear(); _uv.AddRange(uv);
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            if (_v.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(_v);
            mesh.SetNormals(_n);
            mesh.SetUVs(0, _uv);
            mesh.subMeshCount = _tris.Count;
            for (int i = 0; i < _tris.Count; i++)
                mesh.SetTriangles(_tris[i], i, false);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            mesh.UploadMeshData(false);
            return mesh;
        }
    }
}
