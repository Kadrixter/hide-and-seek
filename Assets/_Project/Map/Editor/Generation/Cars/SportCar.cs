using System.Linq;
using UnityEngine;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// Base of the detailed (hero) cars. A model describes its body and greenhouse with keys, picks wheels and
    /// materials and adds its own details; this class lofts the panels, cuts the wheel arches, fits the wheels,
    /// glass and cabin tub and fills in colliders and hazard-light spots. Local space: nose at +z, floor at y = 0.
    /// Material slot 0 is the paint (swapped per placed instance).
    /// </summary>
    internal abstract class SportCar
    {
        protected MeshBuilder Mb;
        protected PropMesh Pm;
        protected MaterialLibrary Lib;
        protected Material Paint, Glass, Trim, Seam, Well, Chrome, Grille, Lens, Tail, Amber, Plastic;
        protected CarLoft Body, Cabin;
        protected BodyKey[] BKeys;
        protected CabinKey[] CKeys;
        private float[] _tubZ, _tubV;

        // ------------------------------------------------------------------ spec

        public abstract string Name { get; }
        public abstract string DefaultPaint { get; }
        public abstract float Length { get; }
        protected abstract BodyKey[] BodyKeys();
        /// <summary>Greenhouse keys (windshield base -> rear glass base); null for none.</summary>
        protected abstract CabinKey[] CabinKeys();
        protected abstract Arch FrontArch { get; }
        protected abstract Arch RearArch { get; }
        protected abstract float TireRadius { get; }
        protected virtual float TireWidth => 0.205f;
        protected virtual float RimRadius => TireRadius * 0.62f;
        protected virtual float TireInset => 0.025f;
        protected virtual RimStyle Rim => RimStyle.FiveSpoke;
        protected virtual Material RimMat => Lib["Car_RimSilver"];
        protected virtual Material Caliper => null;
        protected virtual float WellInset => TireWidth + 0.1f;
        protected abstract float TubWall { get; }
        protected abstract float TubFloor { get; }
        protected virtual float[] BodyBreaks => new float[0];
        protected virtual float[] CabinBreaks => new float[0];
        protected virtual float RoofRound => 1f;
        protected virtual float RoofDip => 0f;
        /// <summary>Extra crease lines of the body (ring indices, e.g. a crisp shoulder).</summary>
        protected virtual int[] SharpRings => new int[0];
        protected virtual Material FrontCap => Paint;
        protected virtual Material RearCap => Paint;

        /// <summary>Reference outlines from real dimensions, drawn as guides in the studio only (not in the mesh):
        /// side (z, y), plan (z, half width), front (half width, y). Null = no guide.</summary>
        public virtual Vector2[] RefSide => null;
        public virtual Vector2[] RefTop => null;
        public virtual Vector2[] RefFront => null;
        public Arch RefFrontArch => FrontArch;
        public Arch RefRearArch => RearArch;
        public float RefTireRadius => TireRadius;

        /// <summary>Greenhouse cell material by ring segment (see CarShapes.C*) and z.</summary>
        protected abstract Material CabinMat(int seg, float z);
        protected abstract void Interior();
        protected abstract void Details();

        // ------------------------------------------------------------------ build

        public void Build(MeshBuilder mb, PropMesh pm)
        {
            Mb = mb; Pm = pm; Lib = mb.Lib;
            Paint = Lib[MaterialLibrary.CarPaints[0]];
            mb.Reserve(Paint);
            Glass = Lib["Car_Glass"]; Trim = Lib["Car_Trim"]; Seam = Lib["Car_Seam"]; Well = Lib["Car_Well"];
            Chrome = Lib["Car_ChromeBright"]; Grille = Lib["Car_Grille"]; Lens = Lib["Car_Lens"]; Tail = Lib["Car_TailRed"];
            Amber = Lib["Car_LensAmber"]; Plastic = Lib["Plastic_Black"];

            BKeys = BodyKeys();
            _tubZ = BKeys.OrderBy(k => k.Z).Select(k => k.Z).ToArray();
            _tubV = BKeys.OrderBy(k => k.Z).Select(k => k.Tub).ToArray();
            var arches = new[] { FrontArch, RearArch };
            Body = CarShapes.Body(BKeys, arches, WellInset, TubWall, TubFloor, BodyBreaks);
            foreach (int r in SharpRings) Body.SharpRing.Add(r);
            Body.Emit(mb, BodyMat);
            if (FrontCap != null) Body.Cap(mb, Body.Stations - 1, 1f, FrontCap);
            if (RearCap != null) Body.Cap(mb, 0, -1f, RearCap);

            foreach (var a in arches)
            {
                var sec = Body.SectionAt(a.Z);
                float yBot = sec[CarShapes.BUnder].y, xIn = sec[CarShapes.BWellIn].x, xOut = sec[CarShapes.BLip].x;
                float wx = sec[CarShapes.BRocker].x - TireInset - TireWidth * 0.5f;
                foreach (float s in new[] { -1f, 1f })
                {
                    CarWheels.Well(mb, a, yBot, xIn, xOut, s);
                    CarWheels.Wheel(mb, new Vector3(s * wx, TireRadius, a.Z), s, TireRadius, TireWidth, RimRadius, Rim, RimMat, Caliper);
                }
            }

            CKeys = CabinKeys();
            if (CKeys != null)
            {
                Cabin = CarShapes.Cabin(CKeys, CabinBreaks, RoofRound, 0.03f, RoofDip);
                Cabin.Emit(mb, (i, j) => CabinMat(i, Mid(Cabin, j)));
                Cabin.Emit(mb, (i, j) => CabinInner(i, Mid(Cabin, j)), inner: true);
            }
            Interior();
            Details();
            Colliders();
        }

        protected static float Mid(CarLoft l, int j) => (l.Z[j] + l.Z[j + 1]) * 0.5f;

        protected float TubAt(float z) => SmoothCurve.Eval(_tubZ, _tubV, z);

        /// <summary>Default panel materials: dark underside and wells, paint outside, trimmed tub inside the cabin.</summary>
        protected virtual Material BodyMat(int seg, int j)
        {
            if (seg <= CarShapes.BWellTop) return Well;
            if (seg >= CarShapes.BRound && TubAt(Mid(Body, j)) > 0.5f)
                return seg == CarShapes.BRound ? Paint : seg == CarShapes.BDeck1 ? Lib["Car_Interior"] : Lib["Car_Dash"];
            return Paint;
        }

        private Material CabinInner(int seg, float z)
        {
            var m = CabinMat(seg, z);
            if (m == null) return null;
            return m == Glass || m == Lib["Car_GlassDark"] ? m : Lib["Car_Headliner"];
        }

        protected virtual void Colliders()
        {
            var b = Mb.IsEmpty ? new Bounds() : BoundsOf(Body);
            float bottom = 0.15f;
            Pm.Colliders.Add(new Bounds(new Vector3(0, (bottom + b.max.y) * 0.5f, b.center.z), new Vector3(b.size.x, b.max.y - bottom, b.size.z)));
            if (Cabin != null)
            {
                var c = BoundsOf(Cabin);
                float y0 = b.max.y - 0.02f;
                if (c.max.y > y0 + 0.05f)
                    Pm.Colliders.Add(new Bounds(new Vector3(0, (y0 + c.max.y) * 0.5f, c.center.z), new Vector3(c.size.x - 0.06f, c.max.y - y0, c.size.z - 0.1f)));
            }
        }

        /// <summary>Bounds of a loft below its shoulder (body) or the whole greenhouse.</summary>
        protected static Bounds BoundsOf(CarLoft l)
        {
            var b = new Bounds(l.P(0, 0), Vector3.zero);
            for (int j = 0; j < l.Stations; j++)
            for (int i = 0; i < l.Ring; i++)
            {
                var p = l.P(j, i);
                b.Encapsulate(p);
                b.Encapsulate(new Vector3(-p.x, p.y, p.z));
            }
            return b;
        }

        // ------------------------------------------------------------------ helpers for models

        /// <summary>Adds a hazard-light spot on the right side and its mirror on the left.</summary>
        protected void BlinkerPair(Vector3 p, Vector3 n, Vector2 size)
        {
            Pm.Blinkers.Add(new Blinker(p, n, size));
            Pm.Blinkers.Add(new Blinker(new Vector3(-p.x, p.y, p.z), new Vector3(-n.x, n.y, n.z), size));
        }

        /// <summary>Vertical panel gap on both sides at z, from y0 to y1 (on the side ring part).</summary>
        protected void SeamSide(float z, float y0, float y1, float width = 0.005f)
        {
            foreach (float s in new[] { -1f, 1f })
                Body.PatchY(Mb, s, z - width * 0.5f, z + width * 0.5f, t => new Vector2(y0, y1), CarShapes.BLip, CarShapes.BShoulder + 1, 0.0012f, Seam, 1, 8);
        }

        /// <summary>Gap along the top of the body at |x| = x from z0 to z1 (hood / boot lid edges), both sides.</summary>
        protected void SeamTop(float z0, float z1, System.Func<float, float> xAt, float width = 0.005f)
        {
            foreach (float s in new[] { -1f, 1f })
                Body.PatchX(Mb, s, z0, z1, t => new Vector2(xAt(t) - width * 0.5f, xAt(t) + width * 0.5f), CarShapes.BShoulder, CarShapes.BCentre, 0.0012f, Seam, 10, 1);
        }

        /// <summary>Gap across the top at z from |x| = 0 to x1 (both halves).</summary>
        protected void SeamAcross(float z, float x1, float width = 0.005f)
        {
            foreach (float s in new[] { -1f, 1f })
                Body.PatchX(Mb, s, z - width * 0.5f, z + width * 0.5f, t => new Vector2(0f, x1), CarShapes.BShoulder, CarShapes.BCentre, 0.0012f, Seam, 1, 8);
        }

        /// <summary>Glass band (seal top .. glass top heights) of the greenhouse at z.</summary>
        protected Vector2 GlassY(float z)
        {
            var sec = Cabin.SectionAt(z);
            return new Vector2(sec[CarShapes.CSeal].y, sec[CarShapes.CGlassTop].y);
        }

        /// <summary>
        /// Panel laid over the side glass (pillars, door sails, kinks): rows from z0 to z1, each covering the glass
        /// height fractions range(t) (0 = bottom of the glass, 1 = top). Both sides.
        /// </summary>
        protected void GlassOverlay(float z0, float z1, System.Func<float, Vector2> range, Material m, int nz = 12)
        {
            foreach (float s in new[] { -1f, 1f })
                Cabin.PatchY(Mb, s, z0, z1, t =>
                {
                    var g = GlassY(Mathf.Lerp(z0, z1, t));
                    var r = range(t);
                    return new Vector2(Mathf.Lerp(g.x, g.y, r.x), Mathf.Lerp(g.x, g.y, r.y));
                }, CarShapes.CSeal, CarShapes.CGlassTop, 0.0025f, m, nz, 4);
        }

        /// <summary>Wipers parked along the screen base, pivots at x (signed), arms pointing to -x (left-hand drive).</summary>
        protected void Wipers(float zBase, float rise, float len, params float[] pivots)
        {
            foreach (float x in pivots)
            {
                var (p, n) = Cabin.OnTopAtX(zBase, Mathf.Abs(x), CarShapes.CRoofEdge, CarShapes.CCentre, x < 0 ? -1f : 1f);
                float tx = x - len * 0.97f;
                var (q, _) = Cabin.OnTopAtX(zBase - rise, Mathf.Max(0.001f, Mathf.Abs(tx)), CarShapes.CRoofEdge, CarShapes.CCentre, tx < 0 ? -1f : 1f);
                CarKit.Wiper(Mb, p + n * 0.008f, q - p, n, len);
            }
        }

        protected (Vector3 p, Vector3 n) Side(float z, float y, float side = 1f) => Body.OnSideAtY(z, y, CarShapes.BLip, CarShapes.BShoulder, side);
        protected (Vector3 p, Vector3 n) Top(float z, float x, float side = 1f) => Body.OnTopAtX(z, x, CarShapes.BShoulder, CarShapes.BCentre, side);
    }
}
