using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HideAndSeek.Game.Editor
{
    /// <summary>
    /// The lobby copy for dead / escaped players during a round (<see cref="DeadLobby"/>): the freshly built lobby scene
    /// saved once more with every root moved by <see cref="DeadLobby.Offset"/>, far away from the parking garage. A copy
    /// built in place (not the lobby moved at runtime) because the player build bakes static batches in world space - a
    /// static-batched lobby moved after loading would leave its picture behind. The copy's spawn points lose their tag,
    /// so nothing that looks for spawn points during a round (medkits, syringes, tests) ever picks one of them.
    /// </summary>
    public static partial class MultiplayerBuilder
    {
        public const string DeadLobbyScenePath = SceneDir + "/" + DeadLobby.SceneName + ".unity";

        /// <summary>Called at the end of BuildLobbyScene with the saved lobby still open: writes the shifted copy.</summary>
        private static void SaveDeadLobbyCopy(Scene lobby)
        {
            var roots = lobby.GetRootGameObjects();
            foreach (var r in roots) r.transform.position += DeadLobby.Offset;
            var untagged = new System.Collections.Generic.List<GameObject>();
            foreach (var r in roots)
                foreach (var t in r.GetComponentsInChildren<Transform>(true))
                    if (t.CompareTag(HideAndSeek.Core.GameLayers.SpawnPointTag)) { t.tag = "Untagged"; untagged.Add(t.gameObject); }
            EditorSceneManager.SaveScene(lobby, DeadLobbyScenePath, true);
            // Back as it was: the open scene stays the real lobby.
            foreach (var g in untagged) g.tag = HideAndSeek.Core.GameLayers.SpawnPointTag;
            foreach (var r in roots) r.transform.position -= DeadLobby.Offset;
            Debug.Log($"[MultiplayerBuilder] dead-lobby copy saved: {DeadLobbyScenePath} ({roots.Length} roots moved by {DeadLobby.Offset}, {untagged.Count} spawn points untagged)");
        }
    }
}
