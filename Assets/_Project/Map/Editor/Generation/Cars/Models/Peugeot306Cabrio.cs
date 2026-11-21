using UnityEngine;
using static HideAndSeek.Map.Editor.CarShapes;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// Peugeot 306 Cabriolet (Pininfarina, phase 1) with the roof down: rounded wedge, slim slot grille with the
    /// lion, wide slanted headlamps, black rubbing strips, chunky screen frame, soft top folded under a black
    /// tonneau, four seats, horizontal tail lamps. 4.13 x 1.69 m, wheelbase 2.58 m.
    /// </summary>
    internal sealed class Peugeot306Cabrio : SportCar
    {
        public override string Name => "Peugeot306Cabrio";
        public override string DefaultPaint => "CarPaint_Sunflower";
        public override float Length => 4.13f;
        protected override Arch FrontArch => new Arch(1.245f, 0.3f, 0.335f);
        protected override Arch RearArch => new Arch(-1.335f, 0.3f, 0.335f);
        protected override float TireRadius => 0.3f;
        protected override float TireWidth => 0.195f;
        protected override float RimRadius => 0.19f;
        protected override RimStyle Rim => RimStyle.Turbine;
        protected override float TubWall => 0.7f;
        protected override float TubFloor => 0.24f;
        protected override float[] BodyBreaks => new[] { 0.52f, -0.42f, -1.02f };

        private const float ZF = 2.065f, ZR = -2.065f;

        private static BodyKey K(float z, float yBot, float wBot, float wMax, float yMax, float wSh, float ySh, float yTop,
            float tub = 0f, float round = 1f) => new BodyKey(z, yBot, wBot, wMax, yMax, wSh, ySh, yTop, round, tub);

        protected override BodyKey[] BodyKeys() => new[]
        {
            K(ZF, 0.27f, 0.52f, 0.54f, 0.38f, 0.5f, 0.44f, 0.46f),
            K(2.04f, 0.22f, 0.66f, 0.68f, 0.39f, 0.64f, 0.53f, 0.55f),
            K(1.98f, 0.19f, 0.75f, 0.77f, 0.41f, 0.73f, 0.6f, 0.62f),
            K(1.85f, 0.18f, 0.8f, 0.82f, 0.44f, 0.78f, 0.66f, 0.68f),
            K(1.6f, 0.17f, 0.825f, 0.84f, 0.47f, 0.805f, 0.73f, 0.75f),
            K(1.245f, 0.165f, 0.83f, 0.845f, 0.5f, 0.815f, 0.79f, 0.8f),
            K(0.62f, 0.165f, 0.83f, 0.845f, 0.52f, 0.81f, 0.855f, 0.86f),
            K(0.5f, 0.165f, 0.83f, 0.845f, 0.52f, 0.81f, 0.86f, 0.86f, 1f),
            K(-0.95f, 0.165f, 0.83f, 0.845f, 0.52f, 0.805f, 0.9f, 0.9f, 1f),
            K(-1.08f, 0.165f, 0.83f, 0.845f, 0.52f, 0.8f, 0.905f, 0.915f),
            K(-1.335f, 0.165f, 0.83f, 0.845f, 0.52f, 0.8f, 0.91f, 0.925f),
            K(-1.8f, 0.19f, 0.815f, 0.835f, 0.52f, 0.785f, 0.905f, 0.925f),
            K(-1.95f, 0.23f, 0.77f, 0.79f, 0.53f, 0.74f, 0.89f, 0.915f),
            K(-2.03f, 0.27f, 0.68f, 0.7f, 0.54f, 0.65f, 0.87f, 0.89f),
            K(ZR, 0.31f, 0.58f, 0.6f, 0.55f, 0.56f, 0.845f, 0.865f),
        };

        // windscreen and its frame only (roof down)
        protected override CabinKey[] CabinKeys() => new[]
        {
            new CabinKey(0.6f, 0.86f, 0.77f, 0.86f, 0.68f),
            new CabinKey(0.0f, 0.875f, 0.76f, 1.3f, 0.64f),
            new CabinKey(-0.06f, 0.875f, 0.76f, 1.31f, 0.635f),
        };

        protected override float[] CabinBreaks => new[] { 0.05f, 0.55f };

        protected override Material CabinMat(int seg, float z)
        {
            if (seg <= 2) return null;                  // no side glass: windows down
            if (seg == 3) return Trim;                  // pillar rubber
            if (seg == 4) return Paint;                 // A-pillar
            if (z < 0.05f) return Paint;                // header rail
            return z > 0.55f || z < 0.1f ? Trim : Glass; // screen with a black frit band
        }

        protected override void Interior()
        {
            var cloth = Lib["Car_Interior"];

            var dark = Lib["Car_LeatherBlack"];
            foreach (float s in new[] { -1f, 1f })
                CarInterior.Seat(Mb, new Vector3(s * 0.34f, TubFloor, -0.12f), 0.5f, 0.46f, cloth, dark, true, 14f, 0.58f);
            CarInterior.RearBench(Mb, 0.56f, TubFloor, 0.5f, -0.82f, cloth, 0.42f, 12f);
            foreach (float s in new[] { -1f, 1f })
                CarBlob.Blob(Mb, new Vector3(s * 0.32f, 1.02f, -1.0f), Quaternion.Euler(-12f, 0, 0), new Vector3(0.12f, 0.075f, 0.045f), 0.45f, cloth, 0.45f);
            CarInterior.Dash(Mb, 0.7f, 0.62f, 0.3f, 0.82f, 0.48f, -0.36f, Lib["Car_Dash"]);
            CarInterior.SteeringWheel(Mb, new Vector3(-0.36f, 0.82f, 0.18f), new Vector3(0, 0.45f, 1f), 0.19f, Lib["Car_LeatherBlack"]);
            CarInterior.Console(Mb, -0.45f, 0.3f, TubFloor, 0.2f, Lib["Car_Dash"], Lib["Car_LeatherBlack"]);
            // folded soft top under a black tonneau behind the rear seats
            foreach (float s in new[] { -1f, 1f })
                Body.PatchX(Mb, s, -1.24f, -1.0f, t => new Vector2(0f, 0.74f), BShoulder, BCentre, 0.002f, Lib["Car_SoftTop"], 6, 6);
            CarBlob.Blob(Mb, new Vector3(0, 0.95f, -1.12f), Quaternion.identity, new Vector3(0.73f, 0.07f, 0.15f), 0.25f, Lib["Car_SoftTop"], 0.6f);
        }

        protected override void Details()
        {
            Front();
            Sides();
            Rear();
        }

        private void Front()
        {
            // slim lamps wrapping round the nose corners: reflector, projector, amber tip, clear cover
            var lamp = new[] { new Vector2(0.24f, 0.505f), new Vector2(0.6f, 0.5f), new Vector2(0.72f, 0.52f), new Vector2(0.765f, 0.56f),
                new Vector2(0.735f, 0.6f), new Vector2(0.24f, 0.598f) };
            var refl = new[] { new Vector2(0.26f, 0.515f), new Vector2(0.58f, 0.512f), new Vector2(0.62f, 0.588f), new Vector2(0.26f, 0.588f) };
            var tip = new[] { new Vector2(0.64f, 0.515f), new Vector2(0.715f, 0.528f), new Vector2(0.75f, 0.56f), new Vector2(0.725f, 0.59f), new Vector2(0.66f, 0.59f) };
            foreach (float s in new[] { -1f, 1f })
            {
                Body.ProjectedLamp(Mb, lamp, 1f, s, Plastic, Seam);
                Body.ProjectedPatch(Mb, refl, 1f, s, 0.006f, Lib["Car_Reflector"]);
                if (Body.Project(s * 0.47f, 0.551f, 1f, out var lp, out var ln)) CarKit.RoundLamp(Mb, lp + ln * 0.004f, ln, 0.04f, Chrome, Lens);
                if (Body.Project(s * 0.33f, 0.551f, 1f, out var lp2, out var ln2)) CarKit.RoundLamp(Mb, lp2 + ln2 * 0.004f, ln2, 0.03f, Chrome, Lens);
                Body.ProjectedPatch(Mb, tip, 1f, s, 0.007f, Amber);
                if (Body.Project(s * 0.7f, 0.556f, 1f, out var ap, out var an)) Pm.Blinkers.Add(new Blinker(ap + an * 0.008f, an, new Vector2(0.08f, 0.07f)));
                Body.ProjectedPatch(Mb, lamp, 1f, s, 0.022f, Glass);
                // bumper: rubbing strip round the corner, lower intake, fog lamps
                Body.ProjectedPatch(Mb, CarLoft.RectAt(0.42f, 0.43f, 0.84f, 0.045f, 0.01f), 1f, s, 0.007f, Plastic, 3);
                Body.ProjectedPatch(Mb, CarLoft.PolyAt(0f, 0.31f, new Vector2(0f, -0.045f), new Vector2(0.3f, -0.045f), new Vector2(0.36f, 0.045f), new Vector2(0f, 0.045f)), 1f, s, 0.003f, Grille, 3);
                if (Body.Project(s * 0.55f, 0.32f, 1f, out var fp, out var fn))
                {
                    CarKit.Lamp(Mb, fp, fn, Vector3.right * s, CarKit.Rect(0.15f, 0.065f, 0.028f), Plastic, Seam, 0.002f, 0.02f);
                    CarKit.Lamp(Mb, fp + fn * 0.003f, fn, Vector3.right * s, CarKit.Rect(0.12f, 0.045f, 0.02f), Lens, Plastic, 0.003f, 0.0f, 0.004f);
                }
                var (rp, rn) = Side(1.62f, 0.62f, s);
                CarKit.Lamp(Mb, rp, rn, Vector3.forward, CarKit.Rect(0.05f, 0.025f, 0.01f), Amber, Plastic, 0.004f, 0.02f);
                Pm.Blinkers.Add(new Blinker(rp + rn * 0.004f, rn, new Vector2(0.05f, 0.025f)));
                // slim grille slot between the lamps
                Body.ProjectedPatch(Mb, CarLoft.RectAt(0.11f, 0.553f, 0.22f, 0.04f, 0.012f), 1f, s, 0.003f, Grille, 2);
            }
            Body.Project(0f, 0.553f, 1f, out var gc, out var gn);
            CarWheels.Lathe(Mb, gc + gn * 0.004f, gn, new[] { new Vector2(0.03f, 0f), new Vector2(0.03f, 0.005f), new Vector2(0f, 0.008f) }, 14, Chrome);
            Body.Project(0f, 0.395f, 1f, out var pc, out var pn);
            CarKit.Plate(Mb, pc + pn * 0.012f, pn, "WE 306CB", 0.9f);
            SeamTop(0.66f, 1.96f, t => Mathf.Lerp(0.74f, 0.6f, t));
            SeamAcross(0.66f, 0.74f);
            Wipers(0.55f, 0.1f, 0.5f, 0.12f, 0.62f);
        }

        private void Sides()
        {
            SeamSide(0.6f, 0.2f, 0.85f);
            SeamSide(-0.62f, 0.2f, 0.9f);
            foreach (float s in new[] { -1f, 1f })
            {
                Body.PatchY(Mb, s, -0.96f, 0.86f, t => new Vector2(0.41f, 0.455f), BLip, BShoulder, 0.007f, Plastic, 12, 2);
                var (hp, hn) = Side(-0.42f, 0.76f, s);
                CarKit.Handle(Mb, hp, hn, Plastic, 0.12f);
                var (mp, _) = Side(0.52f, 0.86f, s);
                CarKit.Mirror(Mb, mp, s, Plastic, 0.06f, 0.17f, 0.11f, 0.05f);
            }
            var (fp, fn) = Side(-1.55f, 0.78f, 1f);
            CarWheels.Lathe(Mb, fp, fn, new[] { new Vector2(0.065f, 0.0005f), new Vector2(0.06f, 0.0012f), new Vector2(0f, 0.0012f) }, 18, Seam);
            CarWheels.Lathe(Mb, fp + fn * 0.0013f, fn, new[] { new Vector2(0.058f, 0f), new Vector2(0f, 0f) }, 18, Paint);
        }

        private void Rear()
        {
            // wide tail lamps wrapping round the corners: red body, reverse inside, amber outside
            var tail = new[] { new Vector2(0.3f, 0.705f), new Vector2(0.7f, 0.7f), new Vector2(0.75f, 0.73f), new Vector2(0.745f, 0.8f), new Vector2(0.3f, 0.81f) };
            foreach (float s in new[] { -1f, 1f })
            {
                Body.ProjectedLamp(Mb, tail, -1f, s, Tail, Seam);
                Body.ProjectedPatch(Mb, CarLoft.RectAt(0.36f, 0.755f, 0.09f, 0.08f, 0.01f), -1f, s, 0.007f, s > 0 ? Lib["Car_Reverse"] : Lib["Car_TailDark"]);
                Body.ProjectedPatch(Mb, CarLoft.RectAt(0.64f, 0.752f, 0.1f, 0.07f, 0.01f), -1f, s, 0.007f, Amber);
                if (Body.Project(s * 0.64f, 0.752f, -1f, out var bp, out var bn))
 Pm.Blinkers.Add(new Blinker(bp + bn * 0.007f, bn, new Vector2(0.1f, 0.07f)));
                Body.ProjectedPatch(Mb, CarLoft.RectAt(0.42f, 0.47f, 0.84f, 0.045f, 0.01f), -1f, s, 0.007f, Plastic, 3);
            }
            Body.Project(0f, 0.62f, -1f, out var pc, out var pn);
            CarKit.Lamp(Mb, pc, pn, Vector3.right, CarKit.Rect(0.56f, 0.15f, 0.02f), Paint, Seam, 0.001f, 0.02f);
            CarKit.Plate(Mb, pc + pn * 0.006f, pn, "WE 306CB", 0.95f);
            Body.Project(0.42f, 0.86f, -1f, out var tp, out _);
            Mb.Text("306", new Vector3(-0.42f, 0.86f, tp.z - 0.004f), Vector3.back, Vector3.up, 0.04f, Lib["Text_White"]);
            Body.Project(0f, 0.775f, -1f, out var lc, out var lcn);
            CarWheels.Lathe(Mb, lc + lcn * 0.002f, lcn, new[] { new Vector2(0.03f, 0f), new Vector2(0.03f, 0.005f), new Vector2(0f, 0.008f) }, 14, Chrome);
            SeamAcross(-1.1f, 0.7f);
            SeamTop(-1.98f, -1.1f, t => 0.7f);
            CarKit.Exhaust(Mb, new Vector3(-0.45f, 0.27f, ZR + 0.12f), 0.035f, 0.18f);
        }
    }
}
