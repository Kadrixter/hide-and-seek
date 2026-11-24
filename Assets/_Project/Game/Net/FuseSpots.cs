using System.Collections.Generic;
using HideAndSeek.Map;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Server: where the fuse boxes hang and where the fuses lie (escape objective). Runs on the loaded garage map
    /// (physics only, no map data needed):
    /// - a box goes on a flat, free part of an outer wall or the front wall of the north rooms: the wall is probed with
    ///   rays (all hits at one depth, facing out: no pilaster, door, pipe or opening), the room in front of it is empty
    ///   (no car, rack or pallet within ~1 m, floor under the stand point), not in a ramp / stair opening, not at the
    ///   exit gate; one random good spot per level, the three far apart;
    /// - a fuse lies on the floor 5-13 m from a random hider spawn of its level (<see cref="ItemPlacement"/>: inside the
    ///   walls, on the level's floor, free, a player can stand there), preferably next to something (a car, a wall, a
    ///   column - "hidden but findable"), far from the other fuses and the boxes.
    /// </summary>
    public static class FuseSpots
    {
        public struct Wall
        {
            public Vector3 Pos; // wall surface at floor height
            public float Yaw;   // box +Z = out of the wall
            public string Name;
        }

        private static readonly RaycastHit[] Hits = new RaycastHit[16];
        private static readonly Collider[] Overlap = new Collider[8];

        /// <summary>All usable wall spots of <paramref name="level"/> (2.5 m apart).</summary>
        public static List<Wall> BoxCandidates(int level)
        {
            var list = new List<Wall>();
            if (!MapInfo.HasGarage) return list;
            float y = MapInfo.FloorY(level);
            Vector2 h = MapInfo.InnerHalfSize;
            for (float z = -h.y + 2f; z <= h.y - 2f; z += 2.5f)
            {
                Probe(list, new Vector3(-h.x, y, z), Vector3.right, level, "W");
                Probe(list, new Vector3(h.x, y, z), Vector3.left, level, "E");
            }
            for (float x = -h.x + 2f; x <= h.x - 2f; x += 2.5f)
            {
                Probe(list, new Vector3(x, y, h.y), Vector3.back, level, "N");
                Probe(list, new Vector3(x, y, -h.y), Vector3.forward, level, "S");
                Probe(list, new Vector3(x, y, 50.8f), Vector3.back, level, "NR"); // front of the north rooms
            }
            return list;
        }

        /// <summary>One random box spot per level, spread out (null entry = none found on that level).</summary>
        public static Wall?[] PickBoxes(System.Random rnd)
        {
            var res = new Wall?[MapInfo.Levels];
            var chosen = new List<Vector3>();
            for (int lv = 0; lv < MapInfo.Levels; lv++)
            {
                var c = BoxCandidates(lv);
                Debug.Log($"[Fuses] level {lv}: {c.Count} wall spots for the fuse box");
                if (c.Count == 0) continue;
                // Prefer spots far (flat) from the boxes already chosen; else any.
                var far = c.FindAll(w => chosen.TrueForAll(o => Flat(o - w.Pos).magnitude > 45f));
                var pool = far.Count > 0 ? far : c;
                var pick = pool[rnd.Next(pool.Count)];
                res[lv] = pick;
                chosen.Add(pick.Pos);
            }
            return res;
        }

        private static void Probe(List<Wall> list, Vector3 nominal, Vector3 n, int level, string side)
        {
            float floor = MapInfo.FloorY(level);
            Vector3 from = nominal + n * 1.4f + Vector3.up * 1.3f;
            if (!WallHit(from, -n, 2.5f, out var mid)) return;
            if (Vector3.Dot(mid.normal, n) < 0.97f) return;
            Vector3 wall = new Vector3(mid.point.x, floor, mid.point.z);
            float depth = Vector3.Dot(mid.point, n);
            Vector3 side3 = Vector3.Cross(Vector3.up, n);
            // A flat stretch of wall the size of the box (+ margin): the same depth everywhere.
            foreach (float dx in new[] { -0.55f, 0f, 0.55f })
            foreach (float dy in new[] { 0.75f, 1.3f, 1.85f })
            {
                Vector3 p = wall + side3 * dx + Vector3.up * dy + n * 1.4f;
                if (!WallHit(p, -n, 2.5f, out var hit) || Vector3.Dot(hit.normal, n) < 0.97f) return;
                if (Mathf.Abs(Vector3.Dot(hit.point, n) - depth) > 0.025f) return;
                string nm = hit.collider.name;
                if (nm.Contains("Door") || nm.Contains("Elev") || nm.Contains("Lift") || nm.Contains("Gate")) return;
            }
            // Room in front: nothing within ~1.3 m of the wall, 1.7 m wide (the open door swings out to one side), from
            // just above the floor to 2.2 m.
            Vector3 c = wall + n * 0.72f + Vector3.up * 1.2f;
            int k = Physics.OverlapBoxNonAlloc(c, new Vector3(0.85f, 0.98f, 0.62f), Overlap, Quaternion.LookRotation(n), ItemPlacement.Mask, QueryTriggerInteraction.Ignore);
            if (k > 0) return;
            // Floor under the stand point.
            Vector3 stand = wall + n * 0.8f;
            if (!Physics.Raycast(stand + Vector3.up * 1f, Vector3.down, out var fh, 1.6f, ItemPlacement.Mask, QueryTriggerInteraction.Ignore)) return;
            if (fh.normal.y < 0.95f || Mathf.Abs(fh.point.y - floor) > 0.12f) return;
            if (MapInfo.InSlabOpening(stand, 1.5f) || !MapInfo.InsideGarage(stand, 0.3f)) return;
            // Not at the street exit gate (escape objective's last part happens there).
            if (level == 0 && Flat(stand - HideAndSeek.Game.Cinematics.ExitGate.DefaultPos).magnitude < 12f) return;
            list.Add(new Wall { Pos = wall, Yaw = Quaternion.LookRotation(n).eulerAngles.y, Name = $"{side}{level}@{wall.x:0},{wall.z:0}" });
        }

        /// <summary>Nearest static hit (no players, items, rigidbodies, network objects).</summary>
        private static bool WallHit(Vector3 from, Vector3 dir, float dist, out RaycastHit best)
        {
            best = default;
            int cnt = Physics.RaycastNonAlloc(from, dir, Hits, dist, ItemPlacement.Mask, QueryTriggerInteraction.Ignore);
            float bd = float.MaxValue;
            for (int i = 0; i < cnt; i++)
            {
                var h = Hits[i];
                if (h.collider.attachedRigidbody != null || h.collider.GetComponentInParent<NetworkObject>() != null) continue;
                if (h.distance < bd) { bd = h.distance; best = h; }
            }
            return bd < float.MaxValue;
        }

        // ================================================================ fuses on the floor

        /// <summary>Floor spot of a fuse on <paramref name="level"/>, away from <paramref name="avoid"/> (other fuses) and the boxes.</summary>
        public static Vector3? FuseSpot(int level, System.Random rnd, List<Vector3> avoid, List<Vector3> boxes)
        {
            var spawns = SpawnPoints.Hider();
            var mine = spawns.FindAll(t => t != null && MapInfo.LevelOf(t.position.y) == level);
            if (mine.Count == 0) mine = spawns;
            if (mine.Count == 0) return null;
            var pf = ItemPlacement.Pickup;
            pf.MinRadius = FuseTuning.SpotMin;
            pf.MaxRadius = FuseTuning.SpotMax;
            pf.Attempts = 12;
            Vector3? best = null;
            float bestScore = float.MinValue;
            for (int k = 0; k < 10; k++)
            {
                var sp = mine[rnd.Next(mine.Count)].position;
                Vector3 p = ItemPlacement.Scatter(sp, pf, avoid, "fuse");
                if (Flat(p - sp).magnitude < 1f) continue; // fell back on the spawn point itself
                if (MapInfo.LevelOf(p.y) != level) continue;
                float score = Cover(p) + (float)rnd.NextDouble() * 1.5f;
                foreach (var a in avoid) if (Flat(a - p).magnitude < FuseTuning.FuseSpacing) score -= 6f;
                foreach (var b in boxes) if (Flat(b - p).magnitude < FuseTuning.BoxClearance && Mathf.Abs(b.y - p.y) < 1.5f) score -= 10f;
                if (score > bestScore) { bestScore = score; best = p; }
            }
            return best;
        }

        /// <summary>How tucked away a floor spot is: rays at ankle height that hit something within 1.6 m (0..4).</summary>
        public static float Cover(Vector3 p)
        {
            int hits = 0;
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                if (Physics.Raycast(p + Vector3.up * 0.25f, d, 1.6f, ItemPlacement.Mask, QueryTriggerInteraction.Ignore)) hits++;
            }
            return Mathf.Min(hits, 4);
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}
