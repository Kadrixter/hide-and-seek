using UnityEngine;
using static HideAndSeek.Map.Editor.CarShapes;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// BMW 3 Series E30 sedan, pre-facelift (1982-87): upright three-box body with a crisp waist crease, flat nose
    /// with a black panel carrying four equal round lamps and the chrome-framed kidneys, chrome bumpers with a
    /// rubber strip, chrome window surrounds, black B-pillar, ribbed tail lamps, BBS cross-spoke wheels.
    /// 4.33 x 1.65 x 1.38 m, wheelbase 2.57 m.
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
        protected override float[] BodyBreaks => new[] { 0.66f, -0.13f, -0.95f };
        protected override int[] SharpRings => new[] { BMax };
        protected override float RoofRound => 0.6f;

        private const float ZF = 2.1625f, ZR = -2.1625f;

        private static BodyKey K(float z, float yBot, float wBot, float wMax, float yMax, float wSh, float ySh, float yTop,
            float tub = 0f, float round = 0.35f) => new BodyKey(z, yBot, wBot, wMax, yMax, wSh, ySh, yTop, round, tub);

        protected override BodyKey[] BodyKeys() => new[]
        {
            K(ZF, 0.27f, 0.74f, 0.76f, 0.6f, 0.74f, 0.73f, 0.74f),
            K(2.14f, 0.22f, 0.785f, 0.805f, 0.62f, 0.785f, 0.74f, 0.75f),
            K(2.05f, 0.19f, 0.8f, 0.82f, 0.66f, 0.8f, 0.755f, 0.765f),
            K(1.41f, 0.17f, 0.8f, 0.8225f, 0.7f, 0.8f, 0.82f, 0.82f),
            K(0.75f, 0.17f, 0.8f, 0.8225f, 0.7f, 0.795f, 0.87f, 0.86f),
            K(0.62f, 0.17f, 0.8f, 0.8225f, 0.7f, 0.795f, 0.875f, 0.87f, 1f),
            K(-0.98f, 0.17f, 0.8f, 0.8225f, 0.7f, 0.795f, 0.895f, 0.89f, 1f),
            K(-1.1f, 0.17f, 0.8f, 0.8225f, 0.7f, 0.795f, 0.9f, 0.9f),
            K(-2.08f, 0.2f, 0.8f, 0.82f, 0.7f, 0.79f, 0.9f, 0.905f),
            K(-2.14f, 0.23f, 0.785f, 0.805f, 0.7f, 0.775f, 0.895f, 0.9f),
            K(ZR, 0.27f, 0.76f, 0.78f, 0.7f, 0.755f, 0.885f, 0.89f),
        };

        protected override CabinKey[] CabinKeys() => new[]
        {
            new CabinKey(0.72f, 0.875f, 0.75f, 0.875f, 0.62f),
            new CabinKey(0.06f, 0.89f, 0.76f, 1.365f, 0.64f),
            new CabinKey(-0.95f, 0.9f, 0.76f, 1.37f, 0.64f),
            new CabinKey(-1.08f, 0.9f, 0.755f, 1.345f, 0.635f),
            new CabinKey(-1.56f, 0.905f, 0.72f, 0.905f, 0.6f),
        };

        protected override float[] CabinBreaks => new[] { 0.09f, -0.06f, -0.17f, -0.9f, -1.02f, 0.68f };

        protected override Material CabinMat(int seg, float z)
        {
            bool front = z < 0.7f && z > -0.06f, rear = z < -0.17f && z > -0.9f, glass = front || rear;
            switch (seg)
            {
                case 0: return glass ? Chrome : Paint;                    // chrome waist trim
                case 1:
                case 2: return glass ? Glass : (z > -0.17f ? Trim : Paint); // black B-pillar, body C-pillar
                case 3: return glass ? Chrome : Paint;                    // chrome window surround
                case 4: return Paint;
                default:
                    if (z > 0.09f) return z > 0.66f || z < 0.12f ? Trim : Glass;
                    if (z < -1.02f) return z < -1.52f || z > -1.06f ? Trim : Glass;
                    return Paint;
            }
        }

        protected override void Interior()
        {
            var cloth = Lib["Car_InteriorGrey"];
            var dark = Lib["Car_Interior"];
            foreach (float s in new[] { -1f, 1f })
                CarInterior.Seat(Mb, new Vector3(s * 0.35f, TubFloor, -0.12f), 0.5f, 0.5f, cloth, dark, false, 14f, 0.6f);
            CarInterior.RearBench(Mb, 0.6f, TubFloor, 0.5f, -0.88f, cloth, 0.55f, 14f);
            CarInterior.Dash(Mb, 0.72f, 0.7f, 0.34f, 0.86f, 0.5f, -0.35f, Lib["Car_Dash"]);
            CarInterior.SteeringWheel(Mb, new Vector3(-0.35f, 0.84f, 0.2f), new Vector3(0, 0.42f, 1f), 0.2f, Lib["Car_Dash"], 4);
            CarInterior.Console(Mb, -0.5f, 0.34f, TubFloor, 0.2f, Lib["Car_Dash"], Lib["Car_LeatherBlack"]);
            Mb.Box(new Vector3(0, 0.905f, -1.3f), new Vector3(1.4f, 0.02f, 0.45f), dark); // parcel shelf
        }

        protected override void Details()
        {
            Front();
            Sides();
            Rear();
        }

        private void Roundel(Vector3 c, Vector3 n, float r)
        {
            CarWheels.Lathe(Mb, c, n, new[] { new Vector2(r, 0f), new Vector2(r, 0.004f), new Vector2(r * 0.8f, 0.006f) }, 20, Chrome);
            CarWheels.Lathe(Mb, c + n * 0.0055f, n, new[] { new Vector2(r * 0.8f, 0f), new Vector2(r * 0.55f, 0.0005f) }, 20, Plastic);
            var rot = Quaternion.LookRotation(n, Vector3.up);
            float q = r * 0.27f;
            for (int i = 0; i < 4; i++)
            {
                Vector3 off = rot * new Vector3((i % 2 == 0 ? 1 : -1) * q, (i < 2 ? 1 : -1) * q, 0.007f);
                Mb.Box(c + off, new Vector3(q * 2f, q * 2f, 0.001f), (i == 0 || i == 3) ? Lib["Car_BadgeBlue"] : Lib["Car_Plate"], rot);
            }
        }

        private void Front()
        {
            Vector3 n = Vector3.forward;
            // black lamp panel across the nose, four equal round lamps, chrome-framed kidneys with vertical bars
            CarKit.Lamp(Mb, new Vector3(0, 0.6f, ZF), n, Vector3.right, CarKit.Rect(1.42f, 0.22f, 0.02f), Plastic, Plastic, 0.004f, 0.02f);
            foreach (float s in new[] { -1f, 1f })
            {
                foreach (float x in new[] { 0.4f, 0.58f })
                    CarKit.RoundLamp(Mb, new Vector3(s * x, 0.6f, ZF + 0.004f), n, 0.074f, Chrome, Lens);
                Vector3 kc = new Vector3(s * 0.07f, 0.6f, ZF + 0.004f);
                CarKit.Lamp(Mb, kc, n, Vector3.right * s, CarKit.Rect(0.12f, 0.2f, 0.035f), Chrome, Chrome, 0.012f, 0.0f);
                CarKit.Lamp(Mb, kc + n * 0.013f, n, Vector3.right * s, CarKit.Rect(0.1f, 0.18f, 0.03f), Grille, Chrome, 0.002f, 0.0f);
                for (int i = -2; i <= 2; i++)
                    Mb.Box(kc + new Vector3(i * 0.018f, 0, 0.016f), new Vector3(0.005f, 0.17f, 0.006f), Chrome);
                // turn signals in the bumper ends, side marker on the wing
                CarKit.Lamp(Mb, new Vector3(s * 0.62f, 0.4f, ZF + 0.06f), n, Vector3.right * s, CarKit.Rect(0.16f, 0.055f, 0.01f), Amber, Plastic, 0.004f, 0.01f);
                BlinkerPair(new Vector3(0.62f, 0.4f, ZF + 0.066f), n, new Vector2(0.16f, 0.055f));
                var (rp, rn) = Side(1.86f, 0.62f, s);
                CarKit.Lamp(Mb, rp, rn, Vector3.forward, CarKit.Rect(0.06f, 0.03f, 0.008f), Amber, Plastic, 0.004f, 0.02f);
                Pm.Blinkers.Add(new Blinker(rp + rn * 0.004f, rn, new Vector2(0.06f, 0.03f)));
            }
            Bumper(1f);
            Roundel(new Vector3(0, 0.765f, ZF - 0.06f) + Vector3.up * 0.002f, new Vector3(0, 0.75f, 0.66f).normalized, 0.035f);
            CarKit.Plate(Mb, new Vector3(0, 0.39f, ZF + 0.088f), n, "WE 320E3", 0.9f);
            SeamTop(0.76f, 2.12f, t => 0.74f);
            SeamAcross(0.76f, 0.74f);
            Wipers(0.68f, 0.1f, 0.55f, 0.05f, 0.62f);
        }

        /// <summary>Chrome bumper bar with a black rubber strip, black corner caps wrapping to the wheel arches.</summary>
        private void Bumper(float dir)
        {
            float z = dir * (2.1625f + 0.045f);
            Vector3 n = Vector3.forward * dir;
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
            SeamSide(0.66f, 0.22f, 0.87f);
            SeamSide(-0.13f, 0.22f, 0.89f);
            SeamSide(-0.95f, 0.22f, 0.9f);
            foreach (float s in new[] { -1f, 1f })
            {
                Body.PatchY(Mb, s, -1.5f, 1.05f, t => new Vector2(0.48f, 0.52f), BLip, BShoulder, 0.006f, Plastic, 14, 2);
                foreach (float z in new[] { -0.02f, -0.83f })
                {
                    var (hp, hn) = Side(z, 0.79f, s);
                    CarKit.Handle(Mb, hp, hn, Chrome, 0.12f);
                }
                var (mp, _) = Side(0.62f, 0.875f, s);
                CarKit.Mirror(Mb, mp, s, Plastic, 0.06f, 0.16f, 0.1f, 0.05f);
            }
            var (fp, fn) = Side(-1.5f, 0.8f, 1f);
            CarWheels.Lathe(Mb, fp, fn, new[] { new Vector2(0.06f, 0.0005f), new Vector2(0.055f, 0.0012f), new Vector2(0f, 0.0012f) }, 18, Seam);
            CarWheels.Lathe(Mb, fp + fn * 0.0013f, fn, new[] { new Vector2(0.053f, 0f), new Vector2(0f, 0f) }, 18, Paint);
        }

        private void Rear()
        {
            Vector3 b = Vector3.back;
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 u = Vector3.right * s;
                Vector3 c = new Vector3(s * 0.57f, 0.68f, ZR);
                CarKit.Lamp(Mb, c, b, u, CarKit.Rect(0.4f, 0.17f, 0.012f), Tail, Chrome, 0.006f, 0.02f);
                CarKit.Lamp(Mb, c + new Vector3(s * -0.13f, 0.04f, -0.006f), b, u, CarKit.Rect(0.12f, 0.07f, 0.006f), Amber, Trim, 0.002f, 0f);
                CarKit.Lamp(Mb, c + new Vector3(s * 0.0f, 0.04f, -0.006f), b, u, CarKit.Rect(0.1f, 0.07f, 0.006f), Lib["Car_Reverse"], Trim, 0.002f, 0f);
                for (int i = 0; i < 6; i++)
                    Mb.Box(c + new Vector3(0, -0.075f + i * 0.03f, -0.0075f), new Vector3(0.39f, 0.003f, 0.002f), Lib["Car_TailDark"]);
                BlinkerPair(new Vector3(0.44f, 0.72f, ZR - 0.012f), b, new Vector2(0.12f, 0.07f));
            }
            CarKit.Lamp(Mb, new Vector3(0, 0.68f, ZR), b, Vector3.right, CarKit.Rect(0.7f, 0.17f, 0.012f), Plastic, Seam, 0.002f, 0.02f);
            CarKit.Plate(Mb, new Vector3(0, 0.68f, ZR - 0.006f), b, "WE 320E3", 0.95f);
            Roundel(new Vector3(0, 0.83f, ZR - 0.002f), b, 0.035f);
            Mb.Text("320i", new Vector3(-0.55f, 0.83f, ZR - 0.004f), b, Vector3.up, 0.035f, Lib["Text_White"]);
            Bumper(-1f);
            CarKit.Exhaust(Mb, new Vector3(-0.5f, 0.26f, ZR + 0.1f), 0.03f, 0.18f);
            SeamAcross(-1.6f, 0.7f);
            SeamTop(-2.12f, -1.6f, t => 0.7f);
        }
    }
}
