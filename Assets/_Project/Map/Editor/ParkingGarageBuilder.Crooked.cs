using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// Sloppy parking: about a third of the cars are skewed, parked on the line, with a wheel over the line or
    /// sticking out towards the aisle. Deterministic (own seed, same draws per car). Every move is checked with
    /// physics against pillars, walls, other cars and props, kept out of spawn points and limited to 0.6 m into
    /// the aisle; if it does not fit it is tried smaller, otherwise the car stays straight.
    /// </summary>
    internal sealed partial class ParkingGarageBuilder
    {
        private struct Parked
        {
            public GameObject Go;
            public Vector3 BayCenter, Inward;
            public float Depth;
            public int Level;
            public string Label;
            public bool Hero, Fixed;
            public float FixLat, FixRot;
        }

        private readonly List<Parked> _parked = new List<Parked>();
        private const float MaxAisleIntrusion = 0.6f;

        private void ParkSomeCarsCrooked()
        {
            var rng = new Random(424242);
            int crooked = 0, shrunk = 0, straight = 0;
            Physics.SyncTransforms();
            foreach (var p in _parked)
            {
                // same number of draws for every car, so one car's outcome never reshuffles the others
                double roll = rng.NextDouble();
                int style = rng.Next(4);
                float a = (float)rng.NextDouble(), b = (float)rng.NextDouble();
                float sgn = rng.NextDouble() < 0.5 ? -1f : 1f;
                bool eligible = p.Label != "WASH" && !p.Label.StartsWith("J") && !p.Label.StartsWith("K");
                if (p.Hero && !Fits(p, true)) Debug.LogWarning($"[ParkingGarage] Hero car {p.Go.name} overlaps something even when straight");
                if (!p.Fixed && (!eligible || roll > 0.34)) continue;

                float lat, back = 0f, rot;
                if (p.Fixed) { lat = p.FixLat; rot = p.FixRot; }
                else switch (style)
                {
                    case 0: rot = sgn * Mathf.Lerp(6f, 14f, a); lat = sgn * Mathf.Lerp(0.1f, 0.35f, b); break;     // skewed in the bay
                    case 1: lat = sgn * Mathf.Lerp(0.45f, 0.8f, a); rot = sgn * Mathf.Lerp(1f, 4f, b); break;      // on the line
                    case 2: lat = sgn * Mathf.Lerp(0.3f, 0.45f, a); rot = -sgn * Mathf.Lerp(3f, 7f, b); break;    // one wheel over the line
                    default: back = Mathf.Lerp(0.35f, 0.65f, a); rot = sgn * Mathf.Lerp(3f, 9f, b); lat = 0f; break; // sticking out
                }

                var t = p.Go.transform;
                Vector3 pos0 = t.position;
                Quaternion rot0 = t.rotation;
                Vector3 side = Vector3.Cross(Vector3.up, p.Inward);
                bool ok = false;
                foreach (float k in new[] { 1f, 0.65f, 0.35f })
                {
                    t.SetPositionAndRotation(pos0 + side * (lat * k) - p.Inward * (back * k), Quaternion.Euler(0f, rot * k, 0f) * rot0);
                    if (Fits(p, p.Fixed)) { ok = true; if (k < 1f) shrunk++; break; }
                }
                if (ok) crooked++;
                else
                {
                    t.SetPositionAndRotation(pos0, rot0);
                    straight++;
                    if (p.Fixed) Debug.LogWarning($"[ParkingGarage] {p.Go.name}: fixed sloppy pose does not fit, parked straight");
                }
                Physics.SyncTransforms();
            }
            Debug.Log($"[ParkingGarage] Crooked parking: {crooked} cars crooked ({shrunk} toned down), {straight} kept straight (no room), {_parked.Count} parked cars");
        }

        /// <summary>Car pose check: inside the bay strip (aisle intrusion limited), clear of spawns and of every collider.</summary>
        private bool Fits(Parked p, bool log = false)
        {
            var t = p.Go.transform;
            Physics.SyncTransforms();
            var boxes = p.Go.GetComponents<BoxCollider>();
            if (boxes.Length == 0) return true;
            var lb = new Bounds(boxes[0].center, boxes[0].size);
            foreach (var bc in boxes) lb.Encapsulate(new Bounds(bc.center, bc.size));
            foreach (float x in new[] { lb.min.x, lb.max.x })
            foreach (float z in new[] { lb.min.z, lb.max.z })
            {
                Vector3 w = t.TransformPoint(new Vector3(x, 0f, z));
                if (Vector3.Dot(w - p.BayCenter, -p.Inward) > p.Depth * 0.5f + MaxAisleIntrusion) return Why(log, p, "aisle");
                if (Mathf.Abs(p.Inward.z) > 0.5f && Mathf.Abs(w.x) > ParkX + 0.4f) return Why(log, p, "cross aisle"); // cross aisles stay clear
            }
            if (NearSpawn(t.position, 3.2f)) return Why(log, p, "spawn point");
            foreach (var bc in boxes)
            {
                Vector3 c = t.TransformPoint(bc.center);
                Vector3 he = bc.size * 0.5f + new Vector3(0.1f, -0.04f, 0.1f);
                foreach (var hit in Physics.OverlapBox(c, he, t.rotation, ~0, QueryTriggerInteraction.Ignore))
                    if (hit.transform != t && !hit.transform.IsChildOf(t)) return Why(log, p, "collider " + hit.name);
            }
            return true;
        }

        private static bool Why(bool log, Parked p, string reason)
        {
            if (log) Debug.Log($"[ParkingGarage] {p.Go.name} at {p.Go.transform.position}: blocked by {reason}");
            return false;
        }
    }
}
