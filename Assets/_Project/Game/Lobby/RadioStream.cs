using System;
using System.Diagnostics;
using System.Threading;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace HideAndSeek.Game
{
    /// <summary>
    /// One track: yt-dlp (audio to stdout) -> ffmpeg (decode, seek, 48 kHz stereo s16le) -> lock-free ring buffer -> the
    /// boombox's audio filter (<see cref="BoomboxAudio"/>). The decoder thread is the only writer, the audio thread the
    /// only reader, so nothing on the audio path ever waits for a lock (the old ring took a lock per sample, and the main
    /// thread polled the same lock every frame).
    /// <para>Timing: <see cref="DspAtZero"/> is the DSP time at which track position 0 is (or would have been) heard. The
    /// reader keeps the playhead on it: silence until the start, skips forward when it is late (a copy that joined while
    /// the song played), waits when it is early. NaN = not playing (loading / paused); the buffer is kept.</para>
    /// </summary>
    public sealed class RadioStream : IDisposable
    {
        public const int Ch = 2;
        /// <summary>Decoded at the mixer's own rate (no resampling on the audio thread).</summary>
        private readonly int Rate, RingFrames;
        private Process _yt, _ff;
        private readonly float[] _ring;
        private long _written, _readFrames; // frames, written by one thread each (Interlocked / Volatile)
        private volatile bool _eof, _disposed;
        private Stopwatch _launchWatch;
        private readonly float _startPos;
        private bool _aligned;
        private long _dspAtZeroBits = BitConverter.DoubleToInt64Bits(double.NaN);

        public volatile string Error;
        /// <summary>Track position (s) of the first decoded sample (ffmpeg -ss).</summary>
        public float StartPos => _startPos;
        /// <summary>DSP time at which track position 0 plays; NaN while loading / paused (main thread writes).</summary>
        public double DspAtZero
        {
            get => BitConverter.Int64BitsToDouble(Interlocked.Read(ref _dspAtZeroBits));
            set => Interlocked.Exchange(ref _dspAtZeroBits, BitConverter.DoubleToInt64Bits(value));
        }
        /// <summary>Track position of the next sample the reader will play (seconds).</summary>
        public float Position => _startPos + Interlocked.Read(ref _readFrames) / (float)Rate;
        public float BufferedSeconds => (Interlocked.Read(ref _written) - Interlocked.Read(ref _readFrames)) / (float)Rate;
        /// <summary>Enough decoded audio to start without stalling (or the whole, short track).</summary>
        public bool Ready => BufferedSeconds >= 1.5f || (_eof && Interlocked.Read(ref _written) > 0);
        public bool Finished => _eof && Interlocked.Read(ref _written) == Interlocked.Read(ref _readFrames) && EverPlayed;
        public bool DecodeEnded => _eof;
        /// <summary>The decoder ended without a single sample (broken link / refused download).</summary>
        public bool Empty => _eof && Interlocked.Read(ref _written) == 0;
        /// <summary>Some audio actually came out (an empty / broken stream is retried instead of skipped).</summary>
        public volatile bool EverPlayed;
        /// <summary>True while the reader outputs the track (not waiting / paused / dry).</summary>
        public volatile bool Playing;
        public int Underruns;

        private RadioStream(float pos, int rate)
        {
            _startPos = pos;
            Rate = rate;
            RingFrames = rate * 30;
            _ring = new float[RingFrames * Ch];
        }

        public static RadioStream Start(string url, float pos, out string error)
        {
            error = null;
            if (RadioTools.YtDlp == null || RadioTools.Ffmpeg == null)
            {
                error = "Brak yt-dlp / ffmpeg - radio nie zagra.";
                return null;
            }
            int rate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
            var s = new RadioStream(pos, rate);
            // Starting yt-dlp / ffmpeg can take a second (antivirus scan of the 18 MB yt-dlp.exe): never on the main
            // thread, which also sends and receives the voice chat.
            new Thread(() =>
            {
                try { s.Launch(url); }
                catch (Exception e)
                {
                    s.Error = "Błąd startu: " + e.Message;
                    s._eof = true;
                }
            }) { IsBackground = true, Name = "RadioLaunch" }.Start();
            return s;
        }

        private void Launch(string url)
        {
            if (_disposed) return;
            var sw = Stopwatch.StartNew();
            // --limit-rate: the download never floods the player's connection (the voice chat shares it); 400 kB/s is
            // still 20x faster than the music plays.
            var yt = RadioTools.Psi(RadioTools.YtDlp, "--no-warnings", "--no-playlist", "--no-part", "-q", "--limit-rate", "400K",
                "-f", "bestaudio/best", "-o", "-", "--", url);
            lock (_ring) _yt = Process.Start(yt);
            var ff = RadioTools.Psi(RadioTools.Ffmpeg, "-loglevel", "error", "-i", "pipe:0", "-ss",
                _startPos.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture), "-vn", "-f", "s16le", "-ac", "2", "-ar", Rate.ToString(), "pipe:1");
            ff.RedirectStandardInput = true;
            lock (_ring) _ff = Process.Start(ff);
            _yt.ErrorDataReceived += (_, e) =>
            {
                if (string.IsNullOrEmpty(e.Data)) return;
                Debug.Log("[Radio] yt-dlp: " + e.Data);
                if (e.Data.Contains("ERROR")) Error = "yt-dlp: " + (e.Data.Length > 90 ? e.Data.Substring(0, 90) + "..." : e.Data);
            };
            _ff.ErrorDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) Debug.Log("[Radio] ffmpeg: " + e.Data); };
            _yt.BeginErrorReadLine();
            _ff.BeginErrorReadLine();
            // ffmpeg only decodes (light) - below normal so it never competes with the game; yt-dlp keeps normal priority,
            // its page extraction is what the start of a track waits for.
            try { _ff.PriorityClass = ProcessPriorityClass.BelowNormal; } catch { /* exited */ }
            _launchWatch = sw;
            Debug.Log($"[Radio] start {url} at {_startPos:0.0}s (processes up in {sw.ElapsedMilliseconds} ms)");
            if (_disposed) { Kill(); return; }
            new Thread(Pump) { IsBackground = true, Name = "RadioPump" }.Start();
            Decode();
        }

        /// <summary>yt-dlp stdout -> ffmpeg stdin.</summary>
        private void Pump()
        {
            var buf = new byte[32768];
            try
            {
                var input = _yt.StandardOutput.BaseStream;
                var output = _ff.StandardInput.BaseStream;
                int n;
                while (!_disposed && (n = input.Read(buf, 0, buf.Length)) > 0) output.Write(buf, 0, n);
                output.Close();
            }
            catch (Exception) { /* killed / pipe closed */ }
        }

        /// <summary>ffmpeg stdout (s16le) -> ring buffer, a block at a time (waits while the ring is full).</summary>
        private void Decode()
        {
            var buf = new byte[16384];
            int have = 0;
            try
            {
                var input = _ff.StandardOutput.BaseStream;
                while (!_disposed)
                {
                    int n = input.Read(buf, have, buf.Length - have);
                    if (n <= 0) break;
                    have += n;
                    int frames = have / (2 * Ch);
                    int done = 0;
                    while (done < frames && !_disposed)
                    {
                        long w = Interlocked.Read(ref _written);
                        int free = RingFrames - (int)(w - Interlocked.Read(ref _readFrames));
                        if (free <= 0) { Thread.Sleep(15); continue; }
                        int take = Math.Min(free, frames - done);
                        for (int f = 0; f < take; f++)
                        {
                            int src = (done + f) * 2 * Ch, dst = (int)((w + f) % RingFrames) * Ch;
                            for (int c = 0; c < Ch; c++)
                                _ring[dst + c] = (short)(buf[src + 2 * c] | (buf[src + 2 * c + 1] << 8)) / 32768f;
                        }
                        done += take;
                        if (Interlocked.Add(ref _written, take) == take) // publish after the samples are in place
                            Debug.Log($"[Radio] first audio after {_launchWatch?.ElapsedMilliseconds ?? -1} ms");
                    }
                    int used = frames * 2 * Ch;
                    have -= used;
                    if (have > 0) Buffer.BlockCopy(buf, used, buf, 0, have);
                }
            }
            catch (Exception) { /* killed */ }
            _eof = true;
        }

        /// <summary>
        /// Audio thread: <paramref name="frames"/> stereo frames into <paramref name="dst"/> (interleaved L R) for the block
        /// that starts at DSP time <paramref name="dspNow"/>. Silence while not due / paused / dry.
        /// </summary>
        public void Read(float[] dst, int frames, double dspNow)
        {
            Array.Clear(dst, 0, frames * Ch);
            double zero = DspAtZero;
            if (double.IsNaN(zero)) { Playing = false; _aligned = false; return; }
            long readF = Interlocked.Read(ref _readFrames);
            long avail = Interlocked.Read(ref _written) - readF;
            double want = dspNow - zero;              // where the clock says the playhead is
            double have = _startPos + readF / (double)Rate;
            double err = want - have;
            // First block after a start / resume: exact. Later only real drift (the server clock estimate is a bit noisy).
            double tol = _aligned ? 0.08 : 0.004;
            int outAt = 0;
            if (err < -tol)
            {
                // Early: silence until the playhead is due (maybe the whole block).
                outAt = (int)Math.Min(frames, Math.Round(-err * Rate));
                if (outAt >= frames) { Playing = false; return; }
                _aligned = true;
            }
            else if (err > tol)
            {
                // Late: drop what is already gone (as far as decoded).
                long skip = Math.Min(avail, (long)Math.Round(err * Rate));
                if (skip > 0)
                {
                    Interlocked.Add(ref _readFrames, skip);
                    readF += skip;
                    avail -= skip;
                }
                if (avail <= 0) { Playing = false; return; } // still decoding up to the right place
                _aligned = true;
            }
            else _aligned = true;

            int n = (int)Math.Min(frames - outAt, avail);
            for (int f = 0; f < n; f++)
            {
                int src = (int)((readF + f) % RingFrames) * Ch, d = (outAt + f) * Ch;
                dst[d] = _ring[src];
                dst[d + 1] = _ring[src + 1];
            }
            if (n > 0)
            {
                Interlocked.Add(ref _readFrames, n);
                EverPlayed = true;
            }
            if (n < frames - outAt && !_eof) Underruns++;
            Playing = n > 0;
        }

        /// <summary>Stops the track. The processes are killed on a worker thread (taskkill waits for them).</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            new Thread(Kill) { IsBackground = true, Name = "RadioKill" }.Start();
        }

        private void Kill()
        {
            Process yt, ff;
            lock (_ring) { yt = _yt; ff = _ff; _yt = _ff = null; }
            foreach (var p in new[] { yt, ff })
            {
                RadioTools.KillTree(p);
                try { p?.Dispose(); } catch { /* gone */ }
            }
        }
    }
}
