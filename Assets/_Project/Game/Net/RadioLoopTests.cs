using System;
using System.Collections;
using System.IO;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// -testradioloop (host + client in the lobby, -radioshots dir): the new boombox panel, downloaded songs, loop of
    /// the queue, loop of one song, seek for everybody, remove / jump. The host drives through server ops and opens the
    /// panel for screenshots; the client logs its playhead (must follow the host's after a seek). "[RadioLoop] PASS/FAIL".
    /// </summary>
    public class RadioLoopTests : MonoBehaviour
    {
        private static string[] Args => Environment.GetCommandLineArgs();
        private static string ArgOr(string name, string def)
        {
            int i = Array.IndexOf(Args, name);
            return i >= 0 && i + 1 < Args.Length ? Args[i + 1] : def;
        }

        public const string ShortUrl = "https://www.youtube.com/watch?v=jNQXAC9IVRw"; // 19 s
        private int _fails;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (Array.IndexOf(Args, "-testradioloop") < 0) return;
            var go = new GameObject("[RadioLoopTests]");
            DontDestroyOnLoad(go);
            go.AddComponent<RadioLoopTests>().StartCoroutine(go.GetComponent<RadioLoopTests>().Run());
        }

        private void Check(bool ok, string what)
        {
            if (!ok) _fails++;
            Debug.Log($"[RadioLoop] {(ok ? "ok" : "FAIL")}: {what} | {Boombox.Instance?.DebugState}");
        }

        private static void Shot(string dir, string name)
        {
            if (string.IsNullOrEmpty(dir)) return;
            Directory.CreateDirectory(dir);
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, name + ".png"));
        }

        private IEnumerator Run()
        {
            MatchManager mm = null;
            while (mm == null || mm.Phase.Value != MatchPhase.Lobby || NetPlayer.Local == null || Boombox.Instance == null || NetPlayer.All.Count < 2)
            {
                mm = MatchManager.Instance;
                yield return null;
            }
            yield return new WaitForSeconds(2f);
            var me = NetPlayer.Local;
            var bb = Boombox.Instance;
            bool host = NetworkManager.Singleton.IsServer;
            string dir = ArgOr("-radioshots", "");
            string who = host ? "host" : "client";
            string url = ArgOr("-radiourl", ShortUrl);

            if (!host)
            {
                // The client: panel open (its own screenshots), playhead log; checks its position after the host's seek.
                Boombox.OpenPanel();
                float t0 = Time.realtimeSinceStartup;
                bool shotDl = false;
                while (Time.realtimeSinceStartup - t0 < 120f)
                {
                    var r = mm.Radio.Value;
                    if (!shotDl && r.Index >= 0 && bb.IsLoading) { shotDl = true; Shot(dir, "client_loading"); }
                    Debug.Log($"[RadioLoop] client t={Time.realtimeSinceStartup - t0:0} | {bb.DebugState}");
                    yield return new WaitForSeconds(2f);
                }
                yield break;
            }

            Boombox.OpenPanel();
            yield return new WaitForSeconds(0.5f);
            Shot(dir, "panel_empty");
            yield return new WaitForSeconds(0.3f);
            float tAdd = Time.realtimeSinceStartup;
            mm.ServerRadio(me, RadioOp.Add, url);
            mm.ServerRadio(me, RadioOp.Add, url);
            mm.ServerRadio(me, RadioOp.SetVolume, "80");
            mm.ServerRadio(me, RadioOp.SetBass, "45");
            yield return new WaitForSeconds(1.2f);
            Shot(dir, "panel_downloading");
            while (!bb.MusicAudible && Time.realtimeSinceStartup - tAdd < 60f) yield return null;
            Check(bb.MusicAudible, $"first sound after {Time.realtimeSinceStartup - tAdd:0.0}s (download + common start)");
            Check(mm.Radio.Value.Volume == 80 && mm.Radio.Value.Bass == 45, "SetVolume / SetBass");
            yield return new WaitForSeconds(3f);
            Shot(dir, "panel_playing");

            // ---- loop the queue: [A, A] -> after the second, back to the first.
            mm.ServerRadio(me, RadioOp.Loop, null);
            Check(mm.Radio.Value.Loop == RadioLoop.Queue, "loop -> queue");
            mm.ServerRadio(me, RadioOp.Next, null);
            yield return new WaitForSeconds(0.5f);
            Check(mm.Radio.Value.Index == 1, "next -> second entry");
            int seq = mm.Radio.Value.Seq;
            float tw = Time.realtimeSinceStartup;
            while (mm.Radio.Value.Index != 0 && Time.realtimeSinceStartup - tw < 45f) yield return null;
            Check(mm.Radio.Value.Index == 0 && mm.Radio.Value.Seq > seq, $"queue loop wrapped to the first after {Time.realtimeSinceStartup - tw:0.0}s");
            tw = Time.realtimeSinceStartup;
            while (!bb.MusicAudible && Time.realtimeSinceStartup - tw < 15f) yield return null;
            Check(bb.MusicAudible, $"wrapped song plays after {Time.realtimeSinceStartup - tw:0.00}s (cached)");

            // ---- loop one song: the same index again, a new seq.
            mm.ServerRadio(me, RadioOp.Loop, null);
            Check(mm.Radio.Value.Loop == RadioLoop.One, "loop -> one song");
            yield return new WaitForSeconds(1f);
            Shot(dir, "panel_loop_one");
            seq = mm.Radio.Value.Seq;
            int idx = mm.Radio.Value.Index;
            tw = Time.realtimeSinceStartup;
            while (mm.Radio.Value.Seq == seq && Time.realtimeSinceStartup - tw < 45f) yield return null;
            Check(mm.Radio.Value.Seq > seq && mm.Radio.Value.Index == idx, $"one-song loop restarted index {idx} after {Time.realtimeSinceStartup - tw:0.0}s");
            yield return new WaitForSeconds(3f);

            // ---- seek for everybody
            mm.ServerRadio(me, RadioOp.Seek, "12.5");
            Check(mm.Radio.Value.Loading && Mathf.Abs(mm.Radio.Value.LoadFrom - 12.5f) < 0.01f, "seek -> loading from 12.5");
            tw = Time.realtimeSinceStartup;
            while ((mm.Radio.Value.Loading || !bb.MusicAudible) && Time.realtimeSinceStartup - tw < 15f) yield return null;
            yield return new WaitForSeconds(1f);
            float server = mm.LobbyRadio.Position(mm.Radio.Value);
            Check(Mathf.Abs(bb.PlayheadSeconds - server) < 0.25f && server > 12.5f && server < 16f,
                $"after seek: playhead {bb.PlayheadSeconds:0.00}s, server {server:0.00}s (restart took {Time.realtimeSinceStartup - tw - 1f:0.00}s)");
            Shot(dir, "panel_after_seek");

            // ---- paused seek, resume
            mm.ServerRadio(me, RadioOp.Toggle, null);
            mm.ServerRadio(me, RadioOp.Seek, "3");
            Check(!mm.Radio.Value.Playing && Mathf.Abs(mm.Radio.Value.PausedAt - 3f) < 0.01f, "paused seek -> paused at 3");
            yield return new WaitForSeconds(1f);
            mm.ServerRadio(me, RadioOp.Toggle, null);
            yield return new WaitForSeconds(2.5f);
            server = mm.LobbyRadio.Position(mm.Radio.Value);
            Check(bb.MusicAudible && Mathf.Abs(bb.PlayheadSeconds - server) < 0.3f && server > 3f && server < 7f, $"resumed from the paused seek: playhead {bb.PlayheadSeconds:0.00}, server {server:0.00}");

            // ---- remove / jump: [A, A, A] remove index 2; remove an earlier one keeps the song; jump
            mm.ServerRadio(me, RadioOp.Loop, null); // off
            mm.ServerRadio(me, RadioOp.Add, url);
            Check(mm.RadioQueue.Count == 3, "added a third entry");
            mm.ServerRadio(me, RadioOp.Jump, "1");
            yield return new WaitForSeconds(0.3f);
            Check(mm.Radio.Value.Index == 1, "jump -> index 1");
            seq = mm.Radio.Value.Seq;
            mm.ServerRadio(me, RadioOp.Remove, "0");
            Check(mm.RadioQueue.Count == 2 && mm.Radio.Value.Index == 0 && mm.Radio.Value.Seq == seq, "removing an earlier entry keeps the song (index 1 -> 0, same seq)");
            mm.ServerRadio(me, RadioOp.Remove, "1");
            Check(mm.RadioQueue.Count == 1, "removed the last entry");
            yield return new WaitForSeconds(4f);
            Check(bb.MusicAudible, "still playing after the removes");
            Shot(dir, "panel_end");
            Debug.Log($"[RadioLoop] {(_fails == 0 ? "PASS" : "FAIL")} {who}: {_fails} fail(s)");
            Boombox.ClosePanel();
        }
    }
}
