using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace HideAndSeek.Game.Gfx
{
    /// <summary>
    /// Test tool (-testnormals &lt;dir&gt;): checks the normals of every readable mesh in the loaded scenes. A zero / NaN
    /// normal, or a triangle whose corner normals point against each other (the interpolated normal passes through
    /// zero inside it), makes the Standard shader return NaN - a black pixel that the post effects can spread.
    /// </summary>
    public static class MeshNormalScan
    {
        public static string Run()
        {
            var seen = new HashSet<Mesh>();
            var sb = new StringBuilder();
            int meshes = 0, badMeshes = 0;
            void Check(Mesh m, Renderer r)
            {
                if (m == null || !seen.Add(m)) return;
                meshes++;
                if (!m.isReadable) return;
                var n = m.normals;
                var v = m.vertices;
                if (n == null || n.Length != v.Length) { sb.AppendLine($"{m.name}: no normals ({Path(r)})"); badMeshes++; return; }
                var line = new StringBuilder();
                int totalBad = 0;
                var mats = r != null ? r.sharedMaterials : null;
                for (int s = 0; s < m.subMeshCount; s++)
                {
                    if (m.GetTopology(s) != MeshTopology.Triangles) continue;
                    var t = m.GetTriangles(s);
                    int zero = 0, nan = 0, opp = 0, degen = 0;
                    var badV = new HashSet<int>();
                    Vector3 sample = default;
                    for (int i = 0; i + 2 < t.Length; i += 3)
                    {
                        int a = t[i], b = t[i + 1], c = t[i + 2];
                        bool triBad = false;
                        foreach (int k in new[] { a, b, c })
                        {
                            var q = n[k];
                            if (float.IsNaN(q.x) || float.IsNaN(q.y) || float.IsNaN(q.z)) { if (badV.Add(k)) nan++; triBad = true; }
                            else if (!HideAndSeek.Core.MeshNormalFix.Usable(q)) { if (badV.Add(k)) zero++; triBad = true; }
                        }
                        float ab = Vector3.Dot(n[a], n[b]), bc = Vector3.Dot(n[b], n[c]), ca = Vector3.Dot(n[c], n[a]);
                        if (ab < -0.7f || bc < -0.7f || ca < -0.7f) { opp++; triBad = true; }
                        if (Vector3.Cross(v[b] - v[a], v[c] - v[a]).sqrMagnitude < 1e-14f) degen++;
                        if (triBad && sample == default) sample = v[a];
                    }
                    if (zero + nan + opp == 0) continue;
                    totalBad += zero + nan + opp;
                    string mat = mats != null && s < mats.Length && mats[s] != null ? mats[s].name : "?";
                    line.Append($"\n    sub {s} [{mat}]: zero normals {zero}, NaN normals {nan}, opposing-normal triangles {opp}, degenerate tris {degen}, e.g. at local {sample.x:0.000},{sample.y:0.000},{sample.z:0.000}");
                }
                if (totalBad == 0) return;
                badMeshes++;
                sb.Append($"{m.name} (v{v.Length}) e.g. {Path(r)}").Append(line).AppendLine();
            }
            foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include))
                Check(mf.sharedMesh, mf.GetComponent<Renderer>());
            foreach (var sm in Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include))
                Check(sm.sharedMesh, sm);
            return $"[Normals] meshes {meshes}, with bad normals {badMeshes}\n" + sb;
        }

        private static string Path(Component c)
        {
            if (c == null) return "-";
            var t = c.transform;
            string s = t.name;
            for (var p = t.parent; p != null; p = p.parent) s = p.name + "/" + s;
            return s;
        }
    }
}
