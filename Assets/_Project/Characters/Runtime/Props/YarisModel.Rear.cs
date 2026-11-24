using UnityEngine;

namespace HideAndSeek.Characters
{
    public static partial class YarisModel
    {
        // ================================================================ rear

        static float TailLow(float s) => Mathf.Lerp(6.05f, 6.5f, s);
        static float TailHigh(float s) => Mathf.Lerp(8.35f, 8.95f, Mathf.Sqrt(s));
        const float TailZ0 = ZR, TailZ1 = -0.1705f;

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
                float z = Mathf.Lerp(TailZ0, -0.1825f, s);
                float k = S(z);
                return new Vector2(z, Mathf.Lerp(Mathf.Lerp(TailLow(k), TailHigh(k), 0.08f), Mathf.Lerp(TailLow(k), TailHigh(k), 0.2f), t));
            }, 6, 4, 0.0012f, MClear);
            // inner part of the cluster on the tailgate face (follows the body outline at the corner)
            EndPoly(m, -1f, new[] { new Vector2(0.0568f, 0.0712f), new Vector2(0.0742f, 0.0712f), new Vector2(0.0716f, 0.0928f), new Vector2(0.0598f, 0.0928f) }, 0.0002f, MTrim);
            EndPoly(m, -1f, new[] { new Vector2(0.058f, 0.0724f), new Vector2(0.0741f, 0.0724f), new Vector2(0.0712f, 0.0916f), new Vector2(0.0608f, 0.0916f) }, 0.0004f, MTail);
            EndPoly(m, -1f, new[] { new Vector2(0.0582f, 0.0738f), new Vector2(0.0741f, 0.0738f), new Vector2(0.0741f, 0.0772f), new Vector2(0.0584f, 0.0772f) }, 0.0006f, MClear);
            Oval(m, -1f, 0.0662f, 0.0848f, 0.0034f, 0.0036f, 0.0006f, MTrim);
            Oval(m, -1f, 0.0662f, 0.0848f, 0.0025f, 0.0027f, 0.0007f, MTail);
            Oval(m, -1f, -0.0662f, 0.0848f, 0.0034f, 0.0036f, 0.0006f, MTrim);
            Oval(m, -1f, -0.0662f, 0.0848f, 0.0025f, 0.0027f, 0.0007f, MTail);
        }

        static void RearEnd(YarisMesh m)
        {
            // Plate recess on the tailgate with the plate lamp garnish, emblem above, TOYOTA / YARIS badges.
            EndQuad(m, -1f, -0.0335f, 0.0335f, 0.0448f, 0.0612f, 0.0002f, MSeam);
            Plate(m, -1f, 0.0468f);
            EndQuad(m, -1f, -0.013f, 0.013f, 0.0712f, 0.0722f, 0.0003f, MChrome);   // tailgate handle garnish
            Emblem(m, -1f, 0.0868f, 0.0066f, 0.0048f, 0.0004f);
            Badge(m, -0.0435f, 0.0848f, 6);  // TOYOTA (viewer's left)
            Badge(m, 0.0425f, 0.0848f, 5);   // YARIS
            // Bumper split line, black lower bumper with the red reflectors, exhaust on the left.
            EndQuad(m, -1f, -0.0745f, 0.0745f, 0.0632f, 0.0642f, 0.0003f, MSeam);
            EndQuad(m, -1f, -0.066f, 0.066f, 0.0345f, 0.0418f, 0.0003f, MTrim);
            EndQuad(m, -1f, -0.064f, 0.064f, 0.0415f, 0.0425f, 0.0004f, MSeam);
            EndQuad(m, -1f, 0.05f, 0.0605f, 0.0478f, 0.0505f, 0.0004f, MTail, true);
            m.Box(new Vector3(-0.047f, 0.0292f, ZR + 0.004f), new Vector3(0.0068f, 0.0052f, 0.016f), Quaternion.identity, MRim, 0.7f);
            m.Box(new Vector3(-0.047f, 0.0292f, ZR - 0.0041f), new Vector3(0.0046f, 0.0032f, 0.0006f), Quaternion.identity, MUnder, 0.2f);
        }

        /// <summary>Small chrome lettering: <paramref name="n"/> letter blocks centred on (cx, cy) on the tailgate.</summary>
        static void Badge(YarisMesh m, float cx, float cy, int n)
        {
            const float lw = 0.0024f, gap = 0.0009f, lh = 0.0026f;
            float x0 = cx - (n * lw + (n - 1) * gap) * 0.5f;
            for (int i = 0; i < n; i++)
            {
                float x = x0 + i * (lw + gap);
                EndQuad(m, -1f, x, x + lw, cy - lh * 0.5f, cy + lh * 0.5f, 0.0004f, MChrome);
                EndQuad(m, -1f, x + lw * 0.3f, x + lw * 0.7f, cy - lh * 0.25f, cy + lh * 0.25f, 0.0005f, MPaint);
            }
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
