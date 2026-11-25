using System;
using System.Collections;
using System.Globalization;
using Unity.Collections;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Lobby TV YouTube tests (no -autostart): -testtvyt &lt;dir&gt; on every copy. The host runs the script (prompt, panel,
    /// play a real video, pause / resume, seek, wait for a late joiner, stop, an invalid link, a round start with the TV on);
    /// every copy logs "[TvTest] sample" lines (server time, picture time, wanted time, sound playhead) twice a second
    /// and takes its own screenshots at the stages it sees. -tvyturl URL overrides the video.
    /// Logs: [TvTest] ... PASS / FAIL.
    /// </summary>
    public partial class NetSession
    {
        // Big Buck Bunny (Blender, CC BY), pasted with the search / start parameters YouTube adds.
        private const string TvTestUrl = "https://www.youtube.com/watch?v=aqz-KE-bpKQ&pp=ygUPYmlnIGJ1Y2sgYnVubnk%3D&t=30s";
        private static readonly string[] TvMoreLinks =
        {
            "https://www.youtube.com/watch?v=onCwpUd-NLg&list=LL&index=4", // the link from the user's report (in a playlist)
            "https://youtu.be/dQw4w9WgXcQ?si=QWERTY12345", // a music video, short link
            "https://youtube.com/shorts/jNQXAC9IVRw", // 19 s, 4:3, as a Shorts link: plays to the end
        };
        private const string TvBadUrl = "https://www.youtube.com/watch?v=aaaaaaaaaaa";
        private static void TvLog(string s) => Debug.Log("[TvTest] " + s);

        private void StartTvYtTests(Func<string, bool> has)
        {
            if (has("-tvplay") && Manager.IsServer) StartCoroutine(TvPlayOnly(ArgOr("-tvplay", TvTestUrl)));
            if (!has("-testtvyt")) return;
            string dir = ArgOr("-testtvyt", "");
            StartCoroutine(TvSamples());
            StartCoroutine(TvWatchShots(dir));
            if (Manager.IsServer) StartCoroutine(TvHostTest(dir));
        }

        /// <summary>-tvplay URL (host): just plays the link on the lobby TV (a test window to watch), logs the time to the picture.</summary>
        private IEnumerator TvPlayOnly(string url)
        {
            yield return TvWaitLobby(1);
            TvLookAt(new Vector3(-4.0f, 0f, 0.5f));
            float t0 = Time.realtimeSinceStartup;
            TvPlay(url);
            var tv = LobbyTvVideo.Instance;
            var ch = LobbyTvVideo.Channel;
            yield return TvWaitFor(() => tv.Prepared || tv.ErrorText != null, 180f);
            TvLog($"-tvplay: picture {(tv.Prepared ? "ready" : "FAILED " + tv.ErrorText)} after {Time.realtimeSinceStartup - t0:0.0}s, title '{tv.Title}'");
            yield return TvWaitFor(() => { var r = ch.State.Value; return r.Playing && !r.Loading && tv.VideoPlaying; }, 120f);
            TvLog($"-tvplay: playing after {Time.realtimeSinceStartup - t0:0.0}s");
            for (int i = 0; i < 6; i++)
            {
                yield return new WaitForSeconds(5f);
                TvLog($"-tvplay: picture {tv.VideoTime:0.00}s sound {tv.AudioPlayhead:0.00}s playing={tv.AudioPlaying} level={tv.AudioLevel:0.00} " +
                      $"volume={ch.State.Value.Volume} -> {Pf(tv.AudioPlaying && tv.AudioLevel > 0.001f && Mathf.Abs((float)tv.VideoTime - tv.AudioPlayhead) < 0.2f)}");
            }
        }

        /// <summary>Every copy: where the picture / sound are against the common clock.</summary>
        private static IEnumerator TvSamples()
        {
            while (true)
            {
                yield return new WaitForSecondsRealtime(0.5f);
                var mm = MatchManager.Instance;
                var tv = LobbyTvVideo.Instance;
                var ch = LobbyTvVideo.Channel;
                if (mm == null || tv == null || ch == null || ch.Current == null) continue;
                var r = ch.State.Value;
                float want = ch.Position(r);
                TvLog(string.Format(CultureInfo.InvariantCulture,
                    "sample now={0:0.000} seq={1} playing={2} loading={3} want={4:0.000} vt={5:0.000} d={6:+0.000;-0.000} speed={7:0.00} audio={8:0.000} aplay={9} level={10:0.00} vplay={11} status='{12}'",
                    mm.Now, r.Seq, r.Playing, r.Loading, want, tv.VideoTime, tv.VideoTime - want, tv.Speed, tv.AudioPlayhead, tv.AudioPlaying,
                    tv.AudioLevel, tv.VideoPlaying, tv.StatusLine));
            }
        }

        /// <summary>Every copy: stands in front of the TV and photographs it at each stage it sees.</summary>
        private IEnumerator TvWatchShots(string dir)
        {
            yield return TvWaitLobby(1);
            string me = Manager.IsServer ? "host" : NetPlayer.Local.Nick.ToLowerInvariant();
            TvLookAt(Manager.IsServer ? new Vector3(-4.0f, 0f, 0.5f) : new Vector3(-2.9f, 0f, 0.6f));
            bool playing = false, paused = false, error = false, stopped = false, any = false;
            string shotFor = null;
            while (true)
            {
                yield return new WaitForSecondsRealtime(0.25f);
                var tv = LobbyTvVideo.Instance;
                var ch = LobbyTvVideo.Channel;
                if (tv == null || ch == null) { if (any) yield break; continue; }
                var r = ch.State.Value;
                bool on = ch.Current != null;
                any |= on;
                if (on && r.Playing && !r.Loading && tv.VideoPlaying && tv.VideoTime > 4 && shotFor != ch.Current)
                {
                    playing = true;
                    shotFor = ch.Current;
                    string id = RadioCache.VideoId(shotFor) ?? "x";
                    yield return new WaitForSecondsRealtime(1.5f);
                    TvLookAt(NetPlayer.Local.transform.position);
                    Shot(dir, $"tv_{me}_playing_{id}");
                    TvLog($"{me}: shot playing {id} at vt={tv.VideoTime:0.00} title='{tv.Title}'");
                }
                if (on && !r.Playing && tv.Prepared && playing && !paused)
                {
                    paused = true;
                    yield return new WaitForSecondsRealtime(1.5f);
                    Shot(dir, $"tv_{me}_paused");
                }
                if (!on && any && !stopped && tv.ErrorText == null && playing && !tv.OwnsScreen)
                {
                    stopped = true;
                    yield return new WaitForSecondsRealtime(1f);
                    Shot(dir, $"tv_{me}_stopped_cctv");
                }
                if (tv.ErrorText != null && !error)
                {
                    error = true;
                    TvLog($"{me}: error on screen '{tv.ErrorText}' status='{tv.StatusLine}'");
                    yield return new WaitForSecondsRealtime(0.5f);
                    Shot(dir, $"tv_{me}_error");
                }
            }
        }

        private static void TvLookAt(Vector3 feet)
        {
            var me = NetPlayer.Local;
            var tv = LobbyTvVideo.Instance;
            if (me == null || tv == null) return;
            Vector3 eye = feet + Vector3.up * 1.6f, to = tv.ScreenCenter - eye;
            float yaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            float pitch = Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg;
            me.TeleportLocal(feet, yaw);
            me.Motor.SetLook(yaw, pitch);
        }

        private static IEnumerator TvWaitLobby(int players)
        {
            float t0 = Time.time;
            while (true)
            {
                var mm = MatchManager.Instance;
                if (mm != null && mm.Phase.Value == MatchPhase.Lobby && NetPlayer.Local != null && LobbyTvVideo.Instance != null &&
                    (NetPlayer.All.Count >= players || Time.time - t0 > 60f)) break;
                yield return null;
            }
            yield return new WaitForSeconds(2f);
        }

        private static IEnumerator TvWaitFor(Func<bool> cond, float max)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!cond() && Time.realtimeSinceStartup - t0 < max) yield return null;
        }

        /// <summary>Pastes <paramref name="pasted"/> as the panel does (normalised link, t= start).</summary>
        private static bool TvPlay(string pasted)
        {
            bool ok = TvLink.TryNormalize(pasted, out string link, out float start);
            TvLog($"link '{pasted}' -> '{link}' start={start:0.0}");
            if (ok) LobbyTvVideo.Play(NetPlayer.Local, LobbyTvVideo.Channel, link, start);
            return ok;
        }

        private static void TvReq(RadioOp op, string arg = "") =>
            LobbyTvVideo.Channel.Request(NetPlayer.Local, op, new FixedString128Bytes(arg ?? ""));
    }
}
