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
    /// Which seated poses (<see cref="SeatPoses"/>) a piece of furniture offers and what they are called. Ids are the
    /// values of NetPlayer.SitPoseVar: 0-3 everywhere (also picked at random when sitting down), 4-7 sofa only (4-6 lie
    /// along both cushions: the other seat of the sofa must be free), 8-11 pouf only. The pose is applied the usual
    /// way: SitPoseVar -> BlockyCharacterActions.SetSitLook -> SeatPoses.
    /// </summary>
    public static class SitPoseCatalog
    {
        static readonly SitPoseInfo[] Basic =
        {
            new SitPoseInfo(0, "Prosto", false),
            new SitPoseInfo(1, "Na luzie", false),
            new SitPoseInfo(2, "Łokcie na kolanach", false),
            new SitPoseInfo(3, "Kostka na kolanie", false),
        };

        static readonly SitPoseInfo[] Sofa =
        {
            Basic[0], Basic[1], Basic[2], Basic[3],
            new SitPoseInfo(4, "Francuska dziewczyna", true),
            new SitPoseInfo(5, "Bez trosk", true),
            new SitPoseInfo(6, "Kocica", true),
            new SitPoseInfo(7, "Diwa", true),
        };

        static readonly SitPoseInfo[] Pouf =
        {
            Basic[0], Basic[1], Basic[2], Basic[3],
            new SitPoseInfo(8, "Femme fatale", true),
            new SitPoseInfo(9, "Pin-up", true),
            new SitPoseInfo(10, "Plażowiczka", true),
            new SitPoseInfo(11, "Modelka", true),
        };

        /// <summary>Poses offered on this seat, in pose-wheel order (clockwise from the top). Null = generic seat.</summary>
        public static IReadOnlyList<SitPoseInfo> For(SeatShape shape)
        {
            var kind = shape != null ? (shape.MirrorSource ?? shape) : null;
            if (kind == null) return Basic;
            if (kind.Kind == SeatKind.Pouf) return Pouf;
            if (kind.Kind == SeatKind.Sofa && SeatPoses.Supports(kind, 4)) return Sofa;
            return Basic;
        }

        /// <summary>True when <paramref name="id"/> is one of the poses of this seat (whether it is free to take now aside).</summary>
        public static bool Allowed(SeatShape shape, byte id)
        {
            var list = For(shape);
            for (int i = 0; i < list.Count; i++)
                if (list[i].Id == id) return true;
            return false;
        }

        /// <summary>
        /// True when the pose can be taken now: one of the seat's poses, and for the lying ones
        /// (<see cref="NeedsFreeNeighbour"/>) nobody sits on the other seat of the same sofa.
        /// </summary>
        public static bool Allowed(SeatShape shape, byte id, bool neighbourTaken) =>
            Allowed(shape, id) && (!neighbourTaken || !NeedsFreeNeighbour(id));

        /// <summary>The pose lies along the whole sofa: the other seat must be free, and stays blocked while it is held.</summary>
        public static bool NeedsFreeNeighbour(byte id) => SeatPoses.SpansNeighbour(id);

        /// <summary>Display name of a pose id ("" when unknown).</summary>
        public static string Name(byte id)
        {
            foreach (var p in Sofa) if (p.Id == id) return p.Name;
            foreach (var p in Pouf) if (p.Id == id) return p.Name;
            return "";
        }
    }
}
