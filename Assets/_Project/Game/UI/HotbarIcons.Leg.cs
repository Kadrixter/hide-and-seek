using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>Pixel icon of a cut-off leg: blue trousers, skin-coloured shin, a dark shoe, the raw red cut with a bone.</summary>
    public static partial class HotbarIcons
    {
        private static Sprite _leg;

        public static Sprite Leg()
        {
            if (_leg != null) return _leg;
            var px = new Color32[N * N];
            var jeans = C(0.2f, 0.32f, 0.62f);
            var jeansDark = C(0.13f, 0.21f, 0.44f);
            var skin = C(0.86f, 0.64f, 0.5f);
            var shoe = C(0.22f, 0.16f, 0.12f);
            var flesh = C(0.72f, 0.06f, 0.06f);
            var fleshDark = C(0.42f, 0.02f, 0.03f);
            var bone = C(0.93f, 0.88f, 0.76f);
            // Diagonal leg: cut top-right, foot bottom-left.
            Line(px, 24f, 25f, 15f, 16f, 4.2f, jeans);
            Line(px, 25f, 23f, 17f, 15f, 1.2f, jeansDark);
            Line(px, 15f, 16f, 8.5f, 9.5f, 3.6f, skin);
            Line(px, 9f, 8f, 4f, 9f, 3f, shoe);
            Rect(px, 2, 6, 6, 7, C(0.1f, 0.08f, 0.06f));
            Line(px, 21.5f, 28.5f, 28.5f, 21.5f, 1.4f, flesh);
            Line(px, 22f, 27f, 27f, 22f, 0.6f, fleshDark);
            Rect(px, 25, 26, 27, 28, bone);
            Rect(px, 27, 15, 28, 17, flesh);             // drips
            Rect(px, 28, 12, 28, 13, flesh);
            Outline(px);
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            tex.SetPixels32(px);
            tex.Apply();
            _leg = Sprite.Create(tex, new UnityEngine.Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 32f);
            return _leg;
        }
    }
}
