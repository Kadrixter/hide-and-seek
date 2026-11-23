using UnityEngine;

namespace HideAndSeek.Characters
{
    public static partial class YarisModel
    {
        /// <summary>Lamps, grilles, plates, seams, handles, mirrors, wipers, spoiler, exhaust - laid on the loft.</summary>
        static void AddDetails(YarisMesh m, YarisLoft L)
        {
            Headlights(m, L);
            FrontEnd(m);
            TailLamps(m, L);
            RearEnd(m);
            Seams(m, L);
            Handles(m, L);
            Mirrors(m, L);
            Glazing(m, L);
            Spoiler(m);
        }

        // ================================================================ front

        /// <summary>Teardrop headlight: from the bumper corner swept back along the fender, up to the hood shut line.</summary>
        static float HeadLow(float s) => 7.35f - 1.05f * Mathf.Sqrt(s);
        static float HeadHigh(float s) => 7.45f + 2.75f * Mathf.Pow(s, 0.8f);
        const float HeadZ0 = 0.158f, HeadZ1 = ZF - 0.0009f;

        static void Headlights(YarisMesh m, YarisLoft L)
        {
            float S(float z) => Mathf.Clamp01((z - HeadZ0) / (HeadZ1 - HeadZ0));
            // black bezel outline (slightly wider, under the lamp)
            L.Sheet(m, (s, t) =>
            {
                float z = Mathf.Lerp(HeadZ0 - 0.003f, HeadZ1, s);
                float k = S(z);
                return new Vector2(z, Mathf.Lerp(HeadLow(k) - 0.16f, HeadHigh(k) + 0.12f, t));
            }, 16, 6, 0.0002f, MTrim);
            // smoked housing behind the clear lens
            L.Sheet(m, (s, t) =>
            {
                float z = Mathf.Lerp(HeadZ0, HeadZ1, s);
                return new Vector2(z, Mathf.Lerp(HeadLow(S(z)), HeadHigh(S(z)), t));
            }, 16, 6, 0.0004f, MGlass);
            // chrome lower edge strip of the housing
            L.Sheet(m, (s, t) =>
            {
                float z = Mathf.Lerp(HeadZ0 + 0.006f, HeadZ1, s);
                float k = S(z);
                return new Vector2(z, Mathf.Lerp(HeadLow(k) + 0.03f, HeadLow(k) + 0.2f, t));
            }, 16, 1, 0.0006f, MRim);
            // two round reflectors: the big main beam at the corner, the smaller projector inboard
            Lamp(m, L, 0.2045f, 7.75f, 0.0088f, true);
            Lamp(m, L, 0.2172f, 9.35f, 0.0062f, false);
            // amber indicator strip along the top rear of the lamp
            L.Sheet(m, (s, t) =>
            {
                float z = Mathf.Lerp(0.174f, 0.193f, s);
                float k = S(z);
                return new Vector2(z, Mathf.Lerp(HeadHigh(k) - 0.3f, HeadHigh(k) - 0.08f, t));
            }, 8, 2, 0.0008f, MAmber);
        }

        /// <summary>Round reflector: chrome bowl, bright lens, dark ring and a small chrome bulb cap.</summary>
        static void Lamp(YarisMesh m, YarisLoft L, float zc, float uc, float r, bool big)
        {
            Disc(m, L, zc, uc, r, 0.0006f, MChrome);
            Disc(m, L, zc, uc, r * 0.78f, 0.0007f, MTrim);
            Disc(m, L, zc, uc, r * 0.68f, 0.0008f, MHead);
            Disc(m, L, zc, uc, r * (big ? 0.26f : 0.34f), 0.0009f, MChrome);
        }

        /// <summary>A round disc of radius r (metres) laid on the surface at (z, u) (both sides).</summary>
        static void Disc(YarisMesh m, YarisLoft L, float zc, float uc, float r, float lift, int slot)
        {
            L.Sample(zc, uc, out var p0, out _);
            L.Sample(zc, uc + 0.1f, out var p1, out _);
            L.Sample(zc + 0.002f, uc, out var p2, out _);
            float perU = Mathf.Max(1e-5f, (p1 - p0).magnitude / 0.1f);
            float perZ = Mathf.Max(1e-5f, (p2 - p0).magnitude / 0.002f);
            float ru = r / perU, rz = r / perZ;
            L.Sheet(m, (s, t) =>
            {
                float a = s * Mathf.PI * 2f, k = Mathf.Lerp(0.03f, 1f, t);
                return new Vector2(zc + Mathf.Cos(a) * rz * k, uc + Mathf.Sin(a) * ru * k);
            }, 20, 2, lift, slot);
        }

        /// <summary>Flat quad on the front (dir +1) or rear (dir -1) cap at z = end + lift.</summary>
        static void EndQuad(YarisMesh m, float dir, float x0, float x1, float y0, float y1, float lift, int slot, bool mirror = false)
        {
            float z = (dir > 0f ? ZF : ZR) + dir * lift;
            Vector3 a = new Vector3(x0, y0, z), b = new Vector3(x0, y1, z), c = new Vector3(x1, y1, z), d = new Vector3(x1, y0, z);
            Vector3 n = new Vector3(0f, 0f, dir);
            if (dir > 0f) m.Quad(a, d, c, b, n, n, n, n, slot); else m.Quad(a, b, c, d, n, n, n, n, slot);
            if (mirror) EndQuad(m, dir, -x1, -x0, y0, y1, lift, slot);
        }

        /// <summary>Trapezoid on an end cap (top half width wt at y1, bottom wb at y0), symmetric.</summary>
        static void EndTrap(YarisMesh m, float dir, float wb, float wt, float y0, float y1, float lift, int slot)
        {
            float z = (dir > 0f ? ZF : ZR) + dir * lift;
            Vector3 a = new Vector3(-wb, y0, z), b = new Vector3(-wt, y1, z), c = new Vector3(wt, y1, z), d = new Vector3(wb, y0, z);
            Vector3 n = new Vector3(0f, 0f, dir);
            if (dir > 0f) m.Quad(a, d, c, b, n, n, n, n, slot); else m.Quad(a, b, c, d, n, n, n, n, slot);
        }

        /// <summary>Oval Toyota emblem on an end cap: chrome ring, dark field, chrome inner ovals.</summary>
        static void Emblem(YarisMesh m, float dir, float y, float rx, float ry, float lift)
        {
            Oval(m, dir, 0f, y, rx, ry, lift, MChrome);
            Oval(m, dir, 0f, y, rx * 0.8f, ry * 0.78f, lift + 0.0001f, MInk);
            Oval(m, dir, 0f, y + ry * 0.12f, rx * 0.62f, ry * 0.3f, lift + 0.0002f, MChrome);
            Oval(m, dir, 0f, y - ry * 0.05f, rx * 0.2f, ry * 0.62f, lift + 0.0003f, MChrome);
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

        static void FrontEnd(YarisMesh m)
        {
            // Upper grille slot with the emblem, chrome strip on its top edge.
            EndQuad(m, 1f, -0.03f, 0.03f, 0.0555f, 0.0628f, 0.0003f, MTrim);
            EndQuad(m, 1f, -0.0285f, 0.0285f, 0.0565f, 0.0618f, 0.0005f, MUnder);
            for (int i = 0; i < 2; i++) EndQuad(m, 1f, -0.0285f, 0.0285f, 0.058f + i * 0.0018f, 0.0586f + i * 0.0018f, 0.0006f, MTrim);
            EndQuad(m, 1f, -0.03f, 0.03f, 0.0628f, 0.0638f, 0.0004f, MChrome);
            Emblem(m, 1f, 0.0598f, 0.0058f, 0.0042f, 0.0009f);
            // Wide lower intake (trapezoid) with horizontal bars, licence plate on it.
            EndTrap(m, 1f, 0.0465f, 0.0415f, 0.0285f, 0.0505f, 0.0003f, MTrim);
            EndTrap(m, 1f, 0.0448f, 0.0399f, 0.0297f, 0.0493f, 0.0005f, MUnder);
            for (int i = 0; i < 4; i++)
            {
                float y = 0.0318f + i * 0.0047f;
                float w = Mathf.Lerp(0.0445f, 0.0402f, (y - 0.0297f) / 0.0196f);
                EndQuad(m, 1f, -w, w, y, y + 0.0011f, 0.0006f, MTrim);
            }
            Plate(m, 1f, 0.0372f);
        }

        /// <summary>EU plate 0.057 x 0.0125 (blue strip on the viewer's left), seven dark characters.</summary>
        static void Plate(YarisMesh m, float dir, float y0)
        {
            const float hwp = 0.0285f, h = 0.0125f;
            EndQuad(m, dir, -hwp - 0.0006f, hwp + 0.0006f, y0 - 0.0006f, y0 + h + 0.0006f, 0.0008f, MInk);
            EndQuad(m, dir, -hwp, hwp, y0, y0 + h, 0.001f, MPlate);
            // viewer's left = +x at the front, -x at the rear
            float eu0 = dir > 0f ? hwp - 0.0055f : -hwp, eu1 = dir > 0f ? hwp : -hwp + 0.0055f;
            EndQuad(m, dir, eu0, eu1, y0, y0 + h, 0.0011f, MEu);
            float start = dir > 0f ? hwp - 0.008f : -hwp + 0.008f, stepX = dir > 0f ? -0.0067f : 0.0067f;
            for (int i = 0; i < 7; i++)
            {
                if (i == 2) continue; // gap between the district code and the number
                float cx = start + stepX * (i + 0.5f);
                EndQuad(m, dir, cx - 0.0021f, cx + 0.0021f, y0 + 0.0025f, y0 + h - 0.0025f, 0.0011f, MInk);
            }
        }

        // ================================================================ sides

        static void Seams(YarisMesh m, YarisLoft L)
        {
            const float g = 0.00034f; // half width of a panel gap
            void Vertical(float z, float u0, float u1, int slot = MSeam)
                => L.Sheet(m, (s, t) => new Vector2(z - g + 2f * g * t, Mathf.Lerp(u0, u1, s)), 10, 1, 0.0005f, slot);
            Vertical(0.0972f, RRk + 0.4f, RGB);        // front door front edge
            Vertical(-0.0105f, RRk + 0.4f, RGB);       // front / rear door
            Vertical(-0.0855f, RRk + 0.6f, RGB);       // rear door rear edge
            // Hood shut lines: along the fender top from the cowl to the headlight, then over the headlight to the nose.
            L.Sheet(m, (s, t) =>
            {
                float z = Mathf.Lerp(ZCowlF + 0.0005f, HeadZ0 + 0.002f, s);
                float u = L.UTop(z, Mathf.Lerp(0.081f, 0.0735f, s), RSh, RC);
                return new Vector2(z, u - 0.07f + 0.14f * t);
            }, 16, 1, 0.0005f, MSeam);
            L.Sheet(m, (s, t) =>
            {
                float z = Mathf.Lerp(HeadZ0 + 0.002f, HeadZ1, s);
                float k = Mathf.Clamp01((z - HeadZ0) / (HeadZ1 - HeadZ0));
                return new Vector2(z, HeadHigh(k) + 0.13f + 0.12f * t);
            }, 16, 1, 0.0005f, MSeam);
            // Hood front edge across the nose.
            L.Sheet(m, (s, t) => new Vector2(ZF - 0.0016f + 0.0008f * t, Mathf.Lerp(10.35f, RC, s)), 8, 1, 0.0005f, MSeam);
            // Fuel filler flap (right rear quarter).
            float fz = -0.152f, fy = 0.0955f;
            float fu0 = L.USide(fz, fy - 0.0055f, RLow, RGB), fu1 = L.USide(fz, fy + 0.0055f, RLow, RGB);
            L.Sheet(m, (s, t) => new Vector2(fz - 0.0058f + 0.0116f * s, Mathf.Lerp(fu0, fu1, t)), 2, 2, 0.0002f, MSeam, false);
            L.Sheet(m, (s, t) => new Vector2(fz - 0.0052f + 0.0104f * s, Mathf.Lerp(fu0 + 0.06f, fu1 - 0.06f, t)), 2, 2, 0.00035f, MPaint, false);
            // Side indicator repeater on the front fender.
            float rz = 0.106f, ry = 0.0845f;
            float ru = L.USide(rz, ry, RLow, RGB);
            L.Sheet(m, (s, t) => new Vector2(rz - 0.0032f + 0.0064f * s, ru - 0.1f + 0.2f * t), 2, 1, 0.0004f, MAmber);
        }

        static void Handles(YarisMesh m, YarisLoft L)
        {
            foreach (float z in new[] { 0.0135f, -0.0745f })
            {
                float y = YBelt(z) - 0.0082f;
                float u = L.USide(z, y, RLow, RGB);
                L.Sample(z, u, out var p, out var n);
                var fwd = Vector3.ProjectOnPlane(Vector3.forward, n).normalized;
                var rot = Quaternion.LookRotation(fwd, n);
                // dark finger recess behind the handle, then the body-colour pull handle
                L.Sheet(m, (s, t) => new Vector2(z - 0.0068f + 0.0136f * s, u - 0.16f + 0.32f * t), 2, 1, 0.0003f, MSeam);
                m.Box(p + n * 0.0011f, new Vector3(0.0034f, 0.0022f, 0.0118f), rot, MPaint, 0.7f);
                var pl = new Vector3(-p.x, p.y, p.z);
                var nl = new Vector3(-n.x, n.y, n.z);
                m.Box(pl + nl * 0.0011f, new Vector3(0.0034f, 0.0022f, 0.0118f), Quaternion.LookRotation(Vector3.ProjectOnPlane(Vector3.forward, nl).normalized, nl), MPaint, 0.7f);
            }
        }

        static void Mirrors(YarisMesh m, YarisLoft L)
        {
            const float z = 0.0835f;
            float y = YBelt(z) + 0.0055f;
            float u = L.USide(z, y, RSh, RGT);
            L.Sample(z, u, out var p, out var n);
            foreach (float side in new[] { 1f, -1f })
            {
                Vector3 S(Vector3 v) => new Vector3(v.x * side, v.y, v.z);
                Vector3 bp = S(p), bn = S(n);
                // black triangular base on the door, short arm, body-colour pod, glass on its back face
                var baseRot = Quaternion.LookRotation(Vector3.ProjectOnPlane(Vector3.forward, bn).normalized, bn);
                m.Box(bp + bn * 0.0012f, new Vector3(0.0075f, 0.0026f, 0.0135f), baseRot, MTrim, 0.5f);
                Vector3 pod = bp + new Vector3(side * 0.0125f, 0.004f, -0.003f);
                m.Box(Vector3.Lerp(bp, pod, 0.5f) + new Vector3(0f, -0.0015f, 0f), new Vector3(0.011f, 0.003f, 0.0045f), Quaternion.Euler(0f, 0f, side * 12f), MTrim, 0.5f);
                var podRot = Quaternion.Euler(-4f, side * -8f, 0f);
                m.Ellipsoid(pod, new Vector3(0.0118f, 0.0074f, 0.0068f), podRot, MPaint, 14, 9, -0.38f);
                // glass: flat ellipse just behind the cut plane
                Vector3 gc = pod + podRot * new Vector3(0f, 0f, -0.0068f * 0.38f - 0.0003f);
                var gn = podRot * Vector3.back;
                const int seg = 16;
                for (int i = 0; i < seg; i++)
                {
                    float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
                    Vector3 q0 = gc + podRot * new Vector3(Mathf.Cos(a0) * 0.0104f, Mathf.Sin(a0) * 0.0064f, 0f);
                    Vector3 q1 = gc + podRot * new Vector3(Mathf.Cos(a1) * 0.0104f, Mathf.Sin(a1) * 0.0064f, 0f);
                    m.Tri(gc, q1, q0, gn, gn, gn, MChrome);
                }
                // body-colour indicator strip on the pod's front edge (facelift mirrors with repeaters)
                m.Box(pod + podRot * new Vector3(side * 0.006f, -0.0045f, 0.0035f), new Vector3(0.0065f, 0.0012f, 0.0016f), podRot, MAmber, 0.4f);
            }
        }

        static void Glazing(YarisMesh m, YarisLoft L)
        {
            // Black belt moulding under the side windows, window seals round the windscreen and hatch glass, wipers.
            L.Sheet(m, (s, t) => new Vector2(Mathf.Lerp(ZCGlassR - 0.002f, ZQuarterF + 0.001f, s), RGB - 0.22f + 0.28f * t), 24, 1, 0.0003f, MTrim);
            L.Sheet(m, (s, t) => new Vector2(ZCowl - 0.0012f + 0.0012f * t, Mathf.Lerp(RRE - 0.05f, RC, s)), 6, 1, 0.0003f, MTrim);
            L.Sheet(m, (s, t) => new Vector2(ZRoofF - 0.0002f + 0.0012f * t, Mathf.Lerp(RRE - 0.05f, RC, s)), 6, 1, 0.0003f, MTrim);
            L.Sheet(m, (s, t) => new Vector2(Mathf.Lerp(ZRoofF, ZCowl, s), RRE - 0.05f + 0.12f * t), 10, 1, 0.0003f, MTrim);
            L.Sheet(m, (s, t) => new Vector2(ZHatchB + 0.0002f + 0.0012f * t, Mathf.Lerp(RRE - 0.05f, RC, s)), 6, 1, 0.0003f, MTrim);
            L.Sheet(m, (s, t) => new Vector2(Mathf.Lerp(ZHatchB, ZRoofR, s), RRE - 0.05f + 0.12f * t), 6, 1, 0.0003f, MTrim);
            // Wipers parked at the bottom of the windscreen (blade + arm), rear wiper on the hatch.
            L.Sheet(m, (s, t) => new Vector2(ZCowl - 0.0042f + 0.0009f * t, Mathf.Lerp(RRE + 0.15f, RC - 0.12f, s)), 8, 1, 0.0013f, MInk);
            L.Sheet(m, (s, t) => new Vector2(ZCowl - 0.0028f + 0.0006f * t, Mathf.Lerp(RRE + 0.35f, RC - 0.25f, s)), 6, 1, 0.0019f, MInk);
            L.Sheet(m, (s, t) => new Vector2(ZHatchB + 0.0034f + 0.0010f * t, Mathf.Lerp(RC - 1.7f, RC, s)), 8, 1, 0.0013f, MInk, false);
            L.Sheet(m, (s, t) => new Vector2(ZHatchB + 0.0028f + 0.0022f * t, RC - 0.12f + 0.12f * s), 1, 1, 0.0016f, MInk, false);
        }
    }
}
