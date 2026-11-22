using System.Collections;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Opt-in tests of the start lever (host + one client).
    /// Host -testlever: pull while not everyone is ready (must rattle), make everyone ready, pull (round starts).
    /// Host -levershots &lt;dir&gt;: model renders (model_*.png), first-person shot of the locked rattle (fp_jam.png), then
    /// records the pull at 30 fps: real screen (fpp/f_###.jpg) and a side camera of the body (tpp/f_###.jpg).
    /// The round is not started (see MatchManager.LeverStay).
    /// Client -leverwatch &lt;dir&gt;: stands beside the lever and saves its own screen while the host pulls (watch_##.png).
    /// </summary>
    public sealed class LeverProbe : MonoBehaviour
    {
        static string Arg(string name)
        {
            var a = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(a, name);
            return i < 0 ? null : i + 1 < a.Length && !a[i + 1].StartsWith("-") ? a[i + 1] : "";
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Arg("-testlever") == null && Arg("-levershots") == null && Arg("-leverwatch") == null) return;
            var g = new GameObject("LeverProbe");
            DontDestroyOnLoad(g);
            g.AddComponent<LeverProbe>();
        }

        IEnumerator Start()
        {
            while (!NetPlayer.Local || !LobbyButton.Instance || MatchManager.Instance == null) yield return null;
            string watch = Arg("-leverwatch");
            if (!string.IsNullOrEmpty(watch)) { yield return Watch(watch); yield break; }
            for (float w = 0f; NetPlayer.All.Count < 2 && w < 90f; w += Time.unscaledDeltaTime) yield return null;
            yield return new WaitForSeconds(2f);
            string shots = Arg("-levershots");
            yield return Run(string.IsNullOrEmpty(shots) ? null : shots);
        }

        static void Place(NetPlayer p, Vector3 leverLocal, Vector3 lookAtLocal)
        {
            var lt = LobbyButton.Instance.transform;
            Vector3 pos = lt.TransformPoint(leverLocal), look = lt.TransformPoint(lookAtLocal) - (pos + Vector3.up * 1.6f);
            float yaw = Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg;
            p.TeleportLocal(pos, yaw);
            p.Motor.SetLook(yaw, Mathf.Atan2(look.y, new Vector2(look.x, look.z).magnitude) * Mathf.Rad2Deg);
        }

        IEnumerator Run(string dir)
        {
            var p = NetPlayer.Local;
            var lever = LobbyButton.Instance;
            var lt = lever.transform;
            if (dir != null)
            {
                System.IO.Directory.CreateDirectory(dir);
                Place(p, new Vector3(3f, 0f, 5f), new Vector3(3f, 1.6f, 8f));
                yield return new WaitForSeconds(1f);
                yield return ModelShots(dir, lt);
            }

            // Locked: not everyone is ready -> the lever only rattles.
            p.TeleportLocal(lever.StandPoint, lever.StandYaw);
            p.Motor.SetLook(lever.StandYaw, 8f);
            yield return new WaitForSeconds(0.8f);
            Debug.Log("[LeverProbe] pull while locked");
            lever.Press();
            yield return new WaitForSeconds(0.3f);
            if (dir != null) ScreenCapture.CaptureScreenshot(dir + "/fp_jam.png");
            yield return new WaitForSeconds(1.5f);
            Debug.Log("[LeverProbe] pulling=" + lever.Pulling + " (expected False)");

            foreach (var o in NetPlayer.All)
                if (o != null && o != p) o.Ready.Value = true;
            Debug.Log("[LeverProbe] everyone ready");
            yield return new WaitForSeconds(3.5f);
            p.Motor.SetLook(lever.StandYaw, 8f);

            if (dir == null)
            {
                lever.Press();
                yield return new WaitForSeconds(0.5f);
                Debug.Log("[LeverProbe] pulling=" + lever.Pulling + " (expected True)");
                yield return new WaitForSeconds(6f);
                Debug.Log("[LeverProbe] phase after pull: " + MatchManager.Instance.Phase.Value);
                yield break;
            }
            yield return Record(dir, p, lever);
            Debug.Log("[LeverProbe] recorded");
            yield return new WaitForSeconds(1f);
            Application.Quit();
        }

        IEnumerator ModelShots(string dir, Transform lt)
        {
            var cam = new GameObject("LeverModelCam").AddComponent<Camera>();
            cam.enabled = false;
            cam.cullingMask = ~(1 << 9);
            var rt = new RenderTexture(1280, 960, 24) { antiAliasing = 4 };
            cam.targetTexture = rt;
            void View(string name, Vector3 from, Vector3 at, float fov)
            {
                cam.fieldOfView = fov;
                cam.transform.position = lt.TransformPoint(from);
                cam.transform.LookAt(lt.TransformPoint(at));
                cam.Render();
                Save(rt, dir + "/model_" + name + ".png", false);
            }
            View("front", new Vector3(0f, 1.25f, 2.7f), new Vector3(0f, 1.2f, 0.1f), 45f);
            View("34", new Vector3(1.5f, 1.55f, 2.1f), new Vector3(0f, 1.15f, 0.2f), 45f);
            View("side", new Vector3(2.3f, 1.2f, 0.75f), new Vector3(0f, 1.15f, 0.35f), 45f);
            View("lock", new Vector3(0.95f, 1.5f, 0.95f), new Vector3(0.33f, 1.27f, 0.25f), 40f);
            var lever = LobbyButton.Instance;
            lever.DebugThrow = 1f;
            yield return null;
            View("down_34", new Vector3(1.5f, 1.4f, 2.1f), new Vector3(0f, 1.0f, 0.2f), 45f);
            View("down_side", new Vector3(2.3f, 1.1f, 0.75f), new Vector3(0f, 1.0f, 0.35f), 45f);
            lever.DebugThrow = -1f;
            yield return null;
            Destroy(cam.gameObject);
            rt.Release();
        }

        IEnumerator Record(string dir, NetPlayer p, LobbyButton lever)
        {
            string fpp = dir + "/fpp", tpp = dir + "/tpp";
            System.IO.Directory.CreateDirectory(fpp);
            System.IO.Directory.CreateDirectory(tpp);
            var lt = lever.transform;
            var cam = new GameObject("LeverSideCam").AddComponent<Camera>();
            cam.enabled = false;
            cam.cullingMask = ~(1 << 9);
            cam.fieldOfView = 50f;
            var rt = new RenderTexture(Screen.width, Screen.height, 24) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.transform.position = lt.TransformPoint(new Vector3(2.35f, 1.2f, 1.35f));
            cam.transform.LookAt(lt.TransformPoint(new Vector3(0f, 1.0f, 0.62f)));
            Time.captureFramerate = 30;
            const int frames = 186, pressAt = 12;
            for (int i = 0; i < frames; i++)
            {
                if (i == pressAt) lever.Press();
                yield return new WaitForEndOfFrame();
                var shot = ScreenCapture.CaptureScreenshotAsTexture();
                System.IO.File.WriteAllBytes(fpp + "/f_" + i.ToString("000") + ".jpg", shot.EncodeToJPG(92));
                Destroy(shot);
                p.Character.SetFirstPersonMode(false);
                cam.Render();
                Save(rt, tpp + "/f_" + i.ToString("000") + ".jpg", true);
                p.Character.SetFirstPersonMode(true);
            }
            Time.captureFramerate = 0;
            Destroy(cam.gameObject);
            rt.Release();
        }

        IEnumerator Watch(string dir)
        {
            System.IO.Directory.CreateDirectory(dir);
            var p = NetPlayer.Local;
            yield return new WaitForSeconds(1f);
            Place(p, new Vector3(-3f, 0f, 5f), new Vector3(-3f, 1.6f, 8f));
            while (!p.Ready.Value) yield return null;
            Vector3 spot = new Vector3(-1.9f, 0f, 1.75f), at = new Vector3(0f, 1.05f, 0.75f);
            Place(p, spot, at);
            bool started = false;
            LobbyButton.PullStarted += () => started = true;
            while (!started) { Place(p, spot, at); yield return null; }
            int n = 0;
            for (float t = 0f; t < 4.6f; t += 0.3f)
            {
                ScreenCapture.CaptureScreenshot(dir + "/watch_" + n.ToString("00") + ".png");
                n++;
                yield return new WaitForSeconds(0.3f);
            }
            Debug.Log("[LeverProbe] watch saved " + n);
            yield return new WaitForSeconds(1f);
            Application.Quit();
        }

        static void Save(RenderTexture rt, string path, bool jpg)
        {
            var old = RenderTexture.active;
            RenderTexture.active = rt;
            var img = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            img.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            img.Apply();
            System.IO.File.WriteAllBytes(path, jpg ? img.EncodeToJPG(92) : img.EncodeToPNG());
            RenderTexture.active = old;
            Destroy(img);
        }
    }
}
