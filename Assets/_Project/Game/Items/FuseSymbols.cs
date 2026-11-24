using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// The 8 marks of the fuses (escape objective): every fuse carries one, every socket of a fuse box has one painted
    /// next to it, and a fuse only fits the socket with its mark. Shapes, not colours (all marks are the same warm
    /// yellow on a black plate). Defined as signed distance fields in a -1..1 square, so the same shape is drawn on the
    /// 3D labels (<see cref="Label"/>, 128 px, anti-aliased, painted look) and in the 32 px hotbar icon (<see cref="Inside"/>).
    /// </summary>
    public static class FuseSymbols
    {
        public const int Count = 8;
        public static readonly string[] Names = { "trójkąt", "kółko", "krzyżyk", "fala", "gwiazda", "kwadrat", "błyskawica", "półksiężyc" };
        public static readonly Color Paint = new Color(1f, 0.8f, 0.22f);
        public static readonly Color Plate = new Color(0.055f, 0.058f, 0.065f);

        private static readonly Texture2D[] Labels = new Texture2D[Count];
        private static Vector2[] _tri, _star, _bolt, _wave;

        public static string Name(int s) => s >= 0 && s < Count ? Names[s] : "?";

        /// <summary>Signed distance (negative inside) of mark <paramref name="s"/> at <paramref name="p"/> (-1..1, y up).</summary>
        public static float Sd(int s, Vector2 p)
        {
            switch (s)
            {
                case 0: return Poly(_tri ??= Tri(), p);
                case 1: return Mathf.Abs(p.magnitude - 0.6f) - 0.2f;
                case 2:
                {
                    const float c = 0.70710678f;
                    var a = new Vector2(c * p.x + c * p.y, -c * p.x + c * p.y);
                    return Mathf.Min(Box(a, 0.86f, 0.19f), Box(a, 0.19f, 0.86f));
                }
                case 3: return Line(_wave ??= Wave(), p) - 0.17f;
                case 4: return Poly(_star ??= Star(), p);
                case 5: return Box(p, 0.62f, 0.62f) - 0.04f;
                case 6: return Poly(_bolt ??= Bolt(), p);
                default: return Mathf.Max(p.magnitude - 0.78f, -((p - new Vector2(0.36f, 0.22f)).magnitude - 0.62f));
            }
        }

        /// <summary>Coverage 0..1 of mark <paramref name="s"/> around <paramref name="p"/> (pixel size <paramref name="px"/>).</summary>
        public static float Inside(int s, Vector2 p, float px) => Mathf.Clamp01(0.5f - Sd(s, p) / px);

        /// <summary>
        /// 128 px label: the mark painted on a black plate with a thin worn rim; a light bevel on the mark's upper edges
        /// and a shade on the lower ones (stamped and painted), some grain. Opaque, clamp, mipmapped.
        /// </summary>
        public static Texture2D Label(int s)
        {
            s = Mathf.Clamp(s, 0, Count - 1);
            if (Labels[s] != null) return Labels[s];
            const int n = 128;
            var px = new Color32[n * n];
            var rnd = new System.Random(811 + s * 37);
            float pix = 2f / n;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                var uv = new Vector2((x + 0.5f) / n * 2f - 1f, (y + 0.5f) / n * 2f - 1f);
                float grain = (float)rnd.NextDouble() * 0.025f;
                // Plate with a slightly lighter worn rim.
                float edge = Mathf.Max(Mathf.Abs(uv.x), Mathf.Abs(uv.y));
                Color c = Plate + new Color(grain, grain, grain);
                if (edge > 0.9f) c = Color.Lerp(c, new Color(0.22f, 0.22f, 0.21f), Mathf.Clamp01((edge - 0.9f) / 0.04f));
                // The mark, 80 % of the plate.
                Vector2 q = uv / 0.8f;
                float d = Sd(s, q);
                float cover = Mathf.Clamp01(0.5f - d / (pix / 0.8f * 1.2f));
                if (cover > 0f)
                {
                    // Bevel: the distance gradient tells the edge direction (light from the top left).
                    float e = 0.02f;
                    var g = new Vector2(Sd(s, q + new Vector2(e, 0f)) - Sd(s, q - new Vector2(e, 0f)), Sd(s, q + new Vector2(0f, e)) - Sd(s, q - new Vector2(0f, e)));
                    float rim = Mathf.Clamp01(1f + d / 0.09f); // 1 at the edge, 0 deeper inside
                    float lit = g.sqrMagnitude > 1e-8f ? Vector2.Dot(g.normalized, new Vector2(-0.6f, 0.8f)) : 0f;
                    Color m = Paint * (0.92f + grain * 2f) * (1f - 0.22f * rim * Mathf.Max(0f, -lit)) + Color.white * (0.18f * rim * Mathf.Max(0f, lit));
                    // Chipped paint here and there.
                    if (rnd.NextDouble() < 0.012) m = Color.Lerp(m, Plate, 0.6f);
                    c = Color.Lerp(c, m, cover);
                }
                c.a = 1f;
                px[y * n + x] = c;
            }
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4, name = "FuseLabel_" + s, hideFlags = HideFlags.DontSave };
            tex.SetPixels32(px);
            tex.Apply(true, false);
            Labels[s] = tex;
            return tex;
        }

        // ---------------------------------------------------------------- shapes

        private static float Box(Vector2 p, float hx, float hy)
        {
            var d = new Vector2(Mathf.Abs(p.x) - hx, Mathf.Abs(p.y) - hy);
            return new Vector2(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f)).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0f);
        }

        /// <summary>Signed distance to a closed polygon (any winding, concave allowed; Inigo Quilez's sdPolygon).</summary>
        private static float Poly(Vector2[] v, Vector2 p)
        {
            float d = Vector2.Dot(p - v[0], p - v[0]);
            float s = 1f;
            for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
            {
                Vector2 e = v[j] - v[i], w = p - v[i];
                Vector2 b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
                d = Mathf.Min(d, Vector2.Dot(b, b));
                bool c1 = p.y >= v[i].y, c2 = p.y < v[j].y, c3 = e.x * w.y > e.y * w.x;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
            }
            return s * Mathf.Sqrt(d);
        }

        /// <summary>Distance to an open polyline.</summary>
        private static float Line(Vector2[] v, Vector2 p)
        {
            float d = float.MaxValue;
            for (int i = 0; i + 1 < v.Length; i++)
            {
                Vector2 a = v[i], ab = v[i + 1] - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                d = Mathf.Min(d, (a + ab * t - p).magnitude);
            }
            return d;
        }

        private static Vector2[] Tri() => new[] { new Vector2(0f, 0.86f), new Vector2(-0.88f, -0.68f), new Vector2(0.88f, -0.68f) };

        private static Vector2[] Star()
        {
            var v = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float a = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                float r = i % 2 == 0 ? 0.95f : 0.4f;
                v[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a) - 0.08f) * r;
            }
            return v;
        }

        private static Vector2[] Bolt() => new[]
        {
            new Vector2(0.02f, 0.95f), new Vector2(0.5f, 0.95f), new Vector2(0.16f, 0.22f), new Vector2(0.52f, 0.22f),
            new Vector2(-0.26f, -0.95f), new Vector2(-0.04f, -0.12f), new Vector2(-0.44f, -0.12f),
        };

        private static Vector2[] Wave()
        {
            var v = new Vector2[41];
            for (int i = 0; i < v.Length; i++)
            {
                float x = -0.78f + 1.56f * i / (v.Length - 1);
                v[i] = new Vector2(x, 0.34f * Mathf.Sin(x / 0.78f * Mathf.PI * 1.5f));
            }
            return v;
        }
    }
}
