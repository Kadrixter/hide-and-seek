using System.Collections;
using System.Globalization;
using HideAndSeek.Game.Net;
using HideAndSeek.Player;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Opt-in test of being drunk: <c>-host -nodiscovery -nosteam -nodiscord -testdrunk N &lt;folder&gt;</c>.
    /// Takes sober reference shots, buys and drinks N cans at the lobby vending machine (real purchases), then records
    /// frame sequences: first person standing and walking in a slow circle (real screen, with the sway, the double
    /// image and the hands: fp_stand_XX / fp_walk_XX) and the body from outside (third person, the same animation other
    /// players see: tpp_stand_XX / tpp_walk_XX). Logs the level and intensity, then quits.
    /// </summary>
    public sealed class DrunkProbe : MonoBehaviour
    {
        int cans = 3;
        string dir;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var a = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(a, "-testdrunk");
            if (i < 0) return;
            var g = new GameObject("DrunkProbe");
            DontDestroyOnLoad(g);
            var p = g.AddComponent<DrunkProbe>();
            int k = i + 1;
            if (k < a.Length && int.TryParse(a[k], NumberStyles.Integer, CultureInfo.InvariantCulture, out int n)) { p.cans = Mathf.Clamp(n, 0, 8); k++; }
            p.dir = k < a.Length && !a[k].StartsWith("-") ? a[k] : System.IO.Path.Combine(Application.persistentDataPath, "drunktest");
        }

        IEnumerator Start()
        {
            while (!NetPlayer.Local || !LobbyVending.Instance || MatchManager.Instance == null || !MatchManager.Instance.IsSpawned) yield return null;
            System.IO.Directory.CreateDirectory(dir);
            var p = NetPlayer.Local;
            var vend = LobbyVending.Instance;
            yield return new WaitForSeconds(2f);
            Debug.Log("[DrunkProbe] start, cans=" + cans + " dir=" + dir);

            Vector3 open = vend.transform.TransformPoint(new Vector3(-3.2f, 0f, 2.6f));
            p.Motor.Teleport(open, vend.transform.eulerAngles.y + 90f);
            yield return new WaitForSeconds(1f);
            yield return Record(p, "sober", 1, 0.1f);
            yield return RecordBody(p, "sober", 1, 0.1f, false);

            for (int c = 0; c < cans; c++)
            {
                p.Motor.Teleport(vend.transform.TransformPoint(new Vector3(-0.98f, 0f, 0.08f)), 90f);
                yield return new WaitForSeconds(0.5f);
                while (!vend.Free) yield return null;
                p.RequestVendingRpc();
                float wait = 0f;
                while (!p.Busy.Value && wait < 3f) { wait += Time.deltaTime; yield return null; }
                while (p.Busy.Value) yield return null;
                yield return new WaitForSeconds(0.6f);
                Debug.Log("[DrunkProbe] can " + (c + 1) + " level=" + p.DrunkLevel.ToString("0.00", CultureInfo.InvariantCulture)
                          + " intensity=" + p.DrunkIntensity.ToString("0.00", CultureInfo.InvariantCulture));
            }

            // Let it kick in (the view eases in over a few seconds).
            p.Motor.Teleport(open, vend.transform.eulerAngles.y + 90f);
            yield return new WaitForSeconds(6f);
            var bob = p.GetComponentInChildren<HeadBob>();
            Debug.Log("[DrunkProbe] drunk level=" + p.DrunkLevel.ToString("0.00", CultureInfo.InvariantCulture)
                      + " intensity=" + p.DrunkIntensity.ToString("0.00", CultureInfo.InvariantCulture)
                      + " shown=" + (bob != null ? bob.DrunkWeight : -1f).ToString("0.00", CultureInfo.InvariantCulture));

            yield return Record(p, "stand", 20, 0.4f);
            p.Motor.CommandProvider = () => new MotorCommand { Move = new Vector2(0f, 1f), Look = new Vector2(32f * Time.deltaTime, 0f) };
            yield return Record(p, "walk", 20, 0.4f);
            p.Motor.CommandProvider = null;
            yield return new WaitForSeconds(0.5f);

            yield return RecordBody(p, "stand", 40, 0.2f, false);
            p.Motor.CommandProvider = () => new MotorCommand { Move = new Vector2(0f, 1f), Look = new Vector2(32f * Time.deltaTime, 0f) };
            yield return RecordBody(p, "walk", 40, 0.2f, true);
            p.Motor.CommandProvider = null;

            Debug.Log("[DrunkProbe] done, level=" + p.DrunkLevel.ToString("0.00", CultureInfo.InvariantCulture)
                      + " intensity=" + p.DrunkIntensity.ToString("0.00", CultureInfo.InvariantCulture));
            yield return new WaitForSeconds(0.5f);
            Application.Quit();
        }

        /// <summary>First person: the real screen (camera sway, double image, hands).</summary>
        IEnumerator Record(NetPlayer p, string tag, int frames, float every)
        {
            for (int i = 0; i < frames; i++)
            {
                ScreenCapture.CaptureScreenshot(dir + "/fp_" + tag + "_" + i.ToString("00") + ".png");
                yield return new WaitForSeconds(every);
            }
        }

        /// <summary>Third person: the body from the front-right, following the player (what others see).</summary>
        IEnumerator RecordBody(NetPlayer p, string tag, int frames, float every, bool follow)
        {
            var cam = new GameObject("DrunkShotCam").AddComponent<Camera>();
            cam.enabled = false;
            cam.fieldOfView = 45f;
            cam.cullingMask = ~(1 << 9);
            var rt = new RenderTexture(960, 960, 24) { antiAliasing = 4 };
            cam.targetTexture = rt;
            var light = new GameObject("DrunkShotLight").AddComponent<Light>();
            light.type = LightType.Point; light.range = 7f; light.intensity = 1.3f;
            p.Character.SetFirstPersonMode(false);
            yield return null;
            Vector3 anchor = p.transform.position;
            Quaternion facing = p.transform.rotation;
            for (int i = 0; i < frames; i++)
            {
                var tr = p.transform;
                if (follow) { anchor = tr.position; facing = tr.rotation; }
                cam.transform.position = anchor + facing * new Vector3(1.6f, 1.35f, 2.4f);
                cam.transform.LookAt(anchor + Vector3.up * 0.95f);
                light.transform.position = anchor + facing * new Vector3(1.2f, 2.4f, 1.2f);
                cam.Render();
                Save(rt, dir + "/tpp_" + tag + "_" + i.ToString("00") + ".png");
                yield return new WaitForSeconds(every);
            }
            p.Character.SetFirstPersonMode(true);
            Destroy(light.gameObject);
            Destroy(cam.gameObject);
            rt.Release();
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
