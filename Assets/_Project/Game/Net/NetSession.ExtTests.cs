using System;
using System.Collections;
using HideAndSeek.Characters;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Extinguisher test: -testextinguisher on both copies (host -forcehider -autostart -hidetime N, client = seeker),
    /// own -port. Host walks to the level 0 bracket (prompt), takes the extinguisher, goes out in front of the seeker,
    /// pulls the pin, sprays him (foam: no attacks - the seeker tries the knife -, slowed, wiping), sprays again during
    /// the effect (extension, capped), again after it (immunity), empties the tank (sputter), drops it on the floor,
    /// takes it again and hangs it back on the bracket. -extshots &lt;dir&gt; = PNGs (first person of both, lit third-person
    /// views), -extframes = also a 15 fps sequence of both views for a video. Logs: [ExtTest].
    /// </summary>
    public partial class NetSession
    {
        private double _extT0;

        private void StartExtTests(Func<string, bool> has)
        {
            if (has("-testextinguisher")) StartCoroutine(ExtTest(has("-extframes")));
        }

        private IEnumerator ExtAt(float sec)
        {
            while (MatchManager.Instance != null && MatchManager.Instance.Now < _extT0 + sec) yield return null;
        }

        private static string ExtState0(MatchManager mm)
        {
            if (mm.Extinguishers.Count == 0) return "none";
            string s = "";
            for (int i = 0; i < mm.Extinguishers.Count; i++)
            {
                var e = mm.Extinguishers[i];
                s += $"[{i}: where={e.Where} holder={(e.Holder == MatchManager.NoExtHolder ? "-" : NetPlayer.Find(e.Holder)?.Nick)} pin={(e.Unlocked ? "out" : "in")} charge={e.Charge}] ";
            }
            return s;
        }

        private IEnumerator ExtTest(bool frames)
        {
            yield return WaitSeeking();
            var mm = MatchManager.Instance;
            _extT0 = mm.Now;
            var me = NetPlayer.Local;
            bool host = NetworkManager.Singleton.IsServer;
            string role = me.IsSeeker ? "seeker" : "hider";
            string dir = ArgOr("-extshots", "");
            NetPlayer Other() { foreach (var p in NetPlayer.All) if (p != null && p != me) return p; return null; }
            Debug.Log($"[ExtTest] {me.Nick} is {role} (host={host}); system={(ExtinguisherSystem.Instance != null)} {ExtState0(mm)}");

            Vector3 hook0;
            ExtinguisherSystem.HookPose(0, out hook0, out _);
            Vector3 front = new Vector3(73.5f, 0.05f, 49.4f), seekerAt = new Vector3(73.5f, 0.05f, 46.3f);
            if (role == "hider")
            {
                me.TeleportLocal(new Vector3(hook0.x - 1.05f, 0.05f, hook0.z - 0.2f), 90f);
                me.Motor.SetLook(90f, 24f);
                ItemLitShot(dir, "wall_l0", hook0 + new Vector3(-1.1f, 0.75f, -0.7f), hook0 + Vector3.up * 0.35f);
                ItemLitShot(dir, "wall_l0_close", hook0 + new Vector3(-0.45f, 0.75f, -0.3f), hook0 + Vector3.up * 0.55f);
            }
            if (role == "seeker") me.TeleportLocal(seekerAt, 0f);
            yield return ExtAt(1.5f);
            var lpc = LocalPlayerController.Instance;
            Debug.Log($"[ExtTest] {role} prompt: {lpc.Prompt} '{lpc.PromptText}'");
            if (role == "hider") { Shot(dir, "a_prompt"); me.RequestExtRpc(MatchManager.ExtTake, 0); }
            if (frames && role == "hider") StartCoroutine(ExtFrames(dir, "take", 0.4f, 1.6f, null));
            yield return ExtAt(2.0f);
            if (role == "hider") Shot(dir, "b_take_fpp");
            yield return ExtAt(3.0f);
            Debug.Log($"[ExtTest] {role} after take: {ExtState0(mm)} holds={me.HoldsExtinguisher}");
            if (role == "hider") Shot(dir, "c_hold_fpp");

            // ---- out in front of the seeker
            if (role == "hider") { me.TeleportLocal(front, 180f); me.Motor.SetLook(180f, -4f); }
            yield return ExtAt(4.0f);
            if (role == "hider")
            {
                ItemLitShot(dir, "d_hold_tpp", me.transform.position + new Vector3(1.6f, 1.4f, -1.2f), me.transform.position + Vector3.up * 0.95f);
                LocalPlayerController.TestExtSpray = true;   // first click: the pin
            }
            if (frames) StartCoroutine(ExtFrames(dir, role, 0.0f, 9.5f, role == "hider" ? Other() : me));
            yield return ExtAt(4.6f);
            if (role == "hider") { Shot(dir, "e_pin_fpp"); ItemLitShot(dir, "e_pin_tpp", me.transform.position + new Vector3(-1.3f, 1.5f, -1.0f), me.transform.position + Vector3.up * 1.0f); }
            yield return ExtAt(6.2f);
            if (role == "hider") Shot(dir, "f_spray_fpp");
            if (role == "hider") ItemLitShot(dir, "f_spray_tpp", me.transform.position + new Vector3(2.6f, 1.6f, -1.4f), me.transform.position + new Vector3(0f, 1.0f, -1.6f));
            yield return ExtAt(6.8f);
            if (role == "hider") LocalPlayerController.TestExtSpray = false;
            Debug.Log($"[ExtTest] {role} after first spray: {ExtState0(mm)}");
            var seeker = role == "seeker" ? me : Other();
            yield return ExtAt(7.2f);
            Debug.Log($"[ExtTest] {role} seeker foamed={seeker.Foamed} left={seeker.FoamLeft:0.00} blocks={seeker.FoamBlocksAttack} speed={seeker.FoamSpeedScale:0.00}");
            if (role == "seeker")
            {
                Shot(dir, "g_foam_fpp_0");
                var h = Other();
                if (h != null) me.RequestKnifeRpc(h.NetworkObjectId); // must be refused
            }
            if (role == "hider") ItemLitShot(dir, "g_wipe_tpp_0", seeker.transform.position + seeker.transform.forward * 1.6f + new Vector3(0.6f, 1.6f, 0f), seeker.transform.position + Vector3.up * 1.4f);
            yield return ExtAt(8.2f);
            if (role == "seeker") Shot(dir, "g_foam_fpp_1");
            if (role == "hider") ItemLitShot(dir, "g_wipe_tpp_1", seeker.transform.position + seeker.transform.forward * 1.5f + new Vector3(-0.7f, 1.5f, 0f), seeker.transform.position + Vector3.up * 1.4f);
            if (role == "hider")
            {
                Debug.Log($"[ExtTest] hider after the seeker's knife try: limping={me.Limping.Value} life={me.Life.Value}");
            }
            yield return ExtAt(9.2f);
            if (role == "seeker") Shot(dir, "g_foam_fpp_2");
            if (role == "hider") { ItemLitShot(dir, "g_wipe_tpp_2", seeker.transform.position + seeker.transform.forward * 1.4f + new Vector3(0.2f, 1.7f, 0f), seeker.transform.position + Vector3.up * 1.45f); LocalPlayerController.TestExtSpray = true; }
            yield return ExtAt(9.8f);
            if (role == "hider") LocalPlayerController.TestExtSpray = false;
            yield return ExtAt(10.2f);
            Debug.Log($"[ExtTest] {role} after extension spray: foamed={seeker.Foamed} total={(seeker.FoamUntil.Value - seeker.FoamStart.Value):0.00} (max {ExtTuning.FoamMax})");
            if (role == "seeker") Shot(dir, "g_foam_fpp_3");
            yield return ExtAt(11.6f);
            if (role == "seeker") Shot(dir, "g_foam_fpp_4");
            yield return ExtAt(13.6f);
            Debug.Log($"[ExtTest] {role} after the effect: foamed={seeker.Foamed} blocks={seeker.FoamBlocksAttack} speed={seeker.FoamSpeedScale:0.00}");

            // ---- immunity, then empty the tank
            if (role == "hider") LocalPlayerController.TestExtSpray = true;
            yield return ExtAt(15f);
            Debug.Log($"[ExtTest] {role} spraying during immunity: foamed={seeker.Foamed} {ExtState0(mm)}");
            if (role == "hider") me.Motor.SetLook(150f, -10f); // away from the seeker
            yield return ExtAt(23f);
            if (role == "hider") { LocalPlayerController.TestExtSpray = false; Shot(dir, "h_empty_fpp"); }
            Debug.Log($"[ExtTest] {role} after emptying: {ExtState0(mm)} cloudAtSeeker={ExtinguisherSpray.Density(seeker.transform.position + Vector3.up * 1.5f):0.00}");

            // ---- put down on the floor, take it again, hang it back on its bracket
            if (role == "hider") LocalPlayerController.TestExtDrop = true;
            yield return ExtAt(24.5f);
            Debug.Log($"[ExtTest] {role} after drop: {ExtState0(mm)}");
            if (role == "hider" && mm.Extinguishers.Count > 0)
            {
                var e = mm.Extinguishers[0];
                ItemLitShot(dir, "i_floor", e.Pos + new Vector3(0.9f, 0.9f, 0.7f), e.Pos + Vector3.up * 0.1f);
                me.RequestExtRpc(MatchManager.ExtTake, 0);
            }
            yield return ExtAt(26f);
            Debug.Log($"[ExtTest] {role} after floor pickup: {ExtState0(mm)}");
            if (role == "hider") { me.TeleportLocal(new Vector3(hook0.x - 1.0f, 0.05f, hook0.z), 90f); }
            yield return ExtAt(26.8f);
            if (role == "hider") LocalPlayerController.TestExtDrop = true;
            yield return ExtAt(28f);
            Debug.Log($"[ExtTest] {role} after hang back: {ExtState0(mm)}");
            if (role == "hider") ItemLitShot(dir, "j_back_on_wall", hook0 + new Vector3(-1.1f, 0.75f, -0.7f), hook0 + Vector3.up * 0.35f);
            Debug.Log($"[ExtTest] {role} done");
        }

        /// <summary>15 fps PNG sequence: the own screen (first person), plus a lit third-person view of <paramref name="watch"/>.</summary>
        private IEnumerator ExtFrames(string dir, string name, float from, float to, NetPlayer watch)
        {
            if (string.IsNullOrEmpty(dir)) yield break;
            string fd = System.IO.Path.Combine(dir, "frames_" + name);
            System.IO.Directory.CreateDirectory(fd);
            yield return ExtAt(from);
            int i = 0;
            float next = 0f;
            var me = NetPlayer.Local;
            while (MatchManager.Instance != null && MatchManager.Instance.Now < _extT0 + to)
            {
                if (Time.unscaledTime >= next)
                {
                    next = Time.unscaledTime + 1f / 15f;
                    ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(fd, $"fp_{i:0000}.png"));
                    if (watch != null)
                    {
                        Vector3 a = (me.transform.position + watch.transform.position) * 0.5f + Vector3.up * 1.1f;
                        Vector3 side = Vector3.Cross(watch.transform.position - me.transform.position, Vector3.up).normalized;
                        ItemLitShot(fd, $"tp_{i:0000}", a + side * 3.4f + Vector3.up * 0.7f, a);
                    }
                    i++;
                }
                yield return null;
            }
            Debug.Log($"[ExtTest] {name}: {i} frames in {fd}");
        }
    }
}
