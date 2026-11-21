using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Building blocks of the security rooms: primitives with lit Standard materials (procedural textures tiled by the
    /// object size, emissive LEDs), baked signs. Renderers use no light / reflection probes, like the garage itself, so
    /// the room is exactly as dark as the rest of the level and only real lights (monitors, lamps, flashlights) show it.
    /// </summary>
    public partial class SecurityRooms
    {
        /// <summary>Surface of a part: optional procedural texture (Tile = metres per repeat), smoothness, metal, glow.</summary>
        private struct Surf
        {
            public int Tex;          // 0 = plain, else SecurityTextures.Kind + 1
            public float Tile, Smooth, Metal;
            public Color Emit;
            public Vector4? St;      // explicit texture scale / offset

            public static Surf T(SecurityTextures.Kind k, float tile, float smooth = 0.12f, float metal = 0f) =>
                new Surf { Tex = (int)k + 1, Tile = tile, Smooth = smooth, Metal = metal };
            public static Surf Shiny(float smooth, float metal = 0f) => new Surf { Smooth = smooth, Metal = metal };
            public static Surf Glow(Color e) => new Surf { Emit = e };
        }

        private readonly Dictionary<(int, int, int, bool), Material> _mats = new();
        private readonly Dictionary<Texture, Material> _signMats = new();
        private Shader _standard;

        private Material SurfMat(in Surf s)
        {
            bool glow = s.Emit.maxColorComponent > 0.001f;
            var key = (s.Tex, Mathf.RoundToInt(s.Smooth * 20f), Mathf.RoundToInt(s.Metal * 10f), glow);
            if (_mats.TryGetValue(key, out var m) && m != null) return m;
            if (_standard == null) _standard = Shader.Find("Standard");
            m = new Material(_standard) { name = "Sec_" + key };
            m.SetFloat("_Glossiness", s.Smooth);
            m.SetFloat("_Metallic", s.Metal);
            if (s.Tex > 0)
            {
                var k = (SecurityTextures.Kind)(s.Tex - 1);
                m.mainTexture = SecurityTextures.Albedo(k);
                m.SetTexture("_BumpMap", SecurityTextures.Normal(k));
                m.SetFloat("_BumpScale", 1f);
                m.EnableKeyword("_NORMALMAP");
            }
            if (glow)
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                m.SetColor("_EmissionColor", Color.white);
            }
            _mats[key] = m;
            return m;
        }

        private static void SetupRenderer(Renderer r, bool shadows = true)
        {
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            r.receiveShadows = true;
        }

        private GameObject Box(Transform parent, string n, Vector3 worldPos, Vector3 size, Color c, bool collider = true, Vector3? euler = null, Surf s = default)
            => Prim(parent, PrimitiveType.Cube, n, worldPos, size, c, euler, collider, s);

        private GameObject Prim(Transform parent, PrimitiveType type, string n, Vector3 worldPos, Vector3 scale, Color c, Vector3? euler = null, bool collider = false, Surf s = default)
        {
            var g = GameObject.CreatePrimitive(type);
            g.name = n;
            if (!collider) Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.position = worldPos;
            if (euler.HasValue) g.transform.rotation = Quaternion.Euler(euler.Value);
            g.transform.localScale = scale;
            Paint(g.GetComponent<MeshRenderer>(), c, s, scale);
            return g;
        }

        /// <summary>Sets material and per-object colour / tiling / glow of a part.</summary>
        private void Paint(Renderer r, Color c, in Surf s, Vector3 scale)
        {
            r.sharedMaterial = SurfMat(s);
            SetupRenderer(r, scale.x > 0.04f && scale.y > 0.04f && scale.z > 0.04f);
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor("_Color", c);
            if (s.Tex > 0)
            {
                // Cube faces map u/v to (x, y), (z, y) or (x, z): tile by the faces you look at.
                float a, b;
                if (scale.y < Mathf.Min(scale.x, scale.z)) { a = scale.x; b = scale.z; }   // slab: floor, desk top
                else { a = Mathf.Max(scale.x, scale.z); b = scale.y; }                    // wall, panel, post
                Vector4 st = s.St ?? new Vector4(Mathf.Max(0.05f, a / s.Tile), Mathf.Max(0.05f, b / s.Tile), 0f, 0f);
                mpb.SetVector("_MainTex_ST", st);
            }
            if (s.Emit.maxColorComponent > 0.001f) mpb.SetColor("_EmissionColor", s.Emit);
            r.SetPropertyBlock(mpb);
        }

        /// <summary>Flat sign / poster / label: a quad with a baked texture, its front facing <paramref name="facing"/>.</summary>
        private GameObject Sign(Transform parent, string n, Vector3 worldPos, Vector2 size, Vector3 facing, Texture2D tex, float smooth = 0.08f, bool cutout = false)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Quad);
            g.name = n;
            Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            // Lying flat (facing up): the top of the texture points north, readable from the south.
            g.transform.SetPositionAndRotation(worldPos, Quaternion.LookRotation(-facing, Mathf.Abs(facing.y) > 0.9f ? Vector3.forward : Vector3.up));
            g.transform.localScale = new Vector3(size.x, size.y, 1f);
            var r = g.GetComponent<MeshRenderer>();
            if (!_signMats.TryGetValue(tex, out var m) || m == null)
            {
                if (_standard == null) _standard = Shader.Find("Standard");
                m = new Material(_standard) { name = "Sign_" + tex.name, mainTexture = tex };
                m.SetFloat("_Glossiness", smooth);
                if (cutout)
                {
                    m.SetFloat("_Mode", 1f);
                    m.SetFloat("_Cutoff", 0.5f);
                    m.EnableKeyword("_ALPHATEST_ON");
                    m.renderQueue = (int)RenderQueue.AlphaTest;
                }
                _signMats[tex] = m;
            }
            r.sharedMaterial = m;
            SetupRenderer(r, false);
            return g;
        }

        /// <summary>A tiny glowing part (LED, lit button) that gives no light of its own.</summary>
        private GameObject Led(Transform parent, string n, Vector3 worldPos, Vector3 size, Color glow, PrimitiveType type = PrimitiveType.Cube)
        {
            var g = Prim(parent, type, n, worldPos, size, glow * 0.3f, null, false, Surf.Glow(glow));
            g.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            return g;
        }

        private static Light AddLight(Transform parent, string n, Vector3 pos, LightType type, float range, float intensity, Color color, LightShadows shadows = LightShadows.None)
        {
            var l = new GameObject(n).AddComponent<Light>();
            l.transform.SetParent(parent, false);
            l.transform.position = pos;
            l.type = type;
            l.range = range;
            l.intensity = intensity;
            l.color = color;
            l.shadows = shadows;
            l.renderMode = LightRenderMode.ForcePixel;
            return l;
        }
    }
}
