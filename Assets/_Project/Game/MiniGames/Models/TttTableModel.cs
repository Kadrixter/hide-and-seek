using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// Tic-tac-toe chalkboard on the wall: a school slate in a moulded wooden frame with screws, a chalk ledge
    /// underneath with a stick of chalk for each player and a felt eraser. The grid and the marks are chalk in a
    /// runtime texture. Local space: origin = centre of the board on the wall surface, +Z into the room, +Y up; the
    /// floor is <see cref="FloorY"/> below (local +X is on the left as seen from the room). Cell (row, col): row 0 = top
    /// row, col 0 = left as seen from the room.
    /// </summary>
    public sealed class TttTableModel
    {
        public const float FrameW = 1.02f, FrameH = 0.84f, SlateW = 0.9f, SlateH = 0.72f;
        public const float SlateZ = 0.022f, GridY = -0.03f, GridSize = 0.54f, Cell = GridSize / 3f;
        public const float FloorY = -MiniGameSpots.BoardHeight;
        public const float LedgeY = -FrameH * 0.5f - 0.01f;
        public const int TexW = 640, TexH = 512;

        public Transform Root, ChalkA, ChalkB, Eraser;
        public Texture2D SlateTex;
        public TableKit Kit;
        Color[] _clean;

        public static Vector3 CellLocal(int cell) => new Vector3((1 - cell % 3) * Cell, GridY + (1 - cell / 3) * Cell, SlateZ);

        /// <summary>Slate texture pixel of a local point on the board.</summary>
        // The slate quad is turned to face the room, so u runs towards -X.
        public static Vector2 ToPixel(Vector3 local) => new Vector2((0.5f - local.x / SlateW) * TexW, (local.y / SlateH + 0.5f) * TexH);

        /// <summary>Where the stick of chalk of a seat lies on the ledge (A on the left, B on the right).</summary>
        public static Vector3 ChalkRest(int seat) => new Vector3(seat == 0 ? 0.32f : -0.32f, LedgeY + 0.022f, 0.045f);

        /// <summary>Eraser on the ledge, the middle.</summary>
        public static readonly Vector3 EraserRest = new Vector3(-0.05f, LedgeY + 0.016f, 0.045f);

        public static TttTableModel Build(Transform root, Material template)
        {
            var m = new TttTableModel { Root = root, Kit = new TableKit(template, root.gameObject.layer, 0) };
            m.BuildBoard();
            return m;
        }

        void BuildBoard()
        {
            var k = Kit;
            var frame = k.Mat(new Color(0.55f, 0.38f, 0.22f), 0f, 0.45f, BoardArt.GrainTable);
            var back = k.Mat(new Color(0.18f, 0.12f, 0.08f), 0f, 0.3f);
            // Backing board, then the four mitred-looking frame rails proud of the slate.
            k.Part("Backing", Root, BoardMeshes.RoundedBox(new Vector3(FrameW - 0.02f, FrameH - 0.02f, 0.012f), 0.004f, 2), back, new Vector3(0f, 0f, 0.006f));
            float rail = (FrameW - SlateW) * 0.5f, railH = (FrameH - SlateH) * 0.5f;
            k.Part("RailTop", Root, BoardMeshes.RoundedBox(new Vector3(FrameW, railH, 0.034f), 0.008f, 3), frame, new Vector3(0f, (FrameH - railH) * 0.5f, 0.017f));
            k.Part("RailBottom", Root, BoardMeshes.RoundedBox(new Vector3(FrameW, railH, 0.034f), 0.008f, 3), frame, new Vector3(0f, -(FrameH - railH) * 0.5f, 0.017f));
            k.Part("RailLeft", Root, BoardMeshes.RoundedBox(new Vector3(rail, SlateH + 0.002f, 0.032f), 0.008f, 3), frame, new Vector3(-(FrameW - rail) * 0.5f, 0f, 0.016f));
            k.Part("RailRight", Root, BoardMeshes.RoundedBox(new Vector3(rail, SlateH + 0.002f, 0.032f), 0.008f, 3), frame, new Vector3((FrameW - rail) * 0.5f, 0f, 0.016f));
            var screw = BoardMeshes.Lathe("tttscrew", new List<Vector2> { new(0f, 0f), new(0.007f, 0f), new(0.0065f, 0.002f), new(0.004f, 0.0035f), new(0f, 0.004f) }, 12, 1f);
            foreach (var (x, y) in new[] { (-1, 1), (1, 1), (-1, -1), (1, -1) })
                k.Part("Screw", Root, screw, k.Brass, new Vector3(x * (FrameW - rail) * 0.5f, y * (FrameH - railH) * 0.5f, 0.034f), Quaternion.Euler(90f, 0f, 0f), false);

            SlateTex = new Texture2D(TexW, TexH, TextureFormat.RGBA32, true) { name = "TttSlateLive", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            ClearSlate();
            var slate = k.Mat(Color.white, 0f, 0.18f, SlateTex);
            // The slate: a quad facing the room on the backing board.
            k.Part("Slate", Root, ChessTableModel.Quad(SlateW, SlateH), slate, new Vector3(0f, 0f, SlateZ), Quaternion.Euler(0f, 180f, 0f), false);

            // Chalk ledge under the board, chalk sticks and the eraser on it.
            k.Part("Ledge", Root, BoardMeshes.RoundedBox(new Vector3(FrameW - 0.06f, 0.014f, 0.075f), 0.004f, 3), frame, new Vector3(0f, LedgeY, 0.0375f));
            k.Part("LedgeLip", Root, BoardMeshes.RoundedBox(new Vector3(FrameW - 0.06f, 0.022f, 0.01f), 0.003f, 2), frame, new Vector3(0f, LedgeY + 0.009f, 0.072f));
            var chalk = k.Mat(new Color(0.95f, 0.95f, 0.92f), 0f, 0.05f);
            var stick = BoardMeshes.Lathe("chalk", new List<Vector2> { new(0f, 0f), new(0.0065f, 0f), new(0.0065f, 0f), new(0.007f, 0.08f), new(0.0048f, 0.084f), new(0f, 0.085f) }, 12, 2f);
            ChalkA = k.Part("ChalkA", Root, stick, chalk, ChalkRest(0), ChalkRestRot).transform;
            ChalkB = k.Part("ChalkB", Root, stick, chalk, ChalkRest(1), ChalkRestRot).transform;
            var felt = k.Mat(new Color(0.2f, 0.22f, 0.32f), 0f, 0.05f, BoardArt.Felt);
            var wood = k.Mat(new Color(0.7f, 0.55f, 0.36f), 0f, 0.4f, BoardArt.GrainFine);
            Eraser = new GameObject("Eraser") { layer = k.Layer }.transform;
            Eraser.SetParent(Root, false);
            Eraser.localPosition = EraserRest;
            k.Part("EraserFelt", Eraser, BoardMeshes.RoundedBox(new Vector3(0.13f, 0.012f, 0.05f), 0.004f, 3), felt, new Vector3(0f, 0.006f, 0f));
            k.Part("EraserBack", Eraser, BoardMeshes.RoundedBox(new Vector3(0.134f, 0.028f, 0.052f), 0.008f, 3), wood, new Vector3(0f, 0.025f, 0f));
        }

        /// <summary>A stick of chalk lying along the ledge.</summary>
        public static readonly Quaternion ChalkRestRot = Quaternion.Euler(0f, 0f, 90f);

        /// <summary>Clean slate: wiped background plus the chalk grid.</summary>
        public void ClearSlate()
        {
            if (SlateTex == null) return;
            if (_clean == null)
            {
                SlateTex.SetPixels(BoardArt.SlateSized(TexW, TexH).GetPixels());
                float top = GridY + GridSize * 0.5f, bottom = GridY - GridSize * 0.5f, half = GridSize * 0.5f;
                for (int i = 1; i <= 2; i++)
                {
                    float x = -half + i * Cell, y = bottom + i * Cell;
                    ChalkLine(new Vector3(x, bottom, 0f), new Vector3(x, top, 0f), 1f, 31 * i);
                    ChalkLine(new Vector3(-half, y, 0f), new Vector3(half, y, 0f), 1f, 57 * i);
                }
                _clean = SlateTex.GetPixels();
            }
            else SlateTex.SetPixels(_clean);
            SlateTex.Apply(true);
        }

        public Color[] CleanPixels => _clean;

        /// <summary>Chalk stroke from a to b (board-local points), drawn up to fraction <paramref name="upTo"/>; no Apply.</summary>
        public void ChalkLine(Vector3 a, Vector3 b, float upTo, int seed, float width = 5.5f, float from = 0f)
        {
            Vector2 pa = ToPixel(a), pb = ToPixel(b);
            float len = Vector2.Distance(pa, pb);
            int steps = Mathf.Max(2, Mathf.CeilToInt(len * upTo));
            Vector2 d = len > 1e-4f ? (pb - pa) / len : Vector2.right, nrm = new Vector2(-d.y, d.x);
            int s0 = Mathf.FloorToInt(len * from);
            for (int s = s0; s <= steps; s++)
            {
                float t = len > 1e-4f ? s / len : 0f;
                Vector2 p = pa + d * Mathf.Min(s, len) + nrm * (Mathf.PerlinNoise(seed, t * 3f) - 0.5f) * 3f;
                Dab(p, width * (0.85f + 0.3f * Mathf.PerlinNoise(seed + 9f, t * 9f)), seed + s);
            }
        }

        public void Dab(Vector2 p, float r, int seed)
        {
            int x0 = Mathf.FloorToInt(p.x - r - 1), x1 = Mathf.CeilToInt(p.x + r + 1), y0 = Mathf.FloorToInt(p.y - r - 1), y1 = Mathf.CeilToInt(p.y + r + 1);
            for (int y = Mathf.Max(0, y0); y <= Mathf.Min(TexH - 1, y1); y++)
                for (int x = Mathf.Max(0, x0); x <= Mathf.Min(TexW - 1, x1); x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), p);
                    if (dist > r) continue;
                    // Grainy chalk: the slate texture breaks the stroke up.
                    float grain = Mathf.PerlinNoise(x * 0.9f + seed * 0.01f, y * 0.9f);
                    float a = Mathf.Clamp01((r - dist) / 1.2f) * Mathf.Clamp01(grain * 1.8f - 0.25f);
                    if (a <= 0f) continue;
                    var c = SlateTex.GetPixel(x, y);
                    SlateTex.SetPixel(x, y, Color.Lerp(c, new Color(0.93f, 0.93f, 0.9f), a * 0.85f));
                }
        }
    }
}
