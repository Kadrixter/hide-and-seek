using System.Collections.Generic;
using HideAndSeek.Characters;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Where metal rods are on the game map: stuck in rubble heaps at the "RodSocket" markers the map generator put
    /// on them (position = where the rod enters the rubble, up = the rod, forward = free floor for the puller).
    /// Maps without sockets (older builds): leaning against pallet racks, cage shelves and temporary wire-mesh fences
    /// ("PalletRack_", "Shelf_Cage_", "TempFence_" objects), a free piece of floor right next to such an object.
    /// </summary>
    public static class RodSpots
    {
        private static readonly string[] Prefixes = { "PalletRack_", "Shelf_Cage_", "TempFence_" };
        private static readonly Collider[] Overlap = new Collider[16];

        public struct Spot
        {
            public Vector3 Base, Top;
            public string Near;
            /// <summary>Stuck in rubble; <see cref="Stand"/> = where the puller stands.</summary>
            public bool Embedded;
            public Vector3 Stand;
        }

        public const string SocketName = "RodSocket";

        /// <summary>Rod sockets on the rubble heaps with a free spot to stand in front (empty on old maps).</summary>
        public static List<Spot> Sockets()
        {
            var list = new List<Spot>();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
            {
                if (t.name != SocketName || InSecurityZone(t.position) || !HideAndSeek.Map.MapInfo.InsideGarage(t.position, 0.3f)) continue;
                Vector3 axis = t.up, fwd = t.forward;
                fwd.y = 0f;
                if (fwd.sqrMagnitude < 1e-4f) continue;
                fwd.Normalize();
                Vector3 stand = t.position + fwd * RodPullTimeline.StandDistance;
                if (!Physics.Raycast(stand + Vector3.up * 0.8f, Vector3.down, out var fh, 1.6f, ~0, QueryTriggerInteraction.Ignore) || fh.normal.y < 0.9f) continue;
                stand = fh.point;
                if (!OnGarageFloor(stand)) continue;
                if (Physics.CheckCapsule(stand + Vector3.up * 0.5f, stand + Vector3.up * 1.6f, 0.28f, ~0, QueryTriggerInteraction.Ignore)) continue;
                Vector3 b = t.position - axis * RodPullTimeline.Buried;
                list.Add(new Spot { Base = b, Top = b + axis * RodModel.Length, Near = t.parent != null ? t.parent.name : t.name, Embedded = true, Stand = stand });
            }
            return list;
        }

        /// <summary>Up to <paramref name="count"/> spots far apart (prefers different levels), random each call.</summary>
        public static List<Spot> Pick(int count, float minSpacing = 18f)
        {
            // Rubble sockets first (shuffled), the old rack / fence spots only when there are too few of them.
            var all = Shuffled(Sockets());
            if (all.Count < count) all.AddRange(Shuffled(Candidates()));
            var picked = new List<Spot>();
            // First pass: different levels; second: anything far enough; third: half the spacing.
            for (int pass = 0; pass < 3 && picked.Count < count; pass++)
                foreach (var s in all)
                {
                    if (picked.Count >= count) break;
                    bool ok = true;
                    foreach (var p in picked)
                    {
                        if (Vector3.Distance(p.Base, s.Base) < (pass == 2 ? minSpacing * 0.5f : minSpacing)) ok = false;
                        if (pass == 0 && Mathf.Abs(p.Base.y - s.Base.y) < 1.5f) ok = false;
                    }
                    if (ok) picked.Add(s);
                }
            return picked;
        }

        private static List<Spot> Shuffled(List<Spot> all)
        {
            for (int i = all.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (all[i], all[j]) = (all[j], all[i]);
            }
            return all;
        }

        /// <summary>Every valid spot next to every rack / shelf / fence (one per object).</summary>
        public static List<Spot> Candidates()
        {
            var list = new List<Spot>();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
            {
                bool match = false;
                foreach (var pre in Prefixes) if (t.name.StartsWith(pre)) { match = true; break; }
                if (!match || InSecurityZone(t.position) || !HideAndSeek.Map.MapInfo.InsideGarage(t.position, 0.3f)) continue;
                if (TrySpot(t, out var s)) list.Add(s);
            }
            return list;
        }

        private static bool OnGarageFloor(Vector3 p)
        {
            if (!HideAndSeek.Map.MapInfo.HasGarage) return true;
            if (!HideAndSeek.Map.MapInfo.InsideGarage(p, 0.45f) || HideAndSeek.Map.MapInfo.InSlabOpening(p, 0.4f)) return false;
            return Mathf.Abs(p.y - HideAndSeek.Map.MapInfo.FloorUnder(p.y)) <= 0.25f;
        }

        /// <summary>The security rooms (north-east) clear props away when they are built.</summary>
        private static bool InSecurityZone(Vector3 p) => p.x > 63.5f && p.x < 80.5f && p.z > 47.5f && p.z < 61f;

        private static bool TrySpot(Transform obj, out Spot spot)
        {
            spot = default;
            var cols = obj.GetComponentsInChildren<Collider>();
            if (cols.Length == 0) return false;
            Bounds b = cols[0].bounds;
            foreach (var c in cols) b.Encapsulate(c.bounds);
            float floorY = b.min.y;
            if (b.size.y < 1.0f) return false;
            // Try the four faces in a random order (object axes).
            Vector3 f = obj.forward, r = obj.right;
            Vector3[] dirs = { f, -f, r, -r };
            int start = Random.Range(0, 4);
            for (int k = 0; k < 4; k++)
            {
                Vector3 n = dirs[(start + k) % 4];
                n.y = 0f;
                n.Normalize();
                // Find the face with a ray from outside towards the centre at lean height.
                float reach = Vector3.Dot(b.extents, new Vector3(Mathf.Abs(n.x), 0f, Mathf.Abs(n.z))) + 1.2f;
                Vector3 c = new Vector3(b.center.x, floorY + 0.85f, b.center.z);
                Vector3 from = c + n * reach;
                if (!Physics.Raycast(from, -n, out var hit, reach + 0.5f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (!hit.collider.transform.IsChildOf(obj)) continue;
                Vector3 face = hit.point;
                Vector3 basePt = new Vector3(face.x, floorY, face.z) + n * 0.26f;
                // Floor under the foot, level with the object's base.
                if (!Physics.Raycast(basePt + Vector3.up * 0.6f, Vector3.down, out var fh, 1.2f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (fh.normal.y < 0.9f || Mathf.Abs(fh.point.y - floorY) > 0.25f) continue;
                basePt = fh.point;
                // The garage floor of that level, inside the walls, not at a ramp / stairwell (a fence on the edge of
                // the map: its outer face is outside).
                if (!OnGarageFloor(basePt)) continue;
                // Free space where the rod and a player stand.
                Vector3 boxC = basePt + n * 0.35f + Vector3.up * 1.0f;
                int cnt = Physics.OverlapBoxNonAlloc(boxC, new Vector3(0.3f, 0.85f, 0.3f), Overlap, Quaternion.LookRotation(n), ~0, QueryTriggerInteraction.Ignore);
                if (cnt > 0) continue;
                Vector3 top = new Vector3(face.x, basePt.y, face.z) + n * 0.03f;
                float h = Mathf.Sqrt(Mathf.Max(0.1f, RodModel.Length * RodModel.Length - 0.23f * 0.23f));
                top.y = basePt.y + h;
                spot = new Spot { Base = basePt, Top = top, Near = obj.name };
                return true;
            }
            return false;
        }
    }
}
