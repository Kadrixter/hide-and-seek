using UnityEngine;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// Bare engine bay (no bonnet): transverse 4-cylinder with the intake manifold's four runners in front, exhaust
    /// manifold behind, timing belt cover, alternator, gearbox on the left, battery with its cables, air box, brake
    /// booster and master cylinder, ABS block, coolant tank, dipstick, harness along the bulkhead and the scuttle.
    /// </summary>
    internal sealed partial class GolfWreck
    {
        private void EngineBay()
        {
            var black = Lib["Wreck_Plastic"];
            var eng = Engine;
            const float ex = 0.12f, ez = 1.42f;
            // bulkhead with its insulation, scuttle tray under the screen
            Mb.Box(new Vector3(0f, 0.58f, 0.925f), new Vector3(1.3f, 0.56f, 0.02f), Primer);
            Mb.Box(new Vector3(0f, 0.6f, 0.94f), new Vector3(1.0f, 0.4f, 0.012f), black);
            Mb.Box(new Vector3(0f, 0.875f, 0.955f), new Vector3(1.3f, 0.05f, 0.08f), black);
            // block, sump, head, valve cover with coil packs and the oil cap
            CarBlob.Blob(Mb, new Vector3(ex, 0.48f, ez), Quaternion.identity, new Vector3(0.235f, 0.17f, 0.15f), 0.12f, eng, 0.12f, 1f, 16, 10);
            CarBlob.Blob(Mb, new Vector3(ex, 0.29f, ez), Quaternion.identity, new Vector3(0.21f, 0.05f, 0.13f), 0.18f, Alu, 0.18f, 1f, 14, 8);
            CarBlob.Blob(Mb, new Vector3(ex, 0.7f, ez), Quaternion.identity, new Vector3(0.245f, 0.055f, 0.13f), 0.12f, Alu, 0.12f, 1f, 16, 8);
            CarBlob.Blob(Mb, new Vector3(ex, 0.785f, ez), Quaternion.identity, new Vector3(0.24f, 0.04f, 0.115f), 0.3f, black, 0.3f, 1f, 18, 8);
            for (int i = 0; i < 4; i++)
            {
                float x = ex - 0.165f + i * 0.11f;
                Mb.Box(new Vector3(x, 0.835f, ez + 0.01f), new Vector3(0.045f, 0.05f, 0.07f), Lib["Wreck_PlasticGrey"]);
                Mb.Box(new Vector3(x, 0.865f, ez + 0.01f), new Vector3(0.03f, 0.012f, 0.04f), black);
            }
            Mb.Cylinder(new Vector3(ex - 0.05f, 0.82f, ez - 0.07f), new Vector3(ex - 0.05f, 0.85f, ez - 0.07f), 0.03f, 14, black);
            Mb.Cylinder(new Vector3(ex - 0.05f, 0.85f, ez - 0.07f), new Vector3(ex - 0.05f, 0.853f, ez - 0.07f), 0.018f, 12, Lib["Wreck_CapYellow"]);
            // intake: plenum low in front, four curved runners up into the head, throttle body on the left end
            Mb.Box(new Vector3(ex, 0.5f, 1.66f), new Vector3(0.5f, 0.1f, 0.09f), black);
            for (int i = 0; i < 4; i++)
            {
                float x = ex - 0.165f + i * 0.11f;
                WreckKit.Tube(Mb, WreckKit.Spline(new[] { new Vector3(x, 0.53f, 1.68f), new Vector3(x, 0.67f, 1.7f), new Vector3(x, 0.74f, 1.64f), new Vector3(x, 0.71f, 1.565f) }), 0.026f, black, 12);
            }
            Mb.Cylinder(new Vector3(ex - 0.25f, 0.52f, 1.66f), new Vector3(ex - 0.33f, 0.52f, 1.66f), 0.038f, 14, Alu);
            // exhaust manifold behind the head, heat shield, down pipe
            var col = new Vector3(ex, 0.38f, 1.2f);
            for (int i = 0; i < 4; i++)
            {
                float x = ex - 0.165f + i * 0.11f;
                WreckKit.Tube(Mb, WreckKit.Spline(new[] { new Vector3(x, 0.62f, 1.285f), new Vector3(x, 0.56f, 1.24f), Vector3.Lerp(new Vector3(x, 0.45f, 1.22f), col, 0.5f), col }), 0.02f, Rust, 8);
            }
            Mb.Box(new Vector3(ex, 0.56f, 1.245f), new Vector3(0.46f, 0.14f, 0.01f), Alu, Quaternion.Euler(-15f, 0, 0));
            WreckKit.Tube(Mb, WreckKit.Spline(new[] { col, col + new Vector3(-0.02f, -0.12f, -0.06f), new Vector3(0.02f, 0.2f, 0.98f) }), 0.028f, Rust, 10);
            // timing belt cover (right end), alternator low in front, engine mount to the wing
            CarBlob.Blob(Mb, new Vector3(ex + 0.265f, 0.55f, ez + 0.02f), Quaternion.identity, new Vector3(0.025f, 0.22f, 0.13f), 0.25f, black, 0.25f, 1f, 12, 8);
            Mb.Cylinder(new Vector3(ex + 0.12f, 0.34f, 1.6f), new Vector3(ex + 0.27f, 0.34f, 1.6f), 0.065f, 16, Alu);
            Mb.Cylinder(new Vector3(ex + 0.27f, 0.34f, 1.6f), new Vector3(ex + 0.29f, 0.34f, 1.6f), 0.03f, 12, Metal);
            Mb.Box(new Vector3(ex + 0.37f, 0.64f, ez), new Vector3(0.12f, 0.06f, 0.12f), Metal);
            // gearbox on the left: bell housing and case
            CarWheels.Lathe(Mb, new Vector3(ex - 0.235f, 0.42f, ez), Vector3.left, new[]
            {
                new Vector2(0f, -0.01f), new Vector2(0.17f, -0.01f), new Vector2(0.17f, 0.08f), new Vector2(0.13f, 0.18f), new Vector2(0.12f, 0.36f), new Vector2(0.06f, 0.4f), new Vector2(0f, 0.4f),
            }, 18, Alu, 40f);
            // battery on its tray (left front) with terminals, clamp and cables
            var bc = new Vector3(-0.42f, 0.615f, 1.66f);
            Mb.Box(bc + Vector3.down * 0.105f, new Vector3(0.28f, 0.02f, 0.21f), black);
            CarBlob.Blob(Mb, bc, Quaternion.identity, new Vector3(0.125f, 0.095f, 0.088f), 0.1f, Lib["Wreck_Plastic"], 0.1f, 1f, 12, 8);
            Mb.Box(bc + new Vector3(0f, 0.098f, 0f), new Vector3(0.24f, 0.006f, 0.16f), Lib["Wreck_PlasticGrey"]);
            var plus = bc + new Vector3(0.08f, 0.12f, 0.05f);
            var minus = bc + new Vector3(-0.08f, 0.115f, 0.05f);
            CarBlob.Blob(Mb, plus, Quaternion.identity, new Vector3(0.03f, 0.022f, 0.03f), 0.5f, Lib["Wreck_WireRed"], 0.5f, 1f, 10, 6);
            Mb.Cylinder(minus + Vector3.down * 0.02f, minus + Vector3.up * 0.01f, 0.012f, 8, Lib["Wreck_Steel"]);
            Mb.Box(bc + new Vector3(0f, -0.08f, -0.09f), new Vector3(0.2f, 0.025f, 0.02f), Metal);
            WreckKit.Tube(Mb, WreckKit.Spline(new[] { plus, plus + new Vector3(0.06f, 0.03f, 0.02f), new Vector3(-0.2f, 0.55f, 1.62f), new Vector3(-0.17f, 0.3f, 1.5f) }), 0.011f, Lib["Wreck_WireRed"], 8);
            WreckKit.Tube(Mb, WreckKit.Spline(new[] { minus, minus + new Vector3(-0.04f, 0.02f, 0.04f), new Vector3(-0.58f, 0.6f, 1.6f), new Vector3(-0.59f, 0.55f, 1.45f) }), 0.01f, Lib["Car_Dash"], 8);
            // air box over the gearbox with the duct to the front
            CarBlob.Blob(Mb, new Vector3(-0.31f, 0.78f, 1.36f), Quaternion.Euler(0, 8f, 0), new Vector3(0.13f, 0.065f, 0.15f), 0.2f, black, 0.2f, 1f, 14, 8);
            WreckKit.Bellows(Mb, new Vector3(-0.31f, 0.74f, 1.52f), new Vector3(-0.34f, 0.72f, 1.62f), 0.045f, 0.045f, 5, Rubber);
            WreckKit.Tube(Mb, WreckKit.Spline(new[] { new Vector3(-0.2f, 0.76f, 1.4f), new Vector3(-0.12f, 0.66f, 1.6f), new Vector3(ex - 0.33f, 0.53f, 1.66f) }), 0.033f, Rubber, 12);
            // brake booster, master cylinder with its reservoir, ABS block with lines
            Mb.Cylinder(new Vector3(-0.4f, 0.64f, 0.935f), new Vector3(-0.4f, 0.64f, 1.03f), 0.125f, 22, black);
            Mb.Cylinder(new Vector3(-0.4f, 0.64f, 1.03f), new Vector3(-0.4f, 0.64f, 1.17f), 0.03f, 12, Alu);
            CarBlob.Blob(Mb, new Vector3(-0.4f, 0.71f, 1.1f), Quaternion.identity, new Vector3(0.05f, 0.035f, 0.05f), 0.4f, Lib["Wreck_TankWhite"], 0.4f, 1f, 12, 6);
            Mb.Cylinder(new Vector3(-0.4f, 0.74f, 1.1f), new Vector3(-0.4f, 0.765f, 1.1f), 0.024f, 12, black);
            Mb.Box(new Vector3(-0.16f, 0.58f, 1.03f), new Vector3(0.12f, 0.09f, 0.1f), Alu);
            Mb.Cylinder(new Vector3(-0.16f, 0.58f, 1.08f), new Vector3(-0.16f, 0.58f, 1.15f), 0.035f, 12, black);
            foreach (float o in new[] { -0.03f, 0f, 0.03f })
                WreckKit.Tube(Mb, WreckKit.Spline(new[] { new Vector3(-0.4f + o, 0.64f, 1.17f), new Vector3(-0.32f + o, 0.62f, 1.14f), new Vector3(-0.2f + o, 0.63f, 1.05f) }), 0.003f, Lib["Wreck_Steel"], 5, false);
            // coolant expansion tank (right rear) with the blue cap, dipstick
            CarBlob.Blob(Mb, new Vector3(0.5f, 0.75f, 1.02f), Quaternion.identity, new Vector3(0.075f, 0.065f, 0.09f), 0.3f, Lib["Wreck_TankWhite"], 0.3f, 1f, 12, 8);
            Mb.Cylinder(new Vector3(0.5f, 0.81f, 1.02f), new Vector3(0.5f, 0.84f, 1.02f), 0.03f, 14, Lib["Wreck_CapBlue"]);
            WreckKit.Tube(Mb, WreckKit.Spline(new[] { new Vector3(0.48f, 0.7f, 1.1f), new Vector3(0.42f, 0.68f, 1.25f), new Vector3(ex + 0.2f, 0.72f, 1.32f) }), 0.012f, Rubber, 8);
            Mb.Cylinder(new Vector3(ex + 0.05f, 0.62f, 1.57f), new Vector3(ex + 0.05f, 0.84f, 1.6f), 0.004f, 5, Lib["Wreck_Steel"]);
            WreckKit.Tube(Mb, WreckKit.Spline(new[] { new Vector3(ex + 0.05f, 0.84f, 1.6f), new Vector3(ex + 0.07f, 0.87f, 1.61f), new Vector3(ex + 0.03f, 0.88f, 1.6f), new Vector3(ex + 0.05f, 0.84f, 1.6f) }), 0.005f, Lib["Wreck_CapYellow"], 6);
            // harness along the bulkhead with branches to the coils, the battery and the left lamp
            var harness = Lib["Car_Dash"];
            WreckKit.Tube(Mb, WreckKit.Spline(new[] { new Vector3(0.58f, 0.8f, 0.98f), new Vector3(0.2f, 0.84f, 0.99f), new Vector3(-0.2f, 0.82f, 0.99f), new Vector3(-0.55f, 0.78f, 1.0f), new Vector3(-0.6f, 0.72f, 1.3f), new Vector3(-0.58f, 0.68f, 1.75f) }), 0.016f, harness, 10);
            WreckKit.Tube(Mb, WreckKit.Spline(new[] { new Vector3(0.1f, 0.84f, 0.99f), new Vector3(0.06f, 0.9f, 1.2f), new Vector3(ex - 0.165f, 0.87f, ez + 0.05f), new Vector3(ex + 0.165f, 0.87f, ez + 0.06f) }), 0.008f, harness, 8);
            WreckKit.Tube(Mb, WreckKit.Spline(new[] { new Vector3(0.55f, 0.8f, 0.99f), new Vector3(0.6f, 0.74f, 1.3f), new Vector3(0.57f, 0.66f, 1.7f) }), 0.012f, harness, 8);
        }
    }
}
