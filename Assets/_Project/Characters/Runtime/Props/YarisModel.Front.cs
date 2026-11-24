using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Front of the XP90 facelift: big clear teardrop headlights swept back and up the fender towards the A-pillar,
    /// black honeycomb upper grille between them with the oval emblem, PL plate on the bumper, wide lower intake.
    /// </summary>
    public static partial class YarisModel
    {
        // Lamp outline: s = 0 at the pointed tail on the fender top .. 1 at the nose. The lower edge (a height on the
        // side) drops down the corner, the upper edge runs along the hood shut line just above the fender crown.
        const float HeadZ0 = 0.154f, HeadZ1 = ZF - 0.0009f;
        static float HeadS(float z) => Mathf.Clamp01((z - HeadZ0) / (HeadZ1 - HeadZ0));
        static float HeadLowY(float s) => Mathf.Lerp(YBelt(HeadZ0) - 0.002f, 0.0682f, Mathf.Pow(s, 0.55f));
        static float HeadHigh(float s) => Mathf.Lerp(8.12f, 8.62f, s);
        static float HeadLow(YarisLoft L, float z, float s) => Mathf.Min(L.USide(z, HeadLowY(s), RLow, RGB), HeadHigh(s) - 0.02f);

        static void Headlights(YarisMesh m, YarisLoft L)
        {
            // thin black seal round the lamp
            L.Sheet(m, (s, t) =>
            {
                float z = Mathf.Lerp(HeadZ0 - 0.003f, HeadZ1, s);
                float k = HeadS(z);
                return new Vector2(z, Mathf.Lerp(HeadLow(L, z, k) - 0.09f, HeadHigh(k) + 0.06f, t));
            }, 20, 6, 0.0002f, MTrim);
            // clear lens over the chrome reflector housing
            L.Sheet(m, (s, t) =>
            {
                float z = Mathf.Lerp(HeadZ0, HeadZ1, s);
                float k = HeadS(z);
                return new Vector2(z, Mathf.Lerp(HeadLow(L, z, k), HeadHigh(k), t));
            }, 20, 6, 0.0004f, MHead);
            // dark trim strip along the bottom of the housing
            L.Sheet(m, (s, t) =>
            {
                float z = Mathf.Lerp(HeadZ0 + 0.016f, HeadZ1, s);
                float k = HeadS(z);
                float u0 = HeadLow(L, z, k);
                return new Vector2(z, Mathf.Lerp(u0 + 0.02f, u0 + 0.14f, t));
            }, 14, 1, 0.0006f, MSeam);
            // big low-beam bowl inboard, high-beam bowl above / behind it, amber indicator bulb towards the tail
            Lamp(m, L, 0.2125f, L.USide(0.2125f, 0.0785f, RLow, RGB), 0.0074f, true);
            Lamp(m, L, 0.1975f, L.USide(0.1965f, 0.0888f, RLow, RGB), 0.0058f, false);
            float ua = L.USide(0.178f, 0.0955f, RLow, RGB);
            Disc(m, L, 0.178f, ua, 0.0028f, 0.0007f, MAmber);
            Disc(m, L, 0.178f, ua, 0.0014f, 0.0008f, MClear);
        }

        /// <summary>Round reflector: chrome bowl, dark ring, bright lens and the bulb cap.</summary>
        static void Lamp(YarisMesh m, YarisLoft L, float zc, float uc, float r, bool big)
        {
            Disc(m, L, zc, uc, r, 0.0006f, MChrome);
            Disc(m, L, zc, uc, r * 0.8f, 0.0007f, MRim);
            Disc(m, L, zc, uc, r * 0.62f, 0.0008f, MClear);
            Disc(m, L, zc, uc, r * (big ? 0.24f : 0.32f), 0.0009f, MChrome);
        }

        /// <summary>A round disc of radius r (metres) laid on the surface at (z, u) (both sides).</summary>
        static void Disc(YarisMesh m, YarisLoft L, float zc, float uc, float r, float lift, int slot)
        {
            L.Sample(zc, uc, out var p0, out _);
            L.Sample(zc, uc + 0.1f, out var p1, out _);
            L.Sample(zc - 0.002f, uc, out var p2, out _);
            float perU = Mathf.Max(1e-5f, (p1 - p0).magnitude / 0.1f);
            float perZ = Mathf.Max(1e-5f, (p2 - p0).magnitude / 0.002f);
            float ru = r / perU, rz = r / perZ;
            L.Sheet(m, (s, t) =>
            {
                float a = s * Mathf.PI * 2f, k = Mathf.Lerp(0.03f, 1f, t);
                return new Vector2(zc + Mathf.Cos(a) * rz * k, uc + Mathf.Sin(a) * ru * k);
            }, 20, 2, lift, slot);
        }

        // ================================================================ flat parts on the end caps

        /// <summary>Flat quad on the front (dir +1) or rear (dir -1) cap at z = end + lift.</summary>
        static void EndQuad(YarisMesh m, float dir, float x0, float x1, float y0, float y1, float lift, int slot, bool mirror = false)
        {
            float z = (dir > 0f ? ZF : ZR) + dir * lift;
            Vector3 a = new Vector3(x0, y0, z), b = new Vector3(x0, y1, z), c = new Vector3(x1, y1, z), d = new Vector3(x1, y0, z);
            Vector3 n = new Vector3(0f, 0f, dir);
            if (dir > 0f) m.Quad(a, d, c, b, n, n, n, n, slot); else m.Quad(a, b, c, d, n, n, n, n, slot);
            if (mirror) EndQuad(m, dir, -x1, -x0, y0, y1, lift, slot);
        }

        /// <summary>Convex half polygon (x &gt;= 0) on an end cap, mirrored to the other side.</summary>
        static void EndPoly(YarisMesh m, float dir, Vector2[] pts, float lift, int slot)
        {
            float z = (dir > 0f ? ZF : ZR) + dir * lift;
            var n = new Vector3(0f, 0f, dir);
            foreach (float sx in new[] { 1f, -1f })
                for (int i = 1; i < pts.Length - 1; i++)
                {
                    Vector3 a = new Vector3(pts[0].x * sx, pts[0].y, z), b = new Vector3(pts[i].x * sx, pts[i].y, z), c = new Vector3(pts[i + 1].x * sx, pts[i + 1].y, z);
                    if (Vector3.Dot(Vector3.Cross(b - a, c - a), n) > 0f) m.Tri(a, b, c, n, n, n, slot);
                    else m.Tri(a, c, b, n, n, n, slot);
                }
        }

        /// <summary>Same half polygon shrunk towards (cx, cy) by <paramref name="k"/> (inner panel of a frame).</summary>
        static Vector2[] Shrink(Vector2[] pts, float k, float cy)
        {
            var r = new Vector2[pts.Length];
            for (int i = 0; i < pts.Length; i++) r[i] = new Vector2(pts[i].x * (1f - k * 0.6f), cy + (pts[i].y - cy) * (1f - k));
            return r;
        }

        /// <summary>Honeycomb: staggered rows of small dark cells inside |x| &lt; halfW(y), y0..y1.</summary>
        static void Honeycomb(YarisMesh m, float dir, float y0, float y1, System.Func<float, float> halfW, float lift)
        {
            const float pitchX = 0.0034f, pitchY = 0.0026f, cw = 0.0021f, ch = 0.0013f;
            int row = 0;
            for (float y = y0 + pitchY * 0.5f; y + ch * 0.5f < y1; y += pitchY, row++)
            {
                float hw = halfW(y) - 0.0012f;
                float off = (row % 2 == 0) ? 0f : pitchX * 0.5f;
                for (float x = off; x + cw * 0.5f < hw; x += pitchX)
                {
                    EndQuad(m, dir, x - cw * 0.5f, x + cw * 0.5f, y - ch * 0.5f, y + ch * 0.5f, lift, MInk);
                    if (x > 1e-4f) EndQuad(m, dir, -x - cw * 0.5f, -x + cw * 0.5f, y - ch * 0.5f, y + ch * 0.5f, lift, MInk);
                }
            }
        }

        /// <summary>Oval Toyota emblem on an end cap: chrome ring, dark field, chrome inner ovals.</summary>
        static void Emblem(YarisMesh m, float dir, float y, float rx, float ry, float lift)
        {
            Oval(m, dir, 0f, y, rx, ry, lift, MChrome);
            Oval(m, dir, 0f, y, rx * 0.8f, ry * 0.76f, lift + 0.0001f, MInk);
            Oval(m, dir, 0f, y + ry * 0.14f, rx * 0.64f, ry * 0.3f, lift + 0.0002f, MChrome);
            Oval(m, dir, 0f, y + ry * 0.14f, rx * 0.5f, ry * 0.17f, lift + 0.0003f, MInk);
            Oval(m, dir, 0f, y - ry * 0.08f, rx * 0.2f, ry * 0.6f, lift + 0.0004f, MChrome);
        }

        static void Oval(YarisMesh m, float dir, float cx, float cy, float rx, float ry, float lift, int slot)
        {
            float z = (dir > 0f ? ZF : ZR) + dir * lift;
            var n = new Vector3(0f, 0f, dir);
            var c = new Vector3(cx, cy, z);
            const int seg = 20;
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
                var p0 = new Vector3(cx + Mathf.Cos(a0) * rx, cy + Mathf.Sin(a0) * ry, z);
                var p1 = new Vector3(cx + Mathf.Cos(a1) * rx, cy + Mathf.Sin(a1) * ry, z);
                if (dir > 0f) m.Tri(c, p0, p1, n, n, n, slot); else m.Tri(c, p1, p0, n, n, n, slot);
            }
        }

        // Upper grille (half outline, x >= 0): straight top under the hood edge, sides and bottom curving into a smile.
        static readonly Vector2[] GrilleUp =
        {
            new(0f, 0.0775f), new(0.0465f, 0.0775f), new(0.0445f, 0.0722f), new(0.037f, 0.0662f), new(0.02f, 0.0631f), new(0f, 0.0625f),
        };
        // Lower intake: wide trapezoid, a little wider at the top ("smile" of the bumper).
        static readonly Vector2[] GrilleLow =
        {
            new(0f, 0.0448f), new(0.0455f, 0.0448f), new(0.0468f, 0.0408f), new(0.0418f, 0.0312f), new(0.03f, 0.0298f), new(0f, 0.0296f),
        };

        static void FrontEnd(YarisMesh m)
        {
            EndPoly(m, 1f, GrilleUp, 0.0003f, MTrim);
            EndPoly(m, 1f, Shrink(GrilleUp, 0.1f, 0.07f), 0.0005f, MUnder);
            Honeycomb(m, 1f, 0.0638f, 0.0765f, y => Mathf.Lerp(0.03f, 0.0445f, Mathf.InverseLerp(0.0638f, 0.0765f, y)), 0.0006f);
            // emblem over the top edge of the grille
            Emblem(m, 1f, 0.0752f, 0.0074f, 0.0054f, 0.0009f);

            EndPoly(m, 1f, GrilleLow, 0.0003f, MTrim);
            EndPoly(m, 1f, Shrink(GrilleLow, 0.1f, 0.0395f), 0.0005f, MUnder);
            Honeycomb(m, 1f, 0.0312f, 0.0438f, y => Mathf.Lerp(0.039f, 0.0445f, Mathf.InverseLerp(0.0312f, 0.0438f, y)), 0.0006f);
            Plate(m, 1f, 0.0462f);
        }

        /// <summary>PL plate 0.057 x 0.0125 (blue EU strip on the viewer's left), dark characters "XX 12345".</summary>
        static void Plate(YarisMesh m, float dir, float y0)
        {
            const float hwp = 0.0285f, h = 0.0125f;
            EndQuad(m, dir, -hwp - 0.0006f, hwp + 0.0006f, y0 - 0.0006f, y0 + h + 0.0006f, 0.0011f, MInk);
            EndQuad(m, dir, -hwp, hwp, y0, y0 + h, 0.0013f, MPlate);
            // viewer's left = +x at the front, -x at the rear
            float eu0 = dir > 0f ? hwp - 0.0055f : -hwp, eu1 = dir > 0f ? hwp : -hwp + 0.0055f;
            EndQuad(m, dir, eu0, eu1, y0, y0 + h, 0.0014f, MEu);
            float start = dir > 0f ? hwp - 0.008f : -hwp + 0.008f, stepX = dir > 0f ? -0.0067f : 0.0067f;
            for (int i = 0; i < 7; i++)
            {
                if (i == 2) continue; // gap between the district code and the number
                float cx = start + stepX * (i + 0.5f);
                EndQuad(m, dir, cx - 0.0021f, cx + 0.0021f, y0 + 0.0025f, y0 + h - 0.0025f, 0.0014f, MInk);
            }
        }
    }
}
