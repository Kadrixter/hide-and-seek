using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>Go-kart model, part 2: bodywork, seat, controls and the engine (see <see cref="KartModel"/>).</summary>
    public static partial class KartModel
    {
        /// <summary>Nose cone, front panel with the number plate, side pods with a stripe, rear bumper with the brake light.</summary>
        private static void BuildBodywork(Transform body, KartParts parts, KartLivery livery)
        {
            var red = new ProcMesh();
            red.At(new Vector3(0f, 0.15f, 0.8f), Quaternion.Euler(-8f, 0f, 0f)).RoundBox(new Vector3(0.7f, 0.13f, 0.22f), 0.05f, 5);
            red.At(new Vector3(0f, 0.26f, 0.61f), Quaternion.Euler(-28f, 0f, 0f)).RoundBox(new Vector3(0.36f, 0.25f, 0.035f), 0.014f, 4);
            for (int s = -1; s <= 1; s += 2)
            {
                red.At(new Vector3(s * 0.44f, 0.15f, 0f)).RoundBox(new Vector3(0.15f, 0.12f, 0.6f), 0.045f, 5);
                // 27.11: pods taper in towards the nose and flare out behind (air guides), a rear wheel guard
                red.At(new Vector3(s * 0.4f, 0.14f, 0.28f), Quaternion.Euler(-6f, -s * 16f, 0f)).RoundBox(new Vector3(0.11f, 0.1f, 0.16f), 0.04f, 4);
                red.At(new Vector3(s * 0.46f, 0.16f, -0.33f), Quaternion.Euler(4f, s * 8f, 0f)).RoundBox(new Vector3(0.13f, 0.1f, 0.14f), 0.04f, 4);
            }
            // nose: a lower splitter wedge under the cone and a raised centre ridge
            red.At(new Vector3(0f, 0.1f, 0.88f), Quaternion.Euler(10f, 0f, 0f)).RoundBox(new Vector3(0.62f, 0.05f, 0.12f), 0.02f, 3);
            red.At(new Vector3(0f, 0.2f, 0.74f), Quaternion.Euler(-14f, 0f, 0f)).RoundBox(new Vector3(0.26f, 0.06f, 0.22f), 0.03f, 4);
            // rear plastic bumper fairing across the back (wraps the tube bumper)
            red.At(new Vector3(0f, 0.17f, -0.83f)).RoundBox(new Vector3(1.0f, 0.1f, 0.09f), 0.035f, 4);
            for (int s = -1; s <= 1; s += 2)
                red.At(new Vector3(s * 0.56f, 0.165f, -0.74f), Quaternion.Euler(0f, s * 30f, 0f)).RoundBox(new Vector3(0.14f, 0.09f, 0.08f), 0.03f, 3);
            Piece(body, "Bodywork", red.ToMesh("KartBodywork"), livery.BodyMat);

            var white = new ProcMesh();
            Quaternion panel = Quaternion.Euler(-28f, 0f, 0f);
            white.At(new Vector3(0f, 0.26f, 0.61f) + panel * new Vector3(0f, 0.01f, 0.02f), panel).RoundBox(new Vector3(0.2f, 0.15f, 0.01f), 0.02f, 3);
            Piece(body, "Plate", white.ToMesh("KartPlate"), White);
            var stripes = new ProcMesh();
            for (int s = -1; s <= 1; s += 2)
                stripes.At(new Vector3(s * 0.44f, 0.185f, 0.01f)).RoundBox(new Vector3(0.155f, 0.024f, 0.48f), 0.008f, 2);
            stripes.At(new Vector3(0f, 0.236f, 0.74f), Quaternion.Euler(-14f, 0f, 0f)).RoundBox(new Vector3(0.1f, 0.01f, 0.21f), 0.004f, 2); // nose stripe (on the ridge)
            stripes.At(new Vector3(0f, 0.222f, -0.83f)).RoundBox(new Vector3(0.6f, 0.012f, 0.05f), 0.004f, 2); // rear fairing stripe
            Piece(body, "Stripes", stripes.ToMesh("KartStripes"), livery.StripeMat);
            NumberPlate(body, new Vector3(0f, 0.26f, 0.61f) + panel * new Vector3(0f, 0.012f, 0.027f), panel, livery.Number);

            var blk = new ProcMesh();
            blk.At(new Vector3(0f, 0.075f, 0.9f)).RoundBox(new Vector3(0.74f, 0.05f, 0.07f), 0.02f, 3); // front lip
            blk.At(Vector3.zero).Tube(new[]
            {
                new Vector3(-0.58f, 0.15f, -0.66f), new Vector3(-0.62f, 0.16f, -0.8f), new Vector3(-0.5f, 0.17f, -0.86f),
                new Vector3(0.5f, 0.17f, -0.86f), new Vector3(0.62f, 0.16f, -0.8f), new Vector3(0.58f, 0.15f, -0.66f),
            }, 0.026f, 10);
            for (int s = -1; s <= 1; s += 2)
                blk.At(Vector3.zero).Tube(new[] { new Vector3(s * 0.2f, 0.08f, -0.74f), new Vector3(s * 0.22f, 0.16f, -0.85f) }, 0.016f, 6);
            Piece(body, "Bumpers", blk.ToMesh("KartBumpers"), Black);

            var light = new ProcMesh();
            light.At(new Vector3(0f, 0.17f, -0.89f)).RoundBox(new Vector3(0.16f, 0.045f, 0.02f), 0.008f, 2);
            parts.BrakeLight = Piece(body, "BrakeLight", light.ToMesh("KartBrakeLight"), BrakeMat()).GetComponent<Renderer>();

            // Pedals with their rods, and the small fuel tank low between the legs.
            var ped = new ProcMesh();
            for (int s = -1; s <= 1; s += 2)
            {
                ped.At(new Vector3(s * 0.1f, 0.14f, 0.47f), Quaternion.Euler(-35f, 0f, 0f)).RoundBox(new Vector3(0.065f, 0.1f, 0.014f), 0.005f, 2);
                ped.At(Vector3.zero).Tube(new[] { new Vector3(s * 0.1f, 0.08f, 0.5f), new Vector3(s * 0.1f, 0.11f, 0.49f) }, 0.008f, 6);
            }
            Piece(body, "Pedals", ped.ToMesh("KartPedals"), Dark);
            var tank = new ProcMesh();
            tank.At(new Vector3(0f, 0.12f, 0.2f)).RoundBox(new Vector3(0.13f, 0.08f, 0.17f), 0.03f, 4);
            Piece(body, "Tank", tank.ToMesh("KartTank"), White);
            var cap = new ProcMesh();
            cap.At(new Vector3(0f, 0.16f, 0.24f)).Lathe(new[] { new Vector2(0.022f, 0f), new Vector2(0.022f, 0.016f), new Vector2(0f, 0.018f) }, 10);
            Piece(body, "TankCap", cap.ToMesh("KartTankCap"), Red);
        }

        private static void NumberPlate(Transform body, Vector3 pos, Quaternion rot, int number)
        {
            var go = new GameObject("Number");
            go.transform.SetParent(body, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot * Quaternion.Euler(0f, 180f, 0f); // text faces forward (+Z)
            var tm = go.AddComponent<TextMesh>();
            tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.text = number.ToString();
            tm.fontSize = 64;
            tm.characterSize = 0.022f;
            tm.fontStyle = FontStyle.Bold;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(0.08f, 0.08f, 0.1f);
            go.GetComponent<MeshRenderer>().sharedMaterial = tm.font.material;
            WorldText.Setup(tm, new Vector2(0.16f, 0.12f));
        }

        /// <summary>Bucket seat with bolsters and stays, the steering column and the three-spoke wheel.</summary>
        private static void BuildSeatAndControls(Transform body, KartParts parts)
        {
            var seat = new ProcMesh();
            seat.At(new Vector3(0f, 0.095f, -0.28f)).RoundBox(new Vector3(0.38f, 0.05f, 0.36f), 0.02f, 4);
            seat.At(new Vector3(0f, 0.33f, -0.49f), Quaternion.Euler(-14f, 0f, 0f)).RoundBox(new Vector3(0.4f, 0.44f, 0.05f), 0.022f, 4);
            for (int s = -1; s <= 1; s += 2)
                seat.At(new Vector3(s * 0.215f, 0.18f, -0.31f), Quaternion.Euler(0f, 0f, s * -8f)).RoundBox(new Vector3(0.05f, 0.16f, 0.36f), 0.02f, 3);
            Piece(body, "Seat", seat.ToMesh("KartSeat"), Black);

            var chrome = new ProcMesh();
            chrome.At(Vector3.zero);
            for (int s = -1; s <= 1; s += 2)
                chrome.Tube(new[] { new Vector3(s * 0.13f, 0.46f, -0.53f), new Vector3(s * 0.19f, 0.24f, -0.6f), new Vector3(s * 0.24f, 0.08f, -0.6f) }, 0.011f, 6);
            Vector3 colDir = WheelTilt * Vector3.forward;
            chrome.Tube(new[] { new Vector3(0f, 0.09f, 0.55f), WheelPos + colDir * 0.06f }, 0.016f, 8);
            Piece(body, "SeatStays", chrome.ToMesh("KartStays"), Chrome);

            var seatPoint = new GameObject("SeatPoint").transform;
            seatPoint.SetParent(body, false);
            seatPoint.localPosition = SeatPos;
            parts.Seat = seatPoint;

            var wheel = new GameObject("SteeringWheel").transform;
            wheel.SetParent(body, false);
            wheel.localPosition = WheelPos;
            wheel.localRotation = WheelTilt;
            parts.SteeringWheel = wheel;
            var rim = new ProcMesh();
            rim.At(Vector3.zero, Quaternion.Euler(90f, 0f, 0f)).Torus(WheelRimRadius, 0.019f, 28, 8);
            Piece(wheel, "Rim", rim.ToMesh("KartWheelRim"), Black);
            var hub = new ProcMesh();
            foreach (float a in new[] { 0f, 180f, 270f })
            {
                float r = a * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(r), Mathf.Sin(r), 0f);
                hub.At(dir * WheelRimRadius * 0.5f, Quaternion.Euler(0f, 0f, a)).RoundBox(new Vector3(WheelRimRadius, 0.022f, 0.01f), 0.004f, 2);
            }
            hub.At(new Vector3(0f, 0f, 0.012f), Quaternion.Euler(-90f, 0f, 0f)).Lathe(new[] { new Vector2(0.04f, 0f), new Vector2(0.04f, 0.022f), new Vector2(0f, 0.026f) }, 14);
            Piece(wheel, "Hub", hub.ToMesh("KartWheelHub"), Chrome);
            var horn = new ProcMesh();
            horn.At(new Vector3(0f, 0f, -0.012f), Quaternion.Euler(-90f, 0f, 0f)).Lathe(new[] { new Vector2(0.025f, 0f), new Vector2(0.022f, 0.008f), new Vector2(0f, 0.01f) }, 12);
            Piece(wheel, "Cap", horn.ToMesh("KartWheelCap"), Red);
        }

        /// <summary>Single-cylinder engine right of the seat: block, finned head, plug, pull start, air box, chain, exhaust.</summary>
        private static void BuildEngine(Transform body, KartParts parts)
        {
            Vector3 e = new(0.34f, 0.2f, -0.36f);
            var grey = new ProcMesh();
            grey.At(e).RoundBox(new Vector3(0.16f, 0.18f, 0.2f), 0.025f, 3);
            for (int i = 0; i < 6; i++)
                grey.At(e + new Vector3(0f, 0.11f + i * 0.018f, 0f)).Box(Vector3.zero, new Vector3(0.19f - i * 0.012f, 0.009f, 0.17f - i * 0.01f));
            grey.At(new Vector3(0.25f, 0.12f, e.z), Quaternion.Euler(0f, 0f, 90f)).Lathe(new[] { new Vector2(0.032f, -0.006f), new Vector2(0.032f, 0.006f), new Vector2(0f, 0.006f) }, 12); // engine sprocket
            Piece(body, "Engine", grey.ToMesh("KartEngine"), Grey);

            var red = new ProcMesh();
            red.At(e + new Vector3(0f, 0f, 0.1f), Quaternion.Euler(90f, 0f, 0f)).Lathe(new[] { new Vector2(0.075f, 0f), new Vector2(0.075f, 0.03f), new Vector2(0.05f, 0.04f), new Vector2(0f, 0.04f) }, 18);
            Piece(body, "PullStart", red.ToMesh("KartPullStart"), Red);

            var blk = new ProcMesh();
            blk.At(e + new Vector3(0f, 0.235f, 0f)).RoundBox(new Vector3(0.03f, 0.04f, 0.03f), 0.008f, 2); // plug cap
            blk.At(new Vector3(0.35f, 0.24f, -0.12f)).RoundBox(new Vector3(0.11f, 0.13f, 0.11f), 0.03f, 3); // air box
            blk.At(Vector3.zero).Tube(new[] { e + new Vector3(0f, 0.235f, 0f), e + new Vector3(-0.05f, 0.29f, 0.04f), new Vector3(0.26f, 0.24f, -0.2f) }, 0.006f, 5); // plug lead
            // Chain from the engine sprocket down to the axle sprocket (two runs).
            Vector3 a = new Vector3(0.245f, 0.12f, e.z), b = new Vector3(0.245f, KartPhysics.RearRadius, RearZ);
            blk.At(Vector3.zero).Tube(new[] { a + Vector3.up * 0.033f, b + Vector3.up * 0.068f }, 0.007f, 4, false);
            blk.At(Vector3.zero).Tube(new[] { a - Vector3.up * 0.033f, b - Vector3.up * 0.068f }, 0.007f, 4, false);
            blk.At(b + new Vector3(0.008f, 0f, 0f), Quaternion.Euler(0f, 0f, -90f)).Lathe(new[] { new Vector2(0.07f, -0.005f), new Vector2(0.07f, 0.005f), new Vector2(0f, 0.005f) }, 20); // axle sprocket
            Piece(body, "EngineBits", blk.ToMesh("KartEngineBits"), Black);

            var pipe = new ProcMesh();
            pipe.At(Vector3.zero).Tube(new[] { e + new Vector3(0.05f, 0.08f, -0.08f), e + new Vector3(0.02f, 0.12f, -0.2f), new Vector3(0.3f, 0.31f, -0.62f) }, 0.018f, 8);
            Piece(body, "Exhaust", pipe.ToMesh("KartExhaust"), Chrome);
            var muffler = new ProcMesh();
            muffler.At(new Vector3(0.3f, 0.31f, -0.6f), Quaternion.Euler(-90f, 0f, 0f)).Lathe(new[]
            {
                new Vector2(0f, 0f), new Vector2(0.045f, 0.01f), new Vector2(0.05f, 0.04f), new Vector2(0.05f, 0.2f),
                new Vector2(0.03f, 0.215f), new Vector2(0.03f, 0.24f), new Vector2(0f, 0.24f),
            }, 14, 50f);
            Piece(body, "Silencer", muffler.ToMesh("KartSilencer"), Dark);
            var tip = new GameObject("ExhaustTip").transform;
            tip.SetParent(body, false);
            tip.localPosition = new Vector3(0.3f, 0.31f, -0.85f);
            parts.Exhaust = tip;
        }
    }
}
