using System.Collections.Generic;
using HideAndSeek.Characters;
using HideAndSeek.Map;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Where a radio can be found on the parking: on top of a pallet pile or a wooden crate (the flat tops the map
    /// generator placed: "PalletPile_", "Crate_"), or on the floor right next to pallets / cardboard boxes / sacks
    /// ("PalletStack_", "BaledCardboard_", "Boxes_", "Sacks_"), its back to them. The map is not changed. Every spot is
    /// checked: flat support under all four feet at the same height, nothing in the radio's box or right above it,
    /// inside the garage on the level's floor, and a free place for a player to stand next to it (on the side it faces).
    /// </summary>
    public static class MapRadioSpots
    {
        private static readonly string[] TopHosts = { "PalletPile_", "Crate_" };
        private static readonly string[] SideHosts = { "PalletStack_", "BaledCardboard_", "Boxes_", "Sacks_", "PalletPile_" };
        private static readonly Collider[] Overlap = new Collider[16];
        private static readonly RaycastHit[] Hits = new RaycastHit[16];

        public struct Spot
        {
            public Vector3 Pos;
            public float Yaw;
            public string Near;
            public bool OnTop;
            public int Candidates;
        }

        /// <summary>Up to <paramref name="count"/> spots far apart (other levels first), random each call; tops preferred.</summary>
        public static List<Spot> Pick(int count, float minSpacing = 25f)
        {
            var all = Candidates();
            for (int i = all.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (all[i], all[j]) = (all[j], all[i]);
            }
            // Tops first (2 of 3 times), so the radio mostly lies on a pallet / crate.
            if (Random.value < 0.67f) all.Sort((a, b) => b.OnTop.CompareTo(a.OnTop));
            var picked = new List<Spot>();
            for (int pass = 0; pass < 2 && picked.Count < count; pass++)
                foreach (var s in all)
                {
                    if (picked.Count >= count) break;
                    bool ok = true;
                    foreach (var p in picked)
                    {
                        if (Vector3.Distance(p.Pos, s.Pos) < minSpacing) ok = false;
                        if (pass == 0 && Mathf.Abs(p.Pos.y - s.Pos.y) < 1.5f) ok = false;
                    }
                    if (!ok) continue;
                    var c = s;
                    c.Candidates = all.Count;
                    picked.Add(c);
                }
            return picked;
        }

        public static List<Spot> Candidates()
        {
            var list = new List<Spot>();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
            {
                string n = t.name;
                bool top = Starts(n, TopHosts), side = Starts(n, SideHosts);
                if (!top && !side) continue;
                if (InSecurityZone(t.position) || (MapInfo.HasGarage && !MapInfo.InsideGarage(t.position, 0.5f))) continue;
                var cols = t.GetComponents<Collider>();
                if (cols.Length == 0) continue;
                Bounds b = cols[0].bounds;
                foreach (var c in cols) b.Encapsulate(c.bounds);
                if (top && TryTop(t, b, out var s1)) list.Add(s1);
                else if (side && TrySide(t, b, out var s2)) list.Add(s2);
            }
            return list;
        }

        private static bool Starts(string n, string[] pre)
        {
            foreach (var p in pre) if (n.StartsWith(p)) return true;
            return false;
        }

        /// <summary>The security rooms (north-east) clear props away when they are built.</summary>
        private static bool InSecurityZone(Vector3 p) => p.x > 63.5f && p.x < 80.5f && p.z > 47.5f && p.z < 61f;

        private static float LevelFloor(Vector3 p) => MapInfo.HasGarage ? MapInfo.FloorUnder(p.y + 0.3f) : p.y;

        // ================================================================ on top

        private static bool TryTop(Transform host, Bounds b, out Spot spot)
        {
            spot = default;
            float floor = LevelFloor(b.min);
            float topY = b.max.y;
            if (topY - floor < 0.45f || topY - floor > 1.3f) return false;
            // Long side of the radio along the longer side of the top (host axes).
            Vector3 hx = host.right, hz = host.forward;
            hx.y = hz.y = 0f;
            Vector3 local = host.InverseTransformVector(b.size);
            bool alongX = Mathf.Abs(local.x) >= Mathf.Abs(local.z);
            Vector3 along = (alongX ? hx : hz).normalized, across = (alongX ? hz : hx).normalized;
            float halfAlong = (alongX ? Mathf.Abs(local.x) : Mathf.Abs(local.z)) * 0.5f;
            float halfAcross = (alongX ? Mathf.Abs(local.z) : Mathf.Abs(local.x)) * 0.5f;
            if (halfAlong < ShoulderRadioRig.Width * 0.5f + 0.04f || halfAcross < ShoulderRadioRig.Depth * 0.5f + 0.04f) return false;

            // Face the more open side; sit a little towards that edge, a bit off centre along it.
            float best = -1f;
            Vector3 face = across;
            foreach (float sgn in new[] { 1f, -1f })
            {
                float free = FreeRun(new Vector3(b.center.x, floor + 0.9f, b.center.z) + across * sgn * (halfAcross + 0.05f), across * sgn);
                free += Random.Range(0f, 0.4f);
                if (free > best) { best = free; face = across * sgn; }
            }
            if (best < 0.9f) return false;
            float slack = halfAlong - ShoulderRadioRig.Width * 0.5f - 0.05f;
            float edge = Mathf.Max(0f, halfAcross - ShoulderRadioRig.Depth * 0.5f - 0.07f);
            Vector3 c = new Vector3(b.center.x, topY, b.center.z) + along * Random.Range(-slack, slack) * 0.7f + face * edge * Random.Range(0.4f, 0.9f);
            float yaw = Quaternion.LookRotation(face).eulerAngles.y + Random.Range(-8f, 8f);
            return Finish(host, c, yaw, floor, true, out spot);
        }

        // ================================================================ on the floor next to it

        private static bool TrySide(Transform host, Bounds b, out Spot spot)
        {
            spot = default;
            float floor = LevelFloor(b.min);
            Vector3 f = host.forward, r = host.right;
            Vector3[] dirs = { f, -f, r, -r };
            int start = Random.Range(0, 4);
            for (int k = 0; k < 4; k++)
            {
                Vector3 n = dirs[(start + k) % 4];
                n.y = 0f;
                n.Normalize();
                float reach = Vector3.Dot(b.extents, new Vector3(Mathf.Abs(n.x), 0f, Mathf.Abs(n.z))) + 1.2f;
                Vector3 from = new Vector3(b.center.x, floor + 0.2f, b.center.z) + n * reach;
                if (!Physics.Raycast(from, -n, out var hit, reach + 0.3f, ItemPlacement.Mask, QueryTriggerInteraction.Ignore)) continue;
                if (!hit.collider.transform.IsChildOf(host)) continue;
                Vector3 c = new Vector3(hit.point.x, floor, hit.point.z) + n * (ShoulderRadioRig.Depth * 0.5f + 0.06f);
                c += Vector3.Cross(Vector3.up, n) * Random.Range(-0.3f, 0.3f);
                float yaw = Quaternion.LookRotation(n).eulerAngles.y + Random.Range(-12f, 12f);
                if (Finish(host, c, yaw, floor, false, out spot)) return true;
            }
            return false;
        }

        // ================================================================ checks

        /// <summary>Free distance along <paramref name="dir"/> from <paramref name="from"/> (max 3 m).</summary>
        private static float FreeRun(Vector3 from, Vector3 dir) =>
            Physics.SphereCast(from, 0.25f, dir, out var h, 3f, ItemPlacement.Mask, QueryTriggerInteraction.Ignore) ? h.distance : 3f;

        private static bool Finish(Transform host, Vector3 c, float yaw, float floor, bool onTop, out Spot spot)
        {
            spot = default;
            var rot = Quaternion.Euler(0f, yaw, 0f);
            // Support: the four feet (and the middle) on a flat surface at the same height.
            float y = float.NaN;
            foreach (var o in new[] { new Vector3(-0.23f, 0f, -0.045f), new Vector3(0.23f, 0f, -0.045f), new Vector3(-0.23f, 0f, 0.045f), new Vector3(0.23f, 0f, 0.045f), Vector3.zero })
            {
                Vector3 p = c + rot * o;
                if (!Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, out var hit, 0.8f, ItemPlacement.Mask, QueryTriggerInteraction.Ignore)) return false;
                if (hit.normal.y < 0.97f) return false;
                if (float.IsNaN(y)) y = hit.point.y;
                else if (Mathf.Abs(hit.point.y - y) > 0.015f) return false;
            }
            c.y = y;
            if (!onTop && Mathf.Abs(y - floor) > 0.1f) return false;
            if (MapInfo.HasGarage && (!MapInfo.InsideGarage(c, 0.5f) || MapInfo.InSlabOpening(c, 0.6f))) return false;
            // The radio's box (and a hand's room above it) is free.
            Vector3 half = new Vector3(ShoulderRadioRig.Width * 0.5f + 0.01f, 0.24f, ShoulderRadioRig.Depth * 0.5f + 0.01f);
            int cnt = Physics.OverlapBoxNonAlloc(c + Vector3.up * (half.y + 0.02f), half, Overlap, rot, ItemPlacement.Mask, QueryTriggerInteraction.Ignore);
            if (cnt > 0) return false;
            // A player can stand in front of it (the side it faces) and see it from there.
            Vector3 fwd = rot * Vector3.forward;
            Vector3 stand = new Vector3(c.x, floor, c.z) + fwd * 0.85f;
            if (!Physics.Raycast(stand + Vector3.up * 0.5f, Vector3.down, out var fh, 1f, ItemPlacement.Mask, QueryTriggerInteraction.Ignore) ||
                fh.normal.y < 0.9f || Mathf.Abs(fh.point.y - floor) > 0.2f) return false;
            if (!ItemPlacement.CapsuleFree(fh.point + Vector3.up * 0.4f, fh.point + Vector3.up * 1.5f, 0.3f)) return false;
            int n = Physics.RaycastNonAlloc(fh.point + Vector3.up * 1.4f, (c + Vector3.up * 0.15f - (fh.point + Vector3.up * 1.4f)).normalized, Hits,
                Vector3.Distance(c + Vector3.up * 0.15f, fh.point + Vector3.up * 1.4f) - 0.12f, ItemPlacement.Mask, QueryTriggerInteraction.Ignore);
            if (n > 0) return false;
            spot = new Spot { Pos = c, Yaw = yaw, Near = host.name, OnTop = onTop };
            return true;
        }
    }
}
