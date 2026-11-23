using UnityEngine;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// Parked two-wheelers (length along +z, floor at y = 0): a naked motorbike (round headlamp, teardrop tank,
    /// engine with fins, exhaust, disc brake, tubular frame, swingarm) and a city bicycle (thin tyres with spokes,
    /// tube frame, mudguards, chain ring, saddle, upright bar). Light meshes, welded by the factory.
    /// </summary>
    internal static class TwoWheelers
    {
        // ------------------------------------------------------------------ shared bits

        private static void Tube(MeshBuilder mb, Vector3 a, Vector3 b, float r, Material m, int sides = 8) => mb.Cylinder(a, b, r, sides, m);

        /// <summary>Arc band (mudguard) round the x axis at c: radius r, half width hw, angles a0..a1 (0 = +z, 90 = up).</summary>
        private static void Arc(MeshBuilder mb, Vector3 c, float r, float hw, float a0, float a1, Material m, int n = 12)
        {
            for (int i = 0; i < n; i++)
            {
                float t0 = Mathf.Lerp(a0, a1, i / (float)n) * Mathf.Deg2Rad, t1 = Mathf.Lerp(a0, a1, (i + 1) / (float)n) * Mathf.Deg2Rad;
                Vector3 d0 = new Vector3(0f, Mathf.Sin(t0), Mathf.Cos(t0)), d1 = new Vector3(0f, Mathf.Sin(t1), Mathf.Cos(t1));
                Vector3 x = Vector3.right * hw;
                mb.Quad(c + d0 * r - x, c + d0 * r + x, c + d1 * r + x, c + d1 * r - x, d0, d0, d1, d1, m);
                mb.Quad(c + d0 * (r - 0.004f) + x, c + d0 * (r - 0.004f) - x, c + d1 * (r - 0.004f) - x, c + d1 * (r - 0.004f) + x, -d0, -d0, -d1, -d1, m);
            }
        }

        private static void Wheel(MeshBuilder mb, Vector3 c, float r, float hw, float rimR, Material rim, int spokes, float spokeW, bool disc)
        {
            var lib = mb.Lib;
            CarWheels.Lathe(mb, c, Vector3.right, new[]
            {
                new Vector2(rimR, -hw * 0.6f), new Vector2(rimR + (r - rimR) * 0.45f, -hw), new Vector2(r - hw * 0.35f, -hw * 0.8f),
                new Vector2(r, 0f), new Vector2(r - hw * 0.35f, hw * 0.8f), new Vector2(rimR + (r - rimR) * 0.45f, hw), new Vector2(rimR, hw * 0.6f),
            }, 18, lib["Rubber"], 60f);
            CarWheels.Lathe(mb, c, Vector3.right, new[]
            {
                new Vector2(rimR - 0.015f, -hw * 0.5f), new Vector2(rimR + 0.004f, -hw * 0.55f), new Vector2(rimR + 0.004f, hw * 0.55f), new Vector2(rimR - 0.015f, hw * 0.5f),
                new Vector2(rimR - 0.015f, -hw * 0.5f),
            }, 18, rim, 30f);
            Tube(mb, c - Vector3.right * hw * 0.7f, c + Vector3.right * hw * 0.7f, Mathf.Max(0.025f, rimR * 0.12f), lib["Metal_Dark"]);
            for (int i = 0; i < spokes; i++)
            {
                float a = i * Mathf.PI * 2f / spokes;
                Vector3 d = new Vector3(0f, Mathf.Sin(a), Mathf.Cos(a));
                Vector3 side = Vector3.right * ((i % 2 == 0 ? 1f : -1f) * hw * 0.3f);
                Vector3 p0 = c + side + d * 0.03f, p1 = c + d * (rimR - 0.01f);
                mb.Box((p0 + p1) * 0.5f, new Vector3(spokeW, spokeW, (p1 - p0).magnitude), rim, Quaternion.LookRotation(p1 - p0, Vector3.right));
            }
            if (disc)
                CarWheels.Lathe(mb, c + Vector3.right * (hw + 0.012f), Vector3.right, new[] { new Vector2(rimR * 0.55f, -0.003f), new Vector2(rimR * 0.55f, 0.003f), new Vector2(rimR * 0.3f, 0.003f) }, 16, lib["Car_ChromeBright"]);
        }

        // ------------------------------------------------------------------ motorbike

        public static void Motorbike(MeshBuilder mb, PropMesh pm, Material paint)
        {
            var lib = mb.Lib;
            var chrome = lib["Car_ChromeBright"];
            var dark = lib["Car_Trim"];
            var metal = lib["Metal_Dark"];
            Vector3 fa = new Vector3(0f, 0.31f, 0.72f), ra = new Vector3(0f, 0.31f, -0.7f);
            Wheel(mb, fa, 0.31f, 0.055f, 0.22f, metal, 10, 0.016f, true);
            Wheel(mb, ra, 0.31f, 0.075f, 0.22f, metal, 10, 0.018f, false);
            Arc(mb, fa, 0.345f, 0.06f, 20f, 115f, paint, 8);
            // fork (raked), steering head, handlebar with grips and mirrors
            Vector3 head = new Vector3(0f, 0.98f, 0.5f);
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 top = head + new Vector3(s * 0.075f, 0.05f, 0.03f), bot = fa + new Vector3(s * 0.075f, 0.02f, 0f);
                Tube(mb, top, Vector3.Lerp(top, bot, 0.55f), 0.022f, chrome);
                Tube(mb, Vector3.Lerp(top, bot, 0.55f), bot, 0.028f, dark);
            }
            Tube(mb, head + new Vector3(0f, -0.08f, 0.02f), head + new Vector3(0f, 0.1f, -0.02f), 0.04f, dark);
            Vector3 bar = head + new Vector3(0f, 0.12f, -0.04f);
            Tube(mb, bar + new Vector3(-0.36f, 0.04f, -0.06f), bar + new Vector3(0.36f, 0.04f, -0.06f), 0.012f, chrome);
            foreach (float s in new[] { -1f, 1f })
            {
                Tube(mb, bar + new Vector3(s * 0.26f, 0.04f, -0.06f), bar + new Vector3(s * 0.38f, 0.04f, -0.07f), 0.018f, lib["Rubber"]);
                Tube(mb, bar + new Vector3(s * 0.22f, 0.04f, -0.06f), bar + new Vector3(s * 0.27f, 0.22f, -0.02f), 0.006f, chrome, 6);
                CarWheels.Lathe(mb, bar + new Vector3(s * 0.28f, 0.24f, -0.02f), Vector3.back, new[] { new Vector2(0.045f, -0.015f), new Vector2(0.045f, 0.004f), new Vector2(0f, 0.006f) }, 12, dark);
            }
            // round headlamp and its shell, small speedo
            Vector3 lamp = head + new Vector3(0f, -0.06f, 0.13f);
            CarWheels.Lathe(mb, lamp, Vector3.forward, new[] { new Vector2(0f, -0.1f), new Vector2(0.07f, -0.08f), new Vector2(0.085f, -0.02f), new Vector2(0.085f, 0f) }, 16, dark);
            CarKit.RoundLamp(mb, lamp, Vector3.forward, 0.075f, chrome, lib["Car_Lens"]);
            CarWheels.Lathe(mb, head + new Vector3(0f, 0.1f, 0.06f), new Vector3(0f, 0.6f, -0.8f), new[] { new Vector2(0.045f, -0.03f), new Vector2(0.045f, 0f), new Vector2(0f, 0.002f) }, 12, dark);
            // frame: backbone, down tubes, rear subframe; swingarm to the rear axle
            Vector3 pivot = new Vector3(0f, 0.45f, -0.18f);
            Tube(mb, head + new Vector3(0f, -0.02f, -0.04f), new Vector3(0f, 0.86f, -0.3f), 0.03f, dark);
            foreach (float s in new[] { -1f, 1f })
            {
                Tube(mb, head + new Vector3(s * 0.03f, -0.08f, -0.02f), new Vector3(s * 0.08f, 0.28f, 0.25f), 0.022f, dark);
                Tube(mb, new Vector3(s * 0.08f, 0.28f, 0.25f), pivot + new Vector3(s * 0.09f, -0.12f, 0.02f), 0.022f, dark);
                Tube(mb, pivot + new Vector3(s * 0.1f, 0f, 0f), ra + new Vector3(s * 0.1f, 0f, 0f), 0.026f, metal);
                Tube(mb, new Vector3(s * 0.08f, 0.82f, -0.25f), new Vector3(s * 0.07f, 0.8f, -0.78f), 0.016f, dark);
                Tube(mb, pivot + new Vector3(s * 0.09f, 0.05f, 0f), new Vector3(s * 0.08f, 0.82f, -0.42f), 0.018f, dark);
                // rear shock absorbers with chrome springs
                Tube(mb, ra + new Vector3(s * 0.11f, 0.05f, 0.02f), new Vector3(s * 0.1f, 0.8f, -0.55f), 0.022f, chrome);
            }
            // engine: crankcase, finned cylinder, side covers
            mb.Box(new Vector3(0f, 0.36f, 0.02f), new Vector3(0.26f, 0.2f, 0.36f), metal);
            CarWheels.Lathe(mb, new Vector3(0f, 0.36f, 0.02f), Vector3.right, new[] { new Vector2(0f, 0.135f), new Vector2(0.11f, 0.135f), new Vector2(0.11f, 0.12f) }, 14, chrome);
            mb.Box(new Vector3(0f, 0.6f, 0.15f), new Vector3(0.13f, 0.26f, 0.12f), metal, Quaternion.Euler(-12f, 0, 0));
            for (int i = 0; i < 6; i++)
                mb.Box(new Vector3(0f, 0.5f + i * 0.035f, 0.13f + i * 0.0075f), new Vector3(0.2f - i * 0.006f, 0.012f, 0.17f), metal, Quaternion.Euler(-12f, 0, 0));
            // exhaust: header down from the cylinder, along the right side to the silencer
            Vector3 e0 = new Vector3(0.05f, 0.55f, 0.24f), e1 = new Vector3(0.1f, 0.25f, 0.26f), e2 = new Vector3(0.16f, 0.24f, -0.05f), e3 = new Vector3(0.17f, 0.34f, -0.45f);
            Tube(mb, e0, e1, 0.022f, chrome); Tube(mb, e1, e2, 0.022f, chrome); Tube(mb, e2, e3, 0.022f, chrome);
            CarWheels.Lathe(mb, e3, new Vector3(0f, 0.1f, -1f), new[] { new Vector2(0.03f, 0f), new Vector2(0.05f, 0.04f), new Vector2(0.05f, 0.34f), new Vector2(0.035f, 0.37f), new Vector2(0f, 0.37f) }, 12, chrome);
            // teardrop tank, seat, tail with lamp and plate, side covers in paint
            CarBlob.Blob(mb, new Vector3(0f, 0.93f, 0.2f), Quaternion.Euler(8f, 0, 0), new Vector3(0.15f, 0.1f, 0.25f), 0.55f, paint, 0.7f, 0.75f, 12, 8);
            CarBlob.Blob(mb, new Vector3(0f, 0.87f, -0.33f), Quaternion.Euler(-3f, 0, 0), new Vector3(0.13f, 0.05f, 0.29f), 0.35f, lib["Car_LeatherBlack"], 0.4f, 1f, 10, 6);
            CarBlob.Blob(mb, new Vector3(0f, 0.84f, -0.7f), Quaternion.identity, new Vector3(0.1f, 0.06f, 0.13f), 0.4f, paint, 0.5f, 1f, 10, 6);
            mb.Box(new Vector3(0f, 0.83f, -0.835f), new Vector3(0.1f, 0.04f, 0.02f), lib["Car_TailRed"]);
            mb.Box(new Vector3(0f, 0.7f, -0.86f), new Vector3(0.2f, 0.12f, 0.006f), lib["Car_Plate"], Quaternion.Euler(-15f, 0, 0));
            Arc(mb, ra, 0.35f, 0.08f, 95f, 170f, dark, 8);
            foreach (float s in new[] { -1f, 1f })
            {
                mb.Box(new Vector3(s * 0.085f, 0.7f, -0.3f), new Vector3(0.02f, 0.18f, 0.24f), paint);
                mb.Box(new Vector3(s * 0.1f, 0.82f, -0.8f), new Vector3(0.04f, 0.03f, 0.05f), lib["Car_LensAmber"]);
                // footpegs
                Tube(mb, new Vector3(s * 0.12f, 0.33f, -0.12f), new Vector3(s * 0.2f, 0.33f, -0.12f), 0.012f, lib["Rubber"]);
            }
            // side stand
            Tube(mb, new Vector3(-0.1f, 0.3f, -0.08f), new Vector3(-0.28f, 0.01f, -0.02f), 0.012f, metal);
            pm.Colliders.Add(new Bounds(new Vector3(0, 0.6f, 0), new Vector3(0.55f, 1.2f, 2.1f)));
        }

        // ------------------------------------------------------------------ bicycle

        public static void Bicycle(MeshBuilder mb, PropMesh pm, Material paint)
        {
            var lib = mb.Lib;
            var steel = lib["Car_ChromeBright"];
            var dark = lib["Car_Trim"];
            Vector3 fa = new Vector3(0f, 0.34f, 0.53f), ra = new Vector3(0f, 0.34f, -0.52f);
            Wheel(mb, fa, 0.34f, 0.019f, 0.31f, steel, 16, 0.004f, false);
            Wheel(mb, ra, 0.34f, 0.02f, 0.31f, steel, 16, 0.004f, false);
            Arc(mb, fa, 0.37f, 0.03f, 10f, 125f, paint, 10);
            Arc(mb, ra, 0.37f, 0.03f, 60f, 175f, paint, 10);
            const float t = 0.017f;
            Vector3 bb = new Vector3(0f, 0.3f, -0.04f), seatTop = new Vector3(0f, 0.8f, -0.22f), headTop = new Vector3(0f, 0.84f, 0.38f), headBot = new Vector3(0f, 0.66f, 0.43f);
            Tube(mb, bb, seatTop, t, paint);
            Tube(mb, bb, headBot, t * 1.15f, paint);
            Tube(mb, seatTop + new Vector3(0f, -0.08f, 0.02f), headTop + new Vector3(0f, -0.06f, 0f), t, paint);
            Tube(mb, headBot, headTop, 0.022f, paint);
            foreach (float s in new[] { -1f, 1f })
            {
                Tube(mb, bb + new Vector3(s * 0.03f, 0f, 0f), ra + new Vector3(s * 0.045f, 0f, 0f), 0.01f, paint, 6);
                Tube(mb, seatTop + new Vector3(s * 0.02f, -0.04f, 0f), ra + new Vector3(s * 0.045f, 0f, 0f), 0.01f, paint, 6);
                Tube(mb, headBot + new Vector3(s * 0.02f, 0f, 0f), fa + new Vector3(s * 0.035f, 0f, 0f), 0.011f, paint, 6);
                // pedals on the crank
                Vector3 crank = bb + new Vector3(s * 0.08f, s * 0.1f, s * 0.12f);
                Tube(mb, bb + new Vector3(s * 0.06f, 0f, 0f), crank, 0.01f, steel, 6);
                mb.Box(crank + new Vector3(s * 0.05f, 0f, 0f), new Vector3(0.09f, 0.02f, 0.06f), dark);
            }
            CarWheels.Lathe(mb, bb + Vector3.right * 0.05f, Vector3.right, new[] { new Vector2(0.1f, -0.003f), new Vector2(0.1f, 0.003f), new Vector2(0.03f, 0.003f) }, 18, steel);
            // seat post + saddle, stem + upright bar with grips, bell, small lamp
            Tube(mb, seatTop, seatTop + new Vector3(0f, 0.12f, -0.03f), 0.012f, steel, 6);
            CarBlob.Blob(mb, seatTop + new Vector3(0f, 0.15f, -0.02f), Quaternion.identity, new Vector3(0.09f, 0.035f, 0.13f), 0.4f, lib["Car_LeatherBlack"], 0.6f, 0.55f, 10, 6);
            // Steering: the quill rises out of the head tube along the steering axis, the stem reaches forward to
            // the bar clamp, the city bar sweeps back to the rider from there (everything joined, nothing floating).
            Vector3 axis = (headTop - headBot).normalized;
            Vector3 quill = headTop + axis * 0.1f;
            Tube(mb, headTop - axis * 0.01f, quill, 0.013f, steel, 8);
            mb.Cylinder(headTop - axis * 0.004f, headTop + axis * 0.012f, 0.021f, 10, steel);   // headset lock ring
            Vector3 clamp = quill + new Vector3(0f, 0.012f, 0.055f);
            Tube(mb, quill - axis * 0.006f, clamp, 0.012f, steel, 8);
            mb.Cylinder(clamp - Vector3.right * 0.03f, clamp + Vector3.right * 0.03f, 0.017f, 10, steel);   // clamp collar
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 b0 = clamp, b1 = clamp + new Vector3(s * 0.11f, 0.004f, -0.006f);
                Vector3 b2 = clamp + new Vector3(s * 0.2f, 0.02f, -0.06f), b3 = clamp + new Vector3(s * 0.25f, 0.026f, -0.14f);
                Vector3 b4 = clamp + new Vector3(s * 0.27f, 0.026f, -0.2f);
                Tube(mb, b0, b1, 0.011f, steel, 8);
                Tube(mb, b1 - (b2 - b1).normalized * 0.004f, b2, 0.011f, steel, 8);
                Tube(mb, b2 - (b3 - b2).normalized * 0.004f, b3, 0.011f, steel, 8);
                Tube(mb, b3 - (b4 - b3).normalized * 0.004f, b4 + (b4 - b3).normalized * 0.012f, 0.011f, steel, 8);
                // rubber grip on the last straight, brake lever reaching forward under it
                Vector3 gd = (b4 - b3).normalized;
                Tube(mb, b3 + gd * 0.005f, b4 + gd * 0.018f, 0.0165f, dark, 8);
                Vector3 lever = b3 - gd * 0.01f + new Vector3(0f, -0.006f, 0f);
                mb.Box(lever + new Vector3(-s * 0.004f, 0f, 0.012f), new Vector3(0.02f, 0.022f, 0.03f), dark);
                Tube(mb, lever + new Vector3(0f, -0.01f, 0.02f), b4 + new Vector3(-s * 0.01f, -0.03f, 0.02f), 0.005f, steel, 5);
            }
            // bell on the left of the clamp
            CarWheels.Lathe(mb, clamp + new Vector3(-0.07f, 0.018f, 0f), Vector3.up, new[] { new Vector2(0.004f, 0f), new Vector2(0.022f, 0.002f), new Vector2(0.02f, 0.014f), new Vector2(0f, 0.02f) }, 12, steel);
            CarWheels.Lathe(mb, headBot + new Vector3(0f, 0.05f, 0.05f), Vector3.forward, new[] { new Vector2(0f, -0.04f), new Vector2(0.03f, -0.02f), new Vector2(0.03f, 0f) }, 10, dark);
            CarWheels.Lathe(mb, headBot + new Vector3(0f, 0.05f, 0.05f), Vector3.forward, new[] { new Vector2(0.028f, 0f), new Vector2(0f, 0.004f) }, 10, lib["Car_Lens"]);
            mb.Box(ra + new Vector3(0f, 0.1f, -0.38f), new Vector3(0.05f, 0.03f, 0.01f), lib["Car_TailRed"]);
            // rear rack
            Vector3 rack = new Vector3(0f, 0.73f, -0.5f);
            mb.Box(rack, new Vector3(0.14f, 0.01f, 0.34f), dark);
            foreach (float s in new[] { -1f, 1f }) Tube(mb, rack + new Vector3(s * 0.065f, 0f, -0.1f), ra + new Vector3(s * 0.05f, 0f, 0f), 0.006f, dark, 5);
            pm.Colliders.Add(new Bounds(new Vector3(0, 0.5f, 0), new Vector3(0.4f, 1.0f, 1.7f)));
        }
    }
}
