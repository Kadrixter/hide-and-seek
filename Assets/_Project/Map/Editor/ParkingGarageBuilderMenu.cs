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
    /// <summary>Editor entry points: build the parking garage scene, validate it and render preview screenshots.</summary>
    public static class ParkingGarageBuilderMenu
    {
        public const string MapFolder = "Assets/_Project/Map";
        public const string ScenePath = MapFolder + "/Scenes/ParkingGarage.unity";
        public const string MeshFolder = MapFolder + "/Meshes";
        public const string LightingPath = MapFolder + "/Scenes/ParkingGarageLighting.lighting";
        public const string PreviewFolder = "docs/previews/map";

        /// <summary>Bake Umbra occlusion data after building (three stacked floors benefit a lot).</summary>
        public static bool BakeOcclusion = true;

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
            foreach (var guid in AssetDatabase.FindAssets("t:Mesh", new[] { MeshFolder }))
                AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));
            foreach (var file in Directory.GetFiles(GarageTextures.FullPath(MeshFolder), "*.asset"))
                AssetDatabase.DeleteAsset(MeshFolder + "/" + Path.GetFileName(file));

            var store = new Dictionary<string, List<Mesh>>();
            var builder = new ParkingGarageBuilder(lib, store);
            builder.Build();

            SaveMeshes(store);
            ApplyLighting();
            Validate(builder);

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Failed to save scene " + ScenePath);

            if (BakeOcclusion)
            {
                var t0 = DateTime.Now;
                StaticOcclusionCulling.smallestOccluder = 3f;
                StaticOcclusionCulling.smallestHole = 0.25f;
                StaticOcclusionCulling.backfaceThreshold = 100f;
                bool ok = StaticOcclusionCulling.Compute();
                Debug.Log($"[ParkingGarage] Occlusion bake {(ok ? "OK" : "FAILED")} in {(DateTime.Now - t0).TotalSeconds:0.0}s");
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            else
            {
                StaticOcclusionCulling.Clear();
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ReportFileSizes();
        }

        private static void SaveMeshes(Dictionary<string, List<Mesh>> store)
        {
            foreach (var kv in store.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                if (kv.Value.Count == 0) continue;
                string path = $"{MeshFolder}/{kv.Key}.asset";
                AssetDatabase.CreateAsset(kv.Value[0], path);
                for (int i = 1; i < kv.Value.Count; i++)
                    AssetDatabase.AddObjectToAsset(kv.Value[i], kv.Value[0]);
            }
            AssetDatabase.SaveAssets();
        }

        private static void ReportFileSizes()
        {
            var files = Directory.GetFiles(GarageTextures.FullPath(MapFolder), "*", SearchOption.AllDirectories)
                .Where(f => !f.EndsWith(".meta")).Select(f => new FileInfo(f)).OrderByDescending(f => f.Length).ToList();
            long total = files.Sum(f => f.Length);
            Debug.Log($"[ParkingGarage] Map folder: {files.Count} files, {total / 1048576f:0.0} MB total. Largest:\n" +
                      string.Join("\n", files.Take(6).Select(f => $"  {f.Length / 1048576f:0.00} MB  {f.Name}")));
            const long limit = 50L * 1024 * 1024;
            if (files.Count > 0 && files[0].Length > limit)
                throw new InvalidOperationException($"{files[0].Name} exceeds the 50 MB limit");
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
        /// Physics sanity checks: every stair flight and ramp segment is a continuous walkable surface,
        /// every spawn point stands on ground with a free capsule above it.
        /// </summary>
        private static void Validate(ParkingGarageBuilder builder)
        {
            Physics.SyncTransforms();
            var errors = new List<string>();

            float GroundAt(float x, float yFrom, float z) =>
                Physics.Raycast(new Vector3(x, yFrom, z), Vector3.down, out var hit, 4f) ? hit.point.y : float.NaN;

            int flights = 0, rampSegments = 0;
            foreach (var s in ParkingGarageBuilder.Stairs)
            {
                for (int k = 0; k < ParkingGarageBuilder.Levels - 1; k++)
                {
                    flights++;
                    float yTop = ParkingGarageBuilder.FloorY(k);
                    float x = s.X0 + 1f;
                    float zEnd = Mathf.Min(s.ZIn, ParkingGarageBuilder.HZ) - 0.2f;
                    for (float z = s.Zf + 0.4f; z <= zEnd; z += 0.1f)
                    {
                        float expected = yTop - 3.4f * Mathf.Clamp01((z - s.ZA) / (s.ZB - ParkingGarageBuilder.Run - s.ZA));
                        float y = GroundAt(x, expected + 1.0f, z);
                        if (float.IsNaN(y) || Mathf.Abs(y - expected) > 0.2f)
                        {
                            errors.Add($"Stair x0={s.X0} flight {k} z={z:0.0}: ground {y:0.00}, expected {expected:0.00}");
                            break;
                        }
                    }
                }
            }
            foreach (var r in ParkingGarageBuilder.Ramps)
            {
                float zc = (r.Z0 + r.Z1) * 0.5f;
                float dir = Mathf.Sign(r.XBot - r.XTop);
                for (int k = 0; k < ParkingGarageBuilder.Levels - 1; k++)
                {
                    rampSegments++;
                    float yTop = ParkingGarageBuilder.FloorY(k);
                    for (float t = -3f; t <= Mathf.Abs(r.XBot - r.XTop) + 3f; t += 0.5f)
                    {
                        float x = r.XTop + dir * t;
                        float expected = yTop - 3.4f * Mathf.Clamp01(t / Mathf.Abs(r.XBot - r.XTop));
                        foreach (float z in new[] { zc - 2f, zc, zc + 2f })
                        {
                            float y = GroundAt(x, expected + 1.2f, z);
                            if (float.IsNaN(y) || Mathf.Abs(y - expected) > 0.15f)
                                errors.Add($"Ramp xTop={r.XTop} seg {k} x={x:0.0} z={z:0.0}: ground {y:0.00}, expected {expected:0.00}");
                        }
                    }
                }
            }
            var root = GameObject.Find("ParkingGarage");
            var spawns = root.transform.Find(HideAndSeek.Core.GameLayers.SpawnPointsRoot);
            int tagged = 0;
            var perLevel = new int[ParkingGarageBuilder.Levels];
            foreach (Transform t in spawns)
            {
                var p = t.position;
                if (t.CompareTag(HideAndSeek.Core.GameLayers.SpawnPointTag))
                {
                    tagged++;
                    perLevel[Mathf.Clamp(Mathf.RoundToInt(-p.y / 3.4f), 0, perLevel.Length - 1)]++;
                }
                float g = GroundAt(p.x, p.y + 1f, p.z);
                if (float.IsNaN(g) || Mathf.Abs(g - (p.y - 0.05f)) > 0.1f) errors.Add($"Spawn {t.name}: ground at {g:0.00}");
                if (Physics.CheckCapsule(p + Vector3.up * 0.45f, p + Vector3.up * 1.55f, 0.4f))
                    errors.Add($"Spawn {t.name}: capsule blocked");
            }
            if (tagged < 36) errors.Add($"Only {tagged} tagged spawn points");
            if (perLevel.Any(c => c < 10)) errors.Add("Spawn points not spread over all levels: " + string.Join(",", perLevel));
            var seeker = spawns.Find("SeekerSpawn");
            if (seeker == null) errors.Add("SeekerSpawn missing");
            else if (seeker.CompareTag(HideAndSeek.Core.GameLayers.SpawnPointTag)) errors.Add("SeekerSpawn must be untagged");

            int lights = Object.FindObjectsByType<Light>().Length;
            if (lights > 25) errors.Add($"Too many realtime lights: {lights}");
            int renderers = Object.FindObjectsByType<MeshRenderer>().Length;
            Debug.Log($"[ParkingGarage] Validation: {tagged} spawn points ({string.Join("/", perLevel)}), {builder.CarCount} cars, " +
                      $"{lights} realtime lights, {flights} stair flights, {rampSegments} ramp segments, {renderers} renderers");
            if (errors.Count > 0)
                throw new InvalidOperationException("Garage validation failed:\n" + string.Join("\n", errors.Take(40)));
        }

        // ================================================================== previews

        private struct Shot
        {
            public string Name;
            public Vector3 Pos, Target;
            public float Fov;
            public Func<Renderer, bool> Hide;
        }

        private static Shot S(string name, float px, float py, float pz, float tx, float ty, float tz, float fov = 70f,
            Func<Renderer, bool> hide = null) =>
            new Shot { Name = name, Pos = new Vector3(px, py, pz), Target = new Vector3(tx, ty, tz), Fov = fov, Hide = hide };

        private static void CapturePreviewsInternal()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            string outDir = GarageTextures.FullPath(PreviewFolder);
            Directory.CreateDirectory(outDir);
            foreach (var old in Directory.GetFiles(outDir, "*.png")) File.Delete(old);

            var camGo = new GameObject("PreviewCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 400f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.allowHDR = false;
            cam.useOcclusionCulling = false;

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
            foreach (var s in new[]
                     {
                         S("dark_flashlight_L1", -52f, 1.6f, -11.75f, -38f, 0.7f, -16f, 65f),
                         S("dark_flashlight_L1_carwash", -20f, 1.6f, -12f, -20f, 0.6f, 0f, 65f),
                         S("dark_flashlight_L2_ev", -45f, -1.8f, -28.25f, -62f, -2.8f, -23f, 65f),
                         S("dark_flashlight_L2_cages", -20f, -1.8f, -11f, -22f, -2.6f, 0f, 65f),
                         S("dark_flashlight_L3_flood", 30f, -5.2f, -28.25f, 48f, -6.8f, -38f, 65f),
                         S("dark_flashlight_L3_construction", -40f, -5.2f, 11.75f, -58f, -6.2f, 19f, 65f),
                         S("dark_flashlight_tunnel_L3", -40f, -5.2f, -58.1f, -20f, -5.6f, -58.1f, 65f),
                     })
                Render(cam, outDir, s);
            flash.enabled = false;
            Render(cam, outDir, S("dark_ambient_L3_flicker", -75f, -5.2f, -11.75f, -40f, -5.2f, -11.75f, 65f));
            Render(cam, outDir, S("dark_ambient_L1_security", 64f, 1.6f, -14f, 80f, 1.2f, -6f, 65f));

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

            Func<Renderer, bool> HideAbove(int lv)
            {
                float threshold = ParkingGarageBuilder.FloorY(lv) + 2.2f;
                string tag = "_L" + (lv + 1);
                return r => r.name.StartsWith("Roof") || r.name.StartsWith("CeilingServices" + tag) ||
                            r.name.StartsWith("FlickeringLamp" + tag) || r.name.StartsWith("ExitSign_Hanging" + tag) ||
                            (lv > 0 && (r.bounds.center.y > threshold || r.name.StartsWith("Ramps_L" + lv + "-") || r.name.StartsWith("Steps_L" + lv + "-")));
            }

            for (int lv = 0; lv < ParkingGarageBuilder.Levels; lv++)
            {
                float y = ParkingGarageBuilder.FloorY(lv);
                Render(cam, outDir, S($"overview_L{lv + 1}", 0f, y + 128f, -104f, 0f, y, 6f, 55f, HideAbove(lv)));
            }
            Render(cam, outDir, S("aerial_L1_center", -14f, 32f, -32f, -6f, 0f, 2f, 60f, HideAbove(0)));
            Render(cam, outDir, S("aerial_L1_north", -50f, 30f, 25f, -45f, 0f, 55f, 60f, HideAbove(0)));
            Render(cam, outDir, S("aerial_L2_center", 0f, -3.4f + 32f, -32f, 5f, -3.4f, 2f, 60f, HideAbove(1)));
            Render(cam, outDir, S("aerial_L3_west", -45f, -6.8f + 34f, -22f, -40f, -6.8f, 12f, 60f, HideAbove(2)));
            Render(cam, outDir, S("aerial_L3_flood", 50f, -6.8f + 30f, -12f, 52f, -6.8f, -40f, 60f, HideAbove(2)));

            foreach (var s in new[]
                     {
                         S("L1_aisle", -72f, 1.7f, -28.25f, 0f, 1.0f, -30f),
                         S("L1_carwash", -20f, 1.6f, -13f, -20f, 1.0f, 2f),
                         S("L1_motorbikes", 19f, 1.6f, -14f, 19f, 0.4f, 0f),
                         S("L1_security_gate", 68f, 1.7f, -13f, 82f, 1.0f, -3f),
                         S("L1_loading_dock", -66f, 1.8f, 46f, -78f, 1.0f, 57f),
                         S("L1_lobby_central", 0f, 1.6f, -14f, 0f, 1.3f, 0f),
                         S("L1_stairwell", -30.6f, 1.65f, -4.0f, -33f, -2.2f, 3f, 75f),
                         S("L1_ramp_top", -36f, 1.7f, -4f, -60f, -1.6f, -4f),
                         S("L2_ev_bays", -45f, -1.7f, -28.25f, -70f, -2.4f, -22f),
                         S("L2_cages", -20f, -1.7f, -11.75f, -20f, -2.4f, 0f),
                         S("L2_electrical", 19f, -1.7f, -4.3f, 19f, -2.5f, 3f, 80f),
                         S("L2_ramps_stacked", -38f, -1.7f, -4f, -60f, -4.5f, -4f),
                         S("L3_construction", -40f, -5.1f, 11.75f, -60f, -6.0f, 20f),
                         S("L3_flood", 30f, -5.1f, -28.25f, 55f, -6.6f, -42f),
                         S("L3_collapsed", 19f, -5.1f, -14f, 19f, -6.2f, 2f),
                         S("L3_tunnel", -40f, -5.1f, -58.1f, 0f, -5.5f, -58.1f, 75f),
                         S("L3_aisle", 72f, -5.1f, 28.25f, 0f, -5.8f, 26f),
                     })
                Render(cam, outDir, s);

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
    }
}
