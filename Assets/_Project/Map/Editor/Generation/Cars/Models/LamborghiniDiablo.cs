using UnityEngine;
using static HideAndSeek.Map.Editor.CarShapes;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// Lamborghini Diablo (6.0 style): low wedge, cab-forward screen continuing the bonnet line, fenders standing
    /// above the bonnet, NACA duct on the door and a big intake behind it, louvred engine cover, rear wing,
    /// four round tail lamps in a black panel, quad exhausts. 4.47 x 2.04 x 1.11 m, wheelbase 2.65 m.
    /// </summary>
    internal sealed class LamborghiniDiablo : SportCar
    {
        public override string Name => "LamborghiniDiablo";
        public override string DefaultPaint => "CarPaint_Arancio";
        public override float Length => 4.47f;
        protected override Arch FrontArch => new Arch(1.22f, 0.33f, 0.37f);
        protected override Arch RearArch => new Arch(-1.43f, 0.335f, 0.38f);
        protected override float TireRadius => 0.33f;
        protected override float TireWidth => 0.265f;
        protected override float RimRadius => 0.235f;
        protected override RimStyle Rim => RimStyle.FiveSpoke;
        protected override Material Caliper => Lib["Car_CaliperYellow"];
        protected override float TubWall => 0.7f;
        protected override float TubFloor => 0.2f;
        protected override float[] BodyBreaks => new[] { 0.62f, -0.55f };

        private const float ZF = 2.235f, ZR = -2.235f;

        private static BodyKey K(float z, float yBot, float wBot, float wMax, float yMax, float wSh, float ySh, float yTop,
            float tub = 0f, float round = 0.6f) => new BodyKey(z, yBot, wBot, wMax, yMax, wSh, ySh, yTop, round, tub);

        protected override BodyKey[] BodyKeys() => new[]
        {
            K(ZF, 0.16f, 0.7f, 0.71f, 0.24f, 0.7f, 0.33f, 0.34f, 0, 0.5f),
            K(2.17f, 0.14f, 0.83f, 0.85f, 0.27f, 0.84f, 0.42f, 0.43f),
            K(1.98f, 0.13f, 0.91f, 0.935f, 0.3f, 0.91f, 0.52f, 0.5f),
            K(1.22f, 0.13f, 0.95f, 0.975f, 0.38f, 0.95f, 0.72f, 0.66f),
            K(0.66f, 0.13f, 0.95f, 0.98f, 0.42f, 0.94f, 0.79f, 0.76f),
            K(0.5f, 0.13f, 0.95f, 0.98f, 0.43f, 0.94f, 0.8f, 0.78f, 1f),
            K(-0.3f, 0.13f, 0.97f, 0.995f, 0.46f, 0.95f, 0.87f, 0.9f, 1f),
            K(-0.86f, 0.13f, 1.0f, 1.02f, 0.5f, 0.97f, 0.93f, 0.97f, 1f),
            K(-1.02f, 0.13f, 1.0f, 1.02f, 0.5f, 0.98f, 0.94f, 0.98f),
            K(-1.43f, 0.13f, 1.01f, 1.03f, 0.5f, 0.99f, 0.95f, 0.98f),
            K(-2.0f, 0.16f, 0.99f, 1.01f, 0.5f, 0.97f, 0.93f, 0.95f),
            K(ZR, 0.25f, 0.93f, 0.95f, 0.5f, 0.93f, 0.89f, 0.91f, 0, 1f),
        };

        protected override CabinKey[] CabinKeys() => new[]
        {
            new CabinKey(0.62f, 0.785f, 0.8f, 0.785f, 0.64f),
            new CabinKey(-0.22f, 0.87f, 0.82f, 1.1f, 0.6f),
            new CabinKey(-0.66f, 0.93f, 0.8f, 1.095f, 0.58f),
            new CabinKey(-1.0f, 0.975f, 0.74f, 0.985f, 0.54f),
        };

        protected override float[] CabinBreaks => new[] { -0.2f, -0.62f, -0.9f, 0.5f };

        protected override Material CabinMat(int seg, float z)
        {
            bool sideGlass = z > -0.62f;
            switch (seg)
            {
                case 0: return Trim;
                case 1:
                case 2: return sideGlass ? Glass : Paint;
                case 3: return sideGlass ? Trim : Paint;
                case 4: return Paint;
                default:
                    if (z > -0.2f) return z > 0.56f || z < -0.15f ? Trim : Glass; // screen with a black frit band
                    if (z < -0.9f) return Glass;                                    // small rear window
                    return Paint;
            }
        }

        protected override void Interior()
        {
            var seat = Lib["Car_LeatherBlack"];
            var bolster = Lib["Car_LeatherRed"];
            foreach (float s in new[] { -1f, 1f })
                CarInterior.Seat(Mb, new Vector3(s * 0.34f, TubFloor, -0.42f), 0.5f, 0.32f, seat, bolster, true, 24f, 0.6f);
            CarInterior.Dash(Mb, 0.7f, 0.64f, 0.18f, 0.68f, 0.36f, -0.34f, Lib["Car_Dash"], false);
            CarInterior.SteeringWheel(Mb, new Vector3(-0.34f, 0.6f, 0.0f), new Vector3(0, 0.38f, 1f), 0.17f, Lib["Car_LeatherBlack"]);
            CarInterior.Console(Mb, -0.75f, 0.2f, TubFloor, 0.24f, Lib["Car_Dash"], Lib["Car_ChromeBright"]);
            // rear bulkhead behind the seats
            Mb.Box(new Vector3(0, 0.55f, -0.88f), new Vector3(1.4f, 0.7f, 0.04f), Lib["Car_Interior"]);
        }

        protected override void Details()
        {
            Front();
            Sides();
            Rear();
        }

        private void Front()
        {
            // front fascia: wide central intake, brake ducts at the corners, small lamps, plate
            Vector3 n = Vector3.forward;
            CarKit.Lamp(Mb, new Vector3(0, 0.235f, ZF), n, Vector3.right, CarKit.Rect(0.8f, 0.1f, 0.03f), Grille, Plastic, 0.002f, 0.04f);
            Mb.Box(new Vector3(0, 0.165f, ZF - 0.02f), new Vector3(1.36f, 0.025f, 0.1f), Plastic); // splitter lip
            foreach (float s in new[] { -1f, 1f })
            {
                CarKit.Lamp(Mb, new Vector3(s * 0.55f, 0.235f, ZF - 0.002f), n, Vector3.right * s, CarKit.Poly(
                    new Vector2(-0.08f, -0.045f), new Vector2(0.1f, -0.045f), new Vector2(0.08f, 0.045f), new Vector2(-0.08f, 0.045f)), Grille, Plastic, 0.004f, 0.04f);
                var (ip, inn) = Top(2.12f, 0.6f, s);
                CarKit.Lamp(Mb, ip, inn, Vector3.right * s, CarKit.Rect(0.15f, 0.035f, 0.01f), Amber, Plastic, 0.004f, 0.02f, 0f, Vector3.forward);
                Pm.Blinkers.Add(new Blinker(ip + inn * 0.004f, inn, new Vector2(0.15f, 0.04f)));
                // headlamps: black housing under a clear cover on the bonnet corners, two projectors each
                Body.PatchX(Mb, s, 1.86f, 2.17f, t => new Vector2(Mathf.Lerp(0.5f, 0.58f, t), Mathf.Lerp(0.86f, 0.8f, t)), BShoulder, BCentre, 0.002f, Plastic, 6, 6);
                foreach (float k in new[] { 0.25f, 0.68f })
                {
                    float z = Mathf.Lerp(1.9f, 2.12f, k), x = Mathf.Lerp(0.66f, 0.7f, k);
                    var (p, pn) = Top(z, x, s);
                    CarKit.RoundLamp(Mb, p, pn, 0.05f, Chrome, Lens);
                }
                Body.PatchX(Mb, s, 1.86f, 2.17f, t => new Vector2(Mathf.Lerp(0.5f, 0.58f, t), Mathf.Lerp(0.86f, 0.8f, t)), BShoulder, BCentre, 0.024f, Glass, 6, 6);
                Body.PatchX(Mb, s, 2.17f, 2.19f, t => new Vector2(0.58f, 0.8f), BShoulder, BCentre, 0.002f, Seam, 1, 4);
            }
            // bonnet shut lines and the front plate
            SeamTop(0.7f, 2.08f, t => Mathf.Lerp(0.6f, 0.5f, t));
            SeamAcross(0.7f, 0.6f);
            SeamAcross(2.08f, 0.5f);
            CarKit.Plate(Mb, new Vector3(0, 0.235f, ZF + 0.012f), n, "KR 6V12", 0.8f);
            // single long wiper lying on the screen
            var (wp, wn) = Cabin.OnTopAtX(0.5f, 0.08f, CRoofEdge, CCentre, -1f);
            var (we, _) = Cabin.OnTopAtX(0.2f, 0.56f, CRoofEdge, CCentre, -1f);
            CarKit.Wiper(Mb, wp + wn * 0.008f, we - wp, wn, 0.66f);
        }

        private void Sides()
        {
            foreach (float s in new[] { -1f, 1f })
            {
                // NACA duct on the door and the big intake ahead of the rear wheel
                Body.PatchY(Mb, s, -0.3f, 0.3f, t => new Vector2(Mathf.Lerp(0.56f, 0.675f, t), Mathf.Lerp(0.7f, 0.69f, t)), BLip, BShoulder, 0.0015f, Well, 8, 3);
                Body.PatchY(Mb, s, -1.0f, -0.6f, t => new Vector2(Mathf.Lerp(0.36f, 0.42f, t), Mathf.Lerp(0.86f, 0.84f, t)), BLip, BShoulder, 0.002f, Well, 6, 6);
                for (int i = 0; i < 6; i++)
                {
                    float y = Mathf.Lerp(0.42f, 0.8f, i / 5f);
                    var (p, pn) = Side(-0.8f, y, s);
                    Mb.Box(p + pn * 0.002f, new Vector3(0.012f, 0.012f, 0.38f), Plastic, Quaternion.LookRotation(Vector3.forward, Vector3.up));
                }
                // repeater on the front wing
                var (rp, rn) = Side(1.7f, 0.5f, s);
                CarKit.Lamp(Mb, rp, rn, Vector3.forward, CarKit.Rect(0.06f, 0.022f, 0.008f), Amber, Plastic, 0.004f, 0.02f);
                Pm.Blinkers.Add(new Blinker(rp + rn * 0.004f, rn, new Vector2(0.06f, 0.022f)));
                // mirror at the screen base
                var (mp, _) = Side(0.45f, 0.78f, s);
                CarKit.Mirror(Mb, mp, s, Paint, 0.07f, 0.17f, 0.085f, 0.06f);
                // door lip under the intake and a hidden handle recess
                var (hp, hn) = Side(-0.55f, 0.74f, s);
                CarKit.Handle(Mb, hp, hn, Plastic, 0.1f);
            }
            // scissor door shut lines and the sill line
            SeamSide(0.62f, 0.2f, 0.8f);
            SeamSide(-0.55f, 0.2f, 0.88f);
            foreach (float s in new[] { -1f, 1f })
                Body.PatchY(Mb, s, -0.55f, 0.62f, t => new Vector2(0.27f, 0.276f), BLip, BShoulder, 0.0012f, Seam, 10, 1);
        }

        private void Rear()
        {
            Vector3 n = Vector3.back;
            // black tail panel with four round lamps, reflector strip and lettering
            CarKit.Lamp(Mb, new Vector3(0, 0.74f, ZR), n, Vector3.right, CarKit.Rect(1.82f, 0.2f, 0.03f), Plastic, Plastic, 0.003f, 0.02f);
            foreach (float s in new[] { -1f, 1f })
            {
                CarKit.RoundLamp(Mb, new Vector3(s * 0.79f, 0.74f, ZR - 0.003f), n, 0.075f, Trim, Tail);
                CarKit.RoundLamp(Mb, new Vector3(s * 0.6f, 0.74f, ZR - 0.003f), n, 0.075f, Trim, s > 0 ? Lib["Car_Reverse"] : Tail);
                Mb.Box(new Vector3(s * 0.32f, 0.74f, ZR - 0.006f), new Vector3(0.26f, 0.05f, 0.004f), Amber);
                BlinkerPair(new Vector3(0.79f, 0.74f, ZR - 0.02f), n, new Vector2(0.13f, 0.13f));
            }
            Mb.Text("LAMBORGHINI", new Vector3(0, 0.82f, ZR - 0.0065f), n, Vector3.up, 0.035f, Lib["Text_White"]);
            CarKit.Plate(Mb, new Vector3(0, 0.52f, ZR - 0.01f), n, "KR 6V12", 0.85f);
            // diffuser with fins and quad exhausts
            CarKit.Lamp(Mb, new Vector3(0, 0.34f, ZR), n, Vector3.right, CarKit.Rect(1.7f, 0.18f, 0.02f), Grille, Plastic, 0.004f, 0.06f);
            for (int i = -3; i <= 3; i++)
                Mb.Box(new Vector3(i * 0.22f, 0.34f, ZR - 0.012f), new Vector3(0.012f, 0.14f, 0.12f), Plastic);
            foreach (float x in new[] { -0.22f, -0.09f, 0.09f, 0.22f })
                CarKit.Exhaust(Mb, new Vector3(x, 0.34f, ZR - 0.03f), 0.045f, 0.12f);
            // louvred engine cover
            foreach (float s in new[] { -1f, 1f })
                Body.PatchX(Mb, s, -1.95f, -1.12f, t => new Vector2(0f, 0.52f), BShoulder, BCentre, 0.002f, Grille, 6, 4);
            for (int i = 0; i < 9; i++)
            {
                float z = Mathf.Lerp(-1.9f, -1.17f, i / 8f);
                var (p, pn) = Top(z, 0.01f);
                Mb.Box(p + pn * 0.012f, new Vector3(1.0f, 0.012f, 0.05f), Paint, Quaternion.LookRotation(Vector3.forward, pn) * Quaternion.Euler(-12f, 0, 0));
            }
            // rear wing on two struts with end plates
            float wy = 1.1f, wz = -1.98f;
            Mb.Box(new Vector3(0, wy, wz), new Vector3(1.7f, 0.035f, 0.3f), Paint, Quaternion.Euler(-6f, 0, 0));
            Mb.Box(new Vector3(0, wy + 0.02f, wz - 0.14f), new Vector3(1.7f, 0.02f, 0.03f), Paint, Quaternion.Euler(-30f, 0, 0));
            foreach (float s in new[] { -1f, 1f })
            {
                Mb.Box(new Vector3(s * 0.86f, wy - 0.02f, wz), new Vector3(0.015f, 0.12f, 0.36f), Paint);
                Mb.Box(new Vector3(s * 0.45f, (wy + 0.93f) * 0.5f, wz + 0.02f), new Vector3(0.04f, wy - 0.93f, 0.1f), Paint, Quaternion.Euler(-10f, 0, 0));
            }
            // boot / engine lid shut lines
            SeamAcross(-1.08f, 0.62f);
            SeamTop(-2.12f, -1.08f, t => 0.62f);
        }
    }
}
