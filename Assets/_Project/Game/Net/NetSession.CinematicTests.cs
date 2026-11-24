using System;
using System.Collections;
using System.Globalization;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using HideAndSeek.Game.Cinematics;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Round intro / outro tests (Game/Cinematics). Log lines start with [CineTest]. Host with -autostart -hidetime N.
    ///  -testintro (any copy): -introshots dir = screenshots at the key moments; -introframes dir = every frame at
    ///      30 fps (Time.captureFramerate) + intro.wav with the sounds, for ffmpeg.
    ///  -testoutro (host): the round ends 2 s after the seekers are released with -outrowinner hiders|seekers
    ///      (default hiders), fake statistics for every award, -outrodead N hiders die first (dead winners stand too);
    ///      any copy: -outroshots dir / -outroframes dir as above (outro.wav), -outroemote Wave = own emote at 4.2 s,
    ///      -outropair HighFive = ask the row neighbour at 4.0 s (the other copy with -autoconsent), -outrohold S = the
    ///      host keeps the Ended phase S s longer (frame recording), -outrowinners N = this copy shows N winners (the
    ///      real ones cut or filled up with stand-in copies, only the picture: emotes of copies are the auto cheers).
    /// </summary>
    public partial class NetSession
    {
        private void StartCinematicTests(Func<string, bool> has)
        {
            if (has("-testintro")) StartCoroutine(IntroTest());
            if (has("-testoutro")) StartCoroutine(OutroTest());
        }

        private IEnumerator IntroTest()
        {
            string shots = ArgOr("-introshots", ""), frames = ArgOr("-introframes", "");
            MatchManager mm = null;
            while ((mm = MatchManager.Instance) == null || IntroDirector.Current == null) yield return null;
            var d = IntroDirector.Current;
            Debug.Log($"[CineTest] intro director up, t={d.T:0.00}, killer={mm.IntroKiller.Value}, phaseEnd in {mm.PhaseEnd.Value - mm.Now:0.0} s");
            if (frames.Length > 0) { yield return Record(frames, "intro", () => IntroDirector.Current != null, true); yield break; }
            float[] at = { 0.3f, 1.0f, 1.7f, 2.3f, 2.7f, 2.95f, 3.15f, 3.28f, 3.45f, 3.7f, 4.2f, 4.7f, 5.2f, 5.6f, 6.1f, 6.6f, 6.95f, 7.05f, 7.4f, 7.7f, 8.05f };
            int k = 0;
            while (IntroDirector.Current != null && k < at.Length)
            {
                if (IntroDirector.Current.T >= at[k])
                {
                    Shot(shots, $"{LocalProfile.Nick}_intro_{k:00}_{at[k]:0.00}".Replace(',', '.'));
                    Debug.Log($"[CineTest] intro shot {k} t={IntroDirector.Current.T:0.00} gate={(ExitGate.Instance != null ? "ok" : "-")}");
                    k++;
                }
                yield return null;
            }
            yield return new WaitForSeconds(0.6f);
            Shot(shots, $"{LocalProfile.Nick}_intro_after");
            var me = NetPlayer.Local;
            Debug.Log($"[CineTest] intro over: phase={mm.Phase.Value} input={(me != null && me.InputReader != null && me.InputReader.InputEnabled)} hidingLeft={mm.PhaseEnd.Value - mm.Now:0.0}");
            Debug.Log("[CineTest] intro done");
        }

        private IEnumerator OutroTest()
        {
            MatchManager mm = null;
            while ((mm = MatchManager.Instance) == null || NetPlayer.Local == null) yield return null;
            if (mm.IsServer) StartCoroutine(OutroHost(mm));
            OutroDirector.TestWinners = int.TryParse(ArgOr("-outrowinners", "0"), out int forced) ? forced : 0;
            string shots = ArgOr("-outroshots", ""), frames = ArgOr("-outroframes", "");
            while (OutroDirector.Current == null) yield return null;
            Debug.Log($"[CineTest] outro director up, row={OutroDirector.Row.Count}");
            StartCoroutine(OutroActions());
            StartCoroutine(OutroFrameLog());
            if (frames.Length > 0) { yield return Record(frames, "outro", () => OutroDirector.Current != null && OutroDirector.Current.T < OutroTimeline.End, false); yield break; }
            float[] at = { 0.3f, 0.8f, 1.1f, 1.6f, 2.4f, 3.4f, 4.4f, 5.4f, 6.4f, 7.4f, 8.3f, 9.4f, 10.6f, 12.0f, 13.5f, 14.3f };
            int k = 0;
            while (OutroDirector.Current != null && k < at.Length)
            {
                if (OutroDirector.Current.T >= at[k])
                {
                    Shot(shots, $"{LocalProfile.Nick}_outro_{k:00}_{at[k]:0.0}".Replace(',', '.'));
                    k++;
                }
                yield return null;
            }
            Debug.Log("[CineTest] outro done");
        }

        /// <summary>Where the figures are on the screen (cut by the frame, hidden behind something, standing in a wall).</summary>
        private IEnumerator OutroFrameLog()
        {
            foreach (float at in new[] { 2.6f, 3.6f, 5.0f, 6.5f, 8.2f, 10.5f, 13.6f })
            {
                while (OutroDirector.Current != null && OutroDirector.Current.T < at) yield return null;
                if (OutroDirector.Current == null) yield break;
                Debug.Log("[CineTest] frame " + OutroDirector.Current.FrameReport());
            }
        }

        private IEnumerator OutroActions()
        {
            var me = NetPlayer.Local;
            string emote = ArgOr("-outroemote", ""), pair = ArgOr("-outropair", "");
            while (OutroDirector.Current != null && OutroDirector.Current.T < 4.0f) yield return null;
            if (pair.Length > 0 && Enum.TryParse<EmoteId>(pair, out var pid))
            {
                var nb = OutroDirector.PairTargetFor(me);
                Debug.Log($"[CineTest] pair {pid} with {(nb != null ? nb.Nick : "-")}: {(nb != null && me.AskPairEmote(pid, nb))}");
            }
            while (OutroDirector.Current != null && OutroDirector.Current.T < 4.2f) yield return null;
            if (emote.Length > 0 && Enum.TryParse<EmoteId>(emote, out var id))
                Debug.Log($"[CineTest] emote {id}: {me.StartSoloEmote(id)}");
        }

        /// <summary>Host: fake numbers for every award, some hiders die, then the round ends with the chosen winner.</summary>
        private IEnumerator OutroHost(MatchManager mm)
        {
            while (mm.Phase.Value != MatchPhase.Seeking) yield return null;
            yield return new WaitForSeconds(2f);
            bool seekersWin = ArgOr("-outrowinner", "hiders").StartsWith("seek");
            int dead = int.TryParse(ArgOr("-outrodead", "0"), out int dn) ? dn : 0;
            var rng = new System.Random(5);
            NetPlayer anySeeker = null;
            foreach (var p in NetPlayer.All) if (p != null && p.IsSeeker) anySeeker = p;
            int k = 0;
            foreach (var p in NetPlayer.All)
            {
                if (p == null) continue;
                var e = RoundStats.TestEntry(p);
                if (e == null) continue;
                e.Distance = 180f + rng.Next(900);
                e.VoiceTime = 20f + rng.Next(240);
                if (p.IsSeeker)
                {
                    e.KnifeKills = 1 + rng.Next(3);
                    e.SawKills = rng.Next(3);
                    e.GlockKills = rng.Next(2) + 1;
                    e.Toys = 1 + rng.Next(2);
                }
                else
                {
                    e.Heals = 1 + rng.Next(3);
                    e.Rescues = rng.Next(3);
                    e.Revives = rng.Next(2);
                    e.RcTime = 10f + rng.Next(90);
                    e.LegStuns = k == 0 ? 1 : 0;
                    e.SeenTime = rng.Next(30);
                    if (anySeeker != null && k == 0) RoundStats.Hit(anySeeker, p, KillWeapon.Knife);
                    if (seekersWin || k < dead) mm.TestKill(p);
                    k++;
                }
            }
            yield return new WaitForSeconds(0.3f);
            Debug.Log($"[CineTest] host: ending the round, winner {(seekersWin ? "seekers" : "hiders")}, dead {dead}");
            mm.TestEndMatch(seekersWin ? MatchWinner.Seeker : MatchWinner.Hiders);
            // -outrohold S: the Ended phase lasts S s longer (slow frame recording must not be cut by the lobby load).
            if (float.TryParse(ArgOr("-outrohold", "0"), NumberStyles.Float, CultureInfo.InvariantCulture, out float hold) && hold > 0f)
                mm.PhaseEnd.Value += hold;
        }

        /// <summary>Every frame as PNG at 30 fps while <paramref name="running"/>, then the sound track as WAV.</summary>
        private IEnumerator Record(string dir, string name, Func<bool> running, bool intro)
        {
            System.IO.Directory.CreateDirectory(dir);
            Time.captureFramerate = 30;
            IntroDirector.Recording = intro;
            OutroDirector.Recording = !intro;
            CineRecorder.Begin();
            float start = intro ? IntroDirector.Current.T : OutroDirector.Current.T;
            int f = 0;
            while (running())
            {
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"f_{f++:0000}.png"));
                yield return null;
            }
            for (int i = 0; i < 20; i++) { ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"f_{f++:0000}.png")); yield return null; }
            Time.captureFramerate = 0;
            IntroDirector.Recording = OutroDirector.Recording = false;
            float len = f / 30f;
            CineRecorder.WriteWav(System.IO.Path.Combine(dir, name + ".wav"), len, -start);
            Debug.Log($"[CineTest] recorded {f} frames from t={start.ToString("0.00", CultureInfo.InvariantCulture)} into {dir}");
            Debug.Log($"[CineTest] {name} done");
        }
    }
}
