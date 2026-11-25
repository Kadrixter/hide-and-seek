using UnityEngine;
using static HideAndSeek.Map.Editor.CarShapes;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// Toyota GT86 (ZN6, 2012-2016 coupe): long low bonnet sunk between peaked front wings, swept headlamps with an
    /// LED strip, a wide lower mouth flanked by fog-lamp pockets, a vented front wing, a short double-bubble roof
    /// falling into a fastback rear screen, high wrap-round tail lamps over a ducktail lid, twin exhausts in a black
    /// diffuser. 4.24 x 1.775 x 1.285 m, wheelbase 2.57 m, 215/45 R17. No maker logos or lettering (blank badges).
    /// Wears an oversized time-attack wing on bumper-mounted stanchions. Split over partial files: Front, Sides, Rear,
    /// Wing, Interior.
    /// </summary>
    internal sealed partial class ToyotaGt86 : SportCar
    {
        public override string Name => "ToyotaGt86";
        public override string DefaultPaint => "CarPaint_OrangeMetallic";
        public override float Length => 4.24f;
        protected override Arch FrontArch => new Arch(AxleF, 0.312f, 0.36f);
        protected override Arch RearArch => new Arch(AxleR, 0.312f, 0.362f);
        protected override float TireRadius => 0.312f;
        protected override float TireWidth => 0.215f;
        protected override float RimRadius => 0.222f;
        protected override float TireInset => 0.03f;
        protected override RimStyle Rim => RimStyle.TenSpoke;
        protected override Material RimMat => Lib["Car_RimSilver"];
        protected override float TubWall => 0.67f;
        protected override float TubFloor => 0.2f;
        protected override float RoofDip => 0.016f;
        protected override float ArchLipBead => 0.012f;
        protected override int[] SharpRings => new[] { BShoulder, BMax };
        protected override float[] BodyBreaks => new[] { DoorF, DoorR, 0.42f, -1.0f };

        /// <summary>Nose / tail z, axles, door shut lines.</summary>
        private const float ZF = 2.12f, ZR = -2.12f, AxleF = 1.28f, AxleR = -1.29f, DoorF = 0.47f, DoorR = -0.8f;

        /// <summary>Greenhouse stations: screen base, screen top, rear screen top and base, B-pillar, quarter-glass tail.</summary>
        private const float ScreenBase = 0.47f, ScreenTop = -0.3f, BackTop = -1.02f, BackBase = -1.66f, BPillar = -0.78f,
            BPillarW = 0.05f, QuarterEnd = -1.2f;

        // ------------------------------------------------------------------ reference outlines (real car, studio guides)

        public override Vector2[] RefSide => new[]
        {
            new Vector2(1.96f, 0.15f), new Vector2(2.07f, 0.19f), new Vector2(2.115f, 0.3f), new Vector2(2.12f, 0.42f), new Vector2(2.09f, 0.55f),
            new Vector2(2.02f, 0.62f), new Vector2(1.85f, 0.67f), new Vector2(1.5f, 0.72f), new Vector2(1.0f, 0.77f), new Vector2(0.47f, 0.84f),
            new Vector2(0.1f, 1.04f), new Vector2(-0.3f, 1.26f), new Vector2(-0.6f, 1.285f), new Vector2(-0.9f, 1.26f), new Vector2(-1.2f, 1.16f),
            new Vector2(-1.5f, 1.03f), new Vector2(-1.7f, 0.97f), new Vector2(-2.0f, 0.985f), new Vector2(-2.07f, 0.95f), new Vector2(-2.1f, 0.85f),
            new Vector2(-2.12f, 0.6f), new Vector2(-2.11f, 0.4f), new Vector2(-2.06f, 0.27f), new Vector2(-1.94f, 0.2f),
        };

        public override Vector2[] RefTop => new[]
        {
            new Vector2(2.12f, 0f), new Vector2(2.11f, 0.3f), new Vector2(2.06f, 0.55f), new Vector2(1.98f, 0.72f), new Vector2(1.85f, 0.82f),
            new Vector2(1.6f, 0.87f), new Vector2(1.28f, 0.88f), new Vector2(0.6f, 0.865f), new Vector2(-0.4f, 0.86f), new Vector2(-1.29f, 0.8875f),
            new Vector2(-1.8f, 0.865f), new Vector2(-2.02f, 0.78f), new Vector2(-2.1f, 0.66f), new Vector2(-2.12f, 0.5f), new Vector2(-2.12f, 0f),
        };

        public override Vector2[] RefFront => new[]
        {
            new Vector2(0f, 1.285f), new Vector2(0.48f, 1.27f), new Vector2(0.58f, 1.2f), new Vector2(0.69f, 0.95f), new Vector2(0.72f, 0.87f),
            new Vector2(0.82f, 0.84f), new Vector2(0.875f, 0.7f), new Vector2(0.8875f, 0.5f), new Vector2(0.88f, 0.3f), new Vector2(0.83f, 0.16f),
        };

        // ------------------------------------------------------------------ body

        private static BodyKey K(float z, float yBot, float wBot, float wMax, float yMax, float wSh, float ySh, float yTop,
            float tub = 0f, float round = 0.45f, float concave = 0f) => new BodyKey(z, yBot, wBot, wMax, yMax, wSh, ySh, yTop, round, tub, concave);

        // Low nose with a near-vertical mouth, wings peaking above the bonnet, doors drawn in under the shoulder between
        // the flared arches, the rear wings the widest part, a short high deck ending in the ducktail.
        protected override BodyKey[] BodyKeys() => new[]
        {
            K(ZF, 0.27f, 0.3f, 0.32f, 0.38f, 0.28f, 0.47f, 0.48f, 0f, 0.8f),
            K(2.105f, 0.2f, 0.5f, 0.53f, 0.39f, 0.47f, 0.555f, 0.565f, 0f, 0.7f),
            K(2.07f, 0.165f, 0.64f, 0.68f, 0.4f, 0.62f, 0.62f, 0.6f, 0f, 0.6f),
            K(1.99f, 0.15f, 0.75f, 0.79f, 0.42f, 0.735f, 0.675f, 0.6f),
            K(1.86f, 0.145f, 0.815f, 0.845f, 0.45f, 0.8f, 0.72f, 0.618f),
            K(1.62f, 0.14f, 0.85f, 0.875f, 0.5f, 0.835f, 0.762f, 0.65f),
            K(AxleF, 0.14f, 0.855f, 0.882f, 0.52f, 0.842f, 0.795f, 0.682f),
            K(0.9f, 0.14f, 0.845f, 0.872f, 0.52f, 0.832f, 0.82f, 0.738f),
            K(0.55f, 0.14f, 0.83f, 0.862f, 0.51f, 0.818f, 0.842f, 0.8f, 0f, 0.45f, 0.01f),
            K(0.42f, 0.14f, 0.825f, 0.86f, 0.505f, 0.815f, 0.84f, 0.84f, 1f, 0.45f, 0.02f),
            K(-0.3f, 0.14f, 0.82f, 0.858f, 0.495f, 0.81f, 0.865f, 0.865f, 1f, 0.45f, 0.025f),
            K(-1.0f, 0.14f, 0.835f, 0.872f, 0.49f, 0.82f, 0.895f, 0.9f, 1f, 0.45f, 0.015f),
            K(-1.12f, 0.14f, 0.85f, 0.882f, 0.49f, 0.828f, 0.905f, 0.915f),
            K(AxleR, 0.14f, 0.862f, 0.8875f, 0.5f, 0.832f, 0.925f, 0.93f),
            K(-1.75f, 0.16f, 0.845f, 0.87f, 0.52f, 0.8f, 0.95f, 0.962f),
            K(-1.96f, 0.19f, 0.78f, 0.81f, 0.55f, 0.74f, 0.965f, 0.978f, 0f, 0.6f),
            K(-2.04f, 0.21f, 0.7f, 0.725f, 0.57f, 0.66f, 0.968f, 0.985f, 0f, 0.6f),
            K(-2.075f, 0.23f, 0.64f, 0.66f, 0.57f, 0.6f, 0.9f, 0.915f, 0f, 0.8f),
            K(-2.1f, 0.245f, 0.58f, 0.6f, 0.56f, 0.55f, 0.76f, 0.775f, 0f, 0.9f),
            K(ZR, 0.26f, 0.52f, 0.54f, 0.52f, 0.5f, 0.66f, 0.67f, 0f, 0.9f),
        };

        // ------------------------------------------------------------------ greenhouse

        protected override CabinKey[] CabinKeys() => new[]
        {
            new CabinKey(ScreenBase, 0.825f, 0.73f, 0.825f, 0.6f),
            new CabinKey(-0.3f, 0.865f, 0.735f, 1.262f, 0.565f),
            new CabinKey(-0.62f, 0.877f, 0.73f, 1.285f, 0.565f),
            new CabinKey(BackTop, 0.9f, 0.7f, 1.2f, 0.55f),
            new CabinKey(-1.35f, 0.925f, 0.66f, 1.07f, 0.53f),
            new CabinKey(BackBase, 0.952f, 0.6f, 0.962f, 0.5f),
        };

        protected override float[] CabinBreaks => new[]
        {
            ScreenBase - 0.03f, ScreenTop, ScreenTop - 0.03f, BPillar + BPillarW * 0.5f, BPillar - BPillarW * 0.5f, QuarterEnd,
            BackTop, BackTop - 0.03f, BackBase + 0.04f, 0.42f,
        };

        protected override Material CabinMat(int seg, float z)
        {
            bool sideGlass = z < 0.42f && z > QuarterEnd && Mathf.Abs(z - BPillar) > BPillarW * 0.5f;
            switch (seg)
            {
                case 0: return Trim;                                   // belt seal
                case 1:
                case 2: return sideGlass ? Glass : z < QuarterEnd ? Paint : Trim;
                case 3: return z > 0.42f ? Paint : z > QuarterEnd ? Trim : Paint;  // black window frame
                case 4: return Paint;
                default:
                    if (z > ScreenTop) return z > ScreenBase - 0.03f || z < ScreenTop - 0.03f ? Trim : Glass;
                    if (z < BackTop) return z > BackTop - 0.03f || z < BackBase + 0.04f ? Trim : Lib["Car_GlassDark"];
                    return Paint;
            }
        }

        protected override void Details()
        {
            Front();
            Sides();
            Rear();
            GtWing();
        }

        // ------------------------------------------------------------------ shared helpers

        /// <summary>Blank oval badge (no maker artwork): chrome rim, dark field, a plain chrome bar.</summary>
        private void BlankBadge(Vector3 c, Vector3 n, float w, float h)
        {
            var rot = Quaternion.LookRotation(n, Vector3.up);
            CarBlob.Blob(Mb, c, rot, new Vector3(w * 0.5f, h * 0.5f, 0.006f), 1f, Chrome, 0.6f, 1f, 24, 8);
            CarBlob.Blob(Mb, c + n * 0.0035f, rot, new Vector3(w * 0.42f, h * 0.38f, 0.004f), 1f, Lib["Car_Dash"], 0.6f, 1f, 24, 6);
            CarBlob.Blob(Mb, c + n * 0.0062f, rot, new Vector3(w * 0.26f, h * 0.07f, 0.0025f), 0.6f, Chrome, 0.6f, 1f, 12, 6);
        }

        /// <summary>Thin strip following a polyline of front / rear projected points (x signed by side).</summary>
        private void ProjectedStrip(Vector2[] line, float dir, float side, float width, float lift, Material m, float minNz = 0.2f)
        {
            for (int i = 0; i < line.Length - 1; i++)
            {
                Vector2 a = line[i], b = line[i + 1];
                Vector2 d = (b - a).normalized, nrm = new Vector2(-d.y, d.x) * (width * 0.5f);
                Body.ProjectedPatch(Mb, new[] { a - nrm, b - nrm + d * 0.004f, b + nrm + d * 0.004f, a + nrm }, dir, side, lift, m, 2, minNz);
            }
        }
    }
}
