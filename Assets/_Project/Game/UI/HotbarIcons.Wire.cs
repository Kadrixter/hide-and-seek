using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>Pixel icon of a piece of fence wire: a grey zig-zag across the slot with light glints, a hook on top.</summary>
    public static partial class HotbarIcons
    {
        private static Sprite _wire;

        public static Sprite Wire()
        {
            if (_wire != null) return _wire;
            var px = new Color32[N * N];
            var steel = C(0.72f, 0.74f, 0.77f);
            var dark = C(0.4f, 0.41f, 0.44f);
            var glint = C(0.96f, 0.97f, 1f);
            // Zig-zag from bottom-left to top-right (the kinks of the mesh it was torn from).
            float[] xs = { 4f, 9f, 9f, 15f, 15f, 21f, 21f, 26f };
            float[] ys = { 5f, 9f, 14f, 13f, 19f, 18f, 24f, 26f };
            for (int i = 0; i + 1 < xs.Length; i++)
            {
                Line(px, xs[i] + 0.6f, ys[i] - 0.6f, xs[i + 1] + 0.6f, ys[i + 1] - 0.6f, 1.4f, dark);
                Line(px, xs[i], ys[i], xs[i + 1], ys[i + 1], 1.3f, steel);
            }
            // Hook at the top end, torn burr at the bottom.
            Line(px, 26f, 26f, 28f, 28f, 1.2f, steel);
            Line(px, 28f, 28f, 29.5f, 26f, 1.2f, steel);
            Line(px, 29.5f, 26f, 28.5f, 24.5f, 1.1f, steel);
            Line(px, 4f, 5f, 2.5f, 3f, 1f, dark);
            Rect(px, 12, 11, 12, 11, glint);
            Rect(px, 18, 16, 18, 16, glint);
            Rect(px, 23, 25, 23, 25, glint);
            Outline(px);
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            tex.SetPixels32(px);
            tex.Apply();
            _wire = Sprite.Create(tex, new UnityEngine.Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 32f);
            return _wire;
        }
    }
}
