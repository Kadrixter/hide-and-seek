using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Game.Editor
{
    /// <summary>
    /// Floor globe (spins, <see cref="LobbyGlobe"/>), coat rack with a jacket / scarf / hat, bookshelf full of books, the crates under the boombox and the empty anchors for
    /// the mini games (MiniGameSpot_Chess, MiniGameSpot_TicTacToe).
    /// </summary>
    public static partial class MultiplayerBuilder
    {
        private static void FloorGlobe(Transform parent, Vector3 pos, Material darkWood)
        {
            var t = Frame(parent, "FloorGlobe", pos, 60f);
            const float cy = 0.74f, r = 0.22f;
            // tripod with a stretcher ring, horizon ring on top of the legs
            for (int i = 0; i < 3; i++)
            {
                var leg = new GameObject("Leg").transform;
                leg.SetParent(t, false);
                leg.localRotation = Quaternion.Euler(0f, i * 120f, 0f);
                var g = LocalBox(leg, "LegWood", new Vector3(0f, 0.36f, 0.25f), new Vector3(0.035f, 0.74f, 0.035f), darkWood);
                g.transform.localRotation = Quaternion.Euler(-7f, 0f, 0f);
                RoundCube(g.transform, 0.01f, 0f);
                MeshObj("Foot", leg, LatheSaved("GlobeFoot", new[] { new Vector2(0.025f, 0f), new Vector2(0.02f, 0.02f), new Vector2(0.012f, 0.03f) }, 12, true, true), Brass, new Vector3(0f, 0f, 0.29f));
            }
            MeshObj("Stretcher", t, LobbyMeshSaved("GlobeStretcher", () => LobbyMesh.Torus(0.2f, 0.012f, 40, 8)), darkWood, new Vector3(0f, 0.22f, 0f));
            MeshObj("Horizon", t, LatheSaved("GlobeHorizon", new[] { new Vector2(0.235f, 0f), new Vector2(0.235f, 0.018f), new Vector2(0.27f, 0.025f), new Vector2(0.27f, 0.0f) }, 48, false, false), darkWood, new Vector3(0f, cy - 0.012f, 0f));
            // tilted axis: meridian ring + sphere spinning around its own axis
            var tilt = new GameObject("Axis").transform;
            tilt.SetParent(t, false);
            tilt.localPosition = new Vector3(0f, cy, 0f);
            tilt.localRotation = Quaternion.Euler(0f, 0f, 23.4f);
            MeshObj("Meridian", tilt, LobbyMeshSaved("GlobeMeridian", () => LobbyMesh.Torus(r + 0.015f, 0.006f, 48, 6)), Brass, Vector3.zero, Quaternion.Euler(90f, 0f, 0f));
            MeshObj("AxisPin", tilt, LatheSaved("GlobePin", new[] { new Vector2(0.006f, -r - 0.03f), new Vector2(0.006f, r + 0.03f) }, 8, true, true), Brass, Vector3.zero);
            var earth = Mat("LobbyGlobeEarth", Color.white, SmoothTex("lobby2_globe", 512, 256, (x, y) =>
            {
                float u = x / 512f, v = y / 256f;
                float lat = (v - 0.5f) * Mathf.PI;
                // continents: noise on the sphere (wraps around), fewer near the poles
                Vector3 p = new Vector3(Mathf.Cos(lat) * Mathf.Cos(u * Mathf.PI * 2f), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Sin(u * Mathf.PI * 2f));
                float n = Mathf.PerlinNoise(p.x * 2.2f + 5f, p.z * 2.2f + p.y * 1.3f + 3f) * 0.7f + Mathf.PerlinNoise(p.x * 6f + 1f, p.y * 6f + p.z * 3f) * 0.3f;
                bool land = n > 0.55f;
                Color sea = Color.Lerp(new Color(0.45f, 0.62f, 0.6f), new Color(0.32f, 0.5f, 0.52f), Mathf.Abs(v - 0.5f) * 1.2f);
                Color ground = Color.Lerp(new Color(0.85f, 0.75f, 0.52f), new Color(0.6f, 0.55f, 0.32f), Mathf.Clamp01((n - 0.55f) * 6f));
                var c = land ? ground : sea;
                if (!land && n > 0.52f) c = Color.Lerp(c, new Color(0.25f, 0.2f, 0.12f), 0.6f); // coast line
                if (Mathf.Abs(Mathf.Repeat(u * 12f, 1f) - 0.5f) > 0.485f || Mathf.Abs(Mathf.Repeat(v * 6f, 1f) - 0.5f) > 0.485f) c *= 0.72f; // grid
                if (Mathf.Abs(v - 0.5f) < 0.004f) c = new Color(0.55f, 0.15f, 0.1f); // equator
                return c * (0.92f + 0.08f * Mathf.PerlinNoise(x * 0.3f, y * 0.3f));
            }), 0.55f, Vector2.one);
            var sphere = Ball("Sphere", tilt, tilt.position, Vector3.one * r * 2f, earth);
            sphere.transform.localPosition = Vector3.zero;
            sphere.transform.localRotation = Quaternion.identity;
            var col = t.gameObject.AddComponent<CapsuleCollider>();
            col.radius = 0.3f;
            col.height = 1.0f;
            col.center = new Vector3(0f, 0.5f, 0f);
            t.gameObject.AddComponent<LobbyGlobe>().sphere = sphere.transform;
            AddProp(t.gameObject, LobbyPropKind.Globe, new Vector3(0f, cy, 0f), 1.9f);
        }

        private static void CoatRack(Transform parent, Vector3 pos, Material darkWood)
        {
            var t = Frame(parent, "CoatRack", pos, 200f);
            bool garage = LobbyLayout.Garage;
            if (garage) darkWood = GarageBlackSteel;
            var Brass = garage ? GarageSteel : MultiplayerBuilder.Brass;
            MeshObj("Base", t, LatheSaved("RackBase", new[] { new Vector2(0.21f, 0f), new Vector2(0.21f, 0.02f), new Vector2(0.17f, 0.05f), new Vector2(0.06f, 0.08f), new Vector2(0.04f, 0.1f) }, 32, true, true), darkWood, Vector3.zero);
            MeshObj("Pole", t, LatheSaved("RackPole", new[] { new Vector2(0.03f, 0f), new Vector2(0.022f, 1.6f), new Vector2(0.03f, 1.66f), new Vector2(0.032f, 1.7f), new Vector2(0.012f, 1.76f) }, 20, false, true), darkWood, new Vector3(0f, 0.1f, 0f));
            for (int i = 0; i < 6; i++)
            {
                var hook = new GameObject("Hook").transform;
                hook.SetParent(t, false);
                hook.localPosition = new Vector3(0f, i % 2 == 0 ? 1.66f : 1.56f, 0f);
                hook.localRotation = Quaternion.Euler(0f, i * 60f, 0f);
                var a = LocalBox(hook, "Arm", new Vector3(0f, 0.04f, 0.09f), new Vector3(0.014f, 0.014f, 0.14f), Brass);
                a.transform.localRotation = Quaternion.Euler(-35f, 0f, 0f);
                RoundCube(a.transform, 0.006f, 0f);
                MeshObj("Knob", hook, LatheSaved("RackKnob", new[] { new Vector2(0f, -0.01f), new Vector2(0.012f, 0f), new Vector2(0f, 0.012f) }, 10, false, false), Brass, new Vector3(0f, 0.085f, 0.15f));
            }
            // jacket on the front hook, a scarf on another, a hat on top
            var jacket = garage ? GrimyFabric("GarageWorkJacket", new Color(0.12f, 0.16f, 0.3f), 2, 44, Vector2.one) : Mat("LobbyJacket", new Color(0.18f, 0.24f, 0.17f), Tex("lobby_fabric", TexKind.Fabric), 0.1f, new Vector2(2f, 3f));
            RBox(t, "Jacket", new Vector3(0f, 1.12f, 0.17f), new Vector3(0.42f, 0.78f, 0.1f), jacket, 0.04f, 0.02f).transform.localRotation = Quaternion.Euler(4f, 0f, 0f);
            RBox(t, "Collar", new Vector3(0f, 1.52f, 0.16f), new Vector3(0.24f, 0.08f, 0.09f), jacket, 0.03f, 0.01f);
            foreach (float sx in new[] { -0.2f, 0.2f })
                RBox(t, "Sleeve", new Vector3(sx, 1.08f, 0.19f), new Vector3(0.1f, 0.62f, 0.1f), jacket, 0.04f, 0.01f).transform.localRotation = Quaternion.Euler(6f, 0f, sx > 0 ? 4f : -4f);
            var scarf = Mat("LobbyScarf", new Color(0.65f, 0.12f, 0.12f), Tex("lobby_fabric", TexKind.Fabric), 0.05f, new Vector2(1f, 4f));
            var hs = new GameObject("ScarfHook").transform;
            hs.SetParent(t, false);
            hs.localRotation = Quaternion.Euler(0f, 120f, 0f);
            RBox(hs, "ScarfA", new Vector3(0.03f, 1.28f, 0.16f), new Vector3(0.12f, 0.6f, 0.025f), scarf, 0.01f).transform.localRotation = Quaternion.Euler(3f, 0f, 3f);
            RBox(hs, "ScarfB", new Vector3(-0.04f, 1.24f, 0.18f), new Vector3(0.12f, 0.66f, 0.025f), scarf, 0.01f).transform.localRotation = Quaternion.Euler(5f, 0f, -4f);
            var felt = Mat("LobbyHat", new Color(0.12f, 0.1f, 0.09f), null, 0.15f, Vector2.one);
            if (garage)
            {
                // a red cap: crown + peak
                var capRed = Mat("GarageCapRed", new Color(0.6f, 0.08f, 0.07f), Tex("lobby_fabric", TexKind.Fabric), 0.1f, Vector2.one);
                var cap = new GameObject("Cap").transform;
                cap.SetParent(t, false);
                cap.localPosition = new Vector3(0f, 1.78f, 0f);
                cap.localRotation = Quaternion.Euler(10f, 30f, -6f);
                MeshObj("Crown", cap, LatheSaved("GarageCapCrown", new[] { new Vector2(0.095f, 0f), new Vector2(0.092f, 0.04f), new Vector2(0.07f, 0.08f), new Vector2(0.03f, 0.098f), new Vector2(0f, 0.1f) }, 24, false, false), capRed, Vector3.zero);
                RBox(cap, "Peak", new Vector3(0f, 0.005f, 0.13f), new Vector3(0.15f, 0.008f, 0.09f), capRed, 0.003f);
            }
            else MeshObj("Hat", t, LatheSaved("Hat", new[] { new Vector2(0f, 0f), new Vector2(0.15f, 0f), new Vector2(0.155f, 0.01f), new Vector2(0.09f, 0.015f), new Vector2(0.085f, 0.1f), new Vector2(0.06f, 0.115f), new Vector2(0f, 0.11f) }, 32, false, false),
                felt, new Vector3(0f, 1.84f, 0f), Quaternion.Euler(8f, 0f, -5f));
            var col = t.gameObject.AddComponent<CapsuleCollider>();
            col.radius = 0.22f;
            col.height = 1.8f;
            col.center = new Vector3(0f, 0.9f, 0f);
        }

        private static void Crates(Transform parent)
        {
            var crate = Mat("LobbyCrate", new Color(0.62f, 0.48f, 0.32f), Tex("lobby_crate", TexKind.Crate), 0.1f, Vector2.one);
            Vector3 c = LobbyLayout.Crates, b = LobbyLayout.Boombox;
            Box("Crate1", parent, c + new Vector3(0f, 0.4f, 0f), new Vector3(0.8f, 0.8f, 0.8f), crate).transform.Rotate(0f, 172f, 0f); // stencil to the room
            Box("Crate2", parent, c + new Vector3(0.05f, 1.15f, 0.05f), new Vector3(0.7f, 0.7f, 0.7f), crate).transform.Rotate(0f, 198f, 0f);
            Box("Crate3", parent, new Vector3(b.x, 0.35f, b.z), new Vector3(0.7f, 0.7f, 0.7f), crate).transform.Rotate(0f, 170f, 0f);
        }

        private static void MiniGameSpots(Transform parent)
        {
            // empty anchors: the mini-games agent builds the chess table / tic-tac-toe board on them
            var chess = new GameObject("MiniGameSpot_Chess").transform;
            chess.SetParent(parent, false);
            chess.position = LobbyLayout.ChessSpot;
            chess.rotation = Quaternion.identity; // players at local +-X (0.75 m), table 0.96 m (seat to seat) x 1.2 m
            var ttt = new GameObject("MiniGameSpot_TicTacToe").transform;
            ttt.SetParent(parent, false);
            ttt.position = LobbyLayout.TicTacToeSpot;
            ttt.rotation = Quaternion.LookRotation(Vector3.back); // forward = into the room
        }
    }
}
