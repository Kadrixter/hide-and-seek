using System.Collections.Generic;
using HideAndSeek.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Procedural blocky hand props (knife, Glock, medkit, loose magazine) built from boxes.
    /// Every face samples a small runtime-generated pixel atlas, so the props get the same chunky pixel look as the skins.
    /// Prop space: origin = centre of the holding fist, +Z = forward (barrel / blade), +Y = up.
    /// </summary>
    public static class BlockyProps
    {
        /// <summary>Prop pixel: half a skin pixel.</summary>
        public const float U = BlockyCharacter.PixelSize * 0.5f;

        public const string MaterialResource = "Props/BlockyProp";

        // Atlas slots (8x8 texel tiles in a 64x64 texture).
        enum Slot
        {
            Blade, Edge, HandleDark, MetalDark, Polymer, Slide, Sight, Bore, MedWhite, MedRed, MedGrey, Brass, GripTexture
        }

        static readonly Color[] SlotColors =
        {
            new Color(0.70f, 0.72f, 0.76f), // Blade
            new Color(0.92f, 0.94f, 0.97f), // Edge
            new Color(0.13f, 0.10f, 0.08f), // HandleDark
            new Color(0.30f, 0.31f, 0.34f), // MetalDark
            new Color(0.075f, 0.075f, 0.085f), // Polymer
            new Color(0.15f, 0.155f, 0.17f), // Slide
            new Color(0.95f, 0.93f, 0.85f), // Sight
            new Color(0.01f, 0.01f, 0.01f), // Bore
            new Color(0.93f, 0.93f, 0.90f), // MedWhite
            new Color(0.78f, 0.08f, 0.07f), // MedRed
            new Color(0.50f, 0.52f, 0.55f), // MedGrey
            new Color(0.80f, 0.62f, 0.25f), // Brass
            new Color(0.10f, 0.10f, 0.11f), // GripTexture
        };

        static Texture2D atlas;
        static Material material;
        static readonly Dictionary<string, Mesh> Meshes = new Dictionary<string, Mesh>();

        // ------------------------------------------------------------------ public API

        /// <summary>Glock layout constants (prop space).</summary>
        public static readonly Vector3 GlockMuzzleLocal = new Vector3(0f, 0.14f, 0.30f);
        public const float GlockSlideTravel = 0.07f;
        const float GripAngle = 18f;

        /// <summary>Creates a prop instance. Renderers use <paramref name="layer"/>.</summary>
        public static HeldProp Create(HeldItem item, Transform parent, int layer)
        {
            var go = new GameObject("Prop_" + item);
            go.layer = layer;
            go.transform.SetParent(parent, false);
            var prop = go.AddComponent<HeldProp>();
            prop.Item = item;
            switch (item)
            {
                case HeldItem.Knife:
                    AddPart(go.transform, "Knife", GetMesh("Knife"), layer);
                    break;
                case HeldItem.Glock:
                {
                    AddPart(go.transform, "Frame", GetMesh("GlockFrame"), layer);
                    var slide = AddPart(go.transform, "Slide", GetMesh("GlockSlide"), layer);
                    var mag = AddPart(go.transform, "Mag", GetMesh("GlockMag"), layer);
                    var muzzle = new GameObject("Muzzle").transform;
                    muzzle.SetParent(go.transform, false);
                    muzzle.localPosition = GlockMuzzleLocal;
                    muzzle.gameObject.layer = layer;
                    prop.Slide = slide;
                    prop.Mag = mag;
                    prop.Muzzle = muzzle;
                    break;
                }
                case HeldItem.Medkit:
                    AddPart(go.transform, "Medkit", GetMesh("Medkit"), layer);
                    break;
            }

            prop.CollectRenderers();
            return prop;
        }

        /// <summary>A loose Glock magazine (prop space identical to the gun, i.e. origin = grip fist).</summary>
        public static GameObject CreateMagazine(Transform parent, int layer)
        {
            var go = new GameObject("Magazine");
            go.layer = layer;
            if (parent != null) go.transform.SetParent(parent, false);
            AddPart(go.transform, "Mag", GetMesh("GlockMag"), layer);
            return go;
        }

        public static Material GetMaterial()
        {
            if (material != null) return material;
            var template = Resources.Load<Material>(MaterialResource);
            if (template != null)
            {
                material = new Material(template);
            }
            else
            {
                var shader = Shader.Find("Standard");
                material = new Material(shader != null ? shader : Shader.Find("Diffuse"));
                material.SetFloat("_Glossiness", 0.25f);
            }

            material.name = "BlockyProp (runtime)";
            material.hideFlags = HideFlags.DontSave;
            material.mainTexture = GetAtlas();
            material.color = Color.white;
            return material;
        }

        static Transform AddPart(Transform parent, string name, Mesh mesh, int layer)
        {
            var go = new GameObject(name);
            go.layer = layer;
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = GetMaterial();
            r.shadowCastingMode = ShadowCastingMode.On;
            r.receiveShadows = true;
            r.lightProbeUsage = LightProbeUsage.BlendProbes;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return go.transform;
        }

        // ------------------------------------------------------------------ atlas

        static Texture2D GetAtlas()
        {
            if (atlas != null) return atlas;
            const int n = 64;
            atlas = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                name = "BlockyPropAtlas",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            var px = new Color32[n * n];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 0, 255, 255);
            for (int s = 0; s < SlotColors.Length; s++)
            {
                int ox = (s % 8) * 8, oy = (s / 8) * 8;
                for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                {
                    float noise = Hash(s * 131 + x * 17 + y * 977);
                    float shade = 1f + (noise - 0.5f) * 0.12f;
                    var slot = (Slot)s;
                    if (slot == Slot.GripTexture && ((x + y) % 2 == 0)) shade *= 1.35f; // stippled grip
                    if (slot == Slot.Slide && y == 7) shade *= 1.25f; // bright top edge
                    if (slot == Slot.Blade) shade *= 0.92f + 0.12f * (y / 7f); // spine darker -> edge brighter
                    if (slot == Slot.MedWhite && (x == 0 || y == 0)) shade *= 0.9f;
                    Color c = SlotColors[s] * shade;
                    c.a = 1f;
                    px[(oy + y) * n + ox + x] = c;
                }
            }

            atlas.SetPixels32(px);
            atlas.Apply(false, false);
            return atlas;
        }

        static float Hash(int v)
        {
            unchecked
            {
                uint h = (uint)v * 2654435761u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                return (h & 0xFFFF) / 65535f;
            }
        }

        // ------------------------------------------------------------------ meshes

        sealed class MeshData
        {
            public readonly List<Vector3> V = new List<Vector3>();
            public readonly List<Vector3> N = new List<Vector3>();
            public readonly List<Vector2> UV = new List<Vector2>();
            public readonly List<int> T = new List<int>();
        }

        static Mesh GetMesh(string key)
        {
            if (Meshes.TryGetValue(key, out var m) && m != null) return m;
            var md = new MeshData();
            switch (key)
            {
                case "Knife": BuildKnife(md); break;
                case "GlockFrame": BuildGlockFrame(md); break;
                case "GlockSlide": BuildGlockSlide(md); break;
                case "GlockMag": BuildGlockMag(md); break;
                case "Medkit": BuildMedkit(md); break;
            }

            m = new Mesh { name = "BlockyProp_" + key, hideFlags = HideFlags.DontSave };
            m.SetVertices(md.V);
            m.SetNormals(md.N);
            m.SetUVs(0, md.UV);
            m.SetTriangles(md.T, 0);
            m.RecalculateBounds();
            m.UploadMeshData(false);
            Meshes[key] = m;
            return m;
        }

        static void BuildKnife(MeshData md)
        {
            // Handle through the fist, pommel, guard, stepped blade (clip point), bright edge strip.
            BoxMinMax(md, new Vector3(-0.026f, -0.032f, -0.14f), new Vector3(0.026f, 0.032f, 0.125f), Slot.HandleDark);
            BoxMinMax(md, new Vector3(-0.03f, -0.036f, -0.165f), new Vector3(0.03f, 0.036f, -0.14f), Slot.MetalDark);
            BoxMinMax(md, new Vector3(-0.032f, -0.06f, 0.125f), new Vector3(0.032f, 0.055f, 0.15f), Slot.MetalDark);
            BoxMinMax(md, new Vector3(-0.009f, -0.022f, 0.15f), new Vector3(0.009f, 0.036f, 0.31f), Slot.Blade);
            BoxMinMax(md, new Vector3(-0.0085f, -0.036f, 0.15f), new Vector3(0.0085f, -0.022f, 0.29f), Slot.Edge);
            BoxMinMax(md, new Vector3(-0.008f, -0.012f, 0.31f), new Vector3(0.008f, 0.036f, 0.345f), Slot.Blade);
            BoxMinMax(md, new Vector3(-0.007f, 0.006f, 0.345f), new Vector3(0.007f, 0.036f, 0.375f), Slot.Edge);
        }

        static void BuildGlockFrame(MeshData md)
        {
            // Dust cover / rail under the front of the slide.
            BoxMinMax(md, new Vector3(-0.03f, 0.075f, -0.07f), new Vector3(0.03f, 0.105f, 0.27f), Slot.Polymer);
            // Grip, raked back.
            var gripRot = Quaternion.Euler(GripAngle, 0f, 0f);
            Box(md, new Vector3(0f, -0.02f, -0.012f), new Vector3(0.058f, 0.235f, 0.088f), Slot.GripTexture, gripRot);
            // Backstrap beavertail.
            Box(md, new Vector3(0f, 0.085f, -0.075f), new Vector3(0.056f, 0.03f, 0.04f), Slot.Polymer, Quaternion.identity);
            // Trigger guard (bottom bar + front bar) and trigger.
            BoxMinMax(md, new Vector3(-0.018f, 0.005f, 0.035f), new Vector3(0.018f, 0.025f, 0.135f), Slot.Polymer);
            BoxMinMax(md, new Vector3(-0.018f, 0.005f, 0.115f), new Vector3(0.018f, 0.085f, 0.135f), Slot.Polymer);
            BoxMinMax(md, new Vector3(-0.008f, 0.03f, 0.06f), new Vector3(0.008f, 0.08f, 0.075f), Slot.MetalDark);
        }

        static void BuildGlockSlide(MeshData md)
        {
            BoxMinMax(md, new Vector3(-0.033f, 0.105f, -0.08f), new Vector3(0.033f, 0.18f, 0.29f), Slot.Slide);
            // Rear serrations (slightly proud dark strips).
            for (int i = 0; i < 3; i++)
            {
                float z = -0.07f + i * 0.022f;
                BoxMinMax(md, new Vector3(-0.0345f, 0.115f, z), new Vector3(0.0345f, 0.17f, z + 0.01f), Slot.Polymer);
            }

            // Ejection port (dark inset on the top right) with a brass glint.
            BoxMinMax(md, new Vector3(0.0f, 0.176f, 0.06f), new Vector3(0.028f, 0.182f, 0.14f), Slot.Bore);
            BoxMinMax(md, new Vector3(0.008f, 0.177f, 0.085f), new Vector3(0.02f, 0.183f, 0.105f), Slot.Brass);
            // Sights: rear notch (two posts) and front post, white dots.
            BoxMinMax(md, new Vector3(-0.03f, 0.18f, -0.075f), new Vector3(-0.009f, 0.205f, -0.055f), Slot.Polymer);
            BoxMinMax(md, new Vector3(0.009f, 0.18f, -0.075f), new Vector3(0.03f, 0.205f, -0.055f), Slot.Polymer);
            BoxMinMax(md, new Vector3(-0.024f, 0.19f, -0.0765f), new Vector3(-0.015f, 0.198f, -0.074f), Slot.Sight);
            BoxMinMax(md, new Vector3(0.015f, 0.19f, -0.0765f), new Vector3(0.024f, 0.198f, -0.074f), Slot.Sight);
            BoxMinMax(md, new Vector3(-0.008f, 0.18f, 0.255f), new Vector3(0.008f, 0.206f, 0.275f), Slot.Polymer);
            BoxMinMax(md, new Vector3(-0.004f, 0.192f, 0.2535f), new Vector3(0.004f, 0.2f, 0.256f), Slot.Sight);
            // Muzzle bore.
            BoxMinMax(md, new Vector3(-0.012f, 0.128f, 0.289f), new Vector3(0.012f, 0.152f, 0.293f), Slot.Bore);
        }

        static void BuildGlockMag(MeshData md)
        {
            var rot = Quaternion.Euler(GripAngle, 0f, 0f);
            // Body (mostly hidden inside the grip), base plate below the grip, top round.
            Box(md, new Vector3(0f, -0.02f, -0.012f), new Vector3(0.042f, 0.235f, 0.07f), Slot.MetalDark, rot);
            Box(md, rot * new Vector3(0f, -0.145f, 0f) + new Vector3(0f, -0.02f, -0.012f), new Vector3(0.064f, 0.03f, 0.096f), Slot.Polymer, rot);
            Box(md, rot * new Vector3(0f, 0.12f, 0.005f) + new Vector3(0f, -0.02f, -0.012f), new Vector3(0.022f, 0.016f, 0.05f), Slot.Brass, rot);
        }

        static void BuildMedkit(MeshData md)
        {
            // Carry handle in the fist, two posts, case hanging below, red crosses on both broad sides, grey latches.
            BoxMinMax(md, new Vector3(-0.02f, -0.02f, -0.09f), new Vector3(0.02f, 0.02f, 0.09f), Slot.MedGrey);
            BoxMinMax(md, new Vector3(-0.018f, -0.135f, -0.095f), new Vector3(0.018f, 0.0f, -0.065f), Slot.MedGrey);
            BoxMinMax(md, new Vector3(-0.018f, -0.135f, 0.065f), new Vector3(0.018f, 0.0f, 0.095f), Slot.MedGrey);
            BoxMinMax(md, new Vector3(-0.07f, -0.37f, -0.17f), new Vector3(0.07f, -0.13f, 0.17f), Slot.MedWhite);
            // Lid seam.
            BoxMinMax(md, new Vector3(-0.0725f, -0.165f, -0.1725f), new Vector3(0.0725f, -0.155f, 0.1725f), Slot.MedGrey);
            for (int side = -1; side <= 1; side += 2)
            {
                float x0 = side * 0.07f, x1 = side * 0.077f;
                float xa = Mathf.Min(x0, x1), xb = Mathf.Max(x0, x1);
                BoxMinMax(md, new Vector3(xa, -0.33f, -0.03f), new Vector3(xb, -0.18f, 0.03f), Slot.MedRed);
                BoxMinMax(md, new Vector3(xa, -0.285f, -0.075f), new Vector3(xb, -0.225f, 0.075f), Slot.MedRed);
            }

            BoxMinMax(md, new Vector3(-0.075f, -0.19f, -0.15f), new Vector3(0.075f, -0.15f, -0.12f), Slot.MedGrey);
            BoxMinMax(md, new Vector3(-0.075f, -0.19f, 0.12f), new Vector3(0.075f, -0.15f, 0.15f), Slot.MedGrey);
        }

        static void BoxMinMax(MeshData md, Vector3 min, Vector3 max, Slot slot)
        {
            Box(md, (min + max) * 0.5f, max - min, slot, Quaternion.identity);
        }

        static void Box(MeshData md, Vector3 center, Vector3 size, Slot slot, Quaternion rot)
        {
            Vector3 h = size * 0.5f;
            // +X, -X, +Y, -Y, +Z, -Z ; each face: normal, axis u, axis v, extent along u, v.
            AddFace(md, center, rot, Vector3.right, Vector3.forward, Vector3.up, h.x, h.z, h.y, size.z, size.y, slot);
            AddFace(md, center, rot, Vector3.left, Vector3.back, Vector3.up, h.x, h.z, h.y, size.z, size.y, slot);
            AddFace(md, center, rot, Vector3.up, Vector3.right, Vector3.forward, h.y, h.x, h.z, size.x, size.z, slot);
            AddFace(md, center, rot, Vector3.down, Vector3.right, Vector3.back, h.y, h.x, h.z, size.x, size.z, slot);
            AddFace(md, center, rot, Vector3.forward, Vector3.left, Vector3.up, h.z, h.x, h.y, size.x, size.y, slot);
            AddFace(md, center, rot, Vector3.back, Vector3.right, Vector3.up, h.z, h.x, h.y, size.x, size.y, slot);
        }

        static void AddFace(MeshData md, Vector3 c, Quaternion rot, Vector3 n, Vector3 u, Vector3 v,
            float dn, float du, float dv, float sizeU, float sizeV, Slot slot)
        {
            int i = md.V.Count;
            Vector3 fc = n * dn;
            md.V.Add(c + rot * (fc - u * du - v * dv));
            md.V.Add(c + rot * (fc + u * du - v * dv));
            md.V.Add(c + rot * (fc - u * du + v * dv));
            md.V.Add(c + rot * (fc + u * du + v * dv));
            Vector3 wn = rot * n;
            for (int k = 0; k < 4; k++) md.N.Add(wn);

            // One atlas texel per prop pixel, at most a full 8x8 tile.
            int s = (int)slot;
            float ox = (s % 8) * 8f, oy = (s / 8) * 8f;
            float tu = Mathf.Clamp(Mathf.Round(sizeU / U), 1f, 8f);
            float tv = Mathf.Clamp(Mathf.Round(sizeV / U), 1f, 8f);
            const float inv = 1f / 64f, e = 0.01f;
            md.UV.Add(new Vector2((ox + e) * inv, (oy + e) * inv));
            md.UV.Add(new Vector2((ox + tu - e) * inv, (oy + e) * inv));
            md.UV.Add(new Vector2((ox + e) * inv, (oy + tv - e) * inv));
            md.UV.Add(new Vector2((ox + tu - e) * inv, (oy + tv - e) * inv));

            // u = n x v for every face above (u is the viewer's right), so bl, tl, tr / bl, tr, br is clockwise.
            md.T.Add(i);
            md.T.Add(i + 2);
            md.T.Add(i + 3);
            md.T.Add(i);
            md.T.Add(i + 3);
            md.T.Add(i + 1);
        }
    }

    /// <summary>Runtime handle on a prop instance (moving parts + render mode).</summary>
    public class HeldProp : MonoBehaviour
    {
        public HeldItem Item;
        public Transform Slide, Mag, Muzzle;
        Renderer[] renderers;

        public void CollectRenderers() => renderers = GetComponentsInChildren<Renderer>(true);

        /// <summary>0 = slide forward (in battery), 1 = locked back.</summary>
        public void SetSlideBack(float amount)
        {
            if (Slide != null) Slide.localPosition = new Vector3(0f, 0f, -BlockyProps.GlockSlideTravel * Mathf.Clamp01(amount));
        }

        public void SetMagVisible(bool visible)
        {
            if (Mag != null && Mag.gameObject.activeSelf != visible) Mag.gameObject.SetActive(visible);
        }

        public void SetRenderMode(ShadowCastingMode mode, bool receiveShadows)
        {
            if (renderers == null) CollectRenderers();
            foreach (var r in renderers)
            {
                if (r == null) continue;
                r.shadowCastingMode = mode;
                r.receiveShadows = receiveShadows;
            }
        }

        public void SetLayer(int layer)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }
    }
}
