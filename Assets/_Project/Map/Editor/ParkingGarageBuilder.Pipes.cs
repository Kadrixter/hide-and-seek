using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// Broken water pipes under the ceiling with water dripping from them (5 / 6 / 8 per level, runtime animation in
    /// <see cref="CeilingDrips"/>). Each is a run along an aisle, 1.7 m off its centre line (clear of the lamps, signs,
    /// beams, ducts, trays and sprinkler lines): it comes down out of the slab with an elbow and a sleeve, hangs on rod
    /// hangers and is snapped somewhere along its length - the supply side ends in a jagged torn end with the drop
    /// forming under it; the other side either hangs down at an angle from its last cracked coupling (the hanger at the
    /// break torn off, its clamp dangling) or is a torn stub with the missing piece lying on the floor. Around the break:
    /// a rust stain on the ceiling (and moss on the lower levels), below it a puddle on a wet halo (with moss). The
    /// spots avoid spawns and slab openings, and the fall line must be free (physics).
    /// </summary>
    internal sealed partial class ParkingGarageBuilder
    {
        /// <summary>z of the sprinkler lines of BuildCeilingServices.</summary>
        private static readonly float[] SprinklerZ = { -52f, -33f, -26f, -16f, 16f, 26f, 33f, 46.5f };

        private static readonly int[] BrokenPipesPerLevel = { 5, 6, 8 };

        // floor heights of the drip puddle layers (between the shared DecalY layers)
        private const float WetHaloY = 0.0075f, FloorMossY = 0.0125f, DripPuddleY = 0.0375f, DripSurfaceY = 0.052f;

        /// <summary>A pipe run along x at <paramref name="z"/> keeps clear of the beams, ducts, cable trays and sprinkler lines.</summary>
        private static bool PipeLaneClear(float z)
        {
            foreach (float s in SprinklerZ) if (Mathf.Abs(z - s) < 0.45f) return false;
            foreach (float b in ColZ) if (Mathf.Abs(z - b) < 0.45f) return false;
            if (Mathf.Abs(Mathf.Abs(z) - 24f) < 0.65f) return false;
            if (Mathf.Abs(Mathf.Abs(z) - 39.6f) < 0.5f) return false;
            return true;
        }

        private void BuildBrokenPipes(int lv, ChunkedBuilder ceil, ChunkedBuilder stains, ChunkedBuilder loose,
            List<CeilingDrips.Spot> spots, Transform markers)
        {
            var rnd = new System.Random(4421 + lv * 131);
            float y0 = FloorY(lv), c = CeilY(lv);
            var cands = new List<(float x, float z, float aisle)>();
            foreach (float az in AisleZ)
                foreach (float s in new[] { -1f, 1f })
                {
                    float z = az + s * 1.7f;
                    if (!PipeLaneClear(z)) continue;
                    for (float x = -66f; x <= 66f; x += 3f) cands.Add((x, z, az));
                }
            for (int i = cands.Count - 1; i > 0; i--)
            {
                int j = rnd.Next(i + 1);
                (cands[i], cands[j]) = (cands[j], cands[i]);
            }
            var holes = SlabHoles();
            var chosen = new List<Vector3>();
            foreach (var (x, z, aisle) in cands)
            {
                if (chosen.Count >= BrokenPipesPerLevel[lv]) break;
                var fp = new Vector3(x, y0, z);
                if (chosen.Any(q => (q - fp).sqrMagnitude < 14f * 14f)) continue;
                if (NearSpawn(fp + Vector3.up * 0.05f, 4f)) continue;
                if (holes.Any(h => x > h.xMin - 8f && x < h.xMax + 8f && z > h.yMin - 1.5f && z < h.yMax + 1.5f)) continue;
                // nothing in the fall line (a car pushed into the aisle, a prop)
                if (Physics.Raycast(new Vector3(x, c - 0.5f, z), Vector3.down, c - 0.5f - y0 - 0.06f, ~0, QueryTriggerInteraction.Ignore)) continue;
                chosen.Add(fp);
                var tip = BrokenPipe(lv, x, z, aisle, ceil, stains, loose, rnd);
                float interval = (lv == 2 ? 1.1f : 1.8f) + (float)rnd.NextDouble() * 2.2f;
                spots.Add(new CeilingDrips.Spot { Tip = tip, Surface = y0 + DripSurfaceY, Level = lv, Interval = interval });
                // marker for the drip ambience (Audio/AmbientEmitters looks for "PipeDrip_")
                var m = new GameObject($"PipeDrip_{Tag(lv)}_{chosen.Count}").transform;
                m.SetParent(markers, false);
                m.position = new Vector3(tip.x, y0 + 0.3f, tip.z);
            }
        }

        /// <summary>One broken run with its stains and puddle; returns where the drop hangs.</summary>
        private Vector3 BrokenPipe(int lv, float xb, float z, float aisleZ, ChunkedBuilder ceil, ChunkedBuilder stains,
            ChunkedBuilder loose, System.Random rnd)
        {
            float Rn(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            float y0 = FloorY(lv), c = CeilY(lv);
            const float r = 0.045f;
            float y = c - 0.36f;
            float dir = rnd.NextDouble() < 0.5 ? -1f : 1f;
            var along = new Vector3(dir, 0f, 0f);
            float xa = xb - dir * Rn(3.5f, 6.5f), xe = xb + dir * Rn(3f, 5f);
            var pm = M(lv == 0 ? "Metal_Galvanized" : lv == 1 ? "PM_Grey" : "Rust");
            var dark = M("Metal_Dark");

            // supply side: out of the slab, along the aisle to the break
            SlabRiser(ceil, new Vector3(xa, y, z), c, r, pm);
            ceil.Cylinder(new Vector3(xa, y, z), new Vector3(xb, y, z), r, 10, pm, false);
            TornEnd(ceil.At(new Vector3(xb, y, z)), new Vector3(xb, y, z), along, r, pm, rnd);
            PipeHangers(ceil, xa + dir * 0.9f, xb - dir * 0.6f, dir, y, z, c, r);

            // far side: into the slab again at xe
            SlabRiser(ceil, new Vector3(xe, y, z), c, r, pm);
            float side = Mathf.Sign(z - aisleZ);
            if (rnd.NextDouble() < 0.6)
            {
                // the loose piece hangs down at an angle from the cracked coupling at the last hanger that holds
                float xh = xb + dir * Rn(1.4f, 1.9f);
                ceil.Cylinder(new Vector3(xe, y, z), new Vector3(xh, y, z), r, 10, pm, false);
                PipeHangers(ceil, xh, xe - dir * 0.6f, dir, y, z, c, r);
                ceil.Cylinder(new Vector3(xh - dir * 0.05f, y, z), new Vector3(xh + dir * 0.05f, y, z), r * 1.25f, 10, pm);
                var hold = new Vector3(xh - dir * 0.04f, y - 0.01f, z);
                var end = new Vector3(xb + dir * Rn(0.2f, 0.4f), y - Rn(0.5f, 0.85f), z + Rn(-0.12f, 0.12f));
                ceil.At(hold).Cylinder(hold, end, r, 10, pm, false);
                TornEnd(ceil.At(end), end, (end - hold).normalized, r, pm, rnd);
            }
            else
            {
                // a torn stub; the missing piece lies on the floor beside the puddle
                float xs = xb + dir * Rn(1.2f, 1.8f);
                ceil.Cylinder(new Vector3(xe, y, z), new Vector3(xs, y, z), r, 10, pm, false);
                TornEnd(ceil.At(new Vector3(xs, y, z)), new Vector3(xs, y, z), -along, r, pm, rnd);
                PipeHangers(ceil, xs + dir * 0.5f, xe - dir * 0.6f, dir, y, z, c, r);
                float len = Mathf.Abs(xs - xb) - 0.15f;
                float ang = Rn(-25f, 25f);
                var f0 = new Vector3(xb + dir * 0.3f, y0 + r, z + side * Rn(0.55f, 0.85f));
                var f1 = f0 + Quaternion.Euler(0f, ang, 0f) * along * len;
                var lb = loose.At(f0);
                lb.Cylinder(f0, f1, r, 10, pm, false);
                TornEnd(lb, f0, (f0 - f1).normalized, r, pm, rnd);
                TornEnd(lb, f1, (f1 - f0).normalized, r, pm, rnd);
            }
            // the hanger at the break: rod torn off, the clamp hanging from it at an angle
            float xr = xb + dir * 0.45f;
            ceil.Cylinder(new Vector3(xr, c, z), new Vector3(xr, c - 0.17f, z), 0.008f, 4, dark, false);
            var cl = new Vector3(xr, c - 0.17f - r - 0.01f, z);
            var clAxis = new Vector3(0.45f * dir, 0.35f, 0.8f).normalized;
            ceil.At(cl).Cylinder(cl - clAxis * 0.022f, cl + clAxis * 0.022f, r + 0.01f, 10, dark);

            // stains around the break on the ceiling, a wet ring where the riser enters the slab
            CeilingDecal(stains, new Vector3(xb, c, z), 0.004f, Rn(0f, 180f), Rn(1.1f, 1.8f), Rn(1.4f, 2.4f), M("Stain_Rust"));
            CeilingDecal(stains, new Vector3(xa, c, z), 0.004f, Rn(0f, 180f), Rn(0.5f, 0.8f), Rn(0.6f, 1f), M("Stain_Wet"));
            if (lv > 0)
                CeilingDecal(stains, new Vector3(xb - dir * Rn(0.1f, 0.5f), c, z + Rn(-0.3f, 0.3f)), 0.007f, Rn(0f, 180f), Rn(0.5f, 0.9f), Rn(0.6f, 1.1f), M("Stain_Moss"));

            // the drop forms under the torn end of the supply side
            var tip = new Vector3(xb + dir * 0.02f, y - r - 0.004f, z);
            var fc = new Vector3(tip.x, y0, tip.z);
            Vector3 Dir() => new Vector3(Rn(-1f, 1f), 0f, Rn(-1f, 1f)) + Vector3.forward * 0.01f;
            float pw = Rn(1.0f, 1.6f), pl = Rn(1.3f, 2.3f);
            stains.FloorDecal(fc + Vector3.up * WetHaloY, Dir(), pw * 1.7f, pl * 1.6f, M("Stain_Wet"));
            if (lv > 0) stains.FloorDecal(fc + new Vector3(Rn(-0.3f, 0.3f), FloorMossY, Rn(-0.3f, 0.3f)), Dir(), pw * 1.3f, pl * 1.2f, M("Stain_Moss"));
            stains.FloorDecal(fc + Vector3.up * DripPuddleY, Dir(), pw, pl, M("Puddle"));
            return tip;
        }

        /// <summary>Rod hangers with clamps every ~1.8 m between <paramref name="x0"/> and <paramref name="x1"/>.</summary>
        private void PipeHangers(ChunkedBuilder b, float x0, float x1, float dir, float y, float z, float c, float r)
        {
            float len = (x1 - x0) * dir;
            if (len < 0f) return;
            int n = Mathf.Max(1, Mathf.RoundToInt(len / 1.8f));
            var dark = M("Metal_Dark");
            for (int i = 0; i <= n; i++)
            {
                float x = x0 + dir * len * i / n;
                b.Cylinder(new Vector3(x, c, z), new Vector3(x, y + r + 0.005f, z), 0.008f, 4, dark, false);
                b.Cylinder(new Vector3(x - 0.025f, y, z), new Vector3(x + 0.025f, y, z), r + 0.008f, 10, dark);
                b.Cylinder(new Vector3(x, c - 0.008f, z), new Vector3(x, c, z), 0.03f, 6, dark);
            }
        }

        /// <summary>
        /// Jagged end of a snapped open pipe at <paramref name="p"/>, pointing along <paramref name="d"/>: the wall
        /// runs on a little with a ragged, slightly flared edge, with a dark inside wall and a dark plug deeper in so
        /// the pipe does not look see-through.
        /// </summary>
        private void TornEnd(MeshBuilder mb, Vector3 p, Vector3 d, float r, Material m, System.Random rnd)
        {
            d.Normalize();
            var inner = M("Cavity_Dark");
            Vector3 e1 = Vector3.Cross(d, Mathf.Abs(d.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 e2 = Vector3.Cross(d, e1);
            const int n = 10;
            var ext = new float[n + 1];
            for (int i = 0; i < n; i++) ext[i] = (0.006f + 0.045f * (float)rnd.NextDouble()) * (rnd.NextDouble() < 0.2 ? 1.8f : 1f);
            ext[n] = ext[0];
            float ri = r * 0.8f, deep = 0.12f;
            Vector3 U(int i) { float a = i * Mathf.PI * 2f / n; return Mathf.Cos(a) * e1 + Mathf.Sin(a) * e2; }
            for (int i = 0; i < n; i++)
            {
                Vector3 u0 = U(i), u1 = U(i + 1);
                float f0 = 1f + ext[i] * 1.2f, f1 = 1f + ext[i + 1] * 1.2f; // the torn lip flares out a little
                Vector3 o0 = p + u0 * r, o1 = p + u1 * r;
                Vector3 O0 = p + u0 * r * f0 + d * ext[i], O1 = p + u1 * r * f1 + d * ext[i + 1];
                Vector3 I0 = p + u0 * ri * f0 + d * ext[i] * 0.9f, I1 = p + u1 * ri * f1 + d * ext[i + 1] * 0.9f;
                Vector3 i0 = p + u0 * ri - d * deep, i1 = p + u1 * ri - d * deep;
                mb.Quad(o0, o1, O1, O0, u0, u1, u1, u0, m);
                mb.Quad(O0, O1, I1, I0, d, d, d, d, m);
                mb.Quad(I0, I1, i1, i0, -u0, -u1, -u1, -u0, inner);
                mb.Tri(p - d * deep, i0, i1, d, d, d, inner);
            }
        }

        /// <summary>Down-facing decal under a ceiling at height <paramref name="c"/> (<paramref name="off"/> below it).</summary>
        private static void CeilingDecal(ChunkedBuilder b, Vector3 c, float off, float angle, float w, float l, Material m)
        {
            SurfaceDecal(b, new Vector3(c.x, c.y, c.z), Vector3.down, off, angle, w, l, m);
        }

        /// <summary>Decal quad (full 0..1 UVs) on a surface with outward normal <paramref name="n"/>.</summary>
        private static void SurfaceDecal(ChunkedBuilder b, Vector3 c, Vector3 n, float off, float angle, float w, float l, Material m)
        {
            Frame(n, angle, out var t1, out var t2);
            b.FaceUV(c + n * off, t1 * (w * 0.5f), t2 * (l * 0.5f), m, Vector2.zero, Vector2.one);
        }

        /// <summary>Tangents of a surface with normal <paramref name="n"/>, turned by <paramref name="angle"/> degrees; cross(t2, t1) = n.</summary>
        private static void Frame(Vector3 n, float angle, out Vector3 t1, out Vector3 t2)
        {
            n.Normalize();
            var t = Vector3.Cross(n, Mathf.Abs(n.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            t1 = Quaternion.AngleAxis(angle, n) * t;
            t2 = Vector3.Cross(t1, n);
        }

        /// <summary>Small water drop (about 1.4 x 2.4 cm, pivot at its top) for <see cref="CeilingDrips"/>.</summary>
        private Mesh DropMesh()
        {
            var mb = new MeshBuilder(_lib);
            var w = M("Drip_Water");
            mb.Cylinder(new Vector3(0f, -0.024f, 0f), new Vector3(0f, -0.017f, 0f), 0.002f, 8, w, true, 0.0068f);
            mb.Cylinder(new Vector3(0f, -0.017f, 0f), new Vector3(0f, -0.009f, 0f), 0.0068f, 8, w, false, 0.0055f);
            mb.Cylinder(new Vector3(0f, -0.009f, 0f), new Vector3(0f, 0f, 0f), 0.0055f, 8, w, true, 0.0008f);
            var m = mb.ToMesh("DripDrop");
            _store["Props"].Add(m);
            return m;
        }

        /// <summary>Flat ring (outer radius 1, inner 0.8, facing up) scaled by <see cref="CeilingDrips"/>.</summary>
        private Mesh RingMesh()
        {
            var mb = new MeshBuilder(_lib);
            var mat = M("Drip_Ring");
            const int n = 20;
            for (int i = 0; i < n; i++)
            {
                float a0 = i * Mathf.PI * 2f / n, a1 = (i + 1) * Mathf.PI * 2f / n;
                Vector3 u0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)), u1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                mb.Quad(u0 * 0.8f, u1 * 0.8f, u1, u0, Vector3.up, Vector3.up, Vector3.up, Vector3.up, mat);
            }
            var m = mb.ToMesh("DripRing");
            _store["Props"].Add(m);
            return m;
        }
    }
}
