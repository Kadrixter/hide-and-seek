using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// A place to sit in the lobby (sofa cushion, pouf). The transform sits on the floor in front of the seat,
    /// facing away from it; the seated pose puts the pelvis ~0.12 m behind and 0.5 m above this point.
    /// </summary>
    public class LobbySeat : MonoBehaviour
    {
        private static readonly List<LobbySeat> Seats = new();

        public static IReadOnlyList<LobbySeat> All => Seats;

        public int Index => Seats.IndexOf(this);

        /// <summary>Point players look at to get the "sit" prompt (the cushion).</summary>
        public Vector3 AimPoint => transform.position + Vector3.up * 0.45f - transform.forward * 0.15f;

        public static LobbySeat Get(int index) => index >= 0 && index < Seats.Count ? Seats[index] : null;

        private void OnEnable()
        {
            Seats.Add(this);
            Seats.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        }

        private void OnDisable() => Seats.Remove(this);

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(AimPoint, 0.12f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.4f);
        }
#endif
    }
}
