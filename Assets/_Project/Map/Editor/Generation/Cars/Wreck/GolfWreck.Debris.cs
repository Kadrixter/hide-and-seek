using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// Parts lying next to the wreck (right side and in front): the cracked front bumper, the ripped-off wheel, a double
    /// round headlamp, pieces of the GTI grille, glass crumbs, plastic chips, the steel tow cable with an old rope, an oil
    /// stain under the engine. Car-local floor coordinates.
    /// </summary>
    internal sealed partial class GolfWreck
    {
        private static readonly Vector3 BumperAt = new Vector3(2.05f, 0f, 0.45f), WheelAt = new Vector3(1.55f, 0f, -1.15f);

        private void Debris()
        {
            var rng = new System.Random(2002);
            Bumper(BumperAt, 78f);
            LooseWheel(WheelAt);
            Headlamp(new Vector3(1.42f, 0f, 2.12f), Quaternion.Euler(-8f, 140f, 22f));
            GrillePieces(rng);
            Chips(rng);
            TowCable();
            Stains();
        }

        private void DebrisColliders()
        {
            Pm.Colliders.Add(new Bounds(BumperAt + new Vector3(0f, 0.12f, 0f), new Vector3(0.6f, 0.24f, 1.6f)));
            Pm.Colliders.Add(new Bounds(WheelAt + new Vector3(0f, TireWidth * 0.5f, 0f), new Vector3(0.62f, TireWidth, 0.62f)));
        }

        // ------------------------------------------------------------------ bumper

        /// <summary>Front bumper cover lying on its back: swept shell along the plan curve, fog lamp holes, lower grille,
        /// GTI lip; the right corner broken off.</summary>
        private void Bumper(Vector3 at, float yaw)
        {
            int v0 = Mb.VertexCount;
            // plan curve (x, z) from the left corner round the nose to the (broken) right corner
            var plan = new List<Vector2>();
            for (int i = 0; i <= 36; i++)
            {
                float t = Mathf.Lerp(-1f, 0.82f, i / 36f);
                float x = t * 0.86f, ax = Mathf.Abs(t);
                float z = 0.1f - 0.1f * ax * ax - 0.22f * Mathf.Pow(WreckKit.Smooth(0.75f, 1f, ax), 1.5f);
                plan.Add(new Vector2(x, z));
            }
            // section (out, up): lower lip, face, shelf at the top, inner return
            var sec = new[] { new Vector2(-0.03f, 0f), new Vector2(0f, 0.02f), new Vector2(0.015f, 0.12f), new Vector2(0.02f, 0.24f),
                new Vector2(0.005f, 0.3f), new Vector2(-0.04f, 0.31f), new Vector2(-0.05f, 0.29f) };
            var P = new Vector3[plan.Count, sec.Length];
            for (int i = 0; i < plan.Count; i++)
            {
                Vector2 d = plan[Mathf.Min(i + 1, plan.Count - 1)] - plan[Mathf.Max(i - 1, 0)];
                Vector2 o = new Vector2(d.y, -d.x).normalized;      // outward (front / sides)
                for (int k = 0; k < sec.Length; k++)
                {
                    Vector2 q = plan[i] + o * sec[k].x;
                    P[i, k] = new Vector3(q.x, sec[k].y, q.y);
                }
            }
            var paint = Lib["Wreck_PaintPart"];
            var inner = Lib["Wreck_Plastic"];
            for (int i = 0; i < plan.Count - 1; i++)
            for (int k = 0; k < sec.Length - 1; k++)
            {
                // a chunk missing from the lower face near the right end
                if (i > 28 && k < 2) continue;
                Vector3 a = P[i, k], b = P[i + 1, k], c = P[i + 1, k + 1], dd = P[i, k + 1];
                Vector3 n = Vector3.Cross(b - a, dd - a).normalized;
                Vector3 mid = (a + c) * 0.5f;
                Vector3 outw = new Vector3(mid.x, 0f, mid.z - (-0.3f)).normalized;
                if (Vector3.Dot(n, outw) < 0f) n = -n;
                Mb.Quad(a, b, c, dd, n, n, n, n, paint);
                Mb.Quad(a - n * 0.004f, b - n * 0.004f, c - n * 0.004f, dd - n * 0.004f, -n, -n, -n, -n, inner);
            }
            // lower grille in the middle, fog lamp holes, number plate holder
            Mb.Box(new Vector3(0f, 0.08f, 0.115f), new Vector3(0.62f, 0.09f, 0.012f), Lib["Car_Grille"]);
            foreach (float s in new[] { -1f, 1f })
            {
                if (s > 0f) continue;
                CarWheels.Lathe(Mb, new Vector3(s * 0.58f, 0.1f, 0.075f), new Vector3(s * 0.25f, 0f, 1f), new[] { new Vector2(0.055f, -0.02f), new Vector2(0.055f, 0.004f), new Vector2(0f, 0.004f) }, 16, Seam);
                CarKit.RoundLamp(Mb, new Vector3(s * 0.58f, 0.1f, 0.08f), new Vector3(s * 0.25f, 0f, 1f), 0.04f, Lib["Car_ChromeBright"], Lens);
            }
            CarWheels.Lathe(Mb, new Vector3(0.58f, 0.1f, 0.072f), new Vector3(0.25f, 0f, 1f), new[] { new Vector2(0.055f, -0.02f), new Vector2(0.055f, 0.002f), new Vector2(0f, 0.002f) }, 16, Seam);
            Mb.Box(new Vector3(0f, 0.2f, 0.122f), new Vector3(0.54f, 0.13f, 0.01f), inner);
            // the bumper lies on its back, slightly twisted, at 'at'
            // set down standing on its lower lip, leaning back, the broken right end twisted down
            var rot = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(-14f, 0f, 0f);
            Mb.Warp(v0, p =>
            {
                float t = WreckKit.Smooth(0.3f, 0.75f, p.x);
                p = Quaternion.AngleAxis(-12f * t, Vector3.right) * p;
                return at + rot * p + Vector3.up * (0.004f + 0.02f * t);
            });
        }

        // ------------------------------------------------------------------ the ripped-off wheel

        private void LooseWheel(Vector3 at)
        {
            int v0 = Mb.VertexCount;
            CarWheels.Wheel(Mb, Vector3.zero, 1f, TireRadius, TireWidth, RimRadius, RimStyle.TenSpoke, Lib["Wreck_Rim"]);
            // torn-off rubber on the outer shoulder
            var rot = Quaternion.Euler(0f, 23f, 0f) * Quaternion.Euler(0f, 0f, 90f);
            Mb.Warp(v0, p => at + rot * p + Vector3.up * (TireWidth * 0.5f + 0.004f));
        }

        // ------------------------------------------------------------------ headlamp

        private void Headlamp(Vector3 at, Quaternion rot)
        {
            int v0 = Mb.VertexCount;
            var black = Lib["Wreck_Plastic"];
            CarBlob.Blob(Mb, new Vector3(0f, 0f, -0.07f), Quaternion.identity, new Vector3(0.21f, 0.065f, 0.07f), 0.25f, black, 0.3f, 1f, 16, 8);
            Mb.Box(new Vector3(0f, 0f, 0.005f), new Vector3(0.42f, 0.12f, 0.01f), Lib["Car_Reflector"]);
            foreach (var (x, r) in new[] { (-0.09f, 0.054f), (0.1f, 0.05f) })
                CarKit.RoundLamp(Mb, new Vector3(x, 0f, 0.012f), Vector3.forward, r, Lib["Car_ChromeBright"], Lens);
            Mb.Box(new Vector3(0.19f, 0f, 0.012f), new Vector3(0.05f, 0.08f, 0.012f), Amber);
            // cracked cover: two shards still in the frame
            Mb.Tri(new Vector3(-0.2f, 0.06f, 0.035f), new Vector3(0.02f, 0.06f, 0.035f), new Vector3(-0.2f, -0.04f, 0.035f), Vector3.forward, Vector3.forward, Vector3.forward, Glass);
            Mb.Tri(new Vector3(0.21f, -0.06f, 0.035f), new Vector3(0.05f, -0.06f, 0.035f), new Vector3(0.21f, 0.03f, 0.035f), Vector3.forward, Vector3.forward, Vector3.forward, Glass);
            // torn wires from the back
            WreckKit.Tube(Mb, WreckKit.Spline(new[] { new Vector3(0.05f, 0f, -0.13f), new Vector3(0.1f, -0.03f, -0.25f), new Vector3(0.2f, -0.06f, -0.3f) }), 0.004f, Lib["Wreck_WireYellow"], 5);
            WreckKit.Tube(Mb, WreckKit.Spline(new[] { new Vector3(0.03f, 0.01f, -0.13f), new Vector3(0.0f, -0.04f, -0.24f), new Vector3(-0.05f, -0.06f, -0.33f) }), 0.004f, Lib["Car_Dash"], 5);
            Mb.Warp(v0, p => at + rot * p + Vector3.up * 0.07f);
            var rng = new System.Random(66);
            for (int i = 0; i < 7; i++)
                WreckKit.Chip(Mb, at + new Vector3((float)rng.NextDouble() * 0.5f - 0.1f, 0.002f, (float)rng.NextDouble() * 0.4f - 0.3f), Vector3.up, 0.02f + (float)rng.NextDouble() * 0.025f, 0.003f, Glass, rng, 4);
        }

        // ------------------------------------------------------------------ grille pieces, chips, glass

        private void GrillePieces(System.Random rng)
        {
            // the right half of the grille with the GTI badge (red letters on chrome, red line along the top)
            var at = new Vector3(1.15f, 0.012f, 2.3f);
            var rot = Quaternion.Euler(-90f + 6f, 205f, 0f);
            int v0 = Mb.VertexCount;
            Mb.Box(Vector3.zero, new Vector3(0.32f, 0.09f, 0.02f), Lib["Wreck_Plastic"]);
            Mb.Box(new Vector3(0f, 0f, 0.008f), new Vector3(0.3f, 0.072f, 0.008f), Lib["Car_Grille"]);
            Mb.Box(new Vector3(0f, 0.04f, 0.012f), new Vector3(0.31f, 0.008f, 0.006f), Lib["Wreck_BadgeRed"]);
            Mb.Box(new Vector3(0f, 0.05f, 0.011f), new Vector3(0.31f, 0.008f, 0.005f), Lib["Car_ChromeBright"]);
            Mb.Box(new Vector3(0.06f, 0f, 0.016f), new Vector3(0.1f, 0.03f, 0.006f), Lib["Car_ChromeBright"]);
            Mb.Text("GTI", new Vector3(0.06f, 0f, 0.0195f), Vector3.forward, Vector3.up, 0.024f, Lib["Wreck_TextRed"]);
            CarBadges.Vw(Mb, new Vector3(-0.16f, 0.005f, 0.014f), Vector3.forward, 0.05f);
            Mb.Warp(v0, p => at + rot * p);
            // honeycomb chunks
            for (int i = 0; i < 3; i++)
            {
                var c = new Vector3(0.95f + i * 0.28f, 0.006f, 1.95f + (float)rng.NextDouble() * 0.5f);
                int c0 = Mb.VertexCount;
                Mb.Box(Vector3.zero, new Vector3(0.07f + (float)rng.NextDouble() * 0.08f, 0.012f, 0.05f + (float)rng.NextDouble() * 0.05f), Lib["Car_Grille"]);
                float yaw = (float)rng.NextDouble() * 360f;
                Mb.Warp(c0, p => c + Quaternion.Euler(0f, yaw, 0f) * p);
            }
        }

        private void Chips(System.Random rng)
        {
            var black = Lib["Wreck_Plastic"];
            var grey = Lib["Wreck_PlasticGrey"];
            var bits = Lib["Wreck_GlassBits"];
            for (int i = 0; i < 46; i++)
            {
                // most in front and along the right side
                bool front = i % 3 != 0;
                var c = front
                    ? new Vector3(-0.8f + (float)rng.NextDouble() * 2.6f, 0f, 2.0f + (float)rng.NextDouble() * 0.7f)
                    : new Vector3(0.95f + (float)rng.NextDouble() * 1.6f, 0f, -0.5f + (float)rng.NextDouble() * 2.4f);
                float s = 0.012f + (float)rng.NextDouble() * (i % 5 == 0 ? 0.06f : 0.025f);
                var m = i % 9 == 0 ? Amber : i % 4 == 0 ? grey : black;
                var n = (Vector3.up + new Vector3((float)rng.NextDouble() - 0.5f, 0f, (float)rng.NextDouble() - 0.5f) * 0.3f).normalized;
                WreckKit.Chip(Mb, c + Vector3.up * 0.0025f, n, s, 0.003f, m, rng, 4 + rng.Next(3));
            }
            // tempered glass crumbs: under the right front door and spread in front
            WreckKit.Crumbs(Mb, new Vector3(1.05f, 0f, 0.35f), Vector3.up, 0.35f, 140, bits, rng);
            WreckKit.Crumbs(Mb, new Vector3(1.0f, 0f, 1.2f), Vector3.up, 0.25f, 60, bits, rng);
            WreckKit.Crumbs(Mb, new Vector3(0.3f, 0f, 2.25f), Vector3.up, 0.45f, 90, bits, rng);
            WreckKit.Crumbs(Mb, new Vector3(-1.1f, 0f, 0.5f), Vector3.up, 0.2f, 25, bits, rng);
        }

        // ------------------------------------------------------------------ tow cable and rope

        private void TowCable()
        {
            const float y = 0.007f;
            Vector3 eye = Pose(Crush(new Vector3(-0.42f, 0.27f, 1.98f)));
            var pts = new List<Vector3> { eye, eye + new Vector3(0.02f, -0.15f, 0.12f), new Vector3(-0.35f, y, 2.35f), new Vector3(-0.05f, y, 2.62f),
                new Vector3(0.45f, y, 2.55f), new Vector3(0.62f, y, 2.25f), new Vector3(0.3f, y, 2.12f), new Vector3(0.05f, y, 2.4f),
                new Vector3(0.4f, y, 2.75f), new Vector3(1.1f, y, 2.7f), new Vector3(1.6f, y, 2.45f), new Vector3(1.85f, y, 2.62f) };
            var path = WreckKit.Spline(pts, 0.012f);
            // twisted steel rope: two strands round the path
            for (int s = 0; s < 2; s++)
            {
                var strand = new List<Vector3>();
                for (int i = 0; i < path.Count; i++)
                {
                    Vector3 t = (path[Mathf.Min(i + 1, path.Count - 1)] - path[Mathf.Max(i - 1, 0)]).normalized;
                    Vector3 a = Vector3.Cross(t, Vector3.up).normalized, b = Vector3.Cross(t, a);
                    float ang = i * 0.9f + s * Mathf.PI;
                    strand.Add(path[i] + (a * Mathf.Cos(ang) + b * Mathf.Sin(ang)) * 0.0035f + Vector3.up * 0.002f);
                }
                WreckKit.Tube(Mb, strand, 0.0038f, Lib["Wreck_Cable"], 6);
            }
            // eye at the loose end, frayed wires
            var end = pts[pts.Count - 1];
            var loop = new List<Vector3>();
            for (int i = 0; i <= 16; i++)
            {
                float a = i / 16f * Mathf.PI * 2f;
                loop.Add(end + new Vector3(0.06f + Mathf.Cos(a) * 0.05f, 0.006f, Mathf.Sin(a) * 0.035f));
            }
            WreckKit.Tube(Mb, loop, 0.005f, Lib["Wreck_Cable"], 6);
            var rng = new System.Random(19);
            for (int i = 0; i < 6; i++)
                Mb.Cylinder(pts[2] + Vector3.up * 0.003f, pts[2] + new Vector3((float)rng.NextDouble() * 0.12f - 0.06f, 0.004f, (float)rng.NextDouble() * 0.1f), 0.0008f, 3, Lib["Wreck_Cable"]);
            // an old orange-brown rope beside it
            var rope = WreckKit.Spline(new[] { new Vector3(0.7f, 0.006f, 1.98f), new Vector3(1.0f, 0.006f, 2.35f), new Vector3(1.4f, 0.006f, 2.2f),
                new Vector3(1.7f, 0.006f, 2.5f), new Vector3(2.1f, 0.006f, 2.4f) }, 0.015f);
            WreckKit.Tube(Mb, rope, 0.006f, Lib["Wreck_Rope"], 6);
        }

        private void Stains()
        {
            var oil = Lib["Oil_Stain"];
            Mb.FloorDecal(new Vector3(0.05f, 0.009f, 1.45f), new Vector3(0.3f, 0f, 1f), 1.1f, 0.9f, oil);
            Mb.FloorDecal(new Vector3(0.6f, 0.0095f, 1.65f), new Vector3(-0.6f, 0f, 1f), 0.55f, 0.45f, oil);
        }
    }
}
