using System;
using System.Text;
using HideAndSeek.Game.Net;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Voice
{
    /// <summary>
    /// Test switches of the voice chat:
    /// -voicetest: a 440 Hz tone at real-time rate instead of the microphone, always transmitting; frames carry their
    /// capture time so the receiver can log the capture-to-playback latency (same PC, shared clock).
    /// -voicerelayall: the server relays every voice to everybody (checks that clients still drop the dead's voice).
    /// -debugpos / -voicetest: every 3 s a [Voice] line with packet counters and per-speaker playback state.
    /// </summary>
    public partial class VoiceChat
    {
        private static readonly bool TestTone = Has("-voicetest");
        private static readonly bool TestRelayAll = Has("-voicerelayall");
        private static readonly bool StatsLog = TestTone || Has("-debugpos");

        private float _toneClock;
        private long _tonePhase;
        private int _rxPackets, _txPackets, _rejected;
        private float _nextStats;

        private static bool Has(string arg) => Array.IndexOf(Environment.GetCommandLineArgs(), arg) >= 0;

        /// <summary>Number of received frames dropped because the living may not hear the dead (client-side check).</summary>
        public int Rejected => _rejected;
        public int Received => _rxPackets;
        public int Sent => _txPackets;

        private void ToneFrames(NetworkManager nm)
        {
            const float frameTime = VoiceCodec.FrameSamples / (float)VoiceCodec.SampleRate;
            _toneClock += Time.unscaledDeltaTime;
            while (_toneClock >= frameTime)
            {
                // first sample of this frame was "captured" _toneClock seconds ago
                long stamp = DateTime.UtcNow.Ticks - (long)(_toneClock * TimeSpan.TicksPerSecond);
                _toneClock -= frameTime;
                for (int i = 0; i < _frame.Length; i++, _tonePhase++)
                    _frame[i] = 0.3f * Mathf.Sin(_tonePhase * 2f * Mathf.PI * 440f / VoiceCodec.SampleRate);
                ProcessFrame(nm, true, stamp);
            }
        }

        private void LogStats()
        {
            if (!StatsLog || Time.unscaledTime < _nextStats) return;
            _nextStats = Time.unscaledTime + 3f;
            var sb = new StringBuilder();
            sb.Append($"[Voice] tx={_txPackets} rx={_rxPackets} rejected={_rejected} transmitting={Transmitting} " +
                      $"delivery={Delivery} me={(NetPlayer.Local != null ? NetPlayer.Local.Nick + "/" + NetPlayer.Local.Life.Value : "-")}");
            foreach (var vp in FindObjectsByType<VoicePlayback>(FindObjectsInactive.Exclude))
            {
                var owner = vp.GetComponentInParent<NetPlayer>();
                var src = vp.GetComponent<AudioSource>();
                var st = vp.Stream;
                sb.Append($"\n[Voice]   from {(owner != null ? owner.Nick : "?")}: packets={vp.Packets} speaking={vp.IsSpeaking} " +
                          $"channel={(vp.DeadChannel ? "dead-2D" : "3D")} spatial={(src != null ? src.spatialBlend : -1f):0} " +
                          $"vol={(src != null ? src.volume : -1f):0.00} floor={vp.FloorGain:0.00} " +
                          $"pvol={PlayerVoiceVolumes.Get(owner != null ? owner.Nick : null):0.00} " +
                          $"target={vp.BufferMs}ms buffered={(st != null ? st.BufferedMs : 0)}ms lost={(st != null ? st.Lost : 0)} " +
                          $"late={(st != null ? st.Late : 0)} {(st != null ? st.LatencyReport() : "")}");
            }
            Debug.Log(sb.ToString());
        }
    }
}
