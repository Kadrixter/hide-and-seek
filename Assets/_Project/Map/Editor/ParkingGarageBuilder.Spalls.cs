using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// Where the rubble came from: above every rubble pile ("Rubble_" props: the construction heaps, the collapsed
    /// area, the fallen heaps of RubbleRods) the concrete is spalled - on the ceiling, or on the wall when the pile lies
    /// against one. A ragged rim of broken concrete round a dark cavity, its broken faces lighter (fresh concrete), the
    /// reinforcement grid exposed with a few bars bent out of it (on the ceiling one with a lump of concrete still
    /// hanging on it), chips stuck in the rim, cracks running out of it and a dust stain. Sized to the pile; the patch
    /// moves off the beams, ducts and cable trays. Deterministic per pile name.
    /// </summary>
    internal sealed partial class ParkingGarageBuilder
    {
        /// <summary>Spalled patches over the rubble piles of level <paramref name="lv"/>; returns how many.</summary>
        private int BuildRubbleSpalls(int lv, ChunkedBuilder ceil, ChunkedBuilder stains)
        {
            float c = CeilY(lv);
            var piles = _tPropsLv[lv].Cast<Transform>().Where(t => t.name.StartsWith("Rubble_")).ToList();
            int count = 0;
            foreach (var pile in piles)
            {
                var rnd = new System.Random(StableHash("spall" + pile.name));
                var mr = pile.GetComponent<MeshRenderer>();
                if (mr == null) continue;
                var ext = mr.bounds.extents;
                float size = Mathf.Clamp(Mathf.Max(ext.x, ext.z) * 0.75f, 0.45f, 1.5f);
                Vector3 p = pile.position;
                if (WallBehind(p, size, out var wall))
                {
                    var n = new Vector3(wall.normal.x, 0f, wall.normal.z).normalized;
                    var wc = new Vector3(wall.point.x, p.y + 1.3f + (float)rnd.NextDouble() * 0.6f, wall.point.z);
                    Spall(ceil, stains, wc, n, size * 0.8f, M(lv == 2 ? "Wall_Concrete_Old" : "Wall_Concrete"), rnd, false);
                }
                else
                {
                    var cc = new Vector3(p.x + ((float)rnd.NextDouble() - 0.5f) * 0.5f, c, p.z + ((float)rnd.NextDouble() - 0.5f) * 0.5f);
                    cc.z = ClearOfCeilingRuns(cc.z, size);
                    Spall(ceil, stains, cc, Vector3.down, size, CeilMat(lv), rnd, true);
                }
                count++;
            }
            return count;
        }

        /// <summary>A wall (merged "Walls_" chunk) within reach of the pile, probed at 1 m height in the four directions.</summary>
        private static bool WallBehind(Vector3 p, float size, out RaycastHit best)
        {
            best = default;
            float bestD = float.MaxValue;
            foreach (var d in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
            {
                if (!Physics.Raycast(p + Vector3.up * 1f, d, out var h, size + 0.9f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (!h.collider.name.StartsWith("Walls_") || Mathf.Abs(h.normal.y) > 0.3f) continue;
                // a real wall goes on up (not a fallen slab lying in the collapsed area)
                if (!Physics.Raycast(p + Vector3.up * 2.3f, d, out var hi, h.distance + 0.3f, ~0, QueryTriggerInteraction.Ignore) ||
                    !hi.collider.name.StartsWith("Walls_") || Mathf.Abs(hi.normal.y) > 0.3f) continue;
                if (h.distance < bestD) { bestD = h.distance; best = h; }
            }
            return bestD < float.MaxValue;
        }

        /// <summary>Moves a ceiling patch of radius <paramref name="size"/> at <paramref name="z"/> off the beams, ducts and trays.</summary>
        private static float ClearOfCeilingRuns(float z, float size)
        {
            var runs = ColZ.Select(b => (b, 0.25f)).Concat(new[] { (-24f, 0.45f), (24f, 0.45f), (-39.6f, 0.3f), (39.6f, 0.3f) }).ToList();
            for (int pass = 0; pass < 3; pass++)
                foreach (var (oz, half) in runs)
                {
                    float need = half + size * 1.05f;
                    if (Mathf.Abs(z - oz) < need) z = oz + (z >= oz ? need : -need);
                }
            return z;
        }

        /// <summary>
        /// One spalled patch centred at <paramref name="c"/> on a surface with outward normal <paramref name="n"/>
        /// (Vector3.down for a ceiling). <paramref name="hangingLump"/>: a lump of concrete dangles on a bent bar.
        /// </summary>
        private void Spall(ChunkedBuilder geo, ChunkedBuilder decals, Vector3 c, Vector3 n, float size, Material surface,
            System.Random rnd, bool hangingLump)
        {
            float Rn(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            Frame(n, Rn(0f, 360f), out var t1, out var t2);
            var mb = geo.At(c);
            var fresh = M("Concrete_Fresh");
            var dark = M("Cavity_Dark");
            var rust = M("Rust");
            const int k = 16;
            var rad = new float[k + 1];
            var lip = new float[k + 1];
            for (int i = 0; i < k; i++)
            {
                rad[i] = size * Rn(0.7f, 1.12f);
                lip[i] = (0.04f + 0.035f * size) * Rn(0.5f, 1.2f);
            }
            rad[k] = rad[0];
            lip[k] = lip[0];
            Vector3 U(int i) { float a = i * Mathf.PI * 2f / k; return Mathf.Cos(a) * t1 + Mathf.Sin(a) * t2; }
            Vector3 P(Vector3 u, float r, float depth) => c + u * r + n * depth;
            for (int i = 0; i < k; i++)
            {
                Vector3 u0 = U(i), u1 = U(i + 1);
                // outer slope: from flush with the surface up to the broken rim
                Vector3 o0 = P(u0, rad[i] * 1.3f, 0.001f), o1 = P(u1, rad[i + 1] * 1.3f, 0.001f);
                Vector3 r0 = P(u0, rad[i], lip[i]), r1 = P(u1, rad[i + 1], lip[i + 1]);
                Vector3 s0 = (n + u0 * 0.5f).normalized, s1 = (n + u1 * 0.5f).normalized;
                mb.Quad(o0, o1, r1, r0, s0, s1, s1, s0, surface);
                // broken inner face (fresh concrete) down into the cavity
                Vector3 q0 = P(u0, rad[i] * 0.82f, 0.006f), q1 = P(u1, rad[i + 1] * 0.82f, 0.006f);
                Vector3 w0 = (n * 0.35f - u0).normalized, w1 = (n * 0.35f - u1).normalized;
                mb.Quad(r0, r1, q1, q0, w0, w1, w1, w0, fresh);
                // cavity floor
                mb.Tri(c + n * 0.004f, q0, q1, n, n, n, dark);
            }
            // chips stuck in the rim
            int chips = 3 + rnd.Next(4);
            for (int i = 0; i < chips; i++)
            {
                int j = rnd.Next(k);
                var u = U(j);
                float s = Rn(0.05f, 0.13f) * Mathf.Min(1f, size + 0.3f);
                mb.Box(P(u, rad[j] * Rn(0.95f, 1.1f), lip[j] * 0.6f), new Vector3(s, s * Rn(0.5f, 0.9f), s * Rn(0.6f, 1.2f)), fresh,
                    Quaternion.LookRotation(u, n) * Quaternion.Euler(Rn(-30f, 30f), Rn(0f, 90f), Rn(-30f, 30f)));
            }
            // reinforcement grid exposed in the cavity (two layers of bars)
            const float bar = 0.006f;
            float reach = size * 0.78f;
            foreach (var (a, b, depth) in new[] { (t1, t2, 0.016f), (t2, t1, 0.026f) })
                for (float s = -reach + 0.12f; s < reach; s += 0.15f)
                {
                    float half = Mathf.Sqrt(Mathf.Max(0f, reach * reach - s * s));
                    if (half < 0.05f) continue;
                    mb.Cylinder(c + b * s - a * half + n * depth, c + b * s + a * half + n * depth, bar, 4, rust, false);
                }
            // bars bent out of the grid, the free ends rusted off
            int bent = 2 + (size > 0.9f ? 1 : 0);
            for (int i = 0; i < bent; i++)
            {
                var u = U(rnd.Next(k));
                var root = c + u * (reach * Rn(0.2f, 0.7f)) + n * 0.016f;
                var knee = root + u * Rn(0.05f, 0.2f) + n * Rn(0.03f, 0.08f);
                var tip = knee + (n * Rn(0.8f, 1f) + u * Rn(0.1f, 0.6f)).normalized * Rn(0.15f, 0.45f);
                mb.Cylinder(root, knee, bar, 4, rust, false);
                mb.Cylinder(knee, tip, bar, 4, rust);
                if (hangingLump && i == 0 && size > 0.55f && rnd.NextDouble() < 0.75)
                {
                    float l = Rn(0.12f, 0.2f);
                    mb.Box(tip + n * l * 0.3f, new Vector3(l, l * 0.6f, l * 0.8f), fresh, Quaternion.LookRotation(u, n) * Quaternion.Euler(Rn(-25f, 25f), Rn(0f, 90f), Rn(-25f, 25f)));
                }
            }
            // cracks running out of it and the dust the fall left
            SurfaceDecal(decals, c, n, 0.002f, Rn(0f, 360f), size * 3.2f, size * 3f, M("Stain_Dust"));
            for (int i = 0; i < 3; i++)
            {
                float a = Rn(0f, Mathf.PI * 2f);
                var off = (Mathf.Cos(a) * t1 + Mathf.Sin(a) * t2) * size * Rn(0.8f, 1.2f);
                float s = size * Rn(1.6f, 2.6f);
                SurfaceDecal(decals, c + off, n, 0.0035f + i * 0.0004f, Rn(0f, 360f), s, s, M("Crack"));
            }
        }
    }
}
