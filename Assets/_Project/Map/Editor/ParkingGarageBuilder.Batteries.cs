using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using DustPreset = HideAndSeek.Map.Editor.MaterialLibrary.DustPreset;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// Car batteries of 26.11 (the camera-power objective, Game: MatchManager.CarBatteries): every parked car (2026-11-27,
    /// was 6 per level) gets a bonnet that opens. The bonnet is
    /// split out of the car body (the upward-facing paint triangles in front of the windscreen, above the wheel arches,
    /// between the wing tops - found per model from the mesh, so finer car meshes keep working), the body gets a copy of
    /// its mesh without them (one per model, stored in "CarHoods"), the bonnet becomes the "Hood" child with its pivot on
    /// the hinge, and under it a dark engine bay (walls following the cut so nothing shows through), an engine block
    /// and a battery in the front-left corner. A <see cref="CarHood"/> component tells the game which car it is.
    /// The engine bay is shared by all cars of a model. Only the car wash car is left out.
    /// </summary>
    internal sealed partial class ParkingGarageBuilder
    {

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
        private readonly Dictionary<HoodSplit, (Mesh mesh, Material[] mats, Vector3 battery)> _engineBays = new Dictionary<HoodSplit, (Mesh, Material[], Vector3)>();
        private const int ProfileBins = 8;

        private static float DustAmount(DustPreset? p) => !p.HasValue ? 0f : p.Value switch
        {
            DustPreset.Light => 0.2f,
            DustPreset.LightPlus => 0.4f,
            DustPreset.Heavy => 0.7f,
            _ => 1f,
        };

        /// <summary>
        /// Splits the bonnet of EVERY parked car (2026-11-27: any bonnet opens; was 6 per level), builds the engine bays
        /// and batteries. Only the car wash car is left out; a model whose bonnet cannot be found keeps a closed body.
        /// </summary>
        private void BuildCarHoods()
        {
            var built = new List<(Parked p, CarHood hood)>();
            int failed = 0;
            foreach (var p in _parked)
            {
                if (p.Label == "WASH" || p.Go == null) continue;
                if (p.Go.name.Contains("_MazdaRx7_")) { failed++; continue; } // low pointed nose: the bay box does not fit under it
                var hood = AddCarHood(p);
                if (hood == null) { failed++; continue; }
                built.Add((p, hood));
            }
            built.Sort((a, b) => a.p.Level != b.p.Level ? a.p.Level.CompareTo(b.p.Level) : string.CompareOrdinal(a.p.Go.name, b.p.Go.name));
            if (built.Count > MaxCarHoods)
            {
                Debug.LogWarning($"[ParkingGarage] Car hoods: {built.Count} cars, the game syncs at most {MaxCarHoods} - the rest stay shut");
                for (int i = MaxCarHoods; i < built.Count; i++) Object.DestroyImmediate(built[i].hood);
                built.RemoveRange(MaxCarHoods, built.Count - MaxCarHoods);
            }
            for (int i = 0; i < built.Count; i++) built[i].hood.Index = i;
            Debug.Log($"[ParkingGarage] Car hoods: {built.Count} cars with an opening bonnet and a battery " +
                      $"(P 0 {built.Count(b => b.p.Level == 0)}, P -1 {built.Count(b => b.p.Level == 1)}, P -2 {built.Count(b => b.p.Level == 2)}), {failed} without, {_hoodSplits.Count(k => k.Value != null)} models split");
        }

        /// <summary>Bonnets the game can sync (HoodBits: 256-bit masks, the RPCs send the index as a byte).</summary>
        private const int MaxCarHoods = 256;

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
            if (!_engineBays.TryGetValue(split, out var bayInfo))
                _engineBays[split] = bayInfo = (EngineBayMesh(split, mf.sharedMesh.name, out var bm, out Vector3 ba), bm, ba);
            var (bayMesh, bmats, batteryAt) = bayInfo;
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

            // Bonnet = upward-facing paint in front of the windscreen, above the arches, between the wing tops. Triangles
            // across that outline are CUT along it (27.11: taking whole triangles gave the bonnet and the hole in the body
            // saw-tooth edges on the coarser meshes); the new vertices are interpolated (position, normal, uv, ...).
            var paint = mesh.GetTriangles(0);
            var keep = new List<int>(paint.Length);
            var hoodTris = new List<int>();
            bool hasN = nrm.Length == v.Length, hasU = uv.Length == v.Length;
            var cv = new List<Vector3>(v);
            var cn = hasN ? new List<Vector3>(nrm) : null;
            var cu = hasU ? new List<Vector2>(uv) : null;
            var uv2a = mesh.uv2;
            var cu2 = uv2a != null && uv2a.Length == v.Length ? new List<Vector2>(uv2a) : null;
            var tana = mesh.tangents;
            var ct = tana != null && tana.Length == v.Length ? new List<Vector4>(tana) : null;
            var cola = mesh.colors;
            var cc = cola != null && cola.Length == v.Length ? new List<Color>(cola) : null;
            var cut = new Dictionary<(int, int, int), int>();
            float zCut = baseZ + 0.03f;
            float Dist(int plane, Vector3 q) => plane switch
            {
                0 => q.z - zCut,
                1 => hx - q.x,
                2 => q.x + hx,
                _ => q.y - yThr,
            };
            int Mid(int i0, int i1, int plane)
            {
                if (i0 > i1) (i0, i1) = (i1, i0);
                if (cut.TryGetValue((i0, i1, plane), out int m)) return m;
                float d0 = Dist(plane, cv[i0]), d1 = Dist(plane, cv[i1]);
                float t = Mathf.Clamp01(d0 / (d0 - d1));
                m = cv.Count;
                cv.Add(Vector3.Lerp(cv[i0], cv[i1], t));
                cn?.Add(Vector3.Slerp(cn[i0], cn[i1], t).normalized);
                cu?.Add(Vector2.Lerp(cu[i0], cu[i1], t));
                cu2?.Add(Vector2.Lerp(cu2[i0], cu2[i1], t));
                ct?.Add(Vector4.Lerp(ct[i0], ct[i1], t));
                cc?.Add(Color.Lerp(cc[i0], cc[i1], t));
                cut[(i0, i1, plane)] = m;
                return m;
            }
            void Fan(List<int> poly, List<int> dst)
            {
                for (int k = 1; k + 1 < poly.Count; k++) { dst.Add(poly[0]); dst.Add(poly[k]); dst.Add(poly[k + 1]); }
            }
            var inside = new List<int>(8);
            var outside = new List<int>(8);
            for (int i = 0; i + 2 < paint.Length; i += 3)
            {
                Vector3 a0 = v[paint[i]], c1 = v[paint[i + 1]], c2 = v[paint[i + 2]];
                var fn = Vector3.Cross(c1 - a0, c2 - a0);
                if (fn.sqrMagnitude <= 1e-12f || fn.normalized.y <= 0.3f)
                {
                    keep.Add(paint[i]); keep.Add(paint[i + 1]); keep.Add(paint[i + 2]);
                    continue;
                }
                var poly = new List<int>(6) { paint[i], paint[i + 1], paint[i + 2] };
                for (int plane = 0; plane < 4 && poly.Count >= 3; plane++)
                {
                    inside.Clear();
                    outside.Clear();
                    for (int k = 0; k < poly.Count; k++)
                    {
                        int pc = poly[k], pn = poly[(k + 1) % poly.Count];
                        bool inC = Dist(plane, cv[pc]) >= 0f, inN = Dist(plane, cv[pn]) >= 0f;
                        (inC ? inside : outside).Add(pc);
                        if (inC != inN)
                        {
                            int m = Mid(pc, pn, plane);
                            inside.Add(m);
                            outside.Add(m);
                        }
                    }
                    if (outside.Count >= 3) Fan(outside, keep);
                    poly = new List<int>(inside);
                }
                if (poly.Count >= 3) Fan(poly, hoodTris);
            }
            v = cv.ToArray();
            if (hasN) nrm = cn.ToArray();
            if (hasU) uv = cu.ToArray();
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
            if (cv.Count != mesh.vertexCount)
            {
                // the cut added vertices: the body gets them too (all the channels the source had)
                if (cv.Count > 65000) body.indexFormat = IndexFormat.UInt32;
                body.SetVertices(cv);
                if (cn != null) body.SetNormals(cn);
                if (cu != null) body.SetUVs(0, cu);
                if (cu2 != null) body.SetUVs(1, cu2);
                if (ct != null) body.SetTangents(ct);
                if (cc != null) body.SetColors(cc);
            }
            body.SetTriangles(keep, 0);
            foreach (int sm in split.ExtraSubs) body.SetTriangles(extraKeep[sm], sm);
            split.Body = body;

            // one asset per model (27.11: every model is split now - a single CarHoods.asset went past the 50 MB limit)
            if (!_store.ContainsKey("CarHoods")) _store["CarHoods"] = new List<Mesh>();
            string key = "CarHoods_" + new string(mesh.name.Where(char.IsLetterOrDigit).ToArray());
            if (!_store.TryGetValue(key, out var list)) _store[key] = list = new List<Mesh>();
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

            // The engine (2026-11-27: a real-looking bay - transverse block, head, ribbed valve cover in a per-model colour,
            // intake runners and plenum, air box with its hose, exhaust manifold, alternator and belt, radiator with the fan,
            // coolant / washer / brake reservoirs, fuse box, strut tops). All of it stays under the closed bonnet.
            float roof = s.MinTop - 0.06f;
            float midZ = (za + zb) * 0.5f, len = zb - za;
            float room = Mathf.Max(0.16f, roof - floor);
            var metal = M("Metal_Dark");
            var black = M("Plastic_Black");
            var rubber = M("Rubber");
            var steel = M("Metal_Steel");
            var brushed = M("Metal_Galvanized");
            int pick = Mathf.Abs(StableHash("engine" + carName)) % 4;
            var cover = pick == 0 ? M("Paint_Red") : pick == 1 ? M("Car_PlasticGrey") : pick == 2 ? brushed : black;
            float blockH = Mathf.Clamp(room * 0.55f, 0.1f, 0.3f);
            float headH = Mathf.Clamp(room * 0.12f, 0.035f, 0.07f);
            float coverH = Mathf.Clamp(room * 0.1f, 0.03f, 0.06f);
            float blockW = x * 1.05f, blockL = Mathf.Min(len * 0.34f, 0.42f);
            var block = new Vector3(x * 0.12f, floor + 0.05f + blockH * 0.5f, midZ - len * 0.02f);
            mb.Box(block, new Vector3(blockW, blockH, blockL), metal);
            mb.Box(block + new Vector3(0f, -blockH * 0.5f - 0.02f, 0f), new Vector3(blockW * 0.9f, 0.05f, blockL * 0.85f), black);
            float headY = block.y + blockH * 0.5f + headH * 0.5f;
            mb.Box(new Vector3(block.x, headY, block.z), new Vector3(blockW * 0.96f, headH, blockL * 0.8f), brushed);
            float coverY = headY + headH * 0.5f + coverH * 0.5f;
            mb.Box(new Vector3(block.x, coverY, block.z), new Vector3(blockW * 0.9f, coverH, blockL * 0.62f), cover);
            for (int k = 0; k < 5; k++)
                mb.Box(new Vector3(block.x - blockW * 0.36f + k * blockW * 0.18f, coverY + coverH * 0.5f + 0.006f, block.z), new Vector3(0.012f, 0.012f, blockL * 0.5f), cover);
            Vector3 cap = new Vector3(block.x + blockW * 0.3f, coverY + coverH * 0.5f, block.z + blockL * 0.12f);
            mb.Cylinder(cap, cap + Vector3.up * 0.022f, 0.026f, 12, M("Paint_Yellow"));
            Vector3 dip = new Vector3(block.x - blockW * 0.42f, block.y + blockH * 0.3f, block.z + blockL * 0.55f);
            float dipH = Mathf.Max(0.04f, Mathf.Min(0.16f, roof - dip.y - 0.03f));
            mb.Cylinder(dip, dip + new Vector3(0f, dipH, 0f), 0.006f, 6, steel);
            mb.Cylinder(dip + Vector3.up * (dipH - 0.01f), dip + new Vector3(0.03f, dipH, 0f), 0.012f, 8, M("Paint_Yellow"));
            // intake: four runners over the back of the head into a plenum
            float plenY = block.y + blockH * 0.15f, plenZ = block.z - blockL * 0.5f - 0.07f;
            mb.Box(new Vector3(block.x, plenY, plenZ), new Vector3(blockW * 0.8f, Mathf.Min(0.1f, room * 0.3f), 0.09f), black);
            for (int k = 0; k < 4; k++)
            {
                float rx = block.x - blockW * 0.3f + k * blockW * 0.2f;
                mb.Cylinder(new Vector3(rx, headY, block.z - blockL * 0.38f), new Vector3(rx, plenY + 0.03f, plenZ + 0.03f), 0.018f, 8, brushed);
            }
            // exhaust manifold down the front of the block, heat shield
            for (int k = 0; k < 4; k++)
            {
                float ex = block.x - blockW * 0.3f + k * blockW * 0.2f;
                mb.Cylinder(new Vector3(ex, headY - 0.01f, block.z + blockL * 0.4f), new Vector3(block.x, floor + 0.04f, block.z + blockL * 0.62f), 0.016f, 8, M("Car_Chrome"));
            }
            mb.Box(new Vector3(block.x, block.y + blockH * 0.2f, block.z + blockL * 0.58f), new Vector3(blockW * 0.7f, blockH * 0.55f, 0.012f), M("Metal_Galvanized"));
            // alternator with pulleys and the belt at the right end of the block
            float bx = block.x + blockW * 0.5f + 0.035f;
            Vector3 crank = new Vector3(bx, block.y - blockH * 0.25f, block.z), alt = new Vector3(bx, block.y + blockH * 0.25f, block.z + blockL * 0.3f);
            mb.Cylinder(crank + Vector3.left * 0.015f, crank + Vector3.right * 0.015f, 0.065f, 14, steel);
            mb.Cylinder(alt + Vector3.left * 0.06f, alt + Vector3.right * 0.03f, 0.055f, 14, brushed);
            mb.Cylinder(alt + Vector3.right * 0.03f, alt + Vector3.right * 0.045f, 0.03f, 12, steel);
            Vector3 beltD = (alt - crank).normalized;
            Vector3 beltN = Vector3.Cross(beltD, Vector3.right).normalized;
            var beltRot = Quaternion.LookRotation(beltD, beltN);
            mb.Box((alt + crank) * 0.5f + beltN * 0.058f + Vector3.right * 0.04f, new Vector3(0.016f, 0.008f, (alt - crank).magnitude), rubber, beltRot);
            mb.Box((alt + crank) * 0.5f - beltN * 0.058f + Vector3.right * 0.04f, new Vector3(0.016f, 0.008f, (alt - crank).magnitude), rubber, beltRot);
            // air box (left, front) with the hose to the plenum
            float airH = Mathf.Clamp(room * 0.4f, 0.08f, 0.18f);
            var airC = new Vector3(-x * 0.55f, floor + 0.08f + airH * 0.5f, midZ - len * 0.08f);
            if (airC.y + airH * 0.5f > roof - 0.01f) airC.y = roof - 0.01f - airH * 0.5f;
            mb.Box(airC, new Vector3(x * 0.5f, airH, len * 0.2f), black);
            mb.Box(airC + Vector3.up * (airH * 0.5f + 0.006f), new Vector3(x * 0.46f, 0.012f, len * 0.18f), M("Car_PlasticGrey"));
            mb.Cylinder(airC + new Vector3(x * 0.2f, 0f, -len * 0.08f), new Vector3(block.x - blockW * 0.4f, plenY + 0.02f, plenZ), 0.035f, 10, rubber);
            // radiator, fan shroud and fan, hoses to the block
            float radH = Mathf.Min(0.32f, room - 0.02f);
            mb.Box(new Vector3(0f, floor + radH * 0.5f, zb - 0.05f), new Vector3(x * 1.7f, radH, 0.04f), steel);
            mb.Box(new Vector3(0f, floor + radH * 0.5f, zb - 0.085f), new Vector3(x * 1.2f, radH * 0.9f, 0.025f), black);
            Vector3 fan = new Vector3(x * 0.2f, floor + radH * 0.5f, zb - 0.1f);
            mb.Cylinder(fan, fan + Vector3.back * 0.02f, Mathf.Min(0.15f, radH * 0.45f), 16, black);
            mb.Cylinder(fan + Vector3.back * 0.02f, fan + Vector3.back * 0.05f, 0.035f, 10, metal);
            float hoseY = Mathf.Min(block.y + blockH * 0.35f, roof - 0.04f);
            mb.Cylinder(new Vector3(x * 0.55f, hoseY, zb - 0.1f), new Vector3(block.x + blockW * 0.3f, hoseY, block.z + blockL * 0.45f), 0.022f, 8, rubber);
            mb.Cylinder(new Vector3(-x * 0.1f, floor + 0.07f, zb - 0.1f), new Vector3(block.x - blockW * 0.2f, floor + 0.07f, block.z + blockL * 0.45f), 0.02f, 8, rubber);
            // reservoirs: coolant (right rear), brake fluid (left, at the bulkhead), washer (right front)
            float resH = Mathf.Clamp(room * 0.35f, 0.07f, 0.13f);
            var cool = new Vector3(x * 0.72f, Mathf.Min(floor + 0.1f + resH * 0.5f, roof - resH * 0.5f - 0.03f), za + 0.12f);
            mb.Box(cool, new Vector3(0.1f, resH, 0.14f), M("Paint_White"));
            mb.Cylinder(cool + Vector3.up * resH * 0.5f, cool + Vector3.up * (resH * 0.5f + 0.02f), 0.025f, 10, M("Paint_Blue"));
            var brake = new Vector3(-x * 0.62f, Mathf.Min(floor + 0.18f, roof - 0.08f), za + 0.08f);
            mb.Box(brake, new Vector3(0.07f, 0.07f, 0.06f), M("Paint_White"));
            mb.Cylinder(brake + Vector3.up * 0.035f, brake + Vector3.up * 0.052f, 0.02f, 10, M("Paint_Yellow"));
            mb.Cylinder(brake + new Vector3(0f, -0.02f, -0.04f), brake + new Vector3(0f, -0.02f, -0.07f), 0.03f, 12, metal);
            var washer = new Vector3(x * 0.72f, Mathf.Min(floor + 0.06f + resH * 0.5f, roof - resH * 0.5f - 0.03f), zb - 0.2f);
            mb.Box(washer, new Vector3(0.11f, resH, 0.12f), M("Paint_White"));
            mb.Cylinder(washer + Vector3.up * resH * 0.5f, washer + Vector3.up * (resH * 0.5f + 0.018f), 0.022f, 10, M("Paint_Blue"));
            // fuse box (left, behind the air box)
            mb.Box(new Vector3(-x * 0.58f, Mathf.Min(floor + 0.12f, roof - 0.07f), za + 0.24f), new Vector3(0.12f, 0.08f, 0.1f), black);
            // strut tops on the wheel housings
            foreach (float sx in new[] { -1f, 1f })
            {
                Vector3 st = new Vector3(sx * (x - 0.09f), Mathf.Min(roof - 0.02f, floor + room * 0.75f), midZ - len * 0.05f);
                mb.Cylinder(st + Vector3.down * 0.02f, st, 0.07f, 14, metal);
                mb.Cylinder(st, st + Vector3.up * 0.015f, 0.022f, 10, steel);
            }

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
