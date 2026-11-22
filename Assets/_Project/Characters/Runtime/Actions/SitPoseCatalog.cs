using System.Collections.Generic;

namespace HideAndSeek.Characters
{
    /// <summary>One seated pose as shown to the player (pose wheel): id = the value of NetPlayer.SitPoseVar.</summary>
    public readonly struct SitPoseInfo
    {
        public readonly byte Id;
        public readonly string Name;
        public readonly bool IsNew;

        public SitPoseInfo(byte id, string name, bool isNew)
        {
            Id = id;
            Name = name;
            IsNew = isNew;
        }
    }

    /// <summary>
    /// Which seated poses (<see cref="SeatPoses"/>) a piece of furniture offers and what they are called.
    /// TEMPORARY version (feature/wheel): only the 4 original poses on every seat. The pose branch (feature/poses)
    /// ships the real catalogue with the same API; on merge its version wins.
    /// </summary>
    public static class SitPoseCatalog
    {
        static readonly SitPoseInfo[] Basic =
        {
            new SitPoseInfo(0, "Prosto", false),
            new SitPoseInfo(1, "Na luzie", false),
            new SitPoseInfo(2, "Łokcie na kolanach", false),
            new SitPoseInfo(3, "Noga na nogę", false),
        };

        /// <summary>Poses offered on this seat, in pose-wheel order (clockwise from the top).</summary>
        public static IReadOnlyList<SitPoseInfo> For(SeatShape shape) => Basic;

        /// <summary>True when <paramref name="id"/> is one of the poses of this seat.</summary>
        public static bool Allowed(SeatShape shape, byte id)
        {
            var list = For(shape);
            for (int i = 0; i < list.Count; i++)
                if (list[i].Id == id) return true;
            return false;
        }
    }
}
