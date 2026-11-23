using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// Materials of the map details (23.11): dusty variants of the car materials (Hns/CarDust, Hns/CarDustGlass),
    /// fluids leaking from under cars (Hns/Fluid) and the fabrics of the car covers.
    /// </summary>
    internal sealed partial class MaterialLibrary
    {
        public const string DustFolder = Folder + "/Dust";

        /// <summary>Dust presets: 0-1 level -1 (light film), 2-3 level -2 (forgotten).</summary>
        public enum DustPreset { Light = 0, LightPlus = 1, Heavy = 2, Forgotten = 3 }

        private struct DustSpec
        {
            public float Top, Side, Grime, Haze;
            public int Level;
            public Color Dust;
        }

        private static readonly DustSpec[] DustSpecs =
        {
            new DustSpec { Top = 0.2f, Side = 0.015f, Grime = 0.12f, Haze = 0.06f, Level = 1, Dust = new Color(0.36f, 0.35f, 0.32f) },
            new DustSpec { Top = 0.3f, Side = 0.03f, Grime = 0.18f, Haze = 0.1f, Level = 1, Dust = new Color(0.36f, 0.35f, 0.32f) },
            new DustSpec { Top = 0.85f, Side = 0.3f, Grime = 0.5f, Haze = 0.45f, Level = 2, Dust = new Color(0.39f, 0.375f, 0.345f) },
            new DustSpec { Top = 1.1f, Side = 0.42f, Grime = 0.62f, Haze = 0.62f, Level = 2, Dust = new Color(0.4f, 0.385f, 0.355f) },
        };

        private readonly Dictionary<(Material, DustPreset), Material> _dusty = new Dictionary<(Material, DustPreset), Material>();

        private void BuildDetailMaterials()
        {
            DetailTextures.GenerateAll();
            System.IO.Directory.CreateDirectory(GarageTextures.FullPath(DustFolder));
            // car covers
            Make("Cover_Grey", new Color(0.58f, 0.6f, 0.62f), "Cover_Fabric", 0.6f, 0f, 0.32f, "Cover_Fabric_N", 0.8f);
            Make("Cover_Hem", new Color(0.22f, 0.23f, 0.25f), "Cover_Fabric", 0.4f, 0f, 0.25f, "Cover_Fabric_N", 0.6f);
            Make("Tarp_BlueHem", new Color(0.78f, 0.8f, 0.82f), "Tarp", 0.8f, 0f, 0.4f, "Tarp_N", 0.6f);
            Make("Foil_Green", new Color(0.52f, 0.72f, 0.5f, 0.36f), "Foil", 1.2f, 0f, 0.9f, "Foil_N", 0.45f, Mode.Transparent);
            Make("Canvas_Old", new Color(0.4f, 0.37f, 0.24f), "Canvas", 0.9f, 0f, 0.12f, "Canvas_N", 1.3f);
            Make("Canvas_Dark", new Color(0.27f, 0.25f, 0.16f), "Canvas", 0.9f, 0f, 0.1f, "Canvas_N", 1.3f);
            Make("Rope_Yellow", new Color(0.85f, 0.66f, 0.12f), null, 1f, 0f, 0.3f);
            Make("Rope_White", new Color(0.82f, 0.82f, 0.78f), null, 1f, 0f, 0.3f);
            Make("Cobweb", new Color(0.8f, 0.8f, 0.78f), "Cobweb", 1f, 0f, 0.3f, mode: Mode.Cutout);
            Make("Brick", new Color(0.55f, 0.24f, 0.15f), "Wall_Concrete", 0.6f, 0f, 0.1f, "Wall_Concrete_N", 1.2f);
            // leaks
            Fluid("Leak_Oil", new Color(0.022f, 0.018f, 0.014f, 0.93f), new Color(0.045f, 0.04f, 0.035f), 0.9f);
            Fluid("Leak_CoolantGreen", new Color(0.12f, 0.5f, 0.1f, 0.7f), new Color(0.2f, 0.32f, 0.12f), 0.15f);
            Fluid("Leak_CoolantPink", new Color(0.62f, 0.12f, 0.3f, 0.7f), new Color(0.35f, 0.16f, 0.2f), 0.15f);
            Fluid("Leak_Brake", new Color(0.5f, 0.33f, 0.07f, 0.75f), new Color(0.24f, 0.17f, 0.07f), 0.35f);
        }

        private void Fluid(string name, Color color, Color edge, float sheen)
        {
            var mat = Custom(name, Folder, "Hns/Fluid");
            mat.SetTexture("_MainTex", DetailTextures.Load("Leak_Atlas"));
            mat.SetColor("_Color", color);
            mat.SetColor("_EdgeColor", edge);
            mat.SetFloat("_Sheen", sheen);
            mat.SetFloat("_Spec", 0.05f);
            mat.SetFloat("_Smooth", 0.95f);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent - 10;
            _byName[name] = mat;
            _tiles[mat] = 1f;
        }

        private static Material Custom(string name, string folder, string shaderName)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException(shaderName + " shader not found");
            string path = $"{folder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = mat == null;
            if (created) mat = new Material(shader);
            mat.shader = shader;
            mat.shaderKeywords = Array.Empty<string>();
            mat.name = name;
            mat.enableInstancing = true;
            if (created) AssetDatabase.CreateAsset(mat, path);
            else EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>
        /// Dusty copy of a car material (cached). Interior, cut-out text and glowing materials stay as they are.
        /// Transparent glass gets the glass dust shader.
        /// </summary>
        public Material Dusty(Material src, DustPreset preset)
        {
            if (src == null) return null;
            if (_dusty.TryGetValue((src, preset), out var cached)) return cached;
            Material result = src;
            string n = src.name;
            bool skip = n.Contains("Interior") || n.Contains("Leather") || n.Contains("Headliner") || n.Contains("Dash") ||
                        n.Contains("Gauge") || n.Contains("Needle") || n.StartsWith("Text") || src.IsKeywordEnabled("_EMISSION") ||
                        src.IsKeywordEnabled("_ALPHATEST_ON") || src.shader == null || src.shader.name != "Standard";
            if (!skip)
            {
                bool glass = src.IsKeywordEnabled("_ALPHAPREMULTIPLY_ON") || src.IsKeywordEnabled("_ALPHABLEND_ON");
                var spec = DustSpecs[(int)preset];
                var mat = Custom($"{n}_D{(int)preset}", DustFolder, glass ? "Hns/CarDustGlass" : "Hns/CarDust");
                mat.SetColor("_Color", src.GetColor("_Color"));
                mat.SetTexture("_MainTex", src.GetTexture("_MainTex"));
                mat.SetFloat("_Metallic", src.GetFloat("_Metallic"));
                mat.SetFloat("_Glossiness", src.GetFloat("_Glossiness"));
                if (!glass && src.IsKeywordEnabled("_NORMALMAP") && src.GetTexture("_BumpMap") != null)
                {
                    mat.SetTexture("_BumpMap", src.GetTexture("_BumpMap"));
                    mat.SetFloat("_BumpScale", src.GetFloat("_BumpScale"));
                    mat.EnableKeyword("_NORMALMAP");
                }
                mat.SetTexture("_DustTex", DetailTextures.Load("Dust_Noise"));
                mat.SetColor("_DustColor", spec.Dust);
                mat.SetColor("_GrimeColor", new Color(0.12f, 0.1f, 0.085f));
                bool tyre = n == "Rubber" || n.Contains("Tire") || n.Contains("Tyre");
                mat.SetFloat("_DustTop", spec.Top * (glass ? 0.9f : 1f));
                mat.SetFloat("_DustSide", spec.Side * (tyre ? 1.6f : 1f));
                mat.SetFloat("_Grime", tyre ? spec.Grime * 0.5f : spec.Grime);
                mat.SetFloat("_GrimeHeight", 0.42f);
                mat.SetFloat("_FloorY", -3.4f * spec.Level);
                mat.SetFloat("_DustScale", 0.8f);
                mat.SetVector("_Seed", new Vector4(0.37f * (int)preset, 0.11f, 0.73f * (int)preset, 0.29f * (int)preset));
                mat.SetFloat("_Haze", glass ? spec.Haze : 0f);
                if (glass) mat.renderQueue = src.renderQueue;
                result = mat;
            }
            _dusty[(src, preset)] = result;
            return result;
        }
    }
}
