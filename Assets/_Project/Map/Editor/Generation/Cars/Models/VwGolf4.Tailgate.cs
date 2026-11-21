using UnityEngine;

namespace HideAndSeek.Map.Editor
{
    /// <summary>Golf IV tailgate: a separate panel with its gap, crease, plate recess, handle, button badge, wiper, third stop lamp.</summary>
    internal sealed partial class VwGolf4
    {
        private void Tailgate()
        {
            foreach (float s in new[] { -1f, 1f })
            {
                // gap round the tailgate: up both sides along the lamps (the bottom gap runs over the bumper)
                Body.ProjectedPatch(Mb, CarLoft.RectAt(0.428f, 0.785f, 0.006f, 0.4f, 0.001f), -1f, s, 0.0015f, Seam, 2);
            }
            Crease();

            // plate set in a recess: a raised frame round it throws the shadow, the floor stays body colour
            Body.Project(0f, 0.66f, -1f, out var pc, out var pn);
            Vector3 up = (Vector3.up - pn * Vector3.Dot(Vector3.up, pn)).normalized;
            Vector3 right = Vector3.Cross(up, pn).normalized;
            CarKit.Lamp(Mb, pc, pn, right, CarKit.Rect(0.58f, 0.17f, 0.03f), Paint, Seam, 0.001f, 0.02f);
            float fw = 0.3f, fh = 0.095f;
            Vector3 c0 = pc + pn * 0.006f;
            CarBlob.Bar(Mb, c0 - right * fw + up * fh, c0 + right * fw + up * fh, pn, 0.03f, 0.022f, Paint, 0.6f);
            CarBlob.Bar(Mb, c0 - right * fw - up * fh, c0 + right * fw - up * fh, pn, 0.03f, 0.022f, Paint, 0.6f);
            foreach (float s in new[] { -1f, 1f })
                CarBlob.Bar(Mb, c0 + right * (s * fw) - up * fh, c0 + right * (s * fw) + up * fh, pn, 0.03f, 0.022f, Paint, 0.6f);
            CarKit.Plate(Mb, pc + pn * 0.006f, pn, "WA 1J4GT", 0.95f);
            // black handle strip over the plate with the two plate lamps in its underside
            var rot = Quaternion.LookRotation(pn, up);
            Vector3 hc = pc + up * 0.125f + pn * 0.014f;
            Mb.Box(hc, new Vector3(0.36f, 0.032f, 0.026f), Plastic, rot);
            foreach (float s in new[] { -1f, 1f })
                Mb.Box(hc + right * (s * 0.12f) - up * 0.0165f, new Vector3(0.05f, 0.003f, 0.014f), Lens, rot);

            // the VW badge is the tailgate release: a dark round recess with the badge standing in it
            Body.Project(0f, 0.845f, -1f, out var bc, out var bcn);
            CarKit.Lamp(Mb, bc, bcn, right, CarKit.Circle(0.072f, 24), Seam, Seam, 0.001f, 0.01f);
            CarWheels.Lathe(Mb, bc + bcn * 0.001f, bcn, new[] { new Vector2(0.072f, 0f), new Vector2(0.072f, 0.004f), new Vector2(0.066f, 0.006f) }, 24, Paint);
            CarBadges.Vw(Mb, bc + bcn * 0.009f, bcn, 0.06f);

            // rear wiper parked across the bottom of the tailgate glass
            var (wp, wn) = Cabin.OnTopAtX(-1.915f, 0.001f, CarShapes.CRoofEdge, CarShapes.CCentre);
            var (wq, _) = Cabin.OnTopAtX(-1.88f, 0.4f, CarShapes.CRoofEdge, CarShapes.CCentre, -1f);
            CarKit.Wiper(Mb, wp + wn * 0.008f, wq - wp, wn, 0.42f);

            // roof spoiler over the glass with the third stop lamp in its trailing edge
            var srot = Quaternion.Euler(-20f, 0, 0);
            Vector3 sc = new Vector3(0, 1.365f, -1.835f);
            CarBlob.Blob(Mb, sc, srot, new Vector3(0.58f, 0.016f, 0.055f), 0.35f, Paint, 0.5f, 1f, 16, 8);
            Mb.Box(sc + srot * new Vector3(0f, -0.004f, -0.053f), new Vector3(0.3f, 0.012f, 0.008f), Tail, srot);
        }

        /// <summary>
        /// Horizontal crease across the tailgate at the lamp tops: the panel above stands 8.5 mm proud and ends in a
        /// sharp edge; the bevel under it faces down and stays dark even under a torch; it ends at the gap by the lamps.
        /// </summary>
        private void Crease()
        {
            const float yc = 0.93f;
            // (height above the crease line, stand-off): soft rise, sharp edge, steep bevel facing down, dark foot line
            var prof = new[] { new Vector2(0.07f, 0f), new Vector2(0.04f, 0.0042f), new Vector2(0.015f, 0.0075f), new Vector2(0.003f, 0.0085f),
                new Vector2(0f, 0.0085f), new Vector2(-0.004f, 0.0042f), new Vector2(-0.0075f, 0.0012f), new Vector2(-0.009f, 0f), new Vector2(-0.0135f, 0f) };
            const int n = 44;
            var P = new Vector3[n + 1, prof.Length];
            var H = new Vector3[n + 1, prof.Length];
            for (int i = 0; i <= n; i++)
            {
                float x = Mathf.Lerp(-0.424f, 0.424f, i / (float)n);
                float end = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.424f, 0.405f, Mathf.Abs(x)));
                for (int k = 0; k < prof.Length; k++)
                {
                    Body.Project(x, yc + prof[k].x, -1f, out var p, out var nn);
                    P[i, k] = p + nn * (prof[k].y * end + 0.0004f);
                    H[i, k] = nn;
                }
            }
            // separate grids keep the edge and the bevel foot sharp: upper face, bevel (paint), foot line (seam colour)
            foreach (var (k0, k1, m) in new[] { (0, 4, Paint), (4, 7, Paint), (7, 8, Seam) })
            {
                var gp = new Vector3[n + 1, k1 - k0 + 1];
                var gh = new Vector3[n + 1, k1 - k0 + 1];
                for (int i = 0; i <= n; i++)
                for (int k = k0; k <= k1; k++) { gp[i, k - k0] = P[i, k]; gh[i, k - k0] = H[i, k]; }
                Grid(gp, gh, m);
            }
        }
    }
}
