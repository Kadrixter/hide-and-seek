using System;
using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Drawing kit of the boombox panel (IMGUI): rounded 9-slice backgrounds, icons rasterised from distance functions
    /// (anti-aliased, white, tinted with GUI.color), text styles scaled to the screen, text shortened with "...".
    /// </summary>
    public static class RadioUi
    {
        public static readonly Color Accent = new(0.36f, 0.85f, 0.5f, 1f);
        public static readonly Color AccentDim = new(0.36f, 0.85f, 0.5f, 0.16f);
        public static readonly Color Text = new(0.93f, 0.94f, 0.92f, 1f);
        public static readonly Color Dim = new(0.6f, 0.63f, 0.66f, 1f);
        public static readonly Color Warn = new(1f, 0.8f, 0.38f, 1f);
        public static readonly Color Bad = new(1f, 0.42f, 0.38f, 1f);
        public static readonly Color PanelBg = new(0.07f, 0.075f, 0.09f, 0.97f);
        public static readonly Color Card = new(0.11f, 0.12f, 0.14f, 1f);
        public static readonly Color CardHover = new(0.15f, 0.165f, 0.19f, 1f);

        public static Texture2D White => Texture2D.whiteTexture;
        private static readonly Dictionary<int, Texture2D> Rounds = new();
        private static readonly Dictionary<int, GUIStyle> RoundStyles = new();
        private static readonly Dictionary<string, Texture2D> Icons = new();

        // ================================================================ rounded rectangles

        /// <summary>White rounded rectangle, 9-slice with <paramref name="r"/> px corners (tint with GUI.color).</summary>
        public static Texture2D Round(int r)
        {
            if (Rounds.TryGetValue(r, out var t)) return t;
            int n = 2 * r + 2;
            t = NewTex(n);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = BoxSd(x + 0.5f - n * 0.5f, y + 0.5f - n * 0.5f, n * 0.5f, n * 0.5f, r);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(0.5f - d) * 255f));
            }
            t.SetPixels32(px);
            t.Apply();
            Rounds[r] = t;
            return t;
        }

        /// <summary>A rounded rectangle in <paramref name="c"/> (Repaint only).</summary>
        public static void Fill(Rect rect, Color c, float radius)
        {
            if (Event.current.type != EventType.Repaint) return;
            int r = Mathf.Clamp(Mathf.RoundToInt(radius), 1, 64);
            r = Mathf.Min(r, Mathf.FloorToInt(Mathf.Min(rect.width, rect.height) * 0.5f));
            if (r < 1) { Draw(rect, White, c); return; }
            if (!RoundStyles.TryGetValue(r, out var st))
                RoundStyles[r] = st = new GUIStyle { normal = { background = Round(r) }, border = new RectOffset(r + 1, r + 1, r + 1, r + 1) };
            Color oc = GUI.color, ob = GUI.backgroundColor;
            GUI.color = c;
            GUI.backgroundColor = Color.white;
            st.Draw(rect, false, false, false, false);
            GUI.color = oc;
            GUI.backgroundColor = ob;
        }

        public static void Draw(Rect rect, Texture tex, Color c)
        {
            if (Event.current.type != EventType.Repaint || tex == null) return;
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(rect, tex, ScaleMode.StretchToFill, true);
            GUI.color = old;
        }

        // ================================================================ icons

        public static Texture2D Icon(string name) => Icons.TryGetValue(name, out var t) ? t : Icons[name] = Bake(name);

        private static Texture2D Bake(string name)
        {
            const int n = 96;
            var t = NewTex(n);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n - 0.5f, v = (y + 0.5f) / n - 0.5f;
                float d = Sd(name, u, v) * n; // in pixels
                px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(0.5f - d) * 255f));
            }
            t.SetPixels32(px);
            t.Apply();
            return t;
        }

        /// <summary>Signed distance (in icon widths, + outside) of icon <paramref name="name"/> at (u, v), y up.</summary>
        private static float Sd(string name, float u, float v)
        {
            switch (name)
            {
                case "play": return Tri(u, v, -0.17f, -0.25f, 0.27f, 0f, -0.17f, 0.25f) - 0.02f;
                case "pause": return Mathf.Min(BoxSd(u + 0.12f, v, 0.065f, 0.24f, 0.03f), BoxSd(u - 0.12f, v, 0.065f, 0.24f, 0.03f));
                case "next": return Mathf.Min(Tri(u, v, -0.24f, -0.22f, 0.15f, 0f, -0.24f, 0.22f) - 0.015f, BoxSd(u - 0.2f, v, 0.04f, 0.22f, 0.02f));
                case "prev": return Sd("next", -u, v);
                case "close": return Mathf.Min(Seg(u, v, -0.2f, -0.2f, 0.2f, 0.2f), Seg(u, v, -0.2f, 0.2f, 0.2f, -0.2f)) - 0.045f;
                case "dot": return Mathf.Sqrt(u * u + v * v) - 0.4f;
                case "stop": return BoxSd(u, v, 0.2f, 0.2f, 0.04f);
                case "ring": return Mathf.Abs(Mathf.Sqrt(u * u + v * v) - 0.42f) - 0.04f;
                case "loop":
                {
                    // Classic repeat: a rounded rectangle outline, open at the top right and the bottom left, an arrow
                    // pointing right at the end of the top line and one pointing left at the end of the bottom line.
                    float d = Mathf.Abs(BoxSd(u, v, 0.31f, 0.19f, 0.13f)) - 0.042f;
                    d = Mathf.Max(d, -Mathf.Max(0.06f - u, -v));  // gap: top right
                    d = Mathf.Max(d, -Mathf.Max(u + 0.06f, v));   // gap: bottom left
                    float h1 = Tri(u, v, 0.05f, 0.31f, 0.25f, 0.19f, 0.05f, 0.07f);
                    float h2 = Tri(u, v, -0.05f, -0.31f, -0.25f, -0.19f, -0.05f, -0.07f);
                    return Mathf.Min(d, Mathf.Min(h1, h2));
                }
                case "vinyl":
                {
                    float r = Mathf.Sqrt(u * u + v * v);
                    return r - 0.48f;
                }
                default: return 1f;
            }
        }

        /// <summary>A record (placeholder art): black disc with grooves and a label, white label left for tinting.</summary>
        public static Texture2D Vinyl()
        {
            if (Icons.TryGetValue("#vinyl", out var t)) return t;
            const int n = 256;
            t = NewTex(n);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n - 0.5f, v = (y + 0.5f) / n - 0.5f, r = Mathf.Sqrt(u * u + v * v);
                float a = Mathf.Clamp01((0.48f - r) * n + 0.5f);
                Color c;
                if (r < 0.018f) c = new Color(0.05f, 0.05f, 0.06f);
                else if (r < 0.15f) c = new Color(0.36f, 0.85f, 0.5f);
                else
                {
                    float groove = 0.5f + 0.5f * Mathf.Sin(r * 380f);
                    float sheen = 0.5f + 0.5f * Mathf.Cos(2f * Mathf.Atan2(v, u));
                    float g = 0.07f + 0.025f * groove + 0.06f * sheen * Mathf.Clamp01((r - 0.16f) * 4f);
                    c = new Color(g, g, g * 1.08f);
                }
                px[y * n + x] = new Color(c.r, c.g, c.b, a);
            }
            t.SetPixels32(px);
            t.Apply();
            Icons["#vinyl"] = t;
            return t;
        }

        // ================================================================ text

        private static readonly Dictionary<(string, int, int), string> Short = new();

        /// <summary><paramref name="text"/> cut with "..." to fit <paramref name="width"/> px in <paramref name="style"/>.</summary>
        public static string Fit(string text, GUIStyle style, float width)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var key = (text, (int)width, style.fontSize);
            if (Short.TryGetValue(key, out var s)) return s;
            if (Short.Count > 2000) Short.Clear();
            var c = new GUIContent(text);
            if (style.CalcSize(c).x <= width) return Short[key] = text;
            int lo = 0, hi = text.Length;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                c.text = text.Substring(0, mid).TrimEnd() + "...";
                if (style.CalcSize(c).x <= width) lo = mid;
                else hi = mid - 1;
            }
            return Short[key] = text.Substring(0, lo).TrimEnd() + "...";
        }

        public static string Time(float t) => t < 0f || float.IsNaN(t) ? "--:--"
            : t >= 3600f ? $"{(int)t / 3600}:{(int)t / 60 % 60:00}:{(int)t % 60:00}" : $"{(int)t / 60}:{(int)t % 60:00}";

        public static GUIStyle Label(int size, Color c, FontStyle fs = FontStyle.Normal, TextAnchor a = TextAnchor.MiddleLeft) =>
            new(GUI.skin.label)
            {
                fontSize = size, fontStyle = fs, alignment = a, wordWrap = false, clipping = TextClipping.Clip,
                normal = { textColor = c }, hover = { textColor = c }, padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0),
            };

        // ================================================================ math

        private static Texture2D NewTex(int n) => new(n, n, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave,
        };

        private static float BoxSd(float x, float y, float hx, float hy, float r)
        {
            float qx = Mathf.Abs(x) - hx + r, qy = Mathf.Abs(y) - hy + r;
            return new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        /// <summary>Distance to a triangle (a, b, c in either winding), + outside.</summary>
        private static float Tri(float px, float py, float ax, float ay, float bx, float by, float cx, float cy)
        {
            Vector2 p = new(px, py), a = new(ax, ay), b = new(bx, by), c = new(cx, cy);
            float d = Mathf.Min(SegD(p, a, b), Mathf.Min(SegD(p, b, c), SegD(p, c, a)));
            float s = Cross(b - a, p - a), s2 = Cross(c - b, p - b), s3 = Cross(a - c, p - c);
            bool inside = (s >= 0 && s2 >= 0 && s3 >= 0) || (s <= 0 && s2 <= 0 && s3 <= 0);
            return inside ? -d : d;
        }

        private static float Seg(float px, float py, float ax, float ay, float bx, float by) => SegD(new Vector2(px, py), new Vector2(ax, ay), new Vector2(bx, by));

        private static float SegD(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            return (p - a - ab * h).magnitude;
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    }
}
