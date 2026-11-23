using UnityEngine;

namespace HideAndSeek.Characters
{
    public static partial class YarisModel
    {
        // ================================================================ rear

        static float TailLow(float s) => Mathf.Lerp(6.0f, 6.55f, s);
        static float TailHigh(float s) => Mathf.Lerp(8.45f, 9.15f, Mathf.Sqrt(s));
        const float TailZ0 = ZR, TailZ1 = -0.1715f;

        static void TailLamps(YarisMesh m, YarisLoft L)
        {
            float S(float z) => Mathf.Clamp01((z - TailZ0) / (TailZ1 - TailZ0));
            L.Sheet(m, (s, t) =>
            {
                float z = Mathf.Lerp(TailZ0, TailZ1 + 0.002f, s);
                return new Vector2(z, Mathf.Lerp(TailLow(S(z)) - 0.14f, TailHigh(S(z)) + 0.1f, t));
            }, 10, 12, 0.0005f, MTrim);
            L.Sheet(m, (s, t) =>
            {
                float z = Mathf.Lerp(TailZ0, TailZ1, s);
                return new Vector2(z, Mathf.Lerp(TailLow(S(z)), TailHigh(S(z)), t));
            }, 10, 12, 0.0009f, MTail);
            // clear reverse lamp band across the middle of the corner part
            L.Sheet(m, (s, t) =>
            {
                float z = Mathf.Lerp(TailZ0, -0.1845f, s);
                float k = S(z);
                return new Vector2(z, Mathf.Lerp(Mathf.Lerp(TailLow(k), TailHigh(k), 0.36f), Mathf.Lerp(TailLow(k), TailHigh(k), 0.54f), t));
            }, 6, 4, 0.0012f, MClear);
            // inner part of the cluster on the tailgate face (follows the body outline at the corner)
            EndPoly(m, -1f, new[] { new Vector2(0.0495f, 0.0745f), new Vector2(0.0738f, 0.0745f), new Vector2(0.0722f, 0.1015f), new Vector2(0.0505f, 0.1015f) }, 0.0002f, MTrim);
            EndPoly(m, -1f, new[] { new Vector2(0.0508f, 0.0757f), new Vector2(0.0738f, 0.0757f), new Vector2(0.0722f, 0.1003f), new Vector2(0.0518f, 0.1003f) }, 0.0004f, MTail);
            EndPoly(m, -1f, new[] { new Vector2(0.0508f, 0.0858f), new Vector2(0.0738f, 0.0858f), new Vector2(0.0734f, 0.0905f), new Vector2(0.0508f, 0.0905f) }, 0.0006f, MClear);
            Oval(m, -1f, 0.0585f, 0.0945f, 0.0042f, 0.0042f, 0.0006f, MTrim);
            Oval(m, -1f, 0.0585f, 0.0945f, 0.0032f, 0.0032f, 0.0007f, MTail);
            Oval(m, -1f, -0.0585f, 0.0945f, 0.0042f, 0.0042f, 0.0006f, MTrim);
            Oval(m, -1f, -0.0585f, 0.0945f, 0.0032f, 0.0032f, 0.0007f, MTail);
        }

        /// <summary>Convex polygon (right side, x &gt; 0, counter-clockwise seen from the viewer) on an end cap, mirrored.</summary>
        static void EndPoly(YarisMesh m, float dir, Vector2[] pts, float lift, int slot)
        {
            float z = (dir > 0f ? ZF : ZR) + dir * lift;
            var n = new Vector3(0f, 0f, dir);
            foreach (float sx in new[] { 1f, -1f })
                for (int i = 1; i < pts.Length - 1; i++)
                {
                    Vector3 a = new Vector3(pts[0].x * sx, pts[0].y, z), b = new Vector3(pts[i].x * sx, pts[i].y, z), c = new Vector3(pts[i + 1].x * sx, pts[i + 1].y, z);
                    // orientation from the actual winding so it works for both sides and both ends
                    if (Vector3.Dot(Vector3.Cross(b - a, c - a), n) > 0f) m.Tri(a, b, c, n, n, n, slot);
                    else m.Tri(a, c, b, n, n, n, slot);
                }
        }

        static void RearEnd(YarisMesh m)
        {
            // Plate recess on the tailgate, emblem above it, bumper split line, reflectors, exhaust.
            EndQuad(m, -1f, -0.033f, 0.033f, 0.0695f, 0.0865f, 0.0002f, MSeam);
            Plate(m, -1f, 0.0715f);
            EndQuad(m, -1f, -0.022f, 0.022f, 0.0872f, 0.0886f, 0.0003f, MChrome); // plate light bar / garnish
            Emblem(m, -1f, 0.0945f, 0.0062f, 0.0045f, 0.0004f);
            EndQuad(m, -1f, -0.0735f, 0.0735f, 0.0638f, 0.0648f, 0.0003f, MSeam);
            EndQuad(m, -1f, 0.054f, 0.0645f, 0.0435f, 0.047f, 0.0004f, MTail, true);
            EndQuad(m, -1f, -0.046f, 0.046f, 0.0292f, 0.0335f, 0.0003f, MTrim); // diffuser lip
            m.Box(new Vector3(-0.047f, 0.0262f, ZR + 0.004f), new Vector3(0.0068f, 0.0052f, 0.016f), Quaternion.identity, MRim, 0.7f);
            m.Box(new Vector3(-0.047f, 0.0262f, ZR - 0.0041f), new Vector3(0.0046f, 0.0032f, 0.0006f), Quaternion.identity, MUnder, 0.2f);
        }

        static void Spoiler(YarisMesh m)
        {
            // Body-colour roof spoiler: continues the roof crown over the hatch glass and ends in a thin lip; the third
            // brake light sits under its trailing edge.
            const int n = 10;
            float zA = ZRoofR + 0.006f, zB = ZRoofR - 0.0095f;
            float hw = RoofW(ZRoofR) - 0.006f;
            var top = new Vector3[n + 1];
            var lip = new Vector3[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float x = hw * i / n;
                // crown like the roof section: flat in the middle, rolling off to the sides
                float k = x / hw;
                float yA = YRoof(zA) + 0.0004f - 0.0062f * k * k * k * k;
                top[i] = new Vector3(x, yA, zA);
                lip[i] = new Vector3(x, yA + 0.0005f - 0.0012f * k * k, zB);
            }
            for (int s = 0; s < 2; s++)
            {
                float sx = s == 0 ? 1f : -1f;
                Vector3 X(Vector3 v) => new Vector3(v.x * sx, v.y, v.z);
                for (int i = 0; i < n; i++)
                {
                    Vector3 a = X(top[i]), b = X(top[i + 1]), c = X(lip[i + 1]), d = X(lip[i]);
                    Vector3 down = Vector3.down * 0.0026f;
                    m.FlatQuadAuto(a, b, c, d, Vector3.up, MPaint);                         // upper face
                    m.FlatQuadAuto(a + down * 0.6f, b + down * 0.6f, c + down, d + down, Vector3.down, MTrim); // under face
                    m.FlatQuadAuto(d, c, c + down, d + down, Vector3.back, MPaint);         // trailing edge
                }
                Vector3 e0 = X(top[n]), e1 = X(lip[n]);
                m.FlatQuadAuto(e0, e1, e1 + Vector3.down * 0.0026f, e0 + Vector3.down * 0.0016f, new Vector3(sx, 0f, 0f), MPaint); // end
            }
            float yl = lip[0].y - 0.0026f;
            m.Box(new Vector3(0f, yl - 0.0006f, zB + 0.0016f), new Vector3(0.024f, 0.0012f, 0.0018f), Quaternion.identity, MTail, 0.3f);
            // Antenna foot pad on the roof.
            m.Box(new Vector3(0f, YRoof(AntennaPos.z) - 0.0004f, AntennaPos.z), new Vector3(0.008f, 0.0016f, 0.013f), Quaternion.identity, MTrim, 0.6f);
        }
    }
}
