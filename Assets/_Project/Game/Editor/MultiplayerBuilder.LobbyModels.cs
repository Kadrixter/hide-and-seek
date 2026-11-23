using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HideAndSeek.Game.Editor
{
    /// <summary>
    /// Lobby model polish: rounded / puffy furniture meshes instead of hard cubes, real tapered leaves for the plants,
    /// tufted poufs. Runs after the lobby is assembled (before the z-fight pass).
    /// </summary>
    public static partial class MultiplayerBuilder
    {
        private static string MeshDir => LobbyArtDir + "/Meshes";

        private static void BuildLobbyModels(Transform root)
        {
            if (!AssetDatabase.IsValidFolder(MeshDir))
            {
                Directory.CreateDirectory(MeshDir);
                AssetDatabase.Refresh();
            }

            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == null) continue;
                switch (t.name)
                {
                    case "CoffeeTable": RoundCube(t, 0.018f, 0f); break;
                }
            }

            BuildSeatModels(root); // sofas and poufs (LobbySeatModels.cs)

            // Plants: tapered, arched leaves (two shades), a few stems.
            var leafDark = Mat("LobbyLeafDark", new Color(0.12f, 0.32f, 0.12f), null, 0.35f, Vector2.one);
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == null || t.name != "Plant") continue;
                Material leaf = null;
                var old = new List<GameObject>();
                foreach (Transform c in t)
                    if (c.name == "Leaf")
                    {
                        if (leaf == null) leaf = c.GetComponent<MeshRenderer>().sharedMaterial;
                        old.Add(c.gameObject);
                    }
                foreach (var g in old) Object.DestroyImmediate(g);
                if (leaf == null) leaf = leafDark;
                var rnd = new System.Random(t.GetSiblingIndex() * 31 + 7);
                int count = 15;
                for (int i = 0; i < count; i++)
                {
                    float yaw = i * 137.5f + (float)rnd.NextDouble() * 20f;
                    bool inner = i % 3 == 0;
                    float len = inner ? 0.75f + (float)rnd.NextDouble() * 0.3f : 0.55f + (float)rnd.NextDouble() * 0.45f;
                    float elev = inner ? 78f : 55f + (float)rnd.NextDouble() * 18f;
                    var go = new GameObject("Leaf");
                    go.transform.SetParent(t, false);
                    go.transform.localPosition = new Vector3(0f, 0.48f, 0f) + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 0f, 0.03f);
                    go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                    go.AddComponent<MeshFilter>().sharedMesh = LeafMesh(len, 0.1f + (float)rnd.NextDouble() * 0.04f, elev, inner ? 35f : 70f);
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = i % 2 == 0 ? leaf : leafDark;
                    go.isStatic = true;
                }
            }
        }

        /// <summary>Replaces a unit-cube primitive (no children) with a rounded box of the same size.</summary>
        private static void RoundCube(Transform t, float radius, float puff)
        {
            var mf = t.GetComponent<MeshFilter>();
            if (mf == null || t.childCount > 0) return;
            Vector3 size = t.localScale;
            mf.sharedMesh = RoundedBox(size, radius, puff);
            t.localScale = Vector3.one;
            var bc = t.GetComponent<BoxCollider>();
            if (bc != null)
            {
                bc.size = size;
                bc.center = Vector3.zero;
            }
        }

        private static Mesh SaveMesh(Mesh m, string key)
        {
            string path = $"{MeshDir}/{key}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                existing.Clear();
                EditorUtility.CopySerialized(m, existing);
                Object.DestroyImmediate(m);
                return existing;
            }
            m.name = key;
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        /// <summary>Box with rounded edges/corners (radius r); puff bulges the faces outwards like a cushion.</summary>
        private static Mesh RoundedBox(Vector3 size, float r, float puff)
        {
            string key = $"RBox_{size.x:0.000}_{size.y:0.000}_{size.z:0.000}_{r:0.000}_{puff:0.000}".Replace(',', '.');
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshDir}/{key}.asset");
            if (existing != null) return existing;

            Vector3 h = size * 0.5f;
            r = Mathf.Min(r, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) * 0.95f);
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            float[] Coords(float half)
            {
                float a = half - r;
                var list = new List<float> { -half, -a - r * 0.62f, -a - r * 0.2f, -a };
                for (int i = 1; i < 4; i++) list.Add(Mathf.Lerp(-a, a, i / 4f));
                list.Add(a); list.Add(a + r * 0.2f); list.Add(a + r * 0.62f); list.Add(half);
                return list.ToArray();
            }

            void Face(Vector3 n, Vector3 u, Vector3 v)
            {
                float hu = Mathf.Abs(Vector3.Dot(h, u)), hv = Mathf.Abs(Vector3.Dot(h, v)), hn = Mathf.Abs(Vector3.Dot(h, n));
                var cu = Coords(hu);
                var cv = Coords(hv);
                int start = verts.Count;
                for (int j = 0; j < cv.Length; j++)
                for (int i = 0; i < cu.Length; i++)
                {
                    Vector3 p = n * hn + u * cu[i] + v * cv[j];
                    Vector3 inner = new Vector3(Mathf.Clamp(p.x, -(h.x - r), h.x - r), Mathf.Clamp(p.y, -(h.y - r), h.y - r), Mathf.Clamp(p.z, -(h.z - r), h.z - r));
                    Vector3 d = p - inner;
                    Vector3 nn = d.sqrMagnitude > 1e-12f ? d.normalized : n;
                    Vector3 pos = inner + nn * r;
                    float fu = cu[i] / hu, fv = cv[j] / hv;
                    pos += n * puff * Mathf.Max(0f, 1f - fu * fu) * Mathf.Max(0f, 1f - fv * fv);
                    verts.Add(pos);
                    norms.Add(nn);
                    uvs.Add(new Vector2(0.5f + 0.5f * fu, 0.5f + 0.5f * fv));
                }
                int w = cu.Length;
                for (int j = 0; j < cv.Length - 1; j++)
                for (int i = 0; i < w - 1; i++)
                {
                    int a = start + j * w + i;
                    tris.Add(a); tris.Add(a + w); tris.Add(a + 1);
                    tris.Add(a + 1); tris.Add(a + w); tris.Add(a + w + 1);
                }
            }

            Face(Vector3.up, Vector3.right, Vector3.forward);
            Face(Vector3.down, Vector3.forward, Vector3.right);
            Face(Vector3.right, Vector3.forward, Vector3.up);
            Face(Vector3.left, Vector3.up, Vector3.forward);
            Face(Vector3.forward, Vector3.up, Vector3.right);
            Face(Vector3.back, Vector3.right, Vector3.up);

            var m = new Mesh();
            m.SetVertices(verts);
            m.SetNormals(norms);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            // Puffed faces: recompute normals where the bulge tilts them, keep the rounded edges smooth.
            if (puff > 0f) m.RecalculateNormals();
            m.RecalculateBounds();
            m.RecalculateTangents();
            return SaveMesh(m, key);
        }

        /// <summary>Round cushion top: radius r, bulging up by height (flat underside at y=0). (Old pouf, unused.)</summary>
        private static Mesh PuffDisc(float r, float height)
        {
            string key = $"Puff_{r:0.000}_{height:0.000}".Replace(',', '.');
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshDir}/{key}.asset");
            if (existing != null) return existing;
            const int seg = 32, rings = 6;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            verts.Add(new Vector3(0, height, 0));
            uvs.Add(new Vector2(0.5f, 0.5f));
            for (int k = 1; k <= rings; k++)
            {
                float f = k / (float)rings;
                float y = height * Mathf.Cos(f * Mathf.PI * 0.5f) * (1f - 0.25f * f * f) - 0.008f * Mathf.Exp(-((f - 0.15f) * (f - 0.15f)) * 300f);
                for (int s = 0; s < seg; s++)
                {
                    float a = s / (float)seg * Mathf.PI * 2f;
                    verts.Add(new Vector3(Mathf.Cos(a) * r * f, y, Mathf.Sin(a) * r * f));
                    uvs.Add(new Vector2(0.5f + 0.5f * f * Mathf.Cos(a), 0.5f + 0.5f * f * Mathf.Sin(a)));
                }
            }
            for (int s = 0; s < seg; s++)
            {
                tris.Add(0); tris.Add(1 + (s + 1) % seg); tris.Add(1 + s);
            }
            for (int k = 1; k < rings; k++)
            for (int s = 0; s < seg; s++)
            {
                int a = 1 + (k - 1) * seg + s, b = 1 + (k - 1) * seg + (s + 1) % seg;
                int c = a + seg, d = b + seg;
                tris.Add(a); tris.Add(b); tris.Add(c);
                tris.Add(b); tris.Add(d); tris.Add(c);
            }
            var m = new Mesh();
            m.SetVertices(verts);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return SaveMesh(m, key);
        }

        /// <summary>
        /// Leaf blade: starts at the origin, rises at <paramref name="elevation"/> degrees towards +Z and arches down by
        /// <paramref name="droop"/> degrees at the tip. Pointed, with a folded midrib. Double sided.
        /// </summary>
        private static Mesh LeafMesh(float length, float width, float elevation, float droop)
        {
            string key = $"Leaf_{length:0.00}_{width:0.000}_{elevation:0}_{droop:0}".Replace(',', '.');
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshDir}/{key}.asset");
            if (existing != null) return existing;
            const int seg = 10;
            var front = new List<Vector3>();
            var uvs = new List<Vector2>();
            Vector3 p = Vector3.zero;
            float step = length / seg;
            for (int i = 0; i <= seg; i++)
            {
                float t = i / (float)seg;
                float ang = (elevation - droop * t * t) * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(0f, Mathf.Sin(ang), Mathf.Cos(ang));
                Vector3 up = new Vector3(0f, Mathf.Cos(ang), -Mathf.Sin(ang));
                float w = width * 0.5f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.Min(1f, t * 0.95f + 0.05f))), 0.75f) * (i == seg ? 0f : 1f);
                if (i == 0) w = width * 0.12f;
                float fold = w * 0.35f;
                front.Add(p + new Vector3(-w, 0f, 0f) - up * fold);
                front.Add(p);
                front.Add(p + new Vector3(w, 0f, 0f) - up * fold);
                uvs.Add(new Vector2(0f, t)); uvs.Add(new Vector2(0.5f, t)); uvs.Add(new Vector2(1f, t));
                p += dir * step;
            }
            var verts = new List<Vector3>(front);
            verts.AddRange(front);
            var allUv = new List<Vector2>(uvs);
            allUv.AddRange(uvs);
            int off = front.Count;
            var tris = new List<int>();
            for (int i = 0; i < seg; i++)
            {
                for (int c = 0; c < 2; c++)
                {
                    int a = i * 3 + c, b = a + 1, d = a + 3, e = a + 4;
                    tris.Add(a); tris.Add(d); tris.Add(b);
                    tris.Add(b); tris.Add(d); tris.Add(e);
                    tris.Add(off + a); tris.Add(off + b); tris.Add(off + d);
                    tris.Add(off + b); tris.Add(off + e); tris.Add(off + d);
                }
            }
            var m = new Mesh();
            m.SetVertices(verts);
            m.SetUVs(0, allUv);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return SaveMesh(m, key);
        }
    }
}
