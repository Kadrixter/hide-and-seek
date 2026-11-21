using UnityEngine;
using static HideAndSeek.Map.Editor.CarShapes;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// Volkswagen Golf IV (1J, 3-door hatchback): short tail, roof falling gently to a near-vertical glazed
    /// tailgate, wide curved C-pillar, one smooth clear cover per side over two round lamps, slim body-coloured
    /// grille with black slats and a chrome bar, bumper with a lower grille and fog lamps, dark horizontal tail
    /// lamps reaching onto the wings. 4.15 x 1.73 x 1.44 m, wheelbase 2.51 m.
    /// </summary>
    internal sealed class VwGolf4 : SportCar
    {
        public override string Name => "VwGolf4";
        public override string DefaultPaint => "CarPaint_ReflexSilver";
        public override float Length => 4.15f;
        protected override Arch FrontArch => new Arch(1.205f, 0.31f, 0.345f);
        protected override Arch RearArch => new Arch(-1.306f, 0.31f, 0.345f);
        protected override float TireRadius => 0.31f;
        protected override float TireWidth => 0.205f;
        protected override float RimRadius => 0.2f;
        protected override RimStyle Rim => RimStyle.FiveSpoke;
        protected override float TubWall => 0.7f;
        protected override float TubFloor => 0.26f;
        protected override float[] BodyBreaks => new[] { 0.88f, -0.42f, -1.96f };
        protected override int[] SharpRings => new[] { BShoulder };
        protected override float RoofRound => 0.8f;

        private const float ZF = 2.075f, ZR = -2.075f;

        private static BodyKey K(float z, float yBot, float wBot, float wMax, float yMax, float wSh, float ySh, float yTop,
            float tub = 0f, float round = 0.55f) => new BodyKey(z, yBot, wBot, wMax, yMax, wSh, ySh, yTop, round, tub);

        protected override BodyKey[] BodyKeys() => new[]
        {
            K(ZF, 0.3f, 0.6f, 0.62f, 0.42f, 0.6f, 0.5f, 0.52f, 0f, 1f),
            K(2.055f, 0.24f, 0.72f, 0.74f, 0.43f, 0.7f, 0.6f, 0.62f, 0f, 1f),
            K(2.0f, 0.21f, 0.78f, 0.8f, 0.45f, 0.765f, 0.69f, 0.71f),
            K(1.94f, 0.2f, 0.81f, 0.83f, 0.47f, 0.79f, 0.74f, 0.765f),
            K(1.85f, 0.19f, 0.83f, 0.845f, 0.5f, 0.81f, 0.775f, 0.8f),
            K(1.6f, 0.18f, 0.845f, 0.86f, 0.53f, 0.83f, 0.82f, 0.845f),
            K(1.205f, 0.18f, 0.85f, 0.865f, 0.55f, 0.835f, 0.88f, 0.9f),
            K(0.95f, 0.18f, 0.85f, 0.865f, 0.56f, 0.835f, 0.93f, 0.95f),
            K(0.82f, 0.18f, 0.85f, 0.865f, 0.56f, 0.835f, 0.94f, 0.955f, 1f),
            K(-1.2f, 0.18f, 0.85f, 0.865f, 0.57f, 0.83f, 0.985f, 0.98f, 1f),
            K(-1.34f, 0.18f, 0.85f, 0.865f, 0.57f, 0.83f, 0.99f, 0.99f),
            K(-1.8f, 0.21f, 0.835f, 0.85f, 0.57f, 0.8f, 0.99f, 1.0f),
            K(-1.97f, 0.24f, 0.795f, 0.81f, 0.58f, 0.76f, 0.995f, 1.0f),
            K(-2.04f, 0.28f, 0.72f, 0.74f, 0.58f, 0.7f, 0.985f, 0.99f, 0f, 1f),
            K(ZR, 0.32f, 0.62f, 0.64f, 0.58f, 0.6f, 0.97f, 0.975f, 0f, 1f),
        };

        protected override CabinKey[] CabinKeys() => new[]
        {
            new CabinKey(0.95f, 0.95f, 0.77f, 0.95f, 0.63f),
            new CabinKey(0.1f, 0.975f, 0.785f, 1.43f, 0.65f),
            new CabinKey(-0.6f, 0.985f, 0.785f, 1.44f, 0.65f),
            new CabinKey(-1.3f, 0.995f, 0.775f, 1.415f, 0.64f),
            new CabinKey(-1.6f, 1.0f, 0.755f, 1.36f, 0.62f),
            new CabinKey(-1.99f, 1.0f, 0.7f, 1.0f, 0.58f),
        };

        protected override float[] CabinBreaks => new[] { 0.12f, -0.42f, -0.47f, -1.02f, 0.9f, -1.62f, -1.66f };

        // door glass to the B-pillar, small quarter light, then the wide C-pillar (its slanted edge is an overlay)
        protected override Material CabinMat(int seg, float z)
        {
            bool glass = (z > -0.42f && z < 0.92f) || (z < -0.47f && z > -1.02f);
            switch (seg)
            {
                case 0: return Trim;
                case 1:
                case 2: return glass ? Glass : (z > -0.47f ? Trim : Paint);
                case 3: return glass ? Trim : Paint;
                case 4: return Paint;
                default:
                    if (z > 0.12f) return z > 0.88f || z < 0.16f ? Trim : Glass;
                    if (z < -1.62f) return z < -1.94f || z > -1.66f ? Trim : Glass; // tailgate glass
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
            Mb.Box(new Vector3(0, 0.98f, -1.6f), new Vector3(1.38f, 0.02f, 0.62f), Lib["Car_Dash"]); // parcel shelf
            Mb.Box(new Vector3(0, 0.6f, -1.22f), new Vector3(1.4f, 0.7f, 0.04f), cloth);
        }

        protected override void Details()
        {
            Front();
            Sides();
            Rear();
        }

        private void Front()
        {
            // one smooth clear cover per side over a chrome reflector bowl with two round lamps, indicator at the end
            var unit = new[] { new Vector2(0.29f, 0.578f), new Vector2(0.6f, 0.572f), new Vector2(0.7f, 0.59f), new Vector2(0.748f, 0.635f),
                new Vector2(0.742f, 0.696f), new Vector2(0.29f, 0.703f) };
            var amber = new[] { new Vector2(0.684f, 0.602f), new Vector2(0.735f, 0.636f), new Vector2(0.73f, 0.685f), new Vector2(0.684f, 0.685f) };
            foreach (float s in new[] { -1f, 1f })
            {
                Body.ProjectedLamp(Mb, unit, 1f, s, Lib["Car_Reflector"], Seam);
                foreach (var (x, r) in new[] { (0.385f, 0.056f), (0.56f, 0.05f) })
                    if (Body.Project(s * x, 0.638f, 1f, out var lp, out var ln))
                    {
                        CarKit.Lamp(Mb, lp + ln * 0.004f, ln, Vector3.right * s, CarKit.Circle(r + 0.008f, 20), Plastic, Plastic, 0.002f, 0.004f);
                        CarKit.RoundLamp(Mb, lp + ln * 0.006f, ln, r, Chrome, Lens);
                    }
                Body.ProjectedPatch(Mb, amber, 1f, s, 0.009f, Amber);
                if (Body.Project(s * 0.708f, 0.643f, 1f, out var ap, out var an)) Pm.Blinkers.Add(new Blinker(ap + an * 0.009f, an, new Vector2(0.05f, 0.08f)));
                Body.ProjectedPatch(Mb, unit, 1f, s, 0.026f, Glass);
                // bumper: lower grille, fog lamps in black surrounds, slim rubbing strip round the corner
                Body.ProjectedLamp(Mb, CarLoft.PolyAt(0f, 0.31f, new Vector2(0f, -0.05f), new Vector2(0.34f, -0.05f), new Vector2(0.38f, 0.05f), new Vector2(0f, 0.05f)), 1f, s, Grille, Seam, 0.008f, 0.002f);
                Body.ProjectedPatch(Mb, CarLoft.RectAt(0.18f, 0.31f, 0.36f, 0.012f, 0.004f), 1f, s, 0.006f, Plastic, 2);
                Body.ProjectedLamp(Mb, CarLoft.RectAt(0.55f, 0.335f, 0.15f, 0.1f, 0.045f), 1f, s, Plastic, Seam, 0.008f, 0.003f);
                if (Body.Project(s * 0.55f, 0.335f, 1f, out var fp, out var fn)) CarKit.RoundLamp(Mb, fp + fn * 0.004f, fn, 0.042f, Chrome, Lens);
                Body.ProjectedPatch(Mb, CarLoft.RectAt(0.42f, 0.455f, 0.84f, 0.022f, 0.008f), 1f, s, 0.004f, Plastic, 3);
                var (rp, rn) = Side(1.6f, 0.72f, s);
                CarKit.Lamp(Mb, rp, rn, Vector3.forward, CarKit.Rect(0.045f, 0.025f, 0.01f), Amber, Plastic, 0.004f, 0.02f);
                Pm.Blinkers.Add(new Blinker(rp + rn * 0.004f, rn, new Vector2(0.05f, 0.025f)));
                // grille: body-coloured frame, black slatted field, chrome strip along the top
                Body.ProjectedLamp(Mb, CarLoft.RectAt(0.13f, 0.632f, 0.26f, 0.09f, 0.012f), 1f, s, Plastic, Seam, 0.006f, 0.002f);
            }
            Body.Project(0f, 0.632f, 1f, out var gc, out _);
            for (int i = 0; i < 4; i++)
                Mb.Box(new Vector3(0, 0.598f + i * 0.022f, gc.z + 0.004f), new Vector3(0.5f, 0.007f, 0.008f), Lib["Car_PlasticGrey"]);
            foreach (float s in new[] { -1f, 1f })
                Body.ProjectedPatch(Mb, CarLoft.RectAt(0.14f, 0.682f, 0.28f, 0.01f, 0.003f), 1f, s, 0.006f, Chrome, 2);
            CarBadges.Vw(Mb, new Vector3(0, 0.632f, gc.z + 0.01f), Vector3.forward, 0.05f);
            Body.Project(0f, 0.37f, 1f, out var pc, out var pn);
            CarKit.Plate(Mb, pc + pn * 0.012f, pn, "WA 1J4GT", 0.9f);
            SeamTop(0.96f, 1.96f, t => Mathf.Lerp(0.77f, 0.7f, t));
            SeamAcross(0.96f, 0.77f);
            Wipers(0.92f, 0.1f, 0.55f, 0.06f, 0.66f);
        }

        private void Sides()
        {
            SeamSide(0.88f, 0.22f, 0.94f);
            SeamSide(-0.42f, 0.22f, 0.98f);
            // wide C-pillar: its front edge leans forward towards the roof (quarter light shaped like a sail)
            GlassOverlay(-1.02f, -0.86f, t => new Vector2(t, 1.02f), Paint);
            foreach (float s in new[] { -1f, 1f })
            {
                Body.PatchY(Mb, s, -0.92f, 0.86f, t => new Vector2(0.5f, 0.525f), BLip, BShoulder, 0.004f, Plastic, 12, 2);
                var (hp, hn) = Side(-0.3f, 0.87f, s);
                CarKit.Handle(Mb, hp, hn, Paint, 0.13f);
                var (mp, _) = Side(0.8f, 0.95f, s);
                CarKit.Mirror(Mb, mp, s, Paint, 0.07f, 0.19f, 0.12f, 0.05f);
            }
            var (fp, fn) = Side(-1.55f, 0.84f, 1f);
            CarWheels.Lathe(Mb, fp, fn, new[] { new Vector2(0.065f, 0.0005f), new Vector2(0.06f, 0.0012f), new Vector2(0f, 0.0012f) }, 18, Seam);
            CarWheels.Lathe(Mb, fp + fn * 0.0013f, fn, new[] { new Vector2(0.058f, 0f), new Vector2(0f, 0f) }, 18, Paint);
        }

        private void Rear()
        {
            // dark horizontal tail lamps running from the tailgate onto the wings
            var tail = new[] { new Vector2(0.44f, 0.8f), new Vector2(0.76f, 0.8f), new Vector2(0.8f, 0.83f), new Vector2(0.8f, 0.915f), new Vector2(0.44f, 0.925f) };
            foreach (float s in new[] { -1f, 1f })
            {
                Body.ProjectedLamp(Mb, tail, -1f, s, Lib["Car_TailDark"], Seam, 0.008f, 0.004f, 0.3f);
                Body.ProjectedPatch(Mb, CarLoft.RectAt(0.6f, 0.862f, 0.3f, 0.065f, 0.02f), -1f, s, 0.006f, Tail, 4, 0.3f);
                // the cluster continues round the corner onto the wing
                Body.PatchY(Mb, s, -2.0f, -1.86f, t => new Vector2(Mathf.Lerp(0.805f, 0.83f, t), Mathf.Lerp(0.915f, 0.9f, t)), BLip, BShoulder, 0.004f, Lib["Car_TailDark"], 6, 4);
                Body.PatchY(Mb, s, -1.99f, -1.9f, t => new Vector2(0.835f, 0.89f), BLip, BShoulder, 0.0055f, Tail, 4, 3);
                Body.ProjectedPatch(Mb, CarLoft.RectAt(0.5f, 0.862f, 0.07f, 0.06f, 0.012f), -1f, s, 0.007f, s > 0 ? Lib["Car_Reverse"] : Tail);
                Body.ProjectedPatch(Mb, CarLoft.RectAt(0.72f, 0.862f, 0.07f, 0.06f, 0.012f), -1f, s, 0.008f, Amber, 4, 0.3f);

                if (Body.Project(s * 0.72f, 0.862f, -1f, out var bp, out var bn)) Pm.Blinkers.Add(new Blinker(bp + bn * 0.008f, bn, new Vector2(0.07f, 0.06f)));
                Body.ProjectedPatch(Mb, CarLoft.RectAt(0.38f, 0.37f, 0.76f, 0.1f, 0.02f), -1f, s, 0.003f, Plastic, 3); // lower bumper
                // gap between the tailgate and the bumper
                Body.ProjectedPatch(Mb, CarLoft.RectAt(0.38f, 0.555f, 0.76f, 0.005f, 0.001f), -1f, s, 0.0015f, Seam, 2);
            }
            Body.Project(0f, 0.86f, -1f, out var bc, out var bcn);
            CarBadges.Vw(Mb, bc + bcn * 0.004f, bcn, 0.06f);
            Body.Project(0.42f, 0.72f, -1f, out var tp, out _);
            Mb.Text("GOLF", new Vector3(-0.42f, 0.72f, tp.z - 0.003f), Vector3.back, Vector3.up, 0.035f, Lib["Text_White"]);
            Body.Project(0f, 0.66f, -1f, out var pc, out var pn);
            CarKit.Lamp(Mb, pc, pn, Vector3.right, CarKit.Rect(0.56f, 0.15f, 0.03f), Paint, Seam, 0.001f, 0.02f);
            CarKit.Plate(Mb, pc + pn * 0.006f, pn, "WA 1J4GT", 0.95f);
            CarKit.Exhaust(Mb, new Vector3(-0.5f, 0.27f, ZR + 0.1f), 0.035f, 0.2f);
            // roof spoiler lip over the tailgate glass
            CarBlob.Blob(Mb, new Vector3(0, 1.38f, -1.63f), Quaternion.Euler(-12f, 0, 0), new Vector3(0.6f, 0.018f, 0.06f), 0.35f, Paint, 0.5f, 1f, 16, 8);
        }
    }
}
