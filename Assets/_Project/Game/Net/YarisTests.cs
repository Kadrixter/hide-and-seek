using System;
using System.Collections;
using System.IO;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Yaris easter egg tests. Host: -testyaris (100 % Yarises) with -forcehider -autostart -hidetime N and a seeker
    /// client; -yarisshots &lt;dir&gt; = PNGs: the cars on the shelf, the car in the hand (first person + side camera),
    /// put down and driven, squashing while driven (a burst through two beats) and frames for a short clip
    /// (dir/frames), then parked (must go still and silent), driven again (music resumes) and parked again.
    /// Client: -yariswatch logs the synced state, the dance and the squash scale once a second and photographs the
    /// dancing (remotely driven) car from its side. Logs: [YarisTest] / [YarisWatch].
    /// </summary>
    public class YarisTests : MonoBehaviour
    {
        private static string[] Args => Environment.GetCommandLineArgs();
        private static bool Has(string a) => Array.IndexOf(Args, a) >= 0;
        private static string ArgOr(string name, string def)
        {
            int i = Array.IndexOf(Args, name);
            return i >= 0 && i + 1 < Args.Length ? Args[i + 1] : def;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            bool test = Has("-testyaris") && Has("-yarisshots"), watch = Has("-yariswatch");
            if (!test && !watch) return;
            var go = new GameObject("[YarisTests]");
            DontDestroyOnLoad(go);
            var t = go.AddComponent<YarisTests>();
            if (test) t.StartCoroutine(t.HostTest(ArgOr("-yarisshots", "")));
            if (watch) t.StartCoroutine(t.Watch(ArgOr("-yarisshots", "")));
        }

        private static string State(MatchManager mm, int i)
        {
            var car = RcCar.Peek(i);
            var skin = car != null ? car.Parts.Root.GetComponent<RcCarSkin>() : null;
            var fx = car != null ? car.GetComponent<YarisCarFx>() : null;
            string sc = skin != null && skin.Yaris != null ? skin.Yaris.Squash.localScale.ToString("F2") : "-";
            return $"car{i}: slot={mm.RcSlot(i).Mode} view={(car != null ? car.View.ToString() : "-")} yaris={(skin != null && skin.IsYaris)} " +
                   $"dance={(fx != null ? fx.Dance : 0f):0.00} music={(fx != null && fx.MusicPlaying)} squash={sc} bpm={YarisMusic.Bpm:0.0}";
        }

        private IEnumerator HostTest(string dir)
        {
            var mm = MatchManager.Instance;
            while (mm == null || mm.Phase.Value != MatchPhase.Seeking || NetPlayer.Local == null) { mm = MatchManager.Instance; yield return null; }
            yield return new WaitForSeconds(1.5f);
            var me = NetPlayer.Local;
            Debug.Log($"[YarisTest] round yaris={mm.YarisRound.Value} active={YarisEgg.Active} hider={me.IsHider} | {State(mm, 0)} | {State(mm, 1)}");
            if (!me.IsHider) { Debug.Log("[YarisTest] need the host as hider (-forcehider)"); yield break; }
            Light(RcCarShelf.Position + new Vector3(-1.4f, 2.4f, 0f), 7f, 1.5f);
            Light(new Vector3(-14.6f, -2.2f, 54.6f), 6f, 1.6f);

            // 1. Shelf.
            var shelfRot = Quaternion.Euler(0f, RcCarShelf.Yaw, 0f);
            me.Motor.Teleport(RcCarShelf.Position + shelfRot * new Vector3(-0.3f, 0f, -1.15f), RcCarShelf.Yaw);
            Vector3 aim = RcCarShelf.CarPos(0) + Vector3.up * 0.1f - (me.transform.position + Vector3.up * 1.62f);
            me.Motor.SetLook(RcCarShelf.Yaw, Mathf.Atan2(aim.y, new Vector2(aim.x, aim.z).magnitude) * Mathf.Rad2Deg);
            yield return new WaitForSeconds(1f);
            Vector3 mid = (RcCarShelf.CarPos(0) + RcCarShelf.CarPos(1)) * 0.5f + Vector3.up * 0.08f;
            Cam(dir, "01_shelf", mid + shelfRot * new Vector3(0.25f, 0.28f, -0.85f), mid, 50f);
            Cam(dir, "02_shelf_close", RcCarShelf.CarPos(0) + Vector3.up * 0.09f + shelfRot * new Vector3(0.2f, 0.12f, -0.5f), RcCarShelf.CarPos(0) + Vector3.up * 0.08f, 40f);
            Shot(dir, "03_shelf_fpp");
            yield return new WaitForSeconds(0.3f);

            // 2. In the hand.
            mm.ServerRcPickup(me, 0);
            yield return new WaitForSeconds(0.6f);
            Hotbar.TestSelect = Hotbar.SlotOf(me, HotbarItem.RcCar);
            me.Motor.Teleport(new Vector3(-13.6f, -3.4f, 54.6f), 270f);
            me.Motor.SetLook(270f, 12f);
            yield return new WaitForSeconds(2.6f);
            Debug.Log($"[YarisTest] in hand: hand={me.HandItem} | {State(mm, 0)}");
            Shot(dir, "04_hand_fpp");
            yield return new WaitForSeconds(0.3f);

            // 3. Put down and drive.
            LocalPlayerController.TestSix = true;
            yield return new WaitForSeconds(2.6f);
            LocalPlayerController.TestRcActive = true;
            LocalPlayerController.TestRcInput = new Vector2(0.35f, 0.6f);
            yield return new WaitForSeconds(1.2f);
            LocalPlayerController.TestRcInput = Vector2.zero;
            yield return new WaitForSeconds(0.8f);
            Debug.Log($"[YarisTest] driving: cam={me.RcCameraOn} | {State(mm, 0)}");
            Shot(dir, "06_drive_carcam");
            yield return new WaitForSeconds(0.5f);
            Check("driving", mm, true);

            // 4. Squash burst while driven (the car stands, the driver holds the remote) from the front three-quarter.
            var car = RcCar.Peek(0);
            Vector3 cp = car.transform.position + Vector3.up * 0.09f;
            Quaternion cr = car.transform.rotation;
            for (int i = 0; i < 10; i++)
            {
                yield return new WaitForEndOfFrame();
                Cam(dir, $"07_squash_{i:00}", cp + cr * new Vector3(0.55f, 0.16f, 0.55f), cp, 42f);
                Debug.Log($"[YarisTest] squash {i}: {State(mm, 0)}");
                yield return new WaitForSeconds(0.1f);
            }
            Cam(dir, "08_side", cp + cr * new Vector3(0.85f, 0.1f, 0f), cp, 40f);
            Cam(dir, "09_rear34", cp + cr * new Vector3(-0.5f, 0.22f, -0.6f), cp, 42f);
            // 5. Frames for a clip (about 4 s at 30 fps, rendered as fast as they come).
            if (!string.IsNullOrEmpty(dir))
            {
                string fdir = Path.Combine(dir, "frames");
                Directory.CreateDirectory(fdir);
                float t0 = Time.time;
                int f = 0;
                while (Time.time - t0 < 4.2f && f < 140)
                {
                    yield return new WaitForEndOfFrame();
                    float want = f / 30f;
                    if (Time.time - t0 + 1e-4f < want) continue;
                    Cam(fdir, $"f{f:000}", cp + cr * new Vector3(0.6f, 0.2f, 0.5f), cp, 40f, 640, 400);
                    f++;
                }
                Debug.Log($"[YarisTest] {f} clip frames in {Time.time - t0:0.0} s");
            }

            // 5b. The driver gets out: dance and music fade out (~0.4 s), the song pauses. Then a second ride resumes it.
            LocalPlayerController.TestRcActive = false;
            LocalPlayerController.TestSix = true; // park, stand up
            yield return new WaitForSeconds(0.2f);
            Debug.Log($"[YarisTest] getting out (fading): {State(mm, 0)}");
            yield return new WaitForSeconds(1.8f);
            Check("parked", mm, false);
            Cam(dir, "09b_parked_still", cp + cr * new Vector3(0.55f, 0.16f, 0.55f), cp, 42f);
            LocalPlayerController.TestSix = true; // sit down and drive again
            LocalPlayerController.TestRcActive = true;
            yield return new WaitForSeconds(1.6f);
            Check("driving again", mm, true);
            LocalPlayerController.TestRcActive = false;
            LocalPlayerController.TestSix = true;
            yield return new WaitForSeconds(2f);
            Check("parked again", mm, false);
            // 6. The seeker stomps on it: dance stops, the music winds down, the wreck keeps the Yaris body.
            NetPlayer seeker = null;
            foreach (var p in NetPlayer.All) if (p != null && p.IsSeeker) seeker = p;
            if (seeker != null)
            {
                Vector3 at = car.transform.position;
                Vector3 from = at + new Vector3(1.2f, 0f, 0.3f);
                seeker.TeleportRpc(from, Quaternion.LookRotation(at - from).eulerAngles.y);
                yield return new WaitForSeconds(0.6f);
                mm.ServerRcStomp(seeker, 0);
                yield return new WaitForSeconds(0.3f);
                Debug.Log($"[YarisTest] stomp: {State(mm, 0)}");
                yield return new WaitForSeconds(1.6f);
                Debug.Log($"[YarisTest] wreck: {State(mm, 0)}");
                Cam(dir, "10_wreck", cp + cr * new Vector3(0.5f, 0.25f, 0.5f), cp - Vector3.up * 0.04f, 42f);
            }
            Debug.Log($"[YarisTest] done | {State(mm, 0)} | {State(mm, 1)}");
        }

        /// <summary>Logs whether car 0 dances / plays music as expected (driven = on, otherwise off).</summary>
        private static void Check(string what, MatchManager mm, bool want)
        {
            var car = RcCar.Peek(0);
            var fx = car != null ? car.GetComponent<YarisCarFx>() : null;
            bool dance = fx != null && fx.Dance > 0.5f, music = fx != null && fx.MusicPlaying;
            bool ok = dance == want && (music == want || YarisMusic.Clip == null);
            Debug.Log($"[YarisTest] check {what}: want {(want ? "dance+music" : "still+silent")} -> {(ok ? "OK" : "FAIL")} | {State(mm, 0)}");
        }

        private IEnumerator Watch(string dir)
        {
            var mm = MatchManager.Instance;
            while (mm == null || mm.Phase.Value != MatchPhase.Seeking) { mm = MatchManager.Instance; yield return null; }
            bool shot = false, handShot = false;
            Light(new Vector3(-14.6f, -2.2f, 54.6f), 6f, 1.6f);
            Light(RcCarShelf.Position + new Vector3(-1.4f, 2.4f, 0f), 7f, 1.5f);
            Light(new Vector3(-15.2f, -1.9f, 55.2f), 5f, 1.4f);
            float nextLog = 0f;
            for (float t = 0f; t < 40f; t += Time.deltaTime)
            {
                yield return null;
                if (t >= nextLog)
                {
                    nextLog = t + 1f;
                    Debug.Log($"[YarisWatch] round yaris={mm.YarisRound.Value} active={YarisEgg.Active} | {State(mm, 0)} | {State(mm, 1)}");
                }
                if (string.IsNullOrEmpty(dir)) continue;
                var slot = mm.RcSlot(0);
                var holder = NetPlayer.Find(slot.Holder);
                if (!handShot && slot.Mode == RcMode.Carried && holder != null && !holder.IsOwner && holder.HandItem == HotbarItem.RcCar)
                {
                    handShot = true;
                    yield return new WaitForSeconds(0.5f);
                    Transform ht = holder.transform;
                    Transform carried = null;
                    foreach (var t2 in ht.GetComponentsInChildren<Transform>()) if (t2.name == "Carried") carried = t2;
                    Vector3 focus = carried != null ? carried.position : ht.position + Vector3.up * 0.9f;
                    // the host test stands facing west on open floor: look from the west, a little to the side
                    Cam(dir, "watch_hand_tpp", focus + new Vector3(-1.5f, 0.45f, 0.75f), focus + Vector3.up * 0.1f, 40f);
                    Cam(dir, "watch_hand_close", focus + new Vector3(-0.6f, 0.15f, 0.35f), focus, 40f);
                }
                var car = RcCar.Peek(0);
                if (!shot && car != null && car.View == RcCar.ViewMode.Remote)
                {
                    shot = true;
                    yield return new WaitForSeconds(1.2f);
                    Vector3 cp = car.transform.position + Vector3.up * 0.09f;
                    for (int k = 0; k < 6; k++)
                    {
                        yield return new WaitForEndOfFrame();
                        Cam(dir, $"watch_{k:00}", cp + car.transform.rotation * new Vector3(-0.6f, 0.2f, 0.5f), cp, 42f);
                        yield return new WaitForSeconds(0.09f);
                    }
                }
            }
        }

        // ================================================================ helpers

        private static void Light(Vector3 pos, float range, float intensity)
        {
            var l = new GameObject("YarisTestLight").AddComponent<Light>();
            l.type = LightType.Point;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.Soft;
            l.transform.position = pos;
        }

        private static void Shot(string dir, string name)
        {
            if (string.IsNullOrEmpty(dir)) return;
            Directory.CreateDirectory(dir);
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, name + ".png"));
        }

        private static void Cam(string dir, string name, Vector3 pos, Vector3 at, float fov, int w = 1280, int h = 720)
        {
            if (string.IsNullOrEmpty(dir)) return;
            Directory.CreateDirectory(dir);
            var go = new GameObject("YarisTestCam");
            var cam = go.AddComponent<Camera>();
            cam.enabled = false;
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.02f;
            cam.cullingMask = ~(1 << 9);
            go.transform.position = pos;
            go.transform.LookAt(at);
            var rt = new RenderTexture(w, h, 24) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            cam.targetTexture = null;
            Destroy(tex);
            rt.Release();
            Destroy(rt);
            Destroy(go);
        }
    }
}
