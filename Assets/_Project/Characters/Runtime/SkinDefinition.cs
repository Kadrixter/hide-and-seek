using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>One colour variant of the blocky character (64x64 Minecraft-layout skin + cutout material).</summary>
    [CreateAssetMenu(menuName = "HideAndSeek/Characters/Skin Definition", fileName = "Skin")]
    public class SkinDefinition : ScriptableObject
    {
        [SerializeField] string displayName = "Skin";
        [SerializeField] Texture2D texture;
        [SerializeField] Material material;
        [Tooltip("Representative colour for UI (player list, lobby).")]
        [SerializeField] Color accentColor = Color.white;

        public string DisplayName => displayName;
        public Texture2D Texture => texture;
        public Material Material => material;
        public Color AccentColor => accentColor;

#if UNITY_EDITOR
        /// <summary>Editor-only setter used by the build pipeline.</summary>
        public void EditorSetup(string name, Texture2D tex, Material mat, Color accent)
        {
            displayName = name;
            texture = tex;
            material = mat;
            accentColor = accent;
        }
#endif
    }
}
