using System;
using System.Collections;
using System.IO;
using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Automated tests of the 2026-11-22 sound update (own component, started from the command line, no NetSession hook):
    /// -testvoicechan (+ -vcshots dir): observer voice channel (host seeker kills the hiders one by one, all with -voicetest).
    /// -testpvol (+ -pvolshots dir): per-player voice volume, saved to the settings file, Esc page screenshot.
    /// -voiceprobe: how far ahead Unity reads a streamed PCMReaderCallback clip (the old voice playback path).
    /// -testfloorsnd: sound between floors (host measures the mixed output for sfx / voice on other floors).
    /// -teststress (+ -stresswav dir): fear breathing and heartbeat as the seeker comes closer.
    /// </summary>
    public partial class AudioTests : MonoBehaviour
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
            if (!Has("-testvoicechan") && !Has("-testpvol") && !Has("-voiceprobe") && !Has("-testfloorsnd") && !Has("-teststress") && !Has("-testsawsound")) return;
            var go = new GameObject("[AudioTests]");
            DontDestroyOnLoad(go);
            var t = go.AddComponent<AudioTests>();
            if (Has("-testvoicechan")) t.StartCoroutine(t.VoiceChannelTest());
            if (Has("-testpvol")) t.StartCoroutine(t.PlayerVolumeTest());
            if (Has("-voiceprobe")) t.StartCoroutine(t.StreamProbe());
            if (Has("-testfloorsnd")) t.StartCoroutine(t.FloorSoundTest());
            if (Has("-teststress")) t.StartCoroutine(t.StressTest());
            if (Has("-testsawsound")) t.StartCoroutine(t.SawSoundTest());
        }

        private static IEnumerator WaitPhase(MatchPhase phase)
        {
            var mm = MatchManager.Instance;
            while (mm == null || mm.Phase.Value != phase || NetPlayer.Local == null) { mm = MatchManager.Instance; yield return null; }
        }

        private static void Shot(string dir, string name)
        {
            if (string.IsNullOrEmpty(dir)) return;
            Directory.CreateDirectory(dir);
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, name + ".png"));
        }

        /// <summary>Records what the player hears: attached to the active AudioListener, sums the final mix.</summary>
        private static ListenerTap Tap()
        {
            AudioListener l = null;
            foreach (var a in FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude)) if (a.isActiveAndEnabled) l = a; // the one that hears
            if (l == null) return null;
            var tap = l.GetComponent<ListenerTap>();
            return tap != null ? tap : l.gameObject.AddComponent<ListenerTap>();
        }
    }
}
