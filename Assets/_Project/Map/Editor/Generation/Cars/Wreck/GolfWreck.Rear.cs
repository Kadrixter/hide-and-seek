using UnityEngine;

namespace HideAndSeek.Map.Editor
{
    /// <summary>The (undamaged) tail of the wreck, as on <see cref="VwGolf4"/>: dark lamps, tailgate with crease, plate
    /// recess, handle, VW button badge, wiper, roof spoiler with the third stop lamp; GTI badge on the right.</summary>
    internal sealed partial class GolfWreck
    {
        private void Rear()
        {
            var tail = new[] { new Vector2(0.44f, 0.8f), new Vector2(0.76f, 0.8f), new Vector2(0.8f, 0.83f), new Vector2(0.8f, 0.915f), new Vector2(0.44f, 0.925f) };
            foreach (float s in new[] { -1f, 1f })
            {
                Body.ProjectedLamp(Mb, tail, -1f, s, Lib["Car_TailDark"], Seam, 0.008f, 0.004f, 0.3f);
                Body.ProjectedPatch(Mb, CarLoft.RectAt(0.6f, 0.862f, 0.3f, 0.065f, 0.02f), -1f, s, 0.006f, Tail, 4, 0.3f);
                Body.PatchY(Mb, s, -1.975f, -1.87f, t => new Vector2(Mathf.Lerp(0.805f, 0.83f, t), Mathf.Lerp(0.9f, 0.89f, t)), CarShapes.BLip, CarShapes.BShoulder, 0.004f, Lib["Car_TailDark"], 6, 4);
                Body.PatchY(Mb, s, -1.965f, -1.9f, t => new Vector2(0.835f, 0.88f), CarShapes.BLip, CarShapes.BShoulder, 0.0055f, Tail, 4, 3);
                Body.ProjectedPatch(Mb, CarLoft.RectAt(0.5f, 0.862f, 0.07f, 0.06f, 0.012f), -1f, s, 0.007f, s > 0 ? Lib["Car_Reverse"] : Tail);
                Body.ProjectedPatch(Mb, CarLoft.RectAt(0.72f, 0.862f, 0.07f, 0.06f, 0.012f), -1f, s, 0.008f, Amber, 4, 0.3f);
                Body.ProjectedPatch(Mb, CarLoft.RectAt(0.38f, 0.37f, 0.76f, 0.1f, 0.02f), -1f, s, 0.003f, Plastic, 3);
                Body.ProjectedPatch(Mb, CarLoft.RectAt(0.38f, 0.585f, 0.76f, 0.005f, 0.001f), -1f, s, 0.0015f, Seam, 2);
                Body.ProjectedPatch(Mb, CarLoft.RectAt(0.428f, 0.785f, 0.006f, 0.4f, 0.001f), -1f, s, 0.0015f, Seam, 2);
            }
            Body.Project(0.42f, 0.72f, -1f, out var tp, out _);
            Mb.Text("GOLF", new Vector3(-0.42f, 0.72f, tp.z - 0.003f), Vector3.back, Vector3.up, 0.035f, Lib["Text_White"]);
            Mb.Text("GTI", new Vector3(0.42f, 0.72f, tp.z - 0.003f), Vector3.back, Vector3.up, 0.04f, Lib["Text_White"]);
            CarKit.Exhaust(Mb, new Vector3(-0.5f, 0.27f, -2.075f + 0.1f), 0.04f, 0.2f);

            // plate in its recess, handle strip with the plate lamps
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
            CarKit.Plate(Mb, pc + pn * 0.006f, pn, "LU 4GT02", 0.95f);
            var rot = Quaternion.LookRotation(pn, up);
            Vector3 hc = pc + up * 0.125f + pn * 0.014f;
            Mb.Box(hc, new Vector3(0.36f, 0.032f, 0.026f), Plastic, rot);
            foreach (float s in new[] { -1f, 1f })
                Mb.Box(hc + right * (s * 0.12f) - up * 0.0165f, new Vector3(0.05f, 0.003f, 0.014f), Lens, rot);
            Body.Project(0f, 0.845f, -1f, out var bc, out var bcn);
            CarKit.Lamp(Mb, bc, bcn, right, CarKit.Circle(0.072f, 24), Seam, Seam, 0.001f, 0.01f);
            CarWheels.Lathe(Mb, bc + bcn * 0.001f, bcn, new[] { new Vector2(0.072f, 0f), new Vector2(0.072f, 0.004f), new Vector2(0.066f, 0.006f) }, 24, Paint);
            CarBadges.Vw(Mb, bc + bcn * 0.009f, bcn, 0.06f);
            var (wp, wn) = Cabin.OnTopAtX(-1.915f, 0.001f, CarShapes.CRoofEdge, CarShapes.CCentre);
            var (wq, _) = Cabin.OnTopAtX(-1.88f, 0.4f, CarShapes.CRoofEdge, CarShapes.CCentre, -1f);
            CarKit.Wiper(Mb, wp + wn * 0.008f, wq - wp, wn, 0.42f);
            var srot = Quaternion.Euler(-20f, 0, 0);
            Vector3 sc = new Vector3(0, 1.365f, -1.835f);
            CarBlob.Blob(Mb, sc, srot, new Vector3(0.58f, 0.016f, 0.055f), 0.35f, Paint, 0.5f, 1f, 16, 8);
            Mb.Box(sc + srot * new Vector3(0f, -0.004f, -0.053f), new Vector3(0.3f, 0.012f, 0.008f), Tail, srot);
            TailCrease();
        }

        /// <summary>Horizontal crease across the tailgate at the lamp tops (sharp edge, dark bevel under it).</summary>
        private void TailCrease()
        {
            const float yc = 0.93f;
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
