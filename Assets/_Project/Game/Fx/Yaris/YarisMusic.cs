using System.Threading.Tasks;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Music of the Yaris egg: <c>Resources/Audio/YarisDealer</c> (mp3 / ogg / wav), loaded on first use. No file = a
    /// silent dance at <see cref="YarisEgg.Bpm"/> (one info line in the log, nothing else). With a file the tempo and the
    /// first beat are estimated once from its loudness envelope (onset strength autocorrelation over the first 40 s,
    /// in a worker thread); a weak result keeps the constant.
    /// </summary>
    public static class YarisMusic
    {
        private static bool _tried;
        private static AudioClip _clip;
        private static Task<(float bpm, float offset, float conf)> _detect;
        private static float _bpm = YarisEgg.Bpm, _offset;

        /// <summary>The music clip (null when there is none).</summary>
        public static AudioClip Clip
        {
            get
            {
                if (_tried) return _clip;
                _tried = true;
                _clip = Resources.Load<AudioClip>(YarisEgg.MusicResource);
                if (_clip == null)
                {
                    Debug.Log($"[Yaris] no music file (Resources/{YarisEgg.MusicResource}) - dancing without music at {YarisEgg.Bpm:0} BPM");
                    return null;
                }
                Debug.Log($"[Yaris] music '{_clip.name}' {_clip.length:0.0} s, {_clip.frequency} Hz, {_clip.channels} ch, load={_clip.loadType}");
                if (YarisEgg.DetectBpm) StartDetect();
                return _clip;
            }
        }

        /// <summary>Beats per minute of the squash (detected or the constant).</summary>
        public static float Bpm { get { Poll(); return _bpm; } }

        /// <summary>Time of the first beat in the music file (s).</summary>
        public static float Offset { get { Poll(); return _offset; } }

        private static void StartDetect()
        {
            if (_clip.loadType != AudioClipLoadType.DecompressOnLoad)
            {
                Debug.Log($"[Yaris] tempo detection skipped (clip load type {_clip.loadType}), using {YarisEgg.Bpm:0} BPM");
                return;
            }
            if (_clip.loadState != AudioDataLoadState.Loaded && !_clip.LoadAudioData()) return;
            int frames = Mathf.Min(_clip.samples, _clip.frequency * 40);
            int ch = Mathf.Max(1, _clip.channels);
            var data = new float[frames * ch];
            if (!_clip.GetData(data, 0)) return;
            int rate = _clip.frequency;
            _detect = Task.Run(() => Detect(data, ch, rate));
        }

        private static void Poll()
        {
            if (_detect == null || !_detect.IsCompleted) return;
            var t = _detect;
            _detect = null;
            if (t.IsFaulted) { Debug.Log("[Yaris] tempo detection failed: " + t.Exception?.GetBaseException().Message); return; }
            var (bpm, offset, conf) = t.Result;
            if (bpm > 0f && conf >= 1.25f)
            {
                _bpm = bpm;
                _offset = offset;
                Debug.Log($"[Yaris] tempo detected {bpm:0.0} BPM, first beat {offset:0.000} s (confidence {conf:0.00})");
            }
            else Debug.Log($"[Yaris] tempo unclear ({bpm:0.0} BPM, confidence {conf:0.00}) - using {YarisEgg.Bpm:0} BPM");
        }

        /// <summary>Onset-strength envelope (10 ms hops) -> autocorrelation -> tempo, then the best beat phase.</summary>
        public static (float bpm, float offset, float conf) Detect(float[] data, int ch, int rate)
        {
            int frames = data.Length / ch;
            int hop = Mathf.Max(1, rate / 100);
            int n = frames / hop;
            if (n < 400) return (0f, 0f, 0f);
            var env = new float[n];
            var envLow = new float[n];
            float prev = 0f, lp = 0f, a = Mathf.Clamp01(2f * Mathf.PI * 150f / rate);
            for (int k = 0; k < n; k++)
            {
                double e = 0, eLow = 0;
                for (int f = k * hop; f < (k + 1) * hop; f++)
                {
                    float s = 0f;
                    for (int c = 0; c < ch; c++) s += data[f * ch + c];
                    s /= ch;
                    float d = s - prev; // first difference: favours transients (kicks, snares, hats)
                    prev = s;
                    e += d * d;
                    lp += (s - lp) * a;  // below ~150 Hz: kick drum and bass, for the beat phase
                    eLow += lp * lp;
                }
                env[k] = Mathf.Log(1f + 1000f * (float)(e / hop));
                envLow[k] = Mathf.Log(1f + 1000f * (float)(eLow / hop));
            }
            var flat = Flatten(env);
            var flatLow = Flatten(envLow);
            // autocorrelation for 60..200 BPM (lag in hops)
            int minLag = 30, maxLag = 100;
            var ac = new float[maxLag * 2 + 2];
            for (int lag = minLag / 2; lag < ac.Length; lag++)
            {
                double sum = 0;
                for (int k = 0; k + lag < n; k++) sum += flat[k] * flat[k + lag];
                ac[lag] = (float)(sum / (n - lag));
            }
            int best = 0;
            float bestScore = 0f, mean = 0f;
            for (int lag = minLag; lag <= maxLag; lag++)
            {
                // the beat period also shows at twice the lag; prefer tempi near 120 a little
                float bpmAt = 6000f / lag;
                float pref = Mathf.Exp(-0.5f * Mathf.Pow(Mathf.Log(bpmAt / 120f, 2f) / 0.9f, 2f));
                float score = (ac[lag] + 0.5f * ac[Mathf.Min(lag * 2, ac.Length - 1)]) * (0.6f + 0.4f * pref);
                mean += score;
                if (score > bestScore) { bestScore = score; best = lag; }
            }
            mean /= (maxLag - minLag + 1);
            if (best == 0 || mean <= 0f) return (0f, 0f, 0f);
            // parabolic refinement of the peak
            float y0 = ac[best - 1], y1 = ac[best], y2 = ac[best + 1];
            float den = y0 - 2f * y1 + y2;
            float lagF = best + (Mathf.Abs(den) > 1e-9f ? 0.5f * (y0 - y2) / den : 0f);
            // fold slow results into 80..160 BPM (a strong bar pattern often wins at twice the beat period)
            while (6000f / lagF < 80f && lagF / 2f >= minLag / 2f) lagF *= 0.5f;
            float bpm = 6000f / lagF;
            best = Mathf.Max(1, Mathf.RoundToInt(lagF));
            // beat phase: offset with the most onset energy on the grid
            int bestOff = 0;
            float bestSum = -1f;
            for (int o = 0; o < best; o++)
            {
                float sum = 0f;
                for (float k = o; k < n; k += lagF) sum += flatLow[(int)k] + 0.25f * flat[(int)k];
                if (sum > bestSum) { bestSum = sum; bestOff = o; }
            }
            return (bpm, bestOff * hop / (float)rate, bestScore / mean);
        }

        /// <summary>Positive slope of an envelope minus its 0.5 s running mean (onset strength).</summary>
        private static float[] Flatten(float[] env)
        {
            int n = env.Length;
            var onset = new float[n];
            for (int k = 1; k < n; k++) onset[k] = Mathf.Max(0f, env[k] - env[k - 1]);
            var flat = new float[n];
            const int win = 25;
            double run = 0;
            for (int k = 0; k < n; k++)
            {
                run += onset[k];
                if (k >= 2 * win) run -= onset[k - 2 * win];
                int cnt = Mathf.Min(k + 1, 2 * win);
                flat[k] = Mathf.Max(0f, onset[k] - (float)(run / cnt));
            }
            return flat;
        }
    }
}
