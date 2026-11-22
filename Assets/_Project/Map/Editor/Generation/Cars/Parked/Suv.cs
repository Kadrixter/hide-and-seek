using UnityEngine;

namespace HideAndSeek.Map.Editor
{
    /// <summary>Compact SUV (Tiguan / Sportage class): high ride, big wheels, black arch cladding, 4.55 x 1.86 x 1.68 m.</summary>
    internal sealed class Suv : ParkedCar
    {
        protected override string Model => "Suv";

        public Suv(int variant) : base(variant)
        {
            Kind = RearKind.Estate;
            L = 4.55f; W = 0.93f; Wb = 2.68f; FrontOver = 0.9f;
            Sill = 0.33f; Nose = 1.01f; Cowl = 1.13f; FaceDepth = 0.15f; Belt = 1.17f; RoofH = 1.68f;
            ScreenZ = 1.27f; RoofFrontZ = 0.42f; RoofRearZ = -1.9f; GlassEndZ = -2.15f;
            BPillar = -0.2f; CPillar = -1.08f; DPillar = -1.72f;
            Tumble = 0.15f; TireR = 0.36f; TireW = 0.235f; RimR = 0.23f;
            RimKind = variant == 0 ? ParkedRim.Alloy5 : ParkedRim.AlloyMulti;
            RimMaterial = variant == 0 ? "Car_RimSilver" : "Car_RimGraphite";
            RoofRails = true; Cladding = true;
            PlateText = variant == 0 ? "WX 77V12" : "NO 4S9K0";
            Paint0 = variant == 0 ? "CarPaint_White" : "CarPaint_Navy";
            Weight = 1.1f;
        }

        protected override Vector2[] HeadLamp => new[]
        {
            new Vector2(0.47f, Nose - 0.16f), new Vector2(0.72f, Nose - 0.17f), new Vector2(0.82f, Nose - 0.12f), new Vector2(0.84f, Nose - 0.03f),
            new Vector2(0.47f, Nose - 0.025f),
        };

        protected override Vector2[] TailLamp => new[]
        {
            new Vector2(0.46f, Belt - 0.16f), new Vector2(0.82f, Belt - 0.17f), new Vector2(0.84f, Belt - 0.02f), new Vector2(0.5f, Belt - 0.02f),
        };

        protected override Vector2[] Grille => CarLoft.RectAt(0.22f, Nose - 0.11f, 0.44f, 0.11f, 0.02f);
        protected override Vector2[] Intake => CarLoft.RectAt(0.3f, Sill + 0.23f, 0.6f, 0.13f, 0.02f);
        protected override bool ChromeGrille => Variant == 0;
    }
}
