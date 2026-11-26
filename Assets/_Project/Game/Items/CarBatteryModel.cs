using HideAndSeek.Characters;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// The 12 V car battery as a carried / dropped item (ItemDefs.CarBattery.cs) and in the security-room power tray
    /// (SecurityRooms.Power.cs): black case with a grey lid, the red + and black - terminal caps, a yellow label and a
    /// folding carry strap on top. Model space: origin = middle of the case, +Y up, the long side along Z.
    /// </summary>
    public static class CarBatteryModel
    {
        public const float HalfL = 0.135f, HalfW = 0.0875f, HalfH = 0.095f;
        /// <summary>Half of the whole height (case bottom to the strap): a battery standing on the floor has its centre this high.</summary>
        public const float HalfHeight = HalfH;
        /// <summary>Where the fist holds it: the middle of the strap.</summary>
        public static readonly Vector3 StrapPoint = new Vector3(-0.03f, HalfH + 0.05f, 0f);
        /// <summary>Terminal posts (model space, top of the post): + and -.</summary>
        public static readonly Vector3 PlusPost = new Vector3(0.04f, HalfH + 0.025f, HalfL - 0.05f), MinusPost = new Vector3(0.04f, HalfH + 0.025f, -HalfL + 0.05f);

        private static Mesh _mesh;
        private static Material[] _mats;

        public static Transform Create(Transform parent, int layer = 0)
        {
            Build();
            var go = new GameObject("CarBattery");
            go.layer = layer;
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = _mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = _mats;
            return go.transform;
        }

        private static void Build()
        {
            if (_mesh != null) return;
            _mats = new[]
            {
                PaMesh.Mat("BatteryCase", new Color(0.07f, 0.07f, 0.08f), 0.35f),
                PaMesh.Mat("BatteryLid", new Color(0.32f, 0.33f, 0.35f), 0.3f),
                PaMesh.Mat("BatteryPlus", new Color(0.8f, 0.1f, 0.07f), 0.45f),
                PaMesh.Mat("BatteryPost", new Color(0.72f, 0.72f, 0.7f), 0.55f, 0.8f),
                PaMesh.Mat("BatteryLabel", new Color(0.9f, 0.7f, 0.12f), 0.3f),
            };
            var m = new PaMesh(5);
            m.Box(new Vector3(0f, -0.01f, 0f), new Vector3(HalfW * 2f, HalfH * 2f - 0.02f, HalfL * 2f), Quaternion.identity, 0);
            m.Box(new Vector3(0f, HalfH - 0.01f, 0f), new Vector3(HalfW * 2f + 0.006f, 0.022f, HalfL * 2f + 0.006f), Quaternion.identity, 1);
            // label on both long sides
            for (int s = -1; s <= 1; s += 2)
                m.Box(new Vector3(s * (HalfW + 0.001f), -0.005f, 0f), new Vector3(0.003f, 0.07f, 0.17f), Quaternion.identity, 4);
            // terminals: steel posts, red cap on +, black cap on -
            foreach (var (p, sub) in new[] { (PlusPost, 2), (MinusPost, 0) })
            {
                m.Lathe(p - new Vector3(0f, 0.025f, 0f), Vector3.up, new[] { new Vector2(0.011f, 0f), new Vector2(0.011f, 0.025f), new Vector2(0f, 0.026f) }, 10, 3);
                m.Box(p + new Vector3(0f, -0.008f, 0f), new Vector3(0.036f, 0.018f, 0.036f), Quaternion.identity, sub);
            }
            // carry strap (two posts + the band)
            for (int s = -1; s <= 1; s += 2)
                m.Box(new Vector3(-0.03f, HalfH + 0.025f, s * 0.06f), new Vector3(0.02f, 0.05f, 0.012f), Quaternion.identity, 0);
            m.Box(new Vector3(-0.03f, HalfH + 0.05f, 0f), new Vector3(0.024f, 0.01f, 0.13f), Quaternion.identity, 0);
            _mesh = m.ToMesh("CarBattery");
        }
    }
}
