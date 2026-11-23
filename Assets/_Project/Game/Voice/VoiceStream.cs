using UnityEngine;

namespace HideAndSeek.Game.Voice
{
    /// <summary>
    /// One remote talker's voice. Both modes of <see cref="VoiceMode"/> share this jitter buffer and the per-sample voice
    /// path of the pre-22.11 playback (commit 2ffd4af): mu-law decode, gain, soft clip x / (1 + 0.35|x|), a dry buffer
    /// fades its last sample out (x 0.96 per 16 kHz sample) - all at 16 kHz, see <see cref="NextSample"/>.
    /// <para>Classic (default, unchanged): a 16 kHz mono streamed AudioClip whose PCMReaderCallback reads the buffer,
    /// Unity resamples it. Start at the delay setting (160 ms), +60 ms after every dropout while the talker is speaking,
    /// -20 ms after 8 steady seconds (down to setting - 60 ms, up to 600 ms); far behind (target + 333 ms) drops the oldest
    /// audio. Unity reads a streamed clip ~0.5 s ahead, which is most of the delay.</para>
    /// <para>Low latency (<c>VoiceStream.Low.cs</c>): the same samples pulled on the audio thread
    /// (OnAudioFilterRead on a constant clip) and resampled linearly to the output rate, no read-ahead.</para>
    /// Packets carry a sequence number: duplicates / late ones are dropped, a missing one is simply skipped (no
    /// concealment, no time-stretching). The AudioSource is created by <see cref="VoicePlayback"/> / <see cref="PhoneVoice"/> /
    /// <see cref="PaVoice"/>; their filters (echo, low-pass, walkie-talkie) act on what this stream plays.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public partial class VoiceStream : MonoBehaviour
    {
        public const int Rate = VoiceCodec.SampleRate;
        private const int BufferSize = Rate * 3;

        private readonly float[] _ring = new float[BufferSize];
        private readonly object _lock = new object();
        private int _write, _read, _count;
        private bool _started;
        private int _target = Rate * 160 / 1000;          // classic: start at 160 ms
        private int _minTarget = Rate * 100 / 1000, _maxTarget = Rate * 600 / 1000;
        private volatile int _underruns;
        private float _lastSample;
        private int _seenUnderruns;
        private float _steadySince;
        private volatile float _gain = 2.4f;

        private ushort _lastSeq;
        private bool _haveSeq;
        private float _lastPushTime = -10f;

        // dry periods (audio thread writes, Push reads, both under _lock)
        private bool _dry, _dryReal;
        private int _dryRun;

        /// <summary>Linear gain applied to the voice (boost x chat volume x per-player volume).</summary>
        public float Gain { set => _gain = value; }
        public int TargetMs => _target * 1000 / Rate;
        public int BufferedMs { get { lock (_lock) return _count * 1000 / Rate; } }
        public int Lost { get; private set; }
        public int Late { get; private set; }
        /// <summary>Times the buffer ran dry (classic: also every sentence end).</summary>
        public int Underruns => _underruns;

        private void Awake()
        {
            _src = GetComponent<AudioSource>();
            _classicClip = AudioClip.Create("voice", Rate, 1, Rate, true, OnRead);
            _src.loop = true;
            Swap(VoiceMode.Mode, VoiceMode.DelayMs); // the source is not playing yet: sets the clip and the buffer policy
        }

        /// <summary>Main thread: one received frame. Returns false when it was late / duplicated and dropped.</summary>
        public bool Push(ushort seq, byte[] data, int length, long stamp = 0)
        {
            float now = Time.unscaledTime;
            bool fresh = !_haveSeq || now - _lastPushTime > 0.5f;
            _lastPushTime = now;
            if (!fresh)
            {
                int diff = (short)(seq - _lastSeq);
                if (diff <= 0) { Late++; return false; }
                if (diff > 1 && diff <= 7) Lost += diff - 1;
            }
            _haveSeq = true;
            _lastSeq = seq;
            lock (_lock)
            {
                // Ran dry while the talker kept talking: a real dropout, not the end of a sentence.
                if (_dry && !fresh && !_dryReal)
                {
                    _dryReal = true;
                    _dropouts++;
                }
                for (int i = 0; i < length; i++)
                {
                    if (_count >= BufferSize)
                    {
                        _read = (_read + 1) % BufferSize;
                        _count--;
                    }
                    _ring[_write] = VoiceCodec.Decode(data[i]);
                    _write = (_write + 1) % BufferSize;
                    _count++;
                }
                if (_low) TrimLow();
                else
                {
                    // Too far behind (e.g. after a long stall): drop the oldest audio down to the target delay.
                    int limit = _target + Rate / 3;
                    if (_count > limit)
                    {
                        int drop = _count - _target;
                        _read = (_read + drop) % BufferSize;
                        _count -= drop;
                    }
                }
            }
            return true;
        }

        /// <summary>Audio thread, under the lock, before a block: (re)start once the buffer holds the target.</summary>
        private void TryStart()
        {
            if (_started || _count < _target) return;
            _started = true;
            if (_dry && _dryReal)
            {
                _gapSamples += _dryRun;
                _gaps++;
            }
            _dry = false;
        }

        /// <summary>
        /// Audio thread, under the lock: the next 16 kHz sample of the voice - exactly the pre-22.11 path (gain, soft clip,
        /// fade-out of the last sample when the buffer runs dry). Both modes play these samples.
        /// </summary>
        private float NextSample(float gain)
        {
            if (_started && _count > 0)
            {
                float x = _ring[_read] * gain;
                _read = (_read + 1) % BufferSize;
                _count--;
                // Soft clip so the boost never crackles.
                x = x / (1f + 0.35f * Mathf.Abs(x));
                _lastSample = Mathf.Clamp(x, -1f, 1f);
            }
            else
            {
                if (_started)
                {
                    _underruns++; // ran dry mid-speech: buffer more next time
                    _dry = true;
                    _dryReal = false;
                    _dryRun = 0;
                }
                _started = false;
                if (_dry) _dryRun++;
                // Fade the last sample out instead of a hard click.
                _lastSample *= 0.96f;
            }
            DiagRecord(_lastSample);
            return _lastSample;
        }

        /// <summary>Classic: Unity's streamed clip asks for the next 16 kHz samples (well ahead of playback).</summary>
        private void OnRead(float[] data)
        {
            float gain = _gain;
            lock (_lock)
            {
                if (_low) { System.Array.Clear(data, 0, data.Length); return; } // not this mode's clip (being swapped)
                TryStart();
                for (int i = 0; i < data.Length; i++) data[i] = NextSample(gain);
            }
            Post?.Process(data); // optional colouring (public-address horn); null for the normal voice
        }

        /// <summary>Main thread, every frame: buffer policy of the current mode, mode / delay changes from the host.</summary>
        public void Tick(bool speaking)
        {
            if (CheckMode()) return;
            if (_low)
            {
                TickLow();
                return;
            }
            // Classic: each dropout while talking adds 60 ms; 8 s without one removes 20 ms.
            int u = _underruns;
            if (u != _seenUnderruns)
            {
                if (speaking) _target = Mathf.Min(_maxTarget, _target + Rate * 60 / 1000);
                _seenUnderruns = u;
                _steadySince = Time.time;
            }
            else if (Time.time - _steadySince > 8f && _target > _minTarget)
            {
                _target = Mathf.Max(_minTarget, _target - Rate * 20 / 1000);
                _steadySince = Time.time;
            }
        }
    }
}
