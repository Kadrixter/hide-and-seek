using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace HideAndSeek.Characters.Editor
{
    /// <summary>
    /// Renders preview sheets of the extinguisher styles (batch: -executeMethod
    /// HideAndSeek.Characters.Editor.ExtinguisherPreview.RenderFromCommandLine -extprev &lt;dir&gt;): per style a 3/4 view on
    /// a wall with its bracket, a close-up of the valve head (gauge, pin, seal) and the label, plus one sheet with all
    /// styles side by side, and a "held" pose (hose out, nozzle forward, pin pulled, gauge half way).
    /// </summary>
    public static class ExtinguisherPreview
    {
        const int TW = 640, TH = 720;

        public static void RenderFromCommandLine()
        {
            string dir = "Logs/extprev";
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-extprev");
            if (i >= 0 && i + 1 < args.Length) dir = args[i + 1];
            try { Render(dir); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); return; }
            EditorApplication.Exit(0);
        }

        [MenuItem("HideAndSeek/Extinguisher Preview")]
        public static void RenderMenu() => Render("Logs/extprev");

        static void Render(string dir)
        {
            Directory.CreateDirectory(dir);
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.16f, 0.16f, 0.18f);
            RenderSettings.fog = false;

            var wallMat = new Material(Shader.Find("Standard")) { color = new Color(0.46f, 0.47f, 0.48f) };
            wallMat.SetFloat("_Glossiness", 0.15f);
            var floorMat = new Material(Shader.Find("Standard")) { color = new Color(0.3f, 0.3f, 0.31f) };
            floorMat.SetFloat("_Glossiness", 0.2f);
            Prim(PrimitiveType.Cube, new Vector3(0f, 1.25f, -0.13f), new Vector3(4f, 2.5f, 0.1f), wallMat);
            Prim(PrimitiveType.Cube, new Vector3(0f, -0.05f, 0.8f), new Vector3(4f, 0.1f, 2f), floorMat);

            var key = NewLight(LightType.Spot, new Vector3(-0.9f, 1.6f, 1.3f), new Color(1f, 0.93f, 0.82f), 4.5f, 6f);
            key.spotAngle = 50f;
            key.transform.LookAt(new Vector3(0f, 0.45f, 0f));
            key.shadows = LightShadows.Soft;
            var fill = NewLight(LightType.Point, new Vector3(1.2f, 0.9f, 1.0f), new Color(0.55f, 0.65f, 0.85f), 1.2f, 4f);
            var rim = NewLight(LightType.Point, new Vector3(0.4f, 1.3f, -0.05f), new Color(1f, 0.95f, 0.9f), 0.8f, 1.5f);

            var camGo = new GameObject("Cam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.06f);
            cam.fieldOfView = 30f;
            cam.nearClipPlane = 0.02f;

            int n = ExtinguisherModel.Styles.Length;
            var all = new Texture2D(TW * n, TH, TextureFormat.RGB24, false);
            for (int s = 0; s < n; s++)
            {
                var style = ExtinguisherModel.Styles[s];
                var sheet = new Texture2D(TW * 3, TH, TextureFormat.RGB24, false);
                var root = new GameObject("Ext" + s).transform;
                root.position = new Vector3(0f, 0.25f, 0f);
                var p = ExtinguisherModel.Create(root, 0, ShadowCastingMode.On, s);
                var bracket = ExtinguisherModel.CreateBracket(root, 0, s);
                p.Root.localPosition = Vector3.forward * (ExtinguisherModel.BracketDepth + style.TankR);
                bracket.localPosition = Vector3.zero;
                ExtinguisherModel.SetCharge(p, 1f);
                float hgt = ExtinguisherModel.Height(style);

                // 1. whole, three-quarter view
                Vector3 c = p.Root.position + Vector3.up * hgt * 0.5f;
                Shot(cam, sheet, 0, c + new Vector3(-0.62f, 0.28f, 1.35f), c, 32f);
                // 2. valve head close-up from the hose side
                Vector3 head = p.Root.TransformPoint(new Vector3(0f, ExtinguisherModel.HeadY(style), 0f));
                Shot(cam, sheet, TW, head + new Vector3(0.33f, 0.12f, 0.36f), head + new Vector3(0f, 0f, 0.01f), 26f);
                // 3. label, straight on
                Vector3 lab = p.Root.TransformPoint(new Vector3(0f, style.TankTop * 0.55f, 0f));
                Shot(cam, sheet, TW * 2, lab + new Vector3(0f, 0.02f, 0.75f), lab, 24f);
                // all styles sheet: whole view
                Shot(cam, all, TW * s, c + new Vector3(-0.55f, 0.25f, 1.45f), c, 32f);

                // Held: off the wall, pin pulled, hose stretched forward, half charge, lever squeezed.
                p.Root.SetParent(null, true);
                p.Root.position = new Vector3(0f, 0.25f, 0.6f);
                p.Root.rotation = Quaternion.Euler(0f, 60f, 0f);
                bracket.gameObject.SetActive(false);
                ExtinguisherModel.SetPinIn(p, false);
                ExtinguisherModel.SetCharge(p, 0.45f);
                ExtinguisherModel.SetSqueeze(p, 1f);
                ExtinguisherModel.LayHose(p, new Vector3(0.18f, 0.5f, 0.32f), new Vector3(0.1f, 0.05f, 1f));
                var held = new Texture2D(TW, TH, TextureFormat.RGB24, false);
                Vector3 hc = p.Root.position + Vector3.up * 0.35f + Vector3.forward * 0.1f;
                Shot(cam, held, 0, hc + new Vector3(0.9f, 0.5f, 0.9f), hc, 36f);
                File.WriteAllBytes(Path.Combine(dir, $"held_{style.Name}.png"), held.EncodeToPNG());

                File.WriteAllBytes(Path.Combine(dir, $"style_{style.Name}.png"), sheet.EncodeToPNG());
                Object.DestroyImmediate(p.Root.gameObject);
                Object.DestroyImmediate(root.gameObject);
                Debug.Log($"[ExtPreview] style {style.Name}: height {hgt:0.000} m");
            }
            File.WriteAllBytes(Path.Combine(dir, "styles_all.png"), all.EncodeToPNG());
            Debug.Log($"[ExtPreview] done -> {Path.GetFullPath(dir)}");
        }

        static void Shot(Camera cam, Texture2D sheet, int x, Vector3 from, Vector3 at, float fov)
        {
            cam.transform.position = from;
            cam.transform.LookAt(at);
            cam.fieldOfView = fov;
            var rt = RenderTexture.GetTemporary(TW, TH, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB, 8);
            cam.targetTexture = rt;
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            sheet.ReadPixels(new Rect(0, 0, TW, TH), x, 0);
            sheet.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
        }

        static GameObject Prim(PrimitiveType t, Vector3 pos, Vector3 size, Material m)
        {
            var g = GameObject.CreatePrimitive(t);
            g.transform.position = pos;
            g.transform.localScale = size;
            g.GetComponent<Renderer>().sharedMaterial = m;
            return g;
        }

        static Light NewLight(LightType type, Vector3 pos, Color c, float intensity, float range)
        {
            var l = new GameObject("Light").AddComponent<Light>();
            l.type = type;
            l.transform.position = pos;
            l.color = c;
            l.intensity = intensity;
            l.range = range;
            return l;
        }
    }
}
