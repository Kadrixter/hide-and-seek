using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static HideAndSeek.Map.Editor.CarShapes;

namespace HideAndSeek.Map.Editor
{
    /// <summary>Shape of the tail: notchback (sedan boot), hatchback, estate / SUV (long roof), box van.</summary>
    internal enum RearKind { Notch, Hatch, Estate, Van }

    /// <summary>
    /// Ordinary parked car (the ~180 random cars of the garage). A model only fills in a handful of dimensions
    /// (length, width, wheelbase, heights of nose / bonnet / belt / roof, where the glass starts and ends) and its
    /// lamp outlines; this class turns them into body and greenhouse keys for the hero-car loft (coarser stations,
    /// light wheels, simple cabin) so every parked car has a smooth body, real glass, lamps, mirrors and handles.
    /// Local space: nose at +z, floor at y = 0, paint in material slot 0.
    /// </summary>
    internal abstract partial class ParkedCar : SportCar
    {
        protected readonly int Variant;
        protected ParkedCar(int variant) { Variant = variant; }

        // ------------------------------------------------------------------ spec (set by the model)

        protected abstract string Model { get; }
        protected RearKind Kind = RearKind.Hatch;
        protected float L = 4.3f, W = 0.87f, Wb = 2.62f, FrontOver = 0.86f;
        protected float FaceDepth = 0.17f;
        protected float Sill = 0.2f, Nose = 0.72f, Cowl = 0.95f, Belt = 1.0f, RoofH = 1.46f, DeckH = 1.0f;
        /// <summary>Windscreen base, roof front / rear edges, rear glass base (z).</summary>
        protected float ScreenZ = 0.9f, RoofFrontZ = 0.05f, RoofRearZ = -1.6f, GlassEndZ = -1.95f;
        /// <summary>B pillar, end of the rear door glass (C pillar), end of the last side glass (estate D pillar).</summary>
        protected float BPillar = -0.45f, CPillar = -1.3f, DPillar = -1.3f;
        protected float Tumble = 0.18f, TireR = 0.31f, TireW = 0.205f, RimR = 0.2f;
        protected ParkedRim RimKind = ParkedRim.Alloy5;
        protected string RimMaterial = "Car_RimSilver";
        protected bool Cargo, RoofRails, Cladding;
        protected string PlateText = "WA 12345";
        protected string Paint0 = "CarPaint_Silver";
        public bool IsVan => Kind == RearKind.Van;
        public float Weight = 1f;

        protected float ZF => L * 0.5f;
        protected float ZR => -L * 0.5f;
        protected float FrontAxle => ZF - FrontOver;
        protected float RearAxle => FrontAxle - Wb;
        protected float Bump => Sill + 0.38f;

        /// <summary>
        /// Merges the many hero materials into ~13 sub-meshes (dark plastics, wells and seams share one, lenses one,
        /// rear lamps one, interior one): about the draw calls of the old box cars per parked car.
        /// </summary>
        public static Dictionary<Material, Material> MaterialAliases(MaterialLibrary lib, ParkedCar car)
        {
            var a = new Dictionary<Material, Material>();
            void Map(string from, string to) { a[lib[from]] = lib[to]; }
            foreach (var n in new[] { "Car_Well", "Car_Seam", "Plastic_Black", "Car_Grille", "Car_Dash", "Metal_Dark", "Car_PlateBlue", "Car_LeatherBlack", "Car_Gauge" })
                Map(n, "Car_Trim");
            Map("Car_Reflector", "Car_Lens");
            Map("Car_Reverse", "Car_Lens");
            Map("Car_TailDark", "Car_TailRed");
            Map("Car_MirrorGlass", "Car_ChromeBright");
            Map("Car_PlasticGrey", "Car_Trim");
            Map("Car_Needle", "Car_LensAmber");
            string cloth = car.Variant % 2 == 0 ? "Car_Interior" : "Car_InteriorGrey";
            foreach (var n in new[] { "Car_Interior", "Car_InteriorGrey", "Car_Headliner" })
                if (n != cloth) Map(n, cloth);
            if (car.RimMaterial != "Car_RimSilver") Map("Car_RimSilver", car.RimMaterial);
            return a;
        }

        // ------------------------------------------------------------------ SportCar plumbing

        public override string Name => Model + "_" + Variant;
        public override string DefaultPaint => Paint0;
        public override float Length => L;
        protected override Arch FrontArch => new Arch(FrontAxle, TireR, TireR + 0.04f);
        protected override Arch RearArch => new Arch(RearAxle, TireR, TireR + 0.04f);
        protected override float TireRadius => TireR;
        protected override float TireWidth => TireW;
        protected override float RimRadius => RimR;
        protected override float TubWall => W - 0.15f;
        protected override float TubFloor => Sill + 0.08f;
        protected override float BodyStep => 0.075f;
        protected override float CabinStep => 0.07f;
        protected override float RoofRound => 1f;
        protected override float SideFullness => 0.35f;
        protected override float[] CabinBreaks => new[]
        {
            ScreenZ - 0.04f, RoofFrontZ, BPillar - 0.045f, BPillar + 0.045f, CPillar, CPillar - 0.06f, DPillar, RoofRearZ, GlassEndZ + 0.03f
        };
        protected override int[] SharpRings => new[] { BDeck1 };

        protected override void BuildWheel(Vector3 c, float side) =>
            ParkedWheel.Build(Mb, c, side, TireR, TireW, RimR, RimKind, Lib[RimMaterial]);

        private static BodyKey K(float z, float yBot, float wBot, float wMax, float yMax, float wSh, float ySh, float yTop,
            float tub = 0f, float round = 0.55f) => new BodyKey(z, yBot, wBot, wMax, yMax, wSh, ySh, yTop, round, tub);

        protected override BodyKey[] BodyKeys()
        {
            float s = Sill, w = W, ym = Sill + 0.37f, bt = Sill + 0.29f;
            var k = new List<BodyKey>
            {
                // nose: low bumper face, a short lamp face up to a crisp bonnet edge (lamps sit right under it), then
                // a nearly flat bonnet to the windscreen; FaceDepth sets how far the lamp face leans back
                K(ZF, s + 0.07f, w - 0.24f, w - 0.225f, s + 0.2f, w - 0.25f, bt - 0.015f, bt, 0f, 1f),
                K(ZF - 0.012f, s + 0.03f, w - 0.16f, w - 0.145f, s + 0.22f, w - 0.17f, bt + 0.02f, bt + 0.035f, 0f, 0.7f),
                K(ZF - 0.03f, s + 0.015f, w - 0.11f, w - 0.095f, s + 0.25f, w - 0.12f, Mathf.Lerp(bt, Nose, 0.35f) - 0.015f, Mathf.Lerp(bt, Nose, 0.35f)),
                K(ZF - Mathf.Max(0.05f, FaceDepth * 0.55f), s + 0.01f, w - 0.075f, w - 0.06f, s + 0.28f, w - 0.085f, Mathf.Lerp(bt, Nose, 0.75f) - 0.015f, Mathf.Lerp(bt, Nose, 0.75f)),
                K(ZF - FaceDepth, s + 0.005f, w - 0.05f, w - 0.035f, s + 0.3f, w - 0.065f, Nose - 0.012f, Nose, 0f, 0.3f),
                K(ZF - FaceDepth - 0.07f, s, w - 0.045f, w - 0.03f, s + 0.31f, w - 0.06f, Nose + 0.004f, Nose + 0.018f, 0f, 0.45f),
                K(Mathf.Lerp(ZF - FaceDepth, ScreenZ, 0.5f), s, w - 0.02f, w - 0.005f, ym - 0.02f, w - 0.035f, Mathf.Lerp(Nose, Cowl, 0.56f) - 0.02f, Mathf.Lerp(Nose, Cowl, 0.56f)),
                K(ScreenZ, s, w - 0.015f, w, ym, w - 0.03f, Cowl - 0.02f, Cowl),
                K(ScreenZ - 0.13f, s, w - 0.015f, w, ym, w - 0.035f, Cowl - 0.005f, Cowl + 0.01f, 1f),
            };
            float bodyH = Kind == RearKind.Notch ? DeckH : Belt;
            if (Kind == RearKind.Notch)
            {
                k.Add(K(GlassEndZ + 0.14f, s, w - 0.015f, w, ym, w - 0.035f, Belt - 0.01f, Belt, 1f));
                k.Add(K(GlassEndZ, s, w - 0.015f, w, ym, w - 0.035f, DeckH - 0.02f, DeckH));
                k.Add(K(ZR + 0.2f, s + 0.02f, w - 0.025f, w - 0.01f, ym, w - 0.05f, DeckH - 0.012f, DeckH + 0.005f));
                k.Add(K(ZR + 0.09f, s + 0.05f, w - 0.05f, w - 0.035f, ym, w - 0.08f, DeckH - 0.03f, DeckH - 0.015f, 0f, 0.7f));
            }
            else
            {
                k.Add(K(RearAxle + 0.1f, s, w - 0.015f, w, ym, w - 0.035f, Belt - 0.01f, Belt, 1f));
                k.Add(K(RearAxle - 0.04f, s, w - 0.015f, w, ym, w - 0.035f, Belt - 0.01f, Belt));
                k.Add(K(ZR + 0.275f, s + 0.03f, w - 0.03f, w - 0.015f, ym, w - 0.065f, Belt + 0.005f, Belt + 0.01f));
                k.Add(K(ZR + 0.125f, s + 0.06f, w - 0.065f, w - 0.05f, ym + 0.01f, w - 0.1f, Belt, Belt + 0.005f));
            }
            float vert = Kind == RearKind.Notch || Kind == RearKind.Hatch ? 0.55f : 0.35f;
            k.Add(K(ZR + 0.075f, s + 0.08f, w - 0.095f, w - 0.08f, ym + 0.01f, w - 0.135f, Mathf.Lerp(bodyH, Bump, vert) - 0.01f, Mathf.Lerp(bodyH, Bump, vert), 0f, 0.8f));
            k.Add(K(ZR + 0.03f, s + 0.1f, w - 0.145f, w - 0.13f, ym, w - 0.185f, Bump + 0.14f, Bump + 0.15f, 0f, 1f));
            k.Add(K(ZR, s + 0.15f, w - 0.225f, w - 0.21f, s + 0.32f, w - 0.265f, Bump, Bump + 0.01f, 0f, 1f));
            // keys must stay ordered and apart (short noses / tails of some models)
            var sorted = k.OrderByDescending(q => q.Z).ToList();
            var outList = new List<BodyKey>();
            foreach (var q in sorted)
                if (outList.Count == 0 || outList[outList.Count - 1].Z - q.Z > 0.006f) outList.Add(q);
            return outList.ToArray();
        }

        protected override CabinKey[] CabinKeys()
        {
            float wb = W - 0.085f, wr = wb - Tumble;
            var c = new List<CabinKey>
            {
                new CabinKey(ScreenZ, Cowl, wb - 0.005f, Cowl, wr - 0.03f),
                new CabinKey(RoofFrontZ, Mathf.Lerp(Cowl, Belt, 0.8f), wb, RoofH - 0.015f, wr),
                new CabinKey((RoofFrontZ + RoofRearZ) * 0.5f, Belt - 0.005f, wb + 0.002f, RoofH, wr + 0.002f),
            };
            switch (Kind)
            {
                case RearKind.Notch:
                    c.Add(new CabinKey(RoofRearZ + 0.2f, Belt, wb, RoofH - 0.02f, wr - 0.01f));
                    c.Add(new CabinKey(RoofRearZ, Belt, wb - 0.01f, RoofH - 0.06f, wr - 0.03f));
                    c.Add(new CabinKey((RoofRearZ + GlassEndZ) * 0.5f, Mathf.Lerp(Belt, DeckH, 0.5f), wb - 0.02f, Mathf.Lerp(RoofH - 0.06f, DeckH, 0.58f), wr - 0.05f));
                    c.Add(new CabinKey(GlassEndZ, DeckH, wb - 0.04f, DeckH + 0.01f, wr - 0.08f));
                    break;
                default:
                    float drop = Kind == RearKind.Van ? 0.02f : Kind == RearKind.Estate ? 0.05f : 0.085f;
                    c.Add(new CabinKey(RoofRearZ + 0.24f, Belt, wb + 0.005f, RoofH - (Kind == RearKind.Van ? 0.01f : 0.035f), wr - 0.015f));
                    c.Add(new CabinKey(RoofRearZ, Belt, wb - 0.01f, RoofH - drop, wr - 0.035f));
                    c.Add(new CabinKey(GlassEndZ, Belt - 0.005f, wb - 0.04f, Belt + 0.01f, wr - 0.07f));
                    break;
            }
            return c.ToArray();
        }

        // ------------------------------------------------------------------ greenhouse materials

        /// <summary>Is z inside the side glass (door windows and the rear quarter / estate side windows)?</summary>
        private bool SideGlass(float z)
        {
            float end = Cargo ? BPillar - 0.045f : Kind == RearKind.Estate || Kind == RearKind.Van ? DPillar : CPillar;
            return z < ScreenZ - 0.04f && z > end;
        }

        private bool Pillar(float z) =>
            Mathf.Abs(z - BPillar) < 0.045f || ((Kind == RearKind.Estate || Kind == RearKind.Van) && !Cargo && z < CPillar && z > CPillar - 0.06f);

        protected override Material CabinMat(int seg, float z)
        {
            bool glass = SideGlass(z);
            switch (seg)
            {
                case 0: return glass ? Trim : Paint;
                case 1:
                case 2: return !glass ? Paint : Pillar(z) ? Trim : Glass;
                case 3: return glass ? Trim : Paint;
                case 4: return Paint;
                default:
                    if (z > RoofFrontZ) return z < ScreenZ - 0.04f ? Glass : Trim;     // windscreen with its lower seal
                    if (z < RoofRearZ) return Cargo ? Paint : z > GlassEndZ + 0.03f ? Glass : Trim;      // rear window
                    return Paint;
            }
        }
    }
}
