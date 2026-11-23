using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HideAndSeek.Game.Editor
{
    /// <summary>
    /// Quick look at the lobby without building the game: rebuilds only the lobby scene and renders views from free
    /// cameras (plus a top-down plan and the three mirror frames) into Logs/lobbyshots (or -lobbyshotdir DIR).
    /// Runtime-built parts (arcade art, vending model, boombox, bricks, flashlight box, lever) are not in these shots.
    /// Batch: -executeMethod HideAndSeek.Game.Editor.MultiplayerBuilder.LobbyShotsFromCommandLine
    /// </summary>
    public static partial class MultiplayerBuilder
    {
        public static void LobbyShotsFromCommandLine() => RunBatch(() =>
        {
            Directory.CreateDirectory(LobbyArtDir);
            BuildLobbyScene();
            AssetDatabase.SaveAssets();
            string dir = CmdArg("-lobbyshotdir") ?? Path.Combine(Directory.GetCurrentDirectory(), "Logs", "lobbyshots");
            Directory.CreateDirectory(dir);
            RenderLobbyViews(dir);
        });

        private static string CmdArg(string name)
        {
            var a = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(a, name);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
        }

        /// <summary>Named views (shared with the in-game -lobbyshots switch).</summary>
        private static (string name, Vector3 eye, Vector3 at, float fov)[] LobbyViews => LobbyShotsProbe.Views;

        private static void RenderLobbyViews(string dir)
        {
            foreach (var v in LobbyViews) RenderView(Path.Combine(dir, v.name + ".png"), v.eye, v.at, v.fov, false);
            // top-down plan without the ceiling
            var ceiling = GameObject.Find("LobbyRoom/Ceiling");
            var hidden = new System.Collections.Generic.List<Renderer>();
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                if (r.bounds.min.y > 2.9f && r.enabled) { r.enabled = false; hidden.Add(r); }
            RenderView(Path.Combine(dir, "plan.png"), new Vector3(0f, 30f, 0f), Vector3.zero, 0f, true);
            foreach (var r in hidden) r.enabled = true;
            // mirror frames
            var root = GameObject.Find("LobbyRoom").transform;
            foreach (MirrorFrame style in System.Enum.GetValues(typeof(MirrorFrame)))
            {
                var old = root.Find("Mirror");
                if (old != null) Object.DestroyImmediate(old.gameObject);
                LobbyMirror(root, LobbyLayout.Mirror, new Vector2(1.9f, 2.2f), style);
                RenderView(Path.Combine(dir, "mirror_" + style + ".png"), new Vector3(1.0f, 1.6f, -3.0f), new Vector3(1.6f, 1.5f, -6f), 58f, false);
            }
            Debug.Log($"[LobbyShots] {LobbyViews.Length + 4} views in {dir}");
        }

        private static void RenderView(string path, Vector3 eye, Vector3 at, float fov, bool ortho)
        {
            const int w = 1280, h = 720;
            var go = new GameObject("ShotCam");
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 60f;
            cam.allowMSAA = true;
            if (ortho)
            {
                cam.orthographic = true;
                cam.orthographicSize = 6.6f;
                go.transform.SetPositionAndRotation(eye, Quaternion.Euler(90f, 0f, 0f));
            }
            else
            {
                cam.fieldOfView = fov;
                go.transform.position = eye;
                go.transform.LookAt(at);
            }
            var rt = new RenderTexture(w, h, 24) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            cam.targetTexture = null;
            rt.Release();
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(go);
        }
    }
}
