using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace HideAndSeek.Game.Editor
{
    /// <summary>
    /// Program icon (exe, taskbar, window) from <c>Assets/_Project/Game/Icon/hidenseek.png</c> (square art by the user).
    /// Set as the default icon and for every Windows standalone icon size, on each build.
    /// </summary>
    public static partial class MultiplayerBuilder
    {
        private const string AppIconPath = "Assets/_Project/Game/Icon/hidenseek.png";

        private static void ApplyAppIcon()
        {
            if (AssetImporter.GetAtPath(AppIconPath) is not TextureImporter imp)
            {
                Debug.LogWarning($"[MultiplayerBuilder] App icon missing: {AppIconPath}");
                return;
            }
            bool dirty = false;
            if (imp.textureType != TextureImporterType.Default) { imp.textureType = TextureImporterType.Default; dirty = true; }
            if (imp.mipmapEnabled) { imp.mipmapEnabled = false; dirty = true; }
            if (imp.maxTextureSize != 1024) { imp.maxTextureSize = 1024; dirty = true; }
            if (imp.textureCompression != TextureImporterCompression.Uncompressed) { imp.textureCompression = TextureImporterCompression.Uncompressed; dirty = true; }
            if (imp.npotScale != TextureImporterNPOTScale.None) { imp.npotScale = TextureImporterNPOTScale.None; dirty = true; }
            if (dirty) imp.SaveAndReimport();

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(AppIconPath);
            if (tex == null) return;
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { tex }, IconKind.Any);
            var sizes = PlayerSettings.GetIconSizes(NamedBuildTarget.Standalone, IconKind.Any);
            var icons = new Texture2D[sizes.Length];
            for (int i = 0; i < icons.Length; i++) icons[i] = tex;
            PlayerSettings.SetIcons(NamedBuildTarget.Standalone, icons, IconKind.Any);
            Debug.Log($"[MultiplayerBuilder] App icon set ({sizes.Length} standalone sizes).");
        }
    }
}
