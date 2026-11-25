using UnityEngine;
using static HideAndSeek.Map.Editor.CarShapes;

namespace HideAndSeek.Map.Editor
{
    /// <summary>GT86 nose: wide lower mouth, fog-lamp pockets, chin lip, swept headlamps with the LED strip, bonnet.</summary>
    internal sealed partial class ToyotaGt86
    {
        /// <summary>Headlamp outline (right half, seen from the front): pointed inner tip, wrapping up round the wing.</summary>
        private static readonly Vector2[] HeadOutline =
        {
            new Vector2(0.36f, 0.585f), new Vector2(0.5f, 0.565f), new Vector2(0.67f, 0.558f), new Vector2(0.775f, 0.578f),
            new Vector2(0.83f, 0.625f), new Vector2(0.845f, 0.69f), new Vector2(0.8f, 0.712f), new Vector2(0.66f, 0.688f),
            new Vector2(0.5f, 0.652f), new Vector2(0.4f, 0.612f),
        };

        private void Front()
        {
            foreach (float s in new[] { -1f, 1f })
            {
                Mouth(s);
                FogPocket(s);
                Headlamp(s);
            }
            // blank badge on the nose above the mouth, plate on the mouth
            if (Body.Project(0f, 0.52f, 1f, out var bc, out var bn)) BlankBadge(bc + bn * 0.004f, bn, 0.105f, 0.07f);
            if (Body.Project(0f, 0.33f, 1f, out var pc, out var pn)) CarKit.Plate(Mb, pc + pn * 0.03f, pn, "KR 86GT", 0.85f);

            // bonnet: shut lines inside the wing peaks, across the cowl and above the mouth
            SeamTop(0.52f, 1.97f, t => Mathf.Lerp(0.63f, 0.43f, t * t));
            SeamAcross(0.52f, 0.63f);
            SeamAcross(1.97f, 0.43f, 0.006f);
            // cowl grille in front of the screen
            foreach (float s in new[] { -1f, 1f })
                Body.PatchX(Mb, s, 0.47f, 0.51f, t => new Vector2(0f, 0.62f), BShoulder, BCentre, 0.002f, Plastic, 4, 8);
            Wipers(0.44f, 0.11f, 0.5f, 0.06f, 0.6f);
        }

        /// <summary>The big lower mouth: grate behind a dark surround, thin horizontal fins, black chin lip under it.</summary>
        private void Mouth(float s)
        {
            var mouth = new[]
            {
                new Vector2(0f, 0.2f), new Vector2(0.38f, 0.2f), new Vector2(0.455f, 0.235f), new Vector2(0.48f, 0.31f),
                new Vector2(0.455f, 0.41f), new Vector2(0.39f, 0.455f), new Vector2(0.2f, 0.465f), new Vector2(0f, 0.465f),
            };
            Body.ProjectedLamp(Mb, mouth, 1f, s, Grille, Plastic, 0.012f, 0.002f, 0.2f);
            foreach (float y in new[] { 0.27f, 0.38f })
                Body.ProjectedPatch(Mb, CarLoft.RectAt(0.22f, y, 0.44f, 0.01f, 0.004f), 1f, s, 0.006f, Plastic, 2, 0.2f);
            // chin lip: a dark strip along the bottom of the bumper, sweeping up at the corners
            ProjectedStrip(new[] { new Vector2(0f, 0.175f), new Vector2(0.4f, 0.172f), new Vector2(0.6f, 0.18f), new Vector2(0.72f, 0.2f) },
                1f, s, 0.03f, 0.005f, Plastic);
        }

        /// <summary>Upright fog-lamp pocket in each bumper corner with a round lamp and a vertical fin.</summary>
        private void FogPocket(float s)
        {
            var pocket = new[]
            {
                new Vector2(0.56f, 0.215f), new Vector2(0.68f, 0.22f), new Vector2(0.745f, 0.28f), new Vector2(0.765f, 0.4f),
                new Vector2(0.73f, 0.435f), new Vector2(0.62f, 0.37f), new Vector2(0.555f, 0.28f),
            };
            Body.ProjectedLamp(Mb, pocket, 1f, s, Grille, Plastic, 0.01f, 0.002f, 0.15f);
            if (Body.Project(s * 0.655f, 0.285f, 1f, out var fp, out var fn)) CarKit.RoundLamp(Mb, fp + fn * 0.002f, fn, 0.034f, Trim, Lens);
            ProjectedStrip(new[] { new Vector2(0.7f, 0.25f), new Vector2(0.725f, 0.33f), new Vector2(0.72f, 0.4f) }, 1f, s, 0.012f, 0.006f, Plastic);
        }

        /// <summary>
        /// Swept headlamp: dark housing, projector and reflector bowls, the LED strip running along the lower edge
        /// and kicking up round the outer end, amber indicator on top of the outer end, clear cover glass.
        /// </summary>
        private void Headlamp(float s)
        {
            const float nz = 0.05f; // the outer end wraps round onto the wing
            Body.ProjectedLamp(Mb, HeadOutline, 1f, s, Lib["Car_PlasticGrey"], Seam, 0.007f, 0.003f, nz);
            Body.ProjectedPatch(Mb, CarLoft.PolyAt(0f, 0f, new Vector2(0.43f, 0.6f), new Vector2(0.7f, 0.585f), new Vector2(0.8f, 0.63f),
                new Vector2(0.78f, 0.675f), new Vector2(0.5f, 0.645f)), 1f, s, 0.005f, Lib["Car_Reflector"], 3, nz);
            foreach (var (x, y, r) in new[] { (0.5f, 0.605f, 0.036f), (0.645f, 0.61f, 0.033f) })
                if (Body.Project(s * x, y, 1f, out var lp, out var ln))
                {
                    var face = (ln + Vector3.forward * 1.5f).normalized; // bowls look ahead, not up the bonnet slope
                    CarKit.RoundLamp(Mb, lp + ln * 0.002f, face, r, Chrome, Lens);
                }
            // the LED strip: along the lower edge, then up the outer end (the "boomerang")
            ProjectedStrip(new[]
            {
                new Vector2(0.4f, 0.592f), new Vector2(0.52f, 0.576f), new Vector2(0.67f, 0.57f), new Vector2(0.77f, 0.59f),
                new Vector2(0.815f, 0.632f), new Vector2(0.828f, 0.685f),
            }, 1f, s, 0.009f, 0.009f, Lens, nz);
            ProjectedStrip(new[] { new Vector2(0.66f, 0.676f), new Vector2(0.76f, 0.694f), new Vector2(0.805f, 0.702f) }, 1f, s, 0.012f, 0.008f, Amber, nz);
            if (Body.Project(s * 0.74f, 0.69f, 1f, out var ap, out var an)) Pm.Blinkers.Add(new Blinker(ap + an * 0.01f, an, new Vector2(0.08f, 0.02f)));
            Body.ProjectedPatch(Mb, HeadOutline, 1f, s, 0.02f, Glass, 4, nz);
        }
    }
}
