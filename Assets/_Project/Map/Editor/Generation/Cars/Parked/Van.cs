using UnityEngine;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// Medium van (Transit Custom / Vito class): short bonnet, tall flat-sided body, sliding side door, two rear doors.
    /// Variant 0 is the panel van (no glass behind the cab), variant 1 the minibus with side windows. 5.0 x 2.0 x 1.98 m.
    /// </summary>
    internal sealed class Van : ParkedCar
    {
        protected override string Model => "Van";

        public Van(int variant) : base(variant)
        {
            Kind = RearKind.Van;
            Cargo = variant == 0;
            L = 5.0f; W = 1.0f; Wb = 3.0f; FrontOver = 0.95f;
            Sill = 0.3f; Nose = 0.93f; Cowl = 1.12f; FaceDepth = 0.32f; Belt = 1.12f; RoofH = 1.98f;
            ScreenZ = 1.62f; RoofFrontZ = 0.78f; RoofRearZ = -2.3f; GlassEndZ = -2.37f;
            BPillar = 0.55f; CPillar = -0.62f; DPillar = -1.95f;
            Tumble = 0.08f; TireR = 0.34f; TireW = 0.215f; RimR = 0.21f;
            RimKind = variant == 0 ? ParkedRim.SteelBlack : ParkedRim.Hubcap;
            RimMaterial = variant == 0 ? "Car_Trim" : "Car_RimSilver";
            PlateText = variant == 0 ? "WN 5D388" : "BI 0A72K";
            Paint0 = "CarPaint_White";
            Weight = 1f;
        }

        protected override float SideFullness => 0.15f;
        protected override float RoofRound => 0.6f;

        protected override Vector2[] HeadLamp => new[]
        {
            new Vector2(0.48f, Nose - 0.16f), new Vector2(0.78f, Nose - 0.17f), new Vector2(0.88f, Nose - 0.12f), new Vector2(0.9f, Nose - 0.02f),
            new Vector2(0.48f, Nose - 0.03f),
        };

        // tall narrow lamps in the rear pillars
        protected override Vector2[] TailLamp => new[]
        {
            new Vector2(0.6f, Bump + 0.06f), new Vector2(0.72f, Bump + 0.06f), new Vector2(0.72f, Belt - 0.02f), new Vector2(0.6f, Belt - 0.02f),
        };

        protected override Vector2[] Grille => CarLoft.RectAt(0.24f, Nose - 0.12f, 0.48f, 0.1f, 0.015f);
        protected override Vector2[] Intake => CarLoft.RectAt(0.35f, Sill + 0.23f, 0.7f, 0.12f, 0.02f);
        protected override float RearPlateY => Bump + 0.12f;
    }
}
