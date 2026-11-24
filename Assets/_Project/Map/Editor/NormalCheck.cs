using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// Batch check of the map meshes for vertex data the lighting cannot handle: normals that are zero, not finite
    /// or far from unit length (the Standard shader normalises them, a zero normal becomes NaN and the pixel turns into
    /// a NaN that the bloom blows up into a glowing dot with a black core), and non-finite positions.
    /// Run: -executeMethod HideAndSeek.Map.Editor.NormalCheck.Run ([NormalCheck] lines, exit code 0 = clean, 1 = bad).
    /// </summary>
    public static class NormalCheck
    {
        public static void Run()
        {
            EditorSceneManager.OpenScene(ParkingGarageBuilderMenu.ScenePath, OpenSceneMode.Single);
            int bad = Scan(Object.FindObjectsByType<MeshFilter>().Select(f => (f.sharedMesh, f.GetComponent<MeshRenderer>(), f.name)), out string report);
            Debug.Log(report);
            EditorApplication.Exit(bad == 0 ? 0 : 1);
        }

        /// <summary>Counts bad vertices of the given meshes (each mesh once), with a per mesh / material report.</summary>
        public static int Scan(IEnumerable<(Mesh mesh, MeshRenderer mr, string owner)> items, out string report)
        {
            var sb = new StringBuilder();
            var seen = new HashSet<Mesh>();
            int meshes = 0, badTotal = 0;
            foreach (var (mesh, mr, owner) in items)
            {
                if (mesh == null || !seen.Add(mesh) || !mesh.isReadable) continue;
                meshes++;
                var v = mesh.vertices;
                var n = mesh.normals;
                if (n == null || n.Length != v.Length) continue;
                var badV = new bool[v.Length];
                int count = 0;
                for (int i = 0; i < v.Length; i++)
                {
                    float m = n[i].magnitude;
                    bool b = float.IsNaN(m) || float.IsInfinity(m) || m < 0.5f || m > 1.5f ||
                             float.IsNaN(v[i].x) || float.IsNaN(v[i].y) || float.IsNaN(v[i].z);
                    if (b) { badV[i] = true; count++; }
                }
                if (count == 0) continue;
                badTotal += count;
                sb.AppendLine($"[NormalCheck] {mesh.name} (on {owner}): {count} of {v.Length} vertices bad");
                var mats = mr != null ? mr.sharedMaterials : null;
                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    var tris = mesh.GetTriangles(s);
                    var used = new HashSet<int>();
                    foreach (int t in tris) if (badV[t]) used.Add(t);
                    if (used.Count == 0) continue;
                    string mat = mats != null && s < mats.Length && mats[s] != null ? mats[s].name : "?";
                    var sample = used.Take(4).Select(i => $"{v[i].x:0.000};{v[i].y:0.000};{v[i].z:0.000} n=({n[i].x:0.00};{n[i].y:0.00};{n[i].z:0.00})");
                    sb.AppendLine($"[NormalCheck]   submesh {s} ({mat}): {used.Count} bad, e.g. {string.Join("  ", sample)}");
                }
            }
            report = $"[NormalCheck] meshes checked: {meshes}, bad vertices: {badTotal}\n{sb}";
            return badTotal;
        }
    }
}
