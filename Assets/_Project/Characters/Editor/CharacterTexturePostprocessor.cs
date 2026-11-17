using UnityEditor;
using UnityEngine;

namespace HideAndSeek.Characters.Editor
{
    /// <summary>
    /// Import rules for character textures:
    /// - Characters/Skins: 64x64 pixel-art skins -> point filtering, no mipmaps, uncompressed, alpha for cutout.
    /// - Characters/Resources/Faces: photo faces -> readable, clamp, uncompressed (bilinear with mipmaps so a
    ///   high-res photo stays smooth on the small head).
    /// </summary>
    public class CharacterTexturePostprocessor : AssetPostprocessor
    {
        public const string SkinsFolder = "Assets/_Project/Characters/Skins/";
        public const string FacesFolder = "Assets/_Project/Characters/Resources/Faces/";

        void OnPreprocessTexture()
        {
            var ti = (TextureImporter)assetImporter;
            string path = assetPath.Replace('\\', '/');

            if (path.StartsWith(SkinsFolder, System.StringComparison.OrdinalIgnoreCase))
            {
                ti.textureType = TextureImporterType.Default;
                ti.sRGBTexture = true;
                ti.alphaSource = TextureImporterAlphaSource.FromInput;
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = false;
                ti.filterMode = FilterMode.Point;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.npotScale = TextureImporterNPOTScale.None;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.maxTextureSize = 64;
                ti.isReadable = false;
            }
            else if (path.StartsWith(FacesFolder, System.StringComparison.OrdinalIgnoreCase))
            {
                ti.textureType = TextureImporterType.Default;
                ti.sRGBTexture = true;
                ti.isReadable = true;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.npotScale = TextureImporterNPOTScale.None;
                ti.mipmapEnabled = true;
                ti.filterMode = FilterMode.Bilinear;
                ti.maxTextureSize = 1024;
                ti.alphaIsTransparency = false;
            }
        }
    }
}
