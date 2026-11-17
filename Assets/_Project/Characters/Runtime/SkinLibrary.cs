using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// All available skins. Lives at Resources/SkinLibrary.asset and is loaded with <see cref="Load"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "HideAndSeek/Characters/Skin Library", fileName = "SkinLibrary")]
    public class SkinLibrary : ScriptableObject
    {
        public const string ResourcePath = "SkinLibrary";

        [SerializeField] List<SkinDefinition> skins = new List<SkinDefinition>();
        [Tooltip("Opaque template material for the photo face quad (texture is assigned at runtime).")]
        [SerializeField] Material facePhotoMaterial;

        static SkinLibrary cached;

        public IReadOnlyList<SkinDefinition> Skins => skins;
        public Material FacePhotoMaterial => facePhotoMaterial;

        /// <summary>Loads (and caches) the library from Resources. Returns null if it has not been built.</summary>
        public static SkinLibrary Load()
        {
            if (cached == null) cached = Resources.Load<SkinLibrary>(ResourcePath);
            return cached;
        }

        /// <summary>Number of skins (0 if the library is missing).</summary>
        public static int Count
        {
            get
            {
                var lib = Load();
                return lib != null ? lib.skins.Count : 0;
            }
        }

        /// <summary>Returns the skin for an index; the index wraps around so any player index is valid.</summary>
        public static SkinDefinition GetSkin(int index)
        {
            var lib = Load();
            if (lib == null || lib.skins.Count == 0) return null;
            int n = lib.skins.Count;
            int i = ((index % n) + n) % n;
            return lib.skins[i];
        }

#if UNITY_EDITOR
        public void EditorSetup(List<SkinDefinition> list, Material faceMaterial)
        {
            skins = list;
            facePhotoMaterial = faceMaterial;
        }
#endif
    }
}
