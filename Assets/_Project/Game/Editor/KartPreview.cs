using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HideAndSeek.Game.Editor
{
    /// <summary>
    /// Studio renders of the go-kart model (batch: -executeMethod HideAndSeek.Game.Editor.KartPreview.RenderFromCommandLine
    /// -kartprev &lt;dir&gt;): an empty scene, one kart (livery 0 and 3), a sheet of views - front three-quarter, side,
    /// rear three-quarter, top, and a wheel close-up. (2026-11-27: slicks, silver rims, new bodywork.)
    /// </summary>
    public static class KartPreview
    {
        const int TW = 640, TH = 420;

        public static void RenderFromCommandLine()
        {
            string dir = "Logs/kartprev";
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-kartprev");
            if (i >= 0 && i + 1 < args.Length) dir = args[i + 1];
            try { Render(dir); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); return; }
            EditorApplication.Exit(0);
        }

        static void Render(string dir)
        {
            Directory.CreateDirectory(dir);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.32f, 0.33f, 0.36f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.localScale = Vector3.one * 2f;
            floor.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Standard")) { color = new Color(0.22f, 0.22f, 0.23f) };
            var camGo = new GameObject("Cam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.1f, 0.11f, 0.13f);
            cam.nearClipPlane = 0.02f;
            foreach (int liv in new[] { 0, 3 })
            {
                var root = new GameObject("Kart").transform;
                KartModel.Build(root, KartLivery.Of(liv));
                var sheet = new Texture2D(TW * 3, TH * 2, TextureFormat.RGB24, false);
                Vector3 c = new Vector3(0f, 0.2f, 0f);
                Shot(cam, sheet, 0, TH, new Vector3(1.5f, 0.9f, 1.9f), c, 40f);
                Shot(cam, sheet, TW, TH, new Vector3(2.6f, 0.35f, 0f), c, 38f);
                Shot(cam, sheet, TW * 2, TH, new Vector3(-1.5f, 0.9f, -1.9f), c, 40f);
                Shot(cam, sheet, 0, 0, new Vector3(0f, 3.2f, 0.01f), c, 40f);
                Shot(cam, sheet, TW, 0, new Vector3(0.95f, 0.25f, -0.55f), new Vector3(0.53f, 0.145f, -0.55f), 40f);
                Shot(cam, sheet, TW * 2, 0, new Vector3(0.95f, 0.3f, 0.9f), new Vector3(0.5f, 0.13f, 0.5f), 40f);
                File.WriteAllBytes(Path.Combine(dir, $"kart_{liv}.png"), sheet.EncodeToPNG());
                Object.DestroyImmediate(root.gameObject);
            }
            Debug.Log($"[KartPreview] done -> {Path.GetFullPath(dir)}");
        }

        static void Shot(Camera cam, Texture2D sheet, int x, int y, Vector3 from, Vector3 at, float fov)
        {
            cam.transform.position = from;
            cam.transform.LookAt(at);
            cam.fieldOfView = fov;
            var rt = RenderTexture.GetTemporary(TW, TH, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB, 4);
            cam.targetTexture = rt;
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            sheet.ReadPixels(new Rect(0, 0, TW, TH), x, y);
            sheet.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
        }
    }
}
