using System;
using System.Collections;
using System.IO;
using HideAndSeek.Core;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    public partial class BoomboxTests
    {
        /// <summary>
        /// -testboombox (host + client in the lobby, + -boomshots dir): the host queues a track at volume 100. Every copy logs
        /// how long it took from the track change to the first sound and at which track position it started, records
        /// 12 s of its own mix at a wall-clock aligned moment (rec_host.wav / rec_client.wav, compared offline for the
        /// sync between copies) and measures the loudness. With -boomshots: loading screenshot + a burst of close-ups.
        /// </summary>
        private IEnumerator BoomboxTest()
        {
            yield return WaitLobby(2);
            bool host = NetworkManager.Singleton.IsServer;
            string who = host ? "host" : "client";
            string dir = ArgOr("-boomshots", "");
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var mm = MatchManager.Instance;
            var me = NetPlayer.Local;
            var bb = Boombox.Instance;
            // Both look at the boombox from a little to the side (the host closer).
            Vector3 stand = host ? new Vector3(4.75f, 0f, -3.75f) : new Vector3(6.6f, 0f, -3.2f);
            Vector3 l = bb.AimPoint - (stand + Vector3.up * 1.62f);
            me.Motor.Teleport(stand, 0f);
            me.Motor.SetLook(Mathf.Atan2(l.x, l.z) * Mathf.Rad2Deg, Mathf.Atan2(l.y, new Vector2(l.x, l.z).magnitude) * Mathf.Rad2Deg);
            yield return new WaitForSeconds(2f);

            if (host)
            {
                for (int i = 0; i < 4; i++) mm.ServerRadio(me, RadioOp.VolUp, null);
                mm.ServerRadio(me, RadioOp.Add, ArgOr("-radiourl", DefaultUrl));
                Debug.Log($"[BoomTest] host queued the track, wall={DateTime.UtcNow:HH:mm:ss.fff}");
            }
            while (mm.Radio.Value.Seq == 0) yield return null; // fresh session: the first track is seq 1
            float changed = Time.realtimeSinceStartup;
            Debug.Log($"[BoomTest] {who} track change seen, wall={DateTime.UtcNow:HH:mm:ss.fff}");
            bool shotLoading = false;
            while (!bb.MusicAudible && Time.realtimeSinceStartup - changed < 40f)
            {
                if (!shotLoading && Time.realtimeSinceStartup - changed > 1f && !string.IsNullOrEmpty(dir))
                {
                    shotLoading = true;
                    ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"loading_{who}.png"));
                }
                yield return null;
            }
            float delay = Time.realtimeSinceStartup - changed;
            var r = mm.Radio.Value;
            Debug.Log($"[BoomTest] {who} first sound after {delay:0.00}s, track position {bb.PlayheadSeconds:0.00}s, " +
                      $"server pos {(float)(mm.Now - r.StartTime):0.00}s, wall={DateTime.UtcNow:HH:mm:ss.fff} | {bb.DebugState}");

            // Record the mix at the same wall-clock moment on both copies (next multiple of 5 s, at least 4 s away).
            var tap = FindAnyObjectByType<AudioListener>()?.GetComponent<ListenerTap>() ?? FindAnyObjectByType<AudioListener>()?.gameObject.AddComponent<ListenerTap>();
            long now = DateTime.UtcNow.Ticks, step = TimeSpan.TicksPerSecond * 5;
            long at = (now / step + 2) * step;
            // Only the music in the recording: mute the voice playback (two copies on one PC hear each other's mic).
            foreach (var vp in FindObjectsByType<HideAndSeek.Game.Voice.VoicePlayback>(FindObjectsInactive.Exclude))
                if (vp.TryGetComponent<AudioSource>(out var vs)) vs.mute = true;
            while (DateTime.UtcNow.Ticks < at) yield return null;
            if (tap != null)
            {
                tap.BeginWindow();
                tap.StartRecording(12f);
            }
            Debug.Log($"[BoomTest] {who} recording from wall={new DateTime(at):HH:mm:ss.fff} (late {(DateTime.UtcNow.Ticks - at) / 10000.0:0.0} ms), track position {bb.PlayheadSeconds:0.00}s");
            for (int i = 0; i < 12; i++)
            {
                yield return new WaitForSeconds(1f);
                if (i == 3 && !string.IsNullOrEmpty(dir)) ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"playing_{who}.png"));
            }
            if (tap != null)
            {
                Debug.Log($"[BoomTest] {who} mix at volume {mm.Radio.Value.Volume}: rms={tap.WindowDb():0.0} dBFS peak={tap.WindowPeakDb():0.0} dBFS");
                tap.SaveRecording(Path.Combine(string.IsNullOrEmpty(dir) ? Application.persistentDataPath : dir, $"rec_{who}.wav"));
            }
            // Speaker motion: excursion every frame for 4 s - range and number of pushes ("bum").
            var ex = new System.Collections.Generic.List<float>();
            float t1 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t1 < 4f) { ex.Add(bb.ConeExcursion); yield return null; }
            var sorted = new System.Collections.Generic.List<float>(ex);
            sorted.Sort();
            float lo = sorted[sorted.Count / 10], hi = sorted[sorted.Count * 9 / 10], mid = (lo + hi) * 0.5f;
            int pushes = 0;
            for (int i = 1; i < ex.Count; i++) if (ex[i - 1] < mid && ex[i] >= mid) pushes++;
            Debug.Log($"[BoomTest] {who} cone excursion over 4 s ({ex.Count} frames): min={sorted[0] * 1000f:0.0}mm p10={lo * 1000f:0.0} " +
                      $"median={sorted[sorted.Count / 2] * 1000f:0.0} p90={hi * 1000f:0.0} max={sorted[sorted.Count - 1] * 1000f:0.0}mm, pushes over {mid * 1000f:0.0}mm: {pushes}");
            if (host && !string.IsNullOrEmpty(dir)) yield return CloseUps(dir);
            Debug.Log($"[BoomTest] {who} done | {bb.DebugState}");
        }

        /// <summary>Camera right in front of the boombox: 48 frames, 1/15 s apart (the speakers pumping).</summary>
        private IEnumerator CloseUps(string dir)
        {
            var bb = Boombox.Instance.transform;
            var go = new GameObject("BoomTestCam");
            var cam = go.AddComponent<Camera>();
            cam.depth = 100;
            cam.nearClipPlane = 0.02f;
            cam.fieldOfView = 50f;
            var views = new[] { (new Vector3(0f, 0.2f, 0.62f), 0), (new Vector3(-0.35f, 0.34f, 0.45f), 1), (new Vector3(0.1f, 0.16f, 0.32f), 2) };
            foreach (var (off, k) in views)
            {
                cam.transform.position = bb.TransformPoint(off);
                cam.transform.LookAt(bb.TransformPoint(new Vector3(k == 2 ? 0.17f : 0f, k == 2 ? 0.125f : 0.15f, 0.06f)));
                for (int i = 0; i < 16; i++)
                {
                    yield return new WaitForSeconds(1f / 15f);
                    ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"close{k}_{i:00}.png"));
                }
            }
            yield return new WaitForSeconds(0.2f);
            Destroy(go);
        }
    }
}
