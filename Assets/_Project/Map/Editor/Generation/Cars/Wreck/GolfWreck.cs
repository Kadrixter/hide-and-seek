using System.Collections.Generic;
using UnityEngine;
using static HideAndSeek.Map.Editor.CarShapes;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// The wrecked black Volkswagen Golf IV GTI (2002, 5-door) of level -2, after the two reference photos: front end torn
    /// off (no bumper, grille or lamps, crash beam hanging to the floor), the right front crushed and scraped to bare
    /// metal, right front wheel ripped off (the car leans on the hub), bare engine bay, driver's door open, cracked
    /// windscreen, red GTI seats, deployed airbags, green foil over the roof, thick dust with "speed &lt;3" written in it,
    /// and the parts lying next to it. Body proportions from <see cref="VwGolf4"/> (4.15 x 1.73 x 1.44 m, wheelbase
    /// 2.51 m). Local space: nose +z, right +x, floor y = 0; the whole car (with the debris) is one prop.
    /// </summary>
    internal sealed partial class GolfWreck : SportCar
    {
        public const string PropName = "VwGolf4GtiWreck";
        public override string Name => PropName;
        public override string DefaultPaint => "Wreck_Paint";
        public override float Length => 4.15f;
        protected override Arch FrontArch => new Arch(1.205f, 0.31f, 0.345f);
        protected override Arch RearArch => new Arch(-1.306f, 0.31f, 0.345f);
        protected override float TireRadius => 0.316f;   // 225/45 R17
        protected override float TireWidth => 0.225f;
        protected override float RimRadius => 0.212f;
        protected override float TubWall => 0.7f;
        protected override float TubFloor => 0.26f;
        protected override float[] BodyBreaks => new[] { DF, DB, RB, HoodZ, 1.5f, -1.95f };
        protected override float[] CabinBreaks => new[] { 0.08f, 0.12f, 0.88f, DF + 0.02f, FG0, BP0, RG1, RG0, DV0, QE, -1.84f, -1.86f, -1.93f };
        protected override float SideFullness => 0.6f;
        protected override float DeckRidge(float z) => 0.014f * Mathf.Clamp01((z - 0.95f) / 0.1f) * Mathf.Clamp01((1.92f - z) / 0.12f);
        protected override int[] SharpRings => new[] { BDeck1 };
        protected override float RoofRound => 0.8f;
        protected override Material FrontCap => null;
        protected override float BodyStep => 0.03f;

        // doors (z): front door from the hinge line DF back to DB, rear door DB..RB; glass openings above the belt
        private const float DF = 0.88f, DB = -0.15f, RB = -0.93f, HoodZ = 0.96f, SillY = 0.35f;
        private const float FG0 = -0.1f, BP0 = -0.2f, RG1 = -0.2f, RG0 = -0.78f, DV0 = -0.82f;
        // quarter light of the rear door: arched rear edge from the roof at QE to the belt at QA
        private const float QE = -1.32f, QA = -1.0f, Recess = 0.012f;
        private const float DoorOpen = 58f;

        private readonly List<(Vector3 c, float side)> _wheels = new List<(Vector3, float)>();
        private Material Primer, Carpet, Metal, Engine, Alu, Rust, PlasticGrey, Rubber;
        private Quaternion _tilt;
        private Vector3 _pivot;

        /// <summary>Builds the wreck (car + debris) as one prop.</summary>
        public static PropMesh Create(PropFactory props, MaterialLibrary lib, List<Mesh> store = null)
        {
            lib.EnsureWreckMaterials();
            return props.Get("Wreck_" + PropName, (mb, pm) =>
            {
                // shared helpers (mirrors, wheels, lamps) pick the dusty versions of their materials
                mb.Alias = new Dictionary<Material, Material>
                {
                    { lib[MaterialLibrary.CarPaints[0]], lib["Wreck_Paint"] },
                    { lib["Car_Trim"], lib["Wreck_Plastic"] },
                    { lib["Plastic_Black"], lib["Wreck_Plastic"] },
                    { lib["Car_Glass"], lib["Wreck_Glass"] },
                    { lib["Rubber"], lib["Wreck_Rubber"] },
                    { lib["Metal_Dark"], lib["Wreck_Disc"] },
                    { lib["Car_RimSilver"], lib["Wreck_Rim"] },
                    { lib["Car_PlasticGrey"], lib["Wreck_PlasticGrey"] },
                };
                new GolfWreck().Build(mb, pm);
            }, store);
        }

        private static BodyKey K(float z, float yBot, float wBot, float wMax, float yMax, float wSh, float ySh, float yTop,
            float tub = 0f, float round = 0.55f) => new BodyKey(z, yBot, wBot, wMax, yMax, wSh, ySh, yTop, round, tub);

        /// <summary>Golf IV lower body without the bumper: the wings end at the lamp line (the front is gone).</summary>
        protected override BodyKey[] BodyKeys()
        {
            Primer = Lib["Wreck_Primer"]; Carpet = Lib["Wreck_Carpet"]; Metal = Lib["Wreck_Steel"]; Engine = Lib["Wreck_Engine"];
            Alu = Lib["Wreck_Alu"]; Rust = Lib["Wreck_Rust"]; PlasticGrey = Lib["Wreck_PlasticGrey"]; Rubber = Lib["Wreck_Rubber"];
            Grille = Lib["Wreck_Plastic"]; Glass = Lib["Wreck_Glass"];
            return new[]
            {
                K(1.97f, 0.215f, 0.8f, 0.815f, 0.485f, 0.79f, 0.73f, 0.75f),
                K(1.9f, 0.2f, 0.825f, 0.84f, 0.5f, 0.81f, 0.77f, 0.79f),
                K(1.6f, 0.18f, 0.845f, 0.86f, 0.53f, 0.83f, 0.82f, 0.84f),
                K(1.205f, 0.18f, 0.85f, 0.865f, 0.55f, 0.835f, 0.88f, 0.895f),
                K(0.95f, 0.18f, 0.85f, 0.865f, 0.56f, 0.835f, 0.93f, 0.95f),
                K(0.905f, 0.18f, 0.85f, 0.865f, 0.56f, 0.835f, 0.935f, 0.952f, 1f),
                K(0.82f, 0.18f, 0.85f, 0.865f, 0.56f, 0.835f, 0.94f, 0.955f, 1f),
                K(-1.2f, 0.18f, 0.85f, 0.865f, 0.57f, 0.83f, 0.985f, 0.98f, 1f),
                K(-1.34f, 0.18f, 0.85f, 0.865f, 0.57f, 0.83f, 0.99f, 0.99f),
                K(-1.8f, 0.21f, 0.835f, 0.85f, 0.57f, 0.8f, 0.995f, 1.0f),
                K(-1.95f, 0.24f, 0.8f, 0.815f, 0.58f, 0.765f, 0.99f, 0.995f),
                K(-2.0f, 0.26f, 0.77f, 0.785f, 0.58f, 0.73f, 0.86f, 0.87f, 0f, 0.8f),
                K(-2.045f, 0.28f, 0.72f, 0.735f, 0.57f, 0.68f, 0.7f, 0.71f, 0f, 1f),
                K(-2.075f, 0.33f, 0.64f, 0.655f, 0.5f, 0.6f, 0.56f, 0.57f, 0f, 1f),
            };
        }

        protected override CabinKey[] CabinKeys() => new[]
        {
            new CabinKey(0.95f, 0.95f, 0.77f, 0.95f, 0.63f),
            new CabinKey(0.05f, 0.975f, 0.785f, 1.425f, 0.65f),
            new CabinKey(-0.4f, 0.985f, 0.785f, 1.44f, 0.65f),
            new CabinKey(-0.95f, 0.99f, 0.8f, 1.437f, 0.648f),
            new CabinKey(-1.3f, 0.993f, 0.828f, 1.43f, 0.643f),
            new CabinKey(-1.6f, 0.997f, 0.815f, 1.405f, 0.628f),
            new CabinKey(-1.84f, 0.998f, 0.79f, 1.345f, 0.6f),
            new CabinKey(-1.95f, 0.99f, 0.762f, 1.0f, 0.57f),
        };

        // the lofts are emitted per side by the wreck itself (asymmetric damage, open door)
        protected override Material BodyMat(int seg, int j) => null;
        protected override Material CabinMat(int seg, float z) => null;

        protected override void BuildWheel(Vector3 c, float side) => _wheels.Add((c, side));

        protected override void Details()
        {
            Skin();
            Seams();
            Glasses();
            DriverDoor();
            Rear();
            FrontStructure();
            // the crash: everything so far is crushed by the deformation field
            Mb.Warp(0, Crush);
            EngineBay();
            Foil();
            // the car settles on the hub: lean forward and to the right
            _pivot = new Vector3(0f, 0.31f, -0.05f);
            _tilt = Quaternion.Euler(2.3f, 0f, -3.8f);
            Mb.Warp(0, p => _pivot + _tilt * (p - _pivot));
            SetMaskPlanes();
            Wheels();
            HangingBeam();
            Debris();
        }

        /// <summary>Point of the car before the lean (local) to the final pose.</summary>
        private Vector3 Pose(Vector3 p) => _pivot + _tilt * (p - _pivot);

        protected override void Colliders()
        {
            var b = Pm.Colliders;
            b.Add(new Bounds(new Vector3(0f, 0.6f, -0.05f), new Vector3(1.72f, 0.84f, 4.05f)));
            b.Add(new Bounds(new Vector3(0f, 1.2f, -0.7f), new Vector3(1.4f, 0.4f, 2.2f)));
            // open driver's door
            b.Add(new Bounds(new Vector3(-1.2f, 0.75f, 0.5f), new Vector3(0.55f, 0.9f, 0.55f)));
            DebrisColliders();
        }
    }
}
