using UnityEngine;

namespace HideAndSeek.Map.Editor
{
    /// <summary>Small five-door city car (Yaris / Corsa class): 3.7 x 1.66 x 1.5 m, wheelbase 2.4 m, tall rounded cabin.</summary>
    internal sealed class CityHatch : ParkedCar
    {
        protected override string Model => "CityHatch";

        public CityHatch(int variant) : base(variant)
        {
            Kind = RearKind.Hatch;
            L = 3.7f; W = 0.83f; Wb = 2.4f; FrontOver = 0.72f;
            Sill = 0.19f; Nose = 0.73f; Cowl = 0.95f; Belt = 0.98f; RoofH = 1.5f;
            ScreenZ = 1.0f; RoofFrontZ = 0.18f; RoofRearZ = -1.42f; GlassEndZ = -1.73f;
            BPillar = -0.28f; CPillar = -1.02f;
            Tumble = 0.19f; TireR = 0.29f; TireW = 0.185f; RimR = 0.18f;
            RimKind = variant == 0 ? ParkedRim.Hubcap : ParkedRim.AlloyMulti;
            PlateText = variant == 0 ? "PO 3H812" : "GD 51RX7";
            Paint0 = variant == 0 ? "CarPaint_Red" : "CarPaint_White";
            Weight = 0.9f;
        }

        protected override float SideFullness => 0.45f;

        // big rounded lamps swept up into the wings
        protected override Vector2[] HeadLamp => new[]
        {
            new Vector2(0.3f, Nose - 0.12f), new Vector2(0.52f, Nose - 0.15f), new Vector2(0.68f, Nose - 0.13f), new Vector2(0.75f, Nose - 0.07f),
            new Vector2(0.74f, Nose - 0.01f), new Vector2(0.6f, Nose - 0.005f), new Vector2(0.3f, Nose - 0.04f),
        };

        // tall lamps standing in the D-pillar corners
        protected override Vector2[] TailLamp => new[]
        {
            new Vector2(0.5f, Belt - 0.24f), new Vector2(0.74f, Belt - 0.25f), new Vector2(0.76f, Belt - 0.02f), new Vector2(0.6f, Belt - 0.015f),
        };

        protected override Vector2[] Grille => CarLoft.RectAt(0.13f, Nose - 0.09f, 0.26f, 0.05f, 0.02f);
        protected override Vector2[] Intake => CarLoft.RectAt(0.3f, Sill + 0.22f, 0.6f, 0.12f, 0.035f);
    }
}
