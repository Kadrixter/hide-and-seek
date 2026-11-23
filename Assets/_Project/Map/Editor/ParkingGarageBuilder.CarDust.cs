using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using DustPreset = HideAndSeek.Map.Editor.MaterialLibrary.DustPreset;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// Map details of 23.11, run after the cars got their final (crooked) poses: dust on the cars of the lower
    /// levels, flat tyres on forgotten cars, fluid leaks, rubble with rods stuck in it.
    /// Level 0 stays clean. Level -1: about a third of the cars carry a light film of dust. Level -2: almost every car
    /// is forgotten - thick dust, milky windows, dirt on the sills, some with flat tyres. Deterministic per bay.
    /// </summary>
    internal sealed partial class ParkingGarageBuilder
    {
        private readonly Dictionary<Mesh, MeshCollider> _probes = new Dictionary<Mesh, MeshCollider>();

        private void BuildMapDetail()
        {
            ApplyCarDust();
            BuildLeakSpots();
            BuildRubbleRods();
            foreach (var c in _probes.Values) if (c != null) Object.DestroyImmediate(c.gameObject);
            _probes.Clear();
        }

        /// <summary>Stable hash of a string (FNV-1a), for per-car decisions that survive layout changes elsewhere.</summary>
        private static int StableHash(string s)
        {
            unchecked
            {
                uint h = 2166136261u;
                foreach (char c in s) { h ^= c; h *= 16777619u; }
                return (int)(h & 0x7fffffff);
            }
        }

        /// <summary>Dust preset of a car on level <paramref name="lv"/> (null = clean).</summary>
        private static DustPreset? DustFor(int lv, string label, bool hero)
        {
            if (lv == 0) return null;
            var r = new System.Random(StableHash("dust" + lv + label));
            double roll = r.NextDouble(), pick = r.NextDouble();
            if (lv == 1) return roll < 0.36 ? (pick < 0.6 ? DustPreset.Light : DustPreset.LightPlus) : (DustPreset?)null;
            if (roll < 0.1) return DustPreset.LightPlus;
            if (hero) return DustPreset.Heavy;
            return pick < 0.45 ? DustPreset.Heavy : DustPreset.Forgotten;
        }

        private Material[] DustyMats(Material[] mats, DustPreset preset) => mats.Select(m => _lib.Dusty(m, preset)).ToArray();

        private void ApplyCarDust()
        {
            int light = 0, heavy = 0, flats = 0;
            foreach (var p in _parked)
            {
                if (p.Label == "WASH") continue;
                var preset = DustFor(p.Level, p.Label, p.Hero);
                if (!preset.HasValue) continue;
                var mr = p.Go.GetComponent<MeshRenderer>();
                mr.sharedMaterials = DustyMats(mr.sharedMaterials, preset.Value);
                if (preset.Value >= DustPreset.Heavy) heavy++; else light++;
                if (p.Level == 2 && !p.Hero && new System.Random(StableHash("flat" + p.Label)).NextDouble() < 0.3 && FlatTyre(p)) flats++;
                if (preset.Value == DustPreset.Forgotten && !p.Hero) Cobwebs(p);
            }
            Debug.Log($"[ParkingGarage] Car dust: {light} lightly dusty (level -1), {heavy} forgotten (level -2), {flats} with a flat tyre");
        }

        /// <summary>Mesh collider of a car mesh far below the map (ray probes; removed after the build step).</summary>
        private MeshCollider Probe(Mesh mesh)
        {
            if (_probes.TryGetValue(mesh, out var c) && c != null) return c;
            var go = new GameObject("Probe_" + mesh.name) { hideFlags = HideFlags.DontSave };
            go.transform.position = TarpDrape.ProbeOffset;
            c = go.AddComponent<MeshCollider>();
            c.sharedMesh = mesh;
            _probes[mesh] = c;
            Physics.SyncTransforms();
            return c;
        }

        /// <summary>Wheel centres (car space) found by probing the car mesh from the sides at the axles.</summary>
        private List<Vector3> WheelCenters(SportCar model, Mesh mesh)
        {
            var list = new List<Vector3>();
            var probe = Probe(mesh);
            float r = model.RefTireRadius;
            foreach (float z in new[] { model.RefFrontArch.Z, model.RefRearArch.Z })
            foreach (float s in new[] { -1f, 1f })
            {
                float y = r * 0.6f;
                var from = TarpDrape.ProbeOffset + new Vector3(s * 3f, y, z);
                if (!probe.Raycast(new Ray(from, new Vector3(-s, 0f, 0f)), out var hit, 3f)) continue;
                list.Add(new Vector3(hit.point.x - s * 0.1f, r, z));
            }
            return list;
        }

        /// <summary>Cobwebs in the corner between a tyre and the floor (one or two wheels of a forgotten car).</summary>
        private void Cobwebs(Parked p)
        {
            var rnd = new System.Random(StableHash("web" + p.Label));
            if (rnd.NextDouble() < 0.35) return;
            var model = ParkedModels.FirstOrDefault(m => p.Go.name.Contains("_" + m.Name + "_"));
            if (model == null) return;
            var wheels = WheelCenters(model, p.Go.GetComponent<MeshFilter>().sharedMesh);
            if (wheels.Count == 0) return;
            var mb = new MeshBuilder(_lib);
            var web = M("Cobweb");
            int count = 1 + rnd.Next(2);
            for (int k = 0; k < count; k++)
            {
                var w = wheels[rnd.Next(wheels.Count)];
                float sx = Mathf.Sign(w.x), sz = rnd.NextDouble() < 0.5 ? 1f : -1f;
                float r = model.RefTireRadius;
                float size = 0.2f + 0.12f * (float)rnd.NextDouble();
                // corner = where the tyre meets the floor, just outside its tread; the web spans to the floor and up the tyre
                float x = w.x + sx * (0.11f + 0.01f * k);
                float zc = w.z + sz * (r * 0.62f);
                Vector3 o = new Vector3(x, 0.004f, zc);
                Vector3 alongFloor = new Vector3(0f, 0f, sz * size), upTyre = new Vector3(0f, size * 0.95f, -sz * size * 0.25f);
                // two faces (seen from both sides), UV corner (0,0) at the tyre / floor corner
                Vector3 a = o, b = o + alongFloor, c = o + alongFloor + upTyre, d = o + upTyre;
                Vector3 n = new Vector3(sx, 0f, 0f);
                mb.FaceUV((a + c) * 0.5f, (b - a) * 0.5f, (d - a) * 0.5f, web, Vector2.zero, Vector2.one);
                mb.FaceUV((a + c) * 0.5f + n * 0.002f, (d - a) * 0.5f, (b - a) * 0.5f, web, Vector2.zero, Vector2.one);
            }
            var mesh = mb.ToMesh("Cobwebs_" + p.Label + "_L" + (p.Level + 1));
            _store["Props"].Add(mesh);
            var go = new GameObject("Cobwebs");
            go.transform.SetParent(p.Go.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = mb.Materials;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            GameObjectUtility.SetStaticEditorFlags(go, DetailFlags & ~StaticEditorFlags.BatchingStatic);
        }

        /// <summary>
        /// One flat tyre: the car sinks at that corner (whole body tilted, the neighbouring tyres squashed a little
        /// into the floor) and the sagging rubber bulges round the rim.
        /// </summary>
        private bool FlatTyre(Parked p)
        {
            var model = ParkedModels.FirstOrDefault(m => p.Go.name.Contains("_" + m.Name + "_"));
            if (model == null) return false;
            var mesh = p.Go.GetComponent<MeshFilter>().sharedMesh;
            var wheels = WheelCenters(model, mesh);
            if (wheels.Count < 4) return false;
            var r = new System.Random(StableHash("flatwheel" + p.Label));
            var w = wheels[r.Next(wheels.Count)];
            float track = Mathf.Abs(wheels.Max(q => q.x) - wheels.Min(q => q.x));
            float wheelbase = Mathf.Abs(wheels.Max(q => q.z) - wheels.Min(q => q.z));
            float d = 0.06f, sx = Mathf.Sign(w.x), sz = Mathf.Sign(w.z);
            float pitch = Mathf.Asin(sz * d / (2f * wheelbase)) * Mathf.Rad2Deg;
            float roll = Mathf.Asin(-sx * d / (2f * track)) * Mathf.Rad2Deg;
            var t = p.Go.transform;
            t.SetPositionAndRotation(t.position + Vector3.down * (d * 0.5f), t.rotation * Quaternion.Euler(pitch, 0f, roll));

            var mb = new MeshBuilder(_lib);
            var rubber = _lib.Dusty(M("Rubber"), DustPreset.Heavy);
            foreach (var q in wheels)
            {
                bool flat = q == w;
                bool adjacent = !flat && (Mathf.Sign(q.x) == sx) != (Mathf.Sign(q.z) == sz);
                if (!flat && !adjacent) continue;
                float k = flat ? 1f : 0.45f;
                // in car space the floor under this wheel is now at -sink (the car went down by that much there)
                float sink = flat ? d : d * 0.5f;
                float y = sink + 0.045f * k + 0.004f;
                float halfLen = 0.13f + 0.06f * k;
                foreach (float side in new[] { -1f, 1f })
                {
                    float x = q.x + side * (0.1f + 0.012f * k);
                    mb.Cylinder(new Vector3(x, y, q.z - halfLen), new Vector3(x, y, q.z + halfLen), 0.045f * k + 0.012f, 10, rubber);
                }
                mb.Box(new Vector3(q.x, y - 0.01f, q.z), new Vector3(0.2f, 0.07f * k + 0.02f, halfLen * 2f), rubber);
            }
            var bulge = mb.ToMesh("FlatTyre_" + p.Label + "_L" + (p.Level + 1));
            _store["Props"].Add(bulge);
            var go = new GameObject("FlatTyre");
            go.transform.SetParent(t, false);
            go.AddComponent<MeshFilter>().sharedMesh = bulge;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = mb.Materials;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            GameObjectUtility.SetStaticEditorFlags(go, DetailFlags & ~StaticEditorFlags.BatchingStatic);
            return true;
        }
    }
}
