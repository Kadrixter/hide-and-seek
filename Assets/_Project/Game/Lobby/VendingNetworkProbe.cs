using System.Collections;
using System.Globalization;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Opt-in test of the vending machine.
    /// -vendingtest: real purchase through the owner RPC, logs the replicated stages.
    /// -vendingshots &lt;folder&gt;: freezes the purchase at fixed timeline points and saves the real screen
    /// (first person, with the hands) as fp_T.png and a side view of the body as side_T.png.
    /// </summary>
    public sealed class VendingNetworkProbe : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var a = System.Environment.GetCommandLineArgs();
            if (System.Array.IndexOf(a, "-vendingtest") < 0 && System.Array.IndexOf(a, "-vendingshots") < 0 && System.Array.IndexOf(a, "-vendingwatch") < 0) return;
            var g = new GameObject("VendingNetworkProbe");
            DontDestroyOnLoad(g);
            g.AddComponent<VendingNetworkProbe>();
        }

        IEnumerator Start()
        {
            while (!NetPlayer.Local || !LobbyVending.Instance) yield return null;
            var p = NetPlayer.Local;
            var args = System.Environment.GetCommandLineArgs();
            int si = System.Array.IndexOf(args, "-vendingshots");
            if (si >= 0 && si + 1 < args.Length) { yield return Shots(args[si + 1]); yield break; }
            int wi = System.Array.IndexOf(args, "-vendingwatch");
            if (wi >= 0 && wi + 1 < args.Length) { yield return Watch(args[wi + 1]); yield break; }
            yield return new WaitForSeconds(1);
            p.Motor.Teleport(LobbyVending.Instance.transform.TransformPoint(new Vector3(-0.98f, 0f, 0.08f)), 90f);
            yield return new WaitForSeconds(1);
            p.RequestVendingRpc();
            Debug.Log("[VendingProbe] client requested purchase");
            int last = -1;
            for (float t = 0; t < 48; t += .25f)
            {
                var mm = MatchManager.Instance;
                int stage = mm.VendingCans.Count == 0 ? -1 : mm.VendingCans[0].Stage;
                if (stage != last) { Debug.Log("[VendingProbe] count=" + mm.VendingCans.Count + " stage=" + stage + " time=" + mm.Now + " position=" + (stage < 0 ? Vector3.zero : mm.VendingCans[0].Position)); last = stage; }
                yield return new WaitForSeconds(.25f);
            }
        }

        /// <summary>-vendingwatch &lt;folder&gt;: a second player stands beside the machine and photographs someone else's purchase.</summary>
        IEnumerator Watch(string dir)
        {
            System.IO.Directory.CreateDirectory(dir);
            var p = NetPlayer.Local;
            var vend = LobbyVending.Instance;
            yield return new WaitForSeconds(1);
            Vector3 spot = vend.transform.TransformPoint(new Vector3(-1.2f, 0f, -1.6f));
            Vector3 look = vend.transform.TransformPoint(new Vector3(-0.9f, 1.0f, 0.1f)) - (spot + Vector3.up * 1.6f);
            float yaw = Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Atan2(look.y, new Vector2(look.x, look.z).magnitude) * Mathf.Rad2Deg;
            p.Motor.Teleport(spot, yaw);
            p.Motor.SetLook(yaw, pitch);
            var mm = MatchManager.Instance;
            while (mm.VendingCans.Count == 0) { p.Motor.SetLook(yaw, pitch); yield return null; }
            float start = Time.time;
            int n = 0;
            while (Time.time - start < 13f)
            {
                p.Motor.SetLook(yaw, pitch);
                ScreenCapture.CaptureScreenshot(dir + "/watch_" + n.ToString("00") + ".png");
                n++;
                yield return new WaitForSeconds(0.6f);
            }
            Debug.Log("[VendingProbe] watch saved " + n);
            Application.Quit();
        }

        IEnumerator Shots(string dir)
        {
            System.IO.Directory.CreateDirectory(dir);
            var p = NetPlayer.Local;
            var vend = LobbyVending.Instance;
            yield return new WaitForSeconds(2);
            p.Motor.Teleport(vend.transform.TransformPoint(new Vector3(-0.98f, 0f, 0.08f)), 90f);
            p.Motor.SetLook(90f, 0f);
            yield return new WaitForSeconds(1);
            ScreenCapture.CaptureScreenshot(dir + "/fp_before.png");
            yield return new WaitForSeconds(0.3f);
            p.RequestVendingRpc();
            yield return new WaitForSeconds(0.5f);
            var mm = MatchManager.Instance;
            var cam = new GameObject("VendingShotCam").AddComponent<Camera>();
            cam.enabled = false;
            var rt = new RenderTexture(900, 900, 24);
            cam.targetTexture = rt;
            cam.fieldOfView = 52f;
            var light = new GameObject("VendingShotLight").AddComponent<Light>();
            light.type = LightType.Point; light.range = 6f; light.intensity = 1.4f;
            foreach (float t in new[] { 0.42f, 1.4f, 2.2f, 2.6f, 3.3f, 4.0f, 4.5f, 5.3f, 6.0f, 6.8f, 7.1f, 7.45f, 7.75f })
            {
                if (mm.VendingCans.Count == 0) break;
                // Hold the clock at t for a moment so the poses settle (weights blend in).
                for (float w = 0f; w < 0.6f; w += Time.deltaTime)
                {
                    var s = mm.VendingCans[0];
                    s.Started = mm.Now - t;
                    mm.VendingCans[0] = s;
                    yield return null;
                }
                string tag = t.ToString("0.00", CultureInfo.InvariantCulture);
                ScreenCapture.CaptureScreenshot(dir + "/fp_" + tag + ".png");
                yield return null;
                yield return new WaitForEndOfFrame();
                // Side view of the body (third person) from the player's right front.
                var tr = p.transform;
                cam.transform.position = vend.transform.TransformPoint(new Vector3(-0.6f, 1.45f, 1.9f));
                cam.transform.LookAt(tr.position + Vector3.up * 1.0f);
                light.transform.position = tr.TransformPoint(new Vector3(1.2f, 2.2f, 0.5f));
                p.Character.SetFirstPersonMode(false);
                // Held props switch their render mode on the next animation tick.
                for (int k = 0; k < 2; k++)
                {
                    var s2 = mm.VendingCans[0];
                    s2.Started = mm.Now - t;
                    mm.VendingCans[0] = s2;
                    yield return null;
                }
                cam.cullingMask = ~(1 << 9);
                cam.Render();
                Save(rt, dir + "/side_" + tag + ".png");
                p.Character.SetFirstPersonMode(true);
                Debug.Log("[VendingProbe] shot t=" + tag);
            }
            while (mm.VendingCans.Count > 0 && mm.VendingCans[0].Stage == 0) yield return null;
            yield return new WaitForSeconds(2.5f);
            ScreenCapture.CaptureScreenshot(dir + "/fp_after.png");
            yield return new WaitForSeconds(0.5f);
            Debug.Log("[VendingProbe] shots saved");
            Application.Quit();
        }

        static void Save(RenderTexture rt, string path)
        {
            var old = RenderTexture.active;
            RenderTexture.active = rt;
            var img = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            img.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            img.Apply();
            System.IO.File.WriteAllBytes(path, img.EncodeToPNG());
            RenderTexture.active = old;
            Destroy(img);
        }
    }
}
