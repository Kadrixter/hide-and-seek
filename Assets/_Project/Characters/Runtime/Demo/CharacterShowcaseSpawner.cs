using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>Spawns a blocky character under this object at runtime (used by the showcase scene).</summary>
    public class CharacterShowcaseSpawner : MonoBehaviour
    {
        public int skinIndex;
        public string playerNick;

        public BlockyCharacter Character { get; private set; }

        void Awake()
        {
            Character = BlockyCharacterFactory.Create(transform, skinIndex, playerNick);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.6f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(new Vector3(0f, 0.9f, 0f), new Vector3(0.5f, 1.8f, 0.3f));
        }
    }
}
