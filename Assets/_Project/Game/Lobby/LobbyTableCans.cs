using HideAndSeek.Characters;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Decorative soda cans on the lounge table: the detailed <see cref="SodaCan"/> model (label art, lid, tab) is built
    /// at runtime, so the lobby builder only stores where they stand. Local position = can centre.
    /// </summary>
    public sealed class LobbyTableCans : MonoBehaviour
    {
        [SerializeField] private Vector3[] positions = new Vector3[0];
        [SerializeField] private Vector3[] angles = new Vector3[0];
        [SerializeField] private int[] brands = new int[0];
        [SerializeField] private bool[] open = new bool[0];
        [SerializeField] private float scale = 1.4f;

        public void Set(Vector3[] pos, Vector3[] rot, int[] brand, bool[] opened, float s)
        {
            positions = pos;
            angles = rot;
            brands = brand;
            open = opened;
            scale = s;
        }

        private void Awake()
        {
            for (int i = 0; i < positions.Length; i++)
            {
                var can = SodaCan.Create(transform, i < brands.Length ? brands[i] : i, gameObject.layer, scale);
                can.transform.localPosition = positions[i];
                can.transform.localRotation = Quaternion.Euler(i < angles.Length ? angles[i] : Vector3.zero);
                if (i < open.Length && open[i]) can.SetOpen(1f);
            }
        }
    }
}
