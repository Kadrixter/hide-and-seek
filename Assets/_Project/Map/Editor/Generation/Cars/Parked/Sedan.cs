using UnityEngine;

namespace HideAndSeek.Map.Editor
{
    /// <summary>Mid-size saloon with a boot (Passat / Octavia class): 4.7 x 1.8 x 1.45 m, wheelbase 2.75 m.</summary>
    internal sealed class Sedan : ParkedCar
    {
        protected override string Model => "Sedan";

        public Sedan(int variant) : base(variant)
        {
            Kind = RearKind.Notch;
            L = 4.7f; W = 0.9f; Wb = 2.75f; FrontOver = 0.92f;
            Sill = 0.2f; Nose = 0.79f; Cowl = 0.93f; FaceDepth = 0.25f; Belt = 0.99f; RoofH = 1.45f; DeckH = 1.0f;
            ScreenZ = 1.38f; RoofFrontZ = 0.5f; RoofRearZ = -0.95f; GlassEndZ = -1.48f;
            BPillar = 0.02f; CPillar = -0.9f;
            TireR = 0.32f; TireW = 0.215f; RimR = 0.21f;
            RimKind = variant == 0 ? ParkedRim.Mesh : ParkedRim.Alloy5;
            RimMaterial = variant == 0 ? "Car_RimSilver" : "Car_RimGraphite";
            PlateText = variant == 0 ? "WE 8M204" : "DW 2PL95";
            Paint0 = variant == 0 ? "CarPaint_Black" : "CarPaint_Grey";
            Weight = 1.1f;
        }

        protected override Vector2[] HeadLamp => new[]
        {
            new Vector2(0.4f, Nose - 0.15f), new Vector2(0.62f, Nose - 0.16f), new Vector2(0.76f, Nose - 0.13f), new Vector2(0.81f, Nose - 0.05f),
            new Vector2(0.8f, Nose - 0.015f), new Vector2(0.4f, Nose - 0.025f),
        };

        // wide lamps on the boot face, wrapping round the corner
        protected override Vector2[] TailLamp => new[]
        {
            new Vector2(0.42f, DeckH - 0.2f), new Vector2(0.8f, DeckH - 0.2f), new Vector2(0.83f, DeckH - 0.1f), new Vector2(0.8f, DeckH - 0.055f),
            new Vector2(0.42f, DeckH - 0.06f),
        };

        protected override Vector2[] Grille => CarLoft.RectAt(0.19f, Nose - 0.075f, 0.38f, 0.09f, 0.012f);
        protected override bool ChromeGrille => true;
        protected override float RearPlateY => DeckH - 0.14f;
    }
}
