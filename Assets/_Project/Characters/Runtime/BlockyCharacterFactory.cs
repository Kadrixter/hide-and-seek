using UnityEngine;

namespace HideAndSeek.Characters
{
    public static class BlockyCharacterFactory
    {
        /// <summary>
        /// Builds the full character as a child of <paramref name="parent"/> (feet at the parent's origin, facing +Z),
        /// applies the skin and adds a <see cref="BlockyCharacterAnimator"/> that reads the
        /// ICharacterMotionSource found on the parent chain (idle when there is none),
        /// plus the <see cref="BlockyCharacterActions"/> layer (ICharacterActions, see <see cref="BlockyCharacter.Actions"/>).
        /// </summary>
        public static BlockyCharacter Create(Transform parent, int skinIndex, string playerNick = null)
        {
            var go = new GameObject("BlockyCharacter");
            if (parent != null)
            {
                go.layer = parent.gameObject.layer;
                go.transform.SetParent(parent, false);
            }

            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            var character = go.AddComponent<BlockyCharacter>();
            character.Build();
            character.SetSkin(skinIndex, playerNick);
            go.AddComponent<BlockyCharacterAnimator>();
            go.AddComponent<BlockyCharacterActions>();
            return character;
        }
    }
}
