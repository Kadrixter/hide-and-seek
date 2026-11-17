using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// A set of <see cref="MeshBuilder"/>s, one per 30 x 30 m spatial chunk. Axis-aligned boxes, quads and
    /// horizontal pipes that cross chunk borders are split, so every resulting mesh has tight bounds and
    /// frustum/occlusion culling work per chunk. UVs are world-projected, so splits are invisible.
    /// </summary>
    internal sealed class ChunkedBuilder
    {
        public const float ChunkSize = 30f;
        public const float OriginX = -90f, OriginZ = -60f;
        public const int NX = 6, NZ = 4;

        private readonly MaterialLibrary _lib;
        private readonly SortedDictionary<int, MeshBuilder> _chunks = new SortedDictionary<int, MeshBuilder>();

        public ChunkedBuilder(MaterialLibrary lib) { _lib = lib; }

        public static int ChunkX(float x) => Mathf.Clamp(Mathf.FloorToInt((x - OriginX) / ChunkSize), 0, NX - 1);
        public static int ChunkZ(float z) => Mathf.Clamp(Mathf.FloorToInt((z - OriginZ) / ChunkSize), 0, NZ - 1);

        public IEnumerable<KeyValuePair<int, MeshBuilder>> Chunks => _chunks;

        public MeshBuilder At(Vector3 p)
        {
            int key = ChunkX(p.x) + ChunkZ(p.z) * NX;
            if (!_chunks.TryGetValue(key, out var b))
            {
                b = new MeshBuilder(_lib);
                _chunks[key] = b;
            }
            return b;
        }

        /// <summary>Splits [lo, hi] at interior chunk borders.</summary>
        private static List<(float a, float b)> Split(float lo, float hi, float origin, int count)
        {
            var r = new List<(float, float)>();
            float a = lo;
            for (int k = 1; k < count; k++)
            {
                float edge = origin + k * ChunkSize;
                if (edge <= a + 1e-3f) continue;
                if (edge >= hi - 1e-3f) break;
                r.Add((a, edge));
                a = edge;
            }
            r.Add((a, hi));
            return r;
        }

        private static bool IsIdentity(Quaternion? q) =>
            !q.HasValue || Quaternion.Angle(q.Value, Quaternion.identity) < 0.01f;

        public void Box(Vector3 center, Vector3 size, Material m, Quaternion? rotation = null,
            MeshBuilder.Faces faces = MeshBuilder.Faces.All, float tileOverride = 0f)
        {
            if (!IsIdentity(rotation))
            {
                At(center).Box(center, size, m, rotation, faces, tileOverride);
                return;
            }
            Vector3 min = center - size * 0.5f, max = center + size * 0.5f;
            foreach (var (xa, xb) in Split(min.x, max.x, OriginX, NX))
            foreach (var (za, zb) in Split(min.z, max.z, OriginZ, NZ))
            {
                var c = new Vector3((xa + xb) * 0.5f, center.y, (za + zb) * 0.5f);
                At(c).Box(c, new Vector3(xb - xa, size.y, zb - za), m, null, faces, tileOverride);
            }
        }

        public void BoxMinMax(Vector3 min, Vector3 max, Material m, MeshBuilder.Faces faces = MeshBuilder.Faces.All)
        {
            Box((min + max) * 0.5f, max - min, m, null, faces);
        }

        private static bool Along(Vector3 v, int axis)
        {
            float mag = v.magnitude;
            return mag > 1e-5f && Mathf.Abs(v[axis]) > 0.999f * mag;
        }

        public void Face(Vector3 c, Vector3 uh, Vector3 vh, Material m, float tileOverride = 0f)
        {
            FaceSplit(c, uh, vh, m, tileOverride, 0);
        }

        private void FaceSplit(Vector3 c, Vector3 uh, Vector3 vh, Material m, float tile, int axisStage)
        {
            if (axisStage > 1)
            {
                At(c).Face(c, uh, vh, m, tile);
                return;
            }
            int axis = axisStage == 0 ? 0 : 2;
            float origin = axis == 0 ? OriginX : OriginZ;
            int count = axis == 0 ? NX : NZ;
            bool splitU = Along(uh, axis), splitV = !splitU && Along(vh, axis);
            if (!splitU && !splitV)
            {
                FaceSplit(c, uh, vh, m, tile, axisStage + 1);
                return;
            }
            Vector3 h = splitU ? uh : vh;
            float half = Mathf.Abs(h[axis]);
            Vector3 dir = h.normalized;
            foreach (var (a, b) in Split(c[axis] - half, c[axis] + half, origin, count))
            {
                Vector3 nc = c;
                nc[axis] = (a + b) * 0.5f;
                Vector3 nh = dir * ((b - a) * 0.5f);
                if (splitU) FaceSplit(nc, nh, vh, m, tile, axisStage + 1);
                else FaceSplit(nc, uh, nh, m, tile, axisStage + 1);
            }
        }

        public void FaceUV(Vector3 c, Vector3 uh, Vector3 vh, Material m, Vector2 uvMin, Vector2 uvMax) =>
            At(c).FaceUV(c, uh, vh, m, uvMin, uvMax);

        public void Text(string s, Vector3 center, Vector3 normal, Vector3 up, float height, Material m) =>
            At(center).Text(s, center, normal, up, height, m);

        public void FloorQuad(float xMin, float xMax, float zMin, float zMax, float y, Material m) =>
            Face(new Vector3((xMin + xMax) * 0.5f, y, (zMin + zMax) * 0.5f),
                new Vector3((xMax - xMin) * 0.5f, 0, 0), new Vector3(0, 0, (zMax - zMin) * 0.5f), m);

        public void FloorDecal(Vector3 center, Vector3 dir, float width, float length, Material m) =>
            At(center).FloorDecal(center, dir, width, length, m);

        public void Cylinder(Vector3 a, Vector3 b, float radius, int sides, Material m, bool caps = true, float radiusB = -1f)
        {
            Vector3 d = b - a;
            int axis = Along(d, 0) ? 0 : Along(d, 2) ? 2 : -1;
            if (axis < 0 || radiusB >= 0f)
            {
                At((a + b) * 0.5f).Cylinder(a, b, radius, sides, m, caps, radiusB);
                return;
            }
            float lo = Mathf.Min(a[axis], b[axis]), hi = Mathf.Max(a[axis], b[axis]);
            foreach (var (s0, s1) in Split(lo, hi, axis == 0 ? OriginX : OriginZ, axis == 0 ? NX : NZ))
            {
                Vector3 pa = a, pb = a;
                pa[axis] = s0;
                pb[axis] = s1;
                At((pa + pb) * 0.5f).Cylinder(pa, pb, radius, sides, m, caps);
            }
        }
    }
}
