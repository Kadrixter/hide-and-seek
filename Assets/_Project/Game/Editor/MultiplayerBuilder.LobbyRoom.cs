using HideAndSeek.Core;
using HideAndSeek.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Game.Editor
{
    /// <summary>
    /// Lobby room (16 x 12 x 3.6 m) built from the floor plan in <see cref="LobbyLayout"/>: shell, wall finish
    /// (LobbyWalls), lounge (sofas, coffee table, poufs, fireplace + TV), lights (LobbyLights), furniture and the mini
    /// interactions (LobbyFurniture), rugs (LobbyRugs), the decor (LobbyDecor / LobbyDetails), spawns and audio.
    /// </summary>
    public static partial class MultiplayerBuilder
    {
        private static Transform BuildLobbyRoom()
        {
            const float W = LobbyLayout.W, D = LobbyLayout.D, H = LobbyLayout.H;
            var root = new GameObject("LobbyRoom").transform;
            // no sky in a closed room: weak environment reflections (shiny wood went grey-blue)
            RenderSettings.reflectionIntensity = 0.25f;

            var wood = Mat("LobbyWood", Color.white, Tex("lobby_wood", TexKind.Wood), 0.35f, new Vector2(4, 3));
            var wall = Mat("LobbyWall", Color.white, Wallpaper(), 0.08f, new Vector2((W + 0.4f) / WallpaperRepeat, 1f));
            var ceiling = Mat("LobbyCeiling", new Color(0.62f, 0.6f, 0.56f), null, 0f, Vector2.one);
            var trim = Mat("LobbyTrim", new Color(0.18f, 0.11f, 0.07f), null, 0.4f, Vector2.one);
            var fabric = Mat("LobbySofa", new Color(0.3f, 0.07f, 0.06f), Tex("lobby_fabric", TexKind.Fabric), 0.05f, new Vector2(2, 1));
            var darkWood = Mat("LobbyDarkWood", new Color(0.55f, 0.4f, 0.3f), Tex("lobby_wood", TexKind.Wood), 0.5f, new Vector2(1, 1));
            var metal = Mat("LobbyMetal", new Color(0.25f, 0.26f, 0.28f), null, 0.7f, Vector2.one, 0.8f);
            var board = Mat("LobbyBoard", new Color(0.08f, 0.12f, 0.1f), null, 0.1f, Vector2.one);

            // ---- shell
            var floor = Box("WoodFloor", root, new Vector3(0, -0.1f, 0), new Vector3(W, 0.2f, D), wood);
            floor.AddComponent<FootstepSurface>().Type = SurfaceType.Wood;
            Box("Ceiling", root, new Vector3(0, H + 0.1f, 0), new Vector3(W, 0.2f, D), ceiling);
            Box("WallN", root, new Vector3(0, H / 2, D / 2 + 0.1f), new Vector3(W + 0.4f, H, 0.2f), wall);
            Box("WallS", root, new Vector3(0, H / 2, -D / 2 - 0.1f), new Vector3(W + 0.4f, H, 0.2f), wall);
            Box("WallE", root, new Vector3(W / 2 + 0.1f, H / 2, 0), new Vector3(0.2f, H, D), wall);
            Box("WallW", root, new Vector3(-W / 2 - 0.1f, H / 2, 0), new Vector3(0.2f, H, D), wall);
            // wallpaper runs 1 m wide strips: per wall tiling so the pattern keeps its size
            BuildLobbyWalls(root, darkWood, trim);
            if (LobbyLayout.Garage) BuildGarageShell(root); // concrete, block walls, beams, conduit (LobbyGarage.cs)

            // ---- lounge: L of two sofas, the coffee table 0.58 m in front of the north-facing one, poufs on the east side
            Sofa(root, LobbyLayout.SofaFront.pos, LobbyLayout.SofaFront.yaw, fabric, darkWood);
            Sofa(root, LobbyLayout.SofaSide.pos, LobbyLayout.SofaSide.yaw, fabric, darkWood);
            Vector3 tc = LobbyLayout.CoffeeTable;
            Box("CoffeeTable", root, tc + new Vector3(0f, 0.42f, 0f), new Vector3(1.6f, 0.06f, 0.9f), darkWood);
            foreach (var (x, z) in new[] { (-0.7f, -0.35f), (0.7f, -0.35f), (-0.7f, 0.35f), (0.7f, 0.35f) })
                Box("TableLeg", root, tc + new Vector3(x, 0.2f, z), new Vector3(0.06f, 0.4f, 0.06f), darkWood);
            var pouf = Mat("LobbyPouf", new Color(0.45f, 0.32f, 0.12f), Tex("lobby_fabric", TexKind.Fabric), 0.05f, new Vector2(2, 1));
            foreach (var (pos, yaw) in LobbyLayout.Poufs) Pouf(root, pos, yaw, pouf);

            // ---- start lever (east wall), mirror (south wall), settings board (north wall)
            BuildLever(root, LobbyLayout.Lever, metal, trim);
            LobbyMirror(root, LobbyLayout.Mirror, new Vector2(1.9f, 2.2f), MirrorStyle);
            const float boardX = 0.9f;
            var boardGo = Box("SettingsBoard", root, new Vector3(boardX, 1.85f, D / 2 - 0.05f), new Vector3(3.6f, 2.16f, 0.06f), board);
            Box("BoardFrame", root, new Vector3(boardX, 1.85f, D / 2 - 0.02f), new Vector3(3.8f, 2.36f, 0.04f), trim);
            var sb = boardGo.AddComponent<LobbySettingsBoard>();
            var sbo = new SerializedObject(sb);
            sbo.FindProperty("screenSize").vector2Value = new Vector2(3.5f, 2.06f);
            sbo.ApplyModifiedPropertiesWithoutUndo();

            // ---- plants, paintings, neon
            var pot = Mat("LobbyPot", new Color(0.22f, 0.12f, 0.08f), null, 0.3f, Vector2.one);
            var leaf = Mat("LobbyLeaf", new Color(0.16f, 0.42f, 0.14f), Tex("lobby_fabric", TexKind.Fabric), 0.2f, new Vector2(0.5f, 0.5f));
            for (int i = 0; i < LobbyLayout.Plants.Length; i++)
                if (!LobbyLayout.Garage || LobbyLayout.Plants[i].x < 7f) Plant(root, LobbyLayout.Plants[i], pot, leaf, i + 1); // garage: tyres in the SE corner
            PaintingAt(root, new Vector3(LobbyLayout.RecordPlayer.x, 1.95f, -D / 2 + 0.003f), new Vector2(1.3f, 0.9f), "lobby_painting_city", 1, 0f);
            if (!LobbyLayout.Garage) PaintingAt(root, new Vector3(-W / 2 + 0.003f, 1.95f, LobbyLayout.Globe.z), new Vector2(0.9f, 1.2f), "lobby_painting_moon", 2, 90f);
            else BuildGarageProps(root); // pegboard with tools instead of the moon, tyres, oil drum (LobbyGarageProps.cs)
            Neon(root, new Vector3(-W / 2 + 0.06f, 2.65f, LobbyLayout.SofaSide.pos.z), "HIDE & SEEK");

            BuildLobbyLights(root, darkWood, metal);
            BuildLobbyDecor(root, darkWood, metal, trim, fabric);
            BuildLobbyFurniture(root, darkWood, metal);
            BuildLobbyRugs(root);

            // ---- spawns, audio
            var spawnRoot = new GameObject(GameLayers.SpawnPointsRoot).transform;
            int n = 0;
            foreach (var s in LobbyLayout.Spawns)
            {
                var sp = new GameObject($"LobbySpawn_{n++:00}");
                sp.tag = GameLayers.SpawnPointTag;
                sp.transform.SetParent(spawnRoot, false);
                sp.transform.position = new Vector3(s.x, 0.02f, s.y);
                sp.transform.rotation = Quaternion.LookRotation(new Vector3(LobbyLayout.SpawnFocus.x - s.x, 0f, LobbyLayout.SpawnFocus.y - s.y)); // half circle facing its centre
            }
            var lobbyAudio = new GameObject("GameSceneAudio").AddComponent<GameSceneAudio>();
            var lao = new SerializedObject(lobbyAudio);
            lao.FindProperty("kind").enumValueIndex = (int)HideAndSeek.Audio.AudioEnvironmentKind.SmallRoom;
            lao.FindProperty("populateEmitters").boolValue = false;
            lao.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        /// <summary>Gilt-framed painting on a wall; <paramref name="yaw"/> = direction it faces (0 = +Z, 90 = +X).</summary>
        private static Transform PaintingAt(Transform root, Vector3 pos, Vector2 size, string tex, int seed, float yaw)
        {
            var p = new GameObject("Painting").transform;
            p.SetParent(root, false);
            var frameMat = Mat("LobbyFrameGilt", new Color(0.62f, 0.46f, 0.2f), null, 0.65f, Vector2.one, 0.7f);
            // built facing +Z (on a south wall), then turned
            float fw = 0.07f;
            var prof = LobbyMesh.MouldingProfile(0.035f, fw, 0.01f);
            FrameRing(p, Vector3.zero, size, fw, prof, frameMat, "PaintFrame");
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "Canvas";
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.transform.SetParent(p, false);
            q.transform.localPosition = new Vector3(0f, 0f, 0.006f);
            q.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            q.transform.localScale = new Vector3(size.x, size.y, 1f);
            q.GetComponent<MeshRenderer>().sharedMaterial = Mat("Painting_" + tex, Color.white, PaintingTex(tex, seed), 0.1f, Vector2.one);
            p.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            return p;
        }

        /// <summary>
        /// Rectangular frame of 4 mitred mouldings around an opening of <paramref name="inner"/> (local XY, the wall at
        /// local z 0, the moulding standing out towards +Z). Profile x = depth, y = width.
        /// </summary>
        private static void FrameRing(Transform parent, Vector3 centre, Vector2 inner, float width, System.Collections.Generic.List<Vector2> prof, Material m, string name)
        {
            float ox = inner.x * 0.5f + width, oy = inner.y * 0.5f + width;
            string key = $"Frame_{inner.x:0.000}_{inner.y:0.000}_{width:0.000}_{prof.Count}".Replace(',', '.');
            void Side(string n, float len, Vector3 pos, float rotZ)
            {
                var mesh = SaveMesh(LobbyMesh.Sweep(prof, len, true, true), key + "_" + n + "_" + len.ToString("0.000").Replace(',', '.'));
                var g = new GameObject(name + n);
                g.transform.SetParent(parent, false);
                // Sweep: run along +X, profile out of the wall towards -Z, width up +Y: turn so it faces +Z
                g.transform.localPosition = centre + pos;
                g.transform.localRotation = Quaternion.Euler(0f, 180f, rotZ);
                g.AddComponent<MeshFilter>().sharedMesh = mesh;
                g.AddComponent<MeshRenderer>().sharedMaterial = m;
            }
            // each side: outer edge length, profile width pointing inwards
            Side("Bottom", ox * 2f, new Vector3(ox, -oy, 0f), 0f);
            Side("Top", ox * 2f, new Vector3(-ox, oy, 0f), 180f);
            Side("Left", oy * 2f, new Vector3(-ox, -oy, 0f), 90f);
            Side("Right", oy * 2f, new Vector3(ox, oy, 0f), -90f);
        }
    }
}
