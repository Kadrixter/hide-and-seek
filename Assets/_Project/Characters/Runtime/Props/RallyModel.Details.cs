using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>Rally car outside details: lamps, light pod, vents, mirrors, scoop, wing, flaps, splitter, numbers.</summary>
    public static partial class RallyModel
    {
        static void AddDetails(YarisMesh m, YarisLoft loft)
        {
            var I = Quaternion.identity;

            // ---- front: splitter, tow hook, headlights, 4-lamp pod on the hood, vents
            m.Box(new Vector3(0f, 0.034f, ZF - 0.002f), new Vector3(0.17f, 0.004f, 0.03f), I, MUnder, 0.2f);
            m.Box(new Vector3(0f, 0.043f, ZF - 0.003f), new Vector3(0.12f, 0.014f, 0.008f), I, MTrim, 0.3f);
            m.Box(new Vector3(0.032f, 0.04f, ZF + 0.004f), new Vector3(0.007f, 0.006f, 0.012f), I, MRed, 0.3f);
            for (int s = -1; s <= 1; s += 2)
            {
                Side(loft, 0.231f, 0.0885f, RLow, RGM, s, out var hp, out var hn);
                m.Ellipsoid(hp + hn * 0.0006f, new Vector3(0.017f, 0.0075f, 0.0028f), Quaternion.LookRotation(hn, Vector3.up), MHead, 12, 6);
            }
            float podY = YRoof(0.206f) + 0.006f;
            m.Box(new Vector3(0f, podY - 0.003f, 0.205f), new Vector3(0.13f, 0.005f, 0.008f), I, MTrim, 0.3f);
            for (int k = 0; k < 4; k++)
            {
                float x = -0.051f + k * 0.034f;
                var c = new Vector3(x, podY + 0.006f, 0.209f);
                m.Ellipsoid(c, new Vector3(0.0108f, 0.0108f, 0.0065f), I, MTrim, 12, 6);
                m.Ellipsoid(c + new Vector3(0f, 0f, 0.0045f), new Vector3(0.0086f, 0.0086f, 0.0026f), I, MHead, 12, 6);
            }
            for (int s = -1; s <= 1; s += 2)
            {
                Top(loft, 0.142f, 0.034f, s, out var vp, out var vn);
                var r = Quaternion.FromToRotation(Vector3.up, vn);
                m.Box(vp + vn * 0.0006f, new Vector3(0.026f, 0.0018f, 0.03f), r, MTrim, 0.2f);
                for (int g = 0; g < 4; g++)
                    m.Box(vp + vn * 0.0014f + r * new Vector3(0f, 0f, -0.011f + g * 0.0073f), new Vector3(0.022f, 0.0012f, 0.0018f), r, MUnder, 0.1f);
            }

            // ---- mirrors (blue housings on black stalks)
            for (int s = -1; s <= 1; s += 2)
            {
                float x0 = WBase(0.068f) - 0.006f;
                m.Box(new Vector3(s * (x0 + 0.005f), 0.121f, 0.069f), new Vector3(0.012f, 0.003f, 0.006f), I, MTrim, 0.3f);
                m.Box(new Vector3(s * (x0 + 0.013f), 0.124f, 0.066f), new Vector3(0.016f, 0.011f, 0.011f), I, MBlue, 0.45f);
                m.Box(new Vector3(s * (x0 + 0.013f), 0.124f, 0.0604f), new Vector3(0.013f, 0.0085f, 0.001f), I, MChrome, 0.1f);
            }

            // ---- roof scoop
            float ry = YRoof(-0.02f);
            m.Box(new Vector3(0f, ry + 0.0035f, -0.022f), new Vector3(0.04f, 0.008f, 0.038f), I, MBlue, 0.4f);
            m.Box(new Vector3(0f, ry + 0.0037f, -0.0025f), new Vector3(0.032f, 0.0052f, 0.0016f), I, MTrim, 0.1f);

            // ---- rear wing on two posts, gurney flap, end plates
            float hy = YRoof(-0.19f);
            const float wingY = 0.195f, wingZ = -0.202f;
            for (int s = -1; s <= 1; s += 2)
                Tube(m, new Vector3(s * 0.052f, hy - 0.004f, -0.188f), new Vector3(s * 0.052f, wingY - 0.002f, wingZ + 0.006f), 0.0045f, MTrim);
            var tilt = Quaternion.Euler(-7f, 0f, 0f);
            m.Box(new Vector3(0f, wingY, wingZ), new Vector3(0.21f, 0.0045f, 0.05f), tilt, MBlue, 0.3f);
            m.Box(new Vector3(0f, wingY + 0.0026f, wingZ), new Vector3(0.21f, 0.0008f, 0.012f), tilt, MRed, 0.1f);
            m.Box(new Vector3(0f, wingY + 0.0045f, wingZ - 0.0245f), new Vector3(0.21f, 0.006f, 0.0018f), I, MTrim, 0.2f);
            for (int s = -1; s <= 1; s += 2)
                m.Box(new Vector3(s * 0.1065f, wingY + 0.002f, wingZ), new Vector3(0.0028f, 0.032f, 0.06f), I, MTrim, 0.25f);

            // ---- rear: tail lamps on the corners, diffuser, exhaust, red tow hook
            for (int s = -1; s <= 1; s += 2)
            {
                Side(loft, -0.2255f, 0.098f, RLow, RGB, s, out var tp, out var tn);
                m.Ellipsoid(tp + tn * 0.0006f, new Vector3(0.015f, 0.0068f, 0.0026f), Quaternion.LookRotation(tn, Vector3.up), MTail, 12, 6);
            }
            m.Box(new Vector3(0f, 0.046f, ZR - 0.002f), new Vector3(0.13f, 0.012f, 0.012f), I, MUnder, 0.2f);
            for (int f = -2; f <= 2; f++) m.Box(new Vector3(f * 0.022f, 0.042f, ZR - 0.004f), new Vector3(0.002f, 0.012f, 0.014f), I, MTrim, 0.1f);
            m.Box(new Vector3(-0.062f, 0.05f, ZR - 0.004f), new Vector3(0.014f, 0.014f, 0.018f), I, MChrome, 0.35f);
            m.Box(new Vector3(-0.062f, 0.05f, ZR - 0.0128f), new Vector3(0.009f, 0.009f, 0.0006f), I, MUnder, 0.1f);
            m.Box(new Vector3(0.05f, 0.05f, ZR - 0.004f), new Vector3(0.007f, 0.006f, 0.012f), I, MRed, 0.3f);

            // ---- mud flaps behind every wheel
            foreach (float a in new[] { AxleF, AxleR })
                for (int s = -1; s <= 1; s += 2)
                {
                    float z = a - ArchR - 0.003f;
                    m.Box(new Vector3(s * TrackX, 0.032f, z), new Vector3(0.034f, 0.04f, 0.0025f), I, a > 0f ? MBlue : MRed, 0.2f);
                    m.Box(new Vector3(s * TrackX, 0.036f, z - 0.0014f), new Vector3(0.02f, 0.006f, 0.0006f), I, MDisc, 0.1f);
                }

            // ---- door numbers: white disc with a black 7 on both doors, one on the hood
            for (int s = -1; s <= 1; s += 2)
            {
                Side(loft, 0.004f, 0.083f, RLow, RSh, s, out var np, out var nn);
                Number(m, np, nn, Vector3.up, 0.021f);
            }
            Top(loft, 0.172f, 0f, 1, out var hp2, out var hn2);
            Number(m, hp2, hn2, Vector3.back, 0.016f); // readable from in front of the car
        }

        /// <summary>White disc with a black "7" laid on the surface (normal <paramref name="n"/>, digit up = <paramref name="up"/>).</summary>
        static void Number(YarisMesh m, Vector3 p, Vector3 n, Vector3 up, float r)
        {
            up = Vector3.ProjectOnPlane(up, n).normalized;
            var rot = Quaternion.LookRotation(n, up);
            m.Ellipsoid(p + n * 0.0004f, new Vector3(r, r, 0.0011f), rot, MDisc, 16, 6);
            Vector3 right = Vector3.Cross(up, -n).normalized; // the viewer's right, looking at the surface
            Vector3 c = p + n * 0.0013f;
            float k = r / 0.021f;
            m.Box(c + up * 0.0085f * k, new Vector3(0.0155f * k, 0.0038f * k, 0.0008f), rot, MInk, 0.1f);
            Vector3 top = c + up * 0.0085f * k + right * 0.0062f * k, bottom = c - up * 0.0115f * k - right * 0.0015f * k;
            var stem = Quaternion.LookRotation(n, top - bottom);
            m.Box((top + bottom) * 0.5f, new Vector3(0.0042f * k, (top - bottom).magnitude, 0.0008f), stem, MInk, 0.1f);
        }

        /// <summary>A square tube from <paramref name="a"/> to <paramref name="b"/>.</summary>
        static void Tube(YarisMesh m, Vector3 a, Vector3 b, float t, int slot)
        {
            Vector3 d = b - a;
            if (d.sqrMagnitude < 1e-10f) return;
            m.Box((a + b) * 0.5f, new Vector3(t, t, d.magnitude + t * 0.6f), Quaternion.LookRotation(d, Mathf.Abs(d.normalized.y) > 0.95f ? Vector3.forward : Vector3.up), slot, 0.45f);
        }

        /// <summary>Point + normal on the side of the body at (z, y), right side (s = +1) or mirrored (s = -1).</summary>
        static void Side(YarisLoft loft, float z, float y, int from, int to, int s, out Vector3 p, out Vector3 n)
        {
            loft.Sample(z, loft.USide(z, y, from, to), out p, out n);
            if (s < 0) { p.x = -p.x; n.x = -n.x; }
        }

        /// <summary>Point + normal on the top of the body at (z, x).</summary>
        static void Top(YarisLoft loft, float z, float x, int s, out Vector3 p, out Vector3 n)
        {
            loft.Sample(z, loft.UTop(z, Mathf.Abs(x), RGT, RC), out p, out n);
            if (s < 0) { p.x = -p.x; n.x = -n.x; }
        }
    }
}
