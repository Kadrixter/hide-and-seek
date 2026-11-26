using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Floor plan of the lobby room - the one place with positions shared by the scene builder
    /// (MultiplayerBuilder.Lobby*.cs) and the runtime objects built in the game (arcades, fork, sockets, bricks,
    /// flashlight box, boombox, mini-game spots). Room: <see cref="W"/> x <see cref="D"/> m (the original size),
    /// centred on the origin, floor at y 0, inner wall faces at x = +-HalfW and z = +-HalfD. North = +Z.
    /// Layout (23.11): lounge with the fireplace + TV on the north wall (north-west), games in the north-east
    /// (two SNAKE cabinets, chess spot, tic-tac-toe board, darts), the pool table in the north-middle, start lever / cork board / vending on the east wall,
    /// mirror, flashlight box, aquarium, record player and exit on the south wall, bricks / bookshelf / globe west.
    /// </summary>
    public static class LobbyLayout
    {
        /// <summary>
        /// Look of the lobby room. Garage (26.11): bare concrete floor with oil stains, painted block walls, steel
        /// beams, shop lights, a half-open sectional garage door to the drift track, greasy torn sofas
        /// (MultiplayerBuilder.LobbyGarage*.cs). Classic: the old lounge (wallpaper, wainscoting, wood floor, the
        /// round arch to the track) - still fully in the code, switch back here and rebuild the scenes.
        /// </summary>
        public static readonly LobbyStyle Style = LobbyStyle.Garage;
        public static bool Garage => Style == LobbyStyle.Garage;

        public const float W = 16f, D = 12f, H = 3.6f;
        public const float HalfW = W * 0.5f, HalfD = D * 0.5f;

        // ---- lounge
        /// <summary>Coffee table centre (floor); it stands 0.58 m in front of the north-facing sofa (closer and the seated poses' hands / legs hit it).</summary>
        public static readonly Vector3 CoffeeTable = new(-4.0f, 0f, 3.03f);
        /// <summary>Sofa facing north (towards the fireplace) and sofa facing east (towards the table): centre, yaw.</summary>
        public static readonly (Vector3 pos, float yaw) SofaFront = (new Vector3(-4.0f, 0f, 1.3f), 180f); // 0.25 m further from the table (players got stuck in the gap)
        public static readonly (Vector3 pos, float yaw) SofaSide = (new Vector3(-6.1f, 0f, 3.6f), -90f);
        public static readonly (Vector3 pos, float yaw)[] Poufs =
        {
            (new Vector3(-2.2f, 0f, 2.75f), -90f), (new Vector3(-2.25f, 0f, 3.9f), -90f),
        };
        public static readonly Vector3 Fireplace = new(-4.0f, 0f, HalfD);
        /// <summary>
        /// Wall TV over the fireplace: 2.2 x the old 1.56 x 0.92 m set (screen 3.26 x 1.85 m), centre height. Bottom edge
        /// 1.42 m (above the mantel candles), top 3.44 m (under the 11 cm crown moulding); the sconces stand outside it
        /// (<see cref="TvSconceDx"/>), the wall clock moved east to make room.
        /// </summary>
        public const float TvScale = 2.2f, TvCenterY = 2.43f, TvSconceDx = 1.95f;
        public static readonly Vector3 WallClock = new(-1.45f, 2.62f, HalfD - 0.04f);
        /// <summary>Snacks / fork were placed for the old table centre (-1.5, 0.8); this moves them onto the new one.</summary>
        public static Vector3 TableShift => CoffeeTable - new Vector3(-1.5f, 0f, 0.8f);

        // ---- arcade cabinets (floor centre, yaw; the front faces local -Z), side by side on the north wall
        public static readonly (Vector3 pos, float yaw)[] Arcades =
        {
            (new Vector3(4.3f, 0f, HalfD - 0.5f), 0f),
            (new Vector3(5.3f, 0f, HalfD - 0.5f), 0f),
        };
        /// <summary>Third cabinet, west of the two SNAKE ones: the dot link puzzle (DotLinkArcade, MatchManager.ArcLink).</summary>
        public static readonly (Vector3 pos, float yaw) LinkArcade = (new Vector3(3.3f, 0f, HalfD - 0.5f), 0f);

        // ---- mini games (agent "minigames" finds the anchors by name)
        /// <summary>MiniGameSpot_Chess: floor centre of the 1.2 x 0.96 m chess / checkers table (pendant lamp above it), players at local +-X (0.75 m).</summary>
        public static readonly Vector3 ChessSpot = new(2.9f, 0f, 2.2f);
        /// <summary>MiniGameSpot_TicTacToe: centre of a ~0.7 m wall board on the north wall (forward = into the room).</summary>
        public static readonly Vector3 TicTacToeSpot = new(6.85f, 1.45f, HalfD - 0.01f);
        /// <summary>
        /// MiniGameSpot_Pool: floor point under the middle of the cloth of the 7 ft pool table (outside 2.30 x 1.31 m),
        /// north-middle of the room between the lounge and the chess spot. The long axis runs along world Z: the root
        /// yaw <see cref="PoolYaw"/> = 90 turns table +X (foot end, rack) to world -Z (south) and table +Z to world +X,
        /// so the head end (kitchen, break) points north; player 1 waits on the west long side, player 2 on the east.
        /// The table's lamp hangs from the ceiling over it (canopies at z 0.58 / 2.02, x -0.1), 1.3 m clear of the
        /// ceiling light at (1.2; 2.6).
        /// </summary>
        public static readonly Vector3 PoolSpot = new(-0.1f, 0f, 1.3f);
        public const float PoolYaw = 90f;

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
        /// <summary>Claude (orange blocky mascot, E = the survey) in the corner where two crates stood: floor centre, yaw (front = local +Z).</summary>
        public static readonly (Vector3 pos, float yaw) Claude = (new Vector3(-7.35f, 0f, -4.95f), 40f);
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
            new(-1.4f, 0.22f, HalfD - 0.019f), new(-HalfW + 0.019f, 0.22f, Garage ? 0.6f : 1.3f), new(HalfW - 0.019f, 0.22f, 1.8f), new(-1.7f, 0.22f, -HalfD + 0.019f),
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

    public enum LobbyStyle { Classic, Garage }
}
