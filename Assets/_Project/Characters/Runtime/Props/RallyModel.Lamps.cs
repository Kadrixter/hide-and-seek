using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Rally car lamps and small outside details (2026-11-24): headlamp housings, amber
    /// indicators and side repeaters, fog lamps and the number plate in the front bumper, tail lamp housings, reversing
    /// lamps and the rear plate, a third brake light on the roof, wipers, door handles and the fuel cap. The lamp
    /// lenses have their own material slots (<see cref="MHead"/>, <see cref="MTail"/>, <see cref="MRev"/>,
    /// <see cref="MAmber"/>) so the game can make them glow per car (MaterialPropertyBlock, RcCarLights).
    /// </summary>
    public static partial class RallyModel
    {
        /// <summary>Where the light sources sit (car space) - for the runtime lights.</summary>
        public static readonly Vector3 HeadlightPos = new Vector3(0f, 0.088f, 0.25f);
        public static readonly Vector3 PodPos = new Vector3(0f, 0.118f, 0.216f);
        public static readonly Vector3 TailPos = new Vector3(0f, 0.035f, -0.3f); // low behind the car: lights the floor, not the plate

        static void AddLamps(YarisMesh m, YarisLoft loft)
        {
            var I = Quaternion.identity;

            // ---- headlamps: a thin black housing round the lens
            for (int s = -1; s <= 1; s += 2)
            {
                Side(loft, 0.231f, 0.0885f, RLow, RGM, s, out var hp, out var hn);
                var hr = Quaternion.LookRotation(hn, Vector3.up);
                m.Ellipsoid(hp + hn * 0.0002f, new Vector3(0.0186f, 0.0088f, 0.0024f), hr, MTrim, 14, 6);
                // Amber indicator behind the lamp, on the corner.
                Side(loft, 0.207f, 0.086f, RLow, RGM, s, out var ip, out var inn);
                m.Ellipsoid(ip + inn * 0.0004f, new Vector3(0.0062f, 0.0036f, 0.0018f), Quaternion.LookRotation(inn, Vector3.up), MAmber, 10, 5);
                // Side repeater on the front fender, behind the arch.
                Side(loft, 0.074f, 0.097f, RMax, RSh, s, out var rp, out var rn);
                m.Ellipsoid(rp + rn * 0.0003f, new Vector3(0.0052f, 0.0022f, 0.0012f), Quaternion.LookRotation(rn, Vector3.up), MAmber, 10, 5);
            }

            // ---- front bumper: two round fog lamps with chrome bezels, the number plate between them
            float zf = ZF + 0.0002f;
            for (int s = -1; s <= 1; s += 2)
            {
                var c = new Vector3(s * 0.045f, 0.062f, zf);
                m.Ellipsoid(c, new Vector3(0.0088f, 0.0088f, 0.0016f), I, MChrome, 14, 5);
                m.Ellipsoid(c + new Vector3(0f, 0f, 0.0004f), new Vector3(0.0068f, 0.0068f, 0.0024f), I, MHead, 14, 5);
            }
            Plate(m, new Vector3(0f, 0.0625f, zf), Vector3.forward, 0.05f);

            // ---- rear: tail lamp housings, reversing lamps and the plate on the tail panel
            for (int s = -1; s <= 1; s += 2)
            {
                Side(loft, -0.2255f, 0.098f, RLow, RGB, s, out var tp, out var tn);
                var tr = Quaternion.LookRotation(tn, Vector3.up);
                m.Ellipsoid(tp + tn * 0.0002f, new Vector3(0.0176f, 0.0088f, 0.0022f), tr, MTrim, 14, 6);
                m.Ellipsoid(tp + tn * 0.0005f, new Vector3(0.0048f, 0.0034f, 0.0031f), tr, MRev, 10, 5); // white reversing segment
                var rc = new Vector3(s * 0.06f, 0.067f, ZR - 0.0002f);
                m.Box(rc, new Vector3(0.016f, 0.0075f, 0.0014f), I, MTrim, 0.3f);
                m.Box(rc + new Vector3(0f, 0f, -0.0006f), new Vector3(0.0128f, 0.0052f, 0.0012f), I, MRev, 0.3f);
            }
            Plate(m, new Vector3(0f, 0.073f, ZR - 0.0002f), Vector3.back, 0.052f);

            // ---- third brake light at the back of the roof, above the hatch glass
            Top(loft, ZRoofR + 0.002f, 0f, 1, out var bp, out var bn);
            var br = Quaternion.LookRotation(Vector3.ProjectOnPlane(Vector3.back, bn).normalized, bn);
            m.Box(bp + bn * 0.0012f, new Vector3(0.034f, 0.0026f, 0.0042f), br, MTrim, 0.3f);
            m.Box(bp + bn * 0.0022f, new Vector3(0.028f, 0.0014f, 0.0026f), br, MTail, 0.3f);

            // ---- wipers parked at the bottom of the windscreen
            foreach (float x in new[] { -0.03f, 0.016f })
            {
                Top(loft, 0.068f, Mathf.Abs(x), x < 0f ? -1 : 1, out var wp, out var wn);
                var wr = Quaternion.FromToRotation(Vector3.up, wn) * Quaternion.Euler(0f, -9f, 0f);
                m.Box(wp + wn * 0.0012f, new Vector3(0.038f, 0.0014f, 0.0018f), wr, MTrim, 0.3f);
            }

            // ---- door handles, fuel cap (right rear quarter)
            for (int s = -1; s <= 1; s += 2)
            {
                Side(loft, -0.036f, 0.104f, RMax, RSh, s, out var dp, out var dn);
                m.Box(dp + dn * 0.0008f, new Vector3(0.012f, 0.0026f, 0.0016f), Quaternion.LookRotation(dn, Vector3.up), MTrim, 0.4f);
            }
            Side(loft, -0.168f, 0.101f, RMax, RSh, 1, out var fp, out var fn);
            m.Ellipsoid(fp + fn * 0.0003f, new Vector3(0.0064f, 0.0064f, 0.0010f), Quaternion.LookRotation(fn, Vector3.up), MChrome, 14, 5);
        }

        /// <summary>White number plate with a blue EU band and dark characters, on a surface with normal <paramref name="n"/>.</summary>
        static void Plate(YarisMesh m, Vector3 c, Vector3 n, float w)
        {
            var rot = Quaternion.LookRotation(n, Vector3.up);
            Vector3 right = rot * Vector3.right;
            const float h = 0.012f;
            m.Box(c, new Vector3(w, h, 0.0012f), rot, MDisc, 0.2f);
            // The band sits on the viewer's left.
            m.Box(c + right * (w * 0.5f - 0.003f) + n * 0.0004f, new Vector3(0.006f, h, 0.0012f), rot, MBlue, 0.1f);
            for (int k = 0; k < 5; k++)
                m.Box(c + right * (-w * 0.5f + 0.01f + k * (w - 0.014f) / 5f + 0.002f) + n * 0.0006f, new Vector3(0.0034f, 0.0072f, 0.0008f), rot, MInk, 0.05f);
        }
    }
}
