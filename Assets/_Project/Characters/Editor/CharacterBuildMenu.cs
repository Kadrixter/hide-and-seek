using System;
using System.Collections.Generic;
using System.IO;
using HideAndSeek.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace HideAndSeek.Characters.Editor
{
    /// <summary>
    /// Generates every Characters asset (skins, materials, skin library, showcase scene) and renders previews.
    /// Command line:
    ///   Unity.exe -batchmode -quit -projectPath &lt;path&gt; -executeMethod HideAndSeek.Characters.Editor.CharacterBuildMenu.BuildFromCommandLine
    ///   Unity.exe -batchmode -quit -projectPath &lt;path&gt; -executeMethod HideAndSeek.Characters.Editor.CharacterBuildMenu.CapturePreviews
    /// </summary>
    public static partial class CharacterBuildMenu
    {
        public const string Root = "Assets/_Project/Characters";
        public const string SkinsDir = Root + "/Skins";
        public const string MaterialsDir = Root + "/Materials";
        public const string ResourcesDir = Root + "/Resources";
        public const string FacesDir = ResourcesDir + "/Faces";
        public const string ScenesDir = Root + "/Scenes";
        public const string LibraryPath = ResourcesDir + "/SkinLibrary.asset";
        public const string ShowcaseScenePath = ScenesDir + "/CharacterShowcase.unity";
        public const string FacePhotoMaterialPath = MaterialsDir + "/FacePhoto.mat";
        public const string FloorMaterialPath = MaterialsDir + "/StudioFloor.mat";
        public const string FloorTexturePath = MaterialsDir + "/StudioFloor.png";

        static readonly Color StudioBackground = new Color(0.12f, 0.13f, 0.16f);

        // ================================================================== build

        [MenuItem("HideAndSeek/Characters/Build All")]
        public static void BuildAll()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildAssets();
            BuildShowcaseScene();
            Debug.Log("[Characters] Build All finished.");
        }

        public static void BuildFromCommandLine()
        {
            try
            {
                BuildAll();
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        /// <summary>Skins, materials, skin definitions, skin library, studio floor. Idempotent.</summary>
        public static void BuildAssets()
        {
            EnsureFolder(SkinsDir);
            EnsureFolder(MaterialsDir);
            EnsureFolder(FacesDir);
            EnsureFolder(ScenesDir);

            var defs = new List<SkinDefinition>();
            foreach (var palette in SkinTextureGenerator.DefaultPalettes)
            {
                string texPath = $"{SkinsDir}/Skin_{palette.Name}.png";
                var tex = SkinTextureGenerator.Generate(palette);
                WritePngAsset(texPath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                var skinTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                if (skinTex == null) throw new Exception("Failed to import skin texture " + texPath);

                var mat = LoadOrCreateMaterial($"{MaterialsDir}/Skin_{palette.Name}.mat");
                ConfigureSkinMaterial(mat, skinTex);

                string defPath = $"{SkinsDir}/Skin_{palette.Name}.asset";
                var def = AssetDatabase.LoadAssetAtPath<SkinDefinition>(defPath);
                if (def == null)
                {
                    def = ScriptableObject.CreateInstance<SkinDefinition>();
                    AssetDatabase.CreateAsset(def, defPath);
                }

                def.EditorSetup(palette.Name, skinTex, mat, palette.Hoodie);
                EditorUtility.SetDirty(def);
                defs.Add(def);
            }

            var faceMat = LoadOrCreateMaterial(FacePhotoMaterialPath);
            ConfigureOpaqueMaterial(faceMat, null, Color.white, 0.12f);

            var lib = AssetDatabase.LoadAssetAtPath<SkinLibrary>(LibraryPath);
            if (lib == null)
            {
                lib = ScriptableObject.CreateInstance<SkinLibrary>();
                AssetDatabase.CreateAsset(lib, LibraryPath);
            }

            lib.EditorSetup(defs, faceMat);
            EditorUtility.SetDirty(lib);

            // Studio floor with a subtle 1 m grid (helps judging foot sliding in the showcase).
            WritePngAsset(FloorTexturePath, BuildFloorTexture().EncodeToPNG());
            var floorImporter = (TextureImporter)AssetImporter.GetAtPath(FloorTexturePath);
            if (floorImporter.anisoLevel != 8 || floorImporter.wrapMode != TextureWrapMode.Repeat)
            {
                floorImporter.anisoLevel = 8;
                floorImporter.wrapMode = TextureWrapMode.Repeat;
                floorImporter.mipmapEnabled = true;
                floorImporter.filterMode = FilterMode.Trilinear;
                floorImporter.SaveAndReimport();
            }

            var floorMat = LoadOrCreateMaterial(FloorMaterialPath);
            ConfigureOpaqueMaterial(floorMat, AssetDatabase.LoadAssetAtPath<Texture2D>(FloorTexturePath), Color.white, 0.18f);
            floorMat.mainTextureScale = new Vector2(60f, 60f);

            BuildActionAssets();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        static Texture2D BuildFloorTexture()
        {
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                bool line = x < 2 || y < 2;
                bool sub = (x % 32 == 0) || (y % 32 == 0);
                byte v = line ? (byte)92 : sub ? (byte)112 : (byte)120;
                px[y * n + x] = new Color32(v, v, (byte)(v + 4), 255);
            }

            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        // ================================================================== materials

        static Material LoadOrCreateMaterial(string path)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        /// <summary>Standard shader, Cutout mode, matte, no specular highlights (pixel-art friendly).</summary>
        public static void ConfigureSkinMaterial(Material m, Texture2D tex)
        {
            m.shader = Shader.Find("Standard");
            m.SetFloat("_Mode", 1f);
            m.SetOverrideTag("RenderType", "TransparentCutout");
            m.SetInt("_SrcBlend", (int)BlendMode.One);
            m.SetInt("_DstBlend", (int)BlendMode.Zero);
            m.SetInt("_ZWrite", 1);
            m.EnableKeyword("_ALPHATEST_ON");
            m.DisableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = (int)RenderQueue.AlphaTest;
            m.SetFloat("_Cutoff", 0.5f);
            m.mainTexture = tex;
            m.color = Color.white;
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Glossiness", 0.12f);
            m.SetFloat("_SpecularHighlights", 0f);
            m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            m.SetFloat("_GlossyReflections", 0f);
            m.EnableKeyword("_GLOSSYREFLECTIONS_OFF");
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
        }

        static void ConfigureOpaqueMaterial(Material m, Texture2D tex, Color color, float gloss)
        {
            m.shader = Shader.Find("Standard");
            m.SetFloat("_Mode", 0f);
            m.SetOverrideTag("RenderType", "");
            m.SetInt("_SrcBlend", (int)BlendMode.One);
            m.SetInt("_DstBlend", (int)BlendMode.Zero);
            m.SetInt("_ZWrite", 1);
            m.DisableKeyword("_ALPHATEST_ON");
            m.DisableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = -1;
            m.mainTexture = tex;
            m.color = color;
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Glossiness", gloss);
            m.SetFloat("_SpecularHighlights", 0f);
            m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
        }

        // ================================================================== showcase scene

        static readonly DemoMotionMode[] ShowcaseModes =
        {
            DemoMotionMode.Idle, DemoMotionMode.Walk, DemoMotionMode.Run, DemoMotionMode.CrouchWalk, DemoMotionMode.CycleAll
        };

        public static void BuildShowcaseScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildStudio(true);

            var lineup = new GameObject("Characters").transform;
            int count = Mathf.Max(1, SkinLibrary.Count);
            const float spacing = 3f;
            for (int i = 0; i < ShowcaseModes.Length; i++)
            {
                var mode = ShowcaseModes[i];
                var go = new GameObject($"{i}_{mode}");
                go.transform.SetParent(lineup, false);
                go.transform.localPosition = new Vector3((i - (ShowcaseModes.Length - 1) * 0.5f) * spacing, 0f, 0f);
                var demo = go.AddComponent<DemoMotionSource>();
                demo.mode = mode;
                demo.trackLength = 4f;
                demo.trackRadius = 1.1f;
                demo.runSpeed = 4.6f;
                demo.lookPitchAmplitude = mode == DemoMotionMode.CycleAll ? 20f : 0f;
                var spawner = go.AddComponent<CharacterShowcaseSpawner>();
                spawner.skinIndex = i % count;
            }

            AddActionShowcase();

            var camGo = new GameObject("Showcase Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            ConfigureCamera(cam);
            cam.fieldOfView = 45f;
            var orbit = camGo.AddComponent<ShowcaseOrbitCamera>();
            orbit.target = new Vector3(0f, 0.9f, 0f);
            orbit.radius = 13f;
            orbit.height = 3.2f;
            orbit.degreesPerSecond = 7f;
            orbit.heightBob = 0.8f;
            camGo.AddComponent<AudioListener>();
            orbit.SendMessage("OnEnable", SendMessageOptions.DontRequireReceiver);

            // Unity assigns random local fileIDs on every save; keep the previous file when nothing else changed
            // so repeated builds produce no diff.
            string full = Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, ShowcaseScenePath);
            string previous = File.Exists(full) ? File.ReadAllText(full) : null;
            EditorSceneManager.SaveScene(scene, ShowcaseScenePath);
            if (previous != null && NormalizeYaml(previous) == NormalizeYaml(File.ReadAllText(full)))
            {
                File.WriteAllText(full, previous);
                AssetDatabase.ImportAsset(ShowcaseScenePath, ImportAssetOptions.ForceUpdate);
                EditorSceneManager.OpenScene(ShowcaseScenePath, OpenSceneMode.Single);
            }

            AssetDatabase.Refresh();
        }

        static string NormalizeYaml(string yaml)
        {
            yaml = System.Text.RegularExpressions.Regex.Replace(yaml, @"&-?\d+", "&");
            yaml = System.Text.RegularExpressions.Regex.Replace(yaml, @"fileID: -?\d+", "fileID:");
            // Objects are written sorted by fileID, so compare the documents order-independently.
            var docs = new List<string>(yaml.Replace("\r\n", "\n").Split(new[] { "--- !u!" }, StringSplitOptions.None));
            docs.Sort(string.CompareOrdinal);
            return string.Join("\n---\n", docs);
        }

        static void ConfigureCamera(Camera cam)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = StudioBackground;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 200f;
            cam.allowHDR = true;
            cam.allowMSAA = true;
        }

        /// <summary>Floor, three-point lighting, ambient and fog. Used by the showcase and the preview renders.</summary>
        static GameObject BuildStudio(bool saveableScene)
        {
            var studio = new GameObject("Studio");

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.SetParent(studio.transform, false);
            floor.transform.localScale = new Vector3(6f, 1f, 6f);
            Object.DestroyImmediate(floor.GetComponent<Collider>());
            var floorMat = AssetDatabase.LoadAssetAtPath<Material>(FloorMaterialPath);
            if (floorMat != null) floor.GetComponent<MeshRenderer>().sharedMaterial = floorMat;
            floor.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            // Key: warm, front-left, high, soft shadows.
            var key = CreateLight(studio.transform, "Key Light", new Vector3(42f, 145f, 0f), new Color(1f, 0.95f, 0.86f), 1.15f, LightShadows.Soft);
            key.shadowStrength = 0.85f;
            key.shadowNormalBias = 0.25f;
            key.shadowBias = 0.03f;
            // Fill: cool, from the other side, no shadows.
            CreateLight(studio.transform, "Fill Light", new Vector3(18f, -150f, 0f), new Color(0.72f, 0.82f, 1f), 0.5f, LightShadows.None);
            // Side fill from the right so profile views are not black.
            CreateLight(studio.transform, "Side Fill", new Vector3(12f, -95f, 0f), new Color(0.9f, 0.88f, 0.85f), 0.45f, LightShadows.None);
            // Rim: from behind to separate the silhouette from the background.
            CreateLight(studio.transform, "Rim Light", new Vector3(28f, 15f, 0f), new Color(0.9f, 0.95f, 1f), 0.75f, LightShadows.None);

            var ambient = studio.AddComponent<ShowcaseAmbient>();
            ShowcaseAmbient.Apply(ambient.sky, ambient.equator, ambient.ground);

            RenderSettings.skybox = null;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = StudioBackground;
            RenderSettings.fogStartDistance = 18f;
            RenderSettings.fogEndDistance = 45f;
            return studio;
        }

        static Light CreateLight(Transform parent, string name, Vector3 euler, Color color, float intensity, LightShadows shadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localRotation = Quaternion.Euler(euler);
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.color = color;
            l.intensity = intensity;
            l.shadows = shadows;
            return l;
        }

        // ================================================================== previews

        sealed class PreviewMotion : ICharacterMotionSource
        {
            public Vector3 Velocity { get; set; }
            public float HorizontalSpeed { get; set; }
            public bool IsGrounded { get; set; } = true;
            public bool IsCrouching { get; set; }
            public bool IsRunning { get; set; }
            public float CrouchAmount { get; set; }
            public float LookPitch { get; set; }
            public LocomotionState State { get; set; }
        }

        [MenuItem("HideAndSeek/Characters/Capture Previews")]
        public static void CapturePreviews()
        {
            bool ok = false;
            bool prevAsync = ShaderUtil.allowAsyncCompilation;
            try
            {
                if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                BuildAssets(); // idempotent; keeps previews in sync with the generator
                ShaderUtil.allowAsyncCompilation = false;
                CaptureAll();
                ok = true;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = prevAsync;
            }

            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        static string PreviewDir => Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, "docs", "previews", "characters");

        static void CaptureAll()
        {
            Directory.CreateDirectory(PreviewDir);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildStudio(false);

            var camGo = new GameObject("Preview Camera");
            var cam = camGo.AddComponent<Camera>();
            ConfigureCamera(cam);
            cam.enabled = false;

            // ---------------------------------------------------------- turnaround (skin 0)
            {
                var c = Spawn(0, Vector3.zero, out var motion, out var anim);
                Warm(anim, 0.8f);
                var views = new[] { 0f, 35f, 90f, 180f };
                var sheet = new Texture2D(views.Length * 420, 620, TextureFormat.RGBA32, false);
                for (int i = 0; i < views.Length; i++)
                {
                    Aim(cam, views[i], 4.2f, 1.0f, 0.95f, 30f);
                    Blit(sheet, Render(cam, 420, 620), i * 420, 0);
                }

                Save(sheet, "turnaround_front_34_side_back.png");

                // Head close-up.
                var heads = new Texture2D(2 * 520, 520, TextureFormat.RGBA32, false);
                Aim(cam, 0f, 1.35f, 1.6f, 1.58f, 30f);
                Blit(heads, Render(cam, 520, 520), 0, 0);
                Aim(cam, 40f, 1.35f, 1.65f, 1.58f, 30f);
                Blit(heads, Render(cam, 520, 520), 520, 0);
                Save(heads, "head_closeup.png");
                Kill(c);
            }

            // ---------------------------------------------------------- lineup
            {
                int n = Mathf.Max(1, SkinLibrary.Count);
                var list = new List<BlockyCharacter>();
                for (int i = 0; i < n; i++)
                {
                    var ch = Spawn(i, new Vector3((i - (n - 1) * 0.5f) * 1.0f, 0f, 0f), out _, out var a);
                    a.randomSeed = 11 + i * 7;
                    Warm(a, 0.6f + i * 0.37f);
                    list.Add(ch);
                }

                Aim(cam, 18f, 6.2f, 1.25f, 0.9f, 34f);
                var tex = Render(cam, 1600, 760);
                Save(tex, "lineup.png");
                foreach (var ch in list) Kill(ch);
            }

            // ---------------------------------------------------------- gait cycles (side view)
            CaptureCycle(cam, "walk_cycle.png", 2.0f, false, 0f, 1);
            CaptureCycle(cam, "run_cycle.png", 5.0f, true, 0f, 2);
            CaptureCycle(cam, "crouch_walk_cycle.png", 1.2f, false, 1f, 3);

            // ---------------------------------------------------------- poses
            {
                var labels = new List<Texture2D>();
                void Pose(int skin, Action<PreviewMotion> setup, float warm, float yaw)
                {
                    var ch = Spawn(skin, Vector3.zero, out var m, out var a);
                    a.idleLookAround = false;
                    setup(m);
                    Warm(a, warm);
                    Aim(cam, yaw, 4.0f, 1.0f, 0.9f, 30f);
                    labels.Add(Render(cam, 360, 520));
                    Kill(ch);
                }

                Pose(4, m => { }, 1f, 40f); // idle
                Pose(4, m => { m.IsCrouching = true; m.CrouchAmount = 1f; }, 1f, 40f); // crouch idle
                Pose(4, m => { m.CrouchAmount = 1f; m.IsCrouching = true; }, 1f, 90f); // crouch idle side
                Pose(4, m => { m.IsGrounded = false; m.Velocity = new Vector3(0f, 4f, 0f); }, 0.6f, 40f); // jump rising
                Pose(4, m => { m.IsGrounded = false; m.Velocity = new Vector3(0f, -6f, 0f); }, 0.6f, 40f); // falling
                Pose(4, m => { m.LookPitch = 45f; }, 1f, 70f); // look up
                Pose(4, m => { m.LookPitch = -50f; }, 1f, 70f); // look down
                var sheet = new Texture2D(labels.Count * 360, 520, TextureFormat.RGBA32, false);
                for (int i = 0; i < labels.Count; i++) Blit(sheet, labels[i], i * 360, 0);
                Save(sheet, "poses_idle_crouch_crouchside_rise_fall_lookup_lookdown.png");
            }

            // ---------------------------------------------------------- crouch close-up (side + 3/4 + front)
            {
                var ch = Spawn(3, Vector3.zero, out var m, out var a);
                a.idleLookAround = false;
                m.IsCrouching = true;
                m.CrouchAmount = 1f;
                Warm(a, 1f);
                var sheet = new Texture2D(3 * 520, 560, TextureFormat.RGBA32, false);
                float[] yaws = { 90f, 35f, 0f };
                for (int i = 0; i < yaws.Length; i++)
                {
                    Aim(cam, yaws[i], 2.8f, 0.9f, 0.62f, 32f);
                    Blit(sheet, Render(cam, 520, 560), i * 520, 0);
                }

                Save(sheet, "crouch_closeup_side_34_front.png");
                Kill(ch);
            }

            // ---------------------------------------------------------- raw skin textures (x8)
            {
                int n = SkinLibrary.Count;
                const int scale = 8;
                var sheet = new Texture2D(n * (64 * scale + 16), 64 * scale, TextureFormat.RGBA32, false);
                var bg = new Color32[sheet.width * sheet.height];
                for (int i = 0; i < bg.Length; i++) bg[i] = ((i % sheet.width / 16 + i / sheet.width / 16) % 2 == 0) ? new Color32(60, 60, 60, 255) : new Color32(80, 80, 80, 255);
                sheet.SetPixels32(bg);
                for (int s = 0; s < n; s++)
                {
                    var def = SkinLibrary.GetSkin(s);
                    string path = AssetDatabase.GetAssetPath(def.Texture);
                    var raw = new Texture2D(2, 2);
                    raw.LoadImage(File.ReadAllBytes(path));
                    for (int y = 0; y < 64 * scale; y++)
                    for (int x = 0; x < 64 * scale; x++)
                    {
                        var c = raw.GetPixel(x / scale, y / scale);
                        if (c.a < 0.5f) continue;
                        sheet.SetPixel(s * (64 * scale + 16) + x, y, c);
                    }
                }

                sheet.Apply();
                Save(sheet, "skin_textures.png");
            }

            // ---------------------------------------------------------- photo face test (temporary test photo)
            CaptureFaceTest(cam);

            CaptureShowcaseScene();

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Debug.Log("[Characters] Previews written to " + PreviewDir);
        }

        /// <summary>
        /// Opens the saved showcase scene and simulates it in edit mode the way Play mode would run it:
        /// spawners create characters, animators find their DemoMotionSource through the parent chain.
        /// </summary>
        static void CaptureShowcaseScene()
        {
            if (!File.Exists(Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, ShowcaseScenePath))) return;
            EditorSceneManager.OpenScene(ShowcaseScenePath, OpenSceneMode.Single);
            var demos = Object.FindObjectsByType<DemoMotionSource>(FindObjectsInactive.Exclude);
            var anims = new List<BlockyCharacterAnimator>();
            foreach (var demo in demos)
            {
                var spawner = demo.GetComponent<CharacterShowcaseSpawner>();
                var ch = BlockyCharacterFactory.Create(demo.transform, spawner != null ? spawner.skinIndex : 0);
                var a = ch.GetComponent<BlockyCharacterAnimator>();
                a.autoUpdate = false;
                anims.Add(a);
            }

            const float dt = 1f / 60f;
            var sheet = new Texture2D(2 * 960, 540, TextureFormat.RGBA32, false);
            var cam = Object.FindFirstObjectByType<Camera>();
            float[] shots = { 2.4f, 6.1f };
            float t = 0f;
            for (int s = 0; s < shots.Length; s++)
            {
                for (; t < shots[s]; t += dt)
                {
                    foreach (var demo in demos) demo.Step(dt);
                    foreach (var a in anims) a.Tick(dt);
                }

                foreach (var a in anims)
                    if (a.Source == null) throw new Exception("Showcase animator did not find its DemoMotionSource.");
                Blit(sheet, Render(cam, 960, 540), s * 960, 0);
            }

            Save(sheet, "showcase_scene.png");
        }

        static void CaptureCycle(Camera cam, string file, float speed, bool run, float crouch, int skin)
        {
            const int frames = 8, w = 260, h = 380;
            var ch = Spawn(skin, Vector3.zero, out var m, out var anim);
            anim.idleLookAround = false;
            m.HorizontalSpeed = speed;
            m.Velocity = new Vector3(0f, 0f, speed);
            m.IsRunning = run;
            m.CrouchAmount = crouch;
            m.IsCrouching = crouch > 0.5f;
            Warm(anim, 2f);

            var sheet = new Texture2D(frames * w, h, TextureFormat.RGBA32, false);
            float acc = 0f, prev = anim.GaitPhase;
            const float dt = 1f / 240f;
            for (int k = 0; k < frames; k++)
            {
                int guard = 0;
                while (acc < k / (float)frames && guard++ < 10000)
                {
                    anim.Tick(dt);
                    acc += Mathf.Repeat(anim.GaitPhase - prev, 1f);
                    prev = anim.GaitPhase;
                }

                Aim(cam, 90f, 4.2f, 0.95f, 0.85f, 28f);
                Blit(sheet, Render(cam, w, h), k * w, 0);
            }

            Save(sheet, file);
            Kill(ch);
        }

        static void CaptureFaceTest(Camera cam)
        {
            string photoPath = FacesDir + "/opszczymorwformie.png";
            bool created = false;
            try
            {
                if (AssetDatabase.LoadAssetAtPath<Texture2D>(photoPath) == null)
                {
                    WritePngAsset(photoPath, BuildTestPhoto().EncodeToPNG());
                    created = true;
                }

                BlockyCharacter.ResetPhotoCache();
                var ch = Spawn(0, Vector3.zero, out _, out var anim, GameLayers.SpecialFaceNick);
                anim.idleLookAround = false;
                Warm(anim, 0.5f);
                if (!ch.HasPhotoFace) throw new Exception("Photo face was not applied for the special nick.");
                var sheet = new Texture2D(2 * 520, 520, TextureFormat.RGBA32, false);
                Aim(cam, 0f, 1.35f, 1.6f, 1.58f, 30f);
                Blit(sheet, Render(cam, 520, 520), 0, 0);
                Aim(cam, 35f, 3.2f, 1.2f, 1.0f, 30f);
                Blit(sheet, Render(cam, 520, 520), 520, 0);
                Save(sheet, created ? "photo_face_test_placeholder.png" : "photo_face.png");
                Kill(ch);

                // A regular nick must not get the photo.
                var normal = Spawn(0, Vector3.zero, out _, out _, "someone");
                if (normal.HasPhotoFace) throw new Exception("Photo face applied to a regular nick.");
                Kill(normal);
            }
            finally
            {
                if (created) AssetDatabase.DeleteAsset(photoPath);
                BlockyCharacter.ResetPhotoCache();
            }

            // Missing photo must fall back silently.
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(photoPath) == null)
            {
                var fb = Spawn(0, Vector3.zero, out _, out _, GameLayers.SpecialFaceNick);
                if (fb.HasPhotoFace) throw new Exception("Photo face shown although the photo is missing.");
                Kill(fb);
                BlockyCharacter.ResetPhotoCache();
            }
        }

        /// <summary>Placeholder "photo": a smooth gradient portrait with asymmetric markers to verify orientation.</summary>
        static Texture2D BuildTestPhoto()
        {
            const int w = 240, h = 300;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float fx = (x - w * 0.5f) / (w * 0.5f), fy = (y - h * 0.55f) / (h * 0.5f);
                Color c = Color.Lerp(new Color(0.25f, 0.35f, 0.6f), new Color(0.6f, 0.75f, 0.95f), y / (float)h);
                float face = fx * fx / 0.55f + fy * fy / 0.75f;
                if (face < 1f) c = Color.Lerp(new Color(0.96f, 0.78f, 0.65f), new Color(0.85f, 0.62f, 0.5f), face);
                // Eyes.
                if (Sq(fx + 0.3f) + Sq(fy - 0.12f) < 0.008f || Sq(fx - 0.3f) + Sq(fy - 0.12f) < 0.008f) c = new Color(0.1f, 0.1f, 0.15f);
                // Smile.
                float smile = Sq(fx) + Sq(fy + 0.05f);
                if (fy < -0.18f && smile > 0.12f && smile < 0.16f && Mathf.Abs(fx) < 0.3f) c = new Color(0.6f, 0.15f, 0.2f);
                // Orientation marker: red square at the photo's top-left.
                if (x < 30 && y > h - 30) c = Color.red;
                px[y * w + x] = c;
            }

            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        static float Sq(float v) => v * v;

        static BlockyCharacter Spawn(int skin, Vector3 pos, out PreviewMotion motion, out BlockyCharacterAnimator anim, string nick = null)
        {
            var root = new GameObject("PreviewRoot");
            root.transform.position = pos;
            var ch = BlockyCharacterFactory.Create(root.transform, skin, nick);
            anim = ch.GetComponent<BlockyCharacterAnimator>();
            anim.autoUpdate = false;
            anim.randomSeed = 1234 + skin;
            motion = new PreviewMotion();
            anim.SetMotionSource(motion);
            return ch;
        }

        static void Kill(BlockyCharacter ch)
        {
            if (ch != null) Object.DestroyImmediate(ch.transform.parent != null ? ch.transform.parent.gameObject : ch.gameObject);
        }

        static void Warm(BlockyCharacterAnimator anim, float seconds)
        {
            const float dt = 1f / 60f;
            for (float t = 0f; t < seconds; t += dt) anim.Tick(dt);
        }

        /// <summary>Points the camera at the origin from a yaw angle (0 = in front of the character).</summary>
        static void Aim(Camera cam, float yawDeg, float distance, float camHeight, float lookHeight, float fov)
        {
            float a = yawDeg * Mathf.Deg2Rad;
            cam.fieldOfView = fov;
            cam.transform.position = new Vector3(Mathf.Sin(a) * distance, camHeight, Mathf.Cos(a) * distance);
            cam.transform.LookAt(new Vector3(0f, lookHeight, 0f));
        }

        static Texture2D Render(Camera cam, int w, int h)
        {
            var rt = RenderTexture.GetTemporary(new RenderTextureDescriptor(w, h, RenderTextureFormat.ARGB32, 24)
            {
                sRGB = true,
                msaaSamples = 8
            });
            var prevActive = RenderTexture.active;
            cam.targetTexture = rt;
            cam.aspect = w / (float)h;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prevActive;
            cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
            return tex;
        }

        static void Blit(Texture2D sheet, Texture2D tile, int x, int y)
        {
            sheet.SetPixels(x, y, tile.width, tile.height, tile.GetPixels());
            sheet.Apply();
            Object.DestroyImmediate(tile);
        }

        static void Save(Texture2D tex, string file)
        {
            string path = Path.Combine(PreviewDir, file);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Debug.Log("[Characters] Preview: " + path);
        }

        // ================================================================== utils

        static void WritePngAsset(string assetPath, byte[] png)
        {
            string full = Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, assetPath);
            bool changed = !File.Exists(full) || !BytesEqual(File.ReadAllBytes(full), png);
            if (changed) File.WriteAllBytes(full, png);
            if (changed || AssetDatabase.LoadMainAssetAtPath(assetPath) == null)
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }

        static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)!.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
