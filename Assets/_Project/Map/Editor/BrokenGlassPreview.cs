using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// Preview of the broken car glass (batch: -executeMethod HideAndSeek.Map.Editor.BrokenGlassPreview.RenderFromCommandLine
    /// -glassprev &lt;dir&gt;): opens the built parking scene, picks a few cars with Hns/CarBrokenGlass and renders each one
    /// from the front three-quarter, the side, the back and close to the windscreen, lit by a lamp beside the camera.
    /// </summary>
    public static class BrokenGlassPreview
    {
        const int TW = 640, TH = 440;

        public static void RenderFromCommandLine()
        {
            string dir = "Logs/glassprev";
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-glassprev");
            if (i >= 0 && i + 1 < args.Length) dir = args[i + 1];
            try { Render(dir); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); return; }
            EditorApplication.Exit(0);
        }

        [MenuItem("HideAndSeek/Map/Broken Glass Preview")]
        public static void RenderMenu() => Render("Logs/glassprev");

        static void Render(string dir)
        {
            Directory.CreateDirectory(dir);
            EditorSceneManager.OpenScene("Assets/_Project/Map/Scenes/ParkingGarage.unity", OpenSceneMode.Single);
            bool flats = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-prevflat") >= 0;
            var broken = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)
                .Where(r => flats ? r.transform.Find("FlatTyre") != null
                              : r.sharedMaterials.Any(m => m != null && m.shader != null && m.shader.name == "Hns/CarBrokenGlass"))
                .OrderBy(r => r.transform.position.x + r.transform.position.z * 0.37f).ToList();
            Debug.Log($"[GlassPreview] cars with broken glass: {broken.Count}");

            var camGo = new GameObject("PreviewCam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.04f, 0.05f);
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 80f;
            var lamp = new GameObject("PreviewLamp").AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.range = 14f;
            lamp.intensity = 2.2f;
            lamp.color = new Color(1f, 0.95f, 0.88f);
            lamp.shadows = LightShadows.Soft;

            int take = Mathf.Min(6, broken.Count);
            var picks = new List<MeshRenderer>();
            for (int k = 0; k < take; k++) picks.Add(broken[k * broken.Count / Mathf.Max(1, take)]);
            foreach (var r in picks)
            {
                var t = r.transform;
                var b = r.GetComponent<MeshFilter>().sharedMesh.bounds;
                Vector3 c = t.TransformPoint(b.center + Vector3.up * b.extents.y * 0.35f);
                float L = b.extents.z, W = b.extents.x;
                var sheet = new Texture2D(TW * 3, TH * 2, TextureFormat.RGB24, false);
                Shot(cam, lamp, sheet, 0, TH, t.TransformPoint(b.center + new Vector3(W + 2.0f, 1.0f, L + 2.4f)), c, 40f);
                Shot(cam, lamp, sheet, TW, TH, t.TransformPoint(b.center + new Vector3(W + 3.0f, 0.5f, 0f)), c, 40f);
                Shot(cam, lamp, sheet, TW * 2, TH, t.TransformPoint(b.center + new Vector3(-W - 3.0f, 0.5f, 0f)), c, 40f);
                Shot(cam, lamp, sheet, 0, 0, t.TransformPoint(b.center + new Vector3(-0.6f, 1.0f, -L - 2.6f)), c, 40f);
                Vector3 ws = t.TransformPoint(new Vector3(b.center.x, b.max.y - 0.25f, b.center.z + L * 0.3f));
                Shot(cam, lamp, sheet, TW, 0, t.TransformPoint(new Vector3(b.center.x + 0.3f, b.max.y + 0.4f, b.max.z + 1.1f)), ws, 34f);
                Vector3 sw = t.TransformPoint(new Vector3(b.max.x, b.max.y - 0.3f, b.center.z));
                Shot(cam, lamp, sheet, TW * 2, 0, t.TransformPoint(new Vector3(b.max.x + 1.5f, b.max.y - 0.1f, b.center.z + 0.3f)), sw, 40f);
                var gm = r.sharedMaterials.FirstOrDefault(mm => mm != null && mm.shader != null && mm.shader.name == "Hns/CarBrokenGlass");
                if (gm != null) Debug.Log($"[GlassPreview] {r.name}: panes {gm.GetVector("_PanesA")} {gm.GetVector("_PanesB")} seg0 {gm.GetVector("_Seg0")} seg1 {gm.GetVector("_Seg1")} seg2 {gm.GetVector("_Seg2")}");
                string name = $"{r.name}".Replace('/', '_');
                File.WriteAllBytes(Path.Combine(dir, $"glass_{picks.IndexOf(r)}_{name}.png"), sheet.EncodeToPNG());
            }
            Debug.Log($"[GlassPreview] done -> {Path.GetFullPath(dir)}");
        }

        static void Shot(Camera cam, Light lamp, Texture2D sheet, int x, int y, Vector3 from, Vector3 at, float fov)
        {
            cam.transform.position = from;
            cam.transform.LookAt(at);
            cam.fieldOfView = fov;
            lamp.transform.position = from + cam.transform.right * 0.8f + Vector3.up * 0.9f;
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
