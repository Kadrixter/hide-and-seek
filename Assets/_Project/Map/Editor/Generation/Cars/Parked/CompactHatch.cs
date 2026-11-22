using UnityEngine;

namespace HideAndSeek.Map.Editor
{
    /// <summary>Five-door compact hatchback (Golf / Astra class): 4.25 x 1.76 x 1.47 m, wheelbase 2.6 m.</summary>
    internal sealed class CompactHatch : ParkedCar
    {
        protected override string Model => "CompactHatch";

        public CompactHatch(int variant) : base(variant)
        {
            Kind = RearKind.Hatch;
            L = 4.25f; W = 0.88f; Wb = 2.6f; FrontOver = 0.88f;
            Sill = 0.2f; Nose = 0.8f; Cowl = 0.96f; FaceDepth = 0.27f; Belt = 1.0f; RoofH = 1.47f;
            ScreenZ = 0.95f; RoofFrontZ = 0.06f; RoofRearZ = -1.66f; GlassEndZ = -2.0f;
            BPillar = -0.42f; CPillar = -1.25f;
            TireR = 0.31f; RimR = 0.2f;
            RimKind = variant == 0 ? ParkedRim.Hubcap : ParkedRim.Alloy5;
            PlateText = variant == 0 ? "WI 4K271" : "KR 7TC33";
            Paint0 = variant == 0 ? "CarPaint_Silver" : "CarPaint_Blue";
            Weight = 1.1f;
        }

        protected override Vector2[] HeadLamp => new[]
        {
            new Vector2(0.36f, Nose - 0.155f), new Vector2(0.58f, Nose - 0.17f), new Vector2(0.72f, Nose - 0.14f), new Vector2(0.79f, Nose - 0.06f),
            new Vector2(0.78f, Nose - 0.02f), new Vector2(0.36f, Nose - 0.025f),
        };

        protected override Vector2[] TailLamp => new[]
        {
            new Vector2(0.44f, Belt - 0.2f), new Vector2(0.79f, Belt - 0.22f), new Vector2(0.81f, Belt - 0.04f), new Vector2(0.5f, Belt - 0.02f),
        };

        protected override bool ChromeGrille => Variant == 1;
    }
}
