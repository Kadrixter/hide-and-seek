using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Pixel icons of the 8 fuses: the cap of a fuse seen from the front (cream ceramic rim, brass blades sticking out at
    /// the sides) with a big black plate in the middle and the fuse's mark in yellow - the mark is what tells them apart.
    /// </summary>
    public static partial class HotbarIcons
    {
        private static readonly Sprite[] _fuse = new Sprite[FuseSymbols.Count];

        public static Sprite Fuse(int symbol)
        {
            symbol = Mathf.Clamp(symbol, 0, FuseSymbols.Count - 1);
            if (_fuse[symbol] != null) return _fuse[symbol];
            var px = new Color32[N * N];
            var cream = C(0.86f, 0.82f, 0.7f);
            var creamDark = C(0.62f, 0.58f, 0.48f);
            var brass = C(0.85f, 0.66f, 0.32f);
            var brassDark = C(0.55f, 0.41f, 0.18f);
            var plate = C(0.07f, 0.07f, 0.08f);
            var paint = new Color32(255, 206, 60, 255);
            // Blades left and right, the body, its shaded lower / right edge, the plate.
            Rect(px, 0, 12, 3, 19, brass);
            Rect(px, 28, 12, 31, 19, brass);
            Rect(px, 0, 12, 3, 12, brassDark);
            Rect(px, 28, 12, 31, 12, brassDark);
            Rect(px, 3, 3, 28, 28, cream);
            Rect(px, 3, 3, 28, 4, creamDark);
            Rect(px, 27, 3, 28, 28, creamDark);
            Rect(px, 5, 5, 26, 26, plate);
            // The mark, 20 x 20 px in the plate.
            for (int y = 6; y <= 25; y++)
            for (int x = 6; x <= 25; x++)
            {
                var p = new Vector2((x + 0.5f - 16f) / 10f, (y + 0.5f - 16f) / 10f);
                if (FuseSymbols.Inside(symbol, p / 0.92f, 0.1f) > 0.45f) px[y * N + x] = paint;
            }
            Outline(px);
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            tex.SetPixels32(px);
            tex.Apply();
            _fuse[symbol] = Sprite.Create(tex, new UnityEngine.Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 32f);
            return _fuse[symbol];
        }
    }
}
