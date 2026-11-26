using UnityEngine;

namespace HideAndSeek.Game.MiniGames.Pool
{
    /// <summary>
    /// Textures of the pool balls, drawn per pixel from the 3D direction so nothing on the sphere is stretched.
    /// Layout: equirectangular around the ball mesh's local Z axis (u = longitude, v = latitude), the same mapping the
    /// mesh in <see cref="PoolBallModels"/> uses. Pattern: the stripe is the band around the equator (|latitude| &lt;
    /// <see cref="StripeLat"/>), the two number circles sit on it at local +Y and -Y, digits upright towards +Z (so a
    /// stripe lying horizontal reads like a real ball). The poles (texture rows 0 / H-1) only ever hold plain colour,
    /// so the pinch of the equirect map never shows. Edges are anti-aliased from the distance in radians.
    /// The cue ball is cream with six small red dots on the axes of a tilted cube (none near a pole).
    /// </summary>
    public static class PoolBallArt
    {
        public const int W = 2048, H = 1024;
        /// <summary>Angular radius of the white number circle (about 24 mm across on a 57 mm ball).</summary>
        public const float CircleR = 0.42f;
        /// <summary>Half width of the stripe as latitude (radians).</summary>
        public const float StripeLat = 0.46f; // narrower band, bigger white caps: a stripe never reads as a solid from above
        /// <summary>Angular radius of a cue-ball dot (about 6 mm across).</summary>
        public const float DotR = 0.105f;
        /// <summary>Glyph height (radians) for one and two digits.</summary>
        const float GlyphH1 = 0.51f, GlyphH2 = 0.43f;

        public static readonly Color Cream = new(0.955f, 0.935f, 0.865f);
        static readonly Color CircleWhite = new(0.975f, 0.965f, 0.93f);
        static readonly Color Ink = new(0.035f, 0.035f, 0.04f);
        static readonly Color DotRed = new(0.80f, 0.06f, 0.06f);

        static readonly Color[] Colors =
        {
            new(0.955f, 0.935f, 0.865f), // 0 cue
            new(1.00f, 0.80f, 0.06f),    // 1 yellow
            new(0.04f, 0.20f, 0.66f),    // 2 blue
            new(0.86f, 0.07f, 0.06f),    // 3 red
            new(0.34f, 0.12f, 0.52f),    // 4 purple
            new(1.00f, 0.44f, 0.04f),    // 5 orange
            new(0.02f, 0.46f, 0.22f),    // 6 green
            new(0.46f, 0.07f, 0.07f),    // 7 maroon
            new(0.035f, 0.035f, 0.04f),  // 8 black
        };

        public static Color BallColor(int n) => n <= 0 ? Colors[0] : Colors[n > 8 ? n - 8 : Mathf.Min(n, 8)];

        public static bool IsStripe(int n) => n >= 9 && n <= 15;

        /// <summary>Direction (ball mesh space) of texel (x, y).</summary>
        public static Vector3 Dir(float u, float v)
        {
            float lon = u * Mathf.PI * 2f, lat = (v - 0.5f) * Mathf.PI;
            float cl = Mathf.Cos(lat);
            return new Vector3(cl * Mathf.Cos(lon), cl * Mathf.Sin(lon), Mathf.Sin(lat));
        }

        /// <summary>The six dot directions of the cue ball (cube axes turned so the cube diagonal lies on the pole axis).</summary>
        static Vector3[] CueDots()
        {
            var q = Quaternion.FromToRotation(new Vector3(1f, 1f, 1f).normalized, Vector3.forward);
            var d = new Vector3[6];
            Vector3[] axes = { Vector3.right, Vector3.up, Vector3.forward };
            for (int i = 0; i < 3; i++) { d[i * 2] = q * axes[i]; d[i * 2 + 1] = -(q * axes[i]); }
            return d;
        }

        public static Texture2D Make(int n) => Make(n, W, H);

        /// <summary>Texture of ball <paramref name="n"/> at a smaller size (<paramref name="w"/> x <paramref name="h"/>, 2:1), e.g. 1024 x 512 in the lobby.</summary>
        public static Texture2D Make(int n, int w, int h) => ToTexture(n, Pixels(n, w, h), w, h);

        /// <summary>
        /// The pixels of ball <paramref name="n"/> (row-major, w x h). Pure maths with no Unity objects, so it may run
        /// on a worker thread; <see cref="ToTexture"/> then uploads them on the main thread.
        /// </summary>
        public static Color32[] Pixels(int n, int w, int h)
        {
            int W = Mathf.Max(8, w), H = Mathf.Max(4, h);
            var px = new Color32[W * H];
            var cosLon = new float[W];
            var sinLon = new float[W];
            for (int x = 0; x < W; x++)
            {
                float lon = (x + 0.5f) / W * Mathf.PI * 2f;
                cosLon[x] = Mathf.Cos(lon);
                sinLon[x] = Mathf.Sin(lon);
            }
            Color baseCol = BallColor(n);
            bool stripe = IsStripe(n);
            string text = n.ToString();
            float glyphH = text.Length > 1 ? GlyphH2 : GlyphH1;
            Vector3[] centres = { Vector3.up, Vector3.down };
            Vector3 up = Vector3.forward;
            var rights = new Vector3[2];
            for (int c = 0; c < 2; c++) rights[c] = Vector3.Cross(centres[c], up);
            float cosOuter = Mathf.Cos(CircleR + 0.02f);
            var dots = n == 0 ? CueDots() : null;
            float cosDot = Mathf.Cos(DotR + 0.02f);

            for (int y = 0; y < H; y++)
            {
                float lat = ((y + 0.5f) / H - 0.5f) * Mathf.PI;
                float cl = Mathf.Cos(lat), sl = Mathf.Sin(lat);
                // Size of one texel in radians (the larger of its two sides, never below the latitude step).
                float pxA = Mathf.PI / H;
                Color rowBase = baseCol;
                if (stripe)
                {
                    float band = Mathf.Clamp01((StripeLat - Mathf.Abs(lat)) / pxA + 0.5f);
                    rowBase = Color.Lerp(CircleWhite, baseCol, band);
                }
                for (int x = 0; x < W; x++)
                {
                    var d = new Vector3(cl * cosLon[x], cl * sinLon[x], sl);
                    Color col = rowBase;
                    if (n == 0)
                    {
                        for (int k = 0; k < dots.Length; k++)
                        {
                            float cd = Vector3.Dot(d, dots[k]);
                            if (cd < cosDot) continue;
                            float a = Mathf.Acos(Mathf.Min(1f, cd));
                            col = Color.Lerp(col, DotRed, Mathf.Clamp01((DotR - a) / pxA + 0.5f));
                        }
                    }
                    else
                    {
                        for (int c = 0; c < 2; c++)
                        {
                            float cd = Vector3.Dot(d, centres[c]);
                            if (cd < cosOuter) continue;
                            float a = Mathf.Acos(Mathf.Min(1f, cd));
                            col = Color.Lerp(col, CircleWhite, Mathf.Clamp01((CircleR - a) / pxA + 0.5f));
                            // Azimuthal equidistant coordinates round the circle centre (radians), digits upright to +Z.
                            Vector3 t = d - centres[c] * cd;
                            float st = t.magnitude;
                            Vector2 p = st > 1e-7f ? new Vector2(Vector3.Dot(t, rights[c]), Vector3.Dot(t, up)) * (a / st) : Vector2.zero;
                            float dist = PoolBallGlyphs.Distance(text, p / glyphH) * glyphH;
                            col = Color.Lerp(col, Ink, Mathf.Clamp01(0.5f - dist / pxA));
                        }
                    }
                    px[y * W + x] = col;
                }
            }
            return px;
        }

        /// <summary>Main thread: a mip-mapped, compressed texture from <see cref="Pixels"/>.</summary>
        public static Texture2D ToTexture(int n, Color32[] px, int w, int h)
        {
            int W = Mathf.Max(8, w), H = Mathf.Max(4, h);
            var tex = new Texture2D(W, H, TextureFormat.RGB24, true, false)
            {
                name = "PoolBall" + n,
                wrapModeU = TextureWrapMode.Repeat,
                wrapModeV = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 8,
            };
            tex.SetPixels32(px);
            tex.Apply(true, false);
            if (SystemInfo.SupportsTextureFormat(TextureFormat.DXT1)) tex.Compress(true);
            tex.Apply(false, true);
            return tex;
        }
    }
}
