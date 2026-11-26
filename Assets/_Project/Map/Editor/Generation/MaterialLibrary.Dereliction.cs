using UnityEngine;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// Materials of the dereliction pass (ParkingGarageBuilder.Dereliction / .Pipes / .Spalls): dripping water, wet and
    /// rusty stains, moss, the dark cavity of spalled concrete, yellowed paper notices, bottles. Made on demand by
    /// <see cref="EnsureDerelictionMaterials"/>, so the main material list stays untouched.
    /// </summary>
    internal sealed partial class MaterialLibrary
    {
        /// <summary>Creates (or updates in place) the dereliction materials once per library.</summary>
        public void EnsureDerelictionMaterials()
        {
            if (_byName.ContainsKey("Drip_Water")) return;
            Make("Drip_Water", new Color(0.55f, 0.62f, 0.66f, 0.55f), null, 1f, 0f, 0.96f, mode: Mode.Transparent);
            Make("Drip_Ring", new Color(0.78f, 0.84f, 0.9f, 0.5f), null, 1f, 0f, 0.96f, mode: Mode.Fade);
            // stains: the puddle blob (white, shape in alpha) tinted
            Make("Stain_Rust", new Color(0.36f, 0.19f, 0.08f, 0.78f), "Puddle", 1f, 0f, 0.2f, mode: Mode.Fade);
            Make("Stain_Moss", new Color(0.12f, 0.18f, 0.06f, 0.85f), "Puddle", 1f, 0f, 0.25f, mode: Mode.Fade);
            Make("Stain_Wet", new Color(0.02f, 0.022f, 0.025f, 0.55f), "Puddle", 1f, 0f, 0.9f, mode: Mode.Fade);
            Make("Stain_Dust", new Color(0.72f, 0.7f, 0.64f, 0.45f), "Puddle", 1f, 0f, 0.05f, mode: Mode.Fade);
            Make("Cavity_Dark", new Color(0.07f, 0.065f, 0.06f), null, 1f, 0f, 0.05f);
            Make("Concrete_Fresh", new Color(0.78f, 0.77f, 0.73f), "Wall_Concrete", 0.5f, 0f, 0.08f, "Wall_Concrete_N", 1.6f);
            Make("Paper_Old", new Color(0.82f, 0.79f, 0.66f), null, 1f, 0f, 0.12f);
            Make("Bottle_Green", new Color(0.06f, 0.22f, 0.08f, 0.75f), null, 1f, 0f, 0.92f, mode: Mode.Transparent);
            Make("Bottle_Brown", new Color(0.22f, 0.1f, 0.03f, 0.8f), null, 1f, 0f, 0.92f, mode: Mode.Transparent);
            Make("Blanket_Old", new Color(0.3f, 0.22f, 0.2f), "Canvas", 0.7f, 0f, 0.08f, "Canvas_N", 1.2f);
        }
    }
}
