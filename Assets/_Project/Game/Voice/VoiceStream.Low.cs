using UnityEngine;

namespace HideAndSeek.Game.Voice
{
    /// <summary>
    /// Low-latency mode of <see cref="VoiceStream"/> and the switching between the modes.
    /// <para>The AudioSource loops a constant 1.0 clip; <see cref="OnAudioFilterRead"/> multiplies it by the voice, so the
    /// source's 3D panning / rolloff and the filters after this component (echo, low-pass, walkie-talkie) still apply.
    /// The voice samples come from the same <see cref="VoiceStream.NextSample"/> as in the classic mode (gain and soft clip
    /// at 16 kHz, then the public-address colouring), pulled in 5 ms chunks only when the output needs them, and are
    /// resampled linearly to the output rate like Unity does for the classic clip. So the two modes sound the same and
    /// differ in the delay: no ~0.5 s stream read-ahead, 20 ms frames, a target the host picks.</para>
    /// <para>(22.11 had soft clip and gain after a Hermite 16 -> 48 kHz interpolation: the clipping harmonics then reach
    /// 8-24 kHz as hiss and rasp - the "przester" when talking - while the classic clip gets them below 8 kHz and
    /// Unity's linear resampler rolls the top off. -voicelegacy22 brings that back for comparisons.)</para>
    /// Changing the mode or the delay fades the source out (10 ms), clears the buffer, swaps the clip and restarts.
    /// </summary>
    public partial class VoiceStream
    {
        private const int Chunk = 80;                       // 5 ms of 16 kHz samples per pull
        private const int LowRise = Rate * 20 / 1000;       // + 20 ms after a real dropout
        private const int LowHeadroom = Rate * 120 / 1000;  // ... up to setting + 120 ms
        private const int LowTrim = Rate * 100 / 1000;      // more than target + 100 ms queued: cut back to the target
        private const int TrimFade = 48;                    // 3 ms fade-in after a cut

        private static AudioClip _dc;
        /// <summary>Tests: -voicelegacy22 = the 22.11 low-latency colouring (Hermite, soft clip after resampling).</summary>
        public static bool Legacy22 = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-voicelegacy22") >= 0;

        private AudioSource _src;
        private AudioClip _classicClip;
        private volatile bool _low;
        private byte _mode = 255;
        private int _delayMs, _baseTarget;
        private volatile float _step = Rate / 48000f;
        private volatile int _dropouts;
        private int _seenDropouts;
        private long _gapSamples;
        private int _gaps;

        // audio thread (under _lock)
        private readonly float[] _chunk = new float[Chunk];
        private int _chunkPos = Chunk;
        private float _p0, _p1, _pm1, _p2, _frac;
        private float _fade = 1f;
        private volatile bool _fadeReq, _faded;
        private float _fadeDeadline;

        /// <summary>The 22.11-style voice that only multiplies the 1.0 clip (shared by every stream).</summary>
        private static AudioClip DcClip()
        {
            if (_dc != null) return _dc;
            int rate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
            _dc = AudioClip.Create("voice-dc", rate, 1, rate, false);
            var ones = new float[rate];
            for (int i = 0; i < ones.Length; i++) ones[i] = 1f;
            _dc.SetData(ones, 0);
            return _dc;
        }

        /// <summary>Main thread: true while a mode / delay change is in progress (fading out, then swapped).</summary>
        private bool CheckMode()
        {
            int outRate = AudioSettings.outputSampleRate;
            if (outRate > 0) _step = Rate / (float)outRate;
            byte mode = VoiceMode.Mode;
            int delay = VoiceMode.DelayMs;
            if (mode == _mode && delay == _delayMs) return false;
            // A playing source fades out on the audio thread first (no click); a stopped one is swapped at once.
            if (_src.isPlaying && !_faded)
            {
                if (!_fadeReq)
                {
                    _fadeReq = true;
                    _fadeDeadline = Time.unscaledTime + 0.25f; // the audio thread may not be calling us (virtual source)
                }
                if (Time.unscaledTime < _fadeDeadline) return true;
            }
            Swap(mode, delay);
            return true;
        }

        /// <summary>Main thread: clears the buffer, puts the clip of <paramref name="mode"/> on the source, restarts it.</summary>
        private void Swap(byte mode, int delay)
        {
            bool playing = _src.isPlaying;
            if (playing) _src.Stop(); // also flushes what Unity read ahead from the classic clip
            bool low = mode == VoiceMode.LowLatency;
            lock (_lock)
            {
                _write = _read = _count = 0;
                _started = false;
                _lastSample = 0f;
                _dry = _dryReal = false;
                _dryRun = 0;
                _chunkPos = Chunk;
                _p0 = _p1 = _pm1 = _p2 = _frac = 0f;
                _low = low;
                _fade = 1f;
            }
            _haveSeq = false;
            _src.clip = low ? DcClip() : _classicClip;
            _mode = mode;
            _delayMs = delay;
            int ms = Rate * delay / 1000;
            if (low)
            {
                _baseTarget = ms;
                _minTarget = ms;
                _maxTarget = ms + LowHeadroom;
            }
            else
            {
                _baseTarget = ms;
                _minTarget = Mathf.Max(Rate * 40 / 1000, ms - Rate * 60 / 1000); // 160 -> 100 ms as before
                _maxTarget = Mathf.Max(Rate * 600 / 1000, ms);
            }
            _target = ms;
            _seenUnderruns = _underruns;
            _seenDropouts = _dropouts;
            _steadySince = Time.time;
            _fadeReq = false;
            _faded = false;
            if (playing) _src.Play();
        }

        /// <summary>Main thread: a real dropout adds 20 ms (up to setting + 120 ms); 5 steady seconds give 10 ms back.</summary>
        private void TickLow()
        {
            int d = _dropouts;
            if (d != _seenDropouts)
            {
                _target = Mathf.Min(_maxTarget, _target + LowRise);
                _seenDropouts = d;
                _steadySince = Time.time;
            }
            else if (Time.time - _steadySince > 5f && _target > _baseTarget)
            {
                _target = Mathf.Max(_baseTarget, _target - Rate * 10 / 1000);
                _steadySince = Time.time;
            }
        }

        /// <summary>Push (main thread, under the lock): a burst after a stall is cut back to the target, 3 ms fade-in at the cut.</summary>
        private void TrimLow()
        {
            if (!_started || _count <= _target + LowTrim) return;
            int drop = _count - _target;
            _read = (_read + drop) % BufferSize;
            _count -= drop;
            for (int i = 0; i < TrimFade && i < _count; i++) _ring[(_read + i) % BufferSize] *= i / (float)TrimFade;
        }

        /// <summary>Audio thread: next voice sample in the 16 kHz domain (chunks of 5 ms through NextSample + Post).</summary>
        private float Pull(float gain)
        {
            if (_chunkPos >= Chunk)
            {
                TryStart();
                for (int k = 0; k < Chunk; k++) _chunk[k] = Legacy22 ? NextRaw() : NextSample(gain);
                Post?.Process(_chunk);
                _chunkPos = 0;
            }
            return _chunk[_chunkPos++];
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (!_low)
            {
                // Classic: the clip already carries the voice; only the fade of a mode change happens here.
                if (_fadeReq) FadeOut(data, channels);
                return;
            }
            float gain = _gain;
            lock (_lock)
            {
                float step = _step;
                for (int i = 0; i < data.Length; i += channels)
                {
                    float x;
                    if (Legacy22)
                    {
                        // 22.11: 4-point Hermite of the raw voice, gain and soft clip after it (at the output rate).
                        float t = _frac;
                        x = _p0 + 0.5f * t * (_p1 - _pm1 + t * (2f * _pm1 - 5f * _p0 + 4f * _p1 - _p2 + t * (3f * (_p0 - _p1) + _p2 - _pm1)));
                        x *= gain;
                        x /= 1f + 0.35f * Mathf.Abs(x);
                    }
                    else x = _p0 + (_p1 - _p0) * _frac; // linear, like Unity's resampler of the classic clip
                    _frac += step;
                    while (_frac >= 1f)
                    {
                        _frac -= 1f;
                        if (Legacy22) { _pm1 = _p0; _p0 = _p1; _p1 = _p2; _p2 = Pull(gain); }
                        else { _p0 = _p1; _p1 = Pull(gain); }
                    }
                    if (_fadeReq)
                    {
                        _fade = Mathf.Max(0f, _fade - 1f / 480f);
                        x *= _fade;
                    }
                    for (int c = 0; c < channels; c++) data[i + c] *= x;
                }
                if (_fadeReq && _fade <= 0f) _faded = true;
            }
        }

        /// <summary>Audio thread, classic mode: 10 ms ramp to silence before a mode / delay change.</summary>
        private void FadeOut(float[] data, int channels)
        {
            for (int i = 0; i < data.Length; i += channels)
            {
                _fade = Mathf.Max(0f, _fade - 1f / 480f);
                for (int c = 0; c < channels; c++) data[i + c] *= _fade;
            }
            if (_fade <= 0f) _faded = true;
        }

        /// <summary>-voicelegacy22 only: the raw decoded sample (gain and soft clip come after the interpolation).</summary>
        private float NextRaw()
        {
            if (_started && _count > 0)
            {
                float x = _ring[_read];
                _read = (_read + 1) % BufferSize;
                _count--;
                _lastSample = x;
            }
            else
            {
                if (_started) { _underruns++; _dry = true; _dryReal = false; _dryRun = 0; }
                _started = false;
                if (_dry) _dryRun++;
                _lastSample *= 0.96f;
            }
            return _lastSample;
        }
    }
}
