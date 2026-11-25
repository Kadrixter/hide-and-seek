using UnityEngine;

namespace HideAndSeek.Core
{
    /// <summary>
    /// Free room around a character at leg height, walls only (floors, ramps, other players, loose props, ragdolls and
    /// triggers do not count). Shared by the motor (a lying body may not back or turn into a wall) and the character
    /// animation (the legs fold up when a wall behind is closer than they reach), so nobody sees legs through a wall.
    /// </summary>
    public static class BodyRoom
    {
        /// <summary>Test switch: false = no footprint push and no leg folding (before / after comparisons).</summary>
        public static bool Enabled = true;

        /// <summary>Test switch: false = only the leg folding works (the motor does not push), to see the fallback alone.</summary>
        public static bool MotorPush = true;

        /// <summary>Layers that never count as a wall: Ignore Raycast, the local body, the view model and ragdolls.</summary>
        public const int Mask = ~((1 << 2) | (1 << 8) | (1 << 9) | GameLayers.RagdollMask);

        /// <summary>
        /// The swept box: as wide as the splayed legs of a crawl (still inside the 0.3 m capsule at the start), from 0.11 m
        /// to 0.29 m above the feet (kerbs and speed bumps pass under it).
        /// </summary>
        private const float HalfWidth = 0.28f, HalfHeight = 0.09f, HalfDepth = 0.04f, Height = 0.2f;

        private static readonly RaycastHit[] Hits = new RaycastHit[16];

        /// <summary>
        /// Horizontal distance from <paramref name="feet"/> (the character root) to the nearest wall along
        /// <paramref name="dir"/>, or <paramref name="maxDist"/> when there is none. Colliders under
        /// <paramref name="self"/> are ignored.
        /// </summary>
        public static float Free(Vector3 feet, Vector3 dir, float maxDist, Transform self)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f || maxDist <= HalfDepth) return maxDist;
            dir.Normalize();
            Vector3 center = feet + Vector3.up * Height;
            var half = new Vector3(HalfWidth, HalfHeight, HalfDepth);
            int n = Physics.BoxCastNonAlloc(center, half, dir, Hits, Quaternion.LookRotation(dir), maxDist - HalfDepth,
                Mask, QueryTriggerInteraction.Ignore);
            float best = maxDist;
            for (int i = 0; i < n; i++)
                if (IsWall(Hits[i], self)) best = Mathf.Min(best, Hits[i].distance + HalfDepth);
            return best;
        }

        /// <summary>
        /// Sweeps a sphere of <paramref name="radius"/> from <paramref name="from"/> towards <paramref name="to"/>; true
        /// with the first wall contact in <paramref name="wall"/>. The start must be free (inside the own capsule).
        /// </summary>
        public static bool SweepWall(Vector3 from, Vector3 to, float radius, Transform self, out RaycastHit wall)
        {
            wall = default;
            Vector3 d = to - from;
            float len = d.magnitude;
            if (len < 1e-4f) return false;
            int n = Physics.SphereCastNonAlloc(from, radius, d / len, Hits, len, Mask, QueryTriggerInteraction.Ignore);
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                if (!IsWall(Hits[i], self) || (found && Hits[i].distance >= wall.distance)) continue;
                wall = Hits[i];
                found = true;
            }
            return found;
        }

        private static bool IsWall(in RaycastHit h, Transform self)
        {
            if (h.distance <= 0f) return false; // started inside: the own capsule zone, never a wall in front of the body
            if (Mathf.Abs(h.normal.y) > 0.6f) return false; // floor, ramp, ceiling
            var col = h.collider;
            if (col == null || col is CharacterController) return false; // players
            if (self != null && col.transform.IsChildOf(self)) return false;
            var rb = h.rigidbody;
            return rb == null || rb.isKinematic; // loose props get pushed, they do not hide legs
        }
    }
}
