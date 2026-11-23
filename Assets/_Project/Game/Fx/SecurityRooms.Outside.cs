using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Outside of the room walls dressed like the garage walls around it: the garage's own concrete material
    /// (old concrete on level -2) and the same painted band at the bottom (grey skirting 0-12 cm, the level colour up to
    /// 1 m, a white stripe up to 1.08 m), UVs projected in world metres with the garage tiling. The painted wall boxes
    /// sit 4 cm inside, so the skin never shares a plane with them. Materials are taken from the built garage.
    /// </summary>
    public partial class SecurityRooms
    {
        private const float SkinInset = 0.04f;
        private Dictionary<string, Material> _garageMats;

        private Material GarageMat(string name)
        {
            if (_garageMats == null)
            {
                _garageMats = new Dictionary<string, Material>();
                foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude))
                {
                    if (r.transform.IsChildOf(transform)) continue;
                    foreach (var m in r.sharedMaterials)
                        if (m != null && !_garageMats.ContainsKey(m.name)) _garageMats[m.name] = m;
                }
            }
            return _garageMats.TryGetValue(name, out var mat) ? mat : null;
        }

        private void BuildOutsideSkin(Transform root, int lv, float y0)
        {
            var concrete = GarageMat(lv == 2 ? "Wall_Concrete_Old" : "Wall_Concrete") ?? GarageMat("Wall_Concrete");
            var grey = GarageMat("Paint_Grey");
            var band = GarageMat(lv == 0 ? "Paint_Blue" : lv == 1 ? "Paint_Orange" : "Paint_Red");
            var white = GarageMat("Paint_White");
            if (concrete == null)
            {
                Debug.LogWarning("[Security] garage wall material not found, room walls keep their paint outside");
                return;
            }
            float h = Wall * 0.5f, top = y0 + Height;
            var faces = new List<(Vector3 a, Vector3 b, Vector3 n)>   // bottom-left / bottom-right corners seen from outside
            {
                (new Vector3(X0 - h, y0, GarageWallZ), new Vector3(X0 - h, y0, Z0 - h), Vector3.left),
                (new Vector3(X1 + h, y0, Z0 - h), new Vector3(X1 + h, y0, GarageWallZ), Vector3.right),
                (new Vector3(X0 - h, y0, Z0 - h), new Vector3(DoorA, y0, Z0 - h), Vector3.back),
                (new Vector3(DoorB, y0, Z0 - h), new Vector3(X1 + h, y0, Z0 - h), Vector3.back),
            };
            var mesh = new Dictionary<Material, ProcQuads>();
            void Add(Material m, Vector3 a, Vector3 b, float ya, float yb, Vector3 n, float tile)
            {
                if (m == null) m = concrete;
                if (!mesh.TryGetValue(m, out var q)) mesh[m] = q = new ProcQuads();
                q.Add(new Vector3(a.x, ya, a.z), new Vector3(b.x, ya, b.z), new Vector3(b.x, yb, b.z), new Vector3(a.x, yb, a.z), n, tile);
            }
            foreach (var (a, b, n) in faces)
            {
                Add(grey, a, b, y0, y0 + 0.12f, n, 1.5f);
                Add(band, a, b, y0 + 0.12f, y0 + 1.0f, n, 1.5f);
                Add(white, a, b, y0 + 1.0f, y0 + 1.08f, n, 1f);
                Add(concrete, a, b, y0 + 1.08f, top, n, 3f);
            }
            // Above the door: concrete only.
            Add(concrete, new Vector3(DoorA, y0, Z0 - h), new Vector3(DoorB, y0, Z0 - h), y0 + 2.1f, top, Vector3.back, 3f);

            var go = new GameObject("OutsideSkin");
            go.transform.SetParent(root, false);
            var mats = new List<Material>();
            var pm = new List<ProcQuads>();
            foreach (var kv in mesh) { mats.Add(kv.Key); pm.Add(kv.Value); }
            go.AddComponent<MeshFilter>().sharedMesh = ProcQuads.Build("SecOutsideSkin_" + lv, pm);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = mats.ToArray();
            SetupRenderer(r, true);
            r.shadowCastingMode = ShadowCastingMode.Off;   // the painted wall boxes behind cast the shadow
        }

        /// <summary>Flat quads for one material with world-projected UVs (along the wall / height in metres over tile).</summary>
        private sealed class ProcQuads
        {
            public readonly List<Vector3> V = new(), N = new();
            public readonly List<Vector2> UV = new();
            public readonly List<int> T = new();

            public void Add(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 n, float tile)
            {
                int b = V.Count;
                Vector3 along = (p1 - p0).normalized;
                foreach (var p in new[] { p0, p1, p2, p3 })
                {
                    V.Add(p);
                    N.Add(n);
                    UV.Add(new Vector2(Vector3.Dot(p, along) / tile, p.y / tile));
                }
                // p0 -> p3 -> p2 -> p1 is clockwise seen from outside (normal n).
                bool cw = Vector3.Dot(Vector3.Cross(p3 - p0, p2 - p0), n) > 0f;
                if (cw) { T.Add(b); T.Add(b + 3); T.Add(b + 2); T.Add(b); T.Add(b + 2); T.Add(b + 1); }
                else { T.Add(b); T.Add(b + 1); T.Add(b + 2); T.Add(b); T.Add(b + 2); T.Add(b + 3); }
            }

            public static Mesh Build(string name, List<ProcQuads> subs)
            {
                var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>();
                var m = new Mesh { name = name };
                var tris = new List<List<int>>();
                foreach (var s in subs)
                {
                    int off = v.Count;
                    v.AddRange(s.V); n.AddRange(s.N); uv.AddRange(s.UV);
                    var t = new List<int>(s.T.Count);
                    foreach (int i in s.T) t.Add(i + off);
                    tris.Add(t);
                }
                m.SetVertices(v);
                m.SetNormals(n);
                m.SetUVs(0, uv);
                m.subMeshCount = tris.Count;
                for (int i = 0; i < tris.Count; i++) m.SetTriangles(tris[i], i);
                m.RecalculateBounds();
                m.RecalculateTangents();
                return m;
            }
        }
    }
}
