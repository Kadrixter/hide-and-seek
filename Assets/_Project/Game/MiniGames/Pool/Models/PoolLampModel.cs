using UnityEngine;

namespace HideAndSeek.Game.MiniGames.Pool
{
    /// <summary>
    /// Pool-table lamp hanging over the table: a bar along X with three shades, hung from the ceiling, three real warm
    /// spot lights shining down on the cloth. Style 0: walnut beam with brass trim, green cased-glass banker's domes
    /// (white opal inside), brass chains to two ceiling canopies, a fabric cable woven down one chain. Style 1: black
    /// steel tube with brass knobs, black enamelled cones, black down-rods on swivel canopies.
    /// Space: TABLE SPACE (origin on the floor under the centre of the cloth, long axis X), the mounts at <c>ceilingY</c>.
    /// </summary>
    public sealed partial class PoolLampModel
    {
        public Transform Root;
        /// <summary>The three spot lights (empty when built without lights).</summary>
        public Light[] Lights;

        /// <summary>Height of the shade rims above the cloth.</summary>
        public const float RimAboveCloth = 0.97f;
        public const float RimY = PoolDims.ClothY + RimAboveCloth;
        /// <summary>X of the three shades.</summary>
        public static readonly float[] ShadeX = { -0.64f, 0f, 0.64f };
        /// <summary>X of the two hangers (chains / down-rods).</summary>
        public const float HangX = 0.72f;
        /// <summary>Bulb centre above the rim (the spot lights sit here).</summary>
        public const float BulbAboveRim = 0.062f;

        static readonly Color LightColor = new Color(1f, 0.86f, 0.68f);
        public const float LightIntensity = 2.1f, LightRange = 3.2f;

        TableKit _kit;
        Material _bulb, _inner, _glass;
        int _style;
        bool _on = true;

        /// <param name="parent">Parent in table space.</param>
        /// <param name="template">Standard material template.</param>
        /// <param name="style">0 = green glass banker's domes on a walnut beam, 1 = black metal cones on a steel tube.</param>
        /// <param name="ceilingY">Height of the ceiling (where the canopies are screwed on).</param>
        /// <param name="lights">Add the three real spot lights.</param>
        public static PoolLampModel Build(Transform parent, Material template, int style, float ceilingY, bool lights)
        {
            var lamp = new PoolLampModel { _style = style == 1 ? 1 : 0 };
            var root = new GameObject("PoolLamp").transform;
            root.SetParent(parent, false);
            root.gameObject.layer = parent != null ? parent.gameObject.layer : 0;
            lamp.Root = root;
            lamp._kit = new TableKit(template, root.gameObject.layer, 0);
            lamp.MakeMaterials();

            float barBottom = lamp.BuildBar(root);
            lamp.BuildHangers(root, barBottom + lamp.BarHeight, Mathf.Max(ceilingY, barBottom + lamp.BarHeight + 0.12f));
            for (int i = 0; i < ShadeX.Length; i++) lamp.BuildShade(root, ShadeX[i], barBottom);

            lamp.Lights = new Light[lights ? ShadeX.Length : 0];
            for (int i = 0; i < lamp.Lights.Length; i++)
            {
                var g = new GameObject("LampSpot" + i) { layer = root.gameObject.layer };
                g.transform.SetParent(root, false);
                g.transform.localPosition = new Vector3(ShadeX[i], RimY + BulbAboveRim, 0f);
                g.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var l = g.AddComponent<Light>();
                l.type = LightType.Spot;
                l.color = LightColor;
                l.intensity = LightIntensity;
                l.range = LightRange;
                l.spotAngle = 118f;
                l.innerSpotAngle = 64f;
                l.shadows = LightShadows.Soft;
                l.shadowStrength = 0.85f;
                // Small bias: the balls sit 3 cm above the cloth, the default bias would wipe their contact shadows out.
                l.shadowBias = 0.003f;
                l.shadowNormalBias = 0.05f;
                l.shadowNearPlane = 0.15f;
                l.shadowResolution = UnityEngine.Rendering.LightShadowResolution.VeryHigh;
                l.renderMode = LightRenderMode.ForcePixel;
                lamp.Lights[i] = l;
            }
            lamp.SetOn(true);
            return lamp;
        }

        /// <summary>Switches the spot lights and the glow of the bulbs / shades.</summary>
        public void SetOn(bool on) => SetLevel(on ? 1f : 0f);

        /// <summary>
        /// Brightness 0..1 (fades): spot intensity and the glow of bulbs / shades; the bulbs turn orange as they dim,
        /// the lights are disabled when dark.
        /// </summary>
        public void SetLevel(float level)
        {
            level = Mathf.Clamp01(level);
            _on = level > 0.002f;
            float warm = Mathf.Clamp01(level * 1.4f);
            var lc = Color.Lerp(new Color(1f, 0.6f, 0.35f), LightColor, warm);
            if (Lights != null)
                foreach (var l in Lights)
                    if (l != null)
                    {
                        l.enabled = _on;
                        l.intensity = LightIntensity * level;
                        l.color = lc;
                    }
            var tint = Color.Lerp(new Color(1f, 0.38f, 0.1f), new Color(1f, 0.82f, 0.56f), warm);
            SetGlow(_bulb, tint * (2.3f * level));
            SetGlow(_inner, new Color(1f, 0.9f, 0.74f) * (0.95f * level));
            SetGlow(_glass, _style == 0 ? new Color(0.03f, 0.16f, 0.06f) * level : Color.black);
            if (_bulb != null) _bulb.color = Color.Lerp(new Color(0.75f, 0.74f, 0.7f), new Color(1f, 0.95f, 0.85f), level);
        }

        public bool IsOn => _on;

        static void SetGlow(Material m, Color c)
        {
            if (m == null) return;
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            m.SetColor("_EmissionColor", c);
        }

        void MakeMaterials()
        {
            _bulb = _kit.Mat(new Color(1f, 0.95f, 0.85f), 0f, 0.9f);
            _bulb.SetTexture("_EmissionMap", InnerGradient); // brightest at the tip, warmer towards the neck
            _inner = _kit.Mat(new Color(0.93f, 0.92f, 0.88f), 0f, _style == 1 ? 0.55f : 0.75f);
            _inner.SetTexture("_EmissionMap", InnerGradient);
            _glass = _style == 1
                ? _kit.Mat(new Color(0.03f, 0.03f, 0.034f), 0.55f, 0.62f)
                : _kit.Mat(new Color(0.03f, 0.3f, 0.11f), 0.05f, 0.93f);
        }

        static Texture2D _gradient;

        /// <summary>Emission map of the shade inside: bright round the bulb (v = 0, top) fading towards the rim (v = 1).</summary>
        static Texture2D InnerGradient
        {
            get
            {
                if (_gradient != null) return _gradient;
                _gradient = new Texture2D(4, 64, TextureFormat.RGBA32, false) { name = "PoolShadeGlow", wrapMode = TextureWrapMode.Clamp };
                var px = new Color[4 * 64];
                for (int y = 0; y < 64; y++)
                {
                    float v = y / 63f;
                    float g = Mathf.Lerp(1f, 0.16f, Mathf.Pow(v, 0.7f));
                    for (int x = 0; x < 4; x++) px[y * 4 + x] = new Color(g, g, g, 1f);
                }
                _gradient.SetPixels(px);
                _gradient.Apply(false);
                return _gradient;
            }
        }

        /// <summary>One shade with its crown, stem up to the bar, socket and bulb.</summary>
        void BuildShade(Transform root, float x, float barBottom)
        {
            var s = new GameObject("Shade") { layer = root.gameObject.layer }.transform;
            s.SetParent(root, false);
            s.localPosition = new Vector3(x, RimY, 0f);
            _kit.Part("Outer", s, PoolLampMeshes.ShadeOuter(_style), _glass, Vector3.zero);
            _kit.Part("Inner", s, PoolLampMeshes.ShadeInner(_style), _inner, Vector3.zero, Quaternion.identity, false);
            _kit.Part("Rim", s, PoolLampMeshes.ShadeRim(_style), _style == 1 ? _glass : _kit.Brass, Vector3.zero);
            _kit.Part("Crown", s, PoolLampMeshes.ShadeCrown(_style), _kit.Brass, Vector3.zero);
            float top = _style == 1 ? 0.13f : 0.17f;
            _kit.Part("Socket", s, PoolLampMeshes.Socket(top), _style == 1 ? _kit.Dark : _kit.Brass, new Vector3(0f, 0.02f, 0f), Quaternion.identity, false);
            _kit.Part("Bulb", s, PoolLampMeshes.BulbGlass(), _bulb, new Vector3(0f, 0.02f, 0f), Quaternion.identity, false);
            // Stem from the crown up into the bar, with a collar on the crown and a cup under the bar.
            float crownTop = PoolLampMeshes.ShadeHeight(_style);
            float len = barBottom - RimY - crownTop + 0.01f;
            _kit.Part("Stem", s, PoolLampMeshes.Rod(0.0065f, len), _kit.Brass, new Vector3(0f, crownTop - 0.004f, 0f));
            _kit.Part("StemNut", s, PoolLampMeshes.Rod(0.011f, 0.012f), _kit.Brass, new Vector3(0f, crownTop - 0.003f, 0f));
            _kit.Part("StemCup", s, StemCup(), _kit.Brass, new Vector3(0f, barBottom - RimY, 0f));
        }

        static Mesh StemCup()
        {
            var p = new System.Collections.Generic.List<Vector2>
            {
                new(0f, -0.022f), new(0.008f, -0.022f), new(0.009f, -0.019f), new(0.009f, -0.016f), new(0.013f, -0.013f),
                new(0.02f, -0.006f), new(0.022f, -0.002f), new(0.022f, 0.001f), new(0f, 0.001f),
            };
            return BoardMeshes.Lathe("poolstemcup", p, 28, 6f);
        }
    }
}
