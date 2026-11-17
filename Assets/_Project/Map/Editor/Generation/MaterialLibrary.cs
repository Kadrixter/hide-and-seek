using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// Creates (or updates in place, keeping GUIDs) every Standard-shader material of the garage and
    /// remembers the world-space tiling size each material expects for its UVs.
    /// </summary>
    internal sealed class MaterialLibrary
    {
        public const string Folder = "Assets/_Project/Map/Materials";

        public enum Mode { Opaque, Cutout, Fade, Transparent }

        private readonly Dictionary<string, Material> _byName = new Dictionary<string, Material>();
        private readonly Dictionary<Material, float> _tiles = new Dictionary<Material, float>();

        public static readonly string[] CarPaints =
        {
            "CarPaint_Red", "CarPaint_Blue", "CarPaint_White", "CarPaint_Black", "CarPaint_Silver",
            "CarPaint_Green", "CarPaint_Yellow", "CarPaint_Orange", "CarPaint_Maroon", "CarPaint_Navy",
            "CarPaint_Grey", "CarPaint_Teal"
        };

        public Material this[string name] =>
            _byName.TryGetValue(name, out var m) ? m : throw new KeyNotFoundException("Material " + name);

        public float Tile(Material m) => m != null && _tiles.TryGetValue(m, out var t) ? t : 1f;

        public static MaterialLibrary CreateAll()
        {
            System.IO.Directory.CreateDirectory(GarageTextures.FullPath(Folder));
            AssetDatabase.Refresh();
            var lib = new MaterialLibrary();
            lib.Build();
            AssetDatabase.SaveAssets();
            return lib;
        }

        private void Build()
        {
            var white = Color.white;
            // --- structure
            Make("Floor_Concrete", white, "Floor_Concrete", 4f, 0f, 0.32f, "Floor_Concrete_N", 1f);
            Make("Ramp_Concrete", new Color(0.85f, 0.85f, 0.85f), "Floor_Concrete", 3f, 0f, 0.28f, "Floor_Concrete_N", 1.2f);
            Make("Wall_Concrete", white, "Wall_Concrete", 3f, 0f, 0.12f, "Wall_Concrete_N", 1f);
            Make("Ceiling_Concrete", new Color(0.62f, 0.62f, 0.64f), "Wall_Concrete", 3f, 0f, 0.08f, "Wall_Concrete_N", 0.8f);
            Make("Column_Concrete", new Color(0.93f, 0.93f, 0.92f), "Wall_Concrete", 2f, 0f, 0.12f, "Wall_Concrete_N", 1f);
            Make("Stair_Concrete", new Color(0.8f, 0.8f, 0.8f), "Floor_Concrete", 2f, 0f, 0.25f, "Floor_Concrete_N", 1f);
            Make("Tile_Floor", white, "Tile_Floor", 1.2f, 0f, 0.55f, "Tile_Floor_N", 1f);
            Make("Hazard", white, "Hazard_Stripes", 0.5f, 0f, 0.35f);
            Make("Concrete_Yellow", new Color(1f, 0.8f, 0.25f), "Wall_Concrete", 1f, 0f, 0.2f, "Wall_Concrete_N", 1f);
            // --- paint (worn, tinted)
            Make("Paint_White", new Color(0.95f, 0.95f, 0.92f), "Paint_Worn", 1f, 0f, 0.38f);
            Make("Paint_Yellow", new Color(1f, 0.76f, 0.1f), "Paint_Worn", 1f, 0f, 0.38f);
            Make("Paint_Blue", new Color(0.2f, 0.42f, 0.85f), "Paint_Worn", 1.5f, 0f, 0.3f);
            Make("Paint_Orange", new Color(0.95f, 0.45f, 0.1f), "Paint_Worn", 1.5f, 0f, 0.3f);
            Make("Paint_Green", new Color(0.25f, 0.6f, 0.32f), "Paint_Worn", 1.5f, 0f, 0.3f);
            Make("Paint_Grey", new Color(0.55f, 0.56f, 0.58f), "Paint_Worn", 1.5f, 0f, 0.25f);
            // --- floor decals
            Make("Text_Floor", white, "Glyphs_Worn", 1f, 0f, 0.35f, mode: Mode.Cutout);
            Make("Text_FloorYellow", new Color(1f, 0.78f, 0.12f), "Glyphs_Worn", 1f, 0f, 0.35f, mode: Mode.Cutout);
            Make("Text_Black", new Color(0.04f, 0.04f, 0.04f), "Glyphs", 1f, 0f, 0.3f, mode: Mode.Cutout);
            Make("Text_White", white, "Glyphs", 1f, 0f, 0.4f, mode: Mode.Cutout);
            Make("Text_Glow", white, "Glyphs", 1f, 0f, 0.5f, mode: Mode.Cutout, emission: new Color(1.4f, 1.4f, 1.4f), emissionTex: "Glyphs");
            Make("Text_GlowRed", new Color(1f, 0.1f, 0.05f), "Glyphs", 1f, 0f, 0.5f, mode: Mode.Cutout, emission: new Color(0.9f, 0.05f, 0.02f), emissionTex: "Glyphs");
            Make("Arrow_White", white, "Arrow", 1f, 0f, 0.35f, mode: Mode.Cutout);
            Make("Hatch_Yellow", new Color(1f, 0.76f, 0.1f), "Hatch", 1f, 0f, 0.35f, mode: Mode.Cutout);
            Make("Puddle", new Color(0.02f, 0.025f, 0.03f, 0.92f), "Puddle", 1f, 0f, 0.97f, mode: Mode.Transparent);
            Make("Flood_Water", new Color(0.035f, 0.045f, 0.05f, 0.9f), "Flood", 1f, 0.1f, 0.98f, mode: Mode.Transparent);
            Make("Oil_Stain", white, "Oil_Stain", 1f, 0f, 0.75f, mode: Mode.Fade);
            Make("Crack", white, "Crack", 1f, 0f, 0.1f, mode: Mode.Fade);
            Make("Grate", white, "Grate", 0.5f, 0.6f, 0.35f);
            // --- signs
            Make("Sign_White", new Color(0.85f, 0.85f, 0.82f), null, 1f, 0f, 0.45f);
            Make("Sign_Blue", new Color(0.08f, 0.22f, 0.6f), null, 1f, 0f, 0.45f);
            Make("Sign_Yellow", new Color(0.95f, 0.72f, 0.08f), null, 1f, 0f, 0.45f);
            Make("Sign_Red", new Color(0.7f, 0.06f, 0.05f), null, 1f, 0f, 0.45f);
            Make("Exit_Green", new Color(0.05f, 0.5f, 0.15f), null, 1f, 0f, 0.6f, emission: new Color(0.03f, 0.75f, 0.2f));
            Make("Sign_Dark", new Color(0.03f, 0.03f, 0.03f), null, 1f, 0f, 0.7f);
            // --- metals
            Make("Metal_Galvanized", new Color(0.85f, 0.86f, 0.88f), "Metal_Brushed", 1f, 0.65f, 0.42f, "Metal_Brushed_N", 0.5f);
            Make("Metal_Steel", new Color(0.9f, 0.9f, 0.92f), "Metal_Brushed", 1.5f, 0.85f, 0.62f, "Metal_Brushed_N", 0.4f);
            Make("Metal_Dark", new Color(0.35f, 0.35f, 0.37f), "Metal_Brushed", 1f, 0.6f, 0.35f, "Metal_Brushed_N", 0.5f);
            Make("Shutter", new Color(0.8f, 0.82f, 0.85f), "Shutter", 1f, 0.55f, 0.35f, "Shutter_N", 1f);
            Make("Wire_Mesh", new Color(0.85f, 0.85f, 0.88f), "Wire_Mesh", 0.5f, 0.6f, 0.4f, mode: Mode.Cutout);
            // --- painted metals
            Make("PM_Red", new Color(0.75f, 0.08f, 0.06f), "Painted_Metal", 1f, 0.15f, 0.45f);
            Make("PM_Green", new Color(0.12f, 0.36f, 0.2f), "Painted_Metal", 1f, 0.15f, 0.35f);
            Make("PM_Blue", new Color(0.12f, 0.25f, 0.55f), "Painted_Metal", 1f, 0.15f, 0.4f);
            Make("PM_Grey", new Color(0.55f, 0.57f, 0.6f), "Painted_Metal", 1f, 0.2f, 0.4f);
            Make("PM_DarkGrey", new Color(0.22f, 0.23f, 0.25f), "Painted_Metal", 1f, 0.2f, 0.35f);
            Make("PM_Yellow", new Color(0.95f, 0.7f, 0.08f), "Painted_Metal", 1f, 0.1f, 0.4f);
            Make("PM_Orange", new Color(0.95f, 0.38f, 0.06f), "Painted_Metal", 1f, 0.05f, 0.4f);
            Make("PM_White", new Color(0.85f, 0.85f, 0.83f), "Painted_Metal", 1f, 0.1f, 0.45f);
            Make("PM_Teal", new Color(0.1f, 0.42f, 0.45f), "Painted_Metal", 1f, 0.15f, 0.4f);
            // --- misc props
            Make("Wood", white, "Wood", 1f, 0f, 0.12f);
            Make("Cardboard", white, "Cardboard", 1f, 0f, 0.08f);
            Make("Rubber", new Color(0.045f, 0.045f, 0.05f), null, 1f, 0f, 0.25f);
            Make("Plastic_Black", new Color(0.06f, 0.06f, 0.065f), null, 1f, 0f, 0.45f);
            Make("Glass_Dark", new Color(0.02f, 0.025f, 0.03f), null, 1f, 0.3f, 0.95f);
            Make("Fixture_Dead", new Color(0.72f, 0.72f, 0.7f), null, 1f, 0f, 0.55f);
            Make("Lamp_Flicker", new Color(0.85f, 0.83f, 0.78f), null, 1f, 0f, 0.6f, emission: new Color(1f, 0.86f, 0.62f) * 1.4f);
            Make("Lamp_Exit", new Color(0.85f, 0.85f, 0.85f), null, 1f, 0f, 0.5f);
            // --- zones / level -3 (older, damp)
            Make("Floor_Concrete_Old", new Color(0.7f, 0.71f, 0.66f), "Floor_Concrete", 4f, 0f, 0.42f, "Floor_Concrete_N", 1.5f);
            Make("Wall_Concrete_Old", new Color(0.66f, 0.7f, 0.63f), "Wall_Concrete", 3f, 0f, 0.2f, "Wall_Concrete_N", 1.4f);
            Make("Ceiling_Concrete_Old", new Color(0.42f, 0.45f, 0.41f), "Wall_Concrete", 3f, 0f, 0.12f, "Wall_Concrete_N", 1.2f);
            Make("Column_Concrete_Old", new Color(0.7f, 0.72f, 0.66f), "Wall_Concrete", 2f, 0f, 0.15f, "Wall_Concrete_N", 1.4f);
            Make("Hazard_Old", new Color(0.62f, 0.6f, 0.52f), "Hazard_Stripes", 0.5f, 0f, 0.3f);
            Make("Paint_Red", new Color(0.62f, 0.12f, 0.1f), "Paint_Worn", 1.5f, 0f, 0.3f);
            Make("Tile_Wash", new Color(0.7f, 0.82f, 0.95f), "Tile_Floor", 1.2f, 0f, 0.75f, "Tile_Floor_N", 1f);
            Make("Brush_Blue", new Color(0.1f, 0.3f, 0.8f), "Tarp", 0.5f, 0f, 0.2f);
            Make("Tarp_Blue", new Color(0.2f, 0.38f, 0.75f), "Tarp", 1.5f, 0f, 0.45f, "Tarp_N", 1f);
            Make("Tarp_Grey", new Color(0.55f, 0.57f, 0.55f), "Tarp", 1.5f, 0f, 0.35f, "Tarp_N", 1f);
            Make("Rubble", new Color(0.8f, 0.8f, 0.78f), "Wall_Concrete", 1f, 0f, 0.1f, "Wall_Concrete_N", 1.5f);
            Make("Rust", new Color(0.55f, 0.3f, 0.16f), "Painted_Metal", 1f, 0.3f, 0.25f);
            Make("Sack", new Color(0.82f, 0.82f, 0.78f), "Cardboard", 1f, 0f, 0.1f);            // --- car parts
            Make("Car_Headlight", new Color(0.9f, 0.9f, 0.85f), null, 1f, 0.4f, 0.92f);
            Make("Car_Taillight", new Color(0.55f, 0.03f, 0.02f), null, 1f, 0.2f, 0.85f);
            Make("Car_Indicator", new Color(0.9f, 0.45f, 0.05f), null, 1f, 0.2f, 0.85f);
            Make("Car_Plate", new Color(0.88f, 0.88f, 0.85f), null, 1f, 0f, 0.5f);
            Make("Car_Chrome", new Color(0.8f, 0.8f, 0.82f), "Metal_Brushed", 1f, 0.95f, 0.85f);
            Make("Car_Trim", new Color(0.07f, 0.07f, 0.075f), null, 1f, 0.1f, 0.35f);
            var paints = new[]
            {
                new Color(0.62f, 0.04f, 0.04f), new Color(0.06f, 0.18f, 0.55f), new Color(0.85f, 0.85f, 0.83f),
                new Color(0.03f, 0.03f, 0.035f), new Color(0.6f, 0.62f, 0.64f), new Color(0.06f, 0.3f, 0.14f),
                new Color(0.9f, 0.68f, 0.05f), new Color(0.85f, 0.32f, 0.04f), new Color(0.35f, 0.05f, 0.08f),
                new Color(0.04f, 0.07f, 0.2f), new Color(0.3f, 0.31f, 0.33f), new Color(0.05f, 0.38f, 0.4f)
            };
            for (int i = 0; i < CarPaints.Length; i++)
                Make(CarPaints[i], paints[i], null, 1f, 0.55f, 0.78f);
        }

        private Material Make(string name, Color color, string tex, float tile, float metallic, float smoothness,
            string normal = null, float bumpScale = 1f, Mode mode = Mode.Opaque, Color? emission = null,
            string emissionTex = null)
        {
            string path = $"{Folder}/{name}.mat";
            var shader = Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("Standard shader not found");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = mat == null;
            if (created) mat = new Material(shader);
            mat.shader = shader;
            mat.shaderKeywords = Array.Empty<string>();
            mat.name = name;
            mat.SetColor("_Color", color);
            mat.SetTexture("_MainTex", tex != null ? GarageTextures.Load(tex) : null);
            mat.SetTextureScale("_MainTex", Vector2.one);
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Glossiness", smoothness);
            mat.SetFloat("_GlossMapScale", smoothness);
            if (normal != null)
            {
                mat.SetTexture("_BumpMap", GarageTextures.Load(normal));
                mat.SetFloat("_BumpScale", bumpScale);
                mat.EnableKeyword("_NORMALMAP");
            }
            else
            {
                mat.SetTexture("_BumpMap", null);
            }
            if (emission.HasValue)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission.Value);
                mat.SetTexture("_EmissionMap", emissionTex != null ? GarageTextures.Load(emissionTex) : null);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            else
            {
                mat.SetColor("_EmissionColor", Color.black);
                mat.SetTexture("_EmissionMap", null);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }
            SetMode(mat, mode);
            mat.enableInstancing = true;

            if (created) AssetDatabase.CreateAsset(mat, path);
            else EditorUtility.SetDirty(mat);

            _byName[name] = mat;
            _tiles[mat] = tile;
            return mat;
        }

        private static void SetMode(Material m, Mode mode)
        {
            m.SetFloat("_Mode", (float)mode);
            m.DisableKeyword("_ALPHATEST_ON");
            m.DisableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            switch (mode)
            {
                case Mode.Opaque:
                    m.SetOverrideTag("RenderType", "");
                    m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                    m.SetInt("_ZWrite", 1);
                    m.renderQueue = -1;
                    break;
                case Mode.Cutout:
                    m.SetOverrideTag("RenderType", "TransparentCutout");
                    m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                    m.SetInt("_ZWrite", 1);
                    m.SetFloat("_Cutoff", 0.5f);
                    m.EnableKeyword("_ALPHATEST_ON");
                    m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                    break;
                case Mode.Fade:
                    m.SetOverrideTag("RenderType", "Transparent");
                    m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    m.SetInt("_ZWrite", 0);
                    m.EnableKeyword("_ALPHABLEND_ON");
                    m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    break;
                case Mode.Transparent:
                    m.SetOverrideTag("RenderType", "Transparent");
                    m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    m.SetInt("_ZWrite", 0);
                    m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                    m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    break;
            }
        }
    }
}
