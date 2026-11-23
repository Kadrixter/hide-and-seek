using System;
using System.IO;
using HideAndSeek.Characters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HideAndSeek.Game.Editor
{
    /// <summary>
    /// Renders the Yaris easter egg car without play mode (fast loop for the model): eight views, a strip of the
    /// squash cycle and the rally car next to it for scale. Batch (no -nographics):
    ///   set HNS_YARIS_OUT=C:\out (default Logs/yarisstudio)
    ///   -executeMethod HideAndSeek.Game.Editor.YarisStudio.Shots
    /// </summary>
    public static class YarisStudio
    {
        const int W = 960, H = 600;

        [MenuItem("Hide and Seek/RC/Yaris studio shots")]
        public static void Shots()
        {
            bool ok = false;
            bool prevAsync = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                Run();
                ok = true;
            }
            catch (Exception e) { Debug.LogException(e); }
            finally { ShaderUtil.allowAsyncCompilation = prevAsync; }
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        static void Run()
        {
            string outDir = Environment.GetEnvironmentVariable("HNS_YARIS_OUT");
            if (string.IsNullOrEmpty(outDir)) outDir = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "yarisstudio");
            Directory.CreateDirectory(outDir);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.42f, 0.44f, 0.48f);
            RenderSettings.ambientEquatorColor = new Color(0.3f, 0.3f, 0.31f);
            RenderSettings.ambientGroundColor = new Color(0.16f, 0.16f, 0.16f);

            var sun = new GameObject("Key").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.05f;
            sun.color = new Color(1f, 0.96f, 0.9f);
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            var fill = new GameObject("Fill").AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = 0.35f;
            fill.color = new Color(0.8f, 0.86f, 1f);
            fill.shadows = LightShadows.None;
            fill.transform.rotation = Quaternion.Euler(25f, 150f, 0f);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.localScale = Vector3.one * 0.4f;
            var fm = new Material(Shader.Find("Standard")) { color = new Color(0.42f, 0.42f, 0.41f) };
            fm.SetFloat("_Glossiness", 0.2f);
            floor.GetComponent<Renderer>().sharedMaterial = fm;

            RcCarSkin.YarisWanted = () => true;
            var car = new GameObject("Car");
            var parts = BlockyProps.CreateRcCar(car.transform, 0);
            var skin = parts.Root.GetComponent<RcCarSkin>();
            RcCarSkin.YarisWanted = () => false;
            var rally = new GameObject("Rally");
            BlockyProps.CreateRcCar(rally.transform, 0);
            rally.transform.position = new Vector3(0.36f, 0f, -0.05f);
            rally.SetActive(false);
            RcCarSkin.YarisWanted = null;

            var cam = new GameObject("StudioCam").AddComponent<Camera>();
            cam.fieldOfView = 28f;
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 20f;
            cam.allowHDR = false;
            cam.allowMSAA = true;
            cam.enabled = false;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.085f, 0.095f);

            Vector3 c = new Vector3(0f, 0.085f, 0.01f);
            void View(string name, float yaw, float pitch, float dist, Vector3? focus = null, float fov = 28f)
            {
                Vector3 f = focus ?? c;
                cam.fieldOfView = fov;
                cam.transform.position = f + Quaternion.Euler(pitch, yaw, 0f) * new Vector3(0f, 0f, -dist);
                cam.transform.LookAt(f);
                Render(cam, Path.Combine(outDir, name + ".png"));
            }
            // yaw 180 = looking at the front (camera in front of the car)
            View("01_front34", 145f, 14f, 1.05f);
            View("02_rear34", -35f, 16f, 1.05f);
            View("03_side", 90f, 4f, 1.1f);
            View("04_front", 180f, 6f, 0.95f);
            View("05_rear", 0f, 8f, 0.95f);
            View("06_top34", 120f, 42f, 1.05f);
            View("07_lamp_close", 155f, 10f, 0.32f, new Vector3(0.055f, 0.075f, 0.2f));
            View("08_tail_close", -25f, 12f, 0.32f, new Vector3(0.05f, 0.09f, -0.18f));
            View("09_wheel_close", 95f, 2f, 0.3f, new Vector3(0.09f, 0.04f, 0.14f));
            View("12_hood", 160f, 50f, 0.42f, new Vector3(0.02f, 0.1f, 0.15f));
            View("13_rear_close", 0f, 14f, 0.5f, new Vector3(0f, 0.08f, -0.19f));

            // Squash cycle strip.
            var sq = skin.Yaris.Squash;
            for (int i = 0; i < 8; i++)
            {
                float beats = -0.1f + i * 0.125f;
                YarisCarFx.Pose(beats, 1f, false, out var s, out float hop, out float roll);
                sq.localScale = s;
                sq.localPosition = new Vector3(0f, hop, 0f);
                sq.localRotation = Quaternion.Euler(0f, 0f, roll);
                View($"10_squash_{i}", 150f, 10f, 1.15f, new Vector3(0f, 0.09f, 0f));
            }
            sq.localScale = Vector3.one;
            sq.localPosition = Vector3.zero;
            sq.localRotation = Quaternion.identity;

            rally.SetActive(true);
            car.transform.position = new Vector3(-0.2f, 0f, 0f);
            rally.transform.position = new Vector3(0.2f, 0f, 0f);
            View("11_vs_rally", 150f, 16f, 1.5f, new Vector3(0f, 0.08f, 0f));
            Debug.Log($"[YarisStudio] shots in {outDir}");
            // Tempo detection check on the music file (when there is one).
            var clip = Resources.Load<AudioClip>(YarisEgg.MusicResource);
            if (clip != null && clip.LoadAudioData())
            {
                int frames = Mathf.Min(clip.samples, clip.frequency * 40);
                var data = new float[frames * clip.channels];
                if (clip.GetData(data, 0))
                {
                    var (bpm, off, conf) = YarisMusic.Detect(data, clip.channels, clip.frequency);
                    Debug.Log($"[YarisStudio] tempo {bpm:0.0} BPM, first beat {off:0.000} s, confidence {conf:0.00}");
                }
            }
        }

        static void Render(Camera cam, string path)
        {
            var rt = new RenderTexture(W, H, 24) { antiAliasing = 8 };
            cam.targetTexture = rt;
            cam.Render();
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
        }
    }
}
