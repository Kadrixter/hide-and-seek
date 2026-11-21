using HideAndSeek.Characters;
using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Metal shelf in the generator room (level -1, between the two generators, against the north wall) with the
    /// chainsaw lying on it. Built at runtime on every client in the game scene; the saw disappears once taken.
    /// </summary>
    public class ChainsawShelf : MonoBehaviour
    {
        /// <summary>Floor point in front of the shelf centre (world).</summary>
        public static readonly Vector3 Position = new Vector3(-21.8f, -3.4f, 59.62f);
        public const float Yaw = 0f; // local -Z (front, lamp side) faces south into the room

        public static ChainsawShelf Instance { get; private set; }

        /// <summary>Where the saw lies (prompt anchor).</summary>
        public Vector3 SawPoint => _saw != null ? _saw.transform.position : transform.position + Vector3.up * 1.0f;

        private GameObject _saw;
        private Light _lamp;

        public static void Ensure(bool taken)
        {
            if (Instance == null)
            {
                var go = new GameObject("ChainsawShelf");
                go.transform.SetPositionAndRotation(Position, Quaternion.Euler(0f, Yaw, 0f));
                Instance = go.AddComponent<ChainsawShelf>();
                Instance.Build();
            }
            if (Instance._saw != null && Instance._saw.activeSelf == taken) Instance._saw.SetActive(!taken);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Build()
        {
            var steel = new Color(0.32f, 0.34f, 0.37f);
            var board = new Color(0.42f, 0.4f, 0.36f);
            const float w = 1.3f, d = 0.45f, h = 1.85f;
            // Local: +Z = towards the north wall, -Z = front (room side).
            foreach (var x in new[] { -w / 2f + 0.025f, w / 2f - 0.025f })
            foreach (var z in new[] { -d / 2f + 0.025f, d / 2f - 0.025f })
                Cube("Post", new Vector3(x, h / 2f, z), new Vector3(0.05f, h, 0.05f), steel);
            foreach (var y in new[] { 0.15f, 0.62f, 1.08f, 1.55f })
                Cube("Board", new Vector3(0f, y, 0f), new Vector3(w, 0.03f, d), board);
            // Some clutter: oil can, rag box.
            Cube("OilCan", new Vector3(0.45f, 0.62f + 0.13f, 0f), new Vector3(0.14f, 0.24f, 0.1f), new Color(0.75f, 0.12f, 0.08f));
            Cube("Box", new Vector3(-0.35f, 1.55f + 0.12f, 0.02f), new Vector3(0.4f, 0.22f, 0.3f), new Color(0.55f, 0.42f, 0.25f));
            Cube("Can2", new Vector3(0.4f, 0.15f + 0.14f, 0.05f), new Vector3(0.22f, 0.26f, 0.22f), new Color(0.2f, 0.35f, 0.2f));

            // The chainsaw lies on the middle board, bar pointing to the side.
            var saw = BlockyProps.Create(HeldItem.Chainsaw, transform, 0);
            saw.transform.localPosition = new Vector3(-0.32f, 1.08f + 0.085f, -0.02f);
            saw.transform.localRotation = Quaternion.Euler(0f, 95f, 0f);
            _saw = saw.gameObject;

            // Dim work lamp above the shelf so the saw can be spotted.
            _lamp = new GameObject("ShelfLamp").AddComponent<Light>();
            _lamp.transform.SetParent(transform, false);
            _lamp.transform.localPosition = new Vector3(0f, 2.1f, -0.5f);
            _lamp.transform.localRotation = Quaternion.Euler(70f, 0f, 0f);
            _lamp.type = LightType.Spot;
            _lamp.spotAngle = 70f;
            _lamp.range = 4f;
            _lamp.intensity = 1.4f;
            _lamp.color = new Color(1f, 0.8f, 0.55f);
        }

        private void Cube(string n, Vector3 lp, Vector3 size, Color c)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = n;
            g.transform.SetParent(transform, false);
            g.transform.localPosition = lp;
            g.transform.localScale = size;
            var r = g.GetComponent<MeshRenderer>();
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor("_Color", c);
            r.SetPropertyBlock(mpb);
        }
    }
}
