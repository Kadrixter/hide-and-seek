using System.Collections.Generic;
using HideAndSeek.Characters;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// A place to sit in the lobby (sofa cushion, pouf). The transform sits on the floor in front of the seat,
    /// facing away from it; the seated poses (<see cref="SeatPoses"/>) are fitted to <see cref="Shape"/>, which is
    /// recognised from the furniture the seat belongs to (no scene data needed).
    /// </summary>
    public class LobbySeat : MonoBehaviour
    {
        private static readonly List<LobbySeat> Seats = new();

        public static IReadOnlyList<LobbySeat> All => Seats;

        public int Index => Seats.IndexOf(this);

        /// <summary>Point players look at to get the "sit" prompt (the cushion).</summary>
        public Vector3 AimPoint => transform.position + Vector3.up * 0.45f - transform.forward * 0.15f;

        public static LobbySeat Get(int index) => index >= 0 && index < Seats.Count ? Seats[index] : null;

        private SeatShape _shape;

        /// <summary>The furniture under this seat in seat space (sofa cushion with backrest and armrest, or a pouf).</summary>
        public SeatShape Shape => _shape ??= DetectShape();

        private SeatShape DetectShape()
        {
            string parent = transform.parent != null ? transform.parent.name : "";
            if (parent.StartsWith("Sofa")) return SeatShape.Sofa(transform.localPosition.x);
            if (parent.StartsWith("Pouf")) return SeatShape.Pouf();
            return SeatShape.Generic();
        }

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
