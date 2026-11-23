using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// Tic-tac-toe table: a smaller game table with a framed school slate (grid and marks drawn in chalk into a
    /// runtime texture), a chalk ledge with a stick of chalk in front of each seat and a felt eraser. Style 1 is the
    /// alternative look (wooden board with carved grooves and wooden X / O tokens). Seat A at -Z, seat B at +Z.
    /// Cell (row, col): row 0 = the far row seen from seat A, col 0 = seat A's left.
    /// </summary>
    public sealed class TttTableModel
    {
        public const float TopY = 0.64f, TableTop = 0.92f, StoolZ = 0.68f;
        public const float SlateSize = 0.54f, GridSize = 0.46f, Cell = GridSize / 3f;
        public const float SlateTop = TopY + 0.02f + 0.006f;

        public Transform Root, ChalkA, ChalkB, Eraser, Tokens;
        public Texture2D SlateTex;
        public TableKit Kit;
        public int Style;
        public Material TokenX, TokenO;

        public static Vector3 CellLocal(int cell) => new Vector3((cell % 3 - 1) * Cell, SlateTop, (1 - cell / 3) * Cell);

        /// <summary>Slate texture pixel of a local point on the slate.</summary>
        public static Vector2 ToPixel(Vector3 local) =>
            new Vector2((local.x / SlateSize + 0.5f) * BoardArt.SlatePx, (local.z / SlateSize + 0.5f) * BoardArt.SlatePx);

        /// <summary>Where the stick of chalk of a seat lies (on its ledge, right-hand side of the player).</summary>
        public static Vector3 ChalkRest(int seat) => seat == 0 ? new Vector3(0.14f, TopY + 0.018f, -0.35f) : new Vector3(-0.14f, TopY + 0.018f, 0.35f);

        public static TttTableModel Build(Transform root, Material template, int style)
        {
            var m = new TttTableModel { Root = root, Style = style, Kit = new TableKit(template, root.gameObject.layer, 0) };
            var k = m.Kit;
            k.Table(root, TableTop, TopY);
            k.Stool(root, new Vector3(0f, 0f, -StoolZ));
            k.Stool(root, new Vector3(0f, 0f, StoolZ));
            if (style == 1) m.BuildWooden();
            else m.BuildSlate();
            return m;
        }

        void BuildSlate()
        {
            var k = Kit;
            var frame = k.Mat(new Color(0.62f, 0.45f, 0.28f), 0f, 0.45f, BoardArt.GrainTable);
            k.Part("SlateFrame", Root, BoardMeshes.RoundedBox(new Vector3(SlateSize + 0.06f, 0.022f, SlateSize + 0.06f), 0.007f, 3), frame, new Vector3(0f, TopY + 0.011f, 0f));
            SlateTex = new Texture2D(BoardArt.SlatePx, BoardArt.SlatePx, TextureFormat.RGBA32, true) { name = "TttSlateLive", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            ClearSlate();
            var slate = k.Mat(Color.white, 0f, 0.18f, SlateTex);
            k.Part("Slate", Root, BoardMeshes.Slab("tttslate", SlateSize, SlateSize, 0.008f), slate, new Vector3(0f, SlateTop - 0.008f, 0f));
            // Chalk ledges with a stick of chalk each, eraser on the side.
            var chalk = k.Mat(new Color(0.95f, 0.95f, 0.92f), 0f, 0.05f);
            var stick = BoardMeshes.Lathe("chalk", new List<Vector2> { new(0f, 0f), new(0.0055f, 0f), new(0.0055f, 0f), new(0.006f, 0.07f), new(0.004f, 0.074f), new(0f, 0.075f) }, 12, 2f);
            foreach (int seat in new[] { 0, 1 })
            {
                float z = seat == 0 ? -0.35f : 0.35f;
                k.Part("Ledge", Root, BoardMeshes.RoundedBox(new Vector3(0.42f, 0.012f, 0.04f), 0.004f, 3), frame, new Vector3(0f, TopY + 0.006f, z));
                k.Part("LedgeLip", Root, BoardMeshes.RoundedBox(new Vector3(0.42f, 0.012f, 0.008f), 0.003f, 2), frame, new Vector3(0f, TopY + 0.014f, z + (seat == 0 ? -0.018f : 0.018f)));
                var c = k.Part(seat == 0 ? "ChalkA" : "ChalkB", Root, stick, chalk, ChalkRest(seat), Quaternion.Euler(0f, 0f, 90f) * Quaternion.Euler(0f, 0f, 0f)).transform;
                if (seat == 0) ChalkA = c; else ChalkB = c;
            }
            var felt = k.Mat(new Color(0.2f, 0.22f, 0.32f), 0f, 0.05f, BoardArt.Felt);
            var wood = k.Mat(new Color(0.7f, 0.55f, 0.36f), 0f, 0.4f, BoardArt.GrainFine);
            Eraser = new GameObject("Eraser") { layer = k.Layer }.transform;
            Eraser.SetParent(Root, false);
            Eraser.localPosition = new Vector3(0.375f, TopY, 0.02f);
            Eraser.localRotation = Quaternion.Euler(0f, 78f, 0f);
            k.Part("EraserFelt", Eraser, BoardMeshes.RoundedBox(new Vector3(0.12f, 0.012f, 0.05f), 0.004f, 3), felt, new Vector3(0f, 0.006f, 0f));
            k.Part("EraserBack", Eraser, BoardMeshes.RoundedBox(new Vector3(0.124f, 0.026f, 0.052f), 0.008f, 3), wood, new Vector3(0f, 0.024f, 0f));
        }

        /// <summary>Clean slate: wiped background plus the chalk grid.</summary>
        public void ClearSlate()
        {
            if (SlateTex == null) return;
            SlateTex.SetPixels(BoardArt.Slate.GetPixels());
            for (int i = 1; i <= 2; i++)
            {
                float o = -GridSize / 2f + i * Cell;
                ChalkLine(new Vector3(o, 0f, -GridSize / 2f), new Vector3(o, 0f, GridSize / 2f), 1f, 31 * i);
                ChalkLine(new Vector3(-GridSize / 2f, 0f, o), new Vector3(GridSize / 2f, 0f, o), 1f, 57 * i);
            }
            SlateTex.Apply(true);
        }

        /// <summary>Chalk stroke from a to b (local slate points), drawn up to fraction <paramref name="upTo"/>; no Apply.</summary>
        public void ChalkLine(Vector3 a, Vector3 b, float upTo, int seed, float width = 4.2f, float from = 0f)
        {
            Vector2 pa = ToPixel(a), pb = ToPixel(b);
            float len = Vector2.Distance(pa, pb);
            int steps = Mathf.Max(2, Mathf.CeilToInt(len * upTo));
            Vector2 d = (pb - pa).normalized, nrm = new Vector2(-d.y, d.x);
            int s0 = Mathf.FloorToInt(len * from);
            for (int s = s0; s <= steps; s++)
            {
                float t = s / len;
                // Slight wobble of a hand-drawn line.
                Vector2 p = pa + d * s + nrm * (Mathf.PerlinNoise(seed, t * 3f) - 0.5f) * 3f;
                Dab(p, width * (0.85f + 0.3f * Mathf.PerlinNoise(seed + 9f, t * 9f)), seed + s);
            }
        }

        public void Dab(Vector2 p, float r, int seed)
        {
            int n = BoardArt.SlatePx;
            int x0 = Mathf.FloorToInt(p.x - r - 1), x1 = Mathf.CeilToInt(p.x + r + 1), y0 = Mathf.FloorToInt(p.y - r - 1), y1 = Mathf.CeilToInt(p.y + r + 1);
            for (int y = Mathf.Max(0, y0); y <= Mathf.Min(n - 1, y1); y++)
                for (int x = Mathf.Max(0, x0); x <= Mathf.Min(n - 1, x1); x++)
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

        // ================================================================ wooden variant (preview / style 1)

        void BuildWooden()
        {
            var k = Kit;
            var board = k.Mat(new Color(0.78f, 0.6f, 0.38f), 0f, 0.55f, BoardArt.GrainTable);
            var groove = k.Mat(new Color(0.2f, 0.12f, 0.07f), 0f, 0.3f);
            k.Part("WoodBoard", Root, BoardMeshes.RoundedBox(new Vector3(SlateSize + 0.06f, 0.03f, SlateSize + 0.06f), 0.01f, 4), board, new Vector3(0f, TopY + 0.015f, 0f));
            for (int i = 1; i <= 2; i++)
            {
                float o = -GridSize / 2f + i * Cell;
                k.Part("Groove", Root, BoardMeshes.RoundedBox(new Vector3(0.008f, 0.004f, GridSize), 0.0015f, 2), groove, new Vector3(o, TopY + 0.0295f, 0f));
                k.Part("Groove", Root, BoardMeshes.RoundedBox(new Vector3(GridSize, 0.004f, 0.008f), 0.0015f, 2), groove, new Vector3(0f, TopY + 0.0295f, o));
            }
            TokenX = k.Mat(new Color(0.12f, 0.08f, 0.06f), 0f, 0.7f, BoardArt.GrainFine);
            TokenO = k.Mat(new Color(0.92f, 0.85f, 0.7f), 0f, 0.65f, BoardArt.GrainFine);
            Tokens = new GameObject("Tokens") { layer = k.Layer }.transform;
            Tokens.SetParent(Root, false);
        }

        /// <summary>Wooden token for style 1: X = two crossed bars, O = a ring.</summary>
        public GameObject Token(bool x, Transform parent)
        {
            var g = new GameObject(x ? "TokenX" : "TokenO") { layer = Kit.Layer };
            g.transform.SetParent(parent, false);
            if (x)
            {
                var bar = BoardMeshes.RoundedBox(new Vector3(0.105f, 0.02f, 0.026f), 0.008f, 3);
                Kit.Part("Bar1", g.transform, bar, TokenX, new Vector3(0f, 0.01f, 0f), Quaternion.Euler(0f, 45f, 0f));
                var bar2 = BoardMeshes.RoundedBox(new Vector3(0.105f, 0.024f, 0.026f), 0.008f, 3);
                Kit.Part("Bar2", g.transform, bar2, TokenX, new Vector3(0f, 0.012f, 0f), Quaternion.Euler(0f, -45f, 0f));
            }
            else
            {
                var ring = new List<Vector2>();
                BoardMeshes.Arc(ring, 0.034f, 0.01f, 0.012f, -90f, 270f, 16);
                Kit.Part("Ring", g.transform, BoardMeshes.Lathe("tttring", ring, 36, 2f), TokenO, Vector3.zero);
            }
            return g;
        }
    }
}
