using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>Pixel icon of the RC transmitter (pilot): blue face, two yellow sticks, green LCD, chrome antenna with a red tip.</summary>
    public static partial class HotbarIcons
    {
        private static Sprite _rcRemote;

        public static Sprite RcRemote()
        {
            if (_rcRemote != null) return _rcRemote;
            var px = new Color32[N * N];
            var caseC = C(0.16f, 0.165f, 0.18f);
            var blue = C(0.1f, 0.34f, 0.88f);
            var yellow = C(1f, 0.78f, 0.08f);
            var rubber = C(0.07f, 0.07f, 0.075f);
            var black = C(0.02f, 0.02f, 0.025f);
            var chrome = C(0.8f, 0.82f, 0.86f);
            Rect(px, 4, 2, 8, 9, rubber);                  // handles
            Rect(px, 23, 2, 27, 9, rubber);                // left handle
            Rect(px, 4, 1, 8, 1, yellow);
            Rect(px, 23, 1, 27, 1, yellow);
            Rect(px, 2, 8, 29, 21, caseC);                 // case
            Rect(px, 2, 8, 29, 8, yellow);                 // bumper
            Rect(px, 3, 10, 28, 20, blue);                 // face plate
            Rect(px, 3, 11, 28, 11, C(0.95f, 0.95f, 0.96f)); // stripe
            Rect(px, 6, 13, 11, 18, black);                // gimbals
            Rect(px, 20, 13, 25, 18, black);
            Rect(px, 8, 15, 10, 17, yellow);               // knobs (right stick steered a little)
            Rect(px, 21, 14, 23, 16, yellow);
            Rect(px, 13, 17, 18, 19, C(0.62f, 0.86f, 0.58f)); // LCD
            Rect(px, 15, 13, 16, 14, C(1f, 0.15f, 0.1f));  // LED
            Rect(px, 4, 22, 5, 22, black);                 // antenna socket (top-left, as the holder sees it)
            Rect(px, 5, 23, 5, 29, chrome);                // antenna
            Rect(px, 4, 29, 6, 31, C(0.85f, 0.07f, 0.05f)); // tip
            Outline(px);
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            tex.SetPixels32(px);
            tex.Apply();
            return _rcRemote = Sprite.Create(tex, new UnityEngine.Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 32f);
        }
    }
}
