using UnityEngine;

namespace HideAndSeek.Map.Editor
{
    /// <summary>Materials of the detailed (hero) cars: glass, lenses, interior trim, rims and their special paints.</summary>
    internal sealed partial class MaterialLibrary
    {
        /// <summary>Paints of the hero cars (not part of the random paint pool of the ordinary cars).</summary>
        public static readonly string[] HeroPaints =
        {
            "CarPaint_Arancio", "CarPaint_MontegoBlue", "CarPaint_VelocityRed", "CarPaint_Brilliantrot",
            "CarPaint_Alpinweiss", "CarPaint_Diamantschwarz", "CarPaint_Sunflower", "CarPaint_ReflexSilver"
        };

        private void BuildDetailedCarMaterials()
        {
            // body paints (a little less metallic than the pool so the colour reads under a flashlight)
            Make("CarPaint_Arancio", new Color(0.95f, 0.36f, 0.02f), null, 1f, 0.35f, 0.82f);
            Make("CarPaint_MontegoBlue", new Color(0.05f, 0.2f, 0.42f), null, 1f, 0.5f, 0.8f);
            Make("CarPaint_VelocityRed", new Color(0.5f, 0.02f, 0.03f), null, 1f, 0.5f, 0.82f);
            Make("CarPaint_Brilliantrot", new Color(0.72f, 0.03f, 0.02f), null, 1f, 0.15f, 0.78f);
            Make("CarPaint_Alpinweiss", new Color(0.9f, 0.9f, 0.87f), null, 1f, 0.05f, 0.75f);
            Make("CarPaint_Diamantschwarz", new Color(0.025f, 0.025f, 0.03f), null, 1f, 0.45f, 0.86f);
            Make("CarPaint_Sunflower", new Color(0.98f, 0.76f, 0.02f), null, 1f, 0.15f, 0.8f);
            Make("CarPaint_ReflexSilver", new Color(0.66f, 0.68f, 0.7f), null, 1f, 0.6f, 0.78f);
            // glass and lenses
            Make("Car_Glass", new Color(0.1f, 0.13f, 0.15f, 0.62f), null, 1f, 0.3f, 0.96f, mode: Mode.Transparent);
            Make("Car_MirrorGlass", new Color(0.7f, 0.74f, 0.78f), null, 1f, 0.9f, 0.97f);
            Make("Car_Lens", new Color(0.8f, 0.84f, 0.88f), null, 1f, 0.55f, 0.95f);
            Make("Car_Reflector", new Color(0.88f, 0.88f, 0.9f), "Metal_Brushed", 0.3f, 0.75f, 0.9f);
            Make("Car_LensAmber", new Color(0.95f, 0.5f, 0.06f), null, 1f, 0.2f, 0.9f);
            Make("Car_TailRed", new Color(0.62f, 0.02f, 0.02f), null, 1f, 0.25f, 0.92f);
            Make("Car_TailDark", new Color(0.3f, 0.015f, 0.02f), null, 1f, 0.3f, 0.93f);
            Make("Car_Reverse", new Color(0.86f, 0.86f, 0.86f), null, 1f, 0.3f, 0.93f);
            // trims
            Make("Car_Well", new Color(0.018f, 0.018f, 0.02f), null, 1f, 0f, 0.12f);
            Make("Car_Seam", new Color(0.01f, 0.01f, 0.012f), null, 1f, 0f, 0.2f);
            Make("Car_ChromeBright", new Color(0.86f, 0.86f, 0.88f), "Metal_Brushed", 0.4f, 0.55f, 0.88f);
            Make("Car_Grille", new Color(0.03f, 0.03f, 0.035f), "Grate", 0.12f, 0.1f, 0.4f);
            Make("Car_PlasticGrey", new Color(0.16f, 0.16f, 0.17f), null, 1f, 0f, 0.3f);
            Make("Car_PlateBlue", new Color(0.05f, 0.15f, 0.55f), null, 1f, 0f, 0.5f);
            Make("Car_BadgeBlue", new Color(0.1f, 0.35f, 0.8f), null, 1f, 0.2f, 0.7f);
            Make("Car_BadgeGold", new Color(0.85f, 0.65f, 0.2f), null, 1f, 0.7f, 0.8f);
            // rims
            Make("Car_RimSilver", new Color(0.78f, 0.79f, 0.8f), "Metal_Brushed", 0.5f, 0.45f, 0.72f);
            Make("Car_RimGraphite", new Color(0.22f, 0.23f, 0.24f), null, 1f, 0.5f, 0.6f);
            Make("Car_RimGold", new Color(0.72f, 0.58f, 0.3f), null, 1f, 0.6f, 0.68f);
            Make("Car_Caliper", new Color(0.75f, 0.06f, 0.04f), null, 1f, 0.1f, 0.6f);
            Make("Car_CaliperYellow", new Color(0.95f, 0.75f, 0.05f), null, 1f, 0.1f, 0.6f);
            // interior
            Make("Car_Interior", new Color(0.075f, 0.075f, 0.08f), null, 1f, 0f, 0.18f);
            Make("Car_InteriorGrey", new Color(0.3f, 0.3f, 0.31f), null, 1f, 0f, 0.18f);
            Make("Car_LeatherBlack", new Color(0.05f, 0.05f, 0.05f), null, 1f, 0f, 0.55f);
            Make("Car_LeatherTan", new Color(0.55f, 0.36f, 0.2f), null, 1f, 0f, 0.5f);
            Make("Car_LeatherRed", new Color(0.45f, 0.05f, 0.04f), null, 1f, 0f, 0.5f);
            Make("Car_Dash", new Color(0.06f, 0.06f, 0.065f), null, 1f, 0f, 0.3f);
            Make("Car_Headliner", new Color(0.5f, 0.5f, 0.48f), null, 1f, 0f, 0.1f);

            Make("Car_Gauge", new Color(0.02f, 0.02f, 0.02f), null, 1f, 0f, 0.85f);
            Make("Car_Needle", new Color(0.95f, 0.35f, 0.05f), null, 1f, 0f, 0.5f);
            Make("Car_SoftTop", new Color(0.04f, 0.04f, 0.045f), "Tarp", 0.3f, 0f, 0.2f, "Tarp_N", 1.2f);
        }
    }
}
