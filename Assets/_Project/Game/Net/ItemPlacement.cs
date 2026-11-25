using System.Collections.Generic;
using HideAndSeek.Map;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Server: where an item may lie on the garage floor. Every random spot (medkits, syringes, phones, toy guns, rods)
    /// and every spot where something is put down or falls (Q drops, the Glock that does not fit, a dead holder's
    /// phone / RC car) goes through <see cref="Check"/>:
    /// 1. inside the garage walls (<see cref="MapInfo"/>) and not in a ramp shaft / stairwell opening,
    /// 2. nothing solid on the straight line from the base point (knee and chest height, both ways: a wall, a fence,
    ///    a car between them = rejected, so nothing ends up behind a wall or outside),
    /// 3. the first surface under it is flat floor at the height of the base point's floor (not a car roof / bonnet,
    ///    a wall top, a lower level or nothing),
    /// 4. free space: the item's box does not touch anything and (pickups) a player fits above it (not under a car,
    ///    not in a wall, a column or a rack).
    /// <see cref="Scatter"/> tries several random spots and falls back to the base point on its floor.
    /// Players and dropped item views never count as obstacles.
    /// </summary>
    public static class ItemPlacement
    {
        public struct Profile
        {
            /// <summary>Half width (x, z) and height of the item's box.</summary>
            public float HalfWidth, Height;
            /// <summary>Free height above the item for a player (0 = only the box has to be free).</summary>
            public float Headroom;
            /// <summary>Radius of the line-of-sight cast from the base point.</summary>
            public float LosRadius;
            /// <summary>Random distance from the base point (<see cref="Scatter"/>).</summary>
            public float MinRadius, MaxRadius;
            public int Attempts;
            /// <summary>Minimum distance to the other items placed in the same pass.</summary>
            public float Spacing;
        }

        /// <summary>Medkit / adrenaline near a hider spawn point: 1.5-4 m away, a player must be able to stand there.</summary>
        public static readonly Profile Pickup = new Profile
        {
            HalfWidth = 0.2f, Height = 0.22f, Headroom = 1.55f, LosRadius = 0.18f,
            MinRadius = 1.5f, MaxRadius = 4f, Attempts = 14, Spacing = 1.2f,
        };

        /// <summary>A small thing put on the floor next to a player or an object (phone, toy gun, RC car).</summary>
        public static readonly Profile Small = new Profile
        {
            HalfWidth = 0.14f, Height = 0.1f, Headroom = 0f, LosRadius = 0.08f,
            MinRadius = 0.3f, MaxRadius = 1.1f, Attempts = 10, Spacing = 0f,
        };

        public enum Reject : byte { None, OutsideMap, Opening, Blocked, NoFloor, Steep, WrongHeight, Occupied, TooClose, Count }

        /// <summary>Statistics for the -testplace probe (rejected candidates by reason, fallbacks).</summary>
        public static readonly int[] Rejected = new int[(int)Reject.Count];
        public static int Placed, Fallbacks;
        /// <summary>-testplace: log every rejected candidate.</summary>
        public static bool Verbose;

        public static void ResetStats()
        {
            System.Array.Clear(Rejected, 0, Rejected.Length);
            Placed = Fallbacks = 0;
        }

        /// <summary>UI, Ignore Raycast, the local body and view models never count.</summary>
        public const int Mask = ~((1 << 2) | (1 << 5) | (1 << 8) | (1 << 9));

        private static readonly RaycastHit[] Hits = new RaycastHit[24];
        private static readonly Collider[] Cols = new Collider[24];

        // ================================================================ queries (players / dropped items ignored)

        private static bool Ignored(Collider c, Transform ignore)
        {
            if (c == null) return true;
            if (ignore != null && c.transform.IsChildOf(ignore)) return true;
            return c.GetComponentInParent<NetPlayer>() != null || c.GetComponentInParent<DroppedItemView>() != null;
        }

        /// <summary>Nearest solid hit of a ray (<paramref name="radius"/> 0) or a sphere cast.</summary>
        public static bool FirstHit(Vector3 from, Vector3 dir, float dist, float radius, out RaycastHit best, Transform ignore = null)
        {
            best = default;
            if (dist <= 1e-4f) return false;
            int n = radius > 0f
                ? Physics.SphereCastNonAlloc(from, radius, dir, Hits, dist, Mask, QueryTriggerInteraction.Ignore)
                : Physics.RaycastNonAlloc(from, dir, Hits, dist, Mask, QueryTriggerInteraction.Ignore);
            bool any = false;
            for (int i = 0; i < n; i++)
            {
                var h = Hits[i];
                if (Ignored(h.collider, ignore)) continue;
                // Sphere casts report colliders they start in with distance 0 and no point: that is "blocked right here".
                if (!any || h.distance < best.distance) { best = h; any = true; }
            }
            return any;
        }

        /// <summary>Something solid on the segment, checked both ways (a ray never sees a collider it starts inside).</summary>
        public static bool Blocked(Vector3 a, Vector3 b, float radius, Transform ignore = null)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 1e-3f) return false;
            d /= len;
            return FirstHit(a, d, len, radius, out _, ignore) || FirstHit(b, -d, len, 0f, out _, ignore);
        }

        public static bool BoxFree(Vector3 center, Vector3 half, Transform ignore = null)
        {
            int n = Physics.OverlapBoxNonAlloc(center, half, Cols, Quaternion.identity, Mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) if (!Ignored(Cols[i], ignore)) return false;
            return true;
        }

        public static bool CapsuleFree(Vector3 a, Vector3 b, float radius, Transform ignore = null)
        {
            int n = Physics.OverlapCapsuleNonAlloc(a, b, radius, Cols, Mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) if (!Ignored(Cols[i], ignore)) return false;
            return true;
        }

        public static bool SphereFree(Vector3 c, float radius, Transform ignore = null)
        {
            int n = Physics.OverlapSphereNonAlloc(c, radius, Cols, Mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) if (!Ignored(Cols[i], ignore)) return false;
            return true;
        }

        // ================================================================ floor

        /// <summary>
        /// Height of the floor something at <paramref name="p"/> stands on: the walkable surface right under it when
        /// that is the level's floor or a raised platform of it (dock, car roof: up to 1.6 m), else the level's floor top.
        /// </summary>
        public static float FloorHeight(Vector3 p)
        {
            bool garage = MapInfo.HasGarage;
            float level = garage ? MapInfo.FloorUnder(p.y) : p.y;
            if (FirstHit(p + Vector3.up * 0.5f, Vector3.down, 1.6f, 0f, out var hit) && hit.normal.y >= 0.8f)
            {
                if (!garage) return hit.point.y;
                float dy = hit.point.y - level;
                if (dy >= -0.3f && dy <= 1.6f) return hit.point.y;
            }
            return level;
        }

        /// <summary>The base point itself, on its floor (the fallback that is always safe).</summary>
        public static Vector3 OnFloor(Vector3 p)
        {
            float y = FloorHeight(p);
            return new Vector3(p.x, y, p.z);
        }

        // ================================================================ checks

        /// <summary>
        /// Can an item described by <paramref name="pf"/> lie at <paramref name="candidate"/> (x, z), reached from
        /// <paramref name="basePoint"/> standing on a floor at <paramref name="floorY"/>? <paramref name="placed"/> is the
        /// point on the floor.
        /// </summary>
        public static Reject Check(Vector3 basePoint, float floorY, Vector3 candidate, in Profile pf, out Vector3 placed, Transform ignore = null)
        {
            placed = new Vector3(candidate.x, floorY, candidate.z);
            if (MapInfo.HasGarage)
            {
                if (!MapInfo.InsideGarage(placed, pf.HalfWidth + 0.2f)) return Reject.OutsideMap;
                if (MapInfo.InSlabOpening(placed, 0.5f)) return Reject.Opening;
            }

            // Nothing between the base point and the spot (knee and chest height).
            for (int k = 0; k < 2; k++)
            {
                float h = k == 0 ? 0.45f : 1.1f;
                var a = new Vector3(basePoint.x, floorY + h, basePoint.z);
                var b = new Vector3(candidate.x, floorY + h, candidate.z);
                if (Blocked(a, b, pf.LosRadius, ignore)) return Reject.Blocked;
            }

            // First surface under the spot, from below car-roof height down to under the floor.
            var top = new Vector3(candidate.x, floorY + 1.6f, candidate.z);
            if (!FirstHit(top, Vector3.down, 2.4f, 0f, out var hit, ignore)) return Reject.NoFloor;
            if (hit.normal.y < 0.9f) return Reject.Steep;
            if (Mathf.Abs(hit.point.y - floorY) > 0.25f) return Reject.WrongHeight;
            placed = hit.point;

            // Free space: the item's box, and room for a player above a pickup.
            float hw = pf.HalfWidth, hh = pf.Height * 0.5f;
            if (!BoxFree(placed + Vector3.up * (hh + 0.03f), new Vector3(hw, hh, hw), ignore)) return Reject.Occupied;
            if (pf.Headroom > 0f)
            {
                float r = Mathf.Max(0.25f, hw);
                if (!CapsuleFree(placed + Vector3.up * (r + 0.06f), placed + Vector3.up * Mathf.Max(r + 0.07f, pf.Headroom - r), r, ignore))
                    return Reject.Occupied;
            }
            return Reject.None;
        }

        /// <summary>
        /// A random free spot <see cref="Profile.MinRadius"/>..<see cref="Profile.MaxRadius"/> m from
        /// <paramref name="basePoint"/> on its floor (later attempts closer), away from <paramref name="avoid"/>.
        /// Falls back to the base point on its floor. Returns the floor point.
        /// </summary>
        public static Vector3 Scatter(Vector3 basePoint, in Profile pf, IList<Vector3> avoid = null, string what = "item", Transform ignore = null)
        {
            float floorY = FloorHeight(basePoint);
            for (int k = 0; k < pf.Attempts; k++)
            {
                // Later attempts closer to the base point (1.5-4 m first, 0.75-1.5 m last for a pickup).
                float t = pf.Attempts > 1 ? k / (pf.Attempts - 1f) : 0f;
                float lo = Mathf.Lerp(pf.MinRadius, pf.MinRadius * 0.5f, t);
                float hi = Mathf.Lerp(pf.MaxRadius, Mathf.Max(lo, pf.MinRadius), t);
                float ang = Random.Range(0f, Mathf.PI * 2f);
                float r = Random.Range(lo, hi);
                var c = basePoint + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * r;
                Vector3 placed = c;
                var why = TooClose(c, avoid, pf.Spacing) ? Reject.TooClose : Check(basePoint, floorY, c, pf, out placed, ignore);
                if (why == Reject.None)
                {
                    Placed++;
                    return placed;
                }
                Count(why, what, c);
            }
            Fallbacks++;
            var safe = new Vector3(basePoint.x, floorY, basePoint.z);
            Debug.Log($"[Place] {what}: no free spot around {basePoint} after {pf.Attempts} tries - lies at the base point {safe}");
            return safe;
        }

        /// <summary>
        /// Something put down by a player standing at <paramref name="feet"/>, wanted at <paramref name="want"/>:
        /// pulled back in front of a wall, kept on the player's floor; else a free spot around the player, else the
        /// player's own spot.
        /// </summary>
        public static Vector3 PutDown(Vector3 feet, Vector3 want, in Profile pf, string what = "item", Transform ignore = null)
        {
            float floorY = FloorHeight(feet);
            Vector3 flat = want - feet;
            flat.y = 0f;
            float d = flat.magnitude;
            Vector3 c = feet;
            if (d > 0.02f)
            {
                flat /= d;
                var from = new Vector3(feet.x, floorY + 0.45f, feet.z);
                if (FirstHit(from, flat, d + pf.HalfWidth + 0.1f, pf.LosRadius, out var hit, ignore))
                    d = Mathf.Max(0f, hit.distance - pf.HalfWidth - 0.1f);
                c = feet + flat * d;
            }
            var why = Check(feet, floorY, c, pf, out var placed, ignore);
            if (why == Reject.None)
            {
                Placed++;
                return placed;
            }
            Count(why, what, c);
            return Scatter(feet, pf, null, what, ignore);
        }

        /// <summary>
        /// Where to start a thrown item: <paramref name="want"/> m from <paramref name="origin"/> along
        /// <paramref name="dir"/>, stopped in front of walls / cars; false when even that point is inside something.
        /// </summary>
        public static bool ThrowStart(Vector3 origin, Vector3 dir, float want, float radius, out Vector3 start, out float reach, Transform ignore = null)
        {
            reach = want;
            if (FirstHit(origin, dir, want + radius + 0.05f, radius, out var hit, ignore)) reach = Mathf.Max(0f, hit.distance - 0.1f);
            start = origin + dir * reach;
            if (MapInfo.HasGarage && !MapInfo.InsideGarage(start, radius)) return false;
            return SphereFree(start, radius, ignore);
        }

        /// <summary>
        /// A dropped item came to rest at <paramref name="pos"/> after being thrown by a player standing at
        /// <paramref name="feet"/>: still inside the garage, not stuck in anything, on something solid, not through a
        /// wall (straight line above car height) or through the floor (lower than the player's floor, unless it rolled
        /// down a ramp / stairwell opening). <paramref name="core"/>: half of the item's smallest size.
        /// </summary>
        public static Reject CheckRest(Vector3 pos, Vector3 feet, Transform self, float core)
        {
            if (MapInfo.HasGarage && !MapInfo.InsideGarage(pos, 0.1f)) return Reject.OutsideMap;
            // The middle of the item (a sphere well inside its box) must not be inside a wall / car / column.
            if (!SphereFree(pos, Mathf.Clamp(core, 0.004f, 0.04f), self)) return Reject.Occupied;
            if (!FirstHit(pos + Vector3.up * 0.05f, Vector3.down, 0.9f, 0f, out _, self)) return Reject.NoFloor;
            // The level's floor top, not a car roof the player may stand on (the item may fall off it).
            float levelY = MapInfo.HasGarage ? MapInfo.FloorUnder(feet.y) : FloorHeight(feet);
            if (pos.y < levelY - 0.35f && !MapInfo.InSlabOpening(pos, 1f) && !MapInfo.InSlabOpening(feet, 1f)) return Reject.WrongHeight;
            if (MapInfo.InSlabOpening(pos, 1f) || MapInfo.InSlabOpening(feet, 1f))
            {
                // Stairs / ramp: the level's floor is far under the player, a flat line at its height runs through the
                // steps. Chest -> just above the item instead (it follows the slope).
                if (Blocked(feet + Vector3.up * 1.2f, pos + Vector3.up * 0.3f, 0f, self)) return Reject.Blocked;
                return Reject.None;
            }
            float h = Mathf.Min(feet.y + 1.8f, levelY + 2.4f); // above the cars, under the ceiling beams
            if (Blocked(new Vector3(feet.x, h, feet.z), new Vector3(pos.x, h, pos.z), 0f, self)) return Reject.Blocked;
            return Reject.None;
        }

        // ================================================================ helpers

        private static bool TooClose(Vector3 c, IList<Vector3> avoid, float spacing)
        {
            if (avoid == null || spacing <= 0f) return false;
            for (int i = 0; i < avoid.Count; i++)
            {
                var a = avoid[i];
                if (Mathf.Abs(a.y - c.y) < 1.7f && new Vector2(a.x - c.x, a.z - c.z).sqrMagnitude < spacing * spacing) return true;
            }
            return false;
        }

        public static void Count(Reject why, string what, Vector3 at)
        {
            Rejected[(int)why]++;
            if (Verbose) Debug.Log($"[Place] {what}: rejected {at} ({why})");
        }

        public static string StatsText()
        {
            var sb = new System.Text.StringBuilder($"placed={Placed} fallbacks={Fallbacks} rejected:");
            for (int i = 1; i < (int)Reject.Count; i++) sb.Append($" {(Reject)i}={Rejected[i]}");
            return sb.ToString();
        }
    }
}
