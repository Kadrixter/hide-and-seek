using UnityEngine;
using static HideAndSeek.Map.Editor.CarShapes;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// Mazda RX-7 (FD3S): very low smooth nose with an oval mouth, front wings standing above the bonnet, closed
    /// pop-up lamps (lid shut lines), double-bubble roof, curved glass hatch, full-width smoked tail panel with
    /// three round lamps a side, hoop spoiler. 4.29 x 1.76 x 1.23 m, wheelbase 2.43 m.
    /// </summary>
    internal sealed class MazdaRx7 : SportCar
    {
        public override string Name => "MazdaRx7";
        public override string DefaultPaint => "CarPaint_Brilliantrot";
        public override float Length => 4.285f;
        protected override Arch FrontArch => new Arch(1.19f, 0.315f, 0.35f);
        protected override Arch RearArch => new Arch(-1.235f, 0.315f, 0.355f);
        protected override float TireRadius => 0.315f;
        protected override float TireWidth => 0.235f;
        protected override float RimRadius => 0.215f;
        protected override RimStyle Rim => RimStyle.FiveSpoke;
        protected override Material RimMat => Lib["Car_RimGraphite"];
        protected override Material Caliper => Lib["Car_Caliper"];
        protected override float TubWall => 0.68f;
        protected override float TubFloor => 0.22f;
        protected override float[] BodyBreaks => new[] { 0.5f, -0.62f };

        private const float ZF = 2.1425f, ZR = -2.1425f;

        private static BodyKey K(float z, float yBot, float wBot, float wMax, float yMax, float wSh, float ySh, float yTop,
            float tub = 0f, float round = 1f) => new BodyKey(z, yBot, wBot, wMax, yMax, wSh, ySh, yTop, round, tub);

        protected override BodyKey[] BodyKeys() => new[]
        {
            K(ZF, 0.2f, 0.46f, 0.48f, 0.27f, 0.44f, 0.33f, 0.33f),
            K(2.12f, 0.17f, 0.6f, 0.62f, 0.28f, 0.58f, 0.38f, 0.38f),
            K(2.06f, 0.15f, 0.71f, 0.73f, 0.3f, 0.69f, 0.44f, 0.43f),
            K(1.92f, 0.14f, 0.8f, 0.82f, 0.33f, 0.78f, 0.535f, 0.5f),
            K(1.7f, 0.13f, 0.845f, 0.865f, 0.36f, 0.82f, 0.64f, 0.575f),
            K(1.19f, 0.13f, 0.86f, 0.88f, 0.4f, 0.84f, 0.78f, 0.66f),
            K(0.6f, 0.13f, 0.85f, 0.875f, 0.45f, 0.82f, 0.8f, 0.785f),
            K(0.46f, 0.13f, 0.85f, 0.875f, 0.45f, 0.82f, 0.81f, 0.8f, 1f),
            K(-0.62f, 0.13f, 0.86f, 0.88f, 0.48f, 0.83f, 0.86f, 0.86f, 1f),
            K(-0.75f, 0.13f, 0.87f, 0.885f, 0.48f, 0.83f, 0.87f, 0.88f),
            K(-1.235f, 0.13f, 0.875f, 0.89f, 0.48f, 0.82f, 0.9f, 0.9f),
            K(-1.8f, 0.16f, 0.85f, 0.87f, 0.5f, 0.79f, 0.9f, 0.91f),
            K(-2.0f, 0.2f, 0.79f, 0.81f, 0.52f, 0.73f, 0.89f, 0.9f),
            K(-2.1f, 0.25f, 0.68f, 0.7f, 0.54f, 0.62f, 0.86f, 0.87f),
            K(ZR, 0.29f, 0.56f, 0.58f, 0.55f, 0.5f, 0.83f, 0.84f),
        };

        protected override CabinKey[] CabinKeys() => new[]
        {
            new CabinKey(0.52f, 0.8f, 0.73f, 0.8f, 0.6f),
            new CabinKey(-0.18f, 0.84f, 0.74f, 1.215f, 0.57f),
            new CabinKey(-0.55f, 0.865f, 0.73f, 1.205f, 0.56f),
            new CabinKey(-1.1f, 0.89f, 0.7f, 1.05f, 0.56f),
            new CabinKey(-1.62f, 0.905f, 0.64f, 0.915f, 0.54f),
        };

        protected override float[] CabinBreaks => new[] { -0.15f, -0.6f, -0.98f, 0.45f, -0.7f, -0.76f };


        protected override Material CabinMat(int seg, float z)
        {
            bool door = z > -0.6f, quarter = z < -0.66f && z > -0.98f;
            switch (seg)
            {
                case 0: return Trim;
                case 1:
                case 2: return door || quarter ? Glass : Trim;
                case 3: return door || quarter ? Trim : Paint;
                case 4: return Paint;
                default:
                    if (z > -0.15f) return z > 0.45f || z < -0.11f ? Trim : Glass;
                    if (z < -0.7f) return z > -0.76f || z < -1.56f ? Trim : Glass; // glass hatch
                    return Paint;
            }
        }

        protected override void Interior()
        {
            var seat = Lib["Car_LeatherBlack"];
            foreach (float s in new[] { -1f, 1f })
                CarInterior.Seat(Mb, new Vector3(s * 0.34f, TubFloor, -0.32f), 0.5f, 0.35f, seat, Lib["Car_Interior"], true, 20f, 0.6f);
            CarInterior.Dash(Mb, 0.68f, 0.5f, 0.12f, 0.74f, 0.42f, -0.34f, Lib["Car_Dash"]);
            CarInterior.SteeringWheel(Mb, new Vector3(-0.34f, 0.7f, -0.08f), new Vector3(0, 0.4f, 1f), 0.18f, Lib["Car_LeatherBlack"]);
            CarInterior.Console(Mb, -0.7f, 0.12f, TubFloor, 0.24f, Lib["Car_Dash"], Lib["Car_LeatherBlack"]);
            Mb.Box(new Vector3(0, 0.6f, -0.85f), new Vector3(1.3f, 0.55f, 0.04f), Lib["Car_Interior"]);
            Mb.Box(new Vector3(0, 0.86f, -1.25f), new Vector3(1.3f, 0.02f, 0.75f), Lib["Car_Interior"]); // luggage deck
        }

        protected override void Details()
        {
            Front();
            Sides();
            Rear();
        }

        private static Vector2[] HalfOval(float cx, float cy, float rx, float ry, int n = 10)
        {
            var o = new Vector2[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float a = -Mathf.PI * 0.5f + i * Mathf.PI / n;
                o[i] = new Vector2(cx + Mathf.Max(0f, Mathf.Cos(a)) * rx, cy + Mathf.Sin(a) * ry);
            }
            return o;
        }

        private void Front()
        {
            // oval mouth with a slim bar, corner lamps wrapping round the nose, closed pop-up lids
            foreach (float s in new[] { -1f, 1f })
            {
                Body.ProjectedLamp(Mb, HalfOval(0f, 0.262f, 0.43f, 0.068f), 1f, s, Grille, Seam, 0.008f, 0.002f);
                Body.ProjectedPatch(Mb, CarLoft.RectAt(0.2f, 0.262f, 0.4f, 0.012f, 0.004f), 1f, s, 0.008f, Plastic, 2);
                var corner = CarLoft.PolyAt(0f, 0f, new Vector2(0.54f, 0.235f), new Vector2(0.7f, 0.25f), new Vector2(0.745f, 0.29f),
                    new Vector2(0.72f, 0.33f), new Vector2(0.56f, 0.32f));
                Body.ProjectedLamp(Mb, corner, 1f, s, Plastic, Seam);
                Body.ProjectedPatch(Mb, CarLoft.PolyAt(0f, 0f, new Vector2(0.56f, 0.285f), new Vector2(0.73f, 0.29f), new Vector2(0.715f, 0.322f), new Vector2(0.57f, 0.315f)), 1f, s, 0.007f, Amber);
                Body.ProjectedPatch(Mb, CarLoft.PolyAt(0f, 0f, new Vector2(0.56f, 0.245f), new Vector2(0.69f, 0.257f), new Vector2(0.725f, 0.282f), new Vector2(0.56f, 0.278f)), 1f, s, 0.007f, Lens);
                if (Body.Project(s * 0.65f, 0.302f, 1f, out var ap, out var an)) Pm.Blinkers.Add(new Blinker(ap + an * 0.008f, an, new Vector2(0.14f, 0.035f)));
                foreach (float x in new[] { 0.5f, 0.72f })
                    Body.PatchX(Mb, s, 1.6f, 1.94f, t => new Vector2(x - 0.0025f, x + 0.0025f), BShoulder, BCentre, 0.0012f, Seam, 8, 1);
                foreach (float z in new[] { 1.6f, 1.94f })
                    Body.PatchX(Mb, s, z - 0.0025f, z + 0.0025f, t => new Vector2(0.5f, 0.72f), BShoulder, BCentre, 0.0012f, Seam, 1, 6);
                var (sm, smn) = Side(1.75f, 0.42f, s);
                CarKit.Lamp(Mb, sm, smn, Vector3.forward, CarKit.Rect(0.07f, 0.02f, 0.008f), Amber, Plastic, 0.003f, 0.02f);
                Pm.Blinkers.Add(new Blinker(sm + smn * 0.003f, smn, new Vector2(0.07f, 0.02f)));
            }
            Body.Project(0f, 0.262f, 1f, out var pc, out var pn);
            CarKit.Plate(Mb, pc + pn * 0.02f, pn, "KR RX7FD", 0.8f);
            SeamTop(0.56f, 1.98f, t => Mathf.Lerp(0.5f, 0.46f, t));
            SeamAcross(1.98f, 0.46f);
            SeamAcross(0.56f, 0.5f);
            Wipers(0.47f, 0.1f, 0.48f, 0.1f, 0.6f);
        }

        private void Sides()
        {
            SeamSide(0.5f, 0.2f, 0.8f);
            SeamSide(-0.62f, 0.2f, 0.86f);
            foreach (float s in new[] { -1f, 1f })
            {
                Body.PatchY(Mb, s, -0.62f, 0.5f, t => new Vector2(0.255f, 0.26f), BLip, BShoulder, 0.0012f, Seam, 10, 1);
                var (hp, hn) = Side(-0.5f, 0.75f, s);
                CarKit.Handle(Mb, hp, hn, Paint, 0.11f);
                var (mp, _) = Side(0.42f, 0.8f, s);
                CarKit.Mirror(Mb, mp, s, Paint, 0.07f, 0.17f, 0.09f, 0.05f);
            }
            var (fp, fn) = Side(-1.6f, 0.75f, -1f);
            CarWheels.Lathe(Mb, fp, fn, new[] { new Vector2(0.06f, 0.0005f), new Vector2(0.055f, 0.0012f), new Vector2(0f, 0.0012f) }, 18, Seam);
            CarWheels.Lathe(Mb, fp + fn * 0.0013f, fn, new[] { new Vector2(0.053f, 0f), new Vector2(0f, 0f) }, 18, Paint);
        }

        private void Rear()
        {
            // smoked panel across the tail with three round lamps a side, wrapping round the corners
            foreach (float s in new[] { -1f, 1f })
            {
                Body.ProjectedLamp(Mb, CarLoft.PolyAt(0f, 0f, new Vector2(0f, 0.675f), new Vector2(0.68f, 0.672f), new Vector2(0.735f, 0.7f),
                    new Vector2(0.735f, 0.775f), new Vector2(0f, 0.79f)), -1f, s, Lib["Car_TailDark"], Seam);
                foreach (var (x, r, m) in new[] { (0.63f, 0.05f, Tail), (0.49f, 0.045f, Amber), (0.36f, 0.04f, s > 0 ? Lib["Car_Reverse"] : Tail) })
                    if (Body.Project(s * x, 0.733f, -1f, out var lp, out var ln)) CarKit.RoundLamp(Mb, lp + ln * 0.003f, ln, r, Trim, m);
                if (Body.Project(s * 0.49f, 0.733f, -1f, out var bp, out var bn)) Pm.Blinkers.Add(new Blinker(bp + bn * 0.018f, bn, new Vector2(0.09f, 0.09f)));
                Body.ProjectedPatch(Mb, CarLoft.RectAt(0.3f, 0.33f, 0.6f, 0.08f, 0.02f), -1f, s, 0.004f, Plastic, 3);
            }
            Body.Project(0f, 0.733f, -1f, out var mc, out _);
            Mb.Text("MAZDA", new Vector3(0, 0.733f, mc.z - 0.006f), Vector3.back, Vector3.up, 0.035f, Lib["Text_White"]);
            Body.Project(0f, 0.52f, -1f, out var pc, out var pn);
            CarKit.Lamp(Mb, pc, pn, Vector3.right, CarKit.Rect(0.58f, 0.16f, 0.03f), Plastic, Seam, 0.001f, 0.02f);
            CarKit.Plate(Mb, pc + pn * 0.006f, pn, "KR RX7FD", 0.95f);
            foreach (float s in new[] { -1f, 1f })
                CarKit.Exhaust(Mb, new Vector3(s * 0.4f, 0.27f, ZR + 0.08f), 0.042f, 0.16f);
            // hoop spoiler on the boot lid
            float wy = 0.99f, wz = -2.0f;
            CarBlob.Blob(Mb, new Vector3(0, wy, wz), Quaternion.Euler(-4f, 0, 0), new Vector3(0.71f, 0.014f, 0.08f), 0.3f, Paint, 0.6f, 1f, 16, 8);
            foreach (float s in new[] { -1f, 1f })
                CarBlob.Bar(Mb, new Vector3(s * 0.66f, 0.88f, wz + 0.02f), new Vector3(s * 0.7f, wy, wz), Vector3.forward, 0.05f, 0.15f, Paint, 0.4f, 1.3f);
            SeamAcross(-1.66f, 0.62f);
            SeamTop(-2.08f, -1.66f, t => 0.62f);
        }
    }
}
