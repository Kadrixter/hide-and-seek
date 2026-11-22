using UnityEngine;

namespace HideAndSeek.Game.Editor
{
    /// <summary>
    /// Lobby trash bin: only an empty "TrashBin" with its collider and <see cref="TrashBinProp"/> goes into the scene;
    /// the perforated bin, liner bag, crumpled papers (in it and missed on the floor) and a crushed can are built at
    /// runtime with procedural textures (Game/Fx/Trash).
    /// </summary>
    public static partial class MultiplayerBuilder
    {
        private static void TrashBin(Transform p, Vector3 pos)
        {
            var g = new GameObject("TrashBin");
            g.transform.SetParent(p, false);
            // Papers spill towards local +Z: into the room (west), away from the wall.
            g.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, -90f, 0f));
            var col = g.AddComponent<CapsuleCollider>();
            col.radius = 0.155f; // outer radius at the rim
            col.height = 0.39f;
            col.center = new Vector3(0f, 0.195f, 0f);
            var bin = g.AddComponent<TrashBinProp>();
            bin.seed = 11;
            bin.floorPapers = 4;
            bin.can = true;
            bin.probes = true;
        }
    }
}
