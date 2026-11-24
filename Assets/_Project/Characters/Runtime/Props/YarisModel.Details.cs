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

        // ================================================================ sides

        static void Seams(YarisMesh m, YarisLoft L)
        {
            const float g = 0.00034f; // half width of a panel gap
            void Vertical(float z, float u0, float u1, int slot = MSeam)
                => L.Sheet(m, (s, t) => new Vector2(z - g + 2f * g * t, Mathf.Lerp(u0, u1, s)), 10, 1, 0.0005f, slot);
            Vertical(0.0988f, RRk + 0.4f, RGB);        // front door front edge
            Vertical(-0.0145f, RRk + 0.4f, RGB);       // front / rear door
            Vertical(-0.1068f, RRk + 0.6f, RGB);       // rear door rear edge
            // Hood shut lines: along the fender top from the cowl to the headlight tail, then over the lamp to the nose.
            L.Sheet(m, (s, t) => new Vector2(Mathf.Lerp(ZCowlF + 0.0005f, HeadZ0 - 0.002f, s), 8.18f + 0.12f * t), 16, 1, 0.0005f, MSeam);
            L.Sheet(m, (s, t) =>
            {
                float z = Mathf.Lerp(HeadZ0 - 0.002f, HeadZ1, s);
                return new Vector2(z, HeadHigh(HeadS(z)) + 0.08f + 0.12f * t);
            }, 16, 1, 0.0005f, MSeam);
            // Hood front edge across the nose.
            L.Sheet(m, (s, t) => new Vector2(ZF - 0.0016f + 0.0008f * t, Mathf.Lerp(HeadHigh(1f) + 0.1f, RC, s)), 8, 1, 0.0005f, MSeam);
            // Fuel filler flap (right rear quarter).
            float fz = -0.152f, fy = 0.0955f;
            float fu0 = L.USide(fz, fy - 0.0055f, RLow, RGB), fu1 = L.USide(fz, fy + 0.0055f, RLow, RGB);
            L.Sheet(m, (s, t) => new Vector2(fz - 0.0058f + 0.0116f * s, Mathf.Lerp(fu0, fu1, t)), 2, 2, 0.0002f, MSeam, false);
            L.Sheet(m, (s, t) => new Vector2(fz - 0.0052f + 0.0104f * s, Mathf.Lerp(fu0 + 0.06f, fu1 - 0.06f, t)), 2, 2, 0.00035f, MPaint, false);
        }

        static void Handles(YarisMesh m, YarisLoft L)
        {
            foreach (float z in new[] { 0.0035f, -0.0905f })
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
            const float z = 0.111f;
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
            L.Sheet(m, (s, t) => new Vector2(Mathf.Lerp(ZCGlassR - 0.002f, ZSailF + 0.001f, s), RGB - 0.22f + 0.28f * t), 24, 1, 0.0003f, MTrim);
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
