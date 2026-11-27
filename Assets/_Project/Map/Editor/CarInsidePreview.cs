using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// Preview of the parked cars from the inside and under the bonnet (batch: -executeMethod
    /// HideAndSeek.Map.Editor.CarInsidePreview.RenderFromCommandLine -carprev &lt;dir&gt;): opens the built parking scene,
    /// takes one car of every model and renders a sheet per car: driver's view forward, driver looking back over the rear
    /// seats, the floor at the front wheel arch, the side window from outside, the open bonnet (when the car has one) and
    /// the outside three-quarter. A lamp rides with the camera. (2026-11-27: wheels showing through the cabin, the engine bay.)
    /// </summary>
    public static class CarInsidePreview
    {
        const int TW = 560, TH = 380;

        public static void RenderFromCommandLine()
        {
            string dir = "Logs/carprev";
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-carprev");
            if (i >= 0 && i + 1 < args.Length) dir = args[i + 1];
            try { Render(dir); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); return; }
            EditorApplication.Exit(0);
        }

        [MenuItem("HideAndSeek/Map/Car Inside Preview")]
        public static void RenderMenu() => Render("Logs/carprev");

        static string ModelOf(string name)
        {
            var parts = name.Split('_');
            return parts.Length > 3 ? parts[2] + "_" + parts[3] : name;
        }

        static void Render(string dir)
        {
            Directory.CreateDirectory(dir);
            EditorSceneManager.OpenScene("Assets/_Project/Map/Scenes/ParkingGarage.unity", OpenSceneMode.Single);
            var cars = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)
                .Where(r => r.name.StartsWith("Car_") && r.GetComponent<MeshFilter>() != null && r.GetComponent<MeshFilter>().sharedMesh != null
                            && r.transform.parent != null && r.GetComponent<MeshFilter>().sharedMesh.bounds.size.z > 3f)
                .OrderBy(r => r.name).ToList();
            int hoods = cars.Count(r => r.GetComponent<CarHood>() != null);
            Debug.Log($"[CarPreview] cars: {cars.Count}, with an opening bonnet: {hoods}");
            var picks = cars.GroupBy(r => ModelOf(r.name)).Select(g => g.OrderByDescending(r => r.GetComponent<CarHood>() != null).First()).ToList();
            string only = Arg("-carprevonly");
            if (!string.IsNullOrEmpty(only)) picks = picks.Where(r => r.name.Contains(only)).ToList();

            var camGo = new GameObject("PreviewCam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.04f, 0.05f);
            cam.nearClipPlane = 0.02f;
            cam.farClipPlane = 60f;
            var lamp = new GameObject("PreviewLamp").AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.range = 9f;
            lamp.intensity = 1.6f;
            lamp.color = new Color(1f, 0.95f, 0.88f);
            lamp.shadows = LightShadows.None;

            foreach (var r in picks)
            {
                var t = r.transform;
                var b = r.GetComponent<MeshFilter>().sharedMesh.bounds;
                var hood = r.GetComponent<CarHood>();
                float L = b.extents.z, W = b.extents.x, H = b.size.y;
                float floor = b.min.y;
                float eyeY = floor + H * 0.78f;
                Vector3 P(float x, float y, float z) => t.TransformPoint(new Vector3(x, y, z));
                var sheet = new Texture2D(TW * 3, TH * 2, TextureFormat.RGB24, false);
                float dx = -W * 0.42f; // driver side (left)
                // driver forward
                Shot(cam, lamp, sheet, 0, TH, P(dx, eyeY, b.center.z - L * 0.05f), P(dx, eyeY - 0.25f, b.center.z + L), 75f);
                // driver looking back over the rear seats
                Shot(cam, lamp, sheet, TW, TH, P(dx * 0.3f, eyeY, b.center.z + L * 0.08f), P(0f, floor + H * 0.3f, b.center.z - L), 80f);
                // down at the front footwell / wheel arch
                Shot(cam, lamp, sheet, TW * 2, TH, P(W * 0.2f, eyeY, b.center.z - L * 0.1f), P(-W * 0.85f, floor + H * 0.25f, b.center.z + L * 0.45f), 80f);
                // side window from outside
                Shot(cam, lamp, sheet, 0, 0, P(W + 1.1f, eyeY + 0.15f, b.center.z), P(0f, floor + H * 0.45f, b.center.z), 60f);
                // the bonnet open
                if (hood != null)
                {
                    hood.SetOpenAngle(hood.OpenAngle);
                    Shot(cam, lamp, sheet, TW, 0, P(0.35f, b.max.y + 0.7f, b.max.z + 1.0f), t.TransformPoint(hood.BatteryLocal + new Vector3(0.3f, -0.2f, -0.3f)), 55f);
                    hood.SetOpenAngle(0f);
                }
                // outside three-quarter
                Shot(cam, lamp, sheet, TW * 2, 0, P(W + 2.2f, b.max.y + 0.6f, b.max.z + 2.4f), P(0f, floor + H * 0.4f, b.center.z), 45f);
                File.WriteAllBytes(Path.Combine(dir, $"car_{ModelOf(r.name)}_{(hood != null ? "hood" : "nohood")}.png"), sheet.EncodeToPNG());
                Debug.Log($"[CarPreview] {r.name}: bounds {b.size}, hood {(hood != null)}");
            }
            Debug.Log($"[CarPreview] done -> {Path.GetFullPath(dir)}");
        }

        static string Arg(string name)
        {
            var a = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(a, name);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
        }

        static void Shot(Camera cam, Light lamp, Texture2D sheet, int x, int y, Vector3 from, Vector3 at, float fov)
        {
            cam.transform.position = from;
            cam.transform.LookAt(at);
            cam.fieldOfView = fov;
            lamp.transform.position = from + Vector3.up * 0.05f;
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
