using System;
using System.Collections.Generic;
using System.Diagnostics;
using HideAndSeek.Game.Net;
using Unity.Collections;
using Unity.Netcode;

namespace HideAndSeek.Game.Voice
{
    /// <summary>
    /// Test hooks of the voice chat used by -testvoicemode (<c>VoiceModeTests</c>):
    /// <list type="bullet">
    /// <item>feed: a speech recording (16 kHz mono) goes out instead of the microphone, at the real-time pace of a
    /// microphone (frame k leaves when its last sample would have been captured), always transmitting;</item>
    /// <item>network simulation on the sender: every outgoing voice frame is held back by a random delay (in order, like
    /// a congested link) and a share of them is dropped. Off unless a test sets <see cref="SimJitterMs"/> / <see cref="SimLossPct"/>.</item>
    /// </list>
    /// </summary>
    public partial class VoiceChat
    {
        /// <summary>Max extra delay of a frame (ms); 0 = off.</summary>
        public static float SimJitterMs;
        /// <summary>Share of frames dropped (%).</summary>
        public static float SimLossPct;
        private static System.Random _simRng = new System.Random(1123);
        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private readonly List<(double due, ulong to, byte[] data)> _simQueue = new();
        private readonly Dictionary<ulong, double> _simLast = new();
        public int SimDropped { get; private set; }
        /// <summary>Tests: the microphone stays closed (only a feed transmits).</summary>
        public static bool TestNoMic;

        private float[] _feed;
        private int _feedPos;
        private long _feedT0;
        /// <summary>UTC ticks when the first sample of the current / last feed was "captured".</summary>
        public long FeedStartTicks => _feedT0;
        public bool Feeding => _feed != null;

        /// <summary>Restarts the simulated network with a fixed seed (every variant gets the same pattern).</summary>
        public static void SimReset(float jitterMs, float lossPct, int seed = 1123)
        {
            SimJitterMs = jitterMs;
            SimLossPct = lossPct;
            _simRng = new System.Random(seed);
        }

        /// <summary>Sends <paramref name="samples"/> (16 kHz mono) as if spoken into the microphone, starting now.</summary>
        public void StartFeed(float[] samples)
        {
            // The microphone gain is applied in ProcessFrame: undo it so the frames carry the recording as it is.
            float g = UnityEngine.Mathf.Max(0.01f, LocalProfile.MicGain);
            _feed = new float[samples.Length];
            for (int i = 0; i < samples.Length; i++) _feed[i] = samples[i] / g;
            _feedPos = 0;
            _feedT0 = DateTime.UtcNow.Ticks;
        }

        /// <summary>Update: sends every feed frame that is complete by now. True while a feed runs (no microphone).</summary>
        private bool FeedFrames(NetworkManager nm)
        {
            if (_feed == null)
            {
                if (TestNoMic) { Transmitting = false; MicLevel = 0f; }
                return TestNoMic;
            }
            int n = _frame.Length;
            long have = (DateTime.UtcNow.Ticks - _feedT0) * VoiceCodec.SampleRate / TimeSpan.TicksPerSecond;
            while (_feedPos + n <= have && _feedPos + n <= _feed.Length)
            {
                Array.Copy(_feed, _feedPos, _frame, 0, n);
                _feedPos += n;
                ProcessFrame(nm, true, 0);
            }
            if (_feedPos + n > _feed.Length)
            {
                _feed = null;
                Transmitting = false;
            }
            return true;
        }

        /// <summary>One voice message to a client: straight out, or through the simulated network.</summary>
        private void Send(NetworkManager nm, ulong to, FastBufferWriter writer)
        {
            if (SimJitterMs <= 0f && SimLossPct <= 0f)
            {
                nm.CustomMessagingManager.SendNamedMessage(Message, to, writer, Delivery);
                return;
            }
            if (_simRng.NextDouble() * 100.0 < SimLossPct) { SimDropped++; return; }
            double r = _simRng.NextDouble();
            double delay = SimJitterMs * r * r;                       // mostly small, sometimes most of the range
            if (_simRng.NextDouble() < 0.04) delay += SimJitterMs;   // rare spikes (a burst of traffic, a stalled frame)
            double now = Clock.Elapsed.TotalMilliseconds;
            _simLast.TryGetValue(to, out double last);
            double due = Math.Max(now + delay, last);                // a link keeps the order
            _simLast[to] = due;
            _simQueue.Add((due, to, writer.ToArray()));
        }

        /// <summary>Update: sends the held-back frames that are due.</summary>
        private void SimFlush(NetworkManager nm)
        {
            if (_simQueue.Count == 0) return;
            double now = Clock.Elapsed.TotalMilliseconds;
            int sent = 0;
            foreach (var q in _simQueue)
            {
                if (q.due > now) break; // in order per client; one list is fine for the tests (one listener)
                using var w = new FastBufferWriter(q.data.Length, Allocator.Temp);
                w.WriteBytesSafe(q.data);
                if (nm.CustomMessagingManager != null) nm.CustomMessagingManager.SendNamedMessage(Message, q.to, w, Delivery);
                sent++;
            }
            if (sent > 0) _simQueue.RemoveRange(0, sent);
        }
    }
}
