using UnityEngine;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// Front end without its bumper, grille and lamps (front photo): frame rails and crash boxes, the bent front-end
    /// carrier with empty lamp mounts, radiator, condenser, fan, the left half of the GTI grille still hanging on,
    /// loose connectors and wires; after the lean the crash beam hangs from the left crash box down to the floor.
    /// </summary>
    internal sealed partial class GolfWreck
    {
        private void FrontStructure()
        {
            var black = Lib["Wreck_Plastic"];
            // frame rails with crash boxes (the right one is crushed by the field)
            foreach (float s in new[] { -1f, 1f })
            {
                Mb.Box(new Vector3(s * 0.46f, 0.33f, 1.42f), new Vector3(0.09f, 0.13f, 1.0f), Primer);
                Mb.Box(new Vector3(s * 0.46f, 0.33f, 1.96f), new Vector3(0.085f, 0.11f, 0.1f), Metal);
                // inner wing apron and the strut tower dome
                Mb.Box(new Vector3(s * 0.6f, 0.5f, 1.38f), new Vector3(0.012f, 0.36f, 0.86f), Primer);
                CarWheels.Lathe(Mb, new Vector3(s * 0.56f, 0.6f, 1.205f), Vector3.up, new[]
                {
                    new Vector2(0.13f, 0f), new Vector2(0.12f, 0.12f), new Vector2(0.09f, 0.16f), new Vector2(0.05f, 0.165f), new Vector2(0f, 0.165f),
                }, 18, Primer);
                foreach (float a in new[] { 0f, 2.1f, 4.2f })
                    Mb.Cylinder(new Vector3(s * 0.56f + Mathf.Cos(a) * 0.04f, 0.76f, 1.205f + Mathf.Sin(a) * 0.04f), new Vector3(s * 0.56f + Mathf.Cos(a) * 0.04f, 0.79f, 1.205f + Mathf.Sin(a) * 0.04f), 0.008f, 6, Metal);
                Mb.Cylinder(new Vector3(s * 0.56f, 0.76f, 1.205f), new Vector3(s * 0.56f, 0.8f, 1.205f), 0.012f, 8, Metal);
            }
            // front-end carrier: upper and lower cross members, posts, lamp mounts with broken brackets
            Mb.Box(new Vector3(0f, 0.7f, 1.9f), new Vector3(1.28f, 0.07f, 0.06f), black);
            // lower member snapped in the middle: the right half hangs down
            Mb.Box(new Vector3(-0.2f, 0.31f, 1.9f), new Vector3(0.56f, 0.06f, 0.07f), black);
            Mb.Box(new Vector3(0.3f, 0.215f, 1.93f), new Vector3(0.44f, 0.06f, 0.07f), black, Quaternion.Euler(8f, -10f, -24f));
            WreckKit.Chip(Mb, new Vector3(0.08f, 0.31f, 1.9f), Vector3.right, 0.04f, 0.02f, black, new System.Random(5), 5);
            foreach (float s in new[] { -1f, 1f })
            {
                Mb.Box(new Vector3(s * 0.35f, 0.5f, 1.9f), new Vector3(0.06f, 0.36f, 0.05f), black, Quaternion.Euler(s > 0 ? -12f : 3f, 0f, s > 0 ? 9f : -4f));
                // empty lamp bay: back plate with the round holes of the two lamps and the bulb sockets
                Mb.Box(new Vector3(s * 0.55f, 0.645f, 1.86f), new Vector3(0.36f, 0.11f, 0.012f), black);
                foreach (float lx in new[] { 0.45f, 0.63f })
                {
                    CarWheels.Lathe(Mb, new Vector3(s * lx, 0.645f, 1.866f), Vector3.forward, new[] { new Vector2(0.05f, 0f), new Vector2(0.04f, 0.002f), new Vector2(0f, 0.002f) }, 16, Seam);
                    Mb.Cylinder(new Vector3(s * lx, 0.645f, 1.84f), new Vector3(s * lx, 0.645f, 1.87f), 0.016f, 8, black);
                }
                // snapped mounting lugs
                Mb.Box(new Vector3(s * 0.74f, 0.69f, 1.9f), new Vector3(0.05f, 0.03f, 0.04f), black, Quaternion.Euler(0, 0, s * 25f));
                Mb.Box(new Vector3(s * 0.38f, 0.6f, 1.93f), new Vector3(0.03f, 0.04f, 0.05f), black, Quaternion.Euler(30f, 0, 0));
            }
            // bonnet lock and release cable
            Mb.Box(new Vector3(0f, 0.68f, 1.93f), new Vector3(0.08f, 0.06f, 0.03f), Metal);
            WreckKit.Tube(Mb, WreckKit.Spline(new[] { new Vector3(0f, 0.68f, 1.92f), new Vector3(-0.25f, 0.72f, 1.88f), new Vector3(-0.5f, 0.66f, 1.7f), new Vector3(-0.62f, 0.6f, 1.2f) }), 0.003f, black, 5);
            // radiator, condenser, fan shroud with the fan
            Mb.Box(new Vector3(0f, 0.5f, 1.8f), new Vector3(0.64f, 0.36f, 0.03f), Lib["Car_Grille"]);
            foreach (float s in new[] { -1f, 1f })
                Mb.Box(new Vector3(s * 0.34f, 0.5f, 1.8f), new Vector3(0.05f, 0.4f, 0.045f), black);
            Mb.Box(new Vector3(0f, 0.5f, 1.835f), new Vector3(0.66f, 0.33f, 0.012f), Lib["Car_Grille"]);
            Mb.Box(new Vector3(0f, 0.5f, 1.765f), new Vector3(0.6f, 0.34f, 0.02f), black, null, MeshBuilder.Faces.All & ~MeshBuilder.Faces.PosZ);
            CarWheels.Lathe(Mb, new Vector3(0.1f, 0.5f, 1.75f), Vector3.back, new[] { new Vector2(0.16f, -0.012f), new Vector2(0.16f, 0.012f), new Vector2(0.06f, 0.012f) }, 24, black);
            for (int b = 0; b < 7; b++)
            {
                var r = Quaternion.AngleAxis(b * 360f / 7f, Vector3.forward);
                Mb.Box(new Vector3(0.1f, 0.5f, 1.74f) + r * new Vector3(0.1f, 0f, 0f), new Vector3(0.1f, 0.04f, 0.004f), black, r * Quaternion.Euler(25f, 0, 0));
            }
            // hoses from the radiator to the engine
            WreckKit.Tube(Mb, WreckKit.Spline(new[] { new Vector3(-0.3f, 0.64f, 1.79f), new Vector3(-0.3f, 0.66f, 1.68f), new Vector3(-0.18f, 0.7f, 1.58f) }), 0.02f, Rubber, 10);
            WreckKit.Tube(Mb, WreckKit.Spline(new[] { new Vector3(0.3f, 0.36f, 1.79f), new Vector3(0.28f, 0.3f, 1.66f), new Vector3(0.12f, 0.32f, 1.56f) }), 0.02f, Rubber, 10);
            GrilleRemnant();
            LooseWires();
        }

        /// <summary>Left half of the GTI grille still hanging from one clip: black honeycomb, red line, chrome strip.</summary>
        private void GrilleRemnant()
        {
            var rot = Quaternion.Euler(18f, 12f, -16f);
            var c = new Vector3(-0.32f, 0.6f, 1.95f);
            Mb.Box(c, new Vector3(0.3f, 0.09f, 0.02f), Lib["Wreck_Plastic"], rot);
            Mb.Box(c + rot * new Vector3(0f, 0f, 0.008f), new Vector3(0.28f, 0.072f, 0.008f), Lib["Car_Grille"], rot);
            Mb.Box(c + rot * new Vector3(0f, 0.04f, 0.012f), new Vector3(0.29f, 0.008f, 0.006f), Lib["Wreck_BadgeRed"], rot);
            Mb.Box(c + rot * new Vector3(0f, 0.05f, 0.011f), new Vector3(0.29f, 0.008f, 0.005f), Lib["Car_ChromeBright"], rot);
            // broken end
            WreckKit.Chip(Mb, c + rot * new Vector3(0.16f, -0.01f, 0f), rot * Vector3.forward, 0.03f, 0.018f, Lib["Wreck_Plastic"], new System.Random(77), 4);
        }

        /// <summary>Lamp connectors and wires hanging out of the right front (front photo: red, yellow, blue).</summary>
        private void LooseWires()
        {
            var rng = new System.Random(4242);
            var mats = new[] { Lib["Wreck_WireRed"], Lib["Wreck_WireYellow"], Lib["Wreck_WireBlue"], Lib["Wreck_Plastic"], Lib["Wreck_WireGreen"] };
            foreach (var (root, end) in new[]
            {
                (new Vector3(0.58f, 0.66f, 1.84f), new Vector3(0.7f, 0.28f, 1.98f)),
                (new Vector3(0.5f, 0.6f, 1.82f), new Vector3(0.6f, 0.2f, 2.0f)),
                (new Vector3(-0.58f, 0.66f, 1.84f), new Vector3(-0.66f, 0.42f, 1.97f)),
            })
            {
                for (int k = 0; k < 5; k++)
                {
                    Vector3 j = new Vector3((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f) * 0.06f;
                    var mid = Vector3.Lerp(root, end, 0.5f) + Vector3.forward * 0.06f + j;
                    WreckKit.Tube(Mb, WreckKit.Spline(new[] { root + j * 0.3f, mid, end + j }), 0.0035f, mats[(k + Mathf.Abs((int)(root.x * 10))) % mats.Length], 5);
                }
                // connector at the end of the bundle
                Mb.Box(end + Vector3.down * 0.02f, new Vector3(0.035f, 0.045f, 0.025f), root.x > 0 ? Lib["Wreck_TankWhite"] : Lib["Wreck_Plastic"], Quaternion.Euler(10f, 20f, 15f));
            }
        }

        /// <summary>Crash beam: still bolted to the left crash box, the right end down on the floor (after the lean).</summary>
        private void HangingBeam()
        {
            Vector3 a = Pose(Crush(new Vector3(-0.46f, 0.33f, 2.02f)));
            Vector3 b = new Vector3(0.66f, 0.045f, 2.18f);
            Vector3 m = Vector3.Lerp(a, b, 0.45f) + new Vector3(0f, -0.04f, 0.05f);
            var pts = new[] { a + new Vector3(-0.22f, 0.02f, -0.02f), a, m, b, b + (b - m).normalized * 0.12f + Vector3.down * 0.01f };
            for (int i = 0; i < pts.Length - 1; i++)
            {
                Vector3 d = pts[i + 1] - pts[i];
                var rot = Quaternion.LookRotation(d, Vector3.Cross(d, Vector3.forward).sqrMagnitude > 1e-6f ? Vector3.Cross(Vector3.Cross(d, Vector3.up), d).normalized * -1f : Vector3.up);
                Mb.Box((pts[i] + pts[i + 1]) * 0.5f, new Vector3(0.11f, 0.07f, d.magnitude + 0.02f), i == 2 ? Rust : Metal, Quaternion.LookRotation(d) * Quaternion.Euler(0, 0, 90f));
            }
            // the torn right crash box still on the beam, polystyrene absorber chunks
            Mb.Box(b + new Vector3(-0.05f, 0.06f, -0.06f), new Vector3(0.08f, 0.09f, 0.12f), Metal, Quaternion.Euler(10f, 30f, 5f));
        }
    }
}
