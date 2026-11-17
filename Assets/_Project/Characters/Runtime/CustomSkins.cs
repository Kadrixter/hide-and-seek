using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Materials for downloaded Minecraft skins (64x64, or legacy 64x32 converted). One material per texture,
    /// cloned from the regular skin material so lighting / cutout stay identical.
    /// </summary>
    public static class CustomSkins
    {
        private static readonly Dictionary<Texture2D, Material> Cache = new();

        public static Material MaterialFor(Texture2D tex, Material template)
        {
            if (tex == null) return null;
            if (Cache.TryGetValue(tex, out var m) && m != null) return m;
            m = template != null ? new Material(template) : new Material(Shader.Find("Standard"));
            m.name = "McSkin_" + tex.name;
            m.mainTexture = tex;
            Cache[tex] = m;
            return m;
        }

        /// <summary>
        /// Normalises a skin PNG: point filtering, no mips, and 64x32 legacy skins expanded to 64x64
        /// (left arm / leg copied from the right ones, as Minecraft does).
        /// </summary>
        public static Texture2D Prepare(Texture2D src, string name)
        {
            if (src == null) return null;
            Texture2D tex = src;
            if (src.width == 64 && src.height == 32)
            {
                tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
                var clear = new Color32[64 * 64];
                tex.SetPixels32(clear);
                // Unity textures start at the bottom: the 64x32 image becomes the top half (y 32..63).
                tex.SetPixels(0, 32, 64, 32, src.GetPixels());
                // Skin-space rect (x, y from top, w, h) -> copy right leg (0,16) to left leg (16,48), right arm (40,16) to left arm (32,48).
                CopyRect(tex, 0, 16, 16, 48, 16, 16);
                CopyRect(tex, 40, 16, 32, 48, 16, 16);
                tex.Apply(false);
            }
            else if (src.width != 64 || src.height != 64)
            {
                return null;
            }
            MakeBaseLayerOpaque(tex);
            tex.name = name;
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.anisoLevel = 0;
            return tex;
        }

        /// <summary>Minecraft draws the inner (base) layer fully opaque; only the outer layer (hat, jacket, sleeves, pants) may be transparent.</summary>
        private static void MakeBaseLayerOpaque(Texture2D t)
        {
            // Skin-space rects (x, y from top, w, h) of the base layer.
            int[,] rects = { { 0, 0, 32, 16 }, { 0, 16, 56, 16 }, { 16, 48, 16, 16 }, { 32, 48, 16, 16 } };
            var px = t.GetPixels32();
            for (int r = 0; r < rects.GetLength(0); r++)
            {
                for (int y = rects[r, 1]; y < rects[r, 1] + rects[r, 3]; y++)
                for (int x = rects[r, 0]; x < rects[r, 0] + rects[r, 2]; x++)
                {
                    int i = (63 - y) * 64 + x;
                    if (px[i].a < 255) px[i] = new Color32(px[i].r, px[i].g, px[i].b, 255);
                }
            }
            t.SetPixels32(px);
            t.Apply(false);
        }

        private static void CopyRect(Texture2D t, int sx, int sy, int dx, int dy, int w, int h)
        {
            // Convert from "y from top" to Unity's bottom-up rows.
            var px = t.GetPixels(sx, 64 - sy - h, w, h);
            t.SetPixels(dx, 64 - dy - h, w, h, px);
        }
    }
}
