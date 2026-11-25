using System.Collections;
using System.Globalization;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>Lobby TV YouTube test, the host's script (see <c>NetSession.TvYtTests.cs</c>).</summary>
    public partial class NetSession
    {
        private IEnumerator TvHostTest(string dir)
        {
            yield return TvWaitLobby(2);
            var mm = MatchManager.Instance;
            var ch = LobbyTvVideo.Channel;
            var lpc = LocalPlayerController.Instance;
            string url = ArgOr("-tvyturl", TvTestUrl);
            yield return new WaitForSeconds(1f);
            TvLog($"tools: yt-dlp={RadioTools.YtDlp ?? "-"} ffmpeg={RadioTools.Ffmpeg ?? "-"}");

            // 1. Prompt at the TV, the panel.
            yield return new WaitForSeconds(0.5f);
            TvLog($"prompt: {lpc.Prompt} '{lpc.PromptText}' -> {Pf(lpc.Prompt == PromptKind.LobbyTv)}");
            Shot(dir, "tv_a_prompt");
            LobbyTvVideo.OpenPanel();
            yield return new WaitForSeconds(0.6f);
            Shot(dir, "tv_b_panel_empty");
            yield return null; // the capture happens a frame later
            yield return null;
            TvLog($"panel open={LobbyTvVideo.PanelOpen} cursor free={CursorPolicy.WantsFree} -> {Pf(LobbyTvVideo.PanelOpen && CursorPolicy.WantsFree)}");
            TvLog($"not a YouTube link refused: {Pf(!TvLink.TryNormalize("https://vimeo.com/76979871", out _, out _) && !TvLink.TryNormalize("https://www.youtube.com/@Blender", out _, out _))}");

            // 2. Play: everybody loads, then a common start.
            float t0 = Time.realtimeSinceStartup;
            TvPlay(url);
            yield return new WaitForSeconds(1.2f);
            Shot(dir, "tv_c_panel_loading");
            yield return null;
            yield return null;
            LobbyTvVideo.ClosePanel();
            Shot(dir, "tv_c_screen_loading");
            var tv = LobbyTvVideo.Instance;
            yield return TvWaitFor(() => { var r = ch.State.Value; return r.Playing && !r.Loading && tv.VideoPlaying; }, 120f);
            yield return new WaitForSeconds(1.5f); // the picture time is read a moment after the start
            var st = ch.State.Value;
            TvLog($"started after {Time.realtimeSinceStartup - t0:0.0}s: playing={st.Playing} loading={st.Loading} ready={st.Ready}/{st.Peers} from={st.LoadFrom:0.0} vt={tv.VideoTime:0.00} title='{tv.Title}' -> {Pf(st.Playing && !st.Loading && tv.VideoPlaying && tv.VideoTime > 29f)}");
            yield return new WaitForSeconds(8f);
            TvLog($"host sync after 8 s: vt={tv.VideoTime:0.000} want={ch.Position(ch.State.Value):0.000} audio={tv.AudioPlayhead:0.000} level={tv.AudioLevel:0.00} -> {Pf(Mathf.Abs((float)tv.VideoTime - ch.Position(ch.State.Value)) < 0.2f && tv.AudioPlaying)}");
            LobbyTvVideo.OpenPanel();
            yield return new WaitForSeconds(0.6f);
            Shot(dir, "tv_d_panel_playing");
            yield return new WaitForSecondsRealtime(0.4f); // the capture is written a few frames later
            LobbyTvVideo.ClosePanel();
            CamShot(dir, "tv_d_room_cam", new Vector3(-1.2f, 1.7f, -0.6f), tv.ScreenCenter - Vector3.up * 0.4f);

            // 3. Volume (wheel / slider = SetVolume), radio ducking.
            TvReq(RadioOp.SetVolume, "35");
            yield return new WaitForSeconds(0.8f);
            TvLog($"volume 35: {ch.State.Value.Volume} duck={LobbyTvVideo.RadioDuck:0.00} -> {Pf(ch.State.Value.Volume == 35 && LobbyTvVideo.RadioDuck < 0.5f)}");
            TvReq(RadioOp.SetVolume, "70");

            // 4. Pause / resume.
            yield return new WaitForSeconds(4f);
            TvReq(RadioOp.Toggle);
            yield return new WaitForSeconds(3f);
            st = ch.State.Value;
            TvLog($"paused: PausedAt={st.PausedAt:0.000} vt={tv.VideoTime:0.000} vplay={tv.VideoPlaying} aplay={tv.AudioPlaying} -> {Pf(!st.Playing && !tv.VideoPlaying && Mathf.Abs((float)tv.VideoTime - st.PausedAt) < 0.1f)}");
            TvReq(RadioOp.Toggle);
            yield return new WaitForSeconds(6f);
            TvLog($"resumed: vt={tv.VideoTime:0.000} want={ch.Position(ch.State.Value):0.000} -> {Pf(tv.VideoPlaying && Mathf.Abs((float)tv.VideoTime - ch.Position(ch.State.Value)) < 0.2f)}");

            // 5. Seek (everybody reloads from there and starts together).
            TvReq(RadioOp.Seek, "300");
            yield return TvWaitFor(() => { var r = ch.State.Value; return r.Playing && !r.Loading && tv.VideoPlaying && tv.VideoTime > 299; }, 40f);
            yield return new WaitForSeconds(5f);
            TvLog($"seek 300: vt={tv.VideoTime:0.000} want={ch.Position(ch.State.Value):0.000} audio={tv.AudioPlayhead:0.000} -> {Pf(tv.VideoTime > 300 && Mathf.Abs((float)tv.VideoTime - ch.Position(ch.State.Value)) < 0.2f)}");

            // 6. Late joiner (started by the runner meanwhile): wait for the third player, give him time to load.
            yield return TvWaitFor(() => NetPlayer.All.Count >= 3, 60f);
            TvLog($"players now {NetPlayer.All.Count}");
            yield return new WaitForSeconds(25f);

            // 7. Other link forms (the reported playlist link, youtu.be music video): each starts; then stop.
            for (int i = 0; i < 2; i++)
            {
                float ts = Time.realtimeSinceStartup;
                TvPlay(TvMoreLinks[i]);
                yield return new WaitForSeconds(1f);
                yield return TvWaitFor(() => { var r = ch.State.Value; return ch.Current != null && r.Playing && !r.Loading && tv.VideoPlaying && tv.VideoTime > 2; }, 120f);
                yield return new WaitForSeconds(6f);
                TvLog($"link {i + 1}: '{tv.Title}' after {Time.realtimeSinceStartup - ts:0.0}s vt={tv.VideoTime:0.00} want={ch.Position(ch.State.Value):0.00} err={tv.ErrorText ?? "-"} -> {Pf(tv.VideoPlaying && tv.ErrorText == null && Mathf.Abs((float)tv.VideoTime - ch.Position(ch.State.Value)) < 0.3f)}");
            }
            TvReq(RadioOp.Clear);
            yield return new WaitForSeconds(2f);
            TvLog($"stopped: current={(ch.Current ?? "-")} owns={tv.OwnsScreen} -> {Pf(ch.Current == null && !tv.OwnsScreen)}");
            Shot(dir, "tv_e_stopped");

            // 8. A Shorts link (19 s): plays to its end, then the TV goes back to the cameras by itself.
            TvPlay(TvMoreLinks[2]);
            yield return TvWaitFor(() => { var r = ch.State.Value; return r.Playing && !r.Loading && tv.VideoPlaying; }, 90f);
            TvLog($"shorts playing: '{tv.Title}' -> {Pf(tv.VideoPlaying)}");
            yield return TvWaitFor(() => ch.Current == null, 40f);
            yield return new WaitForSeconds(1f);
            TvLog($"shorts ended by itself: current={(ch.Current ?? "-")} owns={tv.OwnsScreen} -> {Pf(ch.Current == null && !tv.OwnsScreen)}");

            // 9. An invalid link: a Polish message on the screen, the TV switches itself off.
            TvReq(RadioOp.Replace, TvBadUrl);
            yield return TvWaitFor(() => tv.ErrorText != null, 40f);
            yield return new WaitForSeconds(0.5f);
            TvLog($"bad link: error='{tv.ErrorText}' status='{tv.StatusLine}' -> {Pf(tv.ErrorText != null)}");
            Shot(dir, "tv_f_error");
            yield return TvWaitFor(() => ch.Current == null, 20f);
            TvLog($"bad link ended by the server: current={(ch.Current ?? "-")} -> {Pf(ch.Current == null)}");
            yield return new WaitForSeconds(9f);
            TvLog($"after the message: owns={tv.OwnsScreen} -> {Pf(!tv.OwnsScreen)}");

            // 10. Round start with the TV on: the TV stops, the round starts.
            TvPlay(url);
            yield return TvWaitFor(() => { var r = ch.State.Value; return r.Playing && !r.Loading; }, 60f);
            yield return new WaitForSeconds(3f);
            mm.TestStartRound(); // as the lever, without the flashlight readiness of the test players
            yield return TvWaitFor(() => mm.Phase.Value != MatchPhase.Lobby, 30f);
            yield return new WaitForSeconds(2f);
            st = mm.TvVideo.Value;
            TvLog($"round start: phase={mm.Phase.Value} tv current={(mm.LobbyTv.Current ?? "-")} playing={st.Playing} queue={mm.TvQueue.Count} -> {Pf(mm.Phase.Value != MatchPhase.Lobby && mm.LobbyTv.Current == null && !st.Playing)}");
            TvLog("done");
        }
    }
}
