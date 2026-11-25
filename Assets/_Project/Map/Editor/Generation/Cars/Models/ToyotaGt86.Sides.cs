using UnityEngine;
using static HideAndSeek.Map.Editor.CarShapes;

namespace HideAndSeek.Map.Editor
{
    /// <summary>GT86 flanks: long doors, the finned wing vent with a blank piston badge, mirrors, handles, fuel flap.</summary>
    internal sealed partial class ToyotaGt86
    {
        private void Sides()
        {
            SeamSide(DoorF, 0.2f, 0.84f);
            SeamSide(DoorR, 0.2f, 0.895f);
            foreach (float s in new[] { -1f, 1f })
            {
                // door bottom shut line just above the sill
                Body.PatchY(Mb, s, DoorR, DoorF, t => new Vector2(0.212f, 0.217f), BLip, BShoulder, 0.0012f, Seam, 14, 1);
                WingVent(s);
                var (mp, _) = Side(0.33f, 0.85f, s);
                CarKit.Mirror(Mb, mp, s, Paint, 0.055f, 0.16f, 0.09f, 0.035f);
                // pull handle in body colour in a dark recess, high on the door towards the B-pillar
                var (hp, hn) = Side(-0.6f, 0.785f, s);
                CarKit.Handle(Mb, hp, hn, Paint, 0.13f);
            }
            // fuel flap on the left rear wing
            var (fp, fn) = Side(-1.62f, 0.8f, -1f);
            CarWheels.Lathe(Mb, fp, fn, new[] { new Vector2(0.075f, 0.0005f), new Vector2(0.07f, 0.0012f), new Vector2(0f, 0.0012f) }, 22, Seam);
            CarWheels.Lathe(Mb, fp + fn * 0.0013f, fn, new[] { new Vector2(0.067f, 0f), new Vector2(0f, 0f) }, 22, Paint);
            // B-pillar finisher: gloss black over the glass between door and quarter light
            GlassOverlay(BPillar - BPillarW * 0.5f - 0.004f, BPillar + BPillarW * 0.5f + 0.004f, t => new Vector2(0f, 1f), Lib["Car_GlassDark"], 3);
        }

        /// <summary>
        /// Vent behind the front wheel: a slanted dark opening (rising to the rear) with four horizontal fins and a
        /// blank chrome piston-shaped badge in its front half.
        /// </summary>
        private void WingVent(float side)
        {
            const float z0 = 0.6f, z1 = 0.83f;
            System.Func<float, Vector2> band = t => new Vector2(Mathf.Lerp(0.555f, 0.515f, t), Mathf.Lerp(0.64f, 0.625f, t));
            Body.PatchY(Mb, side, z0, z1, band, BLip, BShoulder, 0.0008f, Well, 6, 4);
            for (int i = 1; i <= 4; i++)
            {
                float f = i / 5f;
                System.Func<float, Vector2> fin = t => { var b = band(t); float y = Mathf.Lerp(b.x, b.y, f); return new Vector2(y - 0.0035f, y + 0.0035f); };
                Body.PatchY(Mb, side, z0 + 0.004f, z1 - 0.004f, fin, BLip, BShoulder, 0.0015f, Lib["Car_PlasticGrey"], 6, 1);
            }
            // blank piston badge: a flat chrome plate, rounded crown over a narrower skirt (no lettering)
            var (bp, bn) = Side(0.74f, 0.585f, side);
            var piston = new[]
            {
                new Vector2(0.024f, -0.012f), new Vector2(0.024f, 0.004f), new Vector2(0.017f, 0.013f), new Vector2(0f, 0.016f),
                new Vector2(-0.017f, 0.013f), new Vector2(-0.024f, 0.004f), new Vector2(-0.024f, -0.012f), new Vector2(-0.016f, -0.019f),
                new Vector2(0.016f, -0.019f),
            };
            CarKit.Lamp(Mb, bp, bn, Vector3.forward, piston, Chrome, Chrome, 0.004f, 0.002f, 0.0015f);
        }
    }
}
