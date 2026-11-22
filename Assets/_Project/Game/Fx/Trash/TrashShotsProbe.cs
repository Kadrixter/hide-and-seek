using System.Collections;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Test only: -trashshots &lt;folder&gt; renders the lobby trash bin from fixed close-up views (side, top, 3/4, rim,
    /// papers on the floor, player's eye) with the real lobby lighting, saves bin_*.png and quits.
    /// With -autostart (host, -hidetime N) it skips the lobby and photographs the bins of the security rooms in the
    /// game instead (sec_N_*.png, lit by a test lamp next to the camera: the rooms are dark).
    /// </summary>
    public sealed class TrashShotsProbe : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-trashshots") < 0) return;
            var g = new GameObject("TrashShotsProbe");
            DontDestroyOnLoad(g);
            g.AddComponent<TrashShotsProbe>();
        }

        IEnumerator Start()
        {
            var args = System.Environment.GetCommandLineArgs();
            int si = System.Array.IndexOf(args, "-trashshots");
            string dir = si + 1 < args.Length ? args[si + 1] : "trashshots";
            System.IO.Directory.CreateDirectory(dir);
            if (System.Array.IndexOf(args, "-autostart") >= 0) { yield return Security(dir); yield break; }
            GameObject bin = null;
            while (!NetPlayer.Local || (bin = GameObject.Find("TrashBin")) == null) yield return null;
            // Player out of the way (centre of the room, facing west).
            NetPlayer.Local.Motor.Teleport(new Vector3(-2f, 0f, 1f), 270f);
            yield return new WaitForSeconds(2.5f);

            // Views are relative to the bin body; the papers spill west (-x, into the room).
            Vector3 c = bin.transform.position;
            foreach (var r in bin.GetComponentsInChildren<Renderer>())
                if (r.name.StartsWith("Bin")) { c = r.bounds.center; break; }
            c.y = 0f;

            var cam = new GameObject("TrashShotCam").AddComponent<Camera>();
            cam.enabled = false;
            cam.nearClipPlane = 0.02f;
            var rt = new RenderTexture(1200, 900, 24) { antiAliasing = 8 };
            cam.targetTexture = rt;
            void View(string name, Vector3 from, Vector3 at, float fov)
            {
                cam.fieldOfView = fov;
                cam.transform.position = c + from;
                cam.transform.LookAt(c + at);
                cam.Render();
                Save(rt, dir + "/bin_" + name + ".png");
            }
            View("1_side", new Vector3(-1.15f, 0.42f, 0.05f), new Vector3(-0.15f, 0.2f, 0f), 40f);
            View("2_34", new Vector3(-0.85f, 0.85f, -0.75f), new Vector3(-0.08f, 0.2f, 0f), 45f);
            View("3_top", new Vector3(-0.32f, 1.15f, 0.06f), new Vector3(0f, 0.3f, 0f), 42f);
            View("4_rim", new Vector3(-0.38f, 0.56f, -0.24f), new Vector3(0f, 0.37f, 0f), 38f);
            View("5_papers", new Vector3(-0.95f, 0.5f, -0.55f), new Vector3(-0.38f, 0.03f, 0.05f), 48f);
            View("6_eye", new Vector3(-1.8f, 1.62f, 0.5f), new Vector3(-0.2f, 0.15f, 0f), 60f);
            View("7_low", new Vector3(-0.55f, 0.12f, -0.45f), new Vector3(0f, 0.18f, 0f), 50f);
            Debug.Log("[TrashShots] saved to " + dir);
            yield return null;
            Application.Quit();
        }

        IEnumerator Security(string dir)
        {
            while (SecurityRooms.Instance == null || FindObjectsByType<TrashBinProp>().Length == 0) yield return null;
            yield return new WaitForSeconds(2f);
            var cam = new GameObject("TrashShotCam").AddComponent<Camera>();
            cam.enabled = false;
            cam.nearClipPlane = 0.02f;
            cam.fieldOfView = 50f;
            var rt = new RenderTexture(1200, 900, 24) { antiAliasing = 8 };
            cam.targetTexture = rt;
            var lamp = new GameObject("TrashShotLamp").AddComponent<Light>();
            lamp.type = LightType.Point; lamp.range = 5f; lamp.intensity = 1.3f; lamp.color = new Color(1f, 0.95f, 0.88f);
            int n = 0;
            foreach (var b in FindObjectsByType<TrashBinProp>())
            {
                var t = b.transform;
                void View(string name, Vector3 from, Vector3 at)
                {
                    cam.transform.position = t.TransformPoint(from);
                    cam.transform.LookAt(t.TransformPoint(at));
                    lamp.transform.position = cam.transform.position + cam.transform.right * 0.4f + Vector3.up * 0.3f;
                    cam.Render();
                    Save(rt, dir + "/sec_" + n + "_" + name + ".png");
                }
                View("front", new Vector3(0.3f, 0.75f, 1.1f), new Vector3(0f, 0.18f, 0.15f));
                View("top", new Vector3(0.05f, 1.1f, 0.35f), new Vector3(0f, 0.3f, 0f));
                n++;
            }
            Debug.Log("[TrashShots] security bins: " + n);
            yield return null;
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
