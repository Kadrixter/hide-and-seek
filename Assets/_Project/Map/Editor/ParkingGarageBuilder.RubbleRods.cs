using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// Rubble for the metal rods (the peg-leg item): a few small heaps of broken concrete in empty bays on levels 0
    /// and -1 (a piece of the ceiling came down there; level -2 has the construction rubble already), and on every
    /// heap one or two "RodSocket" markers: where a rod can stick out of it (position = where it enters the rubble,
    /// up = the rod leaning out, forward = the free floor where a player can stand and pull). The game picks the
    /// sockets each round (Game/Net/RodSpots).
    /// </summary>
    internal sealed partial class ParkingGarageBuilder
    {
        public const string RodSocketName = "RodSocket";

        private PropMesh RubbleSmall(int variant) => _props.Get("RubbleSmall" + variant, (mb, pm) =>
        {
            var rng = new System.Random(3200 + variant);
            int n = 9 + variant * 2;
            for (int i = 0; i < n; i++)
            {
                float r = (float)rng.NextDouble();
                var s = new Vector3(0.16f + r * 0.42f, 0.1f + (float)rng.NextDouble() * 0.24f, 0.16f + (float)rng.NextDouble() * 0.42f);
                float rad = 0.62f * (1f - r * 0.45f);
                var p = new Vector3(((float)rng.NextDouble() - 0.5f) * rad * 2f, s.y * 0.35f + (1f - r) * 0.16f, ((float)rng.NextDouble() - 0.5f) * rad * 1.6f);
                mb.Box(p, s, M("Rubble"), Quaternion.Euler((float)rng.NextDouble() * 40f - 20f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 40f - 20f));
            }
            // gravel and a bent piece of rebar
            for (int i = 0; i < 14; i++)
            {
                var p = new Vector3(((float)rng.NextDouble() - 0.5f) * 1.6f, 0.025f, ((float)rng.NextDouble() - 0.5f) * 1.3f);
                mb.Box(p, Vector3.one * (0.04f + 0.05f * (float)rng.NextDouble()), M("Rubble"), Quaternion.Euler(0f, (float)rng.NextDouble() * 90f, 20f));
            }
            var a = new Vector3(-0.3f, 0.12f, 0.1f);
            mb.Cylinder(a, a + new Vector3(0.5f, 0.25f, -0.2f), 0.01f, 4, M("Rust"));
            pm.Colliders.Add(new Bounds(new Vector3(0, 0.22f, 0), new Vector3(1.4f, 0.44f, 1.1f)));
        });

        private void BuildRubbleRods()
        {
            int added = 0, blocked = 0, skipSpawn = 0;
            for (int lv = 0; lv < 2; lv++)
            {
                var bays = _bays.Where(b => b.Level == lv && !b.NoCars && b.Label != "WASH").ToList();
                var rnd = new System.Random(5150 + lv);
                for (int i = bays.Count - 1; i > 0; i--)
                {
                    int j = rnd.Next(i + 1);
                    (bays[i], bays[j]) = (bays[j], bays[i]);
                }
                var placed = new List<Vector3>();
                foreach (var b in bays)
                {
                    if (placed.Count >= 4) break;
                    Vector3 c = b.Center + b.Inward * (b.Depth * 0.08f);
                    c.y = FloorY(lv);
                    if (placed.Any(q => Vector3.Distance(q, c) < 28f) || NearSpawn(c, 3.5f)) { skipSpawn++; continue; }
                    Physics.SyncTransforms();
                    var hits = Physics.OverlapBox(c + Vector3.up * 0.6f, new Vector3(0.85f, 0.5f, 0.85f), Quaternion.LookRotation(b.Inward), ~0, QueryTriggerInteraction.Ignore);
                    if (hits.Length > 0) { if (blocked++ < 6) Debug.Log($"[ParkingGarage] fallen heap at {c} blocked by {hits[0].name}"); continue; }
                    float yaw = Quaternion.LookRotation(b.Inward).eulerAngles.y + (float)(rnd.NextDouble() * 50.0 - 25.0);
                    PlaceProp(RubbleSmall(rnd.Next(3)), $"Rubble_{Tag(lv)}_{++_propIndex:000}_Fallen", _tPropsLv[lv], c, yaw);
                    placed.Add(c);
                    added++;
                }
            }
            Physics.SyncTransforms();
            int heaps = 0, sockets = 0;
            foreach (var t in _tProps.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("Rubble_")).ToList())
            {
                heaps++;
                sockets += AddRodSockets(t);
            }
            Debug.Log($"[ParkingGarage] Rubble for rods: {added} fallen heaps added on levels 0/-1 ({blocked} bays blocked, {skipSpawn} too close), {heaps} heaps, {sockets} rod sockets");
        }

        /// <summary>Up to two sockets on a heap, on sides with free floor in front of them.</summary>
        private int AddRodSockets(Transform heap)
        {
            var mesh = heap.GetComponent<MeshFilter>().sharedMesh;
            var box = heap.GetComponent<BoxCollider>();
            if (mesh == null || box == null) return 0;
            var probe = Probe(mesh);
            var rnd = new System.Random(StableHash(heap.name));
            int made = 0, start = rnd.Next(8);
            var used = new List<Vector3>();
            for (int k = 0; k < 8 && made < 2; k++)
            {
                float ang = (start + k) * 45f * Mathf.Deg2Rad;
                var dl = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));           // heap space
                // distance from the middle to the side of the collider box along dl
                float ex = Mathf.Abs(dl.x) > 1e-3f ? box.size.x * 0.5f / Mathf.Abs(dl.x) : 99f;
                float ez = Mathf.Abs(dl.z) > 1e-3f ? box.size.z * 0.5f / Mathf.Abs(dl.z) : 99f;
                float edge = Mathf.Min(ex, ez);
                // entry point on the rubble, a bit inside the edge
                Vector3 inLocal = dl * Mathf.Max(0.2f, edge - 0.38f);
                if (!probe.Raycast(new Ray(TarpDrape.ProbeOffset + inLocal + Vector3.up * 2f, Vector3.down), out var hit, 3f)) continue;
                float surf = hit.point.y - TarpDrape.ProbeOffset.y;
                if (surf < 0.14f) continue;
                Vector3 dw = heap.TransformDirection(dl);
                Vector3 entry = heap.TransformPoint(new Vector3(inLocal.x, surf - 0.02f, inLocal.z));
                if (used.Any(u => Vector3.Distance(u, entry) < 0.9f)) continue;
                // the player stands on free floor in front of it
                Vector3 stand = heap.position + dw * (edge + 0.5f);
                stand.y = heap.position.y;
                if (NearSpawn(stand, 0.6f)) continue;
                if (Physics.CheckCapsule(stand + Vector3.up * 0.45f, stand + Vector3.up * 1.6f, 0.32f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (!Physics.Raycast(stand + Vector3.up * 0.5f, Vector3.down, out var floor, 1f, ~0, QueryTriggerInteraction.Ignore) || Mathf.Abs(floor.point.y - stand.y) > 0.08f) continue;
                // lean out towards the stand point, a little sideways
                float lean = 22f + (float)rnd.NextDouble() * 14f, side = (float)rnd.NextDouble() * 24f - 12f;
                Vector3 axis = Quaternion.AngleAxis(side, Vector3.up) * (Quaternion.AngleAxis(lean, Vector3.Cross(Vector3.up, dw)) * Vector3.up);
                var s = new GameObject(RodSocketName).transform;
                s.SetParent(heap, false);
                s.SetPositionAndRotation(entry, Quaternion.LookRotation(dw, axis));
                // LookRotation keeps forward exact; re-orthogonalise so up = rod axis exactly
                s.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(dw, axis).normalized, axis);
                GameObjectUtility.SetStaticEditorFlags(s.gameObject, 0);
                used.Add(entry);
                made++;
            }
            return made;
        }
    }
}
