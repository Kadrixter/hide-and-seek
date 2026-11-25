using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace HideAndSeek.Game
{
    /// <summary>One link's picture file in the <see cref="TvVideoCache"/> (filled by a worker thread).</summary>
    public sealed class TvDownload
    {
        public readonly string Url, Key;
        /// <summary>The finished, playable .mp4 (null while downloading / failed).</summary>
        public volatile string Path;
        /// <summary>Polish message for the TV screen when the video cannot be played.</summary>
        public volatile string Error;
        public volatile string Title;
        /// <summary>0..1 while downloading, 1 when done.</summary>
        public volatile float Progress;
        public bool Done => Path != null;

        public TvDownload(string url, string key)
        {
            Url = url;
            Key = key;
        }
    }

    /// <summary>
    /// Lobby TV pictures: YouTube has no progressive (picture + sound) MP4 any more and its streams are throttled to about
    /// real time when read in one piece, so - like the boombox songs (<see cref="RadioCache"/>) - the picture is downloaded
    /// whole by yt-dlp (fast, in chunks): H.264 at most 480p 30 fps (smaller for long videos) + AAC sound in one file, then
    /// remuxed by ffmpeg into a plain MP4 (brand mp42: Media Foundation refuses YouTube's "dash" brand) the VideoPlayer
    /// seeks well in; a video without H.264 (VP9 / AV1 only), or a file the player still refuses (<see cref="Repair"/>),
    /// is re-encoded by ffmpeg (libx264, at most 854 x 480, 30 fps; the sound is copied). Files live in %LOCALAPPDATA%\HideAndSeek\TvCache (a replay starts at once); cut-off
    /// downloads are deleted, and over <see cref="MaxBytes"/> or after <see cref="MaxAgeDays"/> days the oldest go.
    /// </summary>
    public static partial class TvVideoCache
    {
        public const long MaxBytes = 1L << 30; // 1 GB
        public const int MaxAgeDays = 7;
        /// <summary>Finished files: picture + sound (older picture-only files of test builds are not taken).</summary>
        private const string Ext = ".av.mp4";
        // Picture: H.264 480p up to 45 MB, else 360p up to 60 MB, else 240p (the download stays short: fast start), plain
        // https streams only (no HLS); without any H.264 the picture is re-encoded (Convert). Sound: AAC (m4a), else any,
        // merged into the same file - the TV plays its sound from it, so picture and sound always come together.
        private const string Video =
            "bv*[vcodec^=avc1][protocol=https][height>360][height<=480][fps<=30][filesize<45M]/bv*[vcodec^=avc1][protocol=https][height>240][height<=360][filesize<60M]" +
            "/bv*[vcodec^=avc1][protocol=https][height<=240]/bv*[vcodec^=avc1][height<=480]/bv*[vcodec^=avc1][height<=720]/bv*[height<=480]/bv*";
        private const string Formats = "(" + Video + ")+(ba[ext=m4a]/ba)/b[height<=480]/b";
        private static readonly Dictionary<string, TvDownload> All = new();
        private static readonly List<Process> Running = new();
        private static bool _hooked;
        private static string _dir;

        public static string Dir
        {
            get
            {
                if (_dir != null) return _dir;
                _dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HideAndSeek", "TvCache");
                try
                {
                    Directory.CreateDirectory(_dir);
                    foreach (var f in Directory.GetFiles(_dir))
                    {
                        var age = DateTime.UtcNow - File.GetLastWriteTimeUtc(f);
                        // Leftovers of a cut-off download (fresh ones may belong to another copy on this PC), old videos.
                        if (f.Contains(".dl") ? age > TimeSpan.FromHours(6) : age > TimeSpan.FromDays(MaxAgeDays)) File.Delete(f);
                    }
                }
                catch (Exception e) { Debug.Log("[Tv] cache dir: " + e.Message); }
                return _dir;
            }
        }

        /// <summary>The download of <paramref name="url"/>; starts it (again, after a failure) when needed.</summary>
        public static TvDownload Get(string url)
        {
            if (string.IsNullOrEmpty(url)) return null;
            string key = RadioCache.KeyOf(url);
            lock (All)
            {
                if (!_hooked)
                {
                    _hooked = true;
                    Application.quitting += KillAll;
                }
                if (All.TryGetValue(key, out var d) && d.Error == null) return d;
                d = new TvDownload(url, key);
                All[key] = d;
                string have = System.IO.Path.Combine(Dir, key + Ext);
                if (File.Exists(have) && IsDash(have)) TryDelete(have); // kept as YouTube sent it: the player refuses it
                if (File.Exists(have) && new FileInfo(have).Length > 0)
                {
                    d.Progress = 1f;
                    d.Path = have;
                    try { File.SetLastWriteTimeUtc(have, DateTime.UtcNow); } catch { /* read-only: fine */ }
                    Debug.Log($"[Tv] {url} from the cache");
                }
                else new Thread(() => Run(d)) { IsBackground = true, Name = "TvDownload" }.Start();
                return d;
            }
        }

        private static void Run(TvDownload d)
        {
            var sw = Stopwatch.StartNew();
            string raw = null;
            try
            {
                if (RadioTools.YtDlp == null || RadioTools.Ffmpeg == null) throw new TvError("Brak yt-dlp / ffmpeg (folder Tools obok gry).");
                string tmp = System.IO.Path.Combine(Dir, $"{d.Key}.dl{Process.GetCurrentProcess().Id}");
                string err = null;
                // YouTube now and then answers 403 to a stream URL: once more with fresh URLs.
                for (int attempt = 1; attempt <= 2 && raw == null; attempt++)
                {
                    err = null;
                    var psi = RadioTools.Psi(RadioTools.YtDlp, "--no-warnings", "--no-playlist", "--no-mtime", "--encoding", "utf-8",
                        "--match-filter", "!is_live", "-f", Formats, "--merge-output-format", "mp4", "--limit-rate", "12M", "--newline", "--progress",
                        "--progress-template", "download:HNSPROG %(progress._percent_str)s", "--print", "before_dl:HNSTITLE %(title)s",
                        "--print", "after_move:filepath", "-o", tmp + ".%(ext)s", "--", d.Url);
                    psi.StandardOutputEncoding = Encoding.UTF8;
                    var p = Process.Start(psi);
                    lock (Running) Running.Add(p);
                    try
                    {
                        p.ErrorDataReceived += (_, e) =>
                        {
                            if (string.IsNullOrEmpty(e.Data)) return;
                            Debug.Log("[Tv] yt-dlp: " + e.Data);
                            if (e.Data.Contains("ERROR") || e.Data.Contains("does not pass filter")) err ??= e.Data;
                        };
                        p.BeginErrorReadLine();
                        RadioTools.ReadLinesUntilExit(p, l =>
                        {
                            string line = l.Trim();
                            if (line.StartsWith("HNSPROG"))
                            {
                                if (float.TryParse(line.Substring(7).Trim().TrimEnd('%'), System.Globalization.NumberStyles.Float,
                                        System.Globalization.CultureInfo.InvariantCulture, out float pc))
                                    d.Progress = Mathf.Max(d.Progress, Mathf.Clamp01(pc / 100f) * 0.8f); // picture, then sound
                            }
                            else if (line.StartsWith("HNSTITLE")) d.Title = line.Substring(8).Trim();
                            else if (line.Length > 0 && File.Exists(line)) raw = line;
                        }, 30 * 60 * 1000);
                    }
                    finally
                    {
                        lock (Running) Running.Remove(p);
                        try { p.Dispose(); } catch { /* gone */ }
                    }
                    if (raw == null && (err == null || !err.Contains("403"))) break;
                    if (raw == null) Debug.Log("[Tv] 403 from YouTube - trying again");
                }
                if (raw == null) throw new TvError(Explain(err), err);
                string final = System.IO.Path.Combine(Dir, d.Key + Ext);
                bool h264 = raw.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) && !raw.Contains(".webm");
                if (!h264) Debug.Log($"[Tv] {System.IO.Path.GetFileName(raw)} is not H.264: re-encoding");
                Convert(raw, tmp + ".out.mp4", final, !h264, d); // not tmp + ".mp4": that is the download itself
                d.Progress = 1f;
                d.Path = final;
                Debug.Log($"[Tv] downloaded {d.Url} -> {System.IO.Path.GetFileName(final)} ({new FileInfo(final).Length / 1024} kB) in {sw.ElapsedMilliseconds} ms");
                Trim();
            }
            catch (Exception e)
            {
                d.Error = e is TvError te ? te.Message : "Nie udało się wczytać filmu.";
                Debug.Log($"[Tv] download failed {d.Url}: {(e is TvError t2 && t2.Detail != null ? t2.Detail : e.Message)}");
            }
            finally
            {
                if (raw != null) TryDelete(raw);
            }
        }

        /// <summary>
        /// The player refused <paramref name="bad"/>'s file: the same picture re-encoded (a new download entry under its own
        /// key, the original file is kept for nothing else and deleted).
        /// </summary>
        public static TvDownload Repair(TvDownload bad)
        {
            if (bad == null || bad.Path == null) return null;
            string key = bad.Key + "_x";
            lock (All)
            {
                if (All.TryGetValue(key, out var d) && d.Error == null) return d;
                d = new TvDownload(bad.Url, key) { Title = bad.Title, Progress = 0.02f };
                All[key] = d;
                string final = System.IO.Path.Combine(Dir, key + Ext);
                if (File.Exists(final) && new FileInfo(final).Length > 0) { d.Progress = 1f; d.Path = final; return d; }
                string src = bad.Path;
                new Thread(() =>
                {
                    try
                    {
                        Convert(src, System.IO.Path.Combine(Dir, $"{key}.dl{Process.GetCurrentProcess().Id}.mp4"), final, true, d);
                        d.Progress = 1f;
                        d.Path = final;
                        Debug.Log($"[Tv] re-encoded {bad.Url} -> {System.IO.Path.GetFileName(final)}");
                    }
                    catch (Exception e)
                    {
                        d.Error = e is TvError te ? te.Message : "Tego filmu nie da się odtworzyć.";
                        Debug.Log("[Tv] re-encode failed: " + e.Message);
                    }
                }) { IsBackground = true, Name = "TvRepair" }.Start();
                return d;
            }
        }

        /// <summary>
        /// YouTube's file -> a plain MP4 with the index in front and brand mp42 (<paramref name="reencode"/>: H.264 by
        /// libx264, at most 854 x 480 at 30 fps, else the stream copied as it is). Video only.
        /// </summary>
        private static void Convert(string raw, string tmp, string final, bool reencode, TvDownload d)
        {
            var args = new List<string> { "-y", "-loglevel", "info", "-nostdin", "-nostats", "-progress", "pipe:1", "-i", raw, "-map", "0:v:0", "-map", "0:a:0?", "-c:a", "copy", "-map_metadata", "-1" };
            if (reencode)
                args.AddRange(new[] { "-c:v", "libx264", "-preset", "veryfast", "-crf", "23", "-profile:v", "main", "-pix_fmt", "yuv420p",
                    "-vf", "scale='min(854,iw)':-2,fps=30" });
            else args.AddRange(new[] { "-c:v", "copy" });
            args.AddRange(new[] { "-brand", "mp42", "-movflags", "+faststart", "-f", "mp4", tmp });
            var psi = RadioTools.Psi(RadioTools.Ffmpeg, args.ToArray());
            var sw = Stopwatch.StartNew();
            float from = d.Progress, duration = 0f;
            string lastErr = null;
            using (var p = Process.Start(psi))
            {
                lock (Running) Running.Add(p);
                try { p.PriorityClass = reencode ? ProcessPriorityClass.Normal : ProcessPriorityClass.BelowNormal; } catch { /* exited */ }
                p.ErrorDataReceived += (_, e) =>
                {
                    if (string.IsNullOrEmpty(e.Data)) return;
                    string l = e.Data.Trim();
                    int at = l.IndexOf("Duration: ", StringComparison.Ordinal);
                    if (at >= 0 && duration <= 0f && TimeSpan.TryParse(l.Substring(at + 10).Split(',')[0], System.Globalization.CultureInfo.InvariantCulture, out var ts))
                        duration = (float)ts.TotalSeconds;
                    if (l.Contains("rror") || l.Contains("nvalid")) lastErr = l;
                };
                p.BeginErrorReadLine();
                RadioTools.ReadLinesUntilExit(p, line =>
                {
                    // -progress: out_time_us=12345678 (microseconds of the output so far)
                    if (!line.StartsWith("out_time_us=") || duration <= 0f) return;
                    if (long.TryParse(line.Substring(12), out long us) && us > 0)
                        d.Progress = Mathf.Max(d.Progress, from + (0.995f - from) * Mathf.Clamp01(us / 1e6f / duration));
                }, reencode ? 600000 : 60000);
                lock (Running) Running.Remove(p);
                bool failed = !p.HasExited || p.ExitCode != 0;
                if (failed || !File.Exists(tmp) || new FileInfo(tmp).Length == 0)
                {
                    TryDelete(tmp);
                    throw new TvError("Nie udało się przygotować filmu.", "ffmpeg: " + lastErr);
                }
            }
            Debug.Log($"[Tv] {(reencode ? "re-encoded" : "remuxed")} {System.IO.Path.GetFileName(raw)} ({duration:0}s) in {sw.ElapsedMilliseconds} ms");
            try
            {
                if (File.Exists(final)) TryDelete(tmp); // another copy on this PC was faster
                else File.Move(tmp, final);
            }
            catch (Exception) { if (!File.Exists(final)) throw; }
        }

        private static void KillAll()
        {
            lock (Running)
            {
                foreach (var p in Running) RadioTools.KillTree(p);
                Running.Clear();
            }
        }
    }
}
