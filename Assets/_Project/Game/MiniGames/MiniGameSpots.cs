using UnityEngine;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// Where the board-game tables stand in the lobby. The lobby builder leaves empty anchors named
    /// <see cref="ChessName"/> / <see cref="TttName"/> (position + yaw; the seats are along the anchor's local Z).
    /// FALLBACK: without an anchor (lobby not rebuilt yet) the tables go to fixed free spots in the east half of the
    /// current 16 x 12 m room, between the sofa rug and the lever / vending machine.
    /// </summary>
    public static class MiniGameSpots
    {
        public const string ChessName = "MiniGameSpot_Chess", TttName = "MiniGameSpot_TicTacToe";

        /// <summary>FALLBACK placement (no anchor in the scene): position on the floor and yaw.</summary>
        public static (Vector3 pos, float yaw) Fallback(string name) => name == ChessName
            ? (new Vector3(3.85f, 0f, 1.25f), 0f)
            : (new Vector3(3.85f, 0f, -1.75f), 0f);

        /// <summary>Anchor by name in the loaded scenes, else the fallback (<paramref name="fallback"/> = true).</summary>
        public static void Resolve(string name, out Vector3 pos, out Quaternion rot, out bool fallback)
        {
            var go = GameObject.Find(name);
            if (go != null)
            {
                pos = go.transform.position;
                rot = Quaternion.Euler(0f, go.transform.eulerAngles.y, 0f);
                fallback = false;
                return;
            }
            var f = Fallback(name);
            pos = f.pos;
            rot = Quaternion.Euler(0f, f.yaw, 0f);
            fallback = true;
        }
    }
}
