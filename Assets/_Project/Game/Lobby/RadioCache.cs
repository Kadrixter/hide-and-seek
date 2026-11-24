using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace HideAndSeek.Game
{
    /// <summary>One link's audio file in the <see cref="RadioCache"/> (filled by a worker thread).</summary>
    public sealed class RadioDownload
    {
        public readonly string Url, Key;
        /// <summary>The finished file (null while downloading / failed).</summary>
        public volatile string Path;
        public volatile string Error;
        /// <summary>0..1 while downloading, 1 when done.</summary>
        public volatile float Progress;
        public volatile bool Started;
        public bool Done => Path != null;

        public RadioDownload(string url, string key)
        {
            Url = url;
            Key = key;
        }
    }

    /// <summary>
    /// Boombox songs are downloaded whole, in the best audio quality YouTube has (yt-dlp "bestaudio", the original
    /// stream without re-encoding: usually Opus ~160 kbit/s), into %LOCALAPPDATA%\HideAndSeek\RadioCache, and played
    /// from the file (<see cref="RadioStream"/>). A song in the cache starts at once (loop, previous, the next session).
    /// <para>The current / next song download right away (<see cref="Get"/> with now), the rest of the queue one at a
    /// time in the background. Every copy of the game downloads for itself; copies on one PC share the folder (each
    /// downloads into its own temporary name and moves the file in when done). Over <see cref="MaxBytes"/> the songs
    /// used longest ago are deleted.</para>
    /// </summary>
    public static class RadioCache
    {
        public const long MaxBytes = 3L << 30; // 3 GB
        private static readonly Dictionary<string, RadioDownload> All = new();
        private static readonly Queue<RadioDownload> Background = new();
        private static readonly List<Process> Running = new();
        private static readonly Dictionary<string, string> Keys = new();
        private static bool _worker, _hooked;
        private static string _dir;

        public static string Dir
        {
            get
            {
                if (_dir != null) return _dir;
                _dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HideAndSeek", "RadioCache");
                try
                {
                    Directory.CreateDirectory(_dir);
                    // Leftovers of a download that was cut off (game closed); fresh ones may belong to another copy.
                    foreach (var f in Directory.GetFiles(_dir, "*.dl*"))
                        if (DateTime.UtcNow - File.GetLastWriteTimeUtc(f) > TimeSpan.FromHours(6)) File.Delete(f);
                }
                catch (Exception e) { Debug.Log("[Radio] cache dir: " + e.Message); }
                return _dir;
            }
        }

        /// <summary>
        /// The download of <paramref name="url"/>. <paramref name="now"/>: start it at once (the song is about to play,
        /// a failed one is tried again); else it waits for its turn in the background (a failed one stays failed).
        /// </summary>
        public static RadioDownload Get(string url, bool now)
        {
            if (string.IsNullOrEmpty(url)) return null;
            string key = KeyOf(url);
            lock (All)
            {
                if (!_hooked)
                {
                    _hooked = true;
                    Application.quitting += KillAll;
                }
                if (All.TryGetValue(key, out var d) && (d.Error == null || !now))
                {
                    if (now && !d.Started) StartNow(d);
                    return d;
                }
                d = new RadioDownload(url, key);
                All[key] = d;
                string have = Find(key);
                if (have != null)
                {
                    d.Progress = 1f;
                    d.Started = true;
                    d.Path = have;
                    try { File.SetLastWriteTimeUtc(have, DateTime.UtcNow); } catch { /* read-only: fine */ }
                }
                else if (now) StartNow(d);
                else
                {
                    Background.Enqueue(d);
                    if (!_worker)
                    {
                        _worker = true;
                        new Thread(Worker) { IsBackground = true, Name = "RadioPrefetch" }.Start();
                    }
                }
                return d;
            }
        }

        /// <summary>The download of <paramref name="url"/> if there is one (panel: state of a queue entry).</summary>
        public static RadioDownload Peek(string url)
        {
            if (string.IsNullOrEmpty(url)) return null;
            lock (All) return All.TryGetValue(KeyOf(url), out var d) ? d : null;
        }

        private static void StartNow(RadioDownload d)
        {
            d.Started = true;
            new Thread(() => Run(d, "3M")) { IsBackground = true, Name = "RadioDownload" }.Start();
        }

        private static void Worker()
        {
            while (true)
            {
                RadioDownload d = null;
                lock (All)
                {
                    while (Background.Count > 0 && d == null)
                    {
                        var c = Background.Dequeue();
                        if (!c.Started) d = c;
                    }
                    if (d == null) { _worker = false; return; }
                    d.Started = true;
                }
                Run(d, "1500K"); // background: gentler on the connection the voice chat shares
            }
        }

        private static void Run(RadioDownload d, string rate)
        {
            var sw = Stopwatch.StartNew();
            Process p = null;
            try
            {
                if (RadioTools.YtDlp == null) throw new Exception("brak yt-dlp");
                string tmp = Path.Combine(Dir, $"{d.Key}.dl{Process.GetCurrentProcess().Id}");
                var psi = RadioTools.Psi(RadioTools.YtDlp, "--no-warnings", "--no-playlist", "--no-mtime", "--encoding", "utf-8",
                    "-f", "bestaudio/best", "--limit-rate", rate, "--newline", "--progress",
                    "--progress-template", "download:HNSPROG %(progress._percent_str)s",
                    "--print", "after_move:filepath", "-o", tmp + ".%(ext)s", "--", d.Url);
                psi.StandardOutputEncoding = Encoding.UTF8;
                p = Process.Start(psi);
                lock (Running) Running.Add(p);
                string err = null, file = null, line;
                p.ErrorDataReceived += (_, e) =>
                {
                    if (string.IsNullOrEmpty(e.Data)) return;
                    Debug.Log("[Radio] yt-dlp: " + e.Data);
                    if (e.Data.Contains("ERROR")) err = e.Data.Length > 90 ? e.Data.Substring(0, 90) + "..." : e.Data;
                };
                p.BeginErrorReadLine();
                while ((line = p.StandardOutput.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (line.StartsWith("HNSPROG"))
                    {
                        string v = line.Substring(7).Trim().TrimEnd('%');
                        if (float.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float pc))
                            d.Progress = Mathf.Clamp01(pc / 100f) * 0.99f;
                    }
                    else if (line.Length > 0 && File.Exists(line)) file = line;
                }
                p.WaitForExit(15000);
                if (file == null) throw new Exception(err ?? "yt-dlp nie zapisał pliku");
                string final = Path.Combine(Dir, d.Key + Path.GetExtension(file));
                try
                {
                    if (File.Exists(final)) File.Delete(file); // another copy on this PC was faster
                    else File.Move(file, final);
                }
                catch (Exception) { if (!File.Exists(final)) final = file; }
                d.Progress = 1f;
                d.Path = final;
                Debug.Log($"[Radio] downloaded {d.Url} -> {Path.GetFileName(final)} ({new FileInfo(final).Length / 1024} kB) in {sw.ElapsedMilliseconds} ms");
                Trim();
            }
            catch (Exception e)
            {
                d.Error = "Nie udało się pobrać: " + e.Message;
                Debug.Log($"[Radio] download failed {d.Url}: {e.Message}");
            }
            finally
            {
                if (p != null)
                {
                    lock (Running) Running.Remove(p);
                    try { p.Dispose(); } catch { /* gone */ }
                }
            }
        }

        /// <summary>A finished file of <paramref name="key"/> (any extension; not a temporary one).</summary>
        private static string Find(string key)
        {
            try
            {
                foreach (var f in Directory.GetFiles(Dir, key + ".*"))
                    if (Path.GetFileNameWithoutExtension(f) == key && new FileInfo(f).Length > 0) return f;
            }
            catch { /* no folder */ }
            return null;
        }

        /// <summary>Over <see cref="MaxBytes"/>: deletes the songs used longest ago.</summary>
        private static void Trim()
        {
            try
            {
                var files = new List<FileInfo>();
                long total = 0;
                foreach (var f in new DirectoryInfo(Dir).GetFiles())
                {
                    if (f.Name.Contains(".dl")) continue;
                    files.Add(f);
                    total += f.Length;
                }
                if (total <= MaxBytes) return;
                files.Sort((a, b) => a.LastWriteTimeUtc.CompareTo(b.LastWriteTimeUtc));
                foreach (var f in files)
                {
                    if (total <= MaxBytes * 0.9) break;
                    string key = Path.GetFileNameWithoutExtension(f.Name);
                    lock (All) if (All.ContainsKey(key)) continue; // in this session's queue
                    try { f.Delete(); total -= f.Length; } catch { /* playing in another copy */ }
                }
            }
            catch (Exception e) { Debug.Log("[Radio] cache trim: " + e.Message); }
        }

        private static void KillAll()
        {
            lock (Running)
            {
                foreach (var p in Running) RadioTools.KillTree(p);
                Running.Clear();
            }
        }

        // ================================================================ keys

        private static readonly Regex IdRx = new("^[A-Za-z0-9_-]{6,20}$");

        /// <summary>The YouTube video id of a link (null when it has none).</summary>
        public static string VideoId(string url)
        {
            try
            {
                var u = new Uri(url.Trim());
                string host = u.Host.ToLowerInvariant(), id = null;
                if (host == "youtu.be") id = u.AbsolutePath.Trim('/').Split('/')[0];
                else
                {
                    foreach (var part in u.Query.TrimStart('?').Split('&'))
                        if (part.StartsWith("v=")) id = Uri.UnescapeDataString(part.Substring(2));
                    if (id == null)
                    {
                        var seg = u.AbsolutePath.Trim('/').Split('/');
                        if (seg.Length >= 2 && (seg[0] == "shorts" || seg[0] == "live" || seg[0] == "embed")) id = seg[1];
                    }
                }
                return id != null && IdRx.IsMatch(id) ? id : null;
            }
            catch { return null; }
        }

        /// <summary>
        /// File name of a link: the video id plus a mark of its capital letters (ids differ by case, Windows file names
        /// do not), else a hash of the link.
        /// </summary>
        public static string KeyOf(string url)
        {
            lock (Keys)
            {
                if (Keys.TryGetValue(url, out var k)) return k;
                string id = VideoId(url);
                if (id != null)
                {
                    int caps = 0;
                    for (int i = 0; i < id.Length && i < 31; i++) if (char.IsUpper(id[i])) caps |= 1 << i;
                    k = $"{id}_{caps:x}";
                }
                else
                {
                    using var sha = System.Security.Cryptography.SHA1.Create();
                    var h = sha.ComputeHash(Encoding.UTF8.GetBytes(url.Trim()));
                    k = "u" + BitConverter.ToString(h, 0, 8).Replace("-", "").ToLowerInvariant();
                }
                Keys[url] = k;
                return k;
            }
        }
    }
}
