namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// Heights of the flat floor decals above the slab, one layer each, at least 5 mm apart. The player camera has a
    /// 3 cm near plane, so with a 24-bit depth buffer two surfaces 1-2 mm apart (the old spacing) flicker from about
    /// 20-30 m; 5 mm holds to roughly 60 m, and nothing of the same layer overlaps.
    /// </summary>
    internal sealed partial class ParkingGarageBuilder
    {
        internal static class DecalY
        {
            public const float Under = 0.005f;   // tile / steel plates laid on the floor
            public const float Stain = 0.010f;   // oil stains, drain grates
            public const float Crack = 0.015f;
            public const float Paint = 0.020f;   // painted lines, zebra crossings
            public const float Hatch = 0.025f;   // yellow hatching (may cross painted lines)
            public const float Mark = 0.030f;    // arrows, floor text
            public const float Puddle = 0.035f;
            public const float Flood = 0.040f;   // flood water, +4 mm per overlapping sheet
        }
    }
}
