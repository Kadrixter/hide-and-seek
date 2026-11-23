using System;
using UnityEditor;
using UnityEngine;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// Materials of the wrecked Golf GTI (created on first use, not part of the shared car palette): dusty paint, plastics
    /// and metals on the HideAndSeek/WreckDust shader, dusty / cracked glass on HideAndSeek/WreckGlass, red cloth, foil.
    /// </summary>
    internal sealed partial class MaterialLibrary
    {
        public bool HasWreck => _byName.ContainsKey("Wreck_Paint");

        public void EnsureWreckMaterials()
        {
            if (HasWreck) return;
            WreckTextures.Ensure();
            var dust = GarageTextures.Load("Wreck_Dust");
            // body paint: black, scrapes on the right front, the finger writing on the right rear door (planes set by the model)
            Dusty("Wreck_Paint", new Color(0.018f, 0.018f, 0.021f), 0.4f, 0.86f, dust, 0.9f, 0.3f, 0.32f, m =>
            {
                m.SetTexture("_ScrapeTex", GarageTextures.Load("Wreck_Scrape"));
                m.SetTexture("_WriteTex", GarageTextures.Load("Wreck_Write"));
            });
            Dusty("Wreck_PaintPart", new Color(0.018f, 0.018f, 0.021f), 0.4f, 0.8f, dust, 0.85f, 0.3f, 0.3f);
            Dusty("Wreck_Plastic", new Color(0.035f, 0.035f, 0.038f), 0f, 0.32f, dust, 0.85f, 0.28f, 0.3f);
            Dusty("Wreck_PlasticGrey", new Color(0.13f, 0.13f, 0.135f), 0f, 0.28f, dust, 0.9f, 0.4f, 0.3f);
            Dusty("Wreck_Primer", new Color(0.09f, 0.09f, 0.095f), 0.2f, 0.3f, dust, 0.8f, 0.45f, 0.4f);
            Dusty("Wreck_Steel", new Color(0.3f, 0.3f, 0.3f), 0.7f, 0.42f, dust, 0.9f, 0.45f, 0.3f);
            Dusty("Wreck_Rust", new Color(0.28f, 0.15f, 0.08f), 0.3f, 0.25f, dust, 0.9f, 0.45f, 0.3f);
            Dusty("Wreck_Engine", new Color(0.2f, 0.2f, 0.205f), 0.55f, 0.35f, dust, 1.0f, 0.5f, 0.2f);
            Dusty("Wreck_Alu", new Color(0.52f, 0.52f, 0.5f), 0.75f, 0.5f, dust, 0.9f, 0.45f, 0.2f);
            Dusty("Wreck_Rim", new Color(0.62f, 0.63f, 0.64f), 0.6f, 0.62f, dust, 0.8f, 0.35f, 0.5f);
            Dusty("Wreck_Rubber", new Color(0.04f, 0.04f, 0.042f), 0f, 0.25f, dust, 0.8f, 0.35f, 0.4f);
            Dusty("Wreck_Disc", new Color(0.3f, 0.22f, 0.17f), 0.6f, 0.35f, dust, 0.7f, 0.4f, 0.3f);
            Glass("Wreck_Glass", 0.55f, dust, false);
            Glass("Wreck_Windscreen", 0.5f, dust, true);
            // plain Standard materials
            Make("Wreck_SeatRed", new Color(0.46f, 0.035f, 0.035f), "Tarp", 0.12f, 0f, 0.12f, "Tarp_N", 0.6f);
            Make("Wreck_SeatBlack", new Color(0.035f, 0.035f, 0.037f), "Tarp", 0.12f, 0f, 0.14f, "Tarp_N", 0.6f);
            Make("Wreck_Airbag", new Color(0.78f, 0.77f, 0.72f), "Tarp", 0.2f, 0f, 0.22f, "Tarp_N", 1.2f);
            Make("Wreck_Leather", new Color(0.04f, 0.04f, 0.04f), null, 1f, 0f, 0.5f);
            Make("Wreck_Carpet", new Color(0.05f, 0.05f, 0.052f), "Tarp", 0.08f, 0f, 0.05f, "Tarp_N", 1.5f);
            Make("Wreck_GlassBits", new Color(0.55f, 0.66f, 0.63f), null, 1f, 0.25f, 0.95f);
            Make("Wreck_Foil", new Color(0.55f, 0.74f, 0.66f, 0.3f), null, 1f, 0f, 0.78f, mode: Mode.Transparent);
            Make("Wreck_TankWhite", new Color(0.78f, 0.78f, 0.72f), null, 1f, 0f, 0.55f);
            Make("Wreck_CapBlue", new Color(0.05f, 0.18f, 0.6f), null, 1f, 0f, 0.5f);
            Make("Wreck_CapYellow", new Color(0.9f, 0.68f, 0.05f), null, 1f, 0f, 0.5f);
            Make("Wreck_WireRed", new Color(0.6f, 0.04f, 0.03f), null, 1f, 0f, 0.45f);
            Make("Wreck_WireYellow", new Color(0.8f, 0.62f, 0.05f), null, 1f, 0f, 0.45f);
            Make("Wreck_WireBlue", new Color(0.08f, 0.22f, 0.6f), null, 1f, 0f, 0.45f);
            Make("Wreck_WireGreen", new Color(0.1f, 0.4f, 0.15f), null, 1f, 0f, 0.45f);
            Make("Wreck_Cable", new Color(0.62f, 0.62f, 0.6f), "Metal_Brushed", 0.05f, 0.8f, 0.45f);
            Make("Wreck_Rope", new Color(0.62f, 0.38f, 0.16f), "Tarp", 0.05f, 0f, 0.15f, "Tarp_N", 1.5f);
            Make("Wreck_BadgeRed", new Color(0.7f, 0.03f, 0.03f), null, 1f, 0.2f, 0.75f);
            Make("Wreck_TextRed", new Color(0.75f, 0.04f, 0.03f), "Glyphs", 1f, 0f, 0.5f, mode: Mode.Cutout);
            Make("Wreck_Freshener", new Color(0.1f, 0.45f, 0.15f), null, 1f, 0f, 0.3f);
            Make("Wreck_Coolant", new Color(0.55f, 0.1f, 0.35f, 0.75f), null, 1f, 0f, 0.95f, mode: Mode.Transparent);
            AssetDatabase.SaveAssets();
        }

        private Material Dusty(string name, Color color, float metallic, float smooth, Texture dust, float top, float side, float low,
            Action<Material> extra = null)
        {
            var mat = ShaderMat(name, "HideAndSeek/WreckDust");
            mat.SetColor("_DustColor", new Color(0.42f, 0.4f, 0.365f));
            mat.SetColor("_Color", color);
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Glossiness", smooth);
            mat.SetTexture("_DustTex", dust);
            mat.SetFloat("_DustTop", top);
            mat.SetFloat("_DustSide", side);
            mat.SetFloat("_DustLow", low);
            extra?.Invoke(mat);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private Material Glass(string name, float alpha, Texture dust, bool cracked)
        {
            var mat = ShaderMat(name, "HideAndSeek/WreckGlass");
            mat.SetColor("_Color", new Color(0.07f, 0.085f, 0.095f, alpha));
            mat.SetTexture("_DustTex", dust);
            mat.SetTexture("_CrackTex", cracked ? GarageTextures.Load("Wreck_Crack") : null);
            mat.renderQueue = 3000;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private Material ShaderMat(string name, string shaderName)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException("Shader " + shaderName + " not found");
            string path = $"{Folder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = mat == null;
            if (created) mat = new Material(shader);
            mat.shader = shader;
            mat.name = name;
            mat.enableInstancing = false;
            if (created) AssetDatabase.CreateAsset(mat, path);
            _byName[name] = mat;
            _tiles[mat] = 1f;
            return mat;
        }

        /// <summary>Sets a projected mask plane (origin, axes divided by the mask size) on a wreck material.</summary>
        public void SetWreckPlane(string material, string prefix, Vector3 origin, Vector3 u, Vector3 v)
        {
            var m = this[material];
            m.SetVector(prefix + "O", origin);
            m.SetVector(prefix + "U", u);
            m.SetVector(prefix + "V", v);
            EditorUtility.SetDirty(m);
        }
    }
}
