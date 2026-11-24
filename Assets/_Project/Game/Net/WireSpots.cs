using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Wire-mesh panels on the game map a piece of wire can be torn off (RC antenna upgrade): the temporary
    /// construction fences ("TempFence_" props, 3.4 m of mesh, level -3) and the storage cages (wire-mesh fronts,
    /// back walls and end walls, merged into "Cages_" chunks - their layout is copied from
    /// <c>ParkingGarageBuilder.Cages</c>, keep it in step). Every peer builds the same list from the scene (sorted names,
    /// fixed layout, static geometry only), so a spot index means the same panel everywhere.
    /// </summary>
    public static class WireSpots
    {
        public struct Spot
        {
            /// <summary>Panel centre at grip height (on the mesh plane).</summary>
            public Vector3 Center;
            /// <summary>Horizontal unit vector along the panel / its normal.</summary>
            public Vector3 Along, Normal;
            /// <summary>Usable half length along the panel (m).</summary>
            public float Half;
            /// <summary>No grip within this distance of the centre (the DANGER sign on the fences).</summary>
            public float Gap;
            public float Floor;
            public string Name;

            /// <summary>Point on the mesh at <paramref name="along"/> m from the centre, on <paramref name="side"/>'s face.</summary>
            public Vector3 Grip(float along, int side) => Center + Along * along + Normal * (side * 0.025f);

            /// <summary>Where the player stands to tear the wire there.</summary>
            public Vector3 Stand(float along, int side)
            {
                Vector3 p = Center + Along * along + Normal * (side * HideAndSeek.Characters.WirePullTimeline.StandOff);
                p.y = Floor;
                return p;
            }

            /// <summary>Clamps a wanted position along the panel to the usable part (outside the sign gap).</summary>
            public float ClampAlong(float a)
            {
                a = Mathf.Clamp(a, -Half, Half);
                if (Gap > 0f && Mathf.Abs(a) < Gap) a = a < 0f ? -Gap : Gap;
                return a;
            }
        }

        public const int MaxSpots = 256;
        private const float GripAbove = HideAndSeek.Characters.WirePullTimeline.GripHeight;
        private static List<Spot> _spots;
        private static bool _hooked;

        public static IReadOnlyList<Spot> All
        {
            get
            {
                if (!_hooked)
                {
                    _hooked = true;
                    SceneManager.activeSceneChanged += (_, _) => _spots = null;
                    SceneManager.sceneLoaded += (_, _) => _spots = null;
                }
                if (_spots == null) _spots = Build(SceneManager.GetActiveScene().name == NetSession.GameScene);
                return _spots;
            }
        }

        public static int Count => All.Count;

        private static List<Spot> Build(bool gameMap)
        {
            var list = new List<Spot>();
            if (!gameMap) return list;
            int fences = 0, cages = 0;

            // 1. Temporary construction fences: mesh 3.4 m along the prop's X, both faces, sign in the middle.
            var fenceTs = new List<Transform>();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
                if (t.name.StartsWith("TempFence_")) fenceTs.Add(t);
            fenceTs.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            foreach (var f in fenceTs)
            {
                Vector3 along = Flat(f.right), n = Flat(f.forward);
                if (along == Vector3.zero || n == Vector3.zero) continue;
                Add(list, new Spot { Center = f.position + Vector3.up * GripAbove, Along = along, Normal = n, Half = 1.45f, Gap = 0.55f, Floor = f.position.y, Name = f.name }, ref fences);
            }

            // 2. Storage cages (ParkingGarageBuilder.Cages): fronts per bay (left part a..doorA unless torn away, else
            //    the right part), back walls per bay where there is one, the two end walls.
            const float lv0 = 0f, lvStep = -3.4f;
            float[] north = { -56f, -51.6f, -47.2f, -42.8f, -38.4f, -34f };
            float[] mid = { -29f, -24.75f, -20.5f, -16.25f, -12f };
            CageBlock(list, ref cages, lv0, north, 55.5f, 60f, false, "CageN_L0");
            CageBlock(list, ref cages, lv0 + 2f * lvStep, north, 55.5f, 60f, true, "CageN_L2");
            CageBlock(list, ref cages, lv0 + lvStep, mid, -4.2f, 4.2f, false, "CageM_L1");
            if (list.Count > MaxSpots) list.RemoveRange(MaxSpots, list.Count - MaxSpots);
            Debug.Log($"[RcWire] wire-mesh spots: {list.Count} ({fences} fence panels, {cages} cage panels)");
            return list;
        }

        private static void CageBlock(List<Spot> list, ref int n, float y0, float[] xs, float zFront, float zBack, bool damaged, string name)
        {
            for (int i = 0; i < xs.Length - 1; i++)
            {
                float a = xs[i], c = xs[i + 1];
                float doorA = a + (c - a) * 0.6f, doorB = doorA + 1f;
                bool missing = damaged && i % 2 == 1;
                float p0 = missing ? doorB : a, p1 = missing ? c : doorA;
                Add(list, Panel(new Vector3((p0 + p1) * 0.5f, y0, zFront), Vector3.right, (p1 - p0) * 0.5f - 0.2f, y0, $"{name}_F{i}"), ref n);
                if (zBack < 59.9f)
                    Add(list, Panel(new Vector3((a + c) * 0.5f, y0, zBack), Vector3.right, (c - a) * 0.5f - 0.2f, y0, $"{name}_B{i}"), ref n);
            }
            foreach (int k in new[] { 0, xs.Length - 1 })
                Add(list, Panel(new Vector3(xs[k], y0, (zFront + zBack) * 0.5f), Vector3.forward, (zBack - zFront) * 0.5f - 0.3f, y0, $"{name}_E{k}"), ref n);
        }

        private static Spot Panel(Vector3 floorCentre, Vector3 along, float half, float floorY, string name) => new Spot
        {
            Center = floorCentre + Vector3.up * GripAbove, Along = along, Normal = Vector3.Cross(along, Vector3.up).normalized,
            Half = Mathf.Max(0.2f, half), Floor = floorY, Name = name,
        };

        /// <summary>Keeps a spot only when the mesh is really there (a static collider right on the panel plane).</summary>
        private static void Add(List<Spot> list, Spot s, ref int n)
        {
            if (!MeshThere(s, 1) && !MeshThere(s, -1)) return;
            list.Add(s);
            n++;
        }

        private static readonly RaycastHit[] Hits = new RaycastHit[12];

        private static bool MeshThere(Spot s, int side)
        {
            Vector3 from = s.Center + s.Normal * (side * 0.6f);
            int cnt = Physics.RaycastNonAlloc(from, -s.Normal * side, Hits, 0.75f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < cnt; i++)
            {
                var h = Hits[i];
                if (h.collider.attachedRigidbody != null || h.collider.GetComponentInParent<NetworkObject>() != null) continue;
                if (h.distance > 0.45f && h.distance < 0.72f) return true;
            }
            return false;
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-4f ? v.normalized : Vector3.zero;
        }
    }
}
