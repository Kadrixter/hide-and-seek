using UnityEngine;
using static HideAndSeek.Map.Editor.CarShapes;

namespace HideAndSeek.Map.Editor
{
    /// <summary>Lamps, grille, plates, mirrors, handles, seams, bumpers and a simple cabin of the parked cars.</summary>
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
                {
                    float x = (W - 0.085f - Tumble) * 0.8f * s;
                    float z0 = RoofFrontZ - 0.12f, z1 = RoofRearZ + 0.08f;
                    Mb.Box(new Vector3(x, RoofH + 0.045f, (z0 + z1) * 0.5f), new Vector3(0.035f, 0.025f, z0 - z1), Lib["Car_RimGraphite"]);
                    foreach (float z in new[] { z0 - 0.04f, z1 + 0.04f })
                        Mb.Box(new Vector3(x, RoofH + 0.025f, z), new Vector3(0.04f, 0.05f, 0.08f), Plastic);
                }
        }

        /// <summary>Outline kept below a height (projections must stay on the lamp face, not run onto the bonnet).</summary>
        protected Vector2[] Below(Vector2[] o, float yMax, float inset = 0.07f) =>
            System.Array.ConvertAll(o, p => new Vector2(Mathf.Min(p.x, W - inset), Mathf.Min(p.y, yMax)));

        private void Front()
        {
            var head = Below(HeadLamp, Nose - 0.035f);
            var grille = Grille != null ? Below(Grille, Nose - 0.045f) : null;
            foreach (float s in new[] { -1f, 1f })
            {
                // dark housing with a bright reflector inset: reads on light and dark paint alike
                Body.ProjectedLamp(Mb, head, 1f, s, Plastic, Seam, 0.007f, 0.003f);
                Body.ProjectedPatch(Mb, CarLoft.Grow(head, -0.014f), 1f, s, 0.0052f, Lib["Car_Reflector"]);
                // projector bulb and indicator inside the lamp
                Vector2 c = Centroid(head);
                float span = Span(head);
                var bulb = CarKit.Circle(Mathf.Min(0.04f, span * 0.15f), 12);
                for (int i = 0; i < bulb.Length; i++) bulb[i] += new Vector2(c.x - span * 0.12f, c.y);
                Body.ProjectedPatch(Mb, bulb, 1f, s, 0.0068f, Lens);
                var amber = CarLoft.RectAt(c.x + span * 0.34f, c.y, span * 0.16f, 0.035f, 0.01f);
                Body.ProjectedPatch(Mb, amber, 1f, s, 0.0068f, Amber);
                if (Body.Project(s * (c.x + span * 0.34f), c.y, 1f, out var ap, out var an))
                    Pm.Blinkers.Add(new Blinker(ap + an * 0.006f, an, new Vector2(span * 0.16f, 0.04f)));
                // bumper: lower intake, dark lip
                Body.ProjectedLamp(Mb, Intake, 1f, s, Lib["Car_Grille"], Seam, 0.006f, 0.002f);
                if (grille != null)
                {
                    Body.ProjectedLamp(Mb, grille, 1f, s, Lib["Car_Grille"], Seam, 0.006f, 0.002f);
                    if (ChromeGrille)
                    {
                        float top = float.MinValue, gx = 0f;
                        foreach (var p in grille) { top = Mathf.Max(top, p.y); gx = Mathf.Max(gx, p.x); }
                        Body.ProjectedPatch(Mb, CarLoft.RectAt(gx * 0.5f, top - 0.012f, gx, 0.012f, 0.004f), 1f, s, 0.0045f, Chrome, 2);
                    }
                }
                // side repeater on the wing
                var (rp, rn) = Side(FrontAxle + TireR + 0.18f, Mathf.Min(Belt - 0.12f, Sill + 0.5f), s);
                CarKit.Lamp(Mb, rp, rn, Vector3.forward, CarKit.Rect(0.05f, 0.022f, 0.008f), Amber, Plastic, 0.004f, 0.015f);
                Pm.Blinkers.Add(new Blinker(rp + rn * 0.004f, rn, new Vector2(0.05f, 0.022f)));
                // bonnet shut lines
                Body.ProjectedPatch(Mb, CarLoft.RectAt(W * 0.45f, Nose - 0.035f, W * 0.9f, 0.005f, 0.001f), 1f, s, 0.0015f, Seam, 2);
            }
            if (Body.Project(0f, Sill + 0.26f, 1f, out var pc, out var pn))
                CarKit.Plate(Mb, pc + pn * 0.012f, pn, PlateText, 0.9f);
            SeamAcross(ScreenZ + 0.03f, W - 0.11f);
            SeamTop(ScreenZ + 0.03f, ZF - 0.05f, t => W - 0.11f);
            Wipers(ScreenZ - 0.03f, 0.1f, Kind == RearKind.Van ? 0.6f : 0.5f, 0.06f, W - 0.2f);
        }

        private void Sides()
        {
            float y0 = Sill + 0.03f, y1 = Belt - 0.015f;
            float frontDoor = Mathf.Min(ScreenZ - 0.06f, FrontAxle - TireR - 0.08f);
            float rearDoorEnd = Mathf.Max(CPillar - 0.02f, RearAxle + TireR + 0.06f);
            SeamSide(frontDoor, y0, y1);
            SeamSide(BPillar, y0, y1 + 0.01f);
            if (Kind == RearKind.Van) SeamSide(BPillar - 1.0f, y0, y1);    // sliding side door
            else SeamSide(rearDoorEnd, y0, y1);
            foreach (float s in new[] { -1f, 1f })
            {
                // dark sill strip between the arches, optional rubbing strip / plastic arch cladding
                float za = FrontAxle - TireR - 0.06f, zb = RearAxle + TireR + 0.06f;
                Body.PatchY(Mb, s, zb, za, t => new Vector2(Sill + 0.005f, Sill + (Cladding ? 0.11f : 0.06f)), BLip, BShoulder, 0.003f, Plastic, 8, 2);
                if (Variant % 2 == 1 && !Cladding)
                    Body.PatchY(Mb, s, zb + 0.15f, za - 0.15f, t => new Vector2(Sill + 0.26f, Sill + 0.29f), BLip, BShoulder, 0.004f, Plastic, 8, 2);
                var hy = Belt - 0.09f;
                var (hp, hn) = Side(BPillar + 0.14f, hy, s);
                CarKit.Handle(Mb, hp, hn, Variant % 2 == 0 ? Paint : Lib["Car_ChromeBright"], 0.12f);
                var (hp2, hn2) = Side(Kind == RearKind.Van ? BPillar - 0.9f : rearDoorEnd + 0.12f, hy, s);
                CarKit.Handle(Mb, hp2, hn2, Variant % 2 == 0 ? Paint : Lib["Car_ChromeBright"], 0.12f);
                var (mp, _) = Side(ScreenZ - 0.1f, Belt + 0.01f, s);
                Mirror(mp, s);
            }
            // fuel flap on the right rear wing
            var (fp, fn) = Side(RearAxle + TireR + 0.22f, Belt - 0.12f, 1f);
            CarWheels.Lathe(Mb, fp, fn, new[] { new Vector2(0.065f, 0.0005f), new Vector2(0.06f, 0.0012f), new Vector2(0f, 0.0012f) }, 14, Seam);
        }

        /// <summary>Door mirror: stalk, rounded housing in paint (black on van / cheap trims), glass at the back.</summary>
        private void Mirror(Vector3 root, float s)
        {
            var housing = Kind == RearKind.Van || Variant % 2 == 0 && Kind == RearKind.Hatch ? Plastic : Paint;
            float w = Kind == RearKind.Van ? 0.24f : 0.19f, h = Kind == RearKind.Van ? 0.2f : 0.11f;
            Vector3 head = root + new Vector3(s * 0.09f, 0.05f, -0.02f);
            Mb.Box(root + new Vector3(s * 0.04f, 0.02f, 0.01f), new Vector3(0.08f, 0.04f, 0.12f), Plastic);
            Mb.Box((root + head) * 0.5f + new Vector3(0, 0.02f, 0), new Vector3(0.09f, 0.03f, 0.04f), housing, Quaternion.Euler(0, 0, s * 25f));
            var rot = Quaternion.Euler(0, s * -6f, 0);
            Vector3 hc = head + new Vector3(s * w * 0.42f, 0.02f, 0f);
            CarBlob.Blob(Mb, hc, rot, new Vector3(w * 0.5f, h * 0.5f, 0.05f), 0.42f, housing, 0.55f, 1f, 10, 6);
            Vector3 back = rot * Vector3.back;
            CarKit.Lamp(Mb, hc + back * 0.028f, back, rot * Vector3.right * s, CarKit.Rect(w - 0.03f, h - 0.026f, 0.025f, 2), Lib["Car_MirrorGlass"], Plastic, 0.003f, 0.008f);
        }

        private void Rear()
        {
            var tail = Below(TailLamp, (Kind == RearKind.Notch ? DeckH : Belt) - 0.02f, 0.11f);
            Vector2 tc = Centroid(tail);
            float span = Span(tail);
            foreach (float s in new[] { -1f, 1f })
            {
                Body.ProjectedLamp(Mb, tail, -1f, s, Tail, Seam, 0.007f, 0.004f, 0.25f);
                Body.ProjectedPatch(Mb, CarLoft.RectAt(tc.x - span * 0.25f, tc.y, span * 0.22f, 0.04f, 0.01f), -1f, s, 0.005f, s > 0 ? Lib["Car_Reverse"] : Lib["Car_TailDark"], 2, 0.25f);
                Body.ProjectedPatch(Mb, CarLoft.RectAt(tc.x + span * 0.1f, tc.y, span * 0.2f, 0.035f, 0.01f), -1f, s, 0.005f, Amber, 2, 0.25f);
                if (Body.Project(s * (tc.x + span * 0.1f), tc.y, -1f, out var bp, out var bn))
                    Pm.Blinkers.Add(new Blinker(bp + bn * 0.006f, bn, new Vector2(span * 0.2f, 0.04f)));
                // bumper: dark lower diffuser strip with reflectors
                Body.ProjectedPatch(Mb, CarLoft.RectAt(W * 0.36f, Sill + 0.2f, W * 0.72f, 0.07f, 0.02f), -1f, s, 0.003f, Plastic, 3, 0.2f);
                Body.ProjectedPatch(Mb, CarLoft.RectAt(W * 0.55f, Sill + 0.27f, 0.1f, 0.02f, 0.006f), -1f, s, 0.0045f, Lib["Car_TailDark"], 2, 0.2f);
                // tailgate / boot lid gap above the bumper
                Body.ProjectedPatch(Mb, CarLoft.RectAt(W * 0.36f, Bump - 0.02f, W * 0.72f, 0.005f, 0.001f), -1f, s, 0.0015f, Seam, 2, 0.2f);
            }
            if (Kind == RearKind.Van)
            {
                // split rear doors: the gap runs from the bumper up to the roof
                Body.ProjectedPatch(Mb, CarLoft.RectAt(0.0025f, (Bump + Belt) * 0.5f, 0.005f, Belt - Bump - 0.04f, 0.001f), -1f, 1f, 0.0015f, Seam, 2, 0.2f);
                Mb.Box(new Vector3(0f, (Belt + RoofH) * 0.5f, GlassEndZ - 0.004f), new Vector3(0.006f, RoofH - Belt - 0.06f, 0.004f), Seam);
            }
            if (Kind == RearKind.Notch)
            {
                SeamAcross(GlassEndZ - 0.04f, W - 0.13f);
                SeamTop(ZR + 0.06f, GlassEndZ - 0.04f, t => W - 0.13f);
            }
            if (Body.Project(0f, RearPlateY, -1f, out var pc, out var pn))
                CarKit.Plate(Mb, pc + pn * 0.012f, pn, PlateText, 0.9f);
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
    }
}
