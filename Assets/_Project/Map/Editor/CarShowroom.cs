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
    /// <summary>
    /// Test hook: renders every hero car in an empty, well lit studio and writes one contact sheet per car
    /// (3/4 front and rear, side, front, rear, top, close-ups, interior and a dark garage view with a flashlight).
    /// Batch mode (without -nographics): -executeMethod HideAndSeek.Map.Editor.CarShowroom.CaptureFromCommandLine
    /// -carshots &lt;folder&gt; [-cars Name1,Name2]
    /// </summary>
    public static class CarShowroom
    {
        private const int TileW = 800, TileH = 450, Cols = 3;

        public static void CaptureFromCommandLine()
        {
            try
            {
                var args = Environment.GetCommandLineArgs();
                string Arg(string k) { int i = Array.IndexOf(args, k); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
                string dir = Arg("-carshots") ?? "Builds/CarShots";
                var only = Arg("-cars")?.Split(',').ToList();
                Capture(dir, only);
                Debug.Log("[CarShowroom] done");
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        private static void Capture(string dir, List<string> only)
        {
            Directory.CreateDirectory(dir);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var lib = MaterialLibrary.CreateAll();
            var store = new List<Mesh>();
            var props = new PropFactory(lib, store);

            var floorMat = new Material(Shader.Find("Standard")) { color = new Color(0.36f, 0.36f, 0.37f) };
            floorMat.SetFloat("_Glossiness", 0.2f);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.localScale = new Vector3(6, 1, 6);
            floor.GetComponent<Renderer>().sharedMaterial = floorMat;

            var camGo = new GameObject("Cam");
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 200f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.allowHDR = false;
            var key = new GameObject("Key").AddComponent<Light>();
            key.type = LightType.Directional;
            key.shadows = LightShadows.Soft;
            key.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            var fill = new GameObject("Fill").AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.shadows = LightShadows.None;
            fill.transform.rotation = Quaternion.Euler(25f, 145f, 0f);
            var flash = new GameObject("Flash").AddComponent<Light>();
            flash.type = LightType.Spot;
            flash.spotAngle = 60f;
            flash.range = 30f;
            flash.intensity = 3.2f;
            flash.color = new Color(1f, 0.95f, 0.85f);
            flash.shadows = LightShadows.Soft;
            flash.transform.SetParent(camGo.transform, false);
            flash.transform.localPosition = new Vector3(0.25f, -0.25f, 0f);

            foreach (var car in HeroCars.All())
            {
                if (only != null && !only.Contains(car.Name)) continue;
                var pm = props.Get("Hero_" + car.Name, (mb, p) => car.Build(mb, p));
                var go = new GameObject(car.Name);
                go.AddComponent<MeshFilter>().sharedMesh = pm.Mesh;
                var mats = (Material[])pm.Materials.Clone();
                mats[0] = lib[car.DefaultPaint];
                go.AddComponent<MeshRenderer>().sharedMaterials = mats;
                // blinker anchors shown as small orange cubes on the dark tile
                var anchors = new List<GameObject>();
                foreach (var b in pm.Blinkers)
                {
                    var a = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    a.transform.position = b.Pos + b.Normal * 0.004f;
                    a.transform.rotation = Quaternion.LookRotation(b.Normal);
                    a.transform.localScale = new Vector3(b.Size.x, b.Size.y, 0.008f);
                    a.GetComponent<Renderer>().sharedMaterial = lib["Car_LensAmber"];
                    a.SetActive(false);
                    anchors.Add(a);
                }
                var bnd = pm.Mesh.bounds;
                float L = bnd.size.z, H = bnd.size.y;
                Vector3 c = bnd.center;
                var views = new List<(string, Vector3, Vector3, float)>
                {
                    ("3/4 front", c + new Vector3(3.6f, 1.2f, 4.2f), c, 40f),
                    ("3/4 rear", c + new Vector3(-3.8f, 1.4f, -4.2f), c, 40f),
                    ("side", c + new Vector3(6.5f, 0.35f, 0f), c, 40f),
                    ("front", c + new Vector3(0f, 0.25f, 5.5f), c, 30f),
                    ("rear", c + new Vector3(0f, 0.35f, -5.5f), c, 30f),
                    ("top", c + new Vector3(0.01f, 7.5f, 0f), c, 42f),
                    ("front close", new Vector3(1.3f, 0.75f, bnd.max.z + 1.3f), new Vector3(0.55f, 0.5f, bnd.max.z - 0.1f), 40f),
                    ("rear close", new Vector3(-1.3f, 0.85f, bnd.min.z - 1.3f), new Vector3(-0.55f, 0.6f, bnd.min.z + 0.1f), 40f),
                    ("wheel", new Vector3(1.9f, 0.5f, bnd.max.z - L * 0.15f), new Vector3(0.8f, 0.33f, bnd.max.z - L * 0.22f), 40f),
                    ("interior", new Vector3(-1.6f, H + 0.35f, c.z + 0.2f), new Vector3(0.1f, H * 0.45f, c.z + 0.1f), 50f),
                    ("dark front", c + new Vector3(2.8f, 1.0f, 4.0f), c, 55f),
                    ("dark alarm", c + new Vector3(-3.2f, 1.0f, -3.8f), c, 55f),
                };
                int rows = (views.Count + Cols - 1) / Cols;
                var sheet = new Texture2D(TileW * Cols, TileH * rows, TextureFormat.RGB24, false);
                for (int v = 0; v < views.Count; v++)
                {
                    var (name, pos, target, fov) = views[v];
                    bool dark = name.StartsWith("dark");
                    Studio(dark, key, fill, flash, cam);
                    foreach (var a in anchors) a.SetActive(name == "dark alarm");
                    cam.fieldOfView = fov;
                    cam.transform.position = pos;
                    cam.transform.LookAt(target);
                    Blit(cam, sheet, (v % Cols) * TileW, (rows - 1 - v / Cols) * TileH);
                }
                File.WriteAllBytes(Path.Combine(dir, car.Name + ".png"), sheet.EncodeToPNG());
                Object.DestroyImmediate(sheet);
                foreach (var a in anchors) Object.DestroyImmediate(a);
                Object.DestroyImmediate(go);
                Debug.Log($"[CarShowroom] {car.Name}: {pm.Mesh.vertexCount} verts, {pm.Mesh.subMeshCount} materials, bounds {bnd.size}, colliders {pm.Colliders.Count}, blinkers {pm.Blinkers.Count}");
            }
        }

        private static void Studio(bool dark, Light key, Light fill, Light flash, Camera cam)
        {
            key.enabled = !dark;
            fill.enabled = !dark;
            flash.enabled = dark;
            key.intensity = 1.05f;
            fill.intensity = 0.45f;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = dark ? new Color(0.01f, 0.01f, 0.012f) : new Color(0.42f, 0.43f, 0.46f);
            RenderSettings.fog = dark;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.045f;
            RenderSettings.fogColor = new Color(0.004f, 0.004f, 0.005f);
            cam.backgroundColor = dark ? Color.black : new Color(0.62f, 0.64f, 0.67f);
        }

        private static void Blit(Camera cam, Texture2D sheet, int x, int y)
        {
            var rt = RenderTexture.GetTemporary(TileW, TileH, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB, 8);
            cam.targetTexture = rt;
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            sheet.ReadPixels(new Rect(0, 0, TileW, TileH), x, y);
            sheet.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
        }
    }
}
