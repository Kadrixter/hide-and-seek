using UnityEngine;
using static HideAndSeek.Map.Editor.CarShapes;

namespace HideAndSeek.Map.Editor
{
    /// <summary>GT86 tail: high wrap-round lamps, boot lid with a ducktail lip, plate recess, diffuser, twin exhausts.</summary>
    internal sealed partial class ToyotaGt86
    {
        /// <summary>Tail lamp outline (right half, seen from behind): pointed inner end, wrapping round the corner.</summary>
        private static readonly Vector2[] TailOutline =
        {
            new Vector2(0.4f, 0.86f), new Vector2(0.5f, 0.83f), new Vector2(0.64f, 0.8f), new Vector2(0.73f, 0.795f),
            new Vector2(0.765f, 0.83f), new Vector2(0.76f, 0.91f), new Vector2(0.62f, 0.935f), new Vector2(0.45f, 0.925f),
        };

        private void Rear()
        {
            foreach (float s in new[] { -1f, 1f }) TailLamp(s);
            BootLid();
            Bumper();
            // high-mounted stop lamp just inside the top of the rear screen, roof mast antenna at the back of the roof
            Mb.Box(new Vector3(0f, 1.158f, -1.085f), new Vector3(0.34f, 0.022f, 0.02f), Tail, Quaternion.Euler(-22f, 0, 0));
            var (ap, an) = Cabin.OnTopAtX(-0.96f, 0.001f, CRoofEdge, CCentre);
            CarBlob.Blob(Mb, ap + an * 0.006f, Quaternion.LookRotation(Vector3.forward, an), new Vector3(0.018f, 0.012f, 0.04f), 0.6f, Plastic, 0.5f);
            CarBlob.Bar(Mb, ap + an * 0.012f, ap + an * 0.012f + new Vector3(0f, 0.09f, -0.2f), Vector3.right, 0.007f, 0.007f, Plastic);
        }

        /// <summary>
        /// Smoked-red lamp: a bright red outer block, an inner clear reverse wedge, an amber band between them and a
        /// red LED loop round the outer block, all under a clear cover.
        /// </summary>
        private void TailLamp(float s)
        {
            Body.ProjectedLamp(Mb, TailOutline, -1f, s, Tail, Seam, 0.007f, 0.003f, 0.12f);
            Body.ProjectedPatch(Mb, CarLoft.PolyAt(0f, 0f, new Vector2(0.6f, 0.83f), new Vector2(0.71f, 0.822f), new Vector2(0.735f, 0.85f),
                new Vector2(0.73f, 0.895f), new Vector2(0.62f, 0.912f), new Vector2(0.585f, 0.87f)), -1f, s, 0.006f, Lib["Car_TailDark"], 3, 0.12f);
            Body.ProjectedPatch(Mb, CarLoft.PolyAt(0f, 0f, new Vector2(0.43f, 0.876f), new Vector2(0.5f, 0.855f), new Vector2(0.505f, 0.912f),
                new Vector2(0.46f, 0.912f)), -1f, s, 0.006f, Lib["Car_Reverse"], 2, 0.12f);
            Body.ProjectedPatch(Mb, CarLoft.PolyAt(0f, 0f, new Vector2(0.52f, 0.85f), new Vector2(0.575f, 0.838f), new Vector2(0.565f, 0.915f),
                new Vector2(0.52f, 0.915f)), -1f, s, 0.006f, Amber, 2, 0.12f);
            ProjectedStrip(new[] { new Vector2(0.6f, 0.86f), new Vector2(0.63f, 0.835f), new Vector2(0.7f, 0.83f), new Vector2(0.72f, 0.86f),
                new Vector2(0.71f, 0.89f), new Vector2(0.63f, 0.9f), new Vector2(0.6f, 0.86f) }, -1f, s, 0.007f, 0.01f, Tail, 0.1f);
            if (Body.Project(s * 0.545f, 0.88f, -1f, out var bp, out var bn)) Pm.Blinkers.Add(new Blinker(bp + bn * 0.012f, bn, new Vector2(0.05f, 0.06f)));
            Body.ProjectedPatch(Mb, TailOutline, -1f, s, 0.016f, Glass, 4, 0.12f);
        }

        /// <summary>Boot lid between the lamps: shut lines, blank badge, ducktail lip along its trailing edge.</summary>
        private void BootLid()
        {
            foreach (float s in new[] { -1f, 1f })
            {
                // sides of the lid: down the inner ends of the lamps, then across above the bumper
                ProjectedStrip(new[] { new Vector2(0.4f, 0.93f), new Vector2(0.4f, 0.87f), new Vector2(0.395f, 0.62f) }, -1f, s, 0.005f, 0.0012f, Seam, 0.2f);
                ProjectedStrip(new[] { new Vector2(0f, 0.618f), new Vector2(0.4f, 0.618f) }, -1f, s, 0.005f, 0.0012f, Seam, 0.2f);
            }
            SeamTop(-2.02f, -1.7f, t => Mathf.Lerp(0.55f, 0.6f, t));
            if (Body.Project(0f, 0.79f, -1f, out var bc, out var bn)) BlankBadge(bc + bn * 0.004f, bn, 0.1f, 0.066f);
            // ducktail: the lid's trailing edge rolled up into a lip
            CarBlob.Blob(Mb, new Vector3(0f, 0.988f, -2.03f), Quaternion.Euler(9f, 0, 0), new Vector3(0.56f, 0.012f, 0.04f), 0.35f, Paint, 0.5f, 1f, 20, 8);
        }

        /// <summary>Rear bumper: plate recess with lamps, corner reflectors, black diffuser with fins, fog lamp, exhausts.</summary>
        private void Bumper()
        {
            if (Body.Project(0f, 0.5f, -1f, out var pc, out var pn))
            {
                CarKit.Lamp(Mb, pc, pn, Vector3.right, CarKit.Rect(0.58f, 0.155f, 0.025f), Plastic, Seam, 0.001f, 0.022f);
                CarKit.Plate(Mb, pc + pn * 0.006f, pn, "KR 86GT", 0.95f);
                foreach (float x in new[] { -0.16f, 0.16f })
                    CarKit.Lamp(Mb, pc + pn * 0.003f + new Vector3(x, 0.066f, 0f), pn, Vector3.right, CarKit.Rect(0.05f, 0.012f, 0.004f), Lens, Plastic, 0.002f, 0.004f);
            }
            foreach (float s in new[] { -1f, 1f })
            {
                // bumper crease and the slim upright reflectors at its corners
                ProjectedStrip(new[] { new Vector2(0.3f, 0.43f), new Vector2(0.6f, 0.44f), new Vector2(0.74f, 0.48f) }, -1f, s, 0.004f, 0.0012f, Seam, 0.15f);
                Body.ProjectedLamp(Mb, CarLoft.RectAt(0.69f, 0.5f, 0.03f, 0.09f, 0.01f), -1f, s, Lib["Car_TailDark"], Seam, 0.004f, 0.003f, 0.12f);
                // diffuser: black lower bumper sweeping up at the outer ends, exhaust cut-outs in it
                var diff = new[]
                {
                    new Vector2(0f, 0.265f), new Vector2(0f, 0.385f), new Vector2(0.3f, 0.385f), new Vector2(0.42f, 0.4f), new Vector2(0.62f, 0.4f),
                    new Vector2(0.72f, 0.43f), new Vector2(0.76f, 0.33f), new Vector2(0.66f, 0.265f),
                };
                Body.ProjectedLamp(Mb, diff, -1f, s, Plastic, Seam, 0.006f, 0.004f, 0.15f);
                if (Body.Project(s * 0.5f, 0.31f, -1f, out var ep, out var en))
                {
                    CarKit.Lamp(Mb, ep, en, Vector3.right * s, CarKit.Circle(0.06f, 22), Lib["Car_Well"], Plastic, 0.005f, 0.02f);
                    CarKit.Exhaust(Mb, new Vector3(s * 0.5f, 0.31f, ep.z - 0.02f), 0.045f, 0.18f);
                }
            }
            // fins under the diffuser edge and the centre fog lamp
            if (Body.Project(0f, 0.34f, -1f, out var dc, out _))
            {
                foreach (float x in new[] { -0.3f, -0.18f, 0.18f, 0.3f })
                    Mb.Box(new Vector3(x, 0.29f, dc.z + 0.03f), new Vector3(0.012f, 0.06f, 0.1f), Plastic);
            }
            if (Body.Project(0f, 0.33f, -1f, out var fc, out var fn))
                CarKit.Lamp(Mb, fc, fn, Vector3.right, CarKit.Rect(0.1f, 0.035f, 0.008f), Tail, Plastic, 0.006f, 0.02f);
        }
    }
}
