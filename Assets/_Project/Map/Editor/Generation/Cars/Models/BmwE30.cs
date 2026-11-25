using UnityEngine;
using static HideAndSeek.Map.Editor.CarShapes;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// BMW 3 Series E30 sedan, pre-facelift (1982-87): long low glasshouse with thin pillars and the Hofmeister kink,
    /// short bonnet, shark nose leaning forward with a black panel carrying four equal round lamps and the
    /// chrome-framed kidneys, chrome bumpers with rubber strips and black ends, chrome window surrounds, mirrors on
    /// black door sails, ribbed tail lamps, BBS cross-spoke wheels. 4.33 x 1.65 x 1.38 m, wheelbase 2.57 m.
    /// </summary>
    internal sealed class BmwE30 : SportCar
    {
        public override string Name => "BmwE30";
        public override string DefaultPaint => "CarPaint_Alpinweiss";
        public override float Length => 4.325f;
        protected override Arch FrontArch => new Arch(1.41f, 0.305f, 0.34f);
        protected override Arch RearArch => new Arch(-1.16f, 0.305f, 0.34f);
        protected override float TireRadius => 0.305f;
        protected override float TireWidth => 0.195f;
        protected override float RimRadius => 0.19f;
        protected override RimStyle Rim => RimStyle.CrossSpoke;
        protected override float TubWall => 0.7f;
        protected override float TubFloor => 0.25f;
        protected override float[] BodyBreaks => new[] { 1.1f, -0.17f, -1.02f };
        protected override int[] SharpRings => new[] { BMax, BShoulder };
        protected override float RoofRound => 0.5f;

        private const float ZF = 2.1625f, ZR = -2.1625f;

        private static BodyKey K(float z, float yBot, float wBot, float wMax, float yMax, float wSh, float ySh, float yTop,
            float tub = 0f, float round = 0.3f) => new BodyKey(z, yBot, wBot, wMax, yMax, wSh, ySh, yTop, round, tub);

        // the nose leans forward (its top reaches furthest), slab sides with a crisp waist crease
        protected override BodyKey[] BodyKeys() => new[]
        {
            K(ZF, 0.62f, 0.72f, 0.735f, 0.66f, 0.725f, 0.69f, 0.705f),
            K(2.145f, 0.5f, 0.75f, 0.765f, 0.64f, 0.755f, 0.695f, 0.71f),
            K(2.125f, 0.38f, 0.775f, 0.79f, 0.63f, 0.78f, 0.7f, 0.715f),
            K(2.1f, 0.27f, 0.79f, 0.805f, 0.63f, 0.79f, 0.71f, 0.725f),
            K(2.04f, 0.19f, 0.8f, 0.815f, 0.64f, 0.8f, 0.725f, 0.735f),
            K(1.41f, 0.17f, 0.8f, 0.8225f, 0.67f, 0.8f, 0.8f, 0.79f),
            K(1.12f, 0.17f, 0.8f, 0.8225f, 0.68f, 0.797f, 0.84f, 0.835f),
            K(0.98f, 0.17f, 0.8f, 0.8225f, 0.68f, 0.797f, 0.85f, 0.845f, 1f),
            K(-1.25f, 0.17f, 0.8f, 0.8225f, 0.68f, 0.795f, 0.875f, 0.87f, 1f),
            K(-1.38f, 0.17f, 0.8f, 0.8225f, 0.68f, 0.795f, 0.88f, 0.88f),
            K(-2.08f, 0.2f, 0.8f, 0.82f, 0.68f, 0.79f, 0.885f, 0.89f),
            K(-2.14f, 0.23f, 0.785f, 0.805f, 0.68f, 0.775f, 0.88f, 0.885f),
            K(ZR, 0.27f, 0.76f, 0.78f, 0.68f, 0.755f, 0.87f, 0.875f),
        };

        protected override CabinKey[] CabinKeys() => new[]
        {
            new CabinKey(1.1f, 0.845f, 0.75f, 0.845f, 0.62f),
            new CabinKey(0.38f, 0.86f, 0.762f, 1.37f, 0.645f),
            new CabinKey(-0.85f, 0.875f, 0.762f, 1.375f, 0.645f),
            new CabinKey(-0.98f, 0.878f, 0.755f, 1.355f, 0.64f),
            new CabinKey(-1.42f, 0.88f, 0.72f, 0.88f, 0.6f),
        };

        protected override float[] CabinBreaks => new[] { 0.38f, 0.41f, 1.06f, -0.12f, -0.22f, -1.0f, -0.98f, -1.02f, -1.38f };

        protected override Material CabinMat(int seg, float z)
        {
            bool front = z < 1.08f && z > -0.12f, rear = z < -0.22f && z > -1.0f, glass = front || rear;
            switch (seg)
            {
                case 0: return glass ? Chrome : Paint;                    // chrome waist trim
                case 1:
                case 2: return glass ? Glass : (z > -0.22f ? Trim : Paint); // black B-pillar, body C-pillar
                case 3: return glass ? Chrome : Paint;                    // chrome window surround
                case 4: return Paint;
                default:
                    if (z > 0.38f) return z > 1.06f || z < 0.41f ? Trim : Glass;
                    if (z < -0.98f) return z < -1.38f || z > -1.02f ? Trim : Glass;
                    return Paint;
            }
        }

        protected override void Interior()
        {
            var cloth = Lib["Car_InteriorGrey"];
            var dark = Lib["Car_Interior"];
            foreach (float s in new[] { -1f, 1f })
                CarInterior.Seat(Mb, new Vector3(s * 0.35f, TubFloor, 0.12f), 0.5f, 0.5f, cloth, dark, false, 14f, 0.6f);
            CarInterior.RearBench(Mb, 0.6f, TubFloor, 0.5f, -0.72f, cloth, 0.55f, 14f);
            CarInterior.Dash(Mb, 0.72f, 1.06f, 0.68f, 0.86f, 0.5f, -0.35f, Lib["Car_Dash"]);
            CarInterior.SteeringWheel(Mb, new Vector3(-0.35f, 0.84f, 0.52f), new Vector3(0, 0.42f, 1f), 0.2f, Lib["Car_Dash"], 4);
            CarInterior.Console(Mb, -0.3f, 0.68f, TubFloor, 0.2f, Lib["Car_Dash"], Lib["Car_LeatherBlack"]);
            Mb.Box(new Vector3(0, 0.885f, -1.2f), new Vector3(1.4f, 0.02f, 0.4f), dark); // parcel shelf
        }

        protected override void Details()
        {
            Front();
            Sides();
            Rear();
        }

        private void Front()
        {
            // black panel across the leaning nose, four equal round lamps and the chrome kidneys projected onto it
            foreach (float s in new[] { -1f, 1f })
            {
                Body.ProjectedLamp(Mb, CarLoft.RectAt(0.355f, 0.6f, 0.71f, 0.19f, 0.01f), 1f, s, Plastic, Seam, 0.004f, 0.003f);
                foreach (float x in new[] { 0.4f, 0.58f })
                    if (Body.Project(s * x, 0.6f, 1f, out var lp, out var ln))
                        CarKit.RoundLamp(Mb, lp + ln * 0.004f, ln, 0.074f, Chrome, Lens);
                if (Body.Project(s * 0.07f, 0.6f, 1f, out var kp, out var kn))
                {
                    Vector3 u = Vector3.right * s;
                    CarKit.Lamp(Mb, kp + kn * 0.003f, kn, u, CarKit.Rect(0.12f, 0.2f, 0.035f), Chrome, Chrome, 0.012f, 0.0f);
                    CarKit.Lamp(Mb, kp + kn * 0.016f, kn, u, CarKit.Rect(0.1f, 0.18f, 0.03f), Grille, Chrome, 0.002f, 0.0f);
                    for (int i = -2; i <= 2; i++)
                        Mb.Box(kp + kn * 0.019f + u * (i * 0.018f), new Vector3(0.005f, 0.17f, 0.006f), Chrome, Quaternion.LookRotation(kn, Vector3.up));
                }
                // turn signals in the bumper ends, repeater on the wing
                CarKit.Lamp(Mb, new Vector3(s * 0.62f, 0.4f, ZF + 0.06f), Vector3.forward, Vector3.right * s, CarKit.Rect(0.16f, 0.055f, 0.01f), Amber, Plastic, 0.004f, 0.01f);
                var (rp, rn) = Side(1.86f, 0.62f, s);
                CarKit.Lamp(Mb, rp, rn, Vector3.forward, CarKit.Rect(0.06f, 0.03f, 0.008f), Amber, Plastic, 0.004f, 0.02f);
                Pm.Blinkers.Add(new Blinker(rp + rn * 0.004f, rn, new Vector2(0.06f, 0.03f)));
            }
            BlinkerPair(new Vector3(0.62f, 0.4f, ZF + 0.066f), Vector3.forward, new Vector2(0.16f, 0.055f));
            Bumper(1f);
            var (bp, bn) = Top(2.08f, 0.001f);
            CarBadges.Bmw(Mb, bp + bn * 0.004f, bn, 0.035f);
            CarKit.Plate(Mb, new Vector3(0, 0.39f, ZF + 0.088f), Vector3.forward, "WE 320E3", 0.9f);
            // bonnet edges along the wings, cowl line
            SeamTop(1.12f, 2.12f, t => 0.74f);
            SeamAcross(1.12f, 0.74f);
            Wipers(1.04f, 0.1f, 0.55f, 0.05f, 0.62f);
        }

        /// <summary>Chrome bumper bar with a black rubber strip, black corner caps wrapping to the wheel arches.</summary>
        private void Bumper(float dir)
        {
            float z = dir * (2.1625f + 0.045f);
            Mb.Box(new Vector3(0, 0.39f, z), new Vector3(1.24f, 0.13f, 0.07f), Chrome);
            Mb.Box(new Vector3(0, 0.39f, z + dir * 0.036f), new Vector3(1.2f, 0.045f, 0.006f), Plastic);
            Mb.Box(new Vector3(0, 0.32f, z - dir * 0.02f), new Vector3(1.4f, 0.035f, 0.06f), Plastic);
            foreach (float s in new[] { -1f, 1f })
            {
                CarBlob.Blob(Mb, new Vector3(s * 0.7f, 0.39f, z - dir * 0.04f), Quaternion.Euler(0, s * dir * 35f, 0) * Quaternion.Euler(90f, 0, 0),
                    new Vector3(0.12f, 0.09f, 0.066f), 0.35f, Plastic, 0.35f, 1f, 16, 8);
                Mb.Box(new Vector3(s * 0.818f, 0.39f, dir * 1.93f), new Vector3(0.02f, 0.1f, 0.34f), Plastic);
            }
        }

        private void Sides()
        {
            SeamSide(1.1f, 0.22f, 0.84f);
            SeamSide(-0.17f, 0.22f, 0.87f);
            SeamSide(-1.02f, 0.22f, 0.875f);
            // black door sail under the mirror, Hofmeister kink at the foot of the C-pillar
            GlassOverlay(0.9f, 1.08f, t => new Vector2(0f, 0.42f * t), Trim, 6);
            GlassOverlay(-1.0f, -0.9f, t => new Vector2(0f, 0.25f * (1f - t)), Paint, 6);
            GlassOverlay(-1.0f, -0.8f, t => new Vector2(0.25f + 0.75f * t, 1.02f), Paint, 10);
            foreach (float s in new[] { -1f, 1f })
            {
                Body.PatchY(Mb, s, -1.5f, 1.05f, t => new Vector2(0.48f, 0.52f), BLip, BShoulder, 0.006f, Plastic, 14, 2);
                foreach (float z in new[] { -0.05f, -0.92f })
                {
                    var (hp, hn) = Side(z, 0.79f, s);
                    CarKit.Handle(Mb, hp, hn, Chrome, 0.12f);
                }
                var (mp, _) = Side(1.0f, 0.86f, s);
                CarKit.Mirror(Mb, mp, s, Plastic, 0.06f, 0.16f, 0.1f, 0.05f);
            }
            var (fp, fn) = Side(-1.6f, 0.8f, 1f);
            CarWheels.Lathe(Mb, fp, fn, new[] { new Vector2(0.06f, 0.0005f), new Vector2(0.055f, 0.0012f), new Vector2(0f, 0.0012f) }, 18, Seam);
            CarWheels.Lathe(Mb, fp + fn * 0.0013f, fn, new[] { new Vector2(0.053f, 0f), new Vector2(0f, 0f) }, 18, Paint);
        }

        private void Rear()
        {
            Vector3 b = Vector3.back;
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 u = Vector3.right * s;
                Vector3 c = new Vector3(s * 0.57f, 0.69f, ZR);
                CarKit.Lamp(Mb, c, b, u, CarKit.Rect(0.4f, 0.16f, 0.012f), Tail, Chrome, 0.006f, 0.02f);
                CarKit.Lamp(Mb, c + new Vector3(s * -0.13f, 0.035f, -0.006f), b, u, CarKit.Rect(0.12f, 0.065f, 0.006f), Amber, Trim, 0.002f, 0f);
                CarKit.Lamp(Mb, c + new Vector3(0f, 0.035f, -0.006f), b, u, CarKit.Rect(0.1f, 0.065f, 0.006f), Lib["Car_Reverse"], Trim, 0.002f, 0f);
                for (int i = 0; i < 6; i++)
                    Mb.Box(c + new Vector3(0, -0.07f + i * 0.028f, -0.0075f), new Vector3(0.39f, 0.003f, 0.002f), Lib["Car_TailDark"]);
            }
            BlinkerPair(new Vector3(0.44f, 0.725f, ZR - 0.012f), b, new Vector2(0.12f, 0.065f));
            CarKit.Lamp(Mb, new Vector3(0, 0.69f, ZR), b, Vector3.right, CarKit.Rect(0.7f, 0.16f, 0.012f), Plastic, Seam, 0.002f, 0.02f);
            CarKit.Plate(Mb, new Vector3(0, 0.69f, ZR - 0.006f), b, "WE 320E3", 0.95f);
            CarBadges.Bmw(Mb, new Vector3(0, 0.82f, ZR - 0.002f), b, 0.035f);
            Mb.Text("320i", new Vector3(-0.55f, 0.82f, ZR - 0.004f), b, Vector3.up, 0.035f, Lib["Text_White"]);
            Bumper(-1f);
            CarKit.Exhaust(Mb, new Vector3(-0.5f, 0.26f, ZR + 0.1f), 0.03f, 0.18f);
            // Evo-style boot wing in body colour with a black adjustable flap
            CarWings.BootWing(Mb, new Vector3(0f, 0.95f, -2.0f), 0.7f, 0.2f, 0.58f, 0.885f, Paint, Lib["Car_WingBlack"]);
            SeamAcross(-1.45f, 0.7f);
            SeamTop(-2.12f, -1.45f, t => 0.7f);
        }
    }
}
