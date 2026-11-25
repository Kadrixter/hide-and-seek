using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Game.Editor
{
    /// <summary>
    /// Preview renders of the pool (billiards) models in an empty studio scene (batchmode, no exe):
    /// -executeMethod HideAndSeek.Game.Editor.PoolStudio.RenderFromCommandLine -poolpart &lt;part&gt; -poolout &lt;dir&gt;.
    /// Every part lives in its own file (PoolStudio.&lt;Part&gt;.cs) as <c>static void Part_&lt;part&gt;(Ctx s)</c>
    /// (found by reflection, so one part's file never has to touch another's). The studio: a big dark wooden floor,
    /// grey walls far away, flat ambient, a warm key spot from above with soft shadows and a cool fill.
    /// </summary>
    public static partial class PoolStudio
    {
        public sealed class Ctx
        {
            public string Dir;
            public Material Template;
            public Camera Cam;
            public Light Key, Fill;
            public int Shots;

            /// <summary>An empty root object at <paramref name="pos"/> turned by <paramref name="yaw"/> around Y.</summary>
            public Transform Root(string name, Vector3 pos, float yaw = 0f)
            {
                var t = new GameObject(name).transform;
                t.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
                return t;
            }

            public Light AddLight(LightType type, Vector3 pos, Vector3 lookAt, Color c, float intensity, float range, float spotAngle = 60f, bool shadows = false)
            {
                var l = new GameObject("StudioLight").AddComponent<Light>();
                l.type = type;
                l.transform.position = pos;
                l.transform.LookAt(lookAt);
                l.color = c;
                l.intensity = intensity;
                l.range = range;
                l.spotAngle = spotAngle;
                l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
                return l;
            }

            /// <summary>Render <paramref name="name"/>.png from <paramref name="from"/> looking at <paramref name="at"/>.</summary>
            public void Shot(string name, Vector3 from, Vector3 at, float fov = 35f, int w = 1280, int h = 800)
            {
                Cam.transform.position = from;
                Cam.transform.LookAt(at);
                Cam.fieldOfView = fov;
                var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB, 8);
                Cam.targetTexture = rt;
                Cam.Render();
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                Cam.targetTexture = null;
                RenderTexture.ReleaseTemporary(rt);
                File.WriteAllBytes(Path.Combine(Dir, name + ".png"), tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
                Shots++;
                Debug.Log($"[PoolStudio] shot {name}");
            }
        }

        public static void RenderFromCommandLine()
        {
            int code = 0;
            // The parts change the quality settings for the render: put them back, or Unity saves them into
            // ProjectSettings/QualitySettings.asset on exit (the game's own quality level).
            var shadows = QualitySettings.shadows;
            var shadowRes = QualitySettings.shadowResolution;
            float shadowDist = QualitySettings.shadowDistance;
            int aa = QualitySettings.antiAliasing, pixelLights = QualitySettings.pixelLightCount;
            try { Render(Arg("-poolpart", "all"), Arg("-poolout", "Logs/shots/pool")); }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            finally
            {
                QualitySettings.shadows = shadows;
                QualitySettings.shadowResolution = shadowRes;
                QualitySettings.shadowDistance = shadowDist;
                QualitySettings.antiAliasing = aa;
                QualitySettings.pixelLightCount = pixelLights;
            }
            EditorApplication.Exit(code);
        }

        static string Arg(string name, string def)
        {
            var a = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(a, name);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : def;
        }

        static void Render(string part, string dir)
        {
            Directory.CreateDirectory(dir);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.2f, 0.2f, 0.22f);
            RenderSettings.fog = false;
            QualitySettings.shadowDistance = 30f;
            QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
            QualitySettings.antiAliasing = 8;

            var s = new Ctx { Dir = dir, Template = new Material(Shader.Find("Standard")) { name = "PoolTemplate" } };
            var floor = new Material(s.Template) { color = new Color(0.22f, 0.16f, 0.12f) };
            floor.SetFloat("_Glossiness", 0.25f);
            var wall = new Material(s.Template) { color = new Color(0.36f, 0.35f, 0.33f) };
            wall.SetFloat("_Glossiness", 0.1f);
            Prim(new Vector3(0f, -0.05f, 0f), new Vector3(30f, 0.1f, 30f), floor);
            Prim(new Vector3(0f, 3f, 6f), new Vector3(30f, 6f, 0.1f), wall);
            Prim(new Vector3(-6f, 3f, 0f), new Vector3(0.1f, 6f, 30f), wall);

            s.Key = s.AddLight(LightType.Spot, new Vector3(0.6f, 3.2f, -0.8f), new Vector3(0f, 0.7f, 0f), new Color(1f, 0.94f, 0.84f), 3.2f, 9f, 75f, true);
            s.Fill = s.AddLight(LightType.Directional, new Vector3(-2f, 3f, -3f), new Vector3(0f, 0f, 0f), new Color(0.6f, 0.68f, 0.85f), 0.35f, 0f);

            var cam = new GameObject("StudioCam").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.06f, 0.07f);
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 60f;
            cam.allowMSAA = true;
            s.Cam = cam;

            var m = typeof(PoolStudio).GetMethod("Part_" + part, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (m == null) throw new Exception($"[PoolStudio] no part '{part}' (static void Part_{part}(Ctx s))");
            m.Invoke(null, new object[] { s });
            Debug.Log($"[PoolStudio] part {part}: {s.Shots} shot(s) -> {Path.GetFullPath(dir)}");
        }

        /// <summary>Smoke test of the studio itself: a ball-sized sphere on the floor.</summary>
        static void Part_empty(Ctx s)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            g.transform.position = new Vector3(0f, 0.75f, 0f);
            g.transform.localScale = Vector3.one * 0.05715f;
            s.Shot("empty", new Vector3(0.3f, 0.95f, -0.4f), g.transform.position);
        }

        static void Prim(Vector3 pos, Vector3 size, Material mat)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.transform.position = pos;
            g.transform.localScale = size;
            g.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }
}
