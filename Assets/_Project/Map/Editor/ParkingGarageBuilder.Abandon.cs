using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using DustPreset = HideAndSeek.Map.Editor.MaterialLibrary.DustPreset;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// Abandonment pass of 26.11 (on top of the dust of 23.11): every level looks forgotten now, the lower ones more.
    /// Per ordinary parked car (heroes and the car wash stay as they are), deterministic per level + bay:
    /// flat tyres (P 0 12 %, P -1 30 %, P -2 50 %; a third of them with both tyres of one side flat, the car leaning
    /// over) and broken windows (7 / 15 / 24 % of the cars, Hns/CarBrokenGlass, see BreakGlass) with glass crumbs on
    /// the floor under the windows that fell out.
    /// </summary>
    internal sealed partial class ParkingGarageBuilder
    {
        private static readonly double[] FlatChance = { 0.12, 0.3, 0.5 };
        private static readonly double[] BrokenGlassChance = { 0.07, 0.15, 0.24 };

        /// <summary>Flat tyres and broken windows of one car (after its dust). Returns (flats, broken).</summary>
        private (int flats, bool broken) Abandon(Parked p)
        {
            if (p.Hero || p.Label == "WASH") return (0, false);
            int flats = 0;
            var rf = new System.Random(StableHash("flat" + p.Level + p.Label));
            if (rf.NextDouble() < FlatChance[p.Level]) flats = FlatTyres(p, rf.NextDouble() < 0.33);
            bool broken = false;
            var rg = new System.Random(StableHash("glass" + p.Level + p.Label));
            if (rg.NextDouble() < BrokenGlassChance[p.Level]) broken = BreakGlass(p, rg);
            return (flats, broken);
        }

        // pane states, the same numbers as Hns/CarBrokenGlass
        private const int PaneIntact = 0, PaneStar = 1, PaneWeb = 2, PaneCrazed = 3, PaneGone = 4;

        /// <summary>Where the glass of one car model is (object space), worked out from its glass triangles.</summary>
        private sealed class GlassLayout
        {
            public float MidZ, SideX, SideY0, SideY1;
            public readonly List<(float z0, float z1)> Segs = new List<(float, float)>(); // side windows, front to back
            public Vector3 FrontUp, RearUp;                       // up along the glass (y, z)
            public Vector2 FrontX, FrontV, RearX, RearV;           // extents in the pane's (x, up) coordinates
            public bool HasFront, HasRear;
        }

        private readonly Dictionary<Mesh, GlassLayout> _glassLayouts = new Dictionary<Mesh, GlassLayout>();

        /// <summary>
        /// Splits the glass of a car model into panes: windscreen (laminated), rear window and up to three side windows
        /// per side (tempered), the side windows separated where the side glass has gaps (the pillars).
        /// </summary>
        private GlassLayout Layout(Mesh mesh, Material[] mats)
        {
            if (_glassLayouts.TryGetValue(mesh, out var cached)) return cached;
            var v = mesh.vertices;
            var nrm = mesh.normals;
            var side = new List<(float z0, float z1, bool dark)>();
            var front = new List<int>();
            var rear = new List<int>();
            var sideVerts = new List<int>();
            float zMin = float.MaxValue, zMax = float.MinValue;
            var tris = new List<(int a, int b, int c, bool dark)>();
            for (int s = 0; s < mesh.subMeshCount && s < mats.Length; s++)
            {
                if (!MaterialLibrary.IsCarGlass(mats[s])) continue;
                bool dark = mats[s].name.Contains("Dark");
                var idx = mesh.GetTriangles(s);
                for (int i = 0; i + 2 < idx.Length; i += 3)
                {
                    tris.Add((idx[i], idx[i + 1], idx[i + 2], dark));
                    for (int j = 0; j < 3; j++) { zMin = Mathf.Min(zMin, v[idx[i + j]].z); zMax = Mathf.Max(zMax, v[idx[i + j]].z); }
                }
            }
            if (tris.Count == 0) { _glassLayouts[mesh] = null; return null; }
            var lay = new GlassLayout { MidZ = (zMin + zMax) * 0.5f };
            Vector3 nF = Vector3.zero, nR = Vector3.zero;
            foreach (var (a, b, c, dark) in tris)
            {
                Vector3 n = nrm.Length == v.Length ? (nrm[a] + nrm[b] + nrm[c]).normalized : Vector3.Cross(v[b] - v[a], v[c] - v[a]).normalized;
                float area = Vector3.Cross(v[b] - v[a], v[c] - v[a]).magnitude * 0.5f;
                if (Mathf.Abs(n.x) > 0.55f)
                {
                    side.Add((Mathf.Min(v[a].z, Mathf.Min(v[b].z, v[c].z)), Mathf.Max(v[a].z, Mathf.Max(v[b].z, v[c].z)), dark));
                    sideVerts.Add(a); sideVerts.Add(b); sideVerts.Add(c);
                    continue;
                }
                Vector3 cen = (v[a] + v[b] + v[c]) / 3f;
                bool isFront = cen.z > lay.MidZ;
                var outward = new Vector3(0f, Mathf.Abs(n.y), Mathf.Abs(n.z) * (isFront ? 1f : -1f));
                if (isFront) { front.Add(a); front.Add(b); front.Add(c); nF += outward * area; }
                else { rear.Add(a); rear.Add(b); rear.Add(c); nR += outward * area; }
            }
            Vector3 Up(Vector3 n)
            {
                if (n.sqrMagnitude < 1e-8f) return Vector3.up;
                var u = Vector3.Cross(n.normalized, Vector3.right);
                return u.y < 0f ? -u : u;
            }
            void Extents(List<int> ids, Vector3 up, out Vector2 xs, out Vector2 vs)
            {
                xs = new Vector2(float.MaxValue, float.MinValue);
                vs = new Vector2(float.MaxValue, float.MinValue);
                foreach (int k in ids)
                {
                    float q = v[k].y * up.y + v[k].z * up.z;
                    xs = new Vector2(Mathf.Min(xs.x, v[k].x), Mathf.Max(xs.y, v[k].x));
                    vs = new Vector2(Mathf.Min(vs.x, q), Mathf.Max(vs.y, q));
                }
            }
            lay.HasFront = front.Count > 0;
            lay.HasRear = rear.Count > 0;
            lay.FrontUp = Up(nF);
            lay.RearUp = Up(nR);
            if (lay.HasFront) Extents(front, lay.FrontUp, out lay.FrontX, out lay.FrontV);
            if (lay.HasRear) Extents(rear, lay.RearUp, out lay.RearX, out lay.RearV);

            if (side.Count > 0)
            {
                lay.SideY0 = sideVerts.Min(k => v[k].y);
                lay.SideY1 = sideVerts.Max(k => v[k].y);
                lay.SideX = sideVerts.Max(k => Mathf.Abs(v[k].x));
                // merge the z ranges of the clear side glass (dark strips over the pillars would join the windows)
                var src = side.Where(q => !q.dark).ToList();
                if (src.Count == 0) src = side;
                var merged = new List<(float z0, float z1)>();
                foreach (var q in src.OrderBy(q => q.z0))
                {
                    if (merged.Count > 0 && q.z0 <= merged[merged.Count - 1].z1 + 0.025f)
                        merged[merged.Count - 1] = (merged[merged.Count - 1].z0, Mathf.Max(merged[merged.Count - 1].z1, q.z1));
                    else merged.Add((q.z0, q.z1));
                }
                if (merged.Count > 1) merged.RemoveAll(q => q.z1 - q.z0 < 0.08f);
                while (merged.Count > 3)
                {
                    int best = 0;
                    for (int i = 1; i < merged.Count - 1; i++)
                        if (merged[i + 1].z0 - merged[i].z1 < merged[best + 1].z0 - merged[best].z1) best = i;
                    merged[best] = (merged[best].z0, merged[best + 1].z1);
                    merged.RemoveAt(best + 1);
                }
                merged.Reverse(); // front first
                lay.Segs.AddRange(merged);
            }
            _glassLayouts[mesh] = lay;
            return lay;
        }

        /// <summary>
        /// Breaks the windows of one car the way real car glass breaks (Hns/CarBrokenGlass): one main pane always, now
        /// and then a second or third. The windscreen is laminated: it never falls out, it gets a small star crack or a
        /// spider web round the impact. Side and rear windows are tempered: they either crumble into tiny cubes and fall
        /// out (a jagged fringe of crazed glass stays in the frame, the crumbs lie on the floor below), or stay in place
        /// crazed all over. Every car gets its own glass material with the pane states.
        /// </summary>
        private bool BreakGlass(Parked p, System.Random rnd)
        {
            var mr = p.Go.GetComponent<MeshRenderer>();
            var mats = mr.sharedMaterials;
            var mesh = p.Go.GetComponent<MeshFilter>().sharedMesh;
            var lay = Layout(mesh, mats);
            if (lay == null) return false;
            float F(double a, double b) => (float)(a + rnd.NextDouble() * (b - a));

            // panes: 0 windscreen, 1 rear, 2..4 left side, 5..7 right side (front to back)
            var st = new int[8];
            int segs = Mathf.Min(3, lay.Segs.Count);
            var valid = new List<int>();
            if (lay.HasFront) valid.Add(0);
            if (lay.HasRear) valid.Add(1);
            for (int i = 0; i < segs; i++) { valid.Add(2 + i); valid.Add(5 + i); }
            if (valid.Count == 0) return false;
            int main;
            double r0 = rnd.NextDouble();
            if (r0 < 0.38 && lay.HasFront) main = 0;
            else if (r0 < 0.86 && segs > 0) main = (rnd.NextDouble() < 0.5 ? 2 : 5) + rnd.Next(segs);
            else main = valid[rnd.Next(valid.Count)];
            double extra = 0.1 + 0.06 * p.Level;
            foreach (int i in valid)
            {
                if (i != main && rnd.NextDouble() >= extra) continue;
                st[i] = i == 0 ? (rnd.NextDouble() < 0.55 ? PaneStar : PaneWeb) : (rnd.NextDouble() < 0.62 ? PaneGone : PaneCrazed);
            }

            // windscreen impacts in its (x, up) plane: one, a web sometimes gets a second small star
            Vector4 hit = Vector4.zero, hit2 = Vector4.zero;
            if (st[0] != PaneIntact)
            {
                float reach = st[0] == PaneStar ? F(0.1, 0.22) : F(0.42, 0.75);
                hit = new Vector4(Mathf.Lerp(lay.FrontX.x, lay.FrontX.y, F(0.18, 0.82)), Mathf.Lerp(lay.FrontV.x, lay.FrontV.y, F(0.25, 0.7)), reach, F(0, 100));
                if (st[0] == PaneWeb && rnd.NextDouble() < 0.4)
                    hit2 = new Vector4(Mathf.Lerp(lay.FrontX.x, lay.FrontX.y, F(0.15, 0.85)), Mathf.Lerp(lay.FrontV.x, lay.FrontV.y, F(0.3, 0.75)), F(0.08, 0.18), F(0, 100));
            }
            Vector4 Seg(int i) => i < segs ? new Vector4(lay.Segs[i].z0, lay.Segs[i].z1) : new Vector4(-99f, -99f);
            float seed = F(0, 100);
            for (int i = 0; i < mats.Length; i++)
            {
                if (!MaterialLibrary.IsCarGlass(mats[i])) continue;
                bool dark = mats[i].name.Contains("Dark");
                var m = _lib.BrokenGlass(mats[i], p.Level, $"L{p.Level}_{p.Label}");
                m.SetVector("_PanesA", new Vector4(st[0], st[1], st[2], st[3]));
                m.SetVector("_PanesB", new Vector4(st[4], st[5], st[6], st[7]));
                m.SetVector("_Seg0", Seg(0));
                m.SetVector("_Seg1", Seg(1));
                m.SetVector("_Seg2", Seg(2));
                m.SetVector("_SideY", new Vector4(lay.SideY0, lay.SideY1, lay.MidZ, seed));
                m.SetVector("_FrontUp", new Vector4(0f, lay.FrontUp.y, lay.FrontUp.z, 0f));
                m.SetVector("_RearUp", new Vector4(0f, lay.RearUp.y, lay.RearUp.z, 0f));
                m.SetVector("_RearBox", new Vector4(lay.RearX.x, lay.RearX.y, lay.RearV.x, lay.RearV.y));
                m.SetVector("_Hit", hit);
                m.SetVector("_Hit2", hit2);
                // the dark strips over the pillars are no windows: they never fall out (a dark hatch window does)
                m.SetFloat("_NoGoneSide", dark ? 1f : 0f);
                EditorUtility.SetDirty(m);
                mats[i] = m;
            }
            mr.sharedMaterials = mats;

            // glass crumbs on the floor under the tempered panes that fell out (laminated glass does not crumble)
            var t = p.Go.transform;
            float floor = FloorY(p.Level) + 0.046f;
            var heaps = new List<Vector3>();
            for (int i = 0; i < segs; i++)
            {
                float zc = (lay.Segs[i].z0 + lay.Segs[i].z1) * 0.5f;
                if (st[2 + i] == PaneGone) heaps.Add(new Vector3(-lay.SideX - F(0.05, 0.3), 0f, zc + F(-0.15, 0.15)));
                if (st[5 + i] == PaneGone) heaps.Add(new Vector3(lay.SideX + F(0.05, 0.3), 0f, zc + F(-0.15, 0.15)));
            }
            if (st[1] == PaneGone) heaps.Add(new Vector3(F(-0.3, 0.3), 0f, mesh.bounds.min.z - F(0.15, 0.4)));
            if (heaps.Count == 0) return true;
            var holder = new GameObject("GlassCrumbs");
            holder.transform.position = new Vector3(t.position.x, floor, t.position.z);
            var mb = new MeshBuilder(_lib);
            var shards = M("Glass_Shards");
            for (int k = 0; k < heaps.Count; k++)
            {
                Vector3 w = t.TransformPoint(heaps[k]);
                Vector3 c = new Vector3(w.x, floor, w.z) - holder.transform.position;
                float size = F(0.3, 0.55);
                float a = F(0, Mathf.PI);
                Vector3 u = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * size, vv = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a)) * size * F(0.6, 1.0);
                mb.FaceUV(c + Vector3.up * (k * 0.001f), u, vv, shards, Vector2.zero, Vector2.one);
            }
            var cm = mb.ToMesh("GlassCrumbs_" + p.Label + "_L" + (p.Level + 1));
            _store["Props"].Add(cm);
            holder.AddComponent<MeshFilter>().sharedMesh = cm;
            var hr = holder.AddComponent<MeshRenderer>();
            hr.sharedMaterials = mb.Materials;
            hr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            hr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            holder.transform.SetParent(t, true);
            GameObjectUtility.SetStaticEditorFlags(holder, DetailFlags & ~StaticEditorFlags.BatchingStatic);
            return true;
        }

        /// <summary>
        /// Flat tyre(s): one random wheel, or (<paramref name="twoOneSide"/>) both wheels of one side. The body sinks
        /// and tilts towards the flat corner(s); each flat tyre gets a deflated cover (FlatTyreMesh): the rim sits a few
        /// centimetres above the floor and the empty rubber lies spread and folded on it. Returns the number of flat tyres.
        /// </summary>
        private int FlatTyres(Parked p, bool twoOneSide)
        {
            var model = ParkedModels.FirstOrDefault(m => p.Go.name.Contains("_" + m.Name + "_"));
            if (model == null) return 0;
            var mesh = p.Go.GetComponent<MeshFilter>().sharedMesh;
            var wheels = WheelCenters(model, mesh);
            if (wheels.Count < 4) return 0;
            var r = new System.Random(StableHash("flatwheel" + p.Level + p.Label));
            var first = wheels[r.Next(wheels.Count)];
            float sx = Mathf.Sign(first.x), sz = Mathf.Sign(first.z);
            var flat = twoOneSide ? wheels.Where(q => Mathf.Sign(q.x) == sx).ToList() : new List<Vector3> { first };
            float d = twoOneSide ? 0.085f : 0.075f;
            float track = Mathf.Abs(wheels.Max(q => q.x) - wheels.Min(q => q.x));
            float wheelbase = Mathf.Abs(wheels.Max(q => q.z) - wheels.Min(q => q.z));
            // average drop per axle / side -> pitch and roll (same convention as the single flat of 23.11)
            float Drop(Vector3 q) => flat.Contains(q) ? d : 0f;
            float midZ = wheels.Average(q => q.z), midX = wheels.Average(q => q.x);
            float front = wheels.Where(q => q.z > midZ).Average(Drop), rear = wheels.Where(q => q.z <= midZ).Average(Drop);
            float left = wheels.Where(q => q.x < midX).Average(Drop), right = wheels.Where(q => q.x >= midX).Average(Drop);
            float pitch = Mathf.Asin(Mathf.Clamp((front - rear) / wheelbase, -0.2f, 0.2f)) * Mathf.Rad2Deg;
            float roll = Mathf.Asin(Mathf.Clamp((left - right) / track, -0.2f, 0.2f)) * Mathf.Rad2Deg;
            var t = p.Go.transform;
            t.SetPositionAndRotation(t.position + Vector3.down * (d * 0.5f), t.rotation * Quaternion.Euler(pitch, 0f, roll));

            var rubber = _lib.Dusty(M("Rubber"), DustPreset.Heavy, p.Level);
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            // hub x from the mesh: the outermost vertex round the wheel is the bulging outer sidewall (TireW / 2 + 1.1 cm off the hub)
            var mv = mesh.vertices;
            foreach (var q0 in flat)
            {
                float R = model.RefTireRadius, outer = 0f, s = Mathf.Sign(q0.x);
                foreach (var v in mv)
                    if (v.y < R * 1.6f && Mathf.Abs(v.z - q0.z) < R && Mathf.Sign(v.x) == s) outer = Mathf.Max(outer, Mathf.Abs(v.x));
                var q = outer > 0f ? new Vector3(s * (outer - model.RefTireWidth * 0.5f - 0.011f), q0.y, q0.z) : q0;
                FlatTyreMesh(q, R, model.RefTireWidth, model.RefRimRadius, d * 0.95f, StableHash("flatfold" + p.Label + q.z), verts, uvs, tris);
            }
            var bulge = new Mesh { name = "FlatTyre_" + p.Label + "_L" + (p.Level + 1) };
            bulge.SetVertices(verts);
            bulge.SetUVs(0, uvs);
            bulge.SetTriangles(tris, 0);
            bulge.RecalculateNormals();
            bulge.RecalculateBounds();
            _store["Props"].Add(bulge);
            var go = new GameObject("FlatTyre");
            go.transform.SetParent(t, false);
            go.AddComponent<MeshFilter>().sharedMesh = bulge;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = rubber;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            GameObjectUtility.SetStaticEditorFlags(go, DetailFlags & ~StaticEditorFlags.BatchingStatic);
            return flat.Count;
        }

        /// <summary>
        /// Deflated tyre round wheel centre <paramref name="q"/> (car space, tyre radius <paramref name="R"/>, floor at
        /// <paramref name="floorY"/> in car space after the car sank): the lower 160 degrees of the tyre as a torus a
        /// little larger than the car's own tyre, squashed onto the floor below the rim. The squashed rubber spreads out
        /// sideways (the sidewalls bulge and fold over), the contact patch gets long, and the folds wrinkle irregularly.
        /// </summary>
        private static void FlatTyreMesh(Vector3 q, float R, float tireW, float rimRadius, float floorY, int seed, List<Vector3> verts, List<Vector2> uvs, List<int> tris)
        {
            var rnd = new System.Random(seed);
            float ph1 = (float)rnd.NextDouble() * 6.28f, ph2 = (float)rnd.NextDouble() * 6.28f;
            float rimR = rimRadius, W = tireW;
            float Rc = (R + rimR) * 0.5f * 1.02f, rt = (R - rimR) * 0.5f * 1.08f, wh = W * 0.5f * 1.04f;
            float rimBottom = q.y - rimR;
            float hs = Mathf.Max(0.04f, rimBottom - floorY) + 0.045f;   // the band that sags
            const int nT = 40, nP = 16;
            const float tMax = 80f * Mathf.Deg2Rad, pMax = 140f * Mathf.Deg2Rad;
            int baseIdx = verts.Count;
            for (int i = 0; i <= nT; i++)
            {
                float th = Mathf.Lerp(-tMax, tMax, i / (float)nT);
                for (int j = 0; j <= nP; j++)
                {
                    float ph = Mathf.Lerp(-pMax, pMax, j / (float)nP);
                    float tread = Mathf.Abs(ph) < 0.9f ? (Mathf.Sin(th * 46f) > 0.2f ? 0.004f : 0f) : 0f;
                    float rho = Rc + rt * Mathf.Cos(ph) + tread;
                    float lat = wh * Mathf.Sin(ph);
                    float y = q.y - rho * Mathf.Cos(th);
                    float z = rho * Mathf.Sin(th);
                    float t = (y - floorY) / hs;
                    float f = t >= 1f ? t : t > 0f ? Mathf.Lerp(0.1f, 1f, t * t * (3f - 2f * t)) : Mathf.Max(0.025f, 0.1f * (1f + t * 0.8f));
                    float k = Mathf.Clamp01(1f - f);
                    float wav = Mathf.Sin(th * 17f + ph1) * 0.6f + Mathf.Sin(th * 29f + ph2) * 0.4f;
                    lat *= 1f + 0.62f * k;
                    lat += Mathf.Sign(lat) * k * k * 0.014f * wav;
                    z *= 1f + 0.22f * k;
                    float yy = floorY + hs * f + k * 0.006f * wav * (Mathf.Abs(ph) > 1.2f ? 1f : 0.3f);
                    verts.Add(new Vector3(q.x + lat, Mathf.Max(floorY + 0.004f, yy), q.z + z));
                    uvs.Add(new Vector2(i / (float)nT * 3f, j / (float)nP));
                }
            }
            // winding chosen so that the bottom of the tread faces down (outward)
            for (int i = 0; i < nT; i++)
            for (int j = 0; j < nP; j++)
            {
                int a = baseIdx + i * (nP + 1) + j, b = a + 1, c = a + nP + 1, e = c + 1;
                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(e);
            }
            // check the orientation once with the face at the bottom centre and flip if it points into the tyre
            int mi = baseIdx + (nT / 2) * (nP + 1) + nP / 2;
            Vector3 n = Vector3.Cross(verts[mi + nP + 1] - verts[mi], verts[mi + 1] - verts[mi]);
            Vector3 outward = new Vector3(0f, -1f, 0f);
            if (Vector3.Dot(n, outward) < 0f)
                for (int k = tris.Count - nT * nP * 6; k < tris.Count; k += 3) { int tmp = tris[k + 1]; tris[k + 1] = tris[k + 2]; tris[k + 2] = tmp; }
        }

        /// <summary>
        /// Litter on the floors of level <paramref name="lv"/> (decal sheet "Litter"): along the walls, in the corners of
        /// the bays, a few out in the aisles; plus extra cracks, puddles and wall stains on top of BuildGrime. Uses its
        /// own random sequence, so nothing of the map layout that is built after it moves.
        /// </summary>
        private void BuildLitter(int lv)
        {
            var rnd = new System.Random(2611 + lv * 977);
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            var d = _decals[lv];
            float y0 = FloorY(lv);
            var bays = _bays.Where(b => b.Level == lv).ToList();
            int count = lv == 0 ? 70 : lv == 1 ? 110 : 160;
            var litter = M("Litter");
            for (int i = 0; i < count; i++)
            {
                Vector3 c;
                int sel = rnd.Next(4);
                if (sel == 0) c = new Vector3(R(-84f, 84f), 0, HZ - R(0.3f, 1.1f));                                    // north wall
                else if (sel == 1) c = new Vector3((rnd.NextDouble() < 0.5 ? -1f : 1f) * (HX - R(0.3f, 1.1f)), 0, R(-55f, 55f)); // side walls
                else if (sel == 2 && bays.Count > 0)
                {
                    var bay = bays[rnd.Next(bays.Count)];
                    c = bay.Center + bay.Inward * (bay.Depth * 0.5f - R(0.2f, 0.7f)) + Vector3.Cross(Vector3.up, bay.Inward) * R(-1.1f, 1.1f);
                }
                else c = new Vector3(R(-74f, 74f), 0, AisleZ[rnd.Next(AisleZ.Length)] + R(-2.8f, 2.8f));
                c.y = y0 + DecalY.Mark + 0.002f + i * 0.00001f;
                float s = R(0.7f, 1.6f);
                d.FloorDecal(c, new Vector3(R(-1, 1), 0, R(-1, 1)) + Vector3.forward * 0.01f, s, s, litter);
            }
            // extra cracks and puddles (BuildGrime: 40 / 40 / 110 cracks, 14 / 24 / 55 puddles)
            int cracks = lv == 1 ? 40 : 25;
            for (int i = 0; i < cracks; i++)
            {
                var c = new Vector3(R(-80f, 80f), y0 + DecalY.Crack, rnd.NextDouble() < 0.6 ? AisleZ[rnd.Next(AisleZ.Length)] + R(-2.5f, 2.5f) : R(-52f, 46f));
                float s = R(1.5f, 4f);
                d.FloorDecal(c, new Vector3(R(-1, 1), 0, R(-1, 1)) + Vector3.right * 0.01f, s, s, M("Crack"));
            }
            int puddles = lv == 0 ? 10 : lv == 1 ? 14 : 8;
            for (int i = 0; i < puddles; i++)
            {
                var c = new Vector3(R(-80f, 80f), y0 + DecalY.Puddle, rnd.NextDouble() < 0.6 ? AisleZ[rnd.Next(AisleZ.Length)] + R(-2f, 2f) : R(-55f, 46f));
                d.FloorDecal(c, new Vector3(R(-1, 1), 0, R(-1, 1)) + Vector3.forward * 0.01f, R(1.2f, 3f), R(1.5f, 3.5f), M("Puddle"));
            }
            if (lv < 2) WallStains(lv, lv == 1 ? 28 : 16, rnd);
        }
    }
}
