using UnityEngine;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// Chess table built at runtime: game table (<see cref="TableWidth"/> wide along X, <see cref="TableTop"/> deep from
    /// seat to seat; widened 2026-11-25 for more room round the board), chess board (raised plate + inlaid slab with coordinates), a digital
    /// chess clock with two plungers on the +X side, two stools (seat A at -Z, seat B at +Z). Local space: origin on
    /// the floor under the board centre. The board itself sits on a pivot that turns 180 degrees when the colours swap
    /// (white always plays from its own side, so a1 is always on white's left).
    /// </summary>
    public sealed class ChessTableModel
    {
        public const float TopY = 0.64f, TableTop = 0.96f, TableWidth = 1.2f;
        public const float BoardTop = TopY + 0.028f;
        public const float StoolZ = 0.75f;
        public const float Square = BoardArt.SquareSize;

        public Transform Root, BoardPivot, Pieces, ButtonA, ButtonB;
        public Texture2D ClockTex;
        public Material ClockMat;
        public TableKit Kit;

        /// <summary>Square centre on the board pivot (a1 at -x / -z), on the board surface.</summary>
        public static Vector3 SquareLocal(int sq) => new Vector3((ChessBoard.File(sq) - 3.5f) * Square, BoardTop, (ChessBoard.Rank(sq) - 3.5f) * Square);

        /// <summary>Spot for the n-th piece captured by the player of seat 0 / 1, on the table next to the board (-X side).</summary>
        public static Vector3 CapturedLocal(int seat, int n)
        {
            int col = n / 8, row = n % 8;
            // Rows a bit tighter than the piece pitch, so a full column stays clear of the table's rounded corner.
            float z = (0.07f + row * 0.042f) * (seat == 0 ? -1f : 1f);
            return new Vector3(-0.47f - col * 0.048f, TopY, z);
        }

        public static ChessTableModel Build(Transform root, Material template, int style)
        {
            var m = new ChessTableModel { Root = root, Kit = new TableKit(template, root.gameObject.layer, style) };
            var k = m.Kit;
            k.Table(root, TableWidth, TableTop, TopY);
            k.Stool(root, new Vector3(0f, 0f, -StoolZ));
            k.Stool(root, new Vector3(0f, 0f, StoolZ));

            m.BoardPivot = new GameObject("BoardPivot") { layer = k.Layer }.transform;
            m.BoardPivot.SetParent(root, false);
            float bs = BoardArt.BoardSize;
            var frameMat = k.Mat(style == 1 ? new Color(0.2f, 0.12f, 0.08f) : new Color(0.25f, 0.14f, 0.08f), 0f, 0.6f, BoardArt.GrainTable);
            k.Part("BoardPlate", m.BoardPivot, BoardMeshes.RoundedBox(new Vector3(bs + 0.012f, 0.014f, bs + 0.012f), 0.005f, 3), frameMat, new Vector3(0f, TopY + 0.007f, 0f));
            var boardMat = k.Mat(Color.white, 0f, 0.72f, style == 1 ? BoardArt.BoardAlt : BoardArt.Board);
            k.Part("Board", m.BoardPivot, BoardMeshes.Slab("chessboard", bs, bs, 0.016f), boardMat, new Vector3(0f, TopY + 0.012f, 0f));
            m.Pieces = new GameObject("Pieces") { layer = k.Layer }.transform;
            m.Pieces.SetParent(root, false);
            m.BuildClock();
            return m;
        }

        void BuildClock()
        {
            var k = Kit;
            var c = new GameObject("ChessClock") { layer = k.Layer }.transform;
            c.SetParent(Root, false);
            c.localPosition = new Vector3(0.5f, TopY, 0f);
            var body = k.Mat(Kit.Style == 1 ? new Color(0.09f, 0.09f, 0.1f) : new Color(0.33f, 0.2f, 0.12f), 0.1f, 0.6f, Kit.Style == 1 ? null : BoardArt.GrainTable);
            // Wedge case seen from its end: d > 0 = towards the board (-X), the display slopes up away from the board.
            var profile = new System.Collections.Generic.List<Vector2>
            {
                new(-0.04f, 0f), new(0.04f, 0f), new(0.04f, 0.045f), new(-0.015f, 0.088f), new(-0.04f, 0.085f),
            };
            k.Part("ClockBody", c, BoardMeshes.Extrude("chessclock", profile, _ => 0.1f, 0.006f, 3, 8f), body, Vector3.zero, Quaternion.Euler(0f, -90f, 0f));
            Vector3 n = new Vector3(-0.616f, 0.788f, 0f);
            Vector3 faceCentre = new Vector3(-0.0125f, 0.0665f, 0f);
            ClockTex = new Texture2D(256, 64, TextureFormat.RGBA32, false) { name = "ChessClockLcd", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            ClockMat = k.Glow(new Color(0.75f, 0.95f, 0.8f), ClockTex, 1.1f);
            ClockMat.mainTexture = ClockTex;
            ClockMat.color = new Color(0.25f, 0.3f, 0.27f);
            // LCD (u runs from seat B's half to seat A's half), bezel behind it.
            var look = Quaternion.LookRotation(-n, Vector3.up);
            k.Part("ClockBezel", c, BoardMeshes.RoundedBox(new Vector3(0.17f, 0.052f, 0.004f), 0.0015f, 2), k.Dark, faceCentre + n * 0.0005f, look);
            k.Part("ClockLcd", c, Quad(0.16f, 0.042f), ClockMat, faceCentre + n * 0.0036f, look, false);
            var cap = BoardMeshes.Lathe("clockbtn", new System.Collections.Generic.List<Vector2>
            {
                new(0f, 0f), new(0.011f, 0f), new(0.011f, 0f), new(0.011f, 0.012f), new(0.009f, 0.016f), new(0f, 0.017f),
            }, 18, 1f);
            ButtonA = k.Part("ButtonA", c, cap, k.Brass, new Vector3(0.027f, 0.0855f, -0.06f)).transform;
            ButtonB = k.Part("ButtonB", c, cap, k.Brass, new Vector3(0.027f, 0.0855f, 0.06f)).transform;
        }

        /// <summary>Quad facing +Z (w along X, h along Y), UVs 0..1.</summary>
        public static Mesh Quad(float w, float h)
        {
            string key = $"quad{w:0.000}x{h:0.000}";
            if (BoardMeshes.TryGet(key, out var cached)) return cached;
            var m = new Mesh { name = key };
            m.vertices = new[] { new Vector3(-w / 2, -h / 2, 0), new Vector3(-w / 2, h / 2, 0), new Vector3(w / 2, h / 2, 0), new Vector3(w / 2, -h / 2, 0) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            m.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            m.RecalculateTangents();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>
        /// Yaw of a piece on the board pivot: white faces +Z, black -Z; knights turn their profile to the players,
        /// looking towards the centre files (or the way they last jumped, <paramref name="towardsPlusX"/>).
        /// </summary>
        public static float PieceYaw(int piece, bool towardsPlusX)
        {
            bool white = piece > 0;
            float yaw = white ? 0f : 180f;
            if ((piece < 0 ? -piece : piece) == ChessBoard.Knight) yaw = towardsPlusX ? 78f : -78f;
            return yaw;
        }

        /// <summary>New piece object (one renderer, wood + detail material) standing at the origin of <paramref name="parent"/>.</summary>
        public GameObject Piece(int piece, Transform parent)
        {
            int type = piece < 0 ? -piece : piece;
            bool white = piece > 0;
            var g = Kit.Part("Piece", parent, ChessPieceModels.Get(type), white ? Kit.PieceLight : Kit.PieceDark, Vector3.zero,
                Quaternion.Euler(0f, PieceYaw(piece, true), 0f));
            if (ChessPieceModels.Get(type).subMeshCount > 1)
                g.GetComponent<MeshRenderer>().sharedMaterials = new[] { white ? Kit.PieceLight : Kit.PieceDark, white ? Kit.PieceLightDetail : Kit.PieceDarkDetail };
            return g;
        }
    }
}
