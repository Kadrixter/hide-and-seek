using System.Collections.Generic;
using HideAndSeek.Characters;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Bodies on neighbouring lobby seats must not overlap (the L of sofas, a sofa end next to a pouf, two poufs side
    /// by side): a pose that would reach into somebody sitting on a nearby seat is not offered, and when somebody sits
    /// down next to a pose that now overlaps, one of the two changes to a pose that fits (smoothly, through the normal
    /// pose change). Overlap = the rig's part boxes of both seated bodies (neutral look) intersect by more than the
    /// clothing layer; results are cached per seat / pose pair (the seats never move).
    /// </summary>
    public static class SeatConflicts
    {
        const float Allow = 0.015f;
        static readonly Dictionary<long, bool> Cache = new Dictionary<long, bool>();
        static readonly PartBox[] A = new PartBox[BodyBoxes.Count], B = new PartBox[BodyBoxes.Count];

        /// <summary>Pose <paramref name="va"/> on <paramref name="a"/> and <paramref name="vb"/> on <paramref name="b"/> intersect.</summary>
        public static bool Overlap(LobbySeat a, int va, LobbySeat b, int vb)
        {
            if (a == null || b == null || a == b) return false;
            if ((a.transform.position - b.transform.position).sqrMagnitude > 2.6f * 2.6f) return false;
            // Seat identity by the object (the editor seat check has no seat list), smaller one first.
            long ia = (uint)a.GetHashCode(), ib = (uint)b.GetHashCode();
            if (ia > ib) { (ia, ib) = (ib, ia); (va, vb) = (vb, va); (a, b) = (b, a); }
            long key = (((ia * 1000003L) ^ ib) << 10) ^ (va * 31 + vb);
            if (Cache.TryGetValue(key, out bool hit)) return hit;
            hit = Compute(a, va, b, vb);
            Cache[key] = hit;
            return hit;
        }

        static bool Compute(LobbySeat a, int va, LobbySeat b, int vb)
        {
            Fill(a, va, A);
            Fill(b, vb, B);
            for (int i = 0; i < BodyBoxes.Count; i++)
            for (int j = 0; j < BodyBoxes.Count; j++)
                if (BodyBoxes.Penetration(A[i], B[j]) > Allow) return true;
            return false;
        }

        static void Fill(LobbySeat s, int v, PartBox[] boxes)
        {
            var lim = SeatPoses.Limits(v, s.Shape);
            BodyBoxes.Get(SeatPoses.Evaluate(v, s.Shape, lim.Center, lim.Pitch), boxes);
            for (int i = 0; i < boxes.Length; i++) boxes[i] = boxes[i].Transformed(s.transform.position, s.transform.rotation);
        }

        static readonly Dictionary<long, bool> Blocked = new Dictionary<long, bool>();
        static readonly Collider[] Found = new Collider[16];

        /// <summary>
        /// Pose <paramref name="v"/> on <paramref name="seat"/> reaches into other furniture (a coffee table in front of the
        /// sofa, a lamp): such a pose is not offered on that seat. Tested once per seat / pose against the scene's
        /// colliders (the seat's own furniture and characters ignored), the body boxes shrunk by the soft clothing layer.
        /// </summary>
        public static bool BlockedByFurniture(LobbySeat seat, int v)
        {
            if (seat == null) return false;
            long key = ((long)(uint)seat.GetHashCode() << 8) | (uint)v;
            if (Blocked.TryGetValue(key, out bool hit)) return hit;
            Fill(seat, v, A);
            Transform own = seat.transform.parent != null ? seat.transform.parent : seat.transform;
            hit = false;
            for (int i = 0; i < BodyBoxes.Count && !hit; i++)
            {
                var b = A[i];
                Vector3 half = b.Half - Vector3.one * Allow;
                int n = Physics.OverlapBoxNonAlloc(b.Center, half, Found, b.Rot, ~0, QueryTriggerInteraction.Ignore);
                for (int k = 0; k < n; k++)
                {
                    var c = Found[k];
                    if (c == null || c.transform.IsChildOf(own) || c is CharacterController || c.GetComponentInParent<NetPlayer>() != null) continue;
                    if (c.bounds.size.x > 6f || c.bounds.size.z > 6f) continue; // floor, walls
                    hit = true;
                    break;
                }
            }
            Blocked[key] = hit;
            return hit;
        }

        /// <summary>Pose <paramref name="v"/> on <paramref name="seat"/> would reach into somebody else sitting nearby.</summary>
        public static bool ConflictsWithOthers(LobbySeat seat, int v, NetPlayer who)
        {
            if (seat == null) return false;
            foreach (var p in NetPlayer.All)
            {
                if (p == null || p == who || !p.IsSpawned || p.Seat.Value < 0) continue;
                var other = LobbySeat.Get(p.Seat.Value);
                if (other != null && Overlap(seat, v, other, p.SitPoseVar.Value)) return true;
            }
            return false;
        }

        /// <summary>
        /// The pose to take on <paramref name="seat"/>: <paramref name="preferred"/> if it fits next to the others, else the
        /// first of the plain sitting poses that does (0 when none does).
        /// </summary>
        public static int SafePose(LobbySeat seat, NetPlayer who, int preferred)
        {
            if (SitPoseRules.Allowed(seat, (byte)preferred, who)) return preferred;
            for (int v = 0; v < SeatPoses.RandomCount; v++)
                if (SitPoseRules.Allowed(seat, (byte)v, who)) return v;
            return 0;
        }

        /// <summary>
        /// Who gives way when two seated bodies overlap: a pin-up pose before a plain one, else the player with the higher
        /// client id (decided the same way on both computers, so only one of them changes).
        /// </summary>
        public static bool ShouldYield(NetPlayer me, NetPlayer other)
        {
            bool mine = me.SitPoseVar.Value >= SeatPoses.RandomCount, theirs = other.SitPoseVar.Value >= SeatPoses.RandomCount;
            if (mine != theirs) return mine;
            return me.OwnerClientId > other.OwnerClientId;
        }
    }
}
