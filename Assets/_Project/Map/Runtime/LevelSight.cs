using UnityEngine;

namespace HideAndSeek.Map
{
    /// <summary>
    /// Whether a light can show anything to a camera across the garage levels. The concrete slabs cut every level off
    /// from the others; the only way between two levels is an opening (ramp shaft or stairwell, <see cref="LevelLighting.Holes"/>).
    /// Same level: yes. Neighbour level: only while the camera and the light are both close to the same opening (path
    /// camera - opening - light up to <see cref="OpeningReach"/>). Two levels apart: never. Outside the garage volume
    /// (lobby, race track, above the roof) nothing is restricted. The level of a point is the air it is in
    /// (<see cref="LevelLighting.LevelAt"/>), so on a ramp it changes with the height, like for the light culling masks.
    /// </summary>
    public static class LevelSight
    {
        /// <summary>Longest path (camera to opening + opening to light, on the xz plane) that still links two levels.</summary>
        public const float OpeningReach = 30f;

        /// <summary>True when a light at <paramref name="light"/> may be shown to a camera at <paramref name="eye"/>.</summary>
        public static bool Sees(Vector3 eye, Vector3 light)
        {
            var ll = LevelLighting.Instance;
            if (ll == null || !ll.Restricts(eye) || !ll.Restricts(light)) return true;
            int le = LevelLighting.LevelAt(eye.y), lt = LevelLighting.LevelAt(light.y);
            if (le == lt) return true;
            if (Mathf.Abs(le - lt) > 1) return false;
            var holes = ll.Holes;
            for (int i = 0; i < holes.Count; i++)
                if (DistToArea(holes[i], eye) + DistToArea(holes[i], light) <= OpeningReach) return true;
            return false;
        }

        private static float DistToArea(Rect r, Vector3 p)
        {
            float dx = Mathf.Max(r.xMin - p.x, 0f, p.x - r.xMax);
            float dz = Mathf.Max(r.yMin - p.z, 0f, p.z - r.yMax);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
