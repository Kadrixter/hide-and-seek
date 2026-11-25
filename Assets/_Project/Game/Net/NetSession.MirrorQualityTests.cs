using System.Collections;
using HideAndSeek.Game.Gfx;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// -testmirrorq &lt;dir&gt; (one window, -host, best with -fps 60): stands in front of the lobby mirror and goes through
    /// the "Lustro" levels high / medium / low / off (test override, the settings file is not touched). Per level: a
    /// screenshot, the mirror's state (render texture, rendered part, camera) and the frame cost (frame time, CPU / GPU
    /// time from FrameTimingManager when available, reflection renders per second and their CPU time). Also a shot
    /// while turning at the low level (a reused reflection must not slide on the glass). Logs: [MirrorQ] ...
    /// </summary>
    public partial class NetSession
    {
        private void StartMirrorQualityTests(System.Func<string, bool> has)
        {
            if (has("-testmirrorq")) StartCoroutine(MirrorQualityTest(ArgOr("-testmirrorq", "")));
        }

        private IEnumerator MirrorQualityTest(string dir)
        {
            MatchManager mm = null;
            while (mm == null || mm.Phase.Value != MatchPhase.Lobby || NetPlayer.Local == null) { mm = MatchManager.Instance; yield return null; }
            yield return new WaitForSeconds(3f);
            var me = NetPlayer.Local;
            var mirror = FindAnyObjectByType<PlanarMirror>();
            Debug.Log($"[MirrorQ] mirror={(mirror != null)} timingStats={FrameTimingManager.IsFeatureEnabled()} screen={Screen.width}x{Screen.height} targetFps={Application.targetFrameRate}");
            if (mirror == null) yield break;

            Vector3 mp = mirror.transform.position;
            Vector3 stand = new Vector3(mp.x + 0.5f, 0.02f, mp.z + 2.4f);
            Vector3 to = mp + new Vector3(0f, -0.15f, 0f) - (stand + Vector3.up * 1.62f);
            float yaw = YawTo(to), pitch = PitchTo(to);
            me.Motor.Teleport(stand, yaw, pitch);
            me.Motor.SetLook(yaw, pitch);
            yield return new WaitForSeconds(1f);

            string[] names = { "off", "low", "medium", "high" };
            foreach (int level in new[] { MirrorSettings.High, MirrorSettings.Medium, MirrorSettings.Low, MirrorSettings.Off })
            {
                MirrorSettings.TestQuality = level;
                me.Motor.SetLook(yaw, pitch);
                yield return new WaitForSeconds(1.5f);
                yield return MirrorMeasure(mirror, names[level], 5f);
                Shot(dir, $"mirror_{level}_{names[level]}");
                yield return new WaitForSeconds(0.5f);
            }

            // Turning at low (the reflection is rendered every 3rd frame): it must stay glued to the glass.
            MirrorSettings.TestQuality = MirrorSettings.Low;
            yield return new WaitForSeconds(1f);
            for (int i = 0; i < 40; i++)
            {
                me.Motor.SetLook(yaw + Mathf.Sin(i * 0.25f) * 12f, pitch);
                if (i == 21 || i == 31) Shot(dir, $"mirror_low_turning_{i}");
                yield return null;
            }

            // Far away / close-up at medium.
            MirrorSettings.TestQuality = MirrorSettings.Medium;
            Vector3 far = new Vector3(mp.x - 1.5f, 0.02f, mp.z + 7f);
            to = mp - (far + Vector3.up * 1.62f);
            me.Motor.Teleport(far, YawTo(to), PitchTo(to));
            me.Motor.SetLook(YawTo(to), PitchTo(to));
            yield return new WaitForSeconds(1.2f);
            Debug.Log($"[MirrorQ] medium at 7 m: {mirror.Describe()}");
            Shot(dir, "mirror_2_medium_far");
            yield return new WaitForSeconds(0.5f);

            MirrorSettings.TestQuality = null;
            yield return null;
            Debug.Log($"[MirrorQ] back to the saved level {MirrorSettings.Name(MirrorSettings.Quality)}: {mirror.Describe()}");
            Debug.Log("[MirrorQ] done");
        }

        private static IEnumerator MirrorMeasure(PlanarMirror mirror, string name, float seconds)
        {
            var timings = new FrameTiming[1];
            int renders0 = PlanarMirror.RenderCount;
            double ms0 = PlanarMirror.RenderMsTotal;
            int frames = 0, timed = 0;
            double dt = 0, cpu = 0, gpu = 0;
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < seconds)
            {
                yield return null;
                frames++;
                dt += Time.unscaledDeltaTime * 1000.0;
                FrameTimingManager.CaptureFrameTimings();
                if (FrameTimingManager.GetLatestTimings(1, timings) > 0 && timings[0].gpuFrameTime > 0)
                {
                    timed++;
                    cpu += timings[0].cpuMainThreadFrameTime;
                    gpu += timings[0].gpuFrameTime;
                }
            }
            float secs = Time.realtimeSinceStartup - t0;
            int renders = PlanarMirror.RenderCount - renders0;
            double rms = PlanarMirror.RenderMsTotal - ms0;
            Debug.Log($"[MirrorQ] {name}: {mirror.Describe()}");
            Debug.Log($"[MirrorQ] {name}: frame {dt / frames:0.00} ms ({frames / secs:0} FPS), " +
                      (timed > 0 ? $"main thread {cpu / timed:0.00} ms, GPU {gpu / timed:0.00} ms ({timed} frames timed), " : "no GPU timings, ") +
                      $"reflection renders {renders / secs:0.0}/s, {(renders > 0 ? rms / renders : 0):0.00} ms CPU each, {rms / frames:0.00} ms CPU per frame");
        }
    }
}
