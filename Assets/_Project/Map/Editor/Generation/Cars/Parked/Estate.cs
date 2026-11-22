using UnityEngine;

namespace HideAndSeek.Map.Editor
{
    /// <summary>Estate / kombi (Passat Variant / Octavia Combi class): long roof, roof rails, 4.75 x 1.8 x 1.5 m.</summary>
    internal sealed class Estate : ParkedCar
    {
        protected override string Model => "Estate";

        public Estate(int variant) : base(variant)
        {
            Kind = RearKind.Estate;
            L = 4.75f; W = 0.9f; Wb = 2.75f; FrontOver = 0.92f;
            Sill = 0.2f; Nose = 0.79f; Cowl = 0.93f; FaceDepth = 0.25f; Belt = 0.99f; RoofH = 1.49f;
            ScreenZ = 1.4f; RoofFrontZ = 0.52f; RoofRearZ = -1.98f; GlassEndZ = -2.25f;
            BPillar = 0.04f; CPillar = -0.88f; DPillar = -1.82f;
            TireR = 0.32f; TireW = 0.215f; RimR = 0.21f;
            RimKind = variant == 0 ? ParkedRim.Hubcap : ParkedRim.AlloyMulti;
            RoofRails = true;
            PlateText = variant == 0 ? "LU 6C140" : "SK 9E3T1";
            Paint0 = variant == 0 ? "CarPaint_Green" : "CarPaint_Silver";
            Weight = 1f;
        }

        protected override Vector2[] HeadLamp => new[]
        {
            new Vector2(0.36f, Nose - 0.16f), new Vector2(0.66f, Nose - 0.17f), new Vector2(0.78f, Nose - 0.12f), new Vector2(0.81f, Nose - 0.03f),
            new Vector2(0.36f, Nose - 0.02f),
        };

        // lamps in the corners of the tailgate, reaching up beside the glass
        protected override Vector2[] TailLamp => new[]
        {
            new Vector2(0.5f, Belt - 0.2f), new Vector2(0.8f, Belt - 0.21f), new Vector2(0.81f, Belt - 0.015f), new Vector2(0.72f, Belt - 0.01f),
            new Vector2(0.7f, Belt - 0.06f), new Vector2(0.5f, Belt - 0.07f),
        };

        protected override bool ChromeGrille => Variant == 1;
    }
}
