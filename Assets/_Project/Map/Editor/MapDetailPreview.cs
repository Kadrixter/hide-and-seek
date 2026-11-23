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
    /// Test hook for the map details of 23.11 (dusty cars, covers, leaks) without building the whole map: renders
    /// contact sheets in a small garage-like studio. Batch mode (without -nographics):
    /// -executeMethod HideAndSeek.Map.Editor.MapDetailPreview.CaptureFromCommandLine -detailshots &lt;folder&gt;
    /// </summary>
    public static class MapDetailPreview
    {
        private const int TileW = 800, TileH = 450;

        public static void CaptureFromCommandLine()
        {
            try
            {
                var args = Environment.GetCommandLineArgs();
                int i = Array.IndexOf(args, "-detailshots");
                string dir = i >= 0 && i + 1 < args.Length ? args[i + 1] : "Logs/detailshots";
                Capture(dir);
                Debug.Log("[DetailPreview] done");
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        private sealed class Rig
        {
            public Camera Cam;
            public Light Key, Fill, Flash;
        }

        private static void Capture(string dir)
        {
            Directory.CreateDirectory(dir);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var lib = MaterialLibrary.CreateAll();
            var store = new List<Mesh>();
            var props = new PropFactory(lib, store);
            var rig = MakeRig();
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.localScale = new Vector3(4, 1, 4);
            floor.GetComponent<Renderer>().sharedMaterial = lib["Floor_Concrete_Old"];
            Object.DestroyImmediate(floor.GetComponent<Collider>());

            var models = new Dictionary<string, ParkedCar>();
            foreach (var c in ParkedCars.All()) models[c.Name] = c;

            // ---- dust: clean / level -1 / level -2, two models
            var cars = new List<GameObject>();
            string[] names = { "Sedan_0", "CompactHatch_1" };
            string[] paints = { "CarPaint_Navy", "CarPaint_Red" };
            MaterialLibrary.DustPreset?[] presets = { null, MaterialLibrary.DustPreset.LightPlus, MaterialLibrary.DustPreset.Forgotten };
            for (int row = 0; row < 2; row++)
            for (int k = 0; k < 3; k++)
            {
                var m = models[names[row]];
                var pm = props.Parked(m);
                var mats = (Material[])pm.Materials.Clone();
                mats[0] = lib[paints[row]];
                if (presets[k].HasValue)
                    for (int s = 0; s < mats.Length; s++) mats[s] = lib.Dusty(mats[s], presets[k].Value);
                // each car on the floor height of its level (the dust materials know their floor height)
                var go = Place(pm.Mesh, mats, new Vector3(0f, k == 2 ? -6.8f : k == 1 ? -3.4f : 0f, 0f));
                cars.Add(go);
            }
            var tiles = new List<Texture2D>();
            for (int i = 0; i < cars.Count; i++)
            {
                foreach (var c in cars) c.SetActive(false);
                var car = cars[i];
                car.SetActive(true);
                Vector3 basePos = car.transform.position;
                floor.transform.position = basePos;
                var b = car.GetComponent<MeshFilter>().sharedMesh.bounds;
                Vector3 c0 = basePos + b.center;
                tiles.Add(Tile(rig, false, c0 + new Vector3(3.4f, 1.9f, 4.0f), c0, 38f));
                tiles.Add(Tile(rig, true, c0 + new Vector3(2.6f, 1.2f, 3.6f), c0, 50f));
            }
            Sheet(dir, "dust_clean_L1_L2", tiles, 2);

            // ---- covers: the four kinds
            foreach (var c in cars) Object.DestroyImmediate(c);
            tiles.Clear();
            var kinds = new[] { TarpKind.Cover, TarpKind.BlueTarp, TarpKind.Foil, TarpKind.Canvas };
            var tarpModels = new[] { "Sedan_1", "Estate_0", "CompactHatch_0", "Suv_0" };
            for (int i = 0; i < kinds.Length; i++)
            {
                var m = models[tarpModels[i]];
                var pm = props.Parked(m);
                var mats = (Material[])pm.Materials.Clone();
                mats[0] = lib[i % 2 == 0 ? "CarPaint_Silver" : "CarPaint_Maroon"];
                for (int s = 0; s < mats.Length; s++) mats[s] = lib.Dusty(mats[s], MaterialLibrary.DustPreset.Forgotten);
                var go = Place(pm.Mesh, mats, new Vector3(0f, -6.8f, 0f));
                TarpDrape.DebugLog = kinds[i] == TarpKind.Foil;
                var built = TarpCars.Build(lib, m, pm.Mesh, kinds[i], 17 + i * 31, store);
                TarpDrape.DebugLog = false;
                TarpCars.Attach(go, built, 0);
                floor.transform.position = go.transform.position;
                var b = pm.Mesh.bounds;
                Vector3 c0 = go.transform.position + b.center;
                tiles.Add(Tile(rig, false, c0 + new Vector3(3.6f, 1.6f, 4.2f), c0, 40f));
                tiles.Add(Tile(rig, false, c0 + new Vector3(-3.8f, 1.8f, -4.0f), c0, 40f));
                tiles.Add(Tile(rig, true, c0 + new Vector3(2.4f, 1.1f, 3.4f), c0, 52f));
                Object.DestroyImmediate(go);
                Debug.Log($"[DetailPreview] {kinds[i]} on {m.Name}: cloth {built.Cloth.vertexCount} verts, extras {(built.Extras != null ? built.Extras.vertexCount : 0)}");
            }
            Sheet(dir, "covers", tiles, 3);

            // ---- leaks: four fluids next to each other in front of a car
            tiles.Clear();
            string[] fluids = { "Leak_Oil", "Leak_CoolantGreen", "Leak_CoolantPink", "Leak_Brake" };
            var quad = new Mesh();
            quad.SetVertices(new List<Vector3> { new Vector3(-0.8f, 0f, -1.6f), new Vector3(0.8f, 0f, -1.6f), new Vector3(0.8f, 0f, 1.6f), new Vector3(-0.8f, 0f, 1.6f) });
            quad.SetNormals(new List<Vector3> { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
            quad.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            floor.transform.position = Vector3.zero;
            for (int f = 0; f < fluids.Length; f++)
            {
                quad.SetUVs(0, new List<Vector2> { new Vector2(f % 3 / 3f, 0f), new Vector2((f % 3 + 1) / 3f, 0f), new Vector2((f % 3 + 1) / 3f, 1f), new Vector2(f % 3 / 3f, 1f) });
                var q = Place(Object.Instantiate(quad), new[] { lib[fluids[f]] }, new Vector3(0f, 0.05f, 0f));
                var m = models["Sedan_0"];
                var pm = props.Parked(m);
                var mats = (Material[])pm.Materials.Clone();
                mats[0] = lib["CarPaint_Grey"];
                var car = Place(pm.Mesh, mats, new Vector3(0f, 0f, -0.96f - pm.Mesh.bounds.max.z + 0.8f));
                tiles.Add(Tile(rig, true, new Vector3(1.6f, 1.6f, 3.2f), new Vector3(0f, 0f, 0.3f), 55f));
                tiles.Add(Tile(rig, true, new Vector3(0.4f, 1.0f, 3.0f), new Vector3(0f, 0f, 0.0f), 55f));
                Object.DestroyImmediate(q);
                Object.DestroyImmediate(car);
            }
            Sheet(dir, "leaks", tiles, 2);
        }

        private static GameObject Place(Mesh mesh, Material[] mats, Vector3 pos)
        {
            var go = new GameObject(mesh.name);
            go.transform.position = pos;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = mats;
            return go;
        }

        private static Rig MakeRig()
        {
            var r = new Rig();
            var camGo = new GameObject("Cam");
            r.Cam = camGo.AddComponent<Camera>();
            r.Cam.nearClipPlane = 0.03f;
            r.Cam.farClipPlane = 200f;
            r.Cam.clearFlags = CameraClearFlags.SolidColor;
            r.Cam.allowHDR = false;
            r.Key = new GameObject("Key").AddComponent<Light>();
            r.Key.type = LightType.Directional;
            r.Key.shadows = LightShadows.Soft;
            r.Key.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
            r.Fill = new GameObject("Fill").AddComponent<Light>();
            r.Fill.type = LightType.Directional;
            r.Fill.transform.rotation = Quaternion.Euler(25f, 145f, 0f);
            r.Flash = new GameObject("Flash").AddComponent<Light>();
            r.Flash.type = LightType.Spot;
            r.Flash.spotAngle = 60f;
            r.Flash.range = 30f;
            r.Flash.intensity = 3.2f;
            r.Flash.color = new Color(1f, 0.95f, 0.85f);
            r.Flash.shadows = LightShadows.Soft;
            r.Flash.transform.SetParent(camGo.transform, false);
            r.Flash.transform.localPosition = new Vector3(0.25f, -0.25f, 0f);
            return r;
        }

        private static Texture2D Tile(Rig rig, bool dark, Vector3 pos, Vector3 at, float fov)
        {
            rig.Key.enabled = !dark;
            rig.Fill.enabled = !dark;
            rig.Flash.enabled = dark;
            rig.Key.intensity = 0.95f;
            rig.Fill.intensity = 0.35f;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = dark ? new Color(0.01f, 0.01f, 0.012f) : new Color(0.32f, 0.33f, 0.36f);
            RenderSettings.fog = dark;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.045f;
            RenderSettings.fogColor = new Color(0.004f, 0.004f, 0.005f);
            rig.Cam.backgroundColor = dark ? Color.black : new Color(0.3f, 0.31f, 0.33f);
            rig.Cam.fieldOfView = fov;
            rig.Cam.transform.position = pos;
            rig.Cam.transform.LookAt(at);
            var rt = RenderTexture.GetTemporary(TileW, TileH, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB, 8);
            rig.Cam.targetTexture = rt;
            rig.Cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(TileW, TileH, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, TileW, TileH), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            rig.Cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
            return tex;
        }

        private static void Sheet(string dir, string name, List<Texture2D> tiles, int cols)
        {
            int rows = (tiles.Count + cols - 1) / cols;
            var sheet = new Texture2D(TileW * cols, TileH * rows, TextureFormat.RGB24, false);
            for (int i = 0; i < tiles.Count; i++)
                sheet.SetPixels((i % cols) * TileW, (rows - 1 - i / cols) * TileH, TileW, TileH, tiles[i].GetPixels());
            sheet.Apply();
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), sheet.EncodeToPNG());
            foreach (var t in tiles) Object.DestroyImmediate(t);
            Object.DestroyImmediate(sheet);
        }
    }
}
