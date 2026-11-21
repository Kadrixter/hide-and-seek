using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Paints signs, posters and labels into ordinary textures (used by lit materials, so the text is as dark as the wall it
    /// hangs on and never shows through anything). Shapes and text are laid out on a hidden layer far below the map and
    /// rendered once by an orthographic camera. Coordinates: x, y in 0..1 from the bottom-left corner of the texture.
    /// </summary>
    public sealed class TextureBaker
    {
        private const int Layer = 30;
        private static readonly Vector3 Origin = new Vector3(0f, -2000f, 0f);
        private static Font _font;
        private static Material _textMat;
        private static Shader _shapeShader;
        private static readonly Dictionary<Color, Material> ShapeMats = new();

        private readonly GameObject _root;
        private readonly int _w, _h;
        private readonly float _aspect;
        private readonly Color _bg;
        private float _depth;
        private readonly List<(TextMesh tm, float height, float maxWidth, int lines)> _texts = new();

        public TextureBaker(int width, int height, Color background)
        {
            _w = width; _h = height; _aspect = width / (float)height; _bg = background;
            if (_font == null)
            {
                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                var sh = Resources.Load<Shader>("Shaders/HnsSecurityText");
                _textMat = sh != null ? new Material(sh) { name = "BakeText" } : new Material(_font.material);
                _shapeShader = Shader.Find("Sprites/Default");
            }
            _root = new GameObject("TextureBake");
            _root.transform.position = Origin;
        }

        private Vector3 P(float x, float y) => Origin + new Vector3(x * _aspect, y, -(_depth += 0.002f));

        private static Material ShapeMat(Color c)
        {
            if (ShapeMats.TryGetValue(c, out var m) && m != null) return m;
            m = new Material(_shapeShader) { color = c, name = "BakeShape" };
            ShapeMats[c] = m;
            return m;
        }

        private GameObject Shape(PrimitiveType type, Vector3 pos, Vector3 scale, float angle, Color c)
        {
            var g = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(g.GetComponent<Collider>());
            g.layer = Layer;
            g.transform.SetParent(_root.transform, false);
            g.transform.position = pos;
            g.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            g.transform.localScale = scale;
            g.GetComponent<MeshRenderer>().sharedMaterial = ShapeMat(c);
            return g;
        }

        /// <summary>Filled rectangle, centred at (x, y); w and h are fractions of the texture width / height.</summary>
        public TextureBaker Rect(float x, float y, float w, float h, Color c, float angle = 0f)
        {
            Shape(PrimitiveType.Quad, P(x, y), new Vector3(w * _aspect, h, 1f), angle, c);
            return this;
        }

        /// <summary>Rectangle outline of thickness <paramref name="t"/> (fraction of the height).</summary>
        public TextureBaker Frame(float x, float y, float w, float h, float t, Color c)
        {
            float tw = t / _aspect;
            Rect(x, y + h * 0.5f - t * 0.5f, w, t, c).Rect(x, y - h * 0.5f + t * 0.5f, w, t, c);
            return Rect(x - w * 0.5f + tw * 0.5f, y, tw, h, c).Rect(x + w * 0.5f - tw * 0.5f, y, tw, h, c);
        }

        /// <summary>Filled circle (or ring when <paramref name="hole"/> &gt; 0), radius as a fraction of the height.</summary>
        public TextureBaker Circle(float x, float y, float r, Color c, float hole = 0f, Color? holeColor = null)
        {
            var g = Shape(PrimitiveType.Cylinder, P(x, y), new Vector3(r * 2f, 0.001f, r * 2f), 0f, c);
            g.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            if (hole > 0f) Circle(x, y, hole, holeColor ?? _bg);
            return this;
        }

        /// <summary>Equilateral-ish triangle (warning sign), side as a fraction of the height.</summary>
        public TextureBaker Triangle(float x, float y, float size, Color c, bool pointDown = false)
        {
            var g = new GameObject("Tri") { layer = Layer };
            g.transform.SetParent(_root.transform, false);
            g.transform.position = P(x, y);
            float h = size * 0.866f, s = pointDown ? -1f : 1f;
            var mesh = new Mesh();
            mesh.vertices = new[] { new Vector3(-size * 0.5f, -h / 3f * s, 0f), new Vector3(0f, h * 2f / 3f * s, 0f), new Vector3(size * 0.5f, -h / 3f * s, 0f) };
            mesh.triangles = pointDown ? new[] { 0, 2, 1 } : new[] { 0, 1, 2 };
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            g.AddComponent<MeshRenderer>().sharedMaterial = ShapeMat(c);
            return this;
        }

        /// <summary>Text; <paramref name="height"/> is the cap height of one line as a fraction of the texture height.</summary>
        public TextureBaker Text(string s, float x, float y, float height, Color c, TextAnchor anchor = TextAnchor.MiddleCenter,
            bool bold = true, float maxWidth = 0f, float angle = 0f)
        {
            var go = new GameObject("Text") { layer = Layer };
            go.transform.SetParent(_root.transform, false);
            go.transform.position = P(x, y);
            go.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            var tm = go.AddComponent<TextMesh>();
            tm.font = _font;
            tm.fontSize = 64;
            tm.characterSize = 0.1f;
            tm.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            tm.anchor = anchor;
            tm.alignment = anchor == TextAnchor.MiddleLeft || anchor == TextAnchor.UpperLeft || anchor == TextAnchor.LowerLeft ? TextAlignment.Left
                : anchor == TextAnchor.MiddleRight || anchor == TextAnchor.UpperRight || anchor == TextAnchor.LowerRight ? TextAlignment.Right : TextAlignment.Center;
            tm.color = c;
            tm.text = s;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = _textMat;
            _texts.Add((tm, height, maxWidth * _aspect, s.Split('\n').Length));
            return this;
        }

        /// <summary>Renders everything once, adds optional grime / paper fibre, returns a mipmapped texture.</summary>
        public Texture2D Finish(string name, float grime = 0f, int seed = 1) => Finish(name, grime, seed, null);

        /// <summary>
        /// Stencil mode: draw white on a black background; the result is <paramref name="paint"/> coloured with alpha from
        /// the brightness (roughened like spray paint), for alpha-tested decals.
        /// </summary>
        public Texture2D FinishStencil(string name, Color paint, int seed = 1) => Finish(name, 0f, seed, paint);

        private Texture2D Finish(string name, float grime, int seed, Color? paint)
        {
            if (_textMat != null && _font != null) _textMat.mainTexture = _font.material.mainTexture;
            // Scale texts to the asked cap height (measured from the generated mesh).
            foreach (var (tm, height, maxWidth, lines) in _texts)
            {
                var b = tm.GetComponent<MeshRenderer>().bounds.size;
                if (b.y < 1e-5f) continue;
                float lineH = b.y / lines * 0.78f;              // bounds include descenders: cap height is ~78 % of it
                float k = height / Mathf.Max(1e-5f, lineH);
                if (maxWidth > 0f && b.x * k > maxWidth) k = maxWidth / b.x;
                tm.transform.localScale = Vector3.one * k;
            }
            if (_textMat != null && _font != null) _textMat.mainTexture = _font.material.mainTexture;

            var camGo = new GameObject("BakeCam");
            camGo.transform.position = Origin + new Vector3(_aspect * 0.5f, 0.5f, -5f);
            var cam = camGo.AddComponent<Camera>();
            cam.enabled = false;
            cam.orthographic = true;
            cam.orthographicSize = 0.5f;
            cam.aspect = _aspect;
            cam.cullingMask = 1 << Layer;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = _bg;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 20f;
            cam.allowHDR = false;
            cam.allowMSAA = false;
            var rt = RenderTexture.GetTemporary(_w * 2, _h * 2, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            cam.targetTexture = rt;
            cam.Render();

            // Downsample 2x2 (cheap antialiasing) while reading back.
            var big = new Texture2D(_w * 2, _h * 2, TextureFormat.RGBA32, false);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            big.ReadPixels(new Rect(0, 0, _w * 2, _h * 2), 0, 0);
            RenderTexture.active = prev;
            cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(_root);

            var src = big.GetPixels32();
            Object.DestroyImmediate(big);
            var px = new Color32[_w * _h];
            var rnd = new System.Random(seed);
            for (int y = 0; y < _h; y++)
            for (int x = 0; x < _w; x++)
            {
                int i0 = (y * 2) * _w * 2 + x * 2, i1 = i0 + _w * 2;
                float r = (src[i0].r + src[i0 + 1].r + src[i1].r + src[i1 + 1].r) * 0.25f;
                float g = (src[i0].g + src[i0 + 1].g + src[i1].g + src[i1 + 1].g) * 0.25f;
                float bl = (src[i0].b + src[i0 + 1].b + src[i1].b + src[i1 + 1].b) * 0.25f;
                if (grime > 0f)
                {
                    float n = SecurityTextures.Fbm(x / (float)_w * 5f + seed, y / (float)_h * 5f, 4);
                    float f = 1f - grime * (0.55f * Mathf.Clamp01(n * 1.6f - 0.35f) + 0.06f * (float)rnd.NextDouble());
                    float yellow = grime * 0.12f * (1f - y / (float)_h);
                    r *= f; g *= f * (1f - yellow * 0.3f); bl *= f * (1f - yellow);
                }
                if (paint.HasValue)
                {
                    float a = r / 255f + (SecurityTextures.Noise(x * 0.35f + seed * 7f, y * 0.35f) - 0.5f) * 0.7f;
                    if (SecurityTextures.Noise(x * 0.08f, y * 0.08f + seed) > 0.78f) a -= 0.5f;      // worn patches
                    var pc = (Color32)paint.Value;
                    px[y * _w + x] = new Color32(pc.r, pc.g, pc.b, (byte)(Mathf.Clamp01(a) * 255f));
                    continue;
                }
                px[y * _w + x] = new Color32((byte)Mathf.Clamp(r, 0, 255), (byte)Mathf.Clamp(g, 0, 255), (byte)Mathf.Clamp(bl, 0, 255), 255);
            }
            var tex = new Texture2D(_w, _h, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            tex.SetPixels32(px);
            tex.Apply(true, false);
            return tex;
        }
    }
}
