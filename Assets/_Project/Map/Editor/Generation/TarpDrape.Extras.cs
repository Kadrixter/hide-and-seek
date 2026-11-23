using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// What goes with a draped cover: the canvas pulled back off the bonnet (cut + bunched roll), ropes over a tarp
    /// (pressing the cloth in), metal eyelets along the hem, bricks holding the hem on the floor, an old tyre on the roof.
    /// </summary>
    internal sealed partial class TarpDrape
    {
        /// <summary>Planes (z) of the ropes across the car; the cloth is pressed in along them.</summary>
        public readonly List<float> RopeZ = new List<float>();

        private bool Beyond(Vector3 p) => p.z > CutZ + CutSlope * (p.x - _o.x);

        /// <summary>Collapses everything in front of the cut onto the cut line (degenerate, invisible triangles).</summary>
        private void TarpCut()
        {
            CutLine.Clear();
            for (int j = 0; j < Cols; j++)
            {
                int k = -1;
                for (int i = 0; i < Rows; i++)
                    if (Beyond(P[i, j])) { k = i; break; }
                if (k < 0) continue;
                Vector3 c = P[k, j];
                if (k > 0)
                {
                    Vector3 a = P[k - 1, j], b = P[k, j];
                    float fa = a.z - (CutZ + CutSlope * (a.x - _o.x)), fb = b.z - (CutZ + CutSlope * (b.x - _o.x));
                    c = Vector3.Lerp(a, b, Mathf.Clamp01(fa / (fa - fb)));
                }
                for (int i = k; i < Rows; i++) P[i, j] = c;
                CutLine.Add(c);
            }
            for (int i = 0; i < Rows; i++) P[i, Cols] = P[i, 0];
        }

        /// <summary>Presses the cloth in along the rope planes (before the mesh is made).</summary>
        public void PinchRopes(float depth = 0.018f)
        {
            if (RopeZ.Count == 0) return;
            var n = Normals();
            for (int i = 1; i < Rows - 1; i++)
            for (int j = 0; j < Cols; j++)
                foreach (float z in RopeZ)
                {
                    float d = (P[i, j].z - z) / 0.09f;
                    P[i, j] -= n[i, j] * depth * Mathf.Exp(-d * d);
                }
            for (int i = 0; i < Rows; i++) P[i, Cols] = P[i, 0];
        }

        /// <summary>Highest point of the sheet (where a tyre would lie).</summary>
        public Vector3 Top()
        {
            Vector3 best = P[0, 0];
            for (int i = 0; i < Rows; i++)
            for (int j = 0; j < Cols; j++)
                if (P[i, j].y > best.y && (float.IsNaN(CutZ) || !Beyond(P[i, j] + Vector3.forward * 0.4f))) best = P[i, j];
            return best;
        }

        /// <summary>Eyelets every ~0.45 m just above the hem (blue tarp).</summary>
        public void Grommets(MeshBuilder mb, Material metal)
        {
            var n = Normals();
            int row = Rows - 2;
            float acc = 0f;
            for (int j = 1; j <= Cols; j++)
            {
                acc += Vector3.Distance(P[row, j - 1], P[row, j]);
                if (acc < 0.45f) continue;
                acc = 0f;
                Vector3 p = Vector3.Lerp(P[row, j], P[row - 1, j], 0.35f), nn = n[row, j];
                if (p.y < FloorRest + 0.02f) continue; // lying on the floor: hidden in the folds
                mb.Cylinder(p - nn * 0.002f, p + nn * 0.006f, 0.013f, 8, metal);
            }
        }

        /// <summary>Rope across the car in the plane z = <paramref name="z"/>, following the sheet down to the hem.</summary>
        public void Rope(MeshBuilder mb, MeshCollider cloth, float z, Material rope)
        {
            var pts = new List<Vector3>();
            var c = new Vector3(_o.x, 0.3f, z);
            for (int side = -1; side <= 1; side += 2)
            {
                var half = new List<Vector3>();
                // down the side with horizontal rays, then over the top with rays towards the centre
                for (float y = 0.05f; y < 0.75f; y += 0.06f)
                {
                    var from = new Vector3(_o.x + side * 3f, y, z);
                    if (cloth.Raycast(new Ray(ProbeOffset + from, Vector3.right * -side), out var h, 3f)) half.Add(h.point - ProbeOffset + h.normal * 0.012f);
                }
                for (float a = 10f; a <= 90f; a += 4f)
                {
                    float r = a * Mathf.Deg2Rad;
                    var dir = new Vector3(side * Mathf.Cos(r), Mathf.Sin(r), 0f);
                    if (cloth.Raycast(new Ray(ProbeOffset + c + dir * 3f, -dir), out var h, 3f) && h.point.y - ProbeOffset.y > 0.7f) half.Add(h.point - ProbeOffset + h.normal * 0.012f);
                }
                if (side < 0) pts.AddRange(half);
                else { half.Reverse(); pts.AddRange(half); }
            }
            for (int k = 1; k < pts.Count; k++)
                if (Vector3.Distance(pts[k - 1], pts[k]) < 0.35f)
                    mb.Cylinder(pts[k - 1], pts[k], 0.0075f, 6, rope);
            // knots at both ends
            if (pts.Count > 2)
            {
                mb.Box(pts[0] + Vector3.up * 0.01f, new Vector3(0.03f, 0.04f, 0.03f), rope, Quaternion.Euler(0f, 30f, 12f));
                mb.Box(pts[pts.Count - 1] + Vector3.up * 0.01f, new Vector3(0.03f, 0.04f, 0.03f), rope, Quaternion.Euler(0f, -20f, -10f));
            }
        }

        /// <summary>Bricks on the parts of the hem that lie on the floor (up to <paramref name="count"/>, far apart).</summary>
        public void Bricks(MeshBuilder mb, Material brick, int count)
        {
            var placed = new List<Vector3>();
            var cand = new List<Vector3>();
            for (int j = 0; j < Cols; j++)
            {
                Vector3 p = P[Rows - 1, j], q = P[Rows - 3, j];
                if (p.y <= FloorRest + 0.01f && q.y <= FloorRest + 0.05f) cand.Add(Vector3.Lerp(p, q, 0.5f));
            }
            for (int k = 0; k < cand.Count && placed.Count < count; k++)
            {
                var p = cand[(k * 7 + _rnd.Next(cand.Count)) % cand.Count];
                bool far = true;
                foreach (var q in placed) if (Vector3.Distance(p, q) < 1.4f) far = false;
                if (!far) continue;
                placed.Add(p);
                float yaw = (float)_rnd.NextDouble() * 180f;
                mb.Box(new Vector3(p.x, FloorRest + 0.0345f, p.z), new Vector3(0.215f, 0.065f, 0.103f), brick, Quaternion.Euler(0f, yaw, 0f));
                if (_rnd.NextDouble() < 0.5)
                    mb.Box(new Vector3(p.x + 0.02f, FloorRest + 0.1f, p.z + 0.01f), new Vector3(0.215f, 0.065f, 0.103f), brick, Quaternion.Euler(0f, yaw + 70f, 0f));
            }
        }

        /// <summary>Old tyre lying flat on the roof, pressing the sheet down.</summary>
        public void RoofTyre(MeshBuilder mb, Material rubber, Material hole)
        {
            Vector3 top = Top();
            Vector3 c = new Vector3(top.x + 0.05f, top.y - 0.03f, top.z - 0.15f);
            mb.Cylinder(c, c + Vector3.up * 0.19f, 0.3f, 18, rubber);
            mb.Cylinder(c + Vector3.up * 0.19f, c + Vector3.up * 0.191f, 0.19f, 18, hole);
            // sag of the sheet round it
            for (int i = 0; i < Rows; i++)
            for (int j = 0; j < Cols; j++)
            {
                float d = new Vector2(P[i, j].x - c.x, P[i, j].z - c.z).magnitude;
                if (d < 0.5f) P[i, j].y -= 0.025f * (1f - d / 0.5f);
            }
            for (int i = 0; i < Rows; i++) P[i, Cols] = P[i, 0];
        }

        /// <summary>The canvas bunched up along the cut: a fat, lumpy roll.</summary>
        public void Roll(MeshBuilder mb, Material canvas)
        {
            Vector3? prev = null;
            float acc = 0f;
            for (int k = 0; k < CutLine.Count; k++)
            {
                Vector3 p = CutLine[k];
                if (prev.HasValue && Vector3.Distance(prev.Value, p) < 0.07f && k < CutLine.Count - 1) continue;
                acc += prev.HasValue ? Vector3.Distance(prev.Value, p) : 0f;
                float r = 0.045f + 0.03f * Mathf.PerlinNoise(acc * 3.1f + _noiseSeed, 0.5f);
                Vector3 inward = new Vector3(_o.x - p.x, 0f, 0f).normalized * 0.01f;
                Vector3 q = p + Vector3.back * (r * 0.6f) + Vector3.up * (r * 0.5f) + inward;
                if (q.y < FloorRest + r) q.y = FloorRest + r;
                if (prev.HasValue) mb.Cylinder(prev.Value, q, r, 10, canvas);
                prev = q;
            }
        }
    }
}
