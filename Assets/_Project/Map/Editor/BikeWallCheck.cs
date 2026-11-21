using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HideAndSeek.Map.Editor
{
    /// <summary>Batch check: lists bicycles / racks whose body overlaps walls or other solid map geometry.</summary>
    public static class BikeWallCheck
    {
        public static void Run()
        {
            EditorSceneManager.OpenScene(ParkingGarageBuilderMenu.ScenePath, OpenSceneMode.Single);
            Physics.SyncTransforms();
            var sb = new StringBuilder();
            int bad = 0, total = 0;
            foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
            {
                string n = mf.gameObject.name;
                if (!n.StartsWith("Bicycle_") && !n.StartsWith("BikeRack_")) continue;
                total++;
                var b = mf.sharedMesh.bounds;
                Vector3 half = Vector3.Scale(b.extents, mf.transform.lossyScale) - new Vector3(0.02f, 0.02f, 0.02f);
                Vector3 centre = mf.transform.TransformPoint(b.center);
                var hits = Physics.OverlapBox(centre, half, mf.transform.rotation, ~0, QueryTriggerInteraction.Ignore);
                foreach (var h in hits)
                {
                    if (h.transform.IsChildOf(mf.transform) || h.transform == mf.transform) continue;
                    string hn = h.gameObject.name;
                    if (hn.StartsWith("Bicycle_") || hn.StartsWith("BikeRack_")) continue;
                    bad++;
                    sb.AppendLine($"[BikeCheck] {n} at {mf.transform.position} yaw {mf.transform.eulerAngles.y:0} overlaps '{hn}' (parent {h.transform.parent?.name}) bounds {h.bounds.center} {h.bounds.size}");
                }
            }
            Debug.Log($"[BikeCheck] bikes/racks checked: {total}, overlaps: {bad}\n{sb}");
            EditorApplication.Exit(0);
        }
    }
}
