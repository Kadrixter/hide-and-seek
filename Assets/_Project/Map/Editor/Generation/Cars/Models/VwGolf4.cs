using UnityEngine;
using static HideAndSeek.Map.Editor.CarShapes;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// Volkswagen Golf IV (1J, 3-door): tidy rounded box, twin round lamps under one clear cover, slatted grille
    /// with a chrome bar and the round badge, thick C-pillar, near-vertical hatch, red/white tail lamps on the
    /// corners, black rubbing strips. 4.15 x 1.73 x 1.44 m, wheelbase 2.51 m.
    /// </summary>
    internal sealed class VwGolf4 : SportCar
    {
        public override string Name => "VwGolf4";
        public override string DefaultPaint => "CarPaint_ReflexSilver";
        public override float Length => 4.15f;
        protected override Arch FrontArch => new Arch(1.215f, 0.31f, 0.345f);
        protected override Arch RearArch => new Arch(-1.295f, 0.31f, 0.345f);
        protected override float TireRadius => 0.31f;
        protected override float TireWidth => 0.205f;
        protected override float RimRadius => 0.2f;
        protected override RimStyle Rim => RimStyle.FiveSpoke;
        protected override float TubWall => 0.7f;
        protected override float TubFloor => 0.26f;
        protected override float[] BodyBreaks => new[] { 0.86f, -0.42f };

        private const float ZF = 2.075f, ZR = -2.075f;

        private static BodyKey K(float z, float yBot, float wBot, float wMax, float yMax, float wSh, float ySh, float yTop,
            float tub = 0f, float round = 1f) => new BodyKey(z, yBot, wBot, wMax, yMax, wSh, ySh, yTop, round, tub);

        protected override BodyKey[] BodyKeys() => new[]
        {
            K(ZF, 0.28f, 0.62f, 0.64f, 0.42f, 0.6f, 0.5f, 0.52f),
            K(2.055f, 0.24f, 0.72f, 0.74f, 0.43f, 0.7f, 0.6f, 0.62f),
            K(2.0f, 0.21f, 0.78f, 0.8f, 0.45f, 0.765f, 0.69f, 0.71f),
            K(1.94f, 0.2f, 0.81f, 0.83f, 0.47f, 0.79f, 0.74f, 0.765f),
            K(1.85f, 0.19f, 0.83f, 0.845f, 0.5f, 0.81f, 0.775f, 0.8f),
            K(1.6f, 0.18f, 0.845f, 0.86f, 0.53f, 0.83f, 0.82f, 0.845f),
            K(1.205f, 0.18f, 0.85f, 0.865f, 0.55f, 0.835f, 0.88f, 0.9f),
            K(0.95f, 0.18f, 0.85f, 0.865f, 0.56f, 0.835f, 0.93f, 0.95f),
            K(0.82f, 0.18f, 0.85f, 0.865f, 0.56f, 0.835f, 0.94f, 0.955f, 1f),
            K(-1.2f, 0.18f, 0.85f, 0.865f, 0.57f, 0.83f, 0.985f, 0.98f, 1f),
            K(-1.34f, 0.18f, 0.85f, 0.865f, 0.57f, 0.83f, 0.99f, 0.99f),
            K(-1.86f, 0.21f, 0.83f, 0.85f, 0.57f, 0.8f, 0.995f, 1.0f),
            K(-1.98f, 0.24f, 0.78f, 0.8f, 0.58f, 0.75f, 0.99f, 0.995f),
            K(-2.05f, 0.28f, 0.7f, 0.72f, 0.58f, 0.67f, 0.985f, 0.99f),
            K(ZR, 0.32f, 0.6f, 0.62f, 0.58f, 0.6f, 0.975f, 0.98f),
        };

        protected override CabinKey[] CabinKeys() => new[]
        {
            new CabinKey(0.95f, 0.95f, 0.77f, 0.95f, 0.63f),
            new CabinKey(0.08f, 0.975f, 0.785f, 1.425f, 0.65f),
            new CabinKey(-1.28f, 0.995f, 0.78f, 1.43f, 0.65f),
            new CabinKey(-1.56f, 1.0f, 0.76f, 1.37f, 0.63f),
            new CabinKey(-1.93f, 1.0f, 0.7f, 1.0f, 0.58f),
        };

        protected override float[] CabinBreaks => new[] { 0.1f, -0.42f, -0.47f, -1.0f, 0.9f, -1.5f };

        protected override Material CabinMat(int seg, float z)
        {
            bool door = z > -0.42f, quarter = z < -0.47f && z > -1.0f;
            switch (seg)
            {
                case 0: return Trim;
                case 1:
                case 2: return door || quarter ? Glass : (z > -0.47f ? Trim : Paint); // B-pillar trim, thick C-pillar
                case 3: return door || quarter ? Trim : Paint;
                case 4: return Paint;
                default:
                    if (z > 0.1f) return z > 0.9f || z < 0.14f ? Trim : Glass;
                    if (z < -1.5f) return z < -1.89f || z > -1.54f ? Trim : Glass; // hatch glass
                    return Paint;
            }
        }

        protected override void Interior()
        {
            var cloth = Lib["Car_Interior"];
            var grey = Lib["Car_InteriorGrey"];
            foreach (float s in new[] { -1f, 1f })
                CarInterior.Seat(Mb, new Vector3(s * 0.36f, TubFloor, 0.05f), 0.5f, 0.5f, cloth, grey, false, 14f, 0.6f);
            CarInterior.RearBench(Mb, 0.62f, TubFloor, 0.52f, -0.85f, cloth, 0.55f, 16f);
            CarInterior.Dash(Mb, 0.72f, 0.9f, 0.52f, 0.92f, 0.55f, -0.37f, Lib["Car_Dash"]);
            CarInterior.SteeringWheel(Mb, new Vector3(-0.37f, 0.9f, 0.36f), new Vector3(0, 0.45f, 1f), 0.19f, Lib["Car_Dash"], 4);
            CarInterior.Console(Mb, -0.35f, 0.52f, TubFloor, 0.2f, Lib["Car_Dash"], Lib["Car_LeatherBlack"]);
            Mb.Box(new Vector3(0, 0.97f, -1.55f), new Vector3(1.38f, 0.02f, 0.6f), Lib["Car_Dash"]); // parcel shelf
            Mb.Box(new Vector3(0, 0.6f, -1.22f), new Vector3(1.4f, 0.7f, 0.04f), Lib["Car_Interior"]);

        }

        protected override void Details()
        {
            Front();
            Sides();
            Rear();
        }

        private void Front()
        {
            // one clear cover per side over two round lamps (projected onto the rounded nose), indicator at the end
            var unit = new[] { new Vector2(0.29f, 0.575f), new Vector2(0.6f, 0.57f), new Vector2(0.7f, 0.588f), new Vector2(0.748f, 0.632f),
                new Vector2(0.742f, 0.695f), new Vector2(0.29f, 0.703f) };
            var amber = new[] { new Vector2(0.682f, 0.6f), new Vector2(0.735f, 0.634f), new Vector2(0.73f, 0.685f), new Vector2(0.682f, 0.685f) };
            foreach (float s in new[] { -1f, 1f })
            {
                Body.ProjectedLamp(Mb, unit, 1f, s, Plastic, Seam);
                foreach (var (x, r) in new[] { (0.385f, 0.056f), (0.56f, 0.05f) })
                    if (Body.Project(s * x, 0.637f, 1f, out var lp, out var ln))
                        CarKit.RoundLamp(Mb, lp + ln * 0.002f, ln, r, Lib["Car_Reflector"], Lens);
                Body.ProjectedPatch(Mb, amber, 1f, s, 0.009f, Amber);
                if (Body.Project(s * 0.708f, 0.643f, 1f, out var ap, out var an)) Pm.Blinkers.Add(new Blinker(ap + an * 0.008f, an, new Vector2(0.05f, 0.08f)));
                Body.ProjectedPatch(Mb, unit, 1f, s, 0.024f, Glass);
                // fog lamps in the bumper
                if (Body.Project(s * 0.56f, 0.34f, 1f, out var fp, out var fn))
                {
                    CarKit.Lamp(Mb, fp, fn, Vector3.right * s, CarKit.Circle(0.05f, 16), Plastic, Seam, 0.002f, 0.02f);
                    CarKit.RoundLamp(Mb, fp + fn * 0.002f, fn, 0.04f, Lib["Car_Reflector"], Lens);
                }
                var (rp, rn) = Side(1.6f, 0.72f, s);
                CarKit.Lamp(Mb, rp, rn, Vector3.forward, CarKit.Rect(0.045f, 0.025f, 0.01f), Amber, Plastic, 0.004f, 0.02f);
                Pm.Blinkers.Add(new Blinker(rp + rn * 0.004f, rn, new Vector2(0.05f, 0.025f)));
                // bumper rubbing strip wrapping round the corner, lower intake
                Body.ProjectedPatch(Mb, CarLoft.RectAt(0.42f, 0.455f, 0.84f, 0.04f, 0.008f), 1f, s, 0.006f, Plastic, 3);
                Body.ProjectedPatch(Mb, CarLoft.PolyAt(0f, 0.31f, new Vector2(0f, -0.045f), new Vector2(0.33f, -0.045f), new Vector2(0.36f, 0.045f), new Vector2(0f, 0.045f)), 1f, s, 0.003f, Grille, 3);
            }
            // grille: dark field, grey slats, chrome bar on top, round badge
            foreach (float s in new[] { -1f, 1f })
                Body.ProjectedPatch(Mb, CarLoft.RectAt(0.14f, 0.637f, 0.28f, 0.125f, 0.01f), 1f, s, 0.002f, Plastic, 2);
            Body.Project(0f, 0.637f, 1f, out var gc, out _);
            for (int i = 0; i < 4; i++)
                Mb.Box(new Vector3(0, 0.592f + i * 0.03f, gc.z + 0.006f), new Vector3(0.52f, 0.009f, 0.01f), Lib["Car_PlasticGrey"]);
            Mb.Box(new Vector3(0, 0.69f, gc.z + 0.008f), new Vector3(0.55f, 0.013f, 0.012f), Chrome);
            CarWheels.Lathe(Mb, new Vector3(0, 0.637f, gc.z + 0.01f), Vector3.forward, new[] { new Vector2(0.05f, 0f), new Vector2(0.05f, 0.006f), new Vector2(0.044f, 0.008f), new Vector2(0f, 0.008f) }, 20, Chrome);
            Mb.Text("VW", new Vector3(0, 0.637f, gc.z + 0.0185f), Vector3.forward, Vector3.up, 0.04f, Lib["Text_Black"]);
            Body.Project(0f, 0.37f, 1f, out var pc, out var pn);
            CarKit.Plate(Mb, pc + pn * 0.012f, pn, "WA 1J4GT", 0.9f);
            SeamTop(0.96f, 1.97f, t => Mathf.Lerp(0.77f, 0.7f, t));
            SeamAcross(0.96f, 0.77f);
            Wipers(0.92f, 0.1f, 0.55f, 0.06f, 0.66f);
        }

        private void Sides()
        {
            SeamSide(0.86f, 0.22f, 0.94f);
            SeamSide(-0.42f, 0.22f, 0.98f);
            foreach (float s in new[] { -1f, 1f })
            {
                Body.PatchY(Mb, s, -0.9f, 0.86f, t => new Vector2(0.47f, 0.51f), BLip, BShoulder, 0.006f, Plastic, 12, 2);
                var (hp, hn) = Side(-0.3f, 0.87f, s);
                CarKit.Handle(Mb, hp, hn, Paint, 0.13f);
                var (mp, _) = Side(0.78f, 0.95f, s);
                CarKit.Mirror(Mb, mp, s, Paint, 0.07f, 0.19f, 0.12f, 0.05f);
            }
            var (fp, fn) = Side(-1.5f, 0.84f, 1f);
            CarWheels.Lathe(Mb, fp, fn, new[] { new Vector2(0.065f, 0.0005f), new Vector2(0.06f, 0.0012f), new Vector2(0f, 0.0012f) }, 18, Seam);
            CarWheels.Lathe(Mb, fp + fn * 0.0013f, fn, new[] { new Vector2(0.058f, 0f), new Vector2(0f, 0f) }, 18, Paint);
        }

        private void Rear()
        {
            // corner lamps wrapping onto the wings: red outer part, white / amber inner part
            var tail = new[] { new Vector2(0.42f, 0.79f), new Vector2(0.7f, 0.795f), new Vector2(0.74f, 0.83f), new Vector2(0.735f, 0.93f), new Vector2(0.42f, 0.935f) };


            foreach (float s in new[] { -1f, 1f })
            {
                Body.ProjectedLamp(Mb, tail, -1f, s, Tail, Seam);
                Body.ProjectedPatch(Mb, CarLoft.RectAt(0.48f, 0.87f, 0.07f, 0.1f, 0.01f), -1f, s, 0.007f, s > 0 ? Lib["Car_Reverse"] : Lib["Car_TailDark"]);
                Body.ProjectedPatch(Mb, CarLoft.RectAt(0.56f, 0.87f, 0.07f, 0.1f, 0.01f), -1f, s, 0.007f, Amber);
                if (Body.Project(s * 0.56f, 0.87f, -1f, out var bp, out var bn)) Pm.Blinkers.Add(new Blinker(bp + bn * 0.007f, bn, new Vector2(0.07f, 0.1f)));
                Body.ProjectedPatch(Mb, CarLoft.RectAt(0.42f, 0.5f, 0.84f, 0.045f, 0.008f), -1f, s, 0.006f, Plastic, 3);
            }
            Body.Project(0f, 0.86f, -1f, out var bc, out _);
            CarWheels.Lathe(Mb, new Vector3(0, 0.86f, bc.z - 0.004f), Vector3.back, new[] { new Vector2(0.06f, 0f), new Vector2(0.06f, 0.006f), new Vector2(0.054f, 0.008f), new Vector2(0f, 0.008f) }, 20, Chrome);
            Mb.Text("VW", new Vector3(0, 0.86f, bc.z - 0.0125f), Vector3.back, Vector3.up, 0.045f, Lib["Text_Black"]);
            Body.Project(0.42f, 0.72f, -1f, out var tp, out _);
            Mb.Text("GOLF", new Vector3(-0.42f, 0.72f, tp.z - 0.003f), Vector3.back, Vector3.up, 0.035f, Lib["Text_White"]);
            Body.Project(0f, 0.65f, -1f, out var pc, out var pn);
            CarKit.Lamp(Mb, pc, pn, Vector3.right, CarKit.Rect(0.56f, 0.15f, 0.03f), Paint, Seam, 0.001f, 0.02f);
            CarKit.Plate(Mb, pc + pn * 0.006f, pn, "WA 1J4GT", 0.95f);
            CarKit.Exhaust(Mb, new Vector3(-0.5f, 0.27f, ZR + 0.1f), 0.035f, 0.2f);
            SeamSide(-1.93f, 0.58f, 1.0f);
        }
    }
}
