using UnityEngine;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// Cabin of the wreck: GTI sport seats in red cloth up to chest height with black shoulders and head rests, matching
    /// rear bench, Golf IV dashboard, three-spoke wheel with the deflated driver's airbag, passenger airbag hanging off
    /// the dash, pedals, belts, the plate leaning on the screen (front photo) and glass crumbs everywhere.
    /// </summary>
    internal sealed partial class GolfWreck
    {
        /// <summary>Height above the cushion where the red cloth ends (chest of a seated person).</summary>
        private const float ChestLine = 0.4f;

        protected override void Interior()
        {
            var dash = Lib["Car_Dash"];
            foreach (float s in new[] { -1f, 1f })
                SportSeat(new Vector3(s * 0.36f, TubFloor, 0.02f));
            RearSeat();
            CarInterior.Dash(Mb, 0.72f, 0.9f, 0.52f, 0.92f, 0.55f, -0.37f, dash);
            // rounded dash top and the screen-side shelf
            CarBlob.Blob(Mb, new Vector3(0f, 0.93f, 0.66f), Quaternion.identity, new Vector3(0.7f, 0.04f, 0.16f), 0.3f, dash, 0.5f, 1f, 20, 8);
            var wc = new Vector3(-0.37f, 0.9f, 0.36f);
            var axis = new Vector3(0f, 0.45f, 1f).normalized;
            CarInterior.SteeringWheel(Mb, wc, axis, 0.185f, Lib["Wreck_Leather"], 3);
            CarInterior.Console(Mb, -0.35f, 0.52f, TubFloor, 0.2f, dash, Lib["Wreck_Leather"]);
            DriverAirbag(wc, axis);
            PassengerAirbag();
            // pedals, floor mats
            for (int i = 0; i < 3; i++)
            {
                var pc = new Vector3(-0.26f - i * 0.1f, 0.36f, 0.76f);
                Mb.Box(pc, new Vector3(i == 1 ? 0.08f : 0.06f, 0.07f, 0.012f), Lib["Wreck_Rubber"], Quaternion.Euler(-30f, 0, 0));
                Mb.Cylinder(pc + new Vector3(0, 0.03f, 0.01f), pc + new Vector3(0, 0.2f, 0.08f), 0.008f, 6, dash);
            }
            foreach (float s in new[] { -1f, 1f })
                Mb.Box(new Vector3(s * 0.36f, TubFloor + 0.006f, 0.52f), new Vector3(0.42f, 0.012f, 0.5f), Lib["Wreck_Rubber"]);
            // parcel shelf, rear seat back wall
            Mb.Box(new Vector3(0, 0.98f, -1.6f), new Vector3(1.38f, 0.02f, 0.62f), dash);
            Mb.Box(new Vector3(0, 0.6f, -1.22f), new Vector3(1.4f, 0.7f, 0.04f), Lib["Wreck_SeatBlack"]);
            // loose belts hanging from the B pillars
            foreach (float s in new[] { -1f, 1f })
            {
                var top = new Vector3(s * 0.7f, 1.17f, -0.17f);
                WreckKit.Tube(Mb, WreckKit.Spline(new[] { top, top + new Vector3(-s * 0.02f, -0.35f, 0.02f), new Vector3(s * 0.69f, 0.4f, -0.12f), new Vector3(s * 0.62f, 0.33f, -0.05f) }), 0.012f, Lib["Car_Dash"], 4);
            }
            // plate leaning on the screen (front photo: a blue-banded plate behind the glass)
            CarKit.Plate(Mb, new Vector3(0.14f, 1.0f, 0.74f), new Vector3(0f, 0.55f, 0.84f), "LUB 2002", 0.75f);
            // glass crumbs on the dash and floor
            var rng = new System.Random(905);
            WreckKit.Crumbs(Mb, new Vector3(0.3f, 0.975f, 0.66f), Vector3.up, 0.25f, 40, Lib["Wreck_GlassBits"], rng);
            WreckKit.Crumbs(Mb, new Vector3(0.4f, TubFloor + 0.015f, 0.45f), Vector3.up, 0.2f, 35, Lib["Wreck_GlassBits"], rng);
            WreckKit.Crumbs(Mb, new Vector3(0.7f, SillY + 0.004f, 0.3f), Vector3.up, 0.1f, 18, Lib["Wreck_GlassBits"], rng);
        }

        /// <summary>Front sport seat on its floor point p (facing +z).</summary>
        private void SportSeat(Vector3 p)
        {
            var red = Lib["Wreck_SeatRed"];
            var black = Lib["Wreck_SeatBlack"];
            float cy = p.y + 0.24f;
            var tilt = Quaternion.Euler(-8f, 0, 0);
            // rails and frame
            foreach (float s in new[] { -1f, 1f })
                Mb.Box(new Vector3(p.x + s * 0.18f, p.y + 0.02f, p.z), new Vector3(0.03f, 0.03f, 0.55f), Lib["Wreck_Steel"]);
            Mb.Box(new Vector3(p.x, p.y + 0.11f, p.z - 0.02f), new Vector3(0.38f, 0.16f, 0.42f), Lib["Car_Dash"]);
            // cushion: red centre, high red bolsters
            var cc = new Vector3(p.x, cy, p.z + 0.02f);
            CarBlob.Blob(Mb, cc, tilt, new Vector3(0.17f, 0.055f, 0.245f), 0.3f, red, 0.35f, 1f, 16, 10);
            foreach (float s in new[] { -1f, 1f })
                CarBlob.Blob(Mb, cc + tilt * new Vector3(s * 0.2f, 0.04f, 0f), tilt * Quaternion.Euler(0, 0, -s * 12f), new Vector3(0.05f, 0.07f, 0.24f), 0.45f, red, 0.4f, 1f, 12, 8);
            Mb.Box(cc + tilt * new Vector3(0f, 0.055f, 0.0f), new Vector3(0.006f, 0.004f, 0.4f), black, tilt);
            // backrest: red up to the chest line, black shoulders and integrated head rest above
            var back = Quaternion.Euler(-14f, 0, 0);
            Vector3 b0 = cc + new Vector3(0f, 0.04f, -0.25f);
            Vector3 B(float u, float v, float w) => b0 + back * new Vector3(u, v, w);
            CarBlob.Blob(Mb, B(0f, ChestLine * 0.5f, 0f), back, new Vector3(0.18f, ChestLine * 0.5f, 0.065f), 0.32f, red, 0.35f, 1f, 16, 10);
            foreach (float s in new[] { -1f, 1f })
                CarBlob.Blob(Mb, B(s * 0.22f, ChestLine * 0.5f, 0.035f), back * Quaternion.Euler(0, s * 18f, 0), new Vector3(0.055f, ChestLine * 0.5f + 0.01f, 0.075f), 0.45f, red, 0.4f, 1f, 12, 8);
            CarBlob.Blob(Mb, B(0f, ChestLine + 0.1f, -0.01f), back, new Vector3(0.25f, 0.11f, 0.07f), 0.35f, black, 0.35f, 1f, 16, 10);
            CarBlob.Blob(Mb, B(0f, ChestLine + 0.29f, -0.01f), back, new Vector3(0.14f, 0.1f, 0.065f), 0.4f, black, 0.4f, 1f, 14, 10);
            // piping where red meets black, and the GTI logo on the shoulders
            Mb.Box(B(0f, ChestLine, 0.06f), new Vector3(0.44f, 0.01f, 0.012f), black, back);
            Mb.Text("GTI", B(0f, ChestLine + 0.13f, 0.071f), back * Vector3.forward, back * Vector3.up, 0.04f, Lib["Wreck_TextRed"]);
            // tilt lever
            Mb.Box(new Vector3(p.x + (p.x < 0 ? -0.24f : 0.24f), cy - 0.02f, p.z - 0.2f), new Vector3(0.02f, 0.04f, 0.1f), Lib["Car_Dash"]);
            WreckKit.Crumbs(Mb, cc + tilt * new Vector3(0f, 0.06f, 0f), tilt * Vector3.up, 0.14f, p.x > 0 ? 45 : 14, Lib["Wreck_GlassBits"], new System.Random((int)(p.x * 100) + 50));
        }

        private void RearSeat()
        {
            var red = Lib["Wreck_SeatRed"];
            var black = Lib["Wreck_SeatBlack"];
            CarInterior.RearBench(Mb, 0.62f, TubFloor, 0.52f, -0.85f, black, 0.55f, 16f);
            var back = Quaternion.Euler(-16f, 0, 0);
            foreach (float s in new[] { -1f, 1f })
            {
                CarBlob.Blob(Mb, new Vector3(s * 0.33f, 0.555f, -0.83f), Quaternion.identity, new Vector3(0.2f, 0.02f, 0.2f), 0.3f, red, 0.3f, 1f, 12, 6);
                CarBlob.Blob(Mb, new Vector3(0f, 0.52f, -1.11f) + back * new Vector3(s * 0.33f, 0.24f, 0.066f), back, new Vector3(0.2f, 0.2f, 0.012f), 0.3f, red, 0.3f, 1f, 12, 6);
                CarBlob.Blob(Mb, new Vector3(0f, 0.52f, -1.11f) + back * new Vector3(s * 0.33f, 0.6f, 0.02f), back, new Vector3(0.11f, 0.07f, 0.05f), 0.4f, black, 0.4f, 1f, 12, 8);
            }
        }

        /// <summary>Deflated driver's airbag hanging out of the wheel hub over its lower half.</summary>
        private void DriverAirbag(Vector3 wc, Vector3 axis)
        {
            int a0 = Mb.VertexCount;
            var bag = Lib["Wreck_Airbag"];
            var rot = Quaternion.LookRotation(-axis, Vector3.up);
            // one emptied bag: a soft sheet out of the hub, sagging over the lower rim towards the lap
            CarBlob.Blob(Mb, wc - axis * 0.05f + Vector3.down * 0.06f, rot * Quaternion.Euler(12f, 0, 4f), new Vector3(0.2f, 0.19f, 0.03f), 0.7f, bag, 0.9f, 0.85f, 20, 12);
            Mb.Warp(a0, p =>
            {
                // the lower half folds forward over the rim
                float d = Mathf.Max(0f, wc.y - 0.05f - p.y);
                p += -axis * (d * 0.45f) + Vector3.down * (d * 0.2f);
                return Wrinkle(0.014f, 32f)(p);
            });
            // torn hub cover flaps
            foreach (float s in new[] { -1f, 1f })
                Mb.Box(wc - axis * 0.03f + Vector3.right * (s * 0.06f) + Vector3.up * 0.03f, new Vector3(0.06f, 0.05f, 0.006f), Lib["Wreck_Leather"], rot * Quaternion.Euler(0, s * 50f, 0));
        }

        /// <summary>Passenger airbag burst out of the dash top, draped over the edge towards the seat.</summary>
        private void PassengerAirbag()
        {
            int a0 = Mb.VertexCount;
            var bag = Lib["Wreck_Airbag"];
            CarBlob.Blob(Mb, new Vector3(0.36f, 0.965f, 0.6f), Quaternion.Euler(-10f, 4f, 0), new Vector3(0.22f, 0.035f, 0.16f), 0.6f, bag, 0.8f, 1f, 18, 10);
            CarBlob.Blob(Mb, new Vector3(0.37f, 0.86f, 0.46f), Quaternion.Euler(-70f, 0f, 5f), new Vector3(0.2f, 0.12f, 0.03f), 0.6f, bag, 0.8f, 1f, 16, 8);
            Mb.Warp(a0, Wrinkle(0.014f, 30f));
            // the burst flap of the dash cover
            Mb.Box(new Vector3(0.36f, 0.985f, 0.74f), new Vector3(0.3f, 0.008f, 0.12f), Lib["Car_Dash"], Quaternion.Euler(-55f, 0, 0));
        }

        private static System.Func<Vector3, Vector3> Wrinkle(float amp, float freq) => p => p + new Vector3(
            WreckKit.Noise2(p * freq), WreckKit.Noise2(p * freq + new Vector3(3.3f, 7.1f, 1.9f)), WreckKit.Noise2(p * freq + new Vector3(8.2f, 2.4f, 5.6f))) * amp * 2f;
    }
}
