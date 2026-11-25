using UnityEngine;

namespace HideAndSeek.Game.MiniGames.Pool
{
    /// <summary>
    /// Procedural 7 ft pool table built at runtime (2026-11-25): a slate bed under cloth (nap, head string, head spot,
    /// foot spot sticker), K-66 cushions with proper jaws (facings at 142 deg in the corners, 104 deg at the sides),
    /// lacquered rails with an edge moulding and 18 inlaid sights, six metal pocket castings with leather liners and
    /// net baskets with a fringe, a moulded apron with a maker's plate and four turned legs on levelling feet.
    /// Table space as in <see cref="PoolDims"/>: origin on the floor under the middle of the cloth, long axis X (head at
    /// -X, foot at +X), +Y up. Styles: 0 = walnut, green cloth, brass; 1 = ebonised oak, blue cloth, chrome;
    /// 2 = ebonised oak, burgundy cloth, brass. One box collider for the whole table (no ball physics).
    /// </summary>
    public sealed partial class PoolTableModel
    {
        /// <summary>Outer size of the top (rails included).</summary>
        public const float OuterL = PoolTableGeom.OutX * 2f, OuterW = PoolTableGeom.OutZ * 2f;

        public Transform Root, Cloth;
        public TableKit Kit;
        public int Style;

        Material _wood, _legWood, _cloth, _cushion, _slate, _metal, _leather, _pearl, _steel, _rubber, _raw, _plate;

        /// <summary>Centre of pocket i's hole at cloth height: 0 (-X,-Z), 1 (0,-Z), 2 (+X,-Z), 3 (-X,+Z), 4 (0,+Z), 5 (+X,+Z).</summary>
        public static Vector3 PocketCentre(int i)
        {
            var c = PoolTableGeom.Centre(i);
            return new Vector3(c.x, PoolDims.ClothY, c.y);
        }

        /// <summary>Radius of pocket i's hole.</summary>
        public static float PocketRadius(int i) => PoolTableGeom.HoleR(i);

        public static PoolTableModel Build(Transform root, Material template, int style)
        {
            var m = new PoolTableModel { Root = root, Style = style, Kit = new TableKit(template, root.gameObject.layer, style) };
            m.InitMaterials();
            m.BuildBed();
            m.BuildCushions();
            m.BuildRails();
            m.BuildPockets();
            m.BuildReturn();
            m.BuildFrame();
            m.BuildLegs();
            TableKit.Collider(root, "PoolTableCollider", new Vector3(0f, PoolDims.RailTopY * 0.5f, 0f), new Vector3(OuterL, PoolDims.RailTopY, OuterW));
            return m;
        }

        void InitMaterials()
        {
            var k = Kit;
            bool dark = Style != 0, chrome = Style == 1;
            Color wood = dark ? new Color(0.19f, 0.12f, 0.085f) : new Color(0.46f, 0.28f, 0.17f);
            _wood = k.Mat(wood, 0f, 0.74f, PoolTableArt.GrainAlong);
            _legWood = k.Mat(wood * 0.96f, 0f, 0.72f, PoolTableArt.GrainUp);
            _cloth = k.Mat(Color.white, 0f, 0.05f, PoolTableArt.Bed(Style));
            _cushion = k.Mat(Color.white, 0f, 0.05f, PoolTableArt.Nap(Style));
            _slate = k.Mat(new Color(0.17f, 0.18f, 0.19f), 0f, 0.25f);
            _metal = chrome ? k.Mat(new Color(0.82f, 0.83f, 0.86f), 0.75f, 0.86f) : k.Mat(new Color(0.80f, 0.63f, 0.36f), 0.7f, 0.8f);
            _leather = k.Mat(dark ? new Color(0.07f, 0.06f, 0.06f) : new Color(0.25f, 0.13f, 0.065f), 0f, 0.48f, BoardArt.Leather);
            _pearl = k.Mat(Color.white, 0.1f, 0.92f, PoolTableArt.Pearl);
            _steel = k.Mat(new Color(0.72f, 0.72f, 0.74f), 0.8f, 0.7f);
            _rubber = k.Mat(new Color(0.03f, 0.03f, 0.03f), 0f, 0.25f);
            _raw = k.Mat(new Color(0.42f, 0.32f, 0.22f), 0f, 0.12f, PoolTableArt.GrainAlong);
            _plate = k.Mat(Color.white, 0.6f, 0.72f, PoolTableArt.Plate);
        }

        GameObject Part(string name, Mesh mesh, Material mat, Transform parent = null) =>
            Kit.Part(name, parent != null ? parent : Root, mesh, mat, Vector3.zero, Quaternion.identity);

        Transform Group(string name, Vector3 pos, Quaternion rot)
        {
            var t = new GameObject(name) { layer = Kit.Layer }.transform;
            t.SetParent(Root, false);
            t.localPosition = pos;
            t.localRotation = rot;
            return t;
        }
    }
}
