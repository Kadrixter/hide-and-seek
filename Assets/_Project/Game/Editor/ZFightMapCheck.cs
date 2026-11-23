using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HideAndSeek.Game.Editor
{
    /// <summary>
    /// Batch z-fighting report (<see cref="ZFightScan"/>, 4 mm) for the game scene (garage), the menu and the lobby:
    /// -executeMethod HideAndSeek.Game.Editor.ZFightMapCheck.RunAll [-zfightTol 0.004]. Lines start with [ZFightMap].
    /// </summary>
    public static class ZFightMapCheck
    {
        private static readonly string[] Scenes =
        {
            "Assets/_Project/Game/Scenes/Game_ParkingGarage.unity",
            "Assets/_Project/Game/Scenes/Menu.unity",
            "Assets/_Project/Game/Scenes/Lobby.unity",
        };

        private static readonly string[] ChunkPrefixes =
            { "Slab", "Walls_", "Columns_", "FloorMarkings_", "Signage_", "FloorFixtures_", "CeilingServices_", "Cages_", "Roof", "Ramps_", "Steps_" };

        public static void RunAll()
        {
            float tol = 0.004f;
            var args = System.Environment.GetCommandLineArgs();
            int ti = System.Array.IndexOf(args, "-zfightTol");
            if (ti >= 0 && ti + 1 < args.Length) float.TryParse(args[ti + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out tol);
            foreach (var s in Scenes)
            {
                if (!System.IO.File.Exists(s)) continue;
                EditorSceneManager.OpenScene(s, OpenSceneMode.Single);
                var rends = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude).Cast<Renderer>().ToList();
                var t0 = System.DateTime.Now;
                var pairs = ZFightScan.Find(rends, tol, 2e-4f, r => System.Array.Exists(ChunkPrefixes, c => r.name.StartsWith(c)));
                var sb = new StringBuilder();
                sb.AppendLine($"[ZFightMap] {s}: {rends.Count} renderers, {pairs.Count} pairs within {tol * 1000f:0.0} mm ({(System.DateTime.Now - t0).TotalSeconds:0.0} s)");
                foreach (var p in pairs.Take(400)) sb.AppendLine("[ZFightMap]   " + ZFightScan.Describe(p));
                Debug.Log(sb.ToString());
            }
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
    }
}
