using HideAndSeek.Characters;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Lying along the sofa (pin-up poses 4-6, <see cref="SitPoseCatalog.NeedsFreeNeighbour"/>) takes both seats of
    /// the sofa: such a pose can only be taken while the other seat is free, nobody can sit down on that seat while
    /// somebody lies there, and if two players race for it (one switches to lying while the other sits down) the one
    /// lying sits up (pose 0) on his own computer, which is where the owner-written pose lives.
    /// </summary>
    public partial class NetPlayer
    {
        /// <summary>Somebody other than <paramref name="who"/> sits on the other seat of <paramref name="seat"/>'s sofa.</summary>
        public static bool SeatNeighbourTaken(LobbySeat seat, NetPlayer who = null)
        {
            var n = seat != null ? seat.Neighbour : null;
            if (n == null) return false;
            int idx = n.Index;
            foreach (var p in All)
                if (p != null && p != who && p.IsSpawned && p.Seat.Value == idx) return true;
            return false;
        }

        /// <summary>The seat is free but its neighbour lies along the whole sofa.</summary>
        public static bool SeatBlockedByLying(int index)
        {
            var seat = LobbySeat.Get(index);
            var n = seat != null ? seat.Neighbour : null;
            if (n == null) return false;
            int idx = n.Index;
            foreach (var p in All)
                if (p != null && p.IsSpawned && p.Seat.Value == idx && SitPoseCatalog.NeedsFreeNeighbour(p.SitPoseVar.Value)) return true;
            return false;
        }

        /// <summary>Owner, seated: a lying pose with somebody on the other seat of the sofa gives way to sitting.</summary>
        private void GiveUpLyingIfCrowded(int seat)
        {
            if (!SitPoseCatalog.NeedsFreeNeighbour(SitPoseVar.Value)) return;
            if (SeatNeighbourTaken(LobbySeat.Get(seat), this)) SitPoseVar.Value = 0;
        }
    }
}
