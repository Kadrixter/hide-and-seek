using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// Test hook for the wrecked Golf: renders it (with its debris) on the old garage floor from fixed views, side by side
    /// with the reference photos, and optionally an orbit of frames for a turntable video.
    /// Batch mode (without -nographics): -executeMethod HideAndSeek.Map.Editor.WreckShowroom.CaptureFromCommandLine
    /// -wreckshots &lt;folder&gt; [-wreckorbit &lt;frames&gt;] [-wrecklight dark]
    /// </summary>
    public static class WreckShowroom
    {
        private const int W = 1280, H = 720;
        public const string RefFolder = "C:/Users/mckad/Projects/hns-tools/ref/golf";

        public static void CaptureFromCommandLine()
        {
            try
            {
                var args = Environment.GetCommandLineArgs();
                string Arg(string k) { int i = Array.IndexOf(args, k); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
                string dir = Arg("-wreckshots") ?? "Logs/WreckShots";
                int orbit = int.TryParse(Arg("-wreckorbit"), out int o) ? o : 0;
                Capture(dir, orbit);
                Debug.Log("[WreckShowroom] done");
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        private static void Capture(string dir, int orbit)
        {
            Directory.CreateDirectory(dir);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var lib = MaterialLibrary.CreateAll();
            var props = new PropFactory(lib, new List<Mesh>());
            var t0 = DateTime.Now;
            var pm = GolfWreck.Create(props, lib);
            Debug.Log($"[WreckShowroom] built in {(DateTime.Now - t0).TotalSeconds:0.0}s: {pm.Mesh.vertexCount} verts, {pm.Mesh.subMeshCount} materials, bounds {pm.Mesh.bounds}, colliders {pm.Colliders.Count}");

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.localScale = new Vector3(3, 1, 3);
            var floorMat = new Material(Shader.Find("Standard")) { color = new Color(0.33f, 0.33f, 0.32f) };
            floorMat.SetFloat("_Glossiness", 0.15f);
            floor.GetComponent<Renderer>().sharedMaterial = floorMat;
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0f, 1.5f, -6.5f);
            wall.transform.localScale = new Vector3(14f, 3f, 0.3f);
            wall.GetComponent<Renderer>().sharedMaterial = lib["Wall_Concrete_Old"];

            var go = new GameObject("Wreck");
            go.AddComponent<MeshFilter>().sharedMesh = pm.Mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = pm.Materials;

            var camGo = new GameObject("Cam");
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 100f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.055f);
            cam.allowHDR = false;
            var key = new GameObject("Key").AddComponent<Light>();
            key.type = LightType.Directional;
            key.shadows = LightShadows.Soft;
            key.intensity = 0.95f;
            key.transform.rotation = Quaternion.Euler(52f, -150f, 0f);
            var fill = new GameObject("Fill").AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = 0.35f;
            fill.transform.rotation = Quaternion.Euler(30f, 60f, 0f);
            var lamp = new GameObject("Lamp").AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.range = 9f;
            lamp.intensity = 1.3f;
            lamp.color = new Color(1f, 0.95f, 0.85f);
            lamp.shadows = LightShadows.Soft;
            lamp.transform.position = new Vector3(1.5f, 2.9f, 1.8f);
            var flash = new GameObject("Flash").AddComponent<Light>();
            flash.type = LightType.Spot;
            flash.spotAngle = 55f;
            flash.range = 25f;
            flash.intensity = 3f;
            flash.shadows = LightShadows.Soft;
            flash.transform.SetParent(camGo.transform, false);
            flash.transform.localPosition = new Vector3(0.25f, -0.25f, 0f);
            RenderSettings.ambientMode = AmbientMode.Flat;

            var views = new List<(string name, Vector3 pos, Vector3 at, float fov)>
            {
                ("01_front34", new Vector3(1.9f, 1.15f, 4.4f), new Vector3(0.1f, 0.5f, 1.1f), 42f),
                ("02_front", new Vector3(0.15f, 1.45f, 4.3f), new Vector3(0f, 0.55f, 1.4f), 44f),
                ("03_side_photo", new Vector3(3.3f, 1.55f, 2.9f), new Vector3(0.2f, 0.6f, 0.4f), 46f),
                ("04_right", new Vector3(6.2f, 0.95f, 0.2f), new Vector3(0.6f, 0.55f, 0.2f), 42f),
                ("05_left_door", new Vector3(-3.3f, 1.35f, 2.6f), new Vector3(-0.4f, 0.7f, 0.2f), 44f),
                ("06_interior", new Vector3(-1.4f, 1.2f, -0.25f), new Vector3(0.1f, 0.62f, 0.25f), 60f),
                ("07_writing", new Vector3(2.5f, 1.05f, -0.8f), new Vector3(0.86f, 0.76f, -0.72f), 40f),
                ("08_engine", new Vector3(0.35f, 2.0f, 3.1f), new Vector3(0f, 0.55f, 1.45f), 46f),
                ("09_parts", new Vector3(3.9f, 2.5f, 3.3f), new Vector3(1.4f, 0.1f, 1.2f), 52f),
                ("10_rear34", new Vector3(-3.1f, 1.45f, -4.3f), new Vector3(0f, 0.6f, -0.6f), 42f),
                ("11_top", new Vector3(0.6f, 8f, 0.6f), new Vector3(0.6f, 0f, 0.61f), 48f),
                ("12_dark", new Vector3(2.6f, 1.6f, 3.6f), new Vector3(0.2f, 0.6f, 0.8f), 55f),
            };
            var shots = new Dictionary<string, Texture2D>();
            foreach (var (name, pos, at, fov) in views)
            {
                bool dark = name.EndsWith("dark");
                SetLights(dark, key, fill, lamp, flash);
                cam.fieldOfView = fov;
                cam.transform.position = pos;
                cam.transform.LookAt(at);
                var tex = Render(cam);
                File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
                shots[name] = tex;
            }
            Compare(dir, "cmp_front", "golf_front.png", shots["02_front"]);
            Compare(dir, "cmp_side", "golf_side.jpg", shots["03_side_photo"]);
            for (int f = 0; f < orbit; f++)
            {
                SetLights(false, key, fill, lamp, flash);
                float a = f * Mathf.PI * 2f / orbit + 0.6f;
                var c = new Vector3(0.45f, 0.55f, 0.6f);
                cam.fieldOfView = 44f;
                cam.transform.position = c + new Vector3(Mathf.Sin(a) * 5.6f, 1.05f + 0.25f * Mathf.Sin(a * 2f), Mathf.Cos(a) * 5.6f);
                cam.transform.LookAt(c);
                var tex = Render(cam);
                File.WriteAllBytes(Path.Combine(dir, $"orbit_{f:000}.png"), tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }
        }

        private static void SetLights(bool dark, Light key, Light fill, Light lamp, Light flash)
        {
            key.enabled = fill.enabled = lamp.enabled = !dark;
            flash.enabled = dark;
            RenderSettings.ambientLight = dark ? new Color(0.012f, 0.012f, 0.014f) : new Color(0.3f, 0.31f, 0.33f);
            RenderSettings.fog = dark;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.04f;
            RenderSettings.fogColor = Color.black;
        }

        private static Texture2D Render(Camera cam)
        {
            var rt = RenderTexture.GetTemporary(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB, 8);
            cam.targetTexture = rt;
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
            return tex;
        }

        /// <summary>Reference photo (scaled to the render height) next to the render.</summary>
        private static void Compare(string dir, string name, string refFile, Texture2D shot)
        {
            string path = Path.Combine(RefFolder, refFile);
            if (!File.Exists(path)) return;
            var photo = new Texture2D(2, 2);
            photo.LoadImage(File.ReadAllBytes(path));
            int pw = Mathf.RoundToInt(photo.width * (H / (float)photo.height));
            var sheet = new Texture2D(pw + W, H, TextureFormat.RGB24, false);
            for (int y = 0; y < H; y++)
            for (int x = 0; x < pw; x++)
                sheet.SetPixel(x, y, photo.GetPixelBilinear((x + 0.5f) / pw, (y + 0.5f) / H));
            sheet.SetPixels(pw, 0, W, H, shot.GetPixels());
            sheet.Apply();
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), sheet.EncodeToPNG());
        }
    }
}
