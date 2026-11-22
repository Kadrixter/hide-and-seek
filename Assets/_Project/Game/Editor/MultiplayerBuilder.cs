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
    public static partial class MultiplayerBuilder
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
        private static string BuildOutput
        {
            get
            {
                string[] a = Environment.GetCommandLineArgs();
                int i = Array.IndexOf(a, "-buildOut");
                return i >= 0 && i + 1 < a.Length ? a[i + 1] : "Builds/Windows/HideAndSeek.exe";
            }
        }

        [MenuItem("HideAndSeek/Multiplayer/Build All Scenes")]
        public static void BuildAllMenu() => BuildAll();

        [MenuItem("HideAndSeek/Multiplayer/Build Windows Player")]
        public static void BuildPlayerMenu()
        {
            BuildAllStable();
            BuildPlayer();
        }

        /// <summary>Builds every scene (and the lobby art it needs).</summary>
        private static void BuildAllStable() => BuildAll();

        private static bool _artGenerated; // set when lobby textures / CCTV stills were (re)generated (log only)

        public static void BuildAllFromCommandLine() => RunBatch(BuildAll);

        public static void BuildPlayerFromCommandLine() => RunBatch(() =>
        {
            BuildAllStable();
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
            EnsureLayer(9, "ViewModel");
            EnsureLayer(10, "MenuFloor");
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
            // Steam (AppID 480 for testing) expects steam_appid.txt next to the exe when not launched from Steam.
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(BuildOutput))!, "steam_appid.txt"), "480");
            // Lobby radio tools (yt-dlp, ffmpeg) from <project>/Tools next to the exe.
            string tools = Path.GetFullPath("Tools");
            if (Directory.Exists(tools))
            {
                string dst = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(BuildOutput))!, "Tools");
                Directory.CreateDirectory(dst);
                foreach (var f in Directory.GetFiles(tools, "*.exe"))
                {
                    string to = Path.Combine(dst, Path.GetFileName(f));
                    if (!File.Exists(to) || new FileInfo(to).Length != new FileInfo(f).Length) File.Copy(f, to, true);
                }
            }
            WriteUpdaterFiles(Path.GetDirectoryName(Path.GetFullPath(BuildOutput))!);
            Debug.Log($"[MultiplayerBuilder] Player built to {BuildOutput}");
        }

        // ================================================================ settings

        private static void ConfigurePlayerSettings()
        {
            PlayerSettings.productName = "Hide and Seek";
            // Build stamp: servers reject clients from a different build (see NetSession.Approve).
            PlayerSettings.bundleVersion = DateTime.Now.ToString("yyyy.MM.dd.HHmm");
            PlayerSettings.companyName = "Kadrixter";
            PlayerSettings.runInBackground = true;
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.visibleInBackground = true;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.forceSingleInstance = false;
            // D3D12 crashed ("Unrecoverable GPU device error") with several copies running; D3D11 is the safe choice here.
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { UnityEngine.Rendering.GraphicsDeviceType.Direct3D11 });
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

            var preview = new GameObject("CharacterPreview");
            preview.transform.position = new Vector3(0.9f, 0f, 0.6f);
            preview.transform.rotation = Quaternion.Euler(0, 214, 0); // 3/4 towards the camera
            BuildMenuBackdrop(preview.transform);

            var camGo = new GameObject("MenuCamera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.fieldOfView = 46f;
            cam.allowHDR = true;
            cam.allowMSAA = true;
            camGo.transform.position = new Vector3(-0.7f, 1.4f, -3.9f);
            camGo.transform.LookAt(new Vector3(-0.15f, 1.05f, 0.6f));
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
            CaptureCctv(out _); // renders the CCTV stills (once) in its own scene, before the lobby scene exists
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _seatCounter = 0;
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
            Sofa(root, new Vector3(-1.5f, 0, 3.6f), 0f, fabric, darkWood);     // backrest to the north wall
            Sofa(root, new Vector3(-5.6f, 0, 0.5f), -90f, fabric, darkWood);   // backrest to the west wall
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

            // Start lever on the east wall (room owner pulls it when everyone is ready).
            BuildLever(root, new Vector3(W / 2 - 0.02f, 0f, 0.5f), metal, trim);

            // Big mirror on the south wall.
            BuildMirror(root, new Vector3(0.8f, 1.35f, -D / 2 + 0.03f), new Vector2(1.9f, 2.4f), darkWood);

            // Poufs around the coffee table (seats).
            var pouf = Mat("LobbyPouf", new Color(0.45f, 0.32f, 0.12f), Tex("lobby_fabric", TexKind.Fabric), 0.05f, new Vector2(2, 1));
            Pouf(root, new Vector3(0.6f, 0f, 0.8f), -90f, pouf);
            Pouf(root, new Vector3(-1.5f, 0f, -0.9f), 0f, pouf);
            Pouf(root, new Vector3(-3.5f, 0f, -0.6f), 30f, pouf);

            // Plants in the corners.
            var pot = Mat("LobbyPot", new Color(0.22f, 0.12f, 0.08f), null, 0.3f, Vector2.one);
            var leaf = Mat("LobbyLeaf", new Color(0.16f, 0.42f, 0.14f), Tex("lobby_fabric", TexKind.Fabric), 0.2f, new Vector2(0.5f, 0.5f));
            Plant(root, new Vector3(-7.3f, 0f, 5.3f), pot, leaf, 1);
            Plant(root, new Vector3(7.3f, 0f, 5.3f), pot, leaf, 2);
            Plant(root, new Vector3(-7.3f, 0f, -5.3f), pot, leaf, 3);
            Plant(root, new Vector3(5.6f, 0f, -5.3f), pot, leaf, 4);

            // Paintings on the north wall.
            Painting(root, new Vector3(-1.2f, 1.9f, D / 2 - 0.03f), new Vector2(1.4f, 1.0f), "lobby_painting_city", 1, trim);
            Painting(root, new Vector3(-4.4f, 1.9f, D / 2 - 0.03f), new Vector2(1.0f, 1.3f), "lobby_painting_moon", 2, trim);

            // Floor lamps next to the sofas.
            FloorLamp(root, new Vector3(-6.9f, 0f, 3.9f), metal, lampMat);
            FloorLamp(root, new Vector3(0.3f, 0f, 3.9f), metal, lampMat);

            // Neon sign on the west wall.
            Neon(root, new Vector3(-W / 2 + 0.06f, 2.65f, 1.6f), "HIDE & SEEK");
            // Settings board on the north wall: a big screen (LobbySettingsBoard draws the display at runtime).
            var boardGo = Box("SettingsBoard", root, new Vector3(3.55f, 1.85f, D / 2 - 0.05f), new Vector3(3.6f, 2.16f, 0.06f), board);
            Box("BoardFrame", root, new Vector3(3.55f, 1.85f, D / 2 - 0.02f), new Vector3(3.8f, 2.36f, 0.04f), trim);
            var sb = boardGo.AddComponent<LobbySettingsBoard>();
            var sbo = new SerializedObject(sb);
            sbo.FindProperty("screenSize").vector2Value = new Vector2(3.5f, 2.06f);
            sbo.ApplyModifiedPropertiesWithoutUndo();

            BuildLobbyDecor(root, darkWood, metal, trim, fabric);

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

            var lobbyAudio = new GameObject("GameSceneAudio").AddComponent<GameSceneAudio>();
            var lao = new SerializedObject(lobbyAudio);
            lao.FindProperty("kind").enumValueIndex = (int)HideAndSeek.Audio.AudioEnvironmentKind.SmallRoom;
            lao.FindProperty("populateEmitters").boolValue = false;
            lao.ApplyModifiedPropertiesWithoutUndo();
            BuildLobbyModels(root);
            ZFightCheck.FixOpenScene(); // no flickering coplanar faces
            EditorSceneManager.SaveScene(scene, LobbyScenePath);
        }

        /// <summary>Depth-tested text material + auto-fit (see WorldText).</summary>
        private static void AddWorldText(TextMesh tm, Vector2 fit)
        {
            string path = $"{LobbyArtDir}/WorldText.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("HideAndSeek/WorldText"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.mainTexture = tm.font.material.mainTexture;
            EditorUtility.SetDirty(mat);
            tm.GetComponent<MeshRenderer>().sharedMaterial = mat;
            var wt = tm.gameObject.AddComponent<WorldText>();
            var so = new SerializedObject(wt);
            so.FindProperty("material").objectReferenceValue = mat;
            so.FindProperty("fitSize").vector2Value = fit;
            so.ApplyModifiedPropertiesWithoutUndo();
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
            SofaCushions(s, fabric,
                Mat("LobbyPillowMustard", new Color(0.8f, 0.6f, 0.15f), Tex("lobby_fabric", TexKind.Fabric), 0.05f, new Vector2(1, 1)),
                Mat("LobbyPillowTeal", new Color(0.12f, 0.45f, 0.45f), Tex("lobby_fabric", TexKind.Fabric), 0.05f, new Vector2(1, 1)));
            // Two seats: root on the floor in front of the cushion, facing away from the backrest.
            for (int i = 0; i < 2; i++)
            {
                var seat = new GameObject($"Seat_{_seatCounter++:00}").transform;
                seat.SetParent(s, false);
                seat.localPosition = new Vector3(i == 0 ? -0.55f : 0.55f, 0f, -0.3f);
                seat.localRotation = Quaternion.Euler(0, 180, 0);
                seat.gameObject.AddComponent<LobbySeat>();
            }
            s.position = pos;
            s.rotation = Quaternion.Euler(0, yaw, 0);
        }

        private static int _seatCounter;

        private static void Pouf(Transform root, Vector3 pos, float yaw, Material fabric)
        {
            var p = new GameObject("Pouf").transform;
            p.SetParent(root, false);
            Cylinder("Body", p, new Vector3(0, 0.21f, 0), new Vector3(0.62f, 0.21f, 0.62f), fabric);
            Cylinder("Top", p, new Vector3(0, 0.43f, 0), new Vector3(0.56f, 0.02f, 0.56f), fabric);
            var seat = new GameObject($"Seat_{_seatCounter++:00}").transform;
            seat.SetParent(p, false);
            seat.localPosition = new Vector3(0f, 0f, 0.14f);
            seat.gameObject.AddComponent<LobbySeat>();
            p.position = pos;
            p.rotation = Quaternion.Euler(0, yaw, 0);
        }

        private static void Plant(Transform root, Vector3 pos, Material pot, Material leaf, int seed)
        {
            var p = new GameObject("Plant").transform;
            p.SetParent(root, false);
            Cylinder("Pot", p, new Vector3(0, 0.25f, 0), new Vector3(0.5f, 0.25f, 0.5f), pot);
            Cylinder("Soil", p, new Vector3(0, 0.49f, 0), new Vector3(0.44f, 0.01f, 0.44f), Mat("LobbySoil", new Color(0.12f, 0.08f, 0.05f), null, 0f, Vector2.one));
            var rnd = new System.Random(seed);
            for (int i = 0; i < 9; i++)
            {
                float a = i / 9f * 360f + (float)rnd.NextDouble() * 25f;
                float h = 0.7f + (float)rnd.NextDouble() * 0.7f;
                var l = Box("Leaf", p, Vector3.zero, new Vector3(0.12f, h, 0.03f), leaf);
                UnityEngine.Object.DestroyImmediate(l.GetComponent<Collider>());
                l.transform.localPosition = new Vector3(0f, 0.5f + h * 0.45f, 0f);
                l.transform.localRotation = Quaternion.Euler(0f, a, 0f) * Quaternion.Euler(18f + (float)rnd.NextDouble() * 18f, 0f, 0f);
                l.transform.localPosition += l.transform.localRotation * new Vector3(0f, 0f, 0.08f);
            }
            p.position = pos;
        }

        private static void Painting(Transform root, Vector3 pos, Vector2 size, string tex, int seed, Material frame)
        {
            var p = new GameObject("Painting").transform;
            p.SetParent(root, false);
            p.position = pos;
            var f = Box("Frame", p, pos + new Vector3(0, 0, 0.015f), new Vector3(size.x + 0.12f, size.y + 0.12f, 0.04f), frame);
            UnityEngine.Object.DestroyImmediate(f.GetComponent<Collider>());
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "Canvas";
            UnityEngine.Object.DestroyImmediate(q.GetComponent<Collider>());
            q.transform.SetParent(p, false);
            q.transform.position = pos - new Vector3(0, 0, 0.01f);
            q.transform.localScale = new Vector3(size.x, size.y, 1f);
            q.GetComponent<MeshRenderer>().sharedMaterial = Mat("Painting_" + tex, Color.white, PaintingTex(tex, seed), 0.1f, Vector2.one);
        }

        private static Texture2D PaintingTex(string name, int seed)
        {
            string path = $"{LobbyArtDir}/{name}.png";
            if (!File.Exists(path))
            {
                const int w = 128, h = 128;
                var t = new Texture2D(w, h, TextureFormat.RGB24, false);
                var px = new Color[w * h];
                var rnd = new System.Random(seed);
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)w, v = y / (float)h;
                    Color c;
                    if (seed == 1)
                    {
                        // Night city skyline with lit windows.
                        Color sky = Color.Lerp(new Color(0.05f, 0.05f, 0.15f), new Color(0.35f, 0.15f, 0.3f), 1f - v);
                        int col = x / 9;
                        float bh = 0.2f + Mathf.PerlinNoise(col * 0.73f, 3.1f) * 0.55f;
                        c = v < bh ? new Color(0.06f, 0.06f, 0.09f) : sky;
                        if (v < bh && x % 9 > 1 && x % 9 < 7 && y % 6 < 3 && Mathf.PerlinNoise(x * 0.5f, y * 0.5f) > 0.55f)
                            c = new Color(1f, 0.82f, 0.45f);
                    }
                    else
                    {
                        // Moon over dark hills.
                        c = Color.Lerp(new Color(0.02f, 0.04f, 0.1f), new Color(0.1f, 0.15f, 0.3f), v);
                        float dm = Vector2.Distance(new Vector2(u, v), new Vector2(0.66f, 0.72f));
                        if (dm < 0.12f) c = Color.Lerp(new Color(0.95f, 0.93f, 0.8f), new Color(0.8f, 0.78f, 0.65f), Mathf.PerlinNoise(u * 20f, v * 20f));
                        else if (dm < 0.2f) c += new Color(0.15f, 0.15f, 0.12f) * (1f - (dm - 0.12f) / 0.08f);
                        float hill = 0.25f + Mathf.Sin(u * 7f + 1f) * 0.06f + Mathf.PerlinNoise(u * 4f, 1f) * 0.1f;
                        if (v < hill) c = new Color(0.02f, 0.03f, 0.03f);
                    }
                    c *= 0.9f + (float)rnd.NextDouble() * 0.12f;
                    px[y * w + x] = c;
                }
                t.SetPixels(px);
                t.Apply();
                File.WriteAllBytes(path, t.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(t);
                AssetDatabase.ImportAsset(path);
                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                imp.filterMode = FilterMode.Point;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void FloorLamp(Transform root, Vector3 pos, Material metal, Material shade)
        {
            var p = new GameObject("FloorLamp").transform;
            p.SetParent(root, false);
            Cylinder("Base", p, new Vector3(0, 0.02f, 0), new Vector3(0.36f, 0.02f, 0.36f), metal);
            Cylinder("Pole", p, new Vector3(0, 0.8f, 0), new Vector3(0.04f, 0.8f, 0.04f), metal);
            var s = Cylinder("Shade", p, new Vector3(0, 1.62f, 0), new Vector3(0.42f, 0.16f, 0.42f), shade);
            UnityEngine.Object.DestroyImmediate(s.GetComponent<Collider>());
            var l = new GameObject("LampLight").AddComponent<Light>();
            l.transform.SetParent(p, false);
            l.transform.localPosition = new Vector3(0, 1.45f, 0);
            l.type = LightType.Point;
            l.color = new Color(1f, 0.72f, 0.45f);
            l.range = 4.5f;
            l.intensity = 0.9f;
            p.position = pos;
        }

        private static void Neon(Transform root, Vector3 pos, string text)
        {
            var t = new GameObject("Neon").AddComponent<TextMesh>();
            t.transform.SetParent(root, false);
            t.transform.position = pos;
            t.transform.rotation = Quaternion.Euler(0, -90, 0);
            t.text = text;
            t.characterSize = 0.06f;
            t.fontSize = 80;
            t.fontStyle = FontStyle.Bold;
            t.anchor = TextAnchor.MiddleCenter;
            t.color = new Color(1f, 0.3f, 0.75f);
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            AddWorldText(t, new Vector2(3.2f, 0.6f));
            var l = new GameObject("NeonGlow").AddComponent<Light>();
            l.transform.SetParent(root, false);
            l.transform.position = pos + new Vector3(0.6f, 0f, 0f);
            l.type = LightType.Point;
            l.color = new Color(1f, 0.25f, 0.7f);
            l.range = 5f;
            l.intensity = 1.1f;
        }

        private static void BuildMirror(Transform root, Vector3 center, Vector2 size, Material frame)
        {
            var p = new GameObject("Mirror").transform;
            p.SetParent(root, false);
            var f = Box("MirrorFrame", p, center + new Vector3(0, 0, -0.01f), new Vector3(size.x + 0.16f, size.y + 0.16f, 0.05f), frame);
            UnityEngine.Object.DestroyImmediate(f.GetComponent<Collider>());
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "MirrorGlass";
            UnityEngine.Object.DestroyImmediate(q.GetComponent<Collider>());
            q.transform.SetParent(p, false);
            q.transform.position = center + new Vector3(0, 0, 0.02f);
            q.transform.rotation = Quaternion.Euler(0, 180, 0); // face into the room (+Z)
            q.transform.localScale = new Vector3(size.x, size.y, 1f);
            string path = $"{LobbyArtDir}/MirrorSurface.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("HideAndSeek/Mirror"));
                AssetDatabase.CreateAsset(m, path);
            }
            q.GetComponent<MeshRenderer>().sharedMaterial = m;
            q.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            q.AddComponent<PlanarMirror>();

            // Make-up mirror lighting: warm bulbs around the frame plus soft lights in front of the glass, so whoever
            // stands in front of it (and therefore the reflection) is lit.
            var bulbMat = Mat("MirrorBulb", new Color(1f, 0.93f, 0.8f), null, 0.9f, Vector2.one);
            bulbMat.EnableKeyword("_EMISSION");
            bulbMat.SetColor("_EmissionColor", new Color(1f, 0.85f, 0.6f) * 2.2f);
            float hx = size.x / 2f + 0.13f, hy = size.y / 2f + 0.13f;
            void Bulb(Vector3 lp)
            {
                var b = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                b.name = "MirrorBulb";
                UnityEngine.Object.DestroyImmediate(b.GetComponent<Collider>());
                b.transform.SetParent(p, false);
                b.transform.position = center + lp + new Vector3(0, 0, 0.05f);
                b.transform.localScale = Vector3.one * 0.075f;
                var r = b.GetComponent<MeshRenderer>();
                r.sharedMaterial = bulbMat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            for (int i = 0; i < 5; i++) Bulb(new Vector3(Mathf.Lerp(-hx + 0.15f, hx - 0.15f, i / 4f), hy, 0));
            for (int i = 0; i < 6; i++)
            {
                float y = Mathf.Lerp(-hy + 0.3f, hy - 0.2f, i / 5f);
                Bulb(new Vector3(-hx, y, 0));
                Bulb(new Vector3(hx, y, 0));
            }
            void PointLight(string n, Vector3 lp, float range, float intensity)
            {
                var l = new GameObject(n).AddComponent<Light>();
                l.transform.SetParent(p, false);
                l.transform.position = center + lp;
                l.type = LightType.Point;
                l.color = new Color(1f, 0.86f, 0.68f);
                l.range = range;
                l.intensity = intensity;
            }
            PointLight("MirrorLightL", new Vector3(-hx, 0.2f, 0.35f), 3.6f, 1.3f);
            PointLight("MirrorLightR", new Vector3(hx, 0.2f, 0.35f), 3.6f, 1.3f);
            // Soft key light from above the mirror onto the area in front of it (where players stand).
            var key = new GameObject("MirrorKeyLight").AddComponent<Light>();
            key.transform.SetParent(p, false);
            key.transform.position = center + new Vector3(0, hy + 0.15f, 0.25f);
            key.transform.rotation = Quaternion.LookRotation(new Vector3(0, -0.55f, 1f));
            key.type = LightType.Spot;
            key.spotAngle = 85f;
            key.range = 6f;
            key.intensity = 1.6f;
            key.color = new Color(1f, 0.92f, 0.82f);
            key.shadows = LightShadows.Soft;
        }

        private static void BuildLever(Transform root, Vector3 wallPos, Material metal, Material trim)
        {
            var r = new GameObject("StartLever").transform;
            r.SetParent(root, false);
            var plateMat = Mat("LeverPlate", new Color(0.12f, 0.12f, 0.13f), null, 0.6f, Vector2.one, 0.6f);
            var hazard = Mat("LeverHazard", new Color(0.9f, 0.72f, 0.1f), null, 0.3f, Vector2.one);
            var knobMat = Mat("LeverKnob", new Color(0.75f, 0.08f, 0.06f), null, 0.7f, Vector2.one);
            var lampMat = Mat("LeverLamp", new Color(1f, 0.2f, 0.15f), null, 0.8f, Vector2.one);
            lampMat.EnableKeyword("_EMISSION");
            lampMat.SetColor("_EmissionColor", new Color(1f, 0.2f, 0.15f));

            // Local space: +Z points into the room.
            void L(GameObject g, Vector3 lp) { g.transform.SetParent(r, false); g.transform.localPosition = lp; }
            var plate = Box("Plate", null, Vector3.zero, new Vector3(0.62f, 0.95f, 0.06f), plateMat); L(plate, new Vector3(0, 1.25f, 0.03f));
            var stripe = Box("Hazard", null, Vector3.zero, new Vector3(0.66f, 0.08f, 0.065f), hazard); L(stripe, new Vector3(0, 0.74f, 0.03f));
            var stripe2 = Box("Hazard2", null, Vector3.zero, new Vector3(0.66f, 0.08f, 0.065f), hazard); L(stripe2, new Vector3(0, 1.76f, 0.03f));
            var slot = Box("Slot", null, Vector3.zero, new Vector3(0.08f, 0.55f, 0.02f), Mat("LeverSlot", Color.black, null, 0f, Vector2.one)); L(slot, new Vector3(0, 1.2f, 0.065f));
            var hub = Cylinder("Hub", r, new Vector3(0, 1.2f, 0.09f), new Vector3(0.16f, 0.04f, 0.16f), metal);
            hub.transform.localRotation = Quaternion.Euler(90, 0, 0);

            var pivot = new GameObject("HandlePivot").transform;
            pivot.SetParent(r, false);
            pivot.localPosition = new Vector3(0, 1.2f, 0.11f);
            var arm = Cylinder("Arm", pivot, new Vector3(0, 0.2f, 0), new Vector3(0.05f, 0.2f, 0.05f), metal);
            UnityEngine.Object.DestroyImmediate(arm.GetComponent<Collider>());
            var knob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            knob.name = "Knob";
            knob.transform.SetParent(pivot, false);
            knob.transform.localPosition = new Vector3(0, 0.42f, 0);
            knob.transform.localScale = Vector3.one * 0.13f;
            knob.GetComponent<MeshRenderer>().sharedMaterial = knobMat;

            var lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lamp.name = "StatusLamp";
            lamp.transform.SetParent(r, false);
            lamp.transform.localPosition = new Vector3(0, 1.98f, 0.08f);
            lamp.transform.localScale = Vector3.one * 0.15f;
            lamp.GetComponent<MeshRenderer>().sharedMaterial = lampMat;
            var glow = new GameObject("Glow").AddComponent<Light>();
            glow.transform.SetParent(r, false);
            glow.transform.localPosition = new Vector3(0, 1.98f, 0.35f);
            glow.type = LightType.Point;
            glow.range = 3f;
            glow.intensity = 1.2f;

            var label = new GameObject("Label").AddComponent<TextMesh>();
            label.transform.SetParent(r, false);
            label.transform.localPosition = new Vector3(0, 0.6f, 0.07f);
            label.transform.localRotation = Quaternion.Euler(0, 180, 0);
            label.text = "START";
            label.characterSize = 0.05f;
            label.fontSize = 64;
            label.fontStyle = FontStyle.Bold;
            label.anchor = TextAnchor.MiddleCenter;
            label.color = new Color(0.95f, 0.85f, 0.4f);
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            AddWorldText(label, new Vector2(0.6f, 0.18f));

            r.position = wallPos;
            r.rotation = Quaternion.Euler(0, -90, 0);
            var lb = r.gameObject.AddComponent<LobbyButton>();
            var so = new SerializedObject(lb);
            so.FindProperty("handle").objectReferenceValue = pivot;
            so.FindProperty("knob").objectReferenceValue = knob.transform;
            so.FindProperty("lamp").objectReferenceValue = lamp.GetComponent<Renderer>();
            so.FindProperty("glow").objectReferenceValue = glow;
            so.FindProperty("upAngle").floatValue = 20f;
            so.FindProperty("downAngle").floatValue = 125f;
            so.ApplyModifiedPropertiesWithoutUndo();
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
            // The primitive's CapsuleCollider turns flat cylinders into balls (a pouf became a sphere you float on):
            // use the real cylinder shape instead.
            var cap = go.GetComponent<CapsuleCollider>();
            if (cap != null) UnityEngine.Object.DestroyImmediate(cap);
            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
            mc.convex = true;
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

        private enum TexKind { Wood, Wallpaper, Carpet, Fabric, Concrete, Tiles, Bricks, Crate }

        private static float Hash01(int v)
        {
            unchecked
            {
                uint h = (uint)v * 2654435761u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                return (h & 0xFFFF) / 65535f;
            }
        }

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
                        case TexKind.Tiles:
                        {
                            // 4x4 square stone tiles, dark grout, per-tile shade, pixel noise (blocky look).
                            int tx = x / 64, ty = y / 64, lx = x % 64, ly = y % 64;
                            float tile = 0.62f + 0.32f * Hash01(tx * 7 + ty * 13 + 3);
                            float px8 = 0.88f + 0.2f * Hash01((x / 8) * 31 + (y / 8) * 57);
                            bool grout = lx < 3 || ly < 3;
                            float crack = Mathf.PerlinNoise(u * 14f + tx, v * 14f + ty) > 0.78f ? 0.75f : 1f;
                            float s = grout ? 0.12f : tile * px8 * crack;
                            c = new Color(0.38f * s, 0.4f * s, 0.45f * s);
                            break;
                        }
                        case TexKind.Bricks:
                        {
                            int row = y / 32;
                            int bx = (x + (row % 2) * 32) / 64;
                            int lx = (x + (row % 2) * 32) % 64, ly = y % 32;
                            bool mortar = lx < 3 || ly < 3;
                            float b = 0.55f + 0.4f * Hash01(bx * 17 + row * 29 + 5);
                            float px8 = 0.85f + 0.25f * Hash01((x / 8) * 13 + (y / 8) * 71);
                            float s = mortar ? 0.1f : b * px8;
                            c = new Color(0.3f * s, 0.27f * s, 0.27f * s);
                            break;
                        }
                        case TexKind.Crate:
                        {
                            // Planks, a dark frame around the edge and an X brace (crate face).
                            float plank = 0.8f + 0.25f * Hash01((y / 32) * 11 + 1);
                            float grain = 0.85f + 0.2f * Mathf.PerlinNoise(u * 4f, v * 60f);
                            bool frame = x < 24 || x > n - 25 || y < 24 || y > n - 25;
                            float d1 = Mathf.Abs(x - y), d2 = Mathf.Abs(x - (n - 1 - y));
                            bool brace = (d1 < 14 || d2 < 14) && !frame;
                            bool seam = y % 32 < 2;
                            float s = (frame || brace ? 0.62f : plank) * grain * (seam && !frame && !brace ? 0.5f : 1f);
                            c = new Color(0.5f * s, 0.33f * s, 0.2f * s);
                            if (frame || brace) c *= 0.85f;
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
