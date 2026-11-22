using System;
using System.Collections;
using System.Collections.Generic;
using HideAndSeek.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// -testlobbyreturn N (host): N quick rounds in a row, each one back to the lobby, with the radio playing in the lobby,
    /// items thrown around the map and a dead hider watching through the spectator camera at the end of the round
    /// (the case that left the camera off in the lobby). Logs "[LobbyTest]" with the time of every transition: map loaded,
    /// host back in the lobby, every copy back in the lobby (NGO load event), timed-out copies.
    /// Clients join normally (-join IP); their side logs "[LobbyReturn] ..." by itself.
    /// Options: -autostartplayers N (players to wait for, default 2), -lobbytestplay S (seconds of play per round,
    /// default 6), -lobbytestradio URL (default: the boombox test track), -noradio, -lobbytestquit (host quits at the end).
    /// </summary>
    public partial class NetSession
    {
        private void StartLobbyReturnTests(Func<string, string> arg)
        {
            if (arg("-testlobbyreturn") is string n && int.TryParse(n, out int rounds) && rounds > 0)
                StartCoroutine(LobbyReturnTest(rounds));
        }

        private static void LTLog(string s) => Debug.Log("[LobbyTest] " + s);

        private IEnumerator LobbyReturnTest(int rounds)
        {
            int need = int.TryParse(ArgOr("-autostartplayers", "2"), out int np) ? np : 2;
            float play = float.TryParse(ArgOr("-lobbytestplay", "6"), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float pl) ? pl : 6f;
            while (MatchManager.Instance == null || NetPlayer.Local == null || NetPlayer.All.Count < need ||
                   SceneManager.GetActiveScene().name != LobbyScene)
                yield return new WaitForSeconds(0.5f);
            if (!Manager.IsServer)
            {
                LTLog("client: nothing to drive (the host runs the rounds); [LobbyReturn] lines show this copy's side");
                yield break;
            }
            var mm = MatchManager.Instance;
            LTLog($"start: {rounds} rounds, {NetPlayer.All.Count} players, {play:0.0} s of play per round");

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-noradio") < 0)
            {
                string url = ArgOr("-lobbytestradio", BoomboxTests.DefaultUrl);
                mm.ServerRadio(NetPlayer.Local, RadioOp.Add, url);
                LTLog("radio: queued " + url);
            }

            var times = new List<string>();
            int failures = 0;
            for (int round = 1; round <= rounds; round++)
            {
                // ---- lobby: radio on, everybody ready, start
                yield return new WaitForSeconds(4f);
                var r = mm.Radio.Value;
                if (!r.Playing && r.Index >= 0 && r.Index < mm.RadioQueue.Count) mm.ServerRadio(NetPlayer.Local, RadioOp.Toggle, null);
                yield return new WaitForSeconds(2f);
                LTLog($"round {round}: radio playing={mm.Radio.Value.Playing} loading={mm.Radio.Value.Loading}, boombox={(Boombox.Instance != null)}");
                foreach (var p in NetPlayer.All) if (p != null) p.Ready.Value = true;
                var s = mm.Settings.Value;
                s.Mode = (byte)GameMode.HideAndSeek;
                s.Seekers = 1;
                s.HideTime = 3f; // set directly (the board clamps to 10 s): the seeking phase comes at once
                mm.Settings.Value = s;
                float t0 = Time.realtimeSinceStartup;
                mm.RequestStartRpc();
                while (mm.Phase.Value != MatchPhase.Seeking && Time.realtimeSinceStartup - t0 < 150f) yield return null;
                if (mm.Phase.Value != MatchPhase.Seeking)
                {
                    LTLog($"round {round}: FAIL - no seeking phase after 150 s (phase {mm.Phase.Value}, scene {SceneManager.GetActiveScene().name})");
                    failures++;
                    break;
                }
                LTLog($"round {round}: map + hiding done after {Time.realtimeSinceStartup - t0:0.00} s");

                // ---- on the map: items on the floor, one hider dead (watches through the spectator camera)
                mm.TestScatter(3);
                NetPlayer victim = null;
                foreach (var p in NetPlayer.All) if (p != null && p.IsHider && (victim == null || p != NetPlayer.Local)) victim = p;
                if (victim != null)
                {
                    mm.TestKill(victim); // the last hider: the seekers win at once, the dead one watches the end screen
                    LTLog($"round {round}: {victim.Nick} killed (spectates until the lobby)");
                }
                yield return new WaitForSeconds(play);

                // ---- end -> lobby
                int doneBefore = mm.LobbyReturnsCompleted;
                if (mm.Phase.Value == MatchPhase.Seeking) mm.TestEndRound();
                float tEnd = Time.realtimeSinceStartup;
                while (mm.Phase.Value == MatchPhase.Ended && Time.realtimeSinceStartup - tEnd < 60f) yield return null;
                float tLobby = Time.realtimeSinceStartup;
                while (SceneManager.GetActiveScene().name != LobbyScene && Time.realtimeSinceStartup - tLobby < 60f) yield return null;
                float hostIn = Time.realtimeSinceStartup - tLobby;
                while (mm.LobbyReturnsCompleted == doneBefore && Time.realtimeSinceStartup - tLobby < 150f) yield return null;
                bool ok = mm.Phase.Value == MatchPhase.Lobby && SceneManager.GetActiveScene().name == LobbyScene && mm.LobbyReturnsCompleted > doneBefore;
                var me = NetPlayer.Local;
                bool camOn = me != null && me.Camera != null && me.Camera.enabled;
                string line = $"round {round}: end screen {tLobby - tEnd:0.0} s, host in the lobby after {hostIn:0.00} s, every copy after " +
                              $"{mm.LastLobbyAllLoadedSeconds:0.00} s, timed out {mm.LastLobbyTimedOut}, own camera on={camOn}, players {NetPlayer.All.Count}" +
                              (ok && camOn ? "" : " - FAIL");
                if (!ok || !camOn || mm.LastLobbyTimedOut > 0) failures++;
                times.Add($"{mm.LastLobbyAllLoadedSeconds:0.00}");
                LTLog(line);
                if (!ok) break;
            }
            LTLog($"done: {times.Count}/{rounds} returns, all-copies times [{string.Join(", ", times)}] s, failures {failures}");
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-lobbytestquit") >= 0)
            {
                yield return new WaitForSeconds(2f);
                Application.Quit();
            }
        }
    }
}
