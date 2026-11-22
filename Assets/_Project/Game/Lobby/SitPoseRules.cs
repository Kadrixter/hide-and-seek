using System.Collections.Generic;
using HideAndSeek.Characters;
using HideAndSeek.Game.Net;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Which seated poses a player may switch to on a lobby seat (pose wheel, server check of the request). The single
    /// place that asks <see cref="SitPoseCatalog"/>: when the catalogue's check gets more inputs (e.g. whether the
    /// neighbouring seats are taken, for lying down), only this file changes; <see cref="NeighbourTaken"/> already
    /// answers that.
    /// </summary>
    public static class SitPoseRules
    {
        /// <summary>Poses of the seat in wheel order (empty when there is no seat).</summary>
        public static IReadOnlyList<SitPoseInfo> Poses(LobbySeat seat) =>
            SitPoseCatalog.For(seat != null ? seat.Shape : SeatShape.Generic());

        /// <summary>True when <paramref name="who"/>, sitting on <paramref name="seat"/>, may take pose <paramref name="id"/> now.</summary>
        public static bool Allowed(LobbySeat seat, byte id, NetPlayer who)
        {
            if (seat == null) return false;
            return SitPoseCatalog.Allowed(seat.Shape, id, NeighbourTaken(seat, who));
        }

        /// <summary>
        /// True when another player sits on a seat of the same piece of furniture (the other cushion of the sofa).
        /// </summary>
        public static bool NeighbourTaken(LobbySeat seat, NetPlayer who)
        {
            if (seat == null || seat.transform.parent == null) return false;
            var furniture = seat.transform.parent;
            foreach (var p in NetPlayer.All)
            {
                if (p == null || p == who || !p.IsSpawned) continue;
                var s = LobbySeat.Get(p.Seat.Value);
                if (s != null && s != seat && s.transform.parent == furniture) return true;
            }
            return false;
        }
    }
}
