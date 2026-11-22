using UnityEngine;

namespace HideAndSeek.Map
{
    /// <summary>
    /// What gameplay may assume about the loaded garage map: its footprint (inside the outer walls), the levels and
    /// the height of their floor tops, and the slab openings (ramp shafts, stairwells) that connect the levels.
    /// Read from the <see cref="LevelLighting"/> component the garage builder puts on the map root; without it (lobby,
    /// other scenes) <see cref="HasGarage"/> is false and callers only rely on physics.
    /// Floor tops: level 0 at y 0, level 1 at -3.4, level 2 at -6.8 (ParkingGarageBuilder.FloorY).
    /// </summary>
    public static class MapInfo
    {
        public const int Levels = LevelLighting.Levels;
        public const float LevelHeight = LevelLighting.LevelHeight;
        /// <summary>Thickness of the outer walls (the footprint in LevelLighting includes them).</summary>
        public const float OuterWall = 0.4f;

        /// <summary>True while the garage map is loaded.</summary>
        public static bool HasGarage => LevelLighting.Instance != null;

        /// <summary>Half size (x, z) of the walkable inside of the garage (inner faces of the outer walls).</summary>
        public static Vector2 InnerHalfSize
        {
            get
            {
                var ll = LevelLighting.Instance;
                Vector2 h = ll != null ? ll.HalfSize : new Vector2(90.4f, 60.4f);
                return new Vector2(h.x - OuterWall, h.y - OuterWall);
            }
        }

        /// <summary>Height of the floor top of <paramref name="level"/> (0 = top level).</summary>
        public static float FloorY(int level) => -LevelHeight * Mathf.Clamp(level, 0, Levels - 1);

        /// <summary>Level of something standing / lying at <paramref name="feetY"/> (a little below the floor top counts too).</summary>
        public static int LevelOf(float feetY) => LevelLighting.LevelAt(feetY + 0.3f);

        /// <summary>Floor top under something standing at <paramref name="feetY"/>.</summary>
        public static float FloorUnder(float feetY) => FloorY(LevelOf(feetY));

        /// <summary>
        /// Inside the garage walls with <paramref name="margin"/> metres to spare (negative = that far outside still
        /// counts). Always true when no garage is loaded.
        /// </summary>
        public static bool InsideGarage(Vector3 p, float margin = 0f)
        {
            if (!HasGarage) return true;
            var h = InnerHalfSize;
            return Mathf.Abs(p.x) <= h.x - margin && Mathf.Abs(p.z) <= h.y - margin &&
                   p.y <= FloorY(0) + LevelHeight && p.y >= FloorY(Levels - 1) - 1f;
        }

        /// <summary>Inside a slab opening (ramp shaft, stairwell) grown by <paramref name="pad"/> metres.</summary>
        public static bool InSlabOpening(Vector3 p, float pad = 0f)
        {
            var ll = LevelLighting.Instance;
            if (ll == null) return false;
            var holes = ll.Holes;
            for (int i = 0; i < holes.Count; i++)
            {
                var r = holes[i];
                if (p.x >= r.xMin - pad && p.x <= r.xMax + pad && p.z >= r.yMin - pad && p.z <= r.yMax + pad) return true;
            }
            return false;
        }
    }
}
