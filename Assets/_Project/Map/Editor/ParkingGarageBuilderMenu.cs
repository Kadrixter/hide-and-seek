using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace HideAndSeek.Map.Editor
{
    /// <summary>Editor entry points: build the parking garage scene and render preview screenshots.</summary>
    public static class ParkingGarageBuilderMenu
    {
        public const string MapFolder = "Assets/_Project/Map";
        public const string ScenePath = MapFolder + "/Scenes/ParkingGarage.unity";
        public const string MeshAssetPath = MapFolder + "/Meshes/ParkingGarageMeshes.asset";
        public const string LightingPath = MapFolder + "/Scenes/ParkingGarageLighting.lighting";
        public const string PreviewFolder = "docs/previews/map";

        [MenuItem("HideAndSeek/Map/Build Parking Garage")]
        public static void BuildMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        [MenuItem("HideAndSeek/Map/Capture Parking Garage Previews")]
        public static void CapturePreviewsMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            CapturePreviewsInternal();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        /// <summary>Batch-mode entry: regenerates textures, materials, meshes and saves the scene.</summary>
        public static void BuildFromCommandLine()
        {
            try
            {
                Build();
                Debug.Log("[ParkingGarage] Build finished OK");
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>Batch-mode entry (run without -nographics): renders preview PNGs to docs/previews/map.</summary>
        public static void CapturePreviews()
        {
            try
            {
                CapturePreviewsInternal();
                Debug.Log("[ParkingGarage] Previews finished OK");
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        // ================================================================== build

        public static void Build()
        {
            foreach (var dir in new[] { "Scenes", "Meshes", "Materials", "Textures" })
                Directory.CreateDirectory(GarageTextures.FullPath($"{MapFolder}/{dir}"));
            AssetDatabase.Refresh();

            GarageTextures.GenerateAll();
            var lib = MaterialLibrary.CreateAll();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(MeshAssetPath);

            var meshes = new List<Mesh>();
            var builder = new ParkingGarageBuilder(lib, meshes);
            builder.Build();

            SaveMeshes(meshes);
            ApplyLighting();
            Validate();

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Failed to save scene " + ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void SaveMeshes(List<Mesh> meshes)
        {
            if (meshes.Count == 0) return;
            AssetDatabase.CreateAsset(meshes[0], MeshAssetPath);
            for (int i = 1; i < meshes.Count; i++)
                AssetDatabase.AddObjectToAsset(meshes[i], meshes[0]);
            AssetDatabase.SaveAssets();
        }

        private static void ApplyLighting()
        {
            RenderSettings.skybox = null;
            RenderSettings.sun = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.005f, 0.005f, 0.007f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = null;
            RenderSettings.reflectionIntensity = 0f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.045f;
            RenderSettings.fogColor = new Color(0.004f, 0.004f, 0.005f);

            var ls = AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingPath);
            bool create = ls == null;
            if (create) ls = new LightingSettings { name = "ParkingGarageLighting" };
            ls.bakedGI = false;
            ls.realtimeGI = false;
            if (create) AssetDatabase.CreateAsset(ls, LightingPath);
            else EditorUtility.SetDirty(ls);
            Lightmapping.lightingSettings = ls;
        }

        // ================================================================== validation

        /// <summary>
        /// Physics sanity checks: walkable surfaces along stairs and ramps, and every spawn point
        /// standing on ground with a free capsule above it.
        /// </summary>
        private static void Validate()
        {
            Physics.SyncTransforms();
            var errors = new List<string>();

            float GroundAt(float x, float yFrom, float z)
            {
                return Physics.Raycast(new Vector3(x, yFrom, z), Vector3.down, out var hit, 6f) ? hit.point.y : float.NaN;
            }

            // stairwells: the surface must descend monotonically from 0 to -3.4 without big steps
            foreach (float x in new[] { -37f, 14f })
            {
                float prev = 0.05f;
                for (float z = 21.5f; z <= 29.8f; z += 0.1f)
                {
                    float y = GroundAt(x, 2.5f, z);
                    if (float.IsNaN(y)) { errors.Add($"Stair x={x} z={z:0.0}: no ground"); break; }
                    if (y > prev + 0.02f || prev - y > 0.12f) errors.Add($"Stair x={x} z={z:0.0}: jump {prev:0.00}->{y:0.00}");
                    prev = y;
                }
                if (prev > -3.3f) errors.Add($"Stair x={x}: bottom at {prev:0.00}");
            }
            // ramps
            foreach (var (x, z0, z1, zTop) in new[] { (-42f, -24f, 3f, -22f), (42f, 24f, -3f, 22f) })
            {
                float prev = 0.05f;
                int steps = 54;
                for (int i = 0; i <= steps; i++)
                {
                    float z = Mathf.Lerp(z0, z1, i / (float)steps);
                    float expected = -3.4f * Mathf.Clamp01((z - zTop) / (0f - zTop));
                    float y = GroundAt(x, expected + 1.2f, z);
                    if (float.IsNaN(y)) { errors.Add($"Ramp x={x} z={z:0.0}: no ground"); break; }
                    if (y > prev + 0.02f || prev - y > 0.2f) errors.Add($"Ramp x={x} z={z:0.0}: jump {prev:0.00}->{y:0.00}");
                    prev = y;
                }
                if (prev > -3.3f) errors.Add($"Ramp x={x}: bottom at {prev:0.00}");
            }
            // spawn points
            var root = GameObject.Find("ParkingGarage");
            var spawns = root.transform.Find(HideAndSeek.Core.GameLayers.SpawnPointsRoot);
            int tagged = 0;
            foreach (Transform t in spawns)
            {
                if (t.CompareTag(HideAndSeek.Core.GameLayers.SpawnPointTag)) tagged++;
                var p = t.position;
                float g = GroundAt(p.x, p.y + 1f, p.z);
                if (float.IsNaN(g) || Mathf.Abs(g - (p.y - 0.05f)) > 0.1f) errors.Add($"Spawn {t.name}: ground at {g:0.00}");
                if (Physics.CheckCapsule(p + Vector3.up * 0.45f, p + Vector3.up * 1.55f, 0.4f))
                    errors.Add($"Spawn {t.name}: capsule blocked");
            }
            if (tagged < 12) errors.Add($"Only {tagged} tagged spawn points");
            if (spawns.Find("SeekerSpawn") == null) errors.Add("SeekerSpawn missing");

            int lights = Object.FindObjectsByType<Light>().Length;
            int cars = root.transform.Find("Cars").GetComponentsInChildren<MeshRenderer>().Length;
            Debug.Log($"[ParkingGarage] Validation: {tagged} spawn points, {cars} cars, {lights} realtime lights");
            if (errors.Count > 0)
                throw new InvalidOperationException("Garage validation failed:\n" + string.Join("\n", errors));
        }

        // ================================================================== previews

        private struct Shot
        {
            public string Name;
            public Vector3 Pos, Target;
            public float Fov;
            public Func<Renderer, bool> Hide;
        }

        private static void CapturePreviewsInternal()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            string outDir = GarageTextures.FullPath(PreviewFolder);
            Directory.CreateDirectory(outDir);

            var camGo = new GameObject("PreviewCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 300f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.allowHDR = false;

            var flashGo = new GameObject("Flashlight");
            flashGo.transform.SetParent(camGo.transform, false);
            flashGo.transform.localPosition = new Vector3(0.25f, -0.25f, 0f);
            var flash = flashGo.AddComponent<Light>();
            flash.type = LightType.Spot;
            flash.spotAngle = 60f;
            flash.innerSpotAngle = 25f;
            flash.range = 30f;
            flash.intensity = 3.2f;
            flash.color = new Color(1f, 0.95f, 0.85f);
            flash.shadows = LightShadows.Soft;

            // --- true darkness (scene lighting as saved)
            Render(cam, outDir, new Shot { Name = "dark_flashlight_L1", Pos = new V(-24f, 1.6f, -5.25f), Target = new V(-10f, 0.7f, -9.5f), Fov = 65f });
            Render(cam, outDir, new Shot { Name = "dark_flashlight_L2", Pos = new V(4f, -1.8f, -21.75f), Target = new V(14f, -2.7f, -16f), Fov = 65f });
            Render(cam, outDir, new Shot { Name = "dark_flashlight_cages", Pos = new V(-25f, 1.6f, 21.5f), Target = new V(-27f, 0.8f, 28f), Fov = 65f });
            Render(cam, outDir, new Shot { Name = "dark_flashlight_puddle_L2", Pos = new V(-42f, -1.8f, 9f), Target = new V(-42f, -3.4f, 2.5f), Fov = 65f });
            flash.enabled = false;
            Render(cam, outDir, new Shot { Name = "dark_ambient_L1", Pos = new V(-1f, 1.6f, -5.25f), Target = new V(-30f, 1.6f, -5.25f), Fov = 65f });
            Render(cam, outDir, new Shot { Name = "dark_ambient_stairs_L2", Pos = new V(-20f, -1.8f, 12f), Target = new V(-36f, -1.6f, 21f), Fov = 65f });

            // --- bright inspection rig
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.42f, 0.45f);
            var sunGo = new GameObject("PreviewSun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 0.75f;
            sun.shadows = LightShadows.None;
            sunGo.transform.rotation = Quaternion.Euler(52f, -35f, 0f);

            bool HideAboveL1(Renderer r) => r.name == "Roof" || r.name.StartsWith("CeilingServices_Level-1") ||
                                            r.name.StartsWith("FlickeringLamp") || r.name.StartsWith("ExitSign_Hanging") && r.bounds.center.y > 0f;
            bool HideAboveL2(Renderer r) => r.bounds.center.y > -0.9f || r.name.StartsWith("CeilingServices_Level-2") ||
                                            r.name == "Slab_Level-1" || r.name.StartsWith("ExitSign_Hanging");

            Render(cam, outDir, new Shot { Name = "overview_L1", Pos = new V(0f, 62f, -52f), Target = new V(0f, 0f, 3f), Fov = 55f, Hide = HideAboveL1 });
            Render(cam, outDir, new Shot { Name = "overview_L2", Pos = new V(0f, 58.6f, -52f), Target = new V(0f, -3.4f, 3f), Fov = 55f, Hide = HideAboveL2 });
            Render(cam, outDir, new Shot { Name = "overview_L1_north", Pos = new V(-10f, 22f, 2f), Target = new V(-8f, 0f, 24f), Fov = 60f, Hide = HideAboveL1 });

            Render(cam, outDir, new Shot { Name = "L1_aisle", Pos = new V(-36f, 1.7f, -5.25f), Target = new V(0f, 1.0f, -7f), Fov = 70f });
            Render(cam, outDir, new Shot { Name = "L1_ramp_top", Pos = new V(-42f, 1.8f, -29f), Target = new V(-42f, -1.2f, -10f), Fov = 70f });
            Render(cam, outDir, new Shot { Name = "L1_north_rooms", Pos = new V(-24f, 1.7f, 12f), Target = new V(-17f, 1.0f, 25f), Fov = 70f });
            Render(cam, outDir, new Shot { Name = "L1_lobby", Pos = new V(0f, 1.6f, 16f), Target = new V(0f, 1.3f, 30f), Fov = 70f });
            Render(cam, outDir, new Shot { Name = "L1_stairwell", Pos = new V(-34.6f, 1.65f, 21.6f), Target = new V(-37f, -2.2f, 28.5f), Fov = 75f });
            Render(cam, outDir, new Shot { Name = "L1_cars_closeup", Pos = new V(-12f, 1.4f, -20f), Target = new V(-6f, 0.5f, -15f), Fov = 65f });
            Render(cam, outDir, new Shot { Name = "L1_ceiling", Pos = new V(8f, 1.4f, -4f), Target = new V(16f, 3.2f, -10f), Fov = 75f });
            Render(cam, outDir, new Shot { Name = "L2_aisle", Pos = new V(36f, -1.7f, -21.75f), Target = new V(0f, -2.4f, -16f), Fov = 70f });
            Render(cam, outDir, new Shot { Name = "L2_ramp_bottom", Pos = new V(-42f, -1.7f, 14f), Target = new V(-42f, -2.2f, -12f), Fov = 70f });
            Render(cam, outDir, new Shot { Name = "L2_storage", Pos = new V(-20f, -1.7f, 16f), Target = new V(-27f, -2.4f, 27f), Fov = 70f });
            Render(cam, outDir, new Shot { Name = "L2_corridor", Pos = new V(37.5f, -1.8f, 28.7f), Target = new V(20f, -2.2f, 28.7f), Fov = 75f });

            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(sunGo);
        }

        private static void Render(Camera cam, string outDir, Shot s)
        {
            const int w = 1600, h = 900;
            cam.fieldOfView = s.Fov;
            cam.transform.position = s.Pos;
            cam.transform.LookAt(s.Target);

            var hidden = new List<Renderer>();
            if (s.Hide != null)
                foreach (var r in Object.FindObjectsByType<Renderer>())
                    if (r.enabled && s.Hide(r)) { r.enabled = false; hidden.Add(r); }

            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB, 4);
            cam.targetTexture = rt;
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
            File.WriteAllBytes(Path.Combine(outDir, s.Name + ".png"), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            foreach (var r in hidden) r.enabled = true;
        }

        private struct V
        {
            private readonly Vector3 _v;
            public V(float x, float y, float z) { _v = new Vector3(x, y, z); }
            public static implicit operator Vector3(V v) => v._v;
        }
    }
}
