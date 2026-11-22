using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>Pixel icon of the pink toy phone (antenna with a yellow ball, small screen, colourful keypad).</summary>
    public static partial class HotbarIcons
    {
        private static Sprite _phone;

        public static Sprite Phone()
        {
            if (_phone != null) return _phone;
            var px = new Color32[N * N];
            var pink = C(1f, 0.42f, 0.72f);
            Rect(px, 9, 3, 23, 26, pink);                        // body
            Rect(px, 10, 24, 22, 26, C(1f, 0.66f, 0.85f));       // top cap
            Rect(px, 9, 3, 23, 4, C(0.82f, 0.24f, 0.55f));        // bottom bumper
            Rect(px, 19, 27, 20, 29, C(0.82f, 0.24f, 0.55f));     // antenna
            Rect(px, 18, 29, 21, 31, C(1f, 0.86f, 0.2f));         // ball
            Rect(px, 11, 17, 21, 22, C(0.95f, 0.95f, 0.98f));     // bezel
            Rect(px, 12, 18, 20, 21, C(0.55f, 0.85f, 0.6f));      // screen
            Rect(px, 11, 14, 14, 15, C(0.3f, 0.9f, 0.35f));       // call
            Rect(px, 18, 14, 21, 15, C(1f, 0.25f, 0.25f));        // end
            Color32[] keys = { C(1f, 0.86f, 0.2f), C(0.35f, 0.82f, 1f), C(0.55f, 0.95f, 0.4f), C(0.98f, 0.98f, 0.95f), C(0.72f, 0.5f, 1f), C(1f, 0.6f, 0.25f) };
            for (int r = 0; r < 3; r++)
            for (int c = 0; c < 3; c++)
                Rect(px, 11 + c * 4, 11 - r * 3, 12 + c * 4, 11 - r * 3, keys[(r * 3 + c) % keys.Length]);
            Outline(px);
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            tex.SetPixels32(px);
            tex.Apply();
            return _phone = Sprite.Create(tex, new UnityEngine.Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 32f);
        }
    }
}
