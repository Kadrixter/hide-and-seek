using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HideAndSeek.Core;
using HideAndSeek.Game.Voice;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    public partial class AudioTests
    {
        private readonly Dictionary<string, int> _lastPackets = new Dictionary<string, int>();

        /// <summary>
        /// -testvoicechan, 3 copies, all with -voicetest (tone, always transmitting); host -forceseeker -autostart.
        /// Seeking +7 s the host kills the first hider, +12 s the second (round ends). Every copy logs how many voice
        /// frames it got from each player in each window and whether they play on the observer channel (2D).
        /// </summary>
        private IEnumerator VoiceChannelTest()
        {
            string dir = ArgOr("-vcshots", "");
            yield return WaitPhase(MatchPhase.Seeking);
            bool host = NetworkManager.Singleton.IsServer;
            var hiders = new List<NetPlayer>();
            foreach (var p in NetPlayer.All) if (p != null && p.Role.Value == PlayerRole.Hider) hiders.Add(p);
            hiders.Sort((a, b) => string.CompareOrdinal(a.Nick, b.Nick));

            yield return new WaitForSeconds(4f);
            Snapshot("reset");
            yield return new WaitForSeconds(3f);
            Snapshot("all alive");
            if (host && hiders.Count > 0) { MatchManager.Instance.TestKill(hiders[0]); Debug.Log($"[VoiceChan] host killed {hiders[0].Nick}"); }
            yield return new WaitForSeconds(1.5f);
            Snapshot("reset");
            yield return new WaitForSeconds(3f);
            Snapshot("first hider dead");
            if (NetPlayer.Local.Life.Value == LifeState.Dead) Shot(dir, "observer_" + NetPlayer.Local.Nick);
            yield return new WaitForSeconds(0.5f);
            if (host && hiders.Count > 1) { MatchManager.Instance.TestKill(hiders[1]); Debug.Log($"[VoiceChan] host killed {hiders[1].Nick}"); }
            yield return new WaitForSeconds(1.5f);
            Snapshot("reset");
            yield return new WaitForSeconds(3f);
            Snapshot("both hiders dead");
            Debug.Log($"[VoiceChan] done, rejected on this copy={VoiceChat.Instance?.Rejected}");
        }

        private void Snapshot(string label)
        {
            var me = NetPlayer.Local;
            var mm = MatchManager.Instance;
            var sb = new StringBuilder($"[VoiceChan] {label}: me={me.Nick} life={me.Life.Value} phase={mm.Phase.Value} " +
                                       $"observerLabel=\"{SpectatorVoiceLabel.Current}\" rejected={VoiceChat.Instance?.Rejected}");
            foreach (var p in NetPlayer.All)
            {
                if (p == null || p.IsOwner) continue;
                var vp = p.GetComponentInChildren<VoicePlayback>();
                int packets = vp != null ? vp.Packets : 0;
                _lastPackets.TryGetValue(p.Nick, out int before);
                _lastPackets[p.Nick] = packets;
                if (label == "reset") continue;
                var src = vp != null ? vp.GetComponent<AudioSource>() : null;
                sb.Append($"\n[VoiceChan]    from {p.Nick} ({p.Life.Value}): frames={packets - before} " +
                          $"channel={(vp != null && vp.DeadChannel ? "observer-2D" : "3D")} spatialBlend={(src != null ? src.spatialBlend : -1f):0}");
            }
            if (label != "reset") Debug.Log(sb.ToString());
        }

        /// <summary>
        /// -testpvol (client, lobby): sets the voice volume of every other player (180 % / 40 %), saves the settings
        /// file and checks the keys are in it, then opens the Esc page "Głośność graczy" for a screenshot (-pvolshots).
        /// </summary>
        private IEnumerator PlayerVolumeTest()
        {
            string dir = ArgOr("-pvolshots", "");
            yield return WaitPhase(MatchPhase.Lobby);
            while (NetPlayer.All.Count < 2) yield return null;
            yield return new WaitForSeconds(4f);
            int i = 0;
            foreach (var p in NetPlayer.All)
            {
                if (p == null || p.IsOwner) continue;
                float v = i++ % 2 == 0 ? 1.8f : 0.4f;
                PlayerVoiceVolumes.Set(p.Nick, v);
                Debug.Log($"[PlayerVol] set {p.Nick} -> {v:0.00}, read back {PlayerVoiceVolumes.Get(p.Nick):0.00}");
            }
            Prefs.Save();
            string file = Path.Combine(UserFiles.Root, "ustawienia.txt");
            foreach (var line in File.ReadAllLines(file))
                if (line.StartsWith("hns.pvol.")) Debug.Log("[PlayerVol] settings file: " + line);
            var hud = FindAnyObjectByType<GameHud>();
            if (hud != null) hud.ShowPlayerVolumeMenu();
            yield return new WaitForSeconds(1.5f);
            Shot(dir, "player_volumes");
            yield return new WaitForSeconds(2f);
            // the voice keeps playing with the new gain: log what the playback uses
            foreach (var p in NetPlayer.All)
            {
                if (p == null || p.IsOwner) continue;
                var vp = p.GetComponentInChildren<VoicePlayback>();
                Debug.Log($"[PlayerVol] {p.Nick}: playback={(vp != null)} packets={(vp != null ? vp.Packets : 0)} pvol={PlayerVoiceVolumes.Get(p.Nick):0.00} " +
                          $"chat={LocalProfile.ChatVolume:0.00} -> gain {2.4f * LocalProfile.ChatVolume * PlayerVoiceVolumes.Get(p.Nick):0.00}");
            }
        }

        /// <summary>
        /// -voiceprobe: plays a 16 kHz streamed clip with a PCMReaderCallback (what the voice used before) and logs how
        /// big and how often Unity asks for data - the read-ahead is extra delay on top of the jitter buffer.
        /// </summary>
        private IEnumerator StreamProbe()
        {
            yield return new WaitForSeconds(2f);
            var calls = new List<(double t, int n)>();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var go = new GameObject("StreamProbe");
            var src = go.AddComponent<AudioSource>();
            src.clip = AudioClip.Create("probe", VoiceCodec.SampleRate, 1, VoiceCodec.SampleRate, true, data =>
            {
                lock (calls) calls.Add((sw.Elapsed.TotalMilliseconds, data.Length));
                for (int k = 0; k < data.Length; k++) data[k] = 0f;
            });
            src.loop = true;
            src.volume = 0.01f;
            double started = sw.Elapsed.TotalMilliseconds;
            src.Play();
            yield return new WaitForSeconds(3f);
            src.Stop();
            AudioSettings.GetDSPBufferSize(out int len, out int num);
            var sb = new StringBuilder($"[VoiceProbe] output {AudioSettings.outputSampleRate} Hz, DSP buffer {len} x {num}; play() at {started:0} ms; callbacks:");
            long total = 0;
            lock (calls)
                foreach (var c in calls)
                {
                    total += c.n;
                    sb.Append($" {c.t - started:0}ms:{c.n}");
                }
            double played = 3000.0;
            sb.Append($"\n[VoiceProbe] requested {total} samples = {total * 1000.0 / VoiceCodec.SampleRate:0} ms of audio in {played:0} ms " +
                      $"-> read-ahead ~{total * 1000.0 / VoiceCodec.SampleRate - played:0} ms");
            Debug.Log(sb.ToString());
            Destroy(go);
        }
    }
}
