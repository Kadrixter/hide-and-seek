using System.Collections.Generic;
using UnityEngine;
using static HideAndSeek.Map.Editor.CarShapes;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// Lamps, grille, plates, mirrors, handles, trims, bumpers and a simple cabin of the parked cars. Everything that
    /// shows at a few metres is a solid part (see ParkedCar.Relief / ParkedCar.Shape): lamp units with bezels and
    /// projector bowls, grilles with a frame and slats, handles with a pocket, cup-shaped mirrors with inset glass.
    /// Nothing is laid onto the bonnet top (it may be split off and opened at map build time).
    /// </summary>
    internal abstract partial class ParkedCar
    {
        /// <summary>Headlamp outline seen from the front (right half, x &gt; 0, metres).</summary>
        protected abstract Vector2[] HeadLamp { get; }
        /// <summary>Tail lamp outline seen from behind (right half).</summary>
        protected abstract Vector2[] TailLamp { get; }
        /// <summary>Grille outline (right half, starting at x = 0); null = no upper grille.</summary>
        protected virtual Vector2[] Grille => CarLoft.RectAt(0.16f, Nose - 0.1f, 0.32f, 0.08f, 0.015f);
        /// <summary>Lower air intake in the bumper (right half).</summary>
        protected virtual Vector2[] Intake => CarLoft.RectAt(0.25f, Sill + 0.22f, 0.5f, 0.1f, 0.02f);
        protected virtual bool ChromeGrille => false;
        /// <summary>Height of the rear number plate centre.</summary>
        protected virtual float RearPlateY => Kind == RearKind.Notch ? Bump + 0.1f : Bump + 0.2f;

        protected override void Interior()
        {
            var cloth = Lib[Variant % 2 == 0 ? "Car_Interior" : "Car_InteriorGrey"];
            var dash = Lib["Car_Dash"];
            float floor = TubFloor, seatZ = ScreenZ - 0.95f;
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 p = new Vector3(s * (W * 0.42f), floor, seatZ);
                CarBlob.Blob(Mb, new Vector3(p.x, floor + 0.2f, p.z), Quaternion.Euler(-6f, 0, 0), new Vector3(0.25f, 0.07f, 0.25f), 0.3f, cloth, 0.3f, 1f, 8, 6);
                var back = Quaternion.Euler(-15f, 0, 0);
                Vector3 bc = new Vector3(p.x, floor + 0.22f, p.z - 0.24f) + back * new Vector3(0, 0.3f, 0);
                CarBlob.Blob(Mb, bc, back, new Vector3(0.24f, 0.3f, 0.065f), 0.3f, cloth, 0.35f, 1f, 8, 6);
                CarBlob.Blob(Mb, bc + back * new Vector3(0, 0.42f, 0), back, new Vector3(0.13f, 0.08f, 0.05f), 0.45f, cloth, 0.45f, 1f, 8, 4);
            }
            if (Kind != RearKind.Van || !Cargo)
                CarInterior.RearBench(Mb, W * 0.7f, floor, floor + 0.22f, seatZ - 0.85f, cloth, 0.52f, 18f);
            else
                Mb.Box(new Vector3(0, (floor + RoofH) * 0.5f, BPillar - 0.25f), new Vector3(W * 1.6f, RoofH - floor - 0.05f, 0.04f), Lib["Car_Headliner"]);
            float dz = ScreenZ - 0.05f;
            Mb.Box(new Vector3(0, Cowl - 0.12f, dz - 0.17f), new Vector3(W * 1.62f, 0.2f, 0.34f), dash);
            Mb.Box(new Vector3(-W * 0.42f, Cowl - 0.005f, dz - 0.28f), new Vector3(0.36f, 0.07f, 0.16f), dash);
            Mb.Box(new Vector3(0, Cowl - 0.3f, dz - 0.38f), new Vector3(0.22f, 0.3f, 0.12f), dash);
            Vector3 sw = new Vector3(-W * 0.42f, Cowl - 0.07f, dz - 0.5f);
            CarWheels.Lathe(Mb, sw, new Vector3(0, 0.45f, 1f), new[] { new Vector2(0.185f, -0.012f), new Vector2(0.2f, 0f), new Vector2(0.185f, 0.012f), new Vector2(0.17f, 0f), new Vector2(0.185f, -0.012f) }, 14, dash, 80f);
            Mb.Cylinder(sw, sw + new Vector3(0, 0.12f, 0.27f), 0.04f, 8, dash);
        }

        protected override void Details()
        {
            Front();
            Sides();
            Rear();
            if (RoofRails)
                foreach (float s in new[] { -1f, 1f })
                    RoofRail(s);
        }

        /// <summary>Outline kept below a height (projections must stay on the lamp face, not run onto the bonnet).</summary>
        protected Vector2[] Below(Vector2[] o, float yMax, float inset = 0.07f) =>
            System.Array.ConvertAll(o, p => new Vector2(Mathf.Min(p.x, W - inset), Mathf.Min(p.y, yMax)));

        // ------------------------------------------------------------------ front

        private void Front()
        {
            var head = Below(HeadLamp, Nose - 0.035f);
            var grille = Grille != null ? Below(Grille, Nose - 0.045f) : null;
            Vector2 c = Centroid(head);
            float span = Span(head), lampH = Height(head);
            float bowlR = Mathf.Clamp(lampH * 0.26f, 0.02f, 0.036f);
            foreach (float s in new[] { -1f, 1f })
            {
                // headlamp unit: shut gap round it, the unit standing proud, a dark bezel round the bright reflector face
                Body.ProjectedPatch(Mb, Offset(head, 0.006f), 1f, s, 0.0012f, Seam);
                Relief(head, 1f, s, 0.009f, Lib["Car_Reflector"], Plastic);
                Frame(Offset(head, -0.011f), 0.011f, 1f, s, 0.0135f, Plastic, Plastic);
                // two projector bowls and the indicator inside it
                foreach (float bx in new[] { c.x - span * 0.2f, c.x + span * 0.08f })
                    if (Body.Project(s * bx, c.y, 1f, out var bp, out var bn))
                        Bowl(bp + bn * 0.009f, bn, bowlR);
                Relief(CarLoft.RectAt(c.x + span * 0.34f, c.y, span * 0.16f, 0.035f, 0.01f), 1f, s, 0.012f, Amber, Plastic);
                if (Body.Project(s * (c.x + span * 0.34f), c.y, 1f, out var ap, out var an))
                    Pm.Blinkers.Add(new Blinker(ap + an * 0.013f, an, new Vector2(span * 0.16f, 0.04f)));
                // grille and lower intake: dark back, a frame standing round it, slats bridging it
                if (grille != null) GrilleUnit(grille, s, ChromeGrille ? Chrome : Plastic, Variant == 1 && !ChromeGrille);
                GrilleUnit(Intake, s, Plastic, Variant == 1);
                // side repeater on the wing
                var (rp, rn) = Side(FrontAxle + TireR + 0.18f, Mathf.Min(Belt - 0.12f, Sill + 0.5f), s);
                CarKit.Lamp(Mb, rp, rn, Vector3.forward, CarKit.Rect(0.05f, 0.022f, 0.008f), Amber, Plastic, 0.004f, 0.015f);
                Pm.Blinkers.Add(new Blinker(rp + rn * 0.004f, rn, new Vector2(0.05f, 0.022f)));
                // bonnet shut line right over the lamps; bumper top line under them with the bumper's edge standing proud
                Body.ProjectedPatch(Mb, CarLoft.RectAt(W * 0.45f, Nose - 0.03f, W * 0.9f, 0.005f, 0.001f), 1f, s, 0.0015f, Seam, 2);
                Body.ProjectedPatch(Mb, CarLoft.RectAt(W * 0.42f, Sill + 0.31f, W * 0.84f, 0.005f, 0.001f), 1f, s, 0.0015f, Seam, 2);
                Relief(CarLoft.RectAt(W * 0.42f, Sill + 0.298f, W * 0.84f, 0.016f, 0.004f), 1f, s, 0.004f, Paint, Paint);
                // fog lamps just outside the ends of the intake (better trims)
                float ix = 0f;
                foreach (var p in Intake) ix = Mathf.Max(ix, p.x);
                float fogX = Mathf.Min(ix + 0.065f, W - 0.17f), fogR = Mathf.Clamp(fogX - ix - 0.018f, 0.028f, 0.045f);
                if (Variant == 1 && Body.Project(s * fogX, Sill + 0.19f, 1f, out var fp, out var fn))
                {
                    CarKit.Lamp(Mb, fp + fn * 0.003f, fn, Vector3.right * s, CarKit.Circle(fogR, 12), Plastic, Plastic, 0.012f, 0.004f);
                    Bowl(fp + fn * 0.015f, fn, fogR - 0.011f);
                }
            }
            // badge on the grille (not on the bonnet)
            if (grille != null)
            {
                float gy0 = float.MaxValue, gy1 = float.MinValue;
                foreach (var p in grille) { gy0 = Mathf.Min(gy0, p.y); gy1 = Mathf.Max(gy1, p.y); }
                float br = Mathf.Min(0.03f, (gy1 - gy0) * 0.4f);
                if (Body.Project(0f, (gy0 + gy1) * 0.5f, 1f, out var gp, out var gn))
                    CarWheels.Lathe(Mb, gp + gn * 0.012f, gn, new[] { new Vector2(br, -0.002f), new Vector2(br, 0.002f), new Vector2(br * 0.8f, 0.005f), new Vector2(0f, 0.006f) }, 14, Chrome, 50f);
            }
            if (Body.Project(0f, Sill + 0.26f, 1f, out var pc, out var pn))
                PlateOnHolder(pc, pn);
            SeamTop(ScreenZ + 0.03f, ZF - 0.05f, t => W - 0.11f);
            Wipers(ScreenZ - 0.03f, 0.1f, Kind == RearKind.Van ? 0.6f : 0.5f, 0.06f, W - 0.2f);
        }

        /// <summary>Grille / intake: dark back, a frame round it, horizontal slats (plus vertical bars: a mesh).</summary>
        private void GrilleUnit(Vector2[] o, float s, Material frame, bool mesh)
        {
            Body.ProjectedPatch(Mb, o, 1f, s, 0.002f, Lib["Car_Grille"], 4, 0.25f);
            Frame(o, 0.011f, 1f, s, 0.016f, frame, Plastic, 0.25f);
            float x0 = float.MaxValue, x1 = float.MinValue, y0 = float.MaxValue, y1 = float.MinValue;
            foreach (var p in o) { x0 = Mathf.Min(x0, p.x); x1 = Mathf.Max(x1, p.x); y0 = Mathf.Min(y0, p.y); y1 = Mathf.Max(y1, p.y); }
            int rows = Mathf.Clamp(Mathf.RoundToInt((y1 - y0) / 0.024f), 1, 5);
            for (int k = 1; k <= rows; k++)
            {
                float y = Mathf.Lerp(y0, y1, k / (rows + 1f));
                Bar(new Vector2(Mathf.Max(0f, x0 - 0.004f), y), new Vector2(x1 + 0.004f, y), 0.008f, 1f, s, 0.011f, frame);
            }
            if (!mesh) return;
            int cols = Mathf.Max(1, Mathf.RoundToInt((x1 - x0) / 0.045f));
            for (int k = 1; k < cols; k++)
            {
                float x = Mathf.Lerp(x0, x1, k / (float)cols);
                Bar(new Vector2(x, y0 - 0.004f), new Vector2(x, y1 + 0.004f), 0.006f, 1f, s, 0.009f, Plastic, 1);
            }
        }

        /// <summary>Projector bowl: chrome bezel ring standing out of the lamp face, a domed lens in it.</summary>
        private void Bowl(Vector3 p, Vector3 n, float r)
        {
            CarWheels.Lathe(Mb, p, n, new[] { new Vector2(r + 0.008f, -0.006f), new Vector2(r + 0.008f, 0.004f), new Vector2(r + 0.002f, 0.007f),
                new Vector2(r - 0.002f, 0.002f) }, 14, Chrome, 40f);
            CarWheels.Lathe(Mb, p, n, new[] { new Vector2(r - 0.002f, 0.002f), new Vector2(r * 0.55f, 0.007f), new Vector2(0f, 0.0095f) }, 14, Lens, 60f);
        }

        /// <summary>Number plate on a dark holder that stands it off the curved bumper.</summary>
        private void PlateOnHolder(Vector3 p, Vector3 n)
        {
            n.Normalize();
            Vector3 up = Vector3.up - n * Vector3.Dot(Vector3.up, n);
            if (up.sqrMagnitude < 1e-4f) up = Vector3.forward;
            Mb.Box(p + n * 0.006f, new Vector3(0.36f, 0.07f, 0.022f), Plastic, Quaternion.LookRotation(n, up.normalized));
            CarKit.Plate(Mb, p + n * 0.02f, n, PlateText, 0.9f);
        }

        // ------------------------------------------------------------------ sides

        private void Sides()
        {
            float za = FrontAxle - TireR - 0.06f, zb = RearAxle + TireR + 0.06f;
            foreach (float s in new[] { -1f, 1f })
            {
                // sill trim (or the SUV's plastic cladding) standing off the sill, optional rubbing strip
                SideBand(s, zb, za, Sill + 0.008f, Sill + (Cladding ? 0.11f : 0.06f), Cladding ? 0.014f : 0.009f, Plastic);
                if (Variant % 2 == 1 && !Cladding)
                    SideBand(s, zb + 0.15f, za - 0.15f, Sill + 0.26f, Sill + 0.29f, 0.007f, Plastic);
                // wheel arches: a rolled paint lip, or plastic cladding
                foreach (var a in new[] { FrontArch, RearArch })
                    ArchTrim(a, s, ArchTrimReach, Cladding ? 0.016f : 0.007f, Cladding ? Plastic : Paint);
                var hy = Belt - 0.09f;
                var bar = Variant % 2 == 0 ? Paint : Chrome;
                var (hp, hn) = Side(BPillar + 0.14f, hy, s);
                Handle(hp, hn, bar, true);
                var (hp2, hn2) = Side(Kind == RearKind.Van ? BPillar - 0.9f : RearDoorZ + 0.12f, hy, s);
                Handle(hp2, hn2, bar, false);
                var (mp, _) = Side(ScreenZ - 0.1f, Belt + 0.01f, s);
                Mirror(mp, s);
            }
            // fuel flap on the right rear wing (behind the rear door, over the arch): a gap ring and a proud flap
            var (fp, fn) = Side(RearDoorZ - 0.13f, Belt - 0.12f, 1f);
            CarWheels.Lathe(Mb, fp, fn, new[] { new Vector2(0.067f, 0.0008f), new Vector2(0.06f, 0.0008f) }, 16, Seam);
            CarWheels.Lathe(Mb, fp, fn, new[] { new Vector2(0.059f, -0.001f), new Vector2(0.059f, 0.002f), new Vector2(0.056f, 0.0033f),
                new Vector2(0f, 0.0036f) }, 16, Paint, 50f);
        }

        /// <summary>
        /// Door handle: a dark pocket pressed into the door, a rounded grip bridging it on two posts, the key barrel
        /// of the front doors.
        /// </summary>
        private void Handle(Vector3 p, Vector3 n, Material grip, bool lockBarrel)
        {
            const float len = 0.12f;
            Vector3 fwd = Vector3.forward - n * Vector3.Dot(Vector3.forward, n);
            fwd.Normalize();
            var rot = Quaternion.LookRotation(fwd, n);
            CarBlob.Blob(Mb, p - n * 0.003f, rot, new Vector3(0.025f, 0.0065f, len * 0.56f), 0.5f, Seam, 0.6f, 1f, 8, 4);
            CarBlob.Blob(Mb, p + n * 0.014f, rot, new Vector3(0.011f, 0.0065f, len * 0.5f), 0.45f, grip, 0.5f, 1f, 10, 4);
            foreach (float k in new[] { -1f, 1f })
                Mb.Box(p + n * 0.006f + fwd * (k * len * 0.36f), new Vector3(0.014f, 0.014f, 0.016f), grip, rot);
            if (lockBarrel)
                Mb.Cylinder(p - fwd * (len * 0.5f + 0.02f), p - fwd * (len * 0.5f + 0.02f) + n * 0.008f, 0.009f, 8, Chrome);
        }

        /// <summary>
        /// Door mirror: dark sail on the door corner, an arm, a cup-shaped housing (paint; black on vans / cheap trims)
        /// rounded towards the front, the glass set into its open back behind a rim.
        /// </summary>
        private void Mirror(Vector3 root, float s)
        {
            var housing = Kind == RearKind.Van || Variant % 2 == 0 && Kind == RearKind.Hatch ? Plastic : Paint;
            float w = Kind == RearKind.Van ? 0.24f : 0.19f, h = Kind == RearKind.Van ? 0.2f : 0.11f;
            CarBlob.Blob(Mb, root + new Vector3(s * 0.004f, 0.014f, 0.01f), Quaternion.identity, new Vector3(0.014f, 0.03f, 0.065f), 0.45f, Plastic, 0.5f, 1f, 8, 4);
            Vector3 head = root + new Vector3(s * 0.09f, 0.05f, -0.02f);
            CarBlob.Bar(Mb, root + new Vector3(s * 0.01f, 0.028f, 0.005f), head + new Vector3(s * 0.03f, 0.008f, 0f), Vector3.up, 0.05f, 0.022f, housing, 0.5f, 0.75f);
            var rot = Quaternion.Euler(0, s * -6f, 0);
            Vector3 hc = head + new Vector3(s * w * 0.42f, 0.02f, 0f);
            const float D = 0.05f;
            const int cols = 14;
            float[] dz = { -D, -D * 0.3f, D * 0.32f, D * 0.72f, D * 0.97f };
            float[] sc = { 1f, 1f, 0.9f, 0.62f, 0.22f };
            Vector3 L(float u, float v, float z) => hc + rot * new Vector3(u * s, v, z);
            float Sp(float x) => Mathf.Sign(x) * Mathf.Pow(Mathf.Abs(x), 0.55f);
            var P = new Vector3[dz.Length, cols];
            var rim = new Vector3[cols];
            var glass = new Vector3[cols];
            for (int k = 0; k < cols; k++)
            {
                float a = k * Mathf.PI * 2f / cols, u = Sp(Mathf.Cos(a)) * w * 0.5f, v = Sp(Mathf.Sin(a)) * h * 0.5f;
                for (int r = 0; r < dz.Length; r++) P[r, k] = L(u * sc[r], v * sc[r], dz[r]);
                rim[k] = L(u * 0.86f, v * 0.8f, -D);
                glass[k] = L(u * 0.86f, v * 0.8f, -D + 0.012f);
            }
            Grid(P, housing, q => q - hc);
            Vector3 front = rot * Vector3.forward, back = -front;
            Vector3 tip = L(0f, 0f, D * 1.01f), gc = L(0f, 0f, -D + 0.012f);
            for (int k = 0; k < cols; k++)
            {
                int k1 = (k + 1) % cols;
                int last = dz.Length - 1;
                Mb.Tri(P[last, k], P[last, k1], tip, (P[last, k] - hc).normalized + front, (P[last, k1] - hc).normalized + front, front, housing);
                Mb.Quad(P[0, k], P[0, k1], rim[k1], rim[k], back, back, back, back, housing);
                Vector3 i0 = (hc - rim[k]).normalized, i1 = (hc - rim[k1]).normalized;
                Mb.Quad(rim[k], rim[k1], glass[k1], glass[k], i0, i1, i1, i0, Plastic);
                Mb.Tri(glass[k], glass[k1], gc, back, back, back, Lib["Car_MirrorGlass"]);
            }
        }

        /// <summary>Roof rail: a rounded bar following the roof on two moulded feet.</summary>
        private void RoofRail(float s)
        {
            float x = (W - 0.085f - Tumble) * 0.8f;
            float z0 = RoofFrontZ - 0.12f, z1 = RoofRearZ + 0.08f;
            const int n = 7, cs = 8;
            var centres = new Vector3[n + 1];
            var P = new Vector3[n + 1, cs];
            for (int i = 0; i <= n; i++)
            {
                float z = Mathf.Lerp(z0, z1, i / (float)n);
                var (p, nn) = Cabin.OnTopAtX(z, x, CRoofEdge, CCentre, s);
                // the bar rises off the roof between the feet
                float rise = 0.02f + 0.014f * Mathf.Sin(Mathf.PI * Mathf.Clamp01((i - 0.5f) / (n - 1f)));
                centres[i] = p + Vector3.up * rise;
                for (int k = 0; k < cs; k++)
                {
                    float a = k * Mathf.PI * 2f / cs;
                    float cu = Mathf.Sign(Mathf.Cos(a)) * Mathf.Pow(Mathf.Abs(Mathf.Cos(a)), 0.6f), sv = Mathf.Sign(Mathf.Sin(a)) * Mathf.Pow(Mathf.Abs(Mathf.Sin(a)), 0.6f);
                    P[i, k] = centres[i] + new Vector3(cu * 0.016f, sv * 0.011f, 0f);
                }
            }
            var rail = Lib["Car_RimGraphite"];
            Grid(P, rail, q =>
            {
                Vector3 best = centres[0];
                foreach (var cc in centres) if ((cc - q).sqrMagnitude < (best - q).sqrMagnitude) best = cc;
                Vector3 d = q - best;
                d.z = 0f;
                return d;
            });
            foreach (int i in new[] { 0, n })
            {
                Vector3 e = i == 0 ? Vector3.forward : Vector3.back;
                for (int k = 0; k < cs; k++)
                    Mb.Tri(P[i, k], P[i, (k + 1) % cs], centres[i] + e * 0.004f, e, e, e, rail);
                var (fp, _) = Cabin.OnTopAtX(i == 0 ? z0 : z1, x, CRoofEdge, CCentre, s);
                CarBlob.Blob(Mb, fp + Vector3.up * 0.014f + e * 0.01f, Quaternion.identity, new Vector3(0.022f, 0.022f, 0.05f), 0.5f, Plastic, 0.6f, 1f, 8, 4);
            }
        }

        // ------------------------------------------------------------------ rear

        private void Rear()
        {
            var tail = Below(TailLamp, (Kind == RearKind.Notch ? DeckH : Belt) - 0.02f, 0.11f);
            Vector2 tc = Centroid(tail);
            float span = Span(tail);
            foreach (float s in new[] { -1f, 1f })
            {
                // tail lamp unit standing proud in its gap, reverse lamp, indicator
                Body.ProjectedPatch(Mb, Offset(tail, 0.006f), -1f, s, 0.0012f, Seam, 4, 0.25f);
                Relief(tail, -1f, s, 0.009f, Tail, Plastic, 0.25f);
                Relief(CarLoft.RectAt(tc.x - span * 0.25f, tc.y, span * 0.22f, 0.04f, 0.01f), -1f, s, 0.0115f,
                    s > 0 ? Lib["Car_Reverse"] : Lib["Car_TailDark"], Plastic, 0.25f, 0.003f);
                Relief(CarLoft.RectAt(tc.x + span * 0.1f, tc.y, span * 0.2f, 0.035f, 0.01f), -1f, s, 0.0115f, Amber, Plastic, 0.25f, 0.003f);
                if (Body.Project(s * (tc.x + span * 0.1f), tc.y, -1f, out var bp, out var bn))
                    Pm.Blinkers.Add(new Blinker(bp + bn * 0.013f, bn, new Vector2(span * 0.2f, 0.04f)));
                // bumper: dark lower diffuser standing off it, reflectors
                Relief(CarLoft.RectAt(W * 0.36f, Sill + 0.2f, W * 0.72f, 0.07f, 0.02f), -1f, s, 0.01f, Plastic, Plastic, 0.2f);
                Relief(CarLoft.RectAt(W * 0.55f, Sill + 0.27f, 0.1f, 0.02f, 0.006f), -1f, s, 0.005f, Lib["Car_TailDark"], Plastic, 0.2f);
                // tailgate / boot lid gap above the bumper, the bumper's top edge standing proud
                Body.ProjectedPatch(Mb, CarLoft.RectAt(W * 0.36f, Bump - 0.02f, W * 0.72f, 0.005f, 0.001f), -1f, s, 0.0015f, Seam, 2, 0.2f);
                Relief(CarLoft.RectAt(W * 0.36f, Bump - 0.033f, W * 0.72f, 0.016f, 0.004f), -1f, s, 0.004f, Paint, Paint, 0.2f);
            }
            if (Kind == RearKind.Van)
            {
                // split rear doors: the gap runs from the bumper up to the roof
                Body.ProjectedPatch(Mb, CarLoft.RectAt(0.0025f, (Bump + Belt) * 0.5f, 0.005f, Belt - Bump - 0.04f, 0.001f), -1f, 1f, 0.0015f, Seam, 2, 0.2f);
                Mb.Box(new Vector3(0f, (Belt + RoofH) * 0.5f, GlassEndZ - 0.004f), new Vector3(0.006f, RoofH - Belt - 0.06f, 0.004f), Seam);
            }
            if (Kind == RearKind.Notch)
                SeamTop(ZR + 0.06f, GlassEndZ - 0.04f, t => W - 0.13f);
            // number plate in a raised paint surround
            Frame(CarLoft.RectAt(0f, RearPlateY, 0.56f, 0.15f, 0.02f), 0.016f, -1f, 1f, 0.007f, Paint, Paint, 0.2f);
            if (Body.Project(0f, RearPlateY, -1f, out var pc, out var pn))
                PlateOnHolder(pc, pn);
            CarKit.Exhaust(Mb, new Vector3(-W * 0.55f, Sill + 0.08f, ZR + 0.12f), 0.03f, 0.18f);
        }

        protected static Vector2 Centroid(Vector2[] o)
        {
            Vector2 c = Vector2.zero;
            foreach (var p in o) c += p;
            return c / o.Length;
        }

        protected static float Span(Vector2[] o)
        {
            float a = float.MaxValue, b = float.MinValue;
            foreach (var p in o) { a = Mathf.Min(a, p.x); b = Mathf.Max(b, p.x); }
            return b - a;
        }

        protected static float Height(Vector2[] o)
        {
            float a = float.MaxValue, b = float.MinValue;
            foreach (var p in o) { a = Mathf.Min(a, p.y); b = Mathf.Max(b, p.y); }
            return b - a;
        }
    }
}
