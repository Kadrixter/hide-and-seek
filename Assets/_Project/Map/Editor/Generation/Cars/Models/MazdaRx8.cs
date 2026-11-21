using UnityEngine;
using static HideAndSeek.Map.Editor.CarShapes;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// Mazda RX-8 (SE3P): five-point grille, swept headlamps, wings standing proud of the bonnet, gills behind the
    /// front wheels, freestyle doors (no visible B-pillar), arched coupe roof, round lamps in clear tail clusters.
    /// 4.43 x 1.77 x 1.34 m, wheelbase 2.70 m.
    /// </summary>
    internal sealed class MazdaRx8 : SportCar
    {
        public override string Name => "MazdaRx8";
        public override string DefaultPaint => "CarPaint_MontegoBlue";
        public override float Length => 4.43f;
        protected override Arch FrontArch => new Arch(1.355f, 0.33f, 0.365f);
        protected override Arch RearArch => new Arch(-1.345f, 0.33f, 0.365f);
        protected override float TireRadius => 0.33f;
        protected override float TireWidth => 0.225f;
        protected override float RimRadius => 0.235f;
        protected override RimStyle Rim => RimStyle.TenSpoke;
        protected override float TubWall => 0.68f;
        protected override float TubFloor => 0.24f;
        protected override float[] BodyBreaks => new[] { 0.62f, -0.2f, -0.9f };
        protected override int[] SharpRings => new[] { BMax };

        private const float ZF = 2.215f, ZR = -2.215f;

        private static BodyKey K(float z, float yBot, float wBot, float wMax, float yMax, float wSh, float ySh, float yTop,
            float tub = 0f, float round = 0.7f) => new BodyKey(z, yBot, wBot, wMax, yMax, wSh, ySh, yTop, round, tub);

        // wings stand well above the bonnet; a crease (BMax) climbs from the front wheel to the tail lamps
        protected override BodyKey[] BodyKeys() => new[]
        {
            K(ZF, 0.24f, 0.5f, 0.52f, 0.34f, 0.46f, 0.44f, 0.45f, 0f, 1f),
            K(2.19f, 0.19f, 0.64f, 0.66f, 0.36f, 0.6f, 0.52f, 0.52f, 0f, 1f),
            K(2.12f, 0.16f, 0.74f, 0.76f, 0.38f, 0.71f, 0.6f, 0.57f),
            K(1.95f, 0.15f, 0.83f, 0.85f, 0.42f, 0.81f, 0.7f, 0.63f),
            K(1.6f, 0.15f, 0.865f, 0.885f, 0.47f, 0.84f, 0.8f, 0.66f),
            K(1.355f, 0.15f, 0.87f, 0.89f, 0.5f, 0.85f, 0.845f, 0.69f),
            K(1.0f, 0.15f, 0.865f, 0.885f, 0.53f, 0.84f, 0.865f, 0.79f),
            K(0.75f, 0.15f, 0.86f, 0.88f, 0.54f, 0.835f, 0.885f, 0.875f),
            K(0.6f, 0.15f, 0.86f, 0.88f, 0.545f, 0.835f, 0.89f, 0.88f, 1f),
            K(-0.98f, 0.15f, 0.865f, 0.885f, 0.6f, 0.83f, 0.94f, 0.94f, 1f),
            K(-1.12f, 0.15f, 0.87f, 0.885f, 0.6f, 0.83f, 0.95f, 0.96f),
            K(-1.345f, 0.15f, 0.87f, 0.885f, 0.61f, 0.82f, 0.96f, 0.97f),
            // boot lid dished (centre below the sides), the boot face stands back from a bumper with a shelf on top
            K(-1.9f, 0.18f, 0.83f, 0.85f, 0.62f, 0.77f, 0.975f, 0.965f),
            K(-2.06f, 0.22f, 0.75f, 0.77f, 0.62f, 0.69f, 0.985f, 0.972f),
            K(-2.12f, 0.24f, 0.69f, 0.71f, 0.6f, 0.63f, 0.97f, 0.98f, 0f, 0.8f),
            K(-2.165f, 0.26f, 0.62f, 0.64f, 0.58f, 0.57f, 0.88f, 0.9f, 0f, 1f),
            K(-2.185f, 0.27f, 0.58f, 0.6f, 0.56f, 0.53f, 0.72f, 0.73f, 0f, 0.6f),
            K(-2.2f, 0.28f, 0.55f, 0.57f, 0.5f, 0.5f, 0.69f, 0.7f, 0f, 0.4f),
            K(ZR, 0.31f, 0.5f, 0.52f, 0.48f, 0.46f, 0.66f, 0.67f, 0f, 0.8f),

        };

        protected override CabinKey[] CabinKeys() => new[]
        {
            new CabinKey(0.68f, 0.885f, 0.74f, 0.885f, 0.6f),
            new CabinKey(-0.05f, 0.91f, 0.75f, 1.33f, 0.6f),
            new CabinKey(-0.5f, 0.93f, 0.74f, 1.335f, 0.6f),
            new CabinKey(-1.05f, 0.95f, 0.71f, 1.2f, 0.57f),
            new CabinKey(-1.55f, 0.97f, 0.64f, 0.98f, 0.53f),
        };

        protected override float[] CabinBreaks => new[] { -0.02f, -0.55f, -0.6f, -1.1f, 0.62f, -1.03f };

        protected override Material CabinMat(int seg, float z)
        {
            bool glassSide = z > -1.1f && (z > -0.55f || z < -0.6f);
            switch (seg)
            {
                case 0: return Trim;
                case 1:
                case 2: return glassSide ? Glass : Trim; // thin black seal between the freestyle doors' glass
                case 3: return z > -1.1f ? Trim : Paint;
                case 4: return Paint;
                default:
                    if (z > -0.02f) return z > 0.62f || z < 0.02f ? Trim : Glass;
                    if (z < -1.03f) return z < -1.5f ? Trim : Glass;
                    return Paint;
            }
        }

        protected override void Interior()
        {
            var seat = Lib["Car_LeatherBlack"];
            var red = Lib["Car_LeatherRed"];
            foreach (float s in new[] { -1f, 1f })
            {
                CarInterior.Seat(Mb, new Vector3(s * 0.35f, TubFloor, -0.12f), 0.5f, 0.42f, seat, red, true, 18f, 0.6f);
                CarInterior.Seat(Mb, new Vector3(s * 0.33f, TubFloor, -0.98f), 0.46f, 0.42f, seat, red, true, 26f, 0.5f);
            }
            CarInterior.Dash(Mb, 0.68f, 0.65f, 0.28f, 0.84f, 0.5f, -0.35f, Lib["Car_Dash"]);
            CarInterior.SteeringWheel(Mb, new Vector3(-0.35f, 0.82f, 0.13f), new Vector3(0, 0.42f, 1f), 0.18f, Lib["Car_LeatherBlack"]);
            CarInterior.Console(Mb, -1.2f, 0.28f, TubFloor, 0.26f, Lib["Car_Dash"], Lib["Car_ChromeBright"]);
        }

        protected override void Details()
        {
            Front();
            Sides();
            Rear();
        }

        private void Front()
        {
            // five-sided intake pointing down, Mazda badge above it, slanted lamps with a sharp rear tip
            var pent = new[] { new Vector2(0f, 0.32f), new Vector2(0.26f, 0.37f), new Vector2(0.31f, 0.44f), new Vector2(0.27f, 0.5f), new Vector2(0f, 0.5f) };
            var head = new[] { new Vector2(0.33f, 0.47f), new Vector2(0.56f, 0.475f), new Vector2(0.72f, 0.51f), new Vector2(0.8f, 0.58f),
                new Vector2(0.815f, 0.665f), new Vector2(0.68f, 0.605f), new Vector2(0.4f, 0.535f) };
            foreach (float s in new[] { -1f, 1f })
            {
                Body.ProjectedLamp(Mb, pent, 1f, s, Grille, Seam, 0.008f, 0.002f);
                Body.ProjectedPatch(Mb, CarLoft.PolyAt(0f, 0f, new Vector2(0f, 0.415f), new Vector2(0.29f, 0.415f), new Vector2(0.29f, 0.425f), new Vector2(0f, 0.425f)), 1f, s, 0.005f, Plastic, 2);
                Body.ProjectedLamp(Mb, head, 1f, s, Plastic, Seam);
                Body.ProjectedPatch(Mb, CarLoft.PolyAt(0f, 0f, new Vector2(0.42f, 0.485f), new Vector2(0.7f, 0.52f), new Vector2(0.78f, 0.6f), new Vector2(0.66f, 0.58f), new Vector2(0.45f, 0.53f)), 1f, s, 0.006f, Lib["Car_Reflector"]);
                foreach (var (x, y, r) in new[] { (0.48f, 0.507f, 0.036f), (0.62f, 0.54f, 0.034f) })
                    if (Body.Project(s * x, y, 1f, out var lp, out var ln)) CarKit.RoundLamp(Mb, lp + ln * 0.006f, ln, r, Chrome, Lens);
                Body.ProjectedPatch(Mb, CarLoft.PolyAt(0f, 0f, new Vector2(0.72f, 0.57f), new Vector2(0.79f, 0.59f), new Vector2(0.805f, 0.65f), new Vector2(0.74f, 0.61f)), 1f, s, 0.008f, Amber);
                if (Body.Project(s * 0.765f, 0.6f, 1f, out var ap, out var an)) Pm.Blinkers.Add(new Blinker(ap + an * 0.008f, an, new Vector2(0.06f, 0.035f)));
                Body.ProjectedPatch(Mb, head, 1f, s, 0.026f, Glass);
                // triangular fog intakes at the bumper corners
                var tri = new[] { new Vector2(0.42f, 0.25f), new Vector2(0.72f, 0.26f), new Vector2(0.71f, 0.37f) };
                Body.ProjectedLamp(Mb, tri, 1f, s, Grille, Seam, 0.008f, 0.002f);
                if (Body.Project(s * 0.63f, 0.29f, 1f, out var fp, out var fn)) CarKit.RoundLamp(Mb, fp + fn * 0.004f, fn, 0.032f, Chrome, Lens);
                // gills behind the front wheel
                for (int i = 0; i < 3; i++)
                {
                    float z = 0.86f - i * 0.06f;
                    Body.PatchY(Mb, s, z - 0.012f, z + 0.012f, t => new Vector2(0.56f + i * 0.02f, 0.7f), BLip, BShoulder, 0.0015f, Well, 1, 4);
                }
                var (rp, rn) = Side(0.88f, 0.75f, s);
                CarKit.Lamp(Mb, rp, rn, Vector3.forward, CarKit.Rect(0.06f, 0.02f, 0.008f), Amber, Plastic, 0.004f, 0.02f);
                Pm.Blinkers.Add(new Blinker(rp + rn * 0.004f, rn, new Vector2(0.06f, 0.02f)));
            }
            Body.Project(0f, 0.535f, 1f, out var bc, out var bn);
            CarBadges.Mazda(Mb, bc + bn * 0.004f, bn, 0.04f);
            Body.Project(0f, 0.27f, 1f, out var pc, out var pn);
            CarKit.Plate(Mb, pc + pn * 0.014f, pn, "WX RX8 03", 0.85f);
            SeamTop(0.72f, 2.08f, t => Mathf.Lerp(0.6f, 0.48f, t));
            SeamAcross(0.72f, 0.6f);
            Wipers(0.64f, 0.1f, 0.52f, 0.08f, 0.62f);
        }

        private void Sides()
        {
            // front door + rear-hinged freestyle door (no handle, only its shut line), mirrors, fuel flap
            SeamSide(0.62f, 0.22f, 0.88f);
            SeamSide(-0.2f, 0.22f, 0.92f);
            SeamSide(-0.9f, 0.22f, 0.94f);
            foreach (float s in new[] { -1f, 1f })
            {
                Body.PatchY(Mb, s, -0.9f, 0.62f, t => new Vector2(0.285f, 0.29f), BLip, BShoulder, 0.0012f, Seam, 12, 1);
                var (hp, hn) = Side(-0.1f, 0.8f, s);
                CarKit.Handle(Mb, hp, hn, Chrome, 0.12f);
                var (mp, _) = Side(0.55f, 0.89f, s);
                CarKit.Mirror(Mb, mp, s, Paint, 0.07f, 0.18f, 0.1f, 0.05f);
            }
            var (fp, fn) = Side(-1.55f, 0.82f, 1f);
            CarWheels.Lathe(Mb, fp, fn, new[] { new Vector2(0.06f, 0.0005f), new Vector2(0.055f, 0.0012f), new Vector2(0f, 0.0012f) }, 18, Seam);
            CarWheels.Lathe(Mb, fp + fn * 0.0013f, fn, new[] { new Vector2(0.053f, 0f), new Vector2(0f, 0f) }, 18, Paint);
        }

        private void Rear()
        {
            // silver housings with big round red lamps, wrapping round the corner onto the wing
            var housing = new[] { new Vector2(0.26f, 0.775f), new Vector2(0.58f, 0.75f), new Vector2(0.71f, 0.785f), new Vector2(0.73f, 0.87f), new Vector2(0.28f, 0.885f) };
            foreach (float s in new[] { -1f, 1f })
            {
                Body.ProjectedLamp(Mb, housing, -1f, s, Lib["Car_RimSilver"], Seam, 0.008f, 0.004f, 0.1f);
                foreach (var (x, r, m) in new[] { (0.55f, 0.055f, Tail), (0.4f, 0.05f, s > 0 ? Amber : Tail) })
                    if (Body.Project(s * x, 0.818f, -1f, out var lp, out var ln))
                    {
                        CarKit.Lamp(Mb, lp + ln * 0.004f, ln, Vector3.right * s, CarKit.Circle(r + 0.01f, 22), Plastic, Plastic, 0.002f, 0.004f);
                        CarKit.RoundLamp(Mb, lp + ln * 0.006f, ln, r, Chrome, m);
                    }
                if (Body.Project(s * 0.4f, 0.818f, -1f, out var bp, out var bn)) Pm.Blinkers.Add(new Blinker(bp + bn * 0.022f, bn, new Vector2(0.08f, 0.08f)));
                Body.ProjectedPatch(Mb, housing, -1f, s, 0.012f, Glass, 4, 0.1f);
                // boot lid / bumper split along the shelf, crease across the bumper
                Body.ProjectedPatch(Mb, CarLoft.RectAt(0.33f, 0.707f, 0.66f, 0.005f, 0.001f), -1f, s, 0.0015f, Seam, 2);
                Body.ProjectedPatch(Mb, CarLoft.RectAt(0.38f, 0.455f, 0.76f, 0.004f, 0.001f), -1f, s, 0.0012f, Seam, 2);
                // reflector set in a black recess at each bumper corner
                Body.ProjectedLamp(Mb, CarLoft.RectAt(0.54f, 0.5f, 0.16f, 0.05f, 0.015f), -1f, s, Plastic, Seam, 0.006f, 0.002f);
                Body.ProjectedPatch(Mb, CarLoft.RectAt(0.54f, 0.5f, 0.13f, 0.028f, 0.01f), -1f, s, 0.005f, Lib["Car_TailDark"], 2);
                // black diffuser reaching up into the bumper
                Body.ProjectedLamp(Mb, CarLoft.RectAt(0.3f, 0.35f, 0.6f, 0.15f, 0.03f), -1f, s, Plastic, Seam, 0.006f, 0.004f);
            }
            Body.Project(0f, 0.35f, -1f, out var dc, out _);
            for (int i = -3; i <= 3; i++)
                Mb.Box(new Vector3(i * 0.15f, 0.36f, dc.z - 0.015f), new Vector3(0.012f, 0.1f, 0.06f), Plastic);
            foreach (float s in new[] { -1f, 1f })
                CarKit.Exhaust(Mb, new Vector3(s * 0.4f, 0.31f, dc.z + 0.03f), 0.045f, 0.14f);
            // plate in a recess with two plate lamps over it
            Body.Project(0f, 0.585f, -1f, out var pc, out var pn);
            CarKit.Lamp(Mb, pc, pn, Vector3.right, CarKit.Rect(0.56f, 0.15f, 0.03f), Plastic, Seam, 0.001f, 0.025f);
            CarKit.Plate(Mb, pc + pn * 0.004f, pn, "WX RX8 03", 0.95f);
            foreach (float x in new[] { -0.15f, 0.15f })
                CarKit.Lamp(Mb, pc + pn * 0.003f + new Vector3(x, 0.066f, 0f), pn, Vector3.right, CarKit.Rect(0.05f, 0.012f, 0.004f), Lens, Plastic, 0.002f, 0.004f);
            Body.Project(0f, 0.8f, -1f, out var mc, out var mn);
            CarBadges.Mazda(Mb, mc + mn * 0.004f, mn, 0.035f);
            Body.Project(0.45f, 0.73f, -1f, out var tp, out _);
            Mb.Text("RX-8", new Vector3(-0.45f, 0.735f, tp.z - 0.004f), Vector3.back, Vector3.up, 0.03f, Lib["Text_White"]);
            // boot lid dished in the middle, its trailing edge turned up into a lip spoiler
            CarBlob.Blob(Mb, new Vector3(0, 0.99f, -2.12f), Quaternion.Euler(10f, 0, 0), new Vector3(0.6f, 0.016f, 0.06f), 0.35f, Paint, 0.5f, 1f, 16, 8);
            SeamAcross(-1.6f, 0.66f);
            SeamTop(-2.1f, -1.6f, t => 0.66f);
        }
    }
}
