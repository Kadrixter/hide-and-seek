using System;
using System.Collections.Generic;
using System.IO;
using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using HideAndSeek.Game.UI;
using HideAndSeek.Player;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace HideAndSeek.Game.Editor
{
    /// <summary>
    /// Builds everything the multiplayer game needs: network prefabs, Menu / Lobby / Game scenes,
    /// hint voice list, build settings, and the Windows player.
    /// </summary>
    public static class MultiplayerBuilder
    {
        private const string Root = "Assets/_Project/Game";
        private const string PrefabDir = Root + "/Prefabs";
        private const string SceneDir = Root + "/Scenes";
        private const string LobbyArtDir = Root + "/Lobby/Generated";
        private const string PlayerPrefabPath = "Assets/_Project/Player/Prefabs/Player.prefab";
        private const string NetPlayerPath = PrefabDir + "/NetPlayer.prefab";
        private const string MatchManagerPath = PrefabDir + "/MatchManager.prefab";
        private const string MapScene = "Assets/_Project/Map/Scenes/ParkingGarage.unity";
        public const string MenuScenePath = SceneDir + "/Menu.unity";
        public const string LobbyScenePath = SceneDir + "/Lobby.unity";
        public const string GameScenePath = SceneDir + "/Game_ParkingGarage.unity";
        private const string BuildOutput = "Builds/Windows/HideAndSeek.exe";

        [MenuItem("HideAndSeek/Multiplayer/Build All Scenes")]
        public static void BuildAllMenu() => BuildAll();

        [MenuItem("HideAndSeek/Multiplayer/Build Windows Player")]
        public static void BuildPlayerMenu()
        {
            BuildAll();
            BuildPlayer();
        }

        public static void BuildAllFromCommandLine() => RunBatch(BuildAll);

        public static void BuildPlayerFromCommandLine() => RunBatch(() =>
        {
            BuildAll();
            BuildPlayer();
        });

        private static void RunBatch(Action a)
        {
            try
            {
                a();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        public static void BuildAll()
        {
            Directory.CreateDirectory(PrefabDir);
            Directory.CreateDirectory(SceneDir);
            Directory.CreateDirectory(LobbyArtDir);
            EnsureLayer(PlayerAvatarBinder.LocalBodyLayer, "LocalPlayerBody");
            ConfigurePlayerSettings();
            BuildHintVoices();
            var netPlayer = BuildNetPlayerPrefab();
            var matchManager = BuildMatchManagerPrefab();
            BuildMenuScene(netPlayer, matchManager);
            BuildLobbyScene();
            BuildGameScene();
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MenuScenePath, true),
                new EditorBuildSettingsScene(LobbyScenePath, true),
                new EditorBuildSettingsScene(GameScenePath, true),
            };
            AssetDatabase.SaveAssets();
            Debug.Log("[MultiplayerBuilder] All scenes built.");
        }

        private static void BuildPlayer()
        {
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { MenuScenePath, LobbyScenePath, GameScenePath },
                locationPathName = BuildOutput,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new Exception($"Player build failed: {report.summary.result}, {report.summary.totalErrors} errors");
            Debug.Log($"[MultiplayerBuilder] Player built to {BuildOutput}");
        }

        // ================================================================ settings

        private static void ConfigurePlayerSettings()
        {
            PlayerSettings.productName = "Hide and Seek";
            PlayerSettings.companyName = "Kadrixter";
            PlayerSettings.runInBackground = true;
            PlayerSettings.visibleInBackground = true;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.forceSingleInstance = false;
        }

        private static void EnsureLayer(int index, string name)
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            var entry = layers.GetArrayElementAtIndex(index);
            if (entry.stringValue == name) return;
            entry.stringValue = name;
            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildHintVoices()
        {
            const string resDir = Root + "/Resources";
            Directory.CreateDirectory(resDir);
            string path = resDir + "/HintVoices.asset";
            var asset = AssetDatabase.LoadAssetAtPath<HintVoices>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<HintVoices>();
                AssetDatabase.CreateAsset(asset, path);
            }
            var clips = new List<AudioClip>();
            if (AssetDatabase.IsValidFolder("Assets/_Project/Audio/Voice"))
                foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/_Project/Audio/Voice" }))
                    clips.Add(AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid)));
            asset.clips = clips.ToArray();
            EditorUtility.SetDirty(asset);
            Debug.Log($"[MultiplayerBuilder] Hint voices: {clips.Count}");
        }

        // ================================================================ prefabs

        private static GameObject BuildNetPlayerPrefab()
        {
            var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (basePrefab == null) throw new FileNotFoundException("Player prefab missing", PlayerPrefabPath);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            go.name = "NetPlayer";

            if (go.GetComponent<NetworkObject>() == null) go.AddComponent<NetworkObject>();
            var nt = go.GetComponent<OwnerNetworkTransform>() ?? go.AddComponent<OwnerNetworkTransform>();
            nt.SyncRotAngleX = false;
            nt.SyncRotAngleZ = false;
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
            nt.Interpolate = true;
            nt.InLocalSpace = false;
            if (go.GetComponent<NetPlayer>() == null) go.AddComponent<NetPlayer>();

            // Only the local copy may own the camera, listener and input (enabled at runtime).
            foreach (var cam in go.GetComponentsInChildren<Camera>(true)) cam.enabled = false;
            foreach (var l in go.GetComponentsInChildren<AudioListener>(true)) l.enabled = false;
            foreach (var r in go.GetComponentsInChildren<PlayerInputReader>(true)) r.enabled = false;

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, NetPlayerPath);
            UnityEngine.Object.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject BuildMatchManagerPrefab()
        {
            var go = new GameObject("MatchManager");
            go.AddComponent<NetworkObject>();
            go.AddComponent<MatchManager>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, MatchManagerPath);
            UnityEngine.Object.DestroyImmediate(go);
            return prefab;
        }

        // ================================================================ menu scene

        private static void BuildMenuScene(GameObject netPlayer, GameObject matchManager)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var nmGo = new GameObject("NetworkManager");
            var nm = nmGo.AddComponent<NetworkManager>();
            var transport = nmGo.AddComponent<UnityTransport>();
            nm.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                PlayerPrefab = netPlayer,
                ConnectionApproval = true,
                EnableSceneManagement = true,
                TickRate = 30,
                ClientConnectionBufferTimeout = 15,
            };
            var session = nmGo.AddComponent<NetSession>();
            var so = new SerializedObject(session);
            so.FindProperty("playerPrefab").objectReferenceValue = netPlayer;
            so.FindProperty("matchManagerPrefab").objectReferenceValue = matchManager;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Backdrop: dark concrete corner, the player's character under a cold spotlight.
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.03f, 0.035f, 0.04f);
            RenderSettings.skybox = null;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.08f;
            RenderSettings.fogColor = Color.black;

            var concrete = Mat("MenuConcrete", new Color(0.32f, 0.32f, 0.31f), Tex("menu_concrete", TexKind.Concrete), 0.15f, new Vector2(6, 6));
            Box("Floor", null, new Vector3(0, -0.05f, 0), new Vector3(30, 0.1f, 30), concrete);
            Box("Wall", null, new Vector3(0, 3, 4), new Vector3(30, 6, 0.3f), concrete);
            Box("Pillar", null, new Vector3(-2.2f, 3, 2.2f), new Vector3(0.7f, 6, 0.7f), concrete);
            Box("Pillar2", null, new Vector3(3.6f, 3, 1.4f), new Vector3(0.7f, 6, 0.7f), concrete);

            var preview = new GameObject("CharacterPreview");
            preview.transform.position = new Vector3(0.9f, 0f, 0.6f);
            preview.transform.rotation = Quaternion.Euler(0, 200, 0);

            var spot = new GameObject("Spot").AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.transform.position = new Vector3(0.9f, 4.2f, -0.6f);
            spot.transform.LookAt(new Vector3(0.9f, 0.6f, 0.6f));
            spot.spotAngle = 48f;
            spot.range = 10f;
            spot.intensity = 3.2f;
            spot.color = new Color(0.85f, 0.9f, 1f);
            spot.shadows = LightShadows.Soft;

            var rim = new GameObject("Rim").AddComponent<Light>();
            rim.type = LightType.Point;
            rim.transform.position = new Vector3(2.5f, 1.6f, 2.5f);
            rim.color = new Color(1f, 0.35f, 0.25f);
            rim.range = 5f;
            rim.intensity = 1.2f;

            var camGo = new GameObject("MenuCamera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.fieldOfView = 42f;
            camGo.transform.position = new Vector3(-0.6f, 1.35f, -3.4f);
            camGo.transform.LookAt(new Vector3(-0.25f, 0.95f, 0.6f));
            camGo.AddComponent<AudioListener>();

            var menuGo = new GameObject("MainMenu");
            var menu = menuGo.AddComponent<MainMenu>();
            var mso = new SerializedObject(menu);
            mso.FindProperty("previewRoot").objectReferenceValue = preview.transform;
            mso.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, MenuScenePath);
        }

        // ================================================================ lobby scene

        private static void BuildLobbyScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.09f, 0.08f, 0.07f);
            RenderSettings.skybox = null;
            RenderSettings.fog = false;

            const float W = 16f, D = 12f, H = 3.6f;
            var root = new GameObject("LobbyRoom").transform;

            var wood = Mat("LobbyWood", Color.white, Tex("lobby_wood", TexKind.Wood), 0.35f, new Vector2(4, 3));
            var wall = Mat("LobbyWall", Color.white, Tex("lobby_wallpaper", TexKind.Wallpaper), 0.05f, new Vector2(5, 1.2f));
            var carpet = Mat("LobbyCarpet", Color.white, Tex("lobby_carpet", TexKind.Carpet), 0f, new Vector2(2, 1.5f));
            var ceiling = Mat("LobbyCeiling", new Color(0.55f, 0.53f, 0.5f), null, 0f, Vector2.one);
            var trim = Mat("LobbyTrim", new Color(0.18f, 0.11f, 0.07f), null, 0.4f, Vector2.one);
            var fabric = Mat("LobbySofa", new Color(0.3f, 0.07f, 0.06f), Tex("lobby_fabric", TexKind.Fabric), 0.05f, new Vector2(2, 1));
            var darkWood = Mat("LobbyDarkWood", new Color(0.55f, 0.4f, 0.3f), Tex("lobby_wood", TexKind.Wood), 0.5f, new Vector2(1, 1));
            var metal = Mat("LobbyMetal", new Color(0.25f, 0.26f, 0.28f), null, 0.7f, Vector2.one, 0.8f);
            var lampMat = Mat("LobbyLamp", new Color(1f, 0.85f, 0.6f), null, 0.2f, Vector2.one);
            lampMat.EnableKeyword("_EMISSION");
            lampMat.SetColor("_EmissionColor", new Color(1f, 0.8f, 0.55f) * 2.5f);
            var green = Mat("LobbyButtonGreen", new Color(0.1f, 0.75f, 0.2f), null, 0.6f, Vector2.one);
            green.EnableKeyword("_EMISSION");
            green.SetColor("_EmissionColor", new Color(0.1f, 0.9f, 0.2f));
            var board = Mat("LobbyBoard", new Color(0.08f, 0.12f, 0.1f), null, 0.1f, Vector2.one);

            // Shell
            var floor = Box("WoodFloor", root, new Vector3(0, -0.1f, 0), new Vector3(W, 0.2f, D), wood);
            floor.AddComponent<FootstepSurface>().Type = SurfaceType.Wood;
            Box("Ceiling", root, new Vector3(0, H + 0.1f, 0), new Vector3(W, 0.2f, D), ceiling);
            Box("WallN", root, new Vector3(0, H / 2, D / 2 + 0.1f), new Vector3(W + 0.4f, H, 0.2f), wall);
            Box("WallS", root, new Vector3(0, H / 2, -D / 2 - 0.1f), new Vector3(W + 0.4f, H, 0.2f), wall);
            Box("WallE", root, new Vector3(W / 2 + 0.1f, H / 2, 0), new Vector3(0.2f, H, D), wall);
            Box("WallW", root, new Vector3(-W / 2 - 0.1f, H / 2, 0), new Vector3(0.2f, H, D), wall);
            // Skirting boards
            Box("SkirtN", root, new Vector3(0, 0.07f, D / 2 - 0.01f), new Vector3(W, 0.14f, 0.04f), trim);
            Box("SkirtS", root, new Vector3(0, 0.07f, -D / 2 + 0.01f), new Vector3(W, 0.14f, 0.04f), trim);
            Box("SkirtE", root, new Vector3(W / 2 - 0.01f, 0.07f, 0), new Vector3(0.04f, 0.14f, D), trim);
            Box("SkirtW", root, new Vector3(-W / 2 + 0.01f, 0.07f, 0), new Vector3(0.04f, 0.14f, D), trim);

            // Carpet in the middle (footstep zone)
            var rug = Box("Carpet", root, new Vector3(-1.5f, 0.005f, 0.5f), new Vector3(7f, 0.01f, 5f), carpet);
            UnityEngine.Object.DestroyImmediate(rug.GetComponent<Collider>());
            var rugZone = rug.AddComponent<FootstepSurface>();
            rugZone.Type = SurfaceType.Carpet;
            rugZone.ZoneSize = new Vector3(7f, 0.6f, 5f);
            rugZone.Priority = 5;

            // Sofas around the rug
            Sofa(root, new Vector3(-1.5f, 0, 3.6f), 180f, fabric, darkWood);
            Sofa(root, new Vector3(-5.6f, 0, 0.5f), 90f, fabric, darkWood);
            Box("CoffeeTable", root, new Vector3(-1.5f, 0.42f, 0.8f), new Vector3(1.6f, 0.06f, 0.9f), darkWood);
            foreach (var (x, z) in new[] { (-2.2f, 0.45f), (-0.8f, 0.45f), (-2.2f, 1.15f), (-0.8f, 1.15f) })
                Box("TableLeg", root, new Vector3(x, 0.2f, z), new Vector3(0.06f, 0.4f, 0.06f), darkWood);

            // Shelves on the west wall
            for (int i = 0; i < 4; i++)
                Box("Shelf", root, new Vector3(-W / 2 + 0.25f, 0.6f + i * 0.55f, -3.5f), new Vector3(0.4f, 0.04f, 2.2f), darkWood);
            Box("ShelfSide1", root, new Vector3(-W / 2 + 0.25f, 1.2f, -2.4f), new Vector3(0.4f, 2.4f, 0.04f), darkWood);
            Box("ShelfSide2", root, new Vector3(-W / 2 + 0.25f, 1.2f, -4.6f), new Vector3(0.4f, 2.4f, 0.04f), darkWood);
            var rnd = new System.Random(7);
            for (int i = 0; i < 18; i++)
            {
                int shelf = rnd.Next(4);
                float z = -4.4f + (float)rnd.NextDouble() * 1.8f;
                float h = 0.18f + (float)rnd.NextDouble() * 0.12f;
                var col = Color.HSVToRGB((float)rnd.NextDouble(), 0.5f, 0.35f);
                Box("Book", root, new Vector3(-W / 2 + 0.25f, 0.62f + shelf * 0.55f + h / 2, z), new Vector3(0.26f, h, 0.05f),
                    Mat("Book" + i, col, null, 0.1f, Vector2.one));
            }

            // Crates and a metal locker in the corner
            var crate = Mat("LobbyCrate", new Color(0.62f, 0.48f, 0.32f), Tex("lobby_wood", TexKind.Wood), 0.1f, new Vector2(0.5f, 0.5f));
            Box("Crate1", root, new Vector3(6.6f, 0.4f, -4.6f), new Vector3(0.8f, 0.8f, 0.8f), crate);
            Box("Crate2", root, new Vector3(6.5f, 1.15f, -4.5f), new Vector3(0.7f, 0.7f, 0.7f), crate).transform.Rotate(0, 18, 0);
            Box("Crate3", root, new Vector3(5.7f, 0.35f, -4.8f), new Vector3(0.7f, 0.7f, 0.7f), crate).transform.Rotate(0, -10, 0);
            Box("Locker", root, new Vector3(7.6f, 1f, 3.5f), new Vector3(0.6f, 2f, 1.6f), metal);

            // Ceiling lamps
            foreach (var (x, z) in new[] { (-4f, -2.5f), (4f, -2.5f), (-4f, 3f), (4f, 3f), (0f, 0.5f) })
            {
                Box("LampShade", root, new Vector3(x, H - 0.08f, z), new Vector3(0.6f, 0.08f, 0.6f), lampMat);
                var l = new GameObject("LampLight").AddComponent<Light>();
                l.transform.SetParent(root, false);
                l.transform.position = new Vector3(x, H - 0.35f, z);
                l.type = LightType.Point;
                l.color = new Color(1f, 0.78f, 0.52f);
                l.range = 8f;
                l.intensity = 1.25f;
                l.shadows = x == 0f ? LightShadows.Soft : LightShadows.None;
            }

            // Start button on a pedestal by the east wall
            var buttonRoot = new GameObject("StartButton").transform;
            buttonRoot.SetParent(root, false);
            buttonRoot.position = new Vector3(5.2f, 0f, 0.5f);
            Cylinder("Pedestal", buttonRoot, new Vector3(0, 0.5f, 0), new Vector3(0.7f, 0.5f, 0.7f), metal);
            Cylinder("Rim", buttonRoot, new Vector3(0, 1.02f, 0), new Vector3(0.62f, 0.03f, 0.62f), Mat("ButtonRim", new Color(0.85f, 0.75f, 0.1f), null, 0.6f, Vector2.one));
            var cap = Cylinder("Cap", buttonRoot, new Vector3(0, 1.1f, 0), new Vector3(0.48f, 0.07f, 0.48f), green);
            var glow = new GameObject("Glow").AddComponent<Light>();
            glow.transform.SetParent(buttonRoot, false);
            glow.transform.localPosition = new Vector3(0, 1.5f, 0);
            glow.type = LightType.Point;
            glow.color = new Color(0.2f, 1f, 0.3f);
            glow.range = 3.5f;
            glow.intensity = 1.6f;
            var label = new GameObject("Label").AddComponent<TextMesh>();
            label.transform.SetParent(buttonRoot, false);
            label.transform.localPosition = new Vector3(0, 0.62f, -0.36f);
            label.text = "START";
            label.characterSize = 0.05f;
            label.fontSize = 64;
            label.anchor = TextAnchor.MiddleCenter;
            label.color = new Color(0.9f, 0.9f, 0.85f);
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.GetComponent<MeshRenderer>().sharedMaterial = label.font.material;
            buttonRoot.position = new Vector3(5.2f, 0f, 0.5f);
            buttonRoot.rotation = Quaternion.Euler(0, -90, 0);
            var lb = buttonRoot.gameObject.AddComponent<LobbyButton>();
            var lbo = new SerializedObject(lb);
            lbo.FindProperty("cap").objectReferenceValue = cap.transform;
            lbo.FindProperty("glow").objectReferenceValue = glow;
            lbo.FindProperty("capRenderer").objectReferenceValue = cap.GetComponent<Renderer>();
            lbo.ApplyModifiedPropertiesWithoutUndo();

            // Settings board on the north wall
            var boardGo = Box("SettingsBoard", root, new Vector3(3.2f, 1.75f, D / 2 - 0.05f), new Vector3(2.6f, 1.7f, 0.06f), board);
            Box("BoardFrame", root, new Vector3(3.2f, 1.75f, D / 2 - 0.02f), new Vector3(2.8f, 1.9f, 0.04f), trim);
            var bt = new GameObject("BoardText").AddComponent<TextMesh>();
            bt.transform.SetParent(boardGo.transform.parent, false);
            bt.transform.position = new Vector3(3.2f, 1.75f, D / 2 - 0.09f);
            bt.transform.rotation = Quaternion.Euler(0, 180, 0);
            bt.characterSize = 0.022f;
            bt.fontSize = 60;
            bt.anchor = TextAnchor.MiddleCenter;
            bt.alignment = TextAlignment.Left;
            bt.color = new Color(0.85f, 0.95f, 0.85f);
            bt.text = "USTAWIENIA RUNDY";
            bt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            bt.GetComponent<MeshRenderer>().sharedMaterial = bt.font.material;
            var sb = boardGo.AddComponent<LobbySettingsBoard>();
            var sbo = new SerializedObject(sb);
            sbo.FindProperty("text").objectReferenceValue = bt;
            sbo.ApplyModifiedPropertiesWithoutUndo();

            // Spawn points
            var spawnRoot = new GameObject(GameLayers.SpawnPointsRoot).transform;
            int n = 0;
            foreach (var (x, z) in new[] { (-5f, -3.5f), (-3f, -4f), (-1f, -3.8f), (1f, -4f), (3f, -3.6f), (0f, -2f), (2.5f, 2.5f), (-1f, -1.5f) })
            {
                var sp = new GameObject($"LobbySpawn_{n++:00}");
                sp.tag = GameLayers.SpawnPointTag;
                sp.transform.SetParent(spawnRoot, false);
                sp.transform.position = new Vector3(x, 0.02f, z);
                sp.transform.rotation = Quaternion.LookRotation(new Vector3(5.2f - x, 0f, 0.5f - z));
            }

            var reverb = new GameObject("RoomReverb").AddComponent<AudioReverbZone>();
            reverb.reverbPreset = AudioReverbPreset.Livingroom;
            reverb.minDistance = 30f;
            reverb.maxDistance = 40f;

            EditorSceneManager.SaveScene(scene, LobbyScenePath);
        }

        private static void Sofa(Transform root, Vector3 pos, float yaw, Material fabric, Material wood)
        {
            var s = new GameObject("Sofa").transform;
            s.SetParent(root, false);
            Box("Seat", s, new Vector3(0, 0.25f, 0), new Vector3(2.4f, 0.4f, 0.9f), fabric);
            Box("Back", s, new Vector3(0, 0.65f, 0.38f), new Vector3(2.4f, 0.8f, 0.2f), fabric);
            Box("ArmL", s, new Vector3(-1.15f, 0.5f, 0), new Vector3(0.2f, 0.6f, 0.9f), fabric);
            Box("ArmR", s, new Vector3(1.15f, 0.5f, 0), new Vector3(0.2f, 0.6f, 0.9f), fabric);
            Box("Base", s, new Vector3(0, 0.04f, 0), new Vector3(2.3f, 0.08f, 0.8f), wood);
            s.position = pos;
            s.rotation = Quaternion.Euler(0, yaw, 0);
        }

        // ================================================================ game scene

        private static void BuildGameScene()
        {
            var scene = EditorSceneManager.OpenScene(MapScene, OpenSceneMode.Single);
            foreach (var cam in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
                UnityEngine.Object.DestroyImmediate(cam.gameObject);
            foreach (var l in UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include))
                UnityEngine.Object.DestroyImmediate(l);
            new GameObject("GameSceneAudio").AddComponent<GameSceneAudio>();
            EditorSceneManager.SaveScene(scene, GameScenePath);
        }

        // ================================================================ geometry helpers

        private static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            go.isStatic = true;
            return go;
        }

        private static GameObject Cylinder(string name, Transform parent, Vector3 localPos, Vector3 scale, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            return go;
        }

        private static Material Mat(string name, Color color, Texture2D tex, float smooth, Vector2 tiling, float metallic = 0f)
        {
            string path = $"{LobbyArtDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.color = color;
            m.mainTexture = tex;
            m.mainTextureScale = tiling;
            m.SetFloat("_Glossiness", smooth);
            m.SetFloat("_Metallic", metallic);
            EditorUtility.SetDirty(m);
            return m;
        }

        private enum TexKind { Wood, Wallpaper, Carpet, Fabric, Concrete }

        private static Texture2D Tex(string name, TexKind kind)
        {
            string path = $"{LobbyArtDir}/{name}.png";
            if (!File.Exists(path))
            {
                const int n = 256;
                var tex = new Texture2D(n, n, TextureFormat.RGB24, false);
                var px = new Color[n * n];
                var rnd = new System.Random(name.GetHashCode());
                float[] plankShade = new float[8];
                for (int i = 0; i < 8; i++) plankShade[i] = 0.85f + (float)rnd.NextDouble() * 0.3f;
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = x / (float)n, v = y / (float)n;
                    Color c;
                    switch (kind)
                    {
                        case TexKind.Wood:
                        {
                            int plank = y / 32;
                            float offset = plank * 0.37f;
                            float grain = Mathf.PerlinNoise(u * 3f + offset * 10f, v * 40f) * 0.5f + Mathf.PerlinNoise(u * 18f, v * 90f) * 0.25f;
                            float seam = (y % 32 == 0) || (((x + plank * 97) % 256) == 0) ? 0.45f : 1f;
                            float s = plankShade[plank % 8] * (0.75f + grain * 0.5f) * seam;
                            c = new Color(0.46f * s, 0.29f * s, 0.16f * s);
                            break;
                        }
                        case TexKind.Wallpaper:
                        {
                            float stripe = Mathf.Repeat(u * 16f, 1f) < 0.5f ? 1f : 0.88f;
                            float n2 = 0.92f + Mathf.PerlinNoise(u * 30f, v * 30f) * 0.1f;
                            float lower = v < 0.3f ? 0.6f : 1f;
                            c = new Color(0.33f, 0.36f, 0.3f) * stripe * n2 * lower;
                            break;
                        }
                        case TexKind.Carpet:
                        {
                            float fibre = 0.8f + (float)rnd.NextDouble() * 0.25f;
                            float border = (u < 0.06f || u > 0.94f || v < 0.08f || v > 0.92f) ? 1f : 0f;
                            float pattern = Mathf.Abs(Mathf.Sin(u * 25f) * Mathf.Sin(v * 25f)) > 0.6f ? 0.8f : 1f;
                            c = border > 0 ? new Color(0.55f, 0.42f, 0.18f) * fibre : new Color(0.32f, 0.06f, 0.07f) * fibre * pattern;
                            break;
                        }
                        case TexKind.Fabric:
                        {
                            float weave = ((x + y) % 4 < 2 ? 1f : 0.9f) * (0.9f + (float)rnd.NextDouble() * 0.12f);
                            c = new Color(weave, weave, weave);
                            break;
                        }
                        default:
                        {
                            float nn = Mathf.PerlinNoise(u * 8f, v * 8f) * 0.3f + Mathf.PerlinNoise(u * 40f, v * 40f) * 0.15f + 0.6f;
                            c = new Color(nn, nn, nn * 0.98f);
                            break;
                        }
                    }
                    px[y * n + x] = c;
                }
                tex.SetPixels(px);
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
