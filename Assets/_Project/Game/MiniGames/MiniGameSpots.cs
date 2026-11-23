using UnityEngine;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// Where the board games stand in the lobby. The lobby builder (LobbyLayout) leaves empty anchors:
    /// <see cref="ChessName"/> = centre of the chess table on the floor, the two players along the anchor's local X
    /// (0.75 m out, a round rug under it); <see cref="TttName"/> = centre of the chalkboard on the wall at 1.45 m,
    /// forward pointing into the room. FALLBACK (anchors not in this branch's lobby yet): the same spots as the new
    /// lobby layout uses - chess at (2.9; 0; 2.2), the board on the north wall at (6.85; 1.45; 5.99) facing south.
    /// </summary>
    public static class MiniGameSpots
    {
        public const string ChessName = "MiniGameSpot_Chess", TttName = "MiniGameSpot_TicTacToe";

        /// <summary>Height of the chalkboard centre above the floor (the anchor sits there).</summary>
        public const float BoardHeight = 1.45f;

        /// <summary>FALLBACK placement (no anchor in the scene): anchor position and yaw.</summary>
        public static (Vector3 pos, float yaw) Fallback(string name) => name == ChessName
            ? (new Vector3(2.9f, 0f, 2.2f), 0f)
            : (new Vector3(6.85f, BoardHeight, 5.99f), 180f);

        /// <summary>
        /// Pose of a game's root: chess = table centre on the floor with the seats along the root's Z (anchor X,
        /// so the root is the anchor turned by 90 degrees); tic-tac-toe = the board centre, +Z into the room.
        /// </summary>
        public static void Resolve(string name, out Vector3 pos, out Quaternion rot, out bool fallback)
        {
            var go = GameObject.Find(name);
            float yaw;
            if (go != null)
            {
                pos = go.transform.position;
                yaw = go.transform.eulerAngles.y;
                fallback = false;
            }
            else
            {
                var f = Fallback(name);
                pos = f.pos;
                yaw = f.yaw;
                fallback = true;
            }
            if (name == ChessName) yaw += 90f;
            rot = Quaternion.Euler(0f, yaw, 0f);
        }
    }
}
