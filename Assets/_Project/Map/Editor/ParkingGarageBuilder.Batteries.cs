using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using DustPreset = HideAndSeek.Map.Editor.MaterialLibrary.DustPreset;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// Car batteries of 26.11 (the camera-power objective, Game: MatchManager.CarBatteries): on every level up to
    /// <see cref="HoodsPerLevel"/> ordinary parked cars standing nose to the aisle get a bonnet that opens. The bonnet is
    /// split out of the car body (the upward-facing paint triangles in front of the windscreen, above the wheel arches,
    /// between the wing tops - found per model from the mesh, so finer car meshes keep working), the body gets a copy of
    /// its mesh without them (one per model, stored in "CarHoods"), the bonnet becomes the "Hood" child with its pivot on
    /// the hinge, and under it a dark engine bay (walls following the cut so nothing shows through), an engine block
    /// and a battery in the front-left corner. A <see cref="CarHood"/> component tells the game which car it is.
    /// The game picks 3-5 of them per level each round. Own random sequence (StableHash), heroes, the car wash and vans
    /// are left out.
    /// </summary>
    internal sealed partial class ParkingGarageBuilder
    {
        private const int HoodsPerLevel = 6;
        private const float HoodMinSpacing = 13f;

        /// <summary>One car model's bonnet cut out of its body mesh (car space).</summary>
        private sealed class HoodSplit
        {
            public Mesh Body, Hood;
            public Vector3 Pivot;           // hinge: rear edge of the bonnet, top
            public float HalfX, Z0, Z1;     // bonnet extent (Z0 = hinge end, Z1 = front edge)
            public float MinTop;            // lowest point of the bonnet
            public float[] SideTop, FrontTop, RearTop; // bonnet height along its edges (bins), for the bay walls
            public Vector3 Front;           // middle of the front edge
            public readonly List<int> ExtraSubs = new List<int>(); // source sub-meshes of the bits riding on the bonnet (hood sub-mesh 2, 3, ...)
        }

        private readonly Dictionary<Mesh, HoodSplit> _hoodSplits = new Dictionary<Mesh, HoodSplit>();
        private const int ProfileBins = 8;

        private static float DustAmount(DustPreset? p) => !p.HasValue ? 0f : p.Value switch
        {
            DustPreset.Light => 0.2f,
            DustPreset.LightPlus => 0.4f,
            DustPreset.Heavy => 0.7f,
            _ => 1f,
        };

        /// <summary>Picks the cars, splits their bonnets, builds the engine bays and batteries.</summary>
        private void BuildCarHoods()
        {
            var built = new List<(Parked p, CarHood hood)>();
            for (int lv = 0; lv < Levels; lv++)
            {
                var cands = new List<(Parked p, int key)>();
                foreach (var p in _parked)
                {
                    if (p.Hero || p.Label == "WASH" || p.Go == null) continue;
                    if (Vector3.Dot(p.Go.transform.forward, p.Inward) > -0.5f) continue; // nose into the bay: no room at the bonnet
                    var model = ParkedModels.FirstOrDefault(m => p.Go.name.Contains("_" + m.Name + "_"));
                    if (model == null || model.IsVan) continue;
                    if (p.Level != lv) continue;
                    cands.Add((p, StableHash("hood" + lv + p.Label)));
                }
                cands.Sort((a, b) => a.key.CompareTo(b.key));
                var picked = new List<Vector3>();
                foreach (var (p, _) in cands)
                {
                    if (picked.Count >= HoodsPerLevel) break;
                    Vector3 at = p.Go.transform.position;
                    if (picked.Any(q => (q - at).sqrMagnitude < HoodMinSpacing * HoodMinSpacing)) continue;
                    var hood = AddCarHood(p);
                    if (hood == null) continue;
                    picked.Add(at);
                    built.Add((p, hood));
                }
            }
            built.Sort((a, b) => a.p.Level != b.p.Level ? a.p.Level.CompareTo(b.p.Level) : string.CompareOrdinal(a.p.Go.name, b.p.Go.name));
            for (int i = 0; i < built.Count; i++) built[i].hood.Index = i;
            Debug.Log($"[ParkingGarage] Car hoods: {built.Count} cars with an opening bonnet and a battery " +
                      $"(P 0 {built.Count(b => b.p.Level == 0)}, P -1 {built.Count(b => b.p.Level == 1)}, P -2 {built.Count(b => b.p.Level == 2)}), {_hoodSplits.Count(k => k.Value != null)} models split");
        }

        private CarHood AddCarHood(Parked p)
        {
            var mf = p.Go.GetComponent<MeshFilter>();
            var mr = p.Go.GetComponent<MeshRenderer>();
            if (mf == null || mr == null || mf.sharedMesh == null) return null;
            var split = SplitHood(mf.sharedMesh, mr.sharedMaterials);
            if (split == null) return null;
            var t = p.Go.transform;
            mf.sharedMesh = split.Body;

            // The bonnet: the car's own (dusty) paint on top, dark underside.
            var hoodGo = new GameObject("Hood");
            hoodGo.transform.SetParent(t, false);
            hoodGo.transform.localPosition = split.Pivot;
            hoodGo.AddComponent<MeshFilter>().sharedMesh = split.Hood;
            var hr = hoodGo.AddComponent<MeshRenderer>();
            var carMats = mr.sharedMaterials;
            var hoodMats = new List<Material> { carMats[0], M("Car_Trim") };
            foreach (int sm in split.ExtraSubs) hoodMats.Add(sm < carMats.Length ? carMats[sm] : M("Car_Trim"));
            hr.sharedMaterials = hoodMats.ToArray();
            hr.lightProbeUsage = LightProbeUsage.Off;
            hr.reflectionProbeUsage = ReflectionProbeUsage.Off;

            // Engine bay (static) and the battery (hidden when taken).
            var bay = new GameObject("EngineBay");
            bay.transform.SetParent(t, false);
            var bayMesh = EngineBayMesh(split, p.Go.name, out var bmats, out Vector3 batteryAt);
            bay.AddComponent<MeshFilter>().sharedMesh = bayMesh;
            var br = bay.AddComponent<MeshRenderer>();
            br.sharedMaterials = bmats;
            br.lightProbeUsage = LightProbeUsage.Off;
            br.reflectionProbeUsage = ReflectionProbeUsage.Off;
            GameObjectUtility.SetStaticEditorFlags(bay, DetailFlags & ~StaticEditorFlags.BatchingStatic);

            var bat = new GameObject("Battery");
            bat.transform.SetParent(t, false);
            bat.transform.localPosition = batteryAt;
            bat.AddComponent<MeshFilter>().sharedMesh = BatteryMesh(out var batMats);
            var batR = bat.AddComponent<MeshRenderer>();
            batR.sharedMaterials = batMats;
            batR.lightProbeUsage = LightProbeUsage.Off;
            batR.reflectionProbeUsage = ReflectionProbeUsage.Off;

            var hood = p.Go.AddComponent<CarHood>();
            hood.Hood = hoodGo.transform;
            hood.Battery = bat.transform;
            hood.Level = p.Level;
            hood.Dust = DustAmount(DustFor(p.Level, p.Label, p.Hero));
            hood.FrontLocal = split.Front;
            hood.BatteryLocal = batteryAt + Vector3.up * (BatH + 0.02f);
            return hood;
        }

        // ================================================================== the cut

        private HoodSplit SplitHood(Mesh mesh, Material[] mats)
        {
            if (_hoodSplits.TryGetValue(mesh, out var cached)) return cached;
            _hoodSplits[mesh] = null;
            var v = mesh.vertices;
            var nrm = mesh.normals;
            var uv = mesh.uv;
            var b = mesh.bounds;
            float halfW = Mathf.Max(Mathf.Abs(b.min.x), Mathf.Abs(b.max.x));

            // Windscreen base: the most forward point of the front glass (and its lowest height).
            float baseZ = float.MinValue, baseY = float.MaxValue, glassMid;
            {
                var glass = new List<int>();
                float zMin = float.MaxValue, zMax = float.MinValue;
                for (int s = 0; s < mesh.subMeshCount && s < mats.Length; s++)
                {
                    if (!MaterialLibrary.IsCarGlass(mats[s])) continue;
                    var idx = mesh.GetTriangles(s);
                    glass.AddRange(idx);
                    foreach (int k in idx) { zMin = Mathf.Min(zMin, v[k].z); zMax = Mathf.Max(zMax, v[k].z); }
                }
                glassMid = (zMin + zMax) * 0.5f;
                for (int i = 0; i + 2 < glass.Count; i += 3)
                {
                    Vector3 a = v[glass[i]], c1 = v[glass[i + 1]], c2 = v[glass[i + 2]];
                    var n = Vector3.Cross(c1 - a, c2 - a).normalized;
                    if (Mathf.Abs(n.x) > 0.55f || (a.z + c1.z + c2.z) / 3f < glassMid) continue;
                    foreach (var q in new[] { a, c1, c2 }) { baseZ = Mathf.Max(baseZ, q.z); baseY = Mathf.Min(baseY, q.y); }
                }
            }
            if (baseZ == float.MinValue) { baseZ = b.max.z - b.size.z * 0.42f; baseY = b.center.y + b.extents.y * 0.25f; }
            float yThr = Mathf.Max(baseY - 0.34f, b.min.y + b.size.y * 0.38f);
            // The bonnet ends at the shut lines on the wing tops, ~11 cm in from the body side (ParkedCar SeamTop at
            // x = ±(W - 0.11)); the body side is measured ahead of the mirrors.
            float frontHalf = 0f;
            var paint0 = mesh.GetTriangles(0);
            foreach (int k in paint0)
                if (v[k].z > baseZ + 0.15f && v[k].y > yThr) frontHalf = Mathf.Max(frontHalf, Mathf.Abs(v[k].x));
            float hx = Mathf.Clamp(frontHalf - 0.1f, halfW * 0.55f, halfW * 0.85f);

            // Bonnet = upward-facing paint triangles in front of the windscreen, above the arches, between the wing tops.
            var paint = mesh.GetTriangles(0);
            var keep = new List<int>(paint.Length);
            var hoodTris = new List<int>();
            for (int i = 0; i + 2 < paint.Length; i += 3)
            {
                Vector3 a = v[paint[i]], c1 = v[paint[i + 1]], c2 = v[paint[i + 2]];
                Vector3 cen = (a + c1 + c2) / 3f;
                var fn = Vector3.Cross(c1 - a, c2 - a);
                bool hood = fn.sqrMagnitude > 1e-12f && fn.normalized.y > 0.5f && cen.z > baseZ + 0.03f && cen.y > yThr && Mathf.Abs(cen.x) < hx;
                (hood ? hoodTris : keep).Add(paint[i]);
                (hood ? hoodTris : keep).Add(paint[i + 1]);
                (hood ? hoodTris : keep).Add(paint[i + 2]);
            }
            if (hoodTris.Count < 18)
            {
                Debug.LogWarning($"[ParkingGarage] Car hoods: no bonnet found on {mesh.name} ({hoodTris.Count / 3} triangles)");
                return null;
            }

            // Extents and the hinge.
            var pts = hoodTris.Distinct().Select(k => v[k]).ToList();
            float z0 = pts.Min(q => q.z), z1 = pts.Max(q => q.z);
            float xMax = pts.Max(q => Mathf.Abs(q.x));
            float pivotY = pts.Where(q => q.z < z0 + 0.06f).Max(q => q.y);
            var split = new HoodSplit
            {
                Pivot = new Vector3(0f, pivotY, z0), HalfX = xMax, Z0 = z0, Z1 = z1, MinTop = pts.Min(q => q.y),
                SideTop = Profile(pts, q => Mathf.Abs(q.x) > xMax - 0.12f, q => q.z, z0, z1),
                FrontTop = Profile(pts, q => q.z > z1 - 0.12f, q => q.x, -xMax, xMax),
                RearTop = Profile(pts, q => q.z < z0 + 0.12f, q => q.x, -xMax, xMax),
            };
            split.Front = new Vector3(0f, split.FrontTop[ProfileBins / 2], z1);

            // Bits of other materials lying ON the bonnet (the shut-line strips, badges) ride on it, so they move into the
            // hood too. Strict: every corner inside the bonnet outline (a little past its sides for the strips, never past
            // its front / rear edge, so lamps, grille, cowl and wipers stay), no lower than the bonnet right under it and
            // at most 4 cm above it, the face pointing up. A sub-mesh that would give away a lot is left alone (logged).
            var extra = new Dictionary<int, List<int>>();
            var extraKeep = new Dictionary<int, List<int>>();
            bool OnHood(Vector3 q)
            {
                if (Mathf.Abs(q.x) > xMax + 0.03f || q.z < z0 + 0.005f || q.z > z1 - 0.005f) return false;
                float lo = float.MaxValue, hi = float.MinValue;
                foreach (var h in pts)
                {
                    if (Mathf.Abs(h.x - q.x) > 0.08f || Mathf.Abs(h.z - q.z) > 0.08f) continue;
                    lo = Mathf.Min(lo, h.y);
                    hi = Mathf.Max(hi, h.y);
                }
                return lo < float.MaxValue && q.y >= lo - 0.008f && q.y <= hi + 0.04f;
            }
            const int MaxRiders = 800;
            for (int sm = 1; sm < mesh.subMeshCount; sm++)
            {
                // only the shut-line strips (Car_Seam, merged into Car_Trim on parked cars) and badges ride along;
                // lamps, lenses, chrome rings and the grille always stay on the body
                string sname = sm < mats.Length && mats[sm] != null ? mats[sm].name : "";
                if (!sname.StartsWith("Car_Trim") && !sname.StartsWith("Car_Seam") && !sname.StartsWith("Car_Badge")) continue;
                var idx = mesh.GetTriangles(sm);
                List<int> moved = null, rest = new List<int>(idx.Length);
                for (int i = 0; i + 2 < idx.Length; i += 3)
                {
                    Vector3 a = v[idx[i]], c1 = v[idx[i + 1]], c2 = v[idx[i + 2]];
                    var fn = Vector3.Cross(c1 - a, c2 - a);
                    bool on = fn.sqrMagnitude > 1e-14f && fn.normalized.y > 0.3f && OnHood(a) && OnHood(c1) && OnHood(c2);
                    var dst = on ? (moved ??= new List<int>()) : rest;
                    dst.Add(idx[i]); dst.Add(idx[i + 1]); dst.Add(idx[i + 2]);
                }
                if (moved == null) continue;
                string mname = sm < mats.Length && mats[sm] != null ? mats[sm].name : "sub" + sm;
                if (moved.Count / 3 > MaxRiders)
                {
                    Debug.LogWarning($"[ParkingGarage] Car hoods: {mesh.name}: {moved.Count / 3} triangles of {mname} on the bonnet - too many, left on the body");
                    continue;
                }
                Debug.Log($"[ParkingGarage] Car hoods: {mesh.name}: {moved.Count / 3} triangles of {mname} ride on the bonnet");
                extra[sm] = moved;
                extraKeep[sm] = rest;
                split.ExtraSubs.Add(sm);
            }

            // Bonnet mesh: the triangles (moved to the hinge) + a dark underside a few millimetres below.
            var map = new Dictionary<int, int>();
            var hv = new List<Vector3>();
            var hn = new List<Vector3>();
            var huv = new List<Vector2>();
            var top = new List<int>();
            foreach (int k in hoodTris)
            {
                if (!map.TryGetValue(k, out int j))
                {
                    j = hv.Count;
                    map[k] = j;
                    hv.Add(v[k] - split.Pivot);
                    hn.Add(nrm.Length == v.Length ? nrm[k] : Vector3.up);
                    huv.Add(uv.Length == v.Length ? uv[k] : Vector2.zero);
                }
                top.Add(j);
            }
            int nTop = hv.Count;
            for (int i = 0; i < nTop; i++)
            {
                hv.Add(hv[i] - hn[i] * 0.008f);
                hn.Add(-hn[i]);
                huv.Add(huv[i]);
            }
            var under = new List<int>(top.Count);
            for (int i = 0; i + 2 < top.Count; i += 3)
            {
                under.Add(top[i] + nTop);
                under.Add(top[i + 2] + nTop);
                under.Add(top[i + 1] + nTop);
            }
            var extraTris = new List<List<int>>();
            foreach (int sm in split.ExtraSubs)
            {
                var tl = new List<int>();
                foreach (int k in extra[sm])
                {
                    tl.Add(hv.Count);
                    hv.Add(v[k] - split.Pivot);
                    hn.Add(nrm.Length == v.Length ? nrm[k] : Vector3.up);
                    huv.Add(uv.Length == v.Length ? uv[k] : Vector2.zero);
                }
                extraTris.Add(tl);
            }
            var hm = new Mesh { name = mesh.name + "_Hood" };
            if (hv.Count > 65000) hm.indexFormat = IndexFormat.UInt32;
            hm.SetVertices(hv);
            hm.SetNormals(hn);
            hm.SetUVs(0, huv);
            hm.subMeshCount = 2 + extraTris.Count;
            hm.SetTriangles(top, 0);
            hm.SetTriangles(under, 1);
            for (int e = 0; e < extraTris.Count; e++) hm.SetTriangles(extraTris[e], 2 + e);
            hm.RecalculateBounds();
            hm.RecalculateTangents();
            split.Hood = hm;

            // Body without the bonnet (same sub-meshes, so the per-car materials still fit).
            var body = Object.Instantiate(mesh);
            body.name = mesh.name + "_NoHood";
            body.SetTriangles(keep, 0);
            foreach (int sm in split.ExtraSubs) body.SetTriangles(extraKeep[sm], sm);
            split.Body = body;

            if (!_store.TryGetValue("CarHoods", out var list)) _store["CarHoods"] = list = new List<Mesh>();
            list.Add(body);
            list.Add(hm);
            _hoodSplits[mesh] = split;
            Debug.Log($"[ParkingGarage] Car hoods: {mesh.name} bonnet {hoodTris.Count / 3} triangles (+{extraTris.Sum(t => t.Count) / 3} of {split.ExtraSubs.Count} other materials riding on it), x ±{xMax:0.00} (cut at ±{hx:0.00}, body ±{frontHalf:0.00}), z {z0:0.00}..{z1:0.00}, hinge y {pivotY:0.00}, windscreen base z {baseZ:0.00} y {baseY:0.00}");
            return split;
        }

        /// <summary>Lowest bonnet height at ProfileBins + 1 points from <paramref name="a"/> to <paramref name="b"/> along
        /// <paramref name="coord"/> (points near the edge only; gaps filled from the neighbours).</summary>
        private static float[] Profile(List<Vector3> pts, System.Func<Vector3, bool> near, System.Func<Vector3, float> coord, float a, float b)
        {
            var r = new float[ProfileBins + 1];
            float step = (b - a) / ProfileBins;
            var edge = pts.Where(near).ToList();
            if (edge.Count == 0) edge = pts;
            for (int j = 0; j <= ProfileBins; j++)
            {
                float c = a + step * j;
                r[j] = float.NaN;
                foreach (var q in edge)
                    if (Mathf.Abs(coord(q) - c) <= step * 0.75f && (float.IsNaN(r[j]) || q.y < r[j])) r[j] = q.y;
            }
            float fallback = edge.Min(q => q.y);
            for (int j = 0; j <= ProfileBins; j++)
            {
                if (!float.IsNaN(r[j])) continue;
                float best = fallback;
                for (int d = 1; d <= ProfileBins; d++)
                {
                    if (j - d >= 0 && !float.IsNaN(r[j - d])) { best = r[j - d]; break; }
                    if (j + d <= ProfileBins && !float.IsNaN(r[j + d])) { best = r[j + d]; break; }
                }
                r[j] = best;
            }
            return r;
        }

        // ================================================================== engine bay + battery

        private const float BatL = 0.27f, BatW = 0.175f, BatH = 0.19f;

        /// <summary>Dark open box under the bonnet (inward faces), the engine, hoses; returns where the battery stands (car space, bottom centre).</summary>
        private Mesh EngineBayMesh(HoodSplit s, string carName, out Material[] mats, out Vector3 batteryAt)
        {
            var mb = new MeshBuilder(_lib);
            var wall = M("Car_Trim");
            float x = s.HalfX - 0.015f, za = s.Z0 + 0.02f, zb = s.Z1 - 0.035f;
            float floor = Mathf.Clamp(s.MinTop - 0.42f, 0.24f, 0.4f);
            const float below = 0.014f;
            float SideTop(float z) => Sample(s.SideTop, (z - s.Z0) / Mathf.Max(0.01f, s.Z1 - s.Z0)) - below;
            float FrontTop(float xx) => Sample(s.FrontTop, (xx + s.HalfX) / (2f * s.HalfX)) - below;
            float RearTop(float xx) => Sample(s.RearTop, (xx + s.HalfX) / (2f * s.HalfX)) - below;
            for (int i = 0; i < ProfileBins; i++)
            {
                // side walls (both), facing inwards
                float z1 = Mathf.Lerp(za, zb, i / (float)ProfileBins), z2 = Mathf.Lerp(za, zb, (i + 1) / (float)ProfileBins);
                foreach (float sx in new[] { -1f, 1f })
                {
                    var n = new Vector3(-sx, 0f, 0f);
                    mb.Quad(new Vector3(sx * x, floor, z1), new Vector3(sx * x, floor, z2), new Vector3(sx * x, SideTop(z2), z2), new Vector3(sx * x, SideTop(z1), z1), n, n, n, n, wall);
                }
                // front and rear walls
                float x1 = Mathf.Lerp(-x, x, i / (float)ProfileBins), x2 = Mathf.Lerp(-x, x, (i + 1) / (float)ProfileBins);
                var nb = Vector3.back;
                mb.Quad(new Vector3(x1, floor, zb), new Vector3(x2, floor, zb), new Vector3(x2, FrontTop(x2), zb), new Vector3(x1, FrontTop(x1), zb), nb, nb, nb, nb, wall);
                var nf = Vector3.forward;
                mb.Quad(new Vector3(x1, floor, za), new Vector3(x2, floor, za), new Vector3(x2, RearTop(x2), za), new Vector3(x1, RearTop(x1), za), nf, nf, nf, nf, wall);
            }
            var up = Vector3.up;
            mb.Quad(new Vector3(-x, floor, za), new Vector3(x, floor, za), new Vector3(x, floor, zb), new Vector3(-x, floor, zb), up, up, up, up, wall);

            // Engine block with a valve cover, an air box, a radiator and hoses; kept under the closed bonnet.
            float roof = s.MinTop - 0.06f;
            float midZ = (za + zb) * 0.5f, len = zb - za;
            float blockH = Mathf.Clamp(roof - floor - 0.06f, 0.12f, 0.36f);
            var metal = M("Metal_Dark");
            var black = M("Plastic_Black");
            var rubber = M("Rubber");
            var block = new Vector3(x * 0.15f, floor + blockH * 0.5f, midZ - len * 0.04f);
            mb.Box(block, new Vector3(x * 0.95f, blockH, len * 0.42f), metal);
            mb.Box(block + new Vector3(0f, blockH * 0.5f + 0.025f, 0f), new Vector3(x * 0.8f, 0.05f, len * 0.36f), black);
            float airH = Mathf.Clamp(roof - floor - 0.04f, 0.1f, 0.24f);
            mb.Box(new Vector3(x * 0.62f, floor + airH * 0.5f, midZ + len * 0.22f), new Vector3(x * 0.5f, airH, len * 0.22f), black);
            mb.Box(new Vector3(0f, floor + Mathf.Min(0.3f, roof - floor - 0.03f) * 0.5f, zb - 0.05f), new Vector3(x * 1.7f, Mathf.Min(0.3f, roof - floor - 0.03f), 0.04f), M("Metal_Steel"));
            float hoseY = floor + Mathf.Min(blockH * 0.7f, roof - floor - 0.05f);
            mb.Cylinder(new Vector3(x * 0.35f, hoseY, zb - 0.08f), new Vector3(x * 0.2f, hoseY, block.z + len * 0.2f), 0.025f, 8, rubber);
            mb.Cylinder(new Vector3(-x * 0.1f, hoseY - 0.03f, zb - 0.08f), new Vector3(-x * 0.25f, hoseY - 0.05f, block.z), 0.02f, 8, rubber);

            // Battery corner (front left): a tray under it, the cables into the bay.
            batteryAt = new Vector3(-(x - 0.03f - BatW * 0.5f), floor + 0.02f, zb - 0.04f - BatL * 0.5f);
            if (batteryAt.y + BatH + 0.035f > roof) batteryAt.y = Mathf.Max(floor + 0.005f, roof - BatH - 0.035f);
            mb.Box(batteryAt + new Vector3(0f, -0.01f, 0f), new Vector3(BatW + 0.03f, 0.02f, BatL + 0.03f), black);
            Vector3 plus = batteryAt + new Vector3(0.04f, BatH + 0.03f, BatL * 0.5f - 0.05f);
            Vector3 minus = batteryAt + new Vector3(0.04f, BatH + 0.03f, -BatL * 0.5f + 0.05f);
            mb.Cylinder(plus + new Vector3(0.07f, -0.02f, 0f), new Vector3(block.x - x * 0.4f, hoseY - 0.02f, block.z + len * 0.15f), 0.011f, 6, M("Paint_Red"));
            mb.Cylinder(minus + new Vector3(0.07f, -0.02f, 0f), new Vector3(-x + 0.02f, floor + 0.05f, minus.z - 0.06f), 0.011f, 6, rubber);
            mats = mb.Materials;
            var mesh = mb.ToMesh("EngineBay_" + carName);
            _store["CarHoods"].Add(mesh);
            return mesh;
        }

        private static float Sample(float[] prof, float t)
        {
            t = Mathf.Clamp01(t) * (prof.Length - 1);
            int i = Mathf.Min(prof.Length - 2, Mathf.FloorToInt(t));
            return Mathf.Lerp(prof[i], prof[i + 1], t - i);
        }

        private Mesh _batteryMesh;
        private Material[] _batteryMats;

        /// <summary>The 12 V battery (origin = bottom centre, long side along z): black case with a lid, the red + and
        /// black - terminals with their clamps.</summary>
        private Mesh BatteryMesh(out Material[] mats)
        {
            if (_batteryMesh != null) { mats = _batteryMats; return _batteryMesh; }
            var mb = new MeshBuilder(_lib);
            var black = M("Plastic_Black");
            mb.Box(new Vector3(0f, BatH * 0.45f, 0f), new Vector3(BatW, BatH * 0.9f, BatL), black);
            mb.Box(new Vector3(0f, BatH * 0.95f, 0f), new Vector3(BatW + 0.006f, BatH * 0.1f, BatL + 0.006f), M("Car_Trim"));
            foreach (float s in new[] { 1f, -1f })
            {
                Vector3 post = new Vector3(0.04f, BatH, s * (BatL * 0.5f - 0.05f));
                mb.Cylinder(post, post + Vector3.up * 0.022f, 0.011f, 8, M("Metal_Steel"));
                mb.Cylinder(post + Vector3.up * 0.012f, post + Vector3.up * 0.03f, 0.019f, 8, s > 0 ? M("Paint_Red") : M("Rubber"));
                mb.Box(post + new Vector3(0.045f, 0.02f, 0f), new Vector3(0.06f, 0.014f, 0.022f), s > 0 ? M("Paint_Red") : M("Rubber"));
            }
            mb.Box(new Vector3(-0.045f, BatH + 0.004f, 0f), new Vector3(0.05f, 0.008f, BatL * 0.5f), M("Metal_Dark")); // vent strip
            _batteryMats = mats = mb.Materials;
            _batteryMesh = mb.ToMesh("CarBattery");
            _store["CarHoods"].Add(_batteryMesh);
            return _batteryMesh;
        }
    }
}
