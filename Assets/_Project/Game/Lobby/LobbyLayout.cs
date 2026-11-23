using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Floor plan of the lobby room - the one place with positions shared by the scene builder
    /// (MultiplayerBuilder.Lobby*.cs) and the runtime objects built in the game (arcades, fork, sockets, bricks,
    /// flashlight box, boombox, mini-game spots). Room: <see cref="W"/> x <see cref="D"/> m (the original size),
    /// centred on the origin, floor at y 0, inner wall faces at x = +-HalfW and z = +-HalfD. North = +Z.
    /// Layout (23.11): lounge with the fireplace + TV on the north wall (north-west), games in the north-east
    /// (two SNAKE cabinets, chess spot, tic-tac-toe board, darts), start lever / cork board / vending on the east wall,
    /// mirror, flashlight box, aquarium, record player and exit on the south wall, bricks / bookshelf / globe west.
    /// </summary>
    public static class LobbyLayout
    {
        public const float W = 16f, D = 12f, H = 3.6f;
        public const float HalfW = W * 0.5f, HalfD = D * 0.5f;

        // ---- lounge
        /// <summary>Coffee table centre (floor); it stands 0.58 m in front of the north-facing sofa (closer and the seated poses' hands / legs hit it).</summary>
        public static readonly Vector3 CoffeeTable = new(-4.0f, 0f, 3.03f);
        /// <summary>Sofa facing north (towards the fireplace) and sofa facing east (towards the table): centre, yaw.</summary>
        public static readonly (Vector3 pos, float yaw) SofaFront = (new Vector3(-4.0f, 0f, 1.55f), 180f);
        public static readonly (Vector3 pos, float yaw) SofaSide = (new Vector3(-5.85f, 0f, 3.6f), -90f);
        public static readonly (Vector3 pos, float yaw)[] Poufs =
        {
            (new Vector3(-2.2f, 0f, 2.75f), -90f), (new Vector3(-2.25f, 0f, 3.9f), -90f),
        };
        public static readonly Vector3 Fireplace = new(-4.0f, 0f, HalfD);
        /// <summary>Snacks / fork were placed for the old table centre (-1.5, 0.8); this moves them onto the new one.</summary>
        public static Vector3 TableShift => CoffeeTable - new Vector3(-1.5f, 0f, 0.8f);

        // ---- arcade cabinets (floor centre, yaw; the front faces local -Z), side by side on the north wall
        public static readonly (Vector3 pos, float yaw)[] Arcades =
        {
            (new Vector3(4.3f, 0f, HalfD - 0.5f), 0f),
            (new Vector3(5.3f, 0f, HalfD - 0.5f), 0f),
        };

        // ---- mini games (agent "minigames" finds the anchors by name)
        /// <summary>MiniGameSpot_Chess: floor centre of a 0.8 x 0.8 m table, players at local +-X (0.75 m).</summary>
        public static readonly Vector3 ChessSpot = new(2.9f, 0f, 2.2f);
        /// <summary>MiniGameSpot_TicTacToe: centre of a ~0.7 m wall board on the north wall (forward = into the room).</summary>
        public static readonly Vector3 TicTacToeSpot = new(6.85f, 1.45f, HalfD - 0.01f);

        // ---- east wall
        public static readonly Vector3 Lever = new(HalfW - 0.02f, 0f, 0.5f);
        public static readonly Vector3 CorkBoard = new(HalfW - 0.04f, 1.85f, -1.55f);
        public static readonly Vector3 Vending = new(HalfW - 0.45f, 0f, -3.2f);
        public static readonly Vector3 TrashBin = new(HalfW - 0.4f, 0f, -4.25f);
        /// <summary>Dartboard centre on the east wall; the throw line is 2.37 m in front of it.</summary>
        public static readonly Vector3 Dartboard = new(HalfW - 0.02f, 1.73f, 3.0f);

        // ---- south wall
        public const float ExitDoorX = 5.0f;
        public static readonly Vector3 Mirror = new(1.6f, 1.45f, -HalfD);
        public static readonly Vector3 FlashTable = new(-0.6f, 0f, -HalfD + 0.27f);
        public static readonly Vector3 Aquarium = new(-2.75f, 0f, -HalfD + 0.3f);
        public static readonly Vector3 RecordPlayer = new(-4.7f, 0f, -HalfD + 0.28f);
        public static readonly Vector3 CoatRack = new(6.25f, 0f, -HalfD + 0.45f);

        // ---- south-west corner: crates with the boombox on the small one (no plant next to it)
        public static readonly Vector3 Crates = new(-7.35f, 0f, -5.35f);
        public static readonly Vector3 Boombox = new(-6.5f, 0.7f, -5.5f);
        public const float BoomboxYaw = 38f;

        // ---- west side: bookshelf, globe, toy bricks
        public static readonly Vector3 Bookshelf = new(-HalfW + 0.25f, 0f, -2.6f);
        public static readonly Vector3 Globe = new(-7.2f, 0f, 0.35f);
        /// <summary>Brick baseplate corner (x0, z0); 2.7 x 2.4 m.</summary>
        public static readonly Vector3 BrickOrigin = new(-6.55f, 0f, -4.45f);
        public static readonly Vector3 BrickTub = new(-7.15f, 0f, -0.95f);
        public const float BrickTubYaw = 12f;

        /// <summary>Corner plants (pot centre).</summary>
        public static readonly Vector3[] Plants = { new(-7.45f, 0f, 5.45f), new(7.45f, 0f, -5.45f), new(-2.05f, 0f, 5.55f) };

        // ---- wall sockets (plate centre, facing into the room) - the fork easter egg works with every one
        public static readonly Vector3[] Outlets =
        {
            new(-1.4f, 0.22f, HalfD - 0.019f), new(-HalfW + 0.019f, 0.22f, 1.3f), new(HalfW - 0.019f, 0.22f, 1.8f), new(-1.7f, 0.22f, -HalfD + 0.019f),
        };
        public static readonly Vector3[] OutletNormals = { Vector3.back, Vector3.right, Vector3.left, Vector3.forward };

        /// <summary>
        /// Player spawns: a half circle (radius 2.8 m, 1.25 m apart) south of <see cref="SpawnFocus"/> in the open middle
        /// of the room, everybody facing the focus - nobody stands in front of anybody's camera. Ordered so the first
        /// players to arrive are spread out (spawn 00, 01, ... are used in this order).
        /// </summary>
        public static readonly Vector2 SpawnFocus = new(1.5f, -0.6f);
        public const float SpawnRadius = 2.8f;
        private static readonly int[] SpawnOrder = { 2, 5, 0, 7, 3, 4, 1, 6 };
        public static Vector2[] Spawns
        {
            get
            {
                var r = new Vector2[SpawnOrder.Length];
                for (int i = 0; i < r.Length; i++)
                {
                    float a = (180f + SpawnOrder[i] * 180f / 7f) * Mathf.Deg2Rad;
                    r[i] = SpawnFocus + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * SpawnRadius;
                }
                return r;
            }
        }
    }
}
