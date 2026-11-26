using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Second metal shelf in the generator room (level -1, east wall), with four RC rally cars (two on the middle board, two on the top one)
    /// (their models are the <see cref="RcCar"/> objects themselves). Built at runtime on every client in the game scene.
    /// </summary>
    public class RcCarShelf : MonoBehaviour
    {
        /// <summary>Floor point at the shelf centre (world). Local +Z points at the east wall, -Z (front) into the room.</summary>
        public static readonly Vector3 Position = new Vector3(-12.27f, -3.4f, 53.7f);
        public const float Yaw = 90f;
        public const float BoardY = 1.08f;

        public static RcCarShelf Instance { get; private set; }

        /// <summary>Where car <paramref name="slot"/> stands on the board and the way it faces (slightly angled to the front).</summary>
        public const float TopBoardY = 1.55f;

        public static Vector3 CarPos(int slot) => Position + Quaternion.Euler(0f, Yaw, 0f) *
            new Vector3(slot % 2 == 0 ? -0.3f : 0.3f, (slot < 2 ? BoardY : TopBoardY) + 0.002f, -0.04f);
        public static float CarYaw(int slot) => Yaw + 180f + (slot % 2 == 0 ? (slot < 2 ? -18f : 20f) : (slot < 2 ? 22f : -16f));

        public static void Ensure()
        {
            if (Instance != null) return;
            var go = new GameObject("RcCarShelf");
            go.transform.SetPositionAndRotation(Position, Quaternion.Euler(0f, Yaw, 0f));
            Instance = go.AddComponent<RcCarShelf>();
            Instance.Build();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Build()
        {
            var steel = new Color(0.3f, 0.33f, 0.36f);
            var board = new Color(0.44f, 0.41f, 0.36f);
            const float w = 1.2f, d = 0.42f, h = 1.85f;
            foreach (var x in new[] { -w / 2f + 0.025f, w / 2f - 0.025f })
            foreach (var z in new[] { -d / 2f + 0.025f, d / 2f - 0.025f })
                Cube("Post", new Vector3(x, h / 2f, z), new Vector3(0.05f, h, 0.05f), steel);
            foreach (var y in new[] { 0.15f, 0.62f, BoardY, TopBoardY })
                Cube("Board", new Vector3(0f, y - 0.015f, 0f), new Vector3(w, 0.03f, d), board);
            // Toy-shop clutter: the cars' boxes, a battery pack charger, spare tyres, paint tins.
            Cube("CarBox", new Vector3(-0.25f, 0.62f + 0.11f, 0.03f), new Vector3(0.5f, 0.22f, 0.3f), new Color(0.12f, 0.32f, 0.78f));
            Cube("CarBoxLid", new Vector3(-0.25f, 0.62f + 0.222f, 0.03f), new Vector3(0.48f, 0.004f, 0.28f), new Color(0.95f, 0.78f, 0.1f));
            Cube("Charger", new Vector3(0.32f, 0.62f + 0.04f, 0f), new Vector3(0.16f, 0.08f, 0.11f), new Color(0.12f, 0.12f, 0.13f));
            Cube("ChargerLed", new Vector3(0.32f, 0.62f + 0.081f, -0.03f), new Vector3(0.025f, 0.004f, 0.025f), new Color(0.2f, 1f, 0.3f));
            Cube("Battery", new Vector3(0.12f, 0.62f + 0.025f, -0.02f), new Vector3(0.14f, 0.05f, 0.05f), new Color(0.85f, 0.15f, 0.1f));
            Cube("Tyres", new Vector3(0.36f, 0.15f + 0.05f, 0.04f), new Vector3(0.2f, 0.1f, 0.1f), new Color(0.06f, 0.06f, 0.07f));
            Cube("Tin", new Vector3(-0.35f, 0.15f + 0.08f, 0.05f), new Vector3(0.14f, 0.16f, 0.14f), new Color(0.55f, 0.56f, 0.6f));
            Cube("Tin2", new Vector3(-0.15f, 0.15f + 0.07f, 0.06f), new Vector3(0.12f, 0.14f, 0.12f), new Color(0.75f, 0.2f, 0.12f));

            // Price / "RC" sign on the middle board edge (bright, so the cars are spotted from the door).
            Cube("Sign", new Vector3(0f, BoardY - 0.06f, -d / 2f - 0.003f), new Vector3(0.36f, 0.07f, 0.004f), new Color(1f, 0.82f, 0.12f));
            // No own lamp (2026-11-26): the spot above the shelf lit the cars up unnaturally; the room lighting is enough.
        }

        private void Cube(string n, Vector3 lp, Vector3 size, Color c)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = n;
            g.transform.SetParent(transform, false);
            g.transform.localPosition = lp;
            g.transform.localScale = size;
            if (size.x < 0.3f && size.z < 0.3f || n == "Sign" || n == "CarBoxLid") Destroy(g.GetComponent<Collider>());
            var r = g.GetComponent<MeshRenderer>();
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor("_Color", c);
            r.SetPropertyBlock(mpb);
        }
    }
}
