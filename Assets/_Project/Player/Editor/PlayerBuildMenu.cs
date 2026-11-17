using System;
using System.IO;
using HideAndSeek.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace HideAndSeek.Player.Editor
{
    /// <summary>
    /// Generates all Player module assets (cookie, materials, beam mesh, profile, prefab, test scene)
    /// deterministically and idempotently, and renders preview PNGs.
    /// Batch: -executeMethod HideAndSeek.Player.Editor.PlayerBuildMenu.BuildFromCommandLine
    /// </summary>
    public static class PlayerBuildMenu
    {
        public const string Root = "Assets/_Project/Player";
        public const string GeneratedDir = Root + "/Generated";
        public const string MaterialsDir = Root + "/Materials";
        public const string SettingsDir = Root + "/Settings";
        public const string PrefabsDir = Root + "/Prefabs";
        public const string ScenesDir = Root + "/Scenes";

        public const string CookiePath = GeneratedDir + "/FlashlightCookie.png";
        public const string GridTexturePath = GeneratedDir + "/TestRangeGrid.png";
        public const string BeamMeshPath = GeneratedDir + "/FlashlightBeamMesh.asset";
        public const string BeamMaterialPath = MaterialsDir + "/FlashlightBeam.mat";
        public const string DustMaterialPath = MaterialsDir + "/FlashlightDust.mat";
        public const string ProfilePath = SettingsDir + "/HeadBobProfile.asset";
        public const string PrefabPath = PrefabsDir + "/Player.prefab";
        public const string ScenePath = ScenesDir + "/PlayerTestRange.unity";
        public const string BeamShaderPath = Root + "/Shaders/FlashlightBeam.shader";
        public const string DustShaderPath = Root + "/Shaders/FlashlightDust.shader";
        public const string PreviewDir = "docs/previews/player";

        // Flashlight defaults shared by the prefab & mesh.
        private const float SpotAngle = 58f;
        private const float BeamLength = 13f;
        private const float LightRange = 30f;
        private static readonly Vector3 HoldOffset = new Vector3(0.17f, -0.2f, 0.28f);

        // ------------------------------------------------------------------ entry points

        [MenuItem("HideAndSeek/Player/Build All")]
        public static void BuildAll()
        {
            EnsureFolders();
            var cookie = BuildCookie();
            var beamMesh = BuildBeamMesh();
            var beamMat = BuildMaterial(BeamMaterialPath, BeamShaderPath, m =>
            {
                m.SetColor("_Color", new Color(1f, 0.93f, 0.82f, 1f));
                m.SetFloat("_Intensity", 1f);
                m.SetFloat("_Strength", 0.11f);
                m.SetFloat("_LengthFalloff", 1.6f);
                m.SetFloat("_LensFade", 0.035f);
                m.SetFloat("_EdgePower", 1.7f);
                m.SetFloat("_SoftDepth", 0.9f);
                m.SetFloat("_DepthFadeEnabled", 1f);
                m.SetFloat("_NearFadeStart", 0.3f);
                m.SetFloat("_NearFadeRange", 2.2f);
                m.SetFloat("_NoiseScale", 1.6f);
                m.SetVector("_NoiseSpeed", new Vector4(0.06f, 0.035f, 0.05f, 0f));
                m.SetFloat("_NoiseStrength", 0.35f);
            });
            var dustMat = BuildMaterial(DustMaterialPath, DustShaderPath, m =>
            {
                m.SetColor("_Color", new Color(1f, 0.93f, 0.82f, 1f));
                m.SetFloat("_Intensity", 1f);
                m.SetFloat("_Strength", 1.2f);
                m.SetFloat("_Attenuation", 0.12f);
                m.SetFloat("_NearFade", 0.5f);
            });
            var profile = BuildProfile();
            AssetDatabase.SaveAssets();
            var prefab = BuildPrefab(cookie, beamMesh, beamMat, dustMat, profile);
            BuildTestScene(prefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[PlayerBuild] Build All finished.");
        }

        public static void BuildFromCommandLine()
        {
            try
            {
                BuildAll();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[PlayerBuild] FAILED: " + e);
                EditorApplication.Exit(1);
            }
        }

        // ------------------------------------------------------------------ folders

        private static void EnsureFolders()
        {
            foreach (var dir in new[] { GeneratedDir, MaterialsDir, SettingsDir, PrefabsDir, ScenesDir })
                EnsureFolder(dir);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // ------------------------------------------------------------------ textures

        private static Texture2D BuildCookie()
        {
            var tex = FlashlightProcedural.GenerateCookie(256);
            WritePngIfChanged(CookiePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            var importer = (TextureImporter)AssetImporter.GetAtPath(CookiePath);
            importer.textureType = TextureImporterType.Cookie;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.alphaSource = TextureImporterAlphaSource.FromGrayScale;
            importer.sRGBTexture = false;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 256;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(CookiePath);
        }

        private static Texture2D BuildGridTexture()
        {
            const int size = 512;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            var rng = new System.Random(7);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float v = 0.62f + (float)(rng.NextDouble() - 0.5) * 0.05f;
                // large tiles with thin dark grout + faint sub-grid
                bool major = x % 256 < 3 || y % 256 < 3;
                bool minor = x % 64 < 1 || y % 64 < 1;
                if (major) v *= 0.45f;
                else if (minor) v *= 0.82f;
                // subtle blotches
                v *= 0.93f + 0.07f * Mathf.PerlinNoise(x * 0.012f, y * 0.012f);
                byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);
                px[y * size + x] = new Color32(b, b, b, 255);
            }
            tex.SetPixels32(px);
            tex.Apply();
            WritePngIfChanged(GridTexturePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            var importer = (TextureImporter)AssetImporter.GetAtPath(GridTexturePath);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 4;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(GridTexturePath);
        }

        private static void WritePngIfChanged(string assetPath, byte[] bytes)
        {
            string full = Path.GetFullPath(assetPath);
            if (!File.Exists(full) || !BytesEqual(File.ReadAllBytes(full), bytes))
                File.WriteAllBytes(full, bytes);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        }

        private static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        // ------------------------------------------------------------------ assets

        private static Mesh BuildBeamMesh()
        {
            var mesh = FlashlightProcedural.BuildBeamMesh(SpotAngle, BeamLength);
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(BeamMeshPath);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, BeamMeshPath);
                return mesh;
            }
            existing.Clear();
            EditorUtility.CopySerialized(mesh, existing);
            existing.name = "FlashlightBeamMesh";
            Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static Material BuildMaterial(string path, string shaderPath, Action<Material> setup)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
            if (shader == null) throw new Exception("Shader not found: " + shaderPath);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader) mat.shader = shader;
            setup?.Invoke(mat);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material BuildStandardMaterial(string name, Color color, Texture2D tex, Vector2 tiling, float smoothness, Color emission = default)
        {
            string path = MaterialsDir + "/TestRange_" + name + ".mat";
            var shader = Shader.Find("Standard");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.color = color;
            mat.mainTexture = tex;
            mat.mainTextureScale = tiling;
            mat.SetFloat("_Glossiness", smoothness);
            mat.SetFloat("_Metallic", 0f);
            if (emission.maxColorComponent > 0f)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            else
            {
                mat.DisableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", Color.black);
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static HeadBobProfile BuildProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<HeadBobProfile>(ProfilePath);
            if (profile != null) return profile; // keep designer tuning
            profile = ScriptableObject.CreateInstance<HeadBobProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
            return profile;
        }

        // ------------------------------------------------------------------ prefab

        private static GameObject BuildPrefab(Texture2D cookie, Mesh beamMesh, Material beamMat, Material dustMat, HeadBobProfile profile)
        {
            var root = new GameObject("Player");
            try
            {
                var cc = root.AddComponent<CharacterController>();
                cc.height = 1.8f;
                cc.radius = 0.3f;
                cc.center = new Vector3(0f, 0.9f, 0f);
                cc.stepOffset = 0.4f;
                cc.slopeLimit = 46f;
                cc.skinWidth = 0.04f;
                cc.minMoveDistance = 0f;

                var reader = root.AddComponent<PlayerInputReader>();
                var motor = root.AddComponent<PlayerMotor>();
                SetRef(motor, "input", reader);

                var modelRoot = new GameObject("CharacterModelRoot");
                modelRoot.transform.SetParent(root.transform, false);

                var pivot = new GameObject("CameraPivot");
                pivot.transform.SetParent(root.transform, false);
                pivot.transform.localPosition = new Vector3(0f, 1.62f, 0f);

                var camGo = new GameObject("PlayerCamera");
                camGo.tag = "MainCamera";
                camGo.transform.SetParent(pivot.transform, false);
                var cam = camGo.AddComponent<Camera>();
                cam.fieldOfView = 70f;
                cam.nearClipPlane = 0.03f;
                cam.farClipPlane = 250f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                cam.allowHDR = true;
                cam.allowMSAA = true;
                camGo.AddComponent<AudioListener>();

                var headBob = pivot.AddComponent<HeadBob>();
                SetRef(headBob, "motor", motor);
                SetRef(headBob, "targetCamera", cam);
                SetRef(headBob, "profile", profile);

                // ---- flashlight
                var flGo = new GameObject("Flashlight");
                flGo.transform.SetParent(camGo.transform, false);
                flGo.transform.localPosition = HoldOffset;
                flGo.transform.localRotation = AimRotation(HoldOffset, 9f);

                var light = flGo.AddComponent<Light>();
                light.type = LightType.Spot;
                light.range = LightRange;
                light.spotAngle = SpotAngle;
                light.innerSpotAngle = SpotAngle * 0.4f;
                light.intensity = 3.8f;
                light.color = new Color(1f, 0.93f, 0.82f);
                light.cookie = cookie;
                light.shadows = LightShadows.Soft;
                light.shadowStrength = 0.95f;
                light.shadowResolution = LightShadowResolution.High;
                light.shadowBias = 0.02f;
                light.shadowNormalBias = 0.25f;
                light.shadowNearPlane = 0.1f;
                light.renderMode = LightRenderMode.ForcePixel;
                light.lightmapBakeType = LightmapBakeType.Realtime;
                light.bounceIntensity = 0f;
                light.enabled = false;

                var audio = flGo.AddComponent<AudioSource>();
                audio.playOnAwake = false;
                audio.spatialBlend = 0.7f;
                audio.minDistance = 0.6f;
                audio.maxDistance = 14f;
                audio.rolloffMode = AudioRolloffMode.Logarithmic;
                audio.dopplerLevel = 0f;

                var beamGo = new GameObject("Beam");
                beamGo.transform.SetParent(flGo.transform, false);
                beamGo.AddComponent<MeshFilter>().sharedMesh = beamMesh;
                var beamRenderer = beamGo.AddComponent<MeshRenderer>();
                beamRenderer.sharedMaterial = beamMat;
                ConfigureFxRenderer(beamRenderer);
                beamRenderer.enabled = false;

                var dustGo = new GameObject("DustMotes");
                dustGo.transform.SetParent(flGo.transform, false);
                var ps = dustGo.AddComponent<ParticleSystem>();
                ConfigureDust(ps, dustMat);

                var fl = flGo.AddComponent<Flashlight>();
                SetRef(fl, "spotLight", light);
                SetRef(fl, "beamRenderer", beamRenderer);
                SetRef(fl, "dust", ps);
                SetRef(fl, "audioSource", audio);
                SetRef(fl, "input", reader);
                SetRef(fl, "anchor", camGo.transform);
                SetRef(fl, "body", root.transform);

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool ok);
                if (!ok || prefab == null) throw new Exception("Failed to save prefab " + PrefabPath);
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Quaternion AimRotation(Vector3 localPos, float aimDistance)
        {
            Vector3 dir = new Vector3(0f, 0f, aimDistance) - localPos;
            return Quaternion.LookRotation(dir, Vector3.up);
        }

        private static void ConfigureFxRenderer(Renderer r)
        {
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            r.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            r.allowOcclusionWhenDynamic = false;
        }

        private static void ConfigureDust(ParticleSystem ps, Material mat)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 10f;
            main.loop = true;
            main.prewarm = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 11f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.03f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.007f, 0.02f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 0.25f), new Color(1f, 1f, 1f, 1f));
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.maxParticles = 700;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 75f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.position = new Vector3(0f, 0f, 3.6f);
            shape.scale = new Vector3(3.6f, 3.0f, 6.8f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.05f;
            noise.frequency = 0.35f;
            noise.scrollSpeed = 0.08f;
            noise.quality = ParticleSystemNoiseQuality.Medium;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);

            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sharedMaterial = mat;
            r.minParticleSize = 0f;
            r.maxParticleSize = 0.05f;
            r.sortMode = ParticleSystemSortMode.None;
            ConfigureFxRenderer(r);
        }

        private static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null) throw new Exception($"Field '{field}' not found on {target.GetType().Name}");
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBool(Object target, string field, bool value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null) throw new Exception($"Field '{field}' not found on {target.GetType().Name}");
            prop.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ test scene

        private struct RangeMats
        {
            public Material floor, wall, ceiling, concrete, crate, red, blue, white, yellow;
        }

        private static void BuildTestScene(GameObject prefab)
        {
            var grid = BuildGridTexture();
            var m = new RangeMats
            {
                floor = BuildStandardMaterial("Floor", new Color(0.55f, 0.55f, 0.57f), grid, new Vector2(13f, 17f), 0.25f),
                wall = BuildStandardMaterial("Wall", new Color(0.62f, 0.6f, 0.56f), grid, new Vector2(8f, 2f), 0.1f),
                ceiling = BuildStandardMaterial("Ceiling", new Color(0.3f, 0.3f, 0.32f), grid, new Vector2(8f, 8f), 0.05f),
                concrete = BuildStandardMaterial("Concrete", new Color(0.7f, 0.7f, 0.7f), grid, new Vector2(1f, 1f), 0.15f),
                crate = BuildStandardMaterial("Crate", new Color(0.62f, 0.45f, 0.28f), grid, new Vector2(1f, 1f), 0.2f),
                red = BuildStandardMaterial("Red", new Color(0.7f, 0.12f, 0.1f), null, Vector2.one, 0.45f),
                blue = BuildStandardMaterial("Blue", new Color(0.12f, 0.25f, 0.65f), null, Vector2.one, 0.45f),
                white = BuildStandardMaterial("White", new Color(0.92f, 0.92f, 0.9f), null, Vector2.one, 0.6f),
                yellow = BuildStandardMaterial("HazardYellow", new Color(0.85f, 0.7f, 0.08f), grid, new Vector2(1f, 1f), 0.3f),
            };
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.008f, 0.008f, 0.011f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.skybox = null;
            RenderSettings.sun = null;
            RenderSettings.fog = false;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = null;
            RenderSettings.reflectionIntensity = 0f;

            var env = new GameObject("Environment").transform;

            // Room shell: 26 x 34 m, 6 m high
            Box("Floor", new Vector3(0f, -0.25f, 0f), new Vector3(26f, 0.5f, 34f), m.floor, env);
            Box("Ceiling", new Vector3(0f, 6.25f, 0f), new Vector3(26f, 0.5f, 34f), m.ceiling, env);
            Box("Wall_N", new Vector3(0f, 3f, 17.25f), new Vector3(26f, 6f, 0.5f), m.wall, env);
            Box("Wall_S", new Vector3(0f, 3f, -17.25f), new Vector3(26f, 6f, 0.5f), m.wall, env);
            Box("Wall_E", new Vector3(13.25f, 3f, 0f), new Vector3(0.5f, 6f, 34f), m.wall, env);
            Box("Wall_W", new Vector3(-13.25f, 3f, 0f), new Vector3(0.5f, 6f, 34f), m.wall, env);

            // Stairs (west): 10 steps, 0.2 m rise, 0.32 m run, up to a 2 m platform.
            var stairs = new GameObject("Stairs").transform;
            stairs.SetParent(env, false);
            for (int i = 0; i < 10; i++)
            {
                float top = 0.2f * (i + 1);
                float z = -8f + 0.32f * i + 0.16f;
                Box("Step_" + i, new Vector3(-10f, top * 0.5f, z), new Vector3(3f, top, 0.32f), m.concrete, stairs);
            }
            Box("StairPlatform", new Vector3(-10f, 1f, -1.1f), new Vector3(3f, 2f, 7.4f), m.concrete, stairs);
            Box("StairRail", new Vector3(-8.45f, 2.5f, -1.1f), new Vector3(0.1f, 1f, 7.4f), m.yellow, stairs);
            // Steps down on the other side with a smaller rise (0.15) to test descending snap.
            for (int i = 0; i < 13; i++)
            {
                float top = 2f - 0.15f * (i + 1);
                float z = 2.6f + 0.3f * i + 0.15f;
                Box("StepDown_" + i, new Vector3(-10f, top * 0.5f, z), new Vector3(3f, top, 0.3f), m.concrete, stairs);
            }

            // Ramps (east): 18 degree to a platform, plus an unwalkable 55 degree wedge.
            var ramps = new GameObject("Ramps").transform;
            ramps.SetParent(env, false);
            Ramp("Ramp18", new Vector3(10f, 0f, -9f), 3f, 2f, 18f, m.concrete, ramps);
            float rampRun = 2f / Mathf.Tan(18f * Mathf.Deg2Rad);
            Box("RampPlatform", new Vector3(10f, 1f, -9f + rampRun + 2f), new Vector3(3f, 2f, 4f), m.concrete, ramps);
            Ramp("Ramp30", new Vector3(10f, 0f, -9f + rampRun + 4f + 0f), 3f, 2f, -30f, m.yellow, ramps);
            Ramp("SteepWedge55", new Vector3(5.5f, 0f, 10f), 2.5f, 2.5f, 55f, m.red, ramps);

            // Crawl tunnel (north): inner 1.4 m wide x 1.3 m high x 5 m long.
            var tunnel = new GameObject("CrawlTunnel").transform;
            tunnel.SetParent(env, false);
            Box("Tunnel_L", new Vector3(-1.1f, 1.25f, 10.5f), new Vector3(0.8f, 2.5f, 5f), m.concrete, tunnel);
            Box("Tunnel_R", new Vector3(1.1f, 1.25f, 10.5f), new Vector3(0.8f, 2.5f, 5f), m.concrete, tunnel);
            Box("Tunnel_Roof", new Vector3(0f, 1.9f, 10.5f), new Vector3(3f, 1.2f, 5f), m.concrete, tunnel);
            Box("Tunnel_Sign", new Vector3(0f, 1.6f, 7.98f), new Vector3(1.4f, 0.12f, 0.02f), m.yellow, tunnel);
            // Low overhang beam (1.5 m) - must crouch under it.
            Box("LowBeam", new Vector3(-4.5f, 1.75f, 11f), new Vector3(3f, 0.5f, 0.6f), m.yellow, tunnel);
            Box("LowBeam_PostL", new Vector3(-6.1f, 1f, 11f), new Vector3(0.2f, 2f, 0.6f), m.concrete, tunnel);
            Box("LowBeam_PostR", new Vector3(-2.9f, 1f, 11f), new Vector3(0.2f, 2f, 0.6f), m.concrete, tunnel);

            // Columns & props to light up.
            var props = new GameObject("Props").transform;
            props.SetParent(env, false);
            Vector3[] columns = { new Vector3(-4f, 3f, -4f), new Vector3(4f, 3f, -4f), new Vector3(-4f, 3f, 4f), new Vector3(4f, 3f, 4f) };
            for (int i = 0; i < columns.Length; i++)
                Box("Column_" + i, columns[i], new Vector3(0.7f, 6f, 0.7f), i % 2 == 0 ? m.concrete : m.white, props);

            Box("Crate_A", new Vector3(1.2f, 0.4f, -6f), new Vector3(0.8f, 0.8f, 0.8f), m.crate, props);
            Box("Crate_B", new Vector3(2.0f, 0.5f, -5.6f), new Vector3(1f, 1f, 1f), m.crate, props, 17f);
            Box("Crate_C", new Vector3(1.7f, 1.3f, -5.8f), new Vector3(0.6f, 0.6f, 0.6f), m.crate, props, -9f);
            Box("Crate_D", new Vector3(-1.8f, 0.25f, -2f), new Vector3(1.2f, 0.5f, 0.8f), m.blue, props, 5f);
            Box("Crate_E", new Vector3(-2.2f, 0.6f, 1.5f), new Vector3(1.2f, 1.2f, 1.2f), m.red, props, -22f);
            Box("JumpBox_05", new Vector3(5.5f, 0.25f, -1f), new Vector3(1.5f, 0.5f, 1.5f), m.yellow, props);
            Box("JumpBox_10", new Vector3(5.5f, 0.5f, 1.2f), new Vector3(1.5f, 1f, 1.5f), m.yellow, props);
            Box("Barrel_Like", new Vector3(-0.8f, 0.45f, -8.5f), new Vector3(0.6f, 0.9f, 0.6f), m.white, props, 45f);
            Box("Shelf", new Vector3(-12.6f, 1.2f, 9f), new Vector3(0.8f, 2.4f, 4f), m.crate, props);
            Box("Doorframe_L", new Vector3(3.1f, 1.1f, 14f), new Vector3(0.3f, 2.2f, 0.3f), m.white, props);
            Box("Doorframe_R", new Vector3(4.9f, 1.1f, 14f), new Vector3(0.3f, 2.2f, 0.3f), m.white, props);
            Box("Doorframe_Top", new Vector3(4f, 2.35f, 14f), new Vector3(2.1f, 0.3f, 0.3f), m.white, props);

            // Player
            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            player.transform.SetPositionAndRotation(new Vector3(0f, 0f, -13f), Quaternion.identity);
            var fl = player.GetComponentInChildren<Flashlight>(true);
            SetBool(fl, "startOn", true);

            var debug = new GameObject("PlayerDebugHud");
            var hud = debug.AddComponent<PlayerDebugHud>();
            SetRef(hud, "motor", player.GetComponent<PlayerMotor>());
            SetRef(hud, "flashlight", fl);

            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new Exception("Failed to save scene " + ScenePath);
        }

        private static GameObject Box(string name, Vector3 center, Vector3 size, Material mat, Transform parent, float yaw = 0f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            return go;
        }

        /// <summary>
        /// Ramp whose low edge is at <paramref name="start"/> (floor level) and rises <paramref name="height"/> along +Z
        /// (or along -Z from the far end when angle is negative: the high end is at start).
        /// </summary>
        private static void Ramp(string name, Vector3 start, float width, float height, float angle, Material mat, Transform parent)
        {
            const float thickness = 0.3f;
            bool descending = angle < 0f;
            float a = Mathf.Abs(angle) * Mathf.Deg2Rad;
            float run = height / Mathf.Tan(a);
            float length = height / Mathf.Sin(a);
            var rot = Quaternion.Euler(descending ? Mathf.Abs(angle) : -Mathf.Abs(angle), 0f, 0f);
            Vector3 topCenter = new Vector3(start.x, height * 0.5f, start.z + run * 0.5f);
            Vector3 up = rot * Vector3.up;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = topCenter - up * (thickness * 0.5f);
            go.transform.localRotation = rot;
            go.transform.localScale = new Vector3(width, thickness, length);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;

            // Solid fill under the ramp so it does not look like a floating plank.
            var fill = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fill.name = name + "_Fill";
            fill.transform.SetParent(parent, false);
            float fillH = height * 0.5f;
            fill.transform.localPosition = new Vector3(start.x, fillH * 0.5f, descending ? start.z + run * 0.25f : start.z + run * 0.75f);
            fill.transform.localScale = new Vector3(width * 0.98f, fillH, run * 0.5f - 0.05f);
            fill.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        // ------------------------------------------------------------------ previews

        [MenuItem("HideAndSeek/Player/Capture Previews")]
        public static void CapturePreviews()
        {
            int code = 0;
            try
            {
                if (!File.Exists(Path.GetFullPath(ScenePath))) BuildAll();
                var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                var motor = Object.FindAnyObjectByType<PlayerMotor>();
                if (motor == null) throw new Exception("No PlayerMotor in test scene");
                var fl = motor.GetComponentInChildren<Flashlight>(true);
                var cam = motor.GetComponentInChildren<Camera>(true);
                var pivot = cam.transform.parent;
                Directory.CreateDirectory(Path.GetFullPath(PreviewDir));

                bool depth = !Application.isBatchMode || SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;

                // 1) First person: looking ahead at crates / columns.
                motor.transform.SetPositionAndRotation(new Vector3(0f, 0f, -12.5f), Quaternion.Euler(0f, 4f, 0f));
                pivot.localPosition = new Vector3(0f, 1.62f, 0f);
                pivot.localRotation = Quaternion.Euler(9f, 0f, 0f);
                PoseFlashlight(fl, depth);
                RenderTo(cam, PreviewDir + "/player_fp_flashlight.png", depth);

                // 2) First person: toward the stairs.
                motor.transform.SetPositionAndRotation(new Vector3(-4f, 0f, -12f), Quaternion.Euler(0f, -32f, 0f));
                pivot.localRotation = Quaternion.Euler(4f, 0f, 0f);
                PoseFlashlight(fl, depth);
                RenderTo(cam, PreviewDir + "/player_fp_stairs.png", depth);

                // 3) Crouched in front of the crawl tunnel.
                motor.transform.SetPositionAndRotation(new Vector3(0.3f, 0f, 4.5f), Quaternion.Euler(0f, -3f, 0f));
                pivot.localPosition = new Vector3(0f, 0.96f, 0f);
                pivot.localRotation = Quaternion.Euler(2f, 0f, 0f);
                PoseFlashlight(fl, depth);
                RenderTo(cam, PreviewDir + "/player_fp_tunnel_crouch.png", depth);

                // 4) Third person: side view of the player sweeping the beam across the room.
                motor.transform.SetPositionAndRotation(new Vector3(0f, 0f, -12.5f), Quaternion.Euler(0f, 18f, 0f));
                pivot.localPosition = new Vector3(0f, 1.62f, 0f);
                pivot.localRotation = Quaternion.Euler(10f, 0f, 0f);
                PoseFlashlight(fl, depth);
                var tpGo = new GameObject("PreviewThirdPerson");
                var tp = tpGo.AddComponent<Camera>();
                tp.fieldOfView = 62f;
                tp.nearClipPlane = 0.05f;
                tp.farClipPlane = 200f;
                tp.clearFlags = CameraClearFlags.SolidColor;
                tp.backgroundColor = Color.black;
                tp.allowHDR = true;
                tpGo.transform.position = new Vector3(-6.5f, 2.6f, -13.8f);
                tpGo.transform.LookAt(new Vector3(0.5f, 1.0f, -8.5f));
                cam.enabled = false;
                RenderTo(tp, PreviewDir + "/player_tp_beam.png", depth);


                // 5) Third person: from the front-left, looking back along the beam.
                tpGo.transform.position = new Vector3(-3.6f, 1.9f, -6.2f);
                tpGo.transform.LookAt(new Vector3(0.6f, 1.1f, -10.5f));
                RenderTo(tp, PreviewDir + "/player_tp_beam_front.png", depth);
                Object.DestroyImmediate(tpGo);

                // Don't save the posed scene.
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                Debug.Log("[PlayerBuild] Previews written to " + Path.GetFullPath(PreviewDir));
            }
            catch (Exception e)
            {
                Debug.LogError("[PlayerBuild] Capture FAILED: " + e);
                code = 1;
            }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        private static void PoseFlashlight(Flashlight fl, bool depth)
        {
            fl.transform.localPosition = HoldOffset;
            fl.transform.localRotation = AimRotation(HoldOffset, 9f);
            fl.SetBeamDepthFade(depth);
            fl.ApplyVisuals(1f);
            var ps = fl.GetComponentInChildren<ParticleSystem>(true);
            if (ps != null)
            {
                ps.Simulate(9f, true, true, true);
                fl.ApplyVisuals(1f);
            }
        }

        private static void RenderTo(Camera cam, string assetRelativePath, bool depth)
        {
            const int w = 1280, h = 720;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGBHalf) { antiAliasing = 4 };
            var ldr = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var prevTarget = cam.targetTexture;
            var prevMode = cam.depthTextureMode;
            try
            {
                cam.depthTextureMode = depth ? DepthTextureMode.Depth : DepthTextureMode.None;
                cam.targetTexture = rt;
                cam.Render();
                Graphics.Blit(rt, ldr);
                var prevActive = RenderTexture.active;
                RenderTexture.active = ldr;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                RenderTexture.active = prevActive == rt || prevActive == ldr ? null : prevActive;
                File.WriteAllBytes(Path.GetFullPath(assetRelativePath), tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }
            finally
            {
                if (RenderTexture.active == rt || RenderTexture.active == ldr) RenderTexture.active = null;
                cam.targetTexture = prevTarget;
                cam.depthTextureMode = prevMode;
                rt.Release();
                ldr.Release();
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(ldr);
            }
        }
    }
}
