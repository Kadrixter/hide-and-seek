using System.Collections.Generic;
using HideAndSeek.Map;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Fuses on the road (2026-11-24): candidate spots on a grid over the driving lanes of a level - the six aisles between
    /// the bay rows and the two cross aisles at the east / west ends (layout of ParkingGarageBuilder). Every candidate goes
    /// through <see cref="ItemPlacement.Check"/> (flat floor of the level, nothing on it, a player fits above it: no
    /// parked car, barrier or speed bump) and <see cref="OnMap"/>. The grid is built once per round (all fuses are placed
    /// in the same frame) and a fuse takes a random spot far from the other fuses and the boxes.
    /// </summary>
    public static partial class FuseSpots
    {
        private static readonly float[] AisleZ = { -44.75f, -28.25f, -11.75f, 11.75f, 28.25f, 44.75f };
        private const float LaneX = 73f, CrossX = 79.5f, CrossZMin = -50f, CrossZMax = 46f;
        /// <summary>Across a lane: the middle and 1.6 m to each side (the lanes are 6.5 m between the painted lines).</summary>
        private static readonly float[] Across = { -1.6f, 0f, 1.6f };
        private const float Step = 3f;

        private static readonly Dictionary<int, List<Vector3>> RoadCache = new();
        private static int _roadFrame = -1;

        /// <summary>Every usable road spot of <paramref name="level"/> (cached for this frame).</summary>
        public static List<Vector3> RoadSpots(int level)
        {
            if (_roadFrame != Time.frameCount)
            {
                _roadFrame = Time.frameCount;
                RoadCache.Clear();
            }
            if (RoadCache.TryGetValue(level, out var cached)) return cached;
            var list = new List<Vector3>();
            if (MapInfo.HasGarage)
            {
                float y = MapInfo.FloorY(level);
                foreach (float z in AisleZ)
                    for (float x = -LaneX; x <= LaneX; x += Step)
                        foreach (float d in Across) TryRoad(list, new Vector3(x, y, z + d), level);
                foreach (float sx in new[] { -1f, 1f })
                    for (float z = CrossZMin; z <= CrossZMax; z += Step)
                        foreach (float d in Across) TryRoad(list, new Vector3(sx * CrossX + d, y, z), level);
            }
            Debug.Log($"[Fuses] level {level}: {list.Count} road spots for fuses");
            RoadCache[level] = list;
            return list;
        }

        private static void TryRoad(List<Vector3> list, Vector3 c, int level)
        {
            if (ItemPlacement.Check(c, c.y, c, ItemPlacement.Pickup, out var p) != ItemPlacement.Reject.None) return;
            if (MapInfo.LevelOf(p.y) != level || !OnMap(p)) return;
            list.Add(p);
        }

        /// <summary>A random road spot of the level away from the other fuses and the boxes (spacing relaxed when crowded).</summary>
        public static Vector3? RoadSpot(int level, System.Random rnd, List<Vector3> avoid, List<Vector3> boxes)
        {
            var all = RoadSpots(level);
            if (all.Count == 0) return null;
            foreach (float spacing in new[] { FuseTuning.FuseSpacing, FuseTuning.FuseSpacing * 0.5f, 3f })
            {
                var pool = all.FindAll(p => Clear(p, avoid, spacing) && Clear(p, boxes, FuseTuning.BoxClearance));
                if (pool.Count > 0) return pool[rnd.Next(pool.Count)];
            }
            return null;
        }

        private static bool Clear(Vector3 p, List<Vector3> others, float dist)
        {
            if (others == null) return true;
            foreach (var o in others)
                if (Mathf.Abs(o.y - p.y) < 1.5f && Flat(o - p).magnitude < dist) return false;
            return true;
        }
    }
}
