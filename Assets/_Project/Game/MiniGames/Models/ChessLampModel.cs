using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// Pendant lamp over the chess table: a turned brass ceiling rose with a cord grip, a braided oxblood fabric cable,
    /// a brass lamp holder with a knurled ring, a deep enamel dome (oxblood outside like the stools' leather, white
    /// enamel inside) with a brass gallery band and a rolled brass rim, and a clear globe bulb inside. One warm spot
    /// light shines down from the bulb onto the board (off until <see cref="ChessLamp"/> switches it on).
    /// Space: TABLE SPACE (origin on the floor under the board centre), the rose at <c>ceilingY</c>.
    /// </summary>
    public sealed class ChessLampModel
    {
        /// <summary>Height of the shade rim above the floor (about 1 m above the board, above a standing player's eye line at the table).</summary>
        public const float RimY = 1.69f;
        /// <summary>Radius of the shade rim.</summary>
        public const float RimR = 0.215f;
        /// <summary>Bulb centre above the rim (the spot light sits here).</summary>
        public const float BulbAboveRim = 0.085f;
        const float ShadeH = 0.2f, NeckR = 0.042f;

        public Transform Root;
        public Light Light;
        /// <summary>Glass of the bulb, white enamel inside the shade (their emission follows the light).</summary>
        public Material Bulb, Inner, Filament;
        TableKit _kit;

        public static ChessLampModel Build(Transform parent, Material template, float ceilingY)
        {
            var m = new ChessLampModel();
            var root = new GameObject("ChessLamp") { layer = parent.gameObject.layer }.transform;
            root.SetParent(parent, false);
            m.Root = root;
            m._kit = new TableKit(template, root.gameObject.layer, 0);
            var k = m._kit;
            var enamel = k.Mat(new Color(0.34f, 0.055f, 0.045f), 0.25f, 0.86f);
            m.Inner = k.Mat(new Color(0.92f, 0.9f, 0.85f), 0f, 0.8f);
            m.Inner.SetTexture("_EmissionMap", InnerGlow);
            m.Bulb = k.Mat(new Color(0.85f, 0.83f, 0.78f), 0f, 0.95f);
            m.Filament = k.Mat(new Color(0.25f, 0.2f, 0.15f), 0.6f, 0.6f);
            var cable = k.Mat(new Color(0.2f, 0.07f, 0.055f), 0f, 0.3f, BraidTex);
            cable.mainTextureScale = new Vector2(1f, 60f);

            float c = Mathf.Max(ceilingY, RimY + 0.6f);
            // ---- ceiling rose + cord grip
            k.Part("Rose", root, Lathe("chlamp_rose", new[] { (0f, -0.034f), (0.011f, -0.034f), (0.016f, -0.031f), (0.022f, -0.026f), (0.045f, -0.019f), (0.064f, -0.011f), (0.074f, -0.005f), (0.077f, -0.002f), (0.077f, 0f), (0f, 0f) }, 36), k.Brass, new Vector3(0f, c, 0f));
            k.Part("RoseRing", root, Lathe("chlamp_rosering", Ring(0.07f, 0.0025f), 36), k.Brass, new Vector3(0f, c - 0.008f, 0f));
            k.Part("CordGrip", root, Knurled("chlamp_grip", 0.0075f, 0.032f, 4), k.Brass, new Vector3(0f, c - 0.066f, 0f));
            // ---- cable (straight, slightly heavier than a wire: braided fabric)
            float holderTop = RimY + ShadeH + 0.07f;
            float cableTop = c - 0.06f, cableLen = cableTop - holderTop + 0.01f;
            k.Part("Cable", root, BoardMeshes.Lathe("chlamp_cable" + cableLen.ToString("0.000"), V(new[] { (0f, 0f), (0.0034f, 0f), (0.0034f, cableLen), (0f, cableLen) }), 10, 1f), cable, new Vector3(0f, holderTop - 0.01f, 0f));
            // ---- lamp holder: cord grip cap, knurled ring, body down into the neck of the shade
            k.Part("HolderCap", root, Lathe("chlamp_cap", new[] { (0f, 0f), (0.019f, 0f), (0.02f, 0.004f), (0.017f, 0.012f), (0.009f, 0.02f), (0.006f, 0.026f), (0f, 0.026f) }, 28), k.Brass, new Vector3(0f, holderTop - 0.022f, 0f));
            k.Part("HolderRing", root, Knurled("chlamp_holder", 0.021f, 0.016f, 6), k.Brass, new Vector3(0f, holderTop - 0.04f, 0f));
            k.Part("HolderBody", root, Lathe("chlamp_body", new[] { (0f, 0f), (0.017f, 0f), (0.018f, 0.085f), (0f, 0.085f) }, 24), k.Brass, new Vector3(0f, RimY + ShadeH - 0.05f, 0f));
            // ---- shade
            var s = new GameObject("Shade") { layer = root.gameObject.layer }.transform;
            s.SetParent(root, false);
            s.localPosition = new Vector3(0f, RimY, 0f);
            k.Part("Outer", s, ShadeOuter(), enamel, Vector3.zero);
            k.Part("Inner", s, ShadeInner(), m.Inner, Vector3.zero, Quaternion.identity, false);
            k.Part("Rim", s, Lathe("chlamp_rim", Ring(RimR + 0.001f, 0.0042f), 56), k.Brass, Vector3.zero);
            k.Part("Gallery", s, Lathe("chlamp_gallery", new[] { (NeckR - 0.003f, 0f), (NeckR + 0.001f, 0.002f), (NeckR + 0.001f, 0.012f), (NeckR - 0.003f, 0.014f) }, 40), k.Brass, new Vector3(0f, ShadeH - 0.024f, 0f));
            // ---- bulb: socket shell, globe, a hairpin filament
            k.Part("Socket", s, Lathe("chlamp_socket", new[] { (0f, 0f), (0.014f, 0f), (0.016f, 0.006f), (0.016f, 0.05f), (0f, 0.05f) }, 20), k.Dark, new Vector3(0f, BulbAboveRim + 0.035f, 0f), Quaternion.identity, false);
            k.Part("Bulb", s, BulbGlass(), m.Bulb, new Vector3(0f, BulbAboveRim, 0f), Quaternion.identity, false);
            k.Part("Filament", s, Lathe("chlamp_fil", Ring(0.007f, 0.0009f), 14), m.Filament, new Vector3(0f, BulbAboveRim + 0.004f, 0f), Quaternion.Euler(90f, 0f, 0f), false);

            var g = new GameObject("ChessLampSpot") { layer = root.gameObject.layer };
            g.transform.SetParent(root, false);
            g.transform.localPosition = new Vector3(0f, RimY + BulbAboveRim, 0f);
            g.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var l = g.AddComponent<Light>();
            l.type = LightType.Spot;
            l.color = new Color(1f, 0.82f, 0.6f);
            l.range = 3.4f;
            l.spotAngle = 104f;
            l.innerSpotAngle = 46f;
            l.shadows = LightShadows.Soft;
            l.shadowStrength = 0.8f;
            l.shadowBias = 0.004f;
            l.shadowNormalBias = 0.06f;
            l.shadowNearPlane = 0.12f;
            l.shadowResolution = UnityEngine.Rendering.LightShadowResolution.Medium;
            l.renderMode = LightRenderMode.ForcePixel;
            l.intensity = 0f;
            l.enabled = false;
            m.Light = l;
            return m;
        }

        static IList<Vector2> V(IEnumerable<(float r, float y)> pts)
        {
            var l = new List<Vector2>();
            foreach (var (r, y) in pts) l.Add(new Vector2(r, y));
            return l;
        }

        static Mesh Lathe(string key, (float, float)[] pts, int seg) => BoardMeshes.Lathe(key, V(pts), seg, 6f);
        static Mesh Lathe(string key, IList<Vector2> pts, int seg) => BoardMeshes.Lathe(key, pts, seg, 6f);

        /// <summary>Closed round bead (torus section) of radius <paramref name="r"/> round the axis at <paramref name="at"/>.</summary>
        static List<Vector2> Ring(float at, float r)
        {
            var p = new List<Vector2>();
            BoardMeshes.Arc(p, at, 0f, r, -90f, 270f, 12);
            return p;
        }

        /// <summary>Cylinder with <paramref name="ribs"/> rounded ribs (knurled brass ring), standing on y = 0.</summary>
        static Mesh Knurled(string key, float r, float h, int ribs)
        {
            var p = new List<Vector2> { new(0f, 0f), new(r - 0.002f, 0f) };
            for (int i = 0; i <= ribs * 4; i++)
            {
                float t = i / (float)(ribs * 4);
                p.Add(new Vector2(r - 0.0012f * (1f - Mathf.Abs(Mathf.Sin(t * ribs * Mathf.PI))), Mathf.Lerp(0.001f, h - 0.001f, t)));
            }
            p.Add(new Vector2(r - 0.002f, h));
            p.Add(new Vector2(0f, h));
            return Lathe(key, p, 28);
        }

        /// <summary>Deep dome: flared skirt at the rim, rounded shoulder, a short neck round the holder.</summary>
        static Mesh ShadeOuter()
        {
            var p = new List<Vector2> { new(RimR, 0f), new(RimR, 0.004f) };
            for (int i = 1; i <= 18; i++)
            {
                float t = i / 18f;
                float a = t * 0.5f * Mathf.PI;
                // Superellipse quarter: almost straight flank, round shoulder.
                float r = NeckR + (RimR - 0.004f - NeckR) * Mathf.Pow(Mathf.Cos(a), 0.75f);
                float y = 0.006f + (ShadeH - 0.04f) * Mathf.Pow(Mathf.Sin(a), 1.6f);
                p.Add(new Vector2(r, y));
            }
            p.Add(new Vector2(NeckR - 0.004f, ShadeH - 0.028f));
            p.Add(new Vector2(NeckR - 0.004f, ShadeH));
            p.Add(new Vector2(0f, ShadeH));
            return Lathe("chlamp_out", p, 56);
        }

        /// <summary>White enamel inside, from the neck down to the rim (normals inwards; v from the top).</summary>
        static Mesh ShadeInner()
        {
            var p = new List<Vector2> { new(0f, ShadeH - 0.04f), new(NeckR - 0.008f, ShadeH - 0.04f) };
            for (int i = 17; i >= 0; i--)
            {
                float t = i / 18f;
                float a = t * 0.5f * Mathf.PI;
                float r = NeckR - 0.004f + (RimR - 0.008f - NeckR) * Mathf.Pow(Mathf.Cos(a), 0.75f);
                float y = 0.004f + (ShadeH - 0.046f) * Mathf.Pow(Mathf.Sin(a), 1.6f);
                p.Add(new Vector2(r, y));
            }
            return BoardMeshes.Lathe("chlamp_in", p, 56, 1f / 0.32f);
        }

        /// <summary>Clear globe bulb (G95-ish) with its neck going up into the socket; centre at y = 0.</summary>
        static Mesh BulbGlass()
        {
            var p = new List<Vector2> { new(0f, -0.046f) };
            BoardMeshes.Arc(p, 0f, 0f, 0.046f, -88f, 62f, 18);
            p.Add(new Vector2(0.016f, 0.05f));
            p.Add(new Vector2(0.014f, 0.06f));
            p.Add(new Vector2(0f, 0.06f));
            return BoardMeshes.Lathe("chlamp_bulb", p, 32, 4f);
        }

        static Texture2D _innerGlow, _braid;

        /// <summary>Emission map of the white enamel: bright round the bulb (top), fading towards the rim.</summary>
        static Texture2D InnerGlow
        {
            get
            {
                if (_innerGlow != null) return _innerGlow;
                _innerGlow = new Texture2D(4, 64, TextureFormat.RGBA32, false) { name = "ChessLampGlow", wrapMode = TextureWrapMode.Clamp };
                var px = new Color[4 * 64];
                for (int y = 0; y < 64; y++)
                {
                    float g = Mathf.Lerp(1f, 0.2f, Mathf.Pow(y / 63f, 0.8f));
                    for (int x = 0; x < 4; x++) px[y * 4 + x] = new Color(g, g, g, 1f);
                }
                _innerGlow.SetPixels(px);
                _innerGlow.Apply(false);
                return _innerGlow;
            }
        }

        /// <summary>Diagonal braid of the fabric cable (two-tone twill).</summary>
        static Texture2D BraidTex
        {
            get
            {
                if (_braid != null) return _braid;
                _braid = new Texture2D(32, 32, TextureFormat.RGBA32, false) { name = "ChessLampBraid", wrapMode = TextureWrapMode.Repeat };
                var px = new Color[32 * 32];
                for (int y = 0; y < 32; y++)
                    for (int x = 0; x < 32; x++)
                    {
                        float a = Mathf.Sin((x + y) / 32f * Mathf.PI * 8f), b = Mathf.Sin((x - y) / 32f * Mathf.PI * 8f);
                        float v = 0.75f + 0.25f * (((x / 4 + y / 4) & 1) == 0 ? a : b);
                        px[y * 32 + x] = new Color(v, v, v, 1f);
                    }
                _braid.SetPixels(px);
                _braid.Apply(true);
                return _braid;
            }
        }
    }
}
