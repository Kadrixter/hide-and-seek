using HideAndSeek.Map;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Dust motes of a flashlight stay on its level. The motes (FlashlightDust shader) glow wherever they are inside the
    /// light cone, with no shadow test, and the cone does not stop at the concrete: a flashlight aimed at the ceiling lit
    /// up motes in the air of the level above, a cloud of sparks over the floor right above the player holding it - also
    /// with shadows on. The shader takes a height band (_FlashSlab) and this sets it to the floor .. ceiling of the
    /// light's level; open (no band) outside the garage and while the light is in a ramp shaft / stairwell opening.
    /// </summary>
    public sealed partial class FlashlightLevelCulling
    {
        /// <summary>Height of the air of a level (floor top to the slab underside above it).</summary>
        private const float CeilingClear = LevelLighting.LevelHeight - 0.4f;
        private static readonly int SlabId = Shader.PropertyToID("_FlashSlab");
        private static readonly Vector4 NoBand = new Vector4(-100000f, 100000f, 0f, 0f);
        private MaterialPropertyBlock _dustMpb;

        private void UpdateDustBand(Entry e, LevelLighting ll)
        {
            if (e.Dust == null) return;
            Vector4 band = NoBand;
            Vector3 p = e.L.transform.position;
            if (ll != null && ll.Restricts(p))
            {
                int lv = LevelLighting.LevelAt(p.y);
                if (ll.LevelBits(p) == 1 << LevelLighting.LayerOf(lv))   // not in an opening: only its own level
                {
                    float floor = -LevelLighting.LevelHeight * lv;
                    band = new Vector4(floor - 0.05f, floor + CeilingClear, 0f, 0f);
                }
            }
            if (band == e.Band) return;
            e.Band = band;
            _dustMpb ??= new MaterialPropertyBlock();
            e.Dust.GetPropertyBlock(_dustMpb);
            _dustMpb.SetVector(SlabId, band);
            e.Dust.SetPropertyBlock(_dustMpb);
        }
    }
}
