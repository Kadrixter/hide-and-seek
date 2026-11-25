using HideAndSeek.Characters;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// A small red jerry can for the chainsaw fuel (ItemDefs.FuelCan.cs): rounded flat body with the embossed X on both
    /// sides, three handles joined by one grip bar on top, a dark spout with its cap on the front top corner.
    /// Model space: origin = middle of the body, +Y up, the body lies in the XY plane (flat sides along Z).
    /// </summary>
    public static class FuelCanModel
    {
        /// <summary>Half of the whole height (body bottom to the grip bar): a can standing on the floor has its centre this high.</summary>
        public const float HalfHeight = 0.197f;
        /// <summary>Where the fist holds it: the middle of the grip bar.</summary>
        public static readonly Vector3 HandlePoint = new Vector3(0f, 0.21f, 0f);

        private const float HalfW = 0.135f, HalfH = 0.17f, HalfD = 0.065f;
        private static Mesh _mesh;
        private static Material[] _mats;

        public static Transform Create(Transform parent, int layer = 0)
        {
            Build();
            var go = new GameObject("FuelCan");
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
                PaMesh.Mat("FuelCanPaint", new Color(0.78f, 0.1f, 0.07f), 0.5f),
                PaMesh.Mat("FuelCanEmboss", new Color(0.55f, 0.06f, 0.05f), 0.4f),
                PaMesh.Mat("FuelCanSpout", new Color(0.12f, 0.12f, 0.13f), 0.55f, 0.6f),
            };
            var m = new PaMesh(3);
            m.Pebble(PaMesh.RoundRect(HalfW, HalfH, 0.03f), PaMesh.SlabRings(-HalfD, HalfD, 0.018f), Vector3.zero, Vector3.right, Vector3.up, Vector3.forward, 0);
            // The X pressed into both flat sides (standing out a little, darker).
            for (int side = -1; side <= 1; side += 2)
            for (int k = -1; k <= 1; k += 2)
                m.Box(new Vector3(0f, -0.01f, side * (HalfD + 0.002f)), new Vector3(0.3f, 0.024f, 0.008f), Quaternion.Euler(0f, 0f, k * 40f), 1);
            // Three handles joined by the grip bar.
            for (int i = -1; i <= 1; i++)
                m.Box(new Vector3(i * 0.075f, HalfH + 0.018f, 0f), new Vector3(0.024f, 0.05f, 0.026f), Quaternion.identity, 0);
            m.Box(new Vector3(0f, 0.21f, 0f), new Vector3(0.2f, 0.024f, 0.032f), Quaternion.identity, 0);
            // Spout with its cap, leaning out of the front top corner.
            var axis = new Vector3(0.5f, 0.85f, 0f).normalized;
            m.Lathe(new Vector3(HalfW - 0.03f, HalfH - 0.01f, 0f), axis, new[]
            {
                new Vector2(0.022f, 0f), new Vector2(0.022f, 0.03f), new Vector2(0.028f, 0.03f), new Vector2(0.028f, 0.05f), new Vector2(0f, 0.052f),
            }, 14, 2);
            _mesh = m.ToMesh("FuelCan");
        }
    }
}
