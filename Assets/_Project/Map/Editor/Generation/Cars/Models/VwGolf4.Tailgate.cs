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
            // horizontal crease at the height of the lamp tops: a soft ridge running into the lamps
            var ridge = new System.Collections.Generic.List<Vector3>();
            for (int i = -4; i <= 4; i++)
                if (Body.Project(i * 0.105f, 0.93f, -1f, out var rp, out _)) ridge.Add(rp);
            for (int i = 0; i < ridge.Count - 1; i++)
                CarBlob.Bar(Mb, ridge[i], ridge[i + 1], Vector3.up, 0.022f, 0.012f, Paint, 0.8f);

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
            Body.Project(0f, 0.86f, -1f, out var bc, out var bcn);
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
    }
}
