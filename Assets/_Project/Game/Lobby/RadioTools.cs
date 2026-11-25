using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace HideAndSeek.Game
{
    /// <summary>Finds yt-dlp.exe / ffmpeg.exe: Tools next to the game (or the project), then PATH.</summary>
    public static class RadioTools
    {
        private static string _ytdlp, _ffmpeg;
        private static bool _searched;

        public static string YtDlp { get { Search(); return _ytdlp; } }
        public static string Ffmpeg { get { Search(); return _ffmpeg; } }

        private static readonly object SearchLock = new();

        /// <summary>On the main thread at the start (Application.dataPath cannot be read from a worker thread).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            if (!Application.isBatchMode) Search();
        }

        private static void Search()
        {
            lock (SearchLock) SearchLocked();
        }

        private static void SearchLocked()
        {
            if (_searched) return;
            var dirs = new List<string>
            {
                Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Tools"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HideAndSeek", "Tools"),
            };
            foreach (var d in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator)) if (!string.IsNullOrWhiteSpace(d)) dirs.Add(d.Trim());
            foreach (var d in dirs)
            {
                try
                {
                    if (_ytdlp == null && File.Exists(Path.Combine(d, "yt-dlp.exe"))) _ytdlp = Path.Combine(d, "yt-dlp.exe");
                    if (_ffmpeg == null && File.Exists(Path.Combine(d, "ffmpeg.exe"))) _ffmpeg = Path.Combine(d, "ffmpeg.exe");
                }
                catch { /* bad PATH entry */ }
            }
            _searched = true;
            Debug.Log($"[Radio] tools: yt-dlp={_ytdlp ?? "-"} ffmpeg={_ffmpeg ?? "-"}");
        }

        /// <summary>
        /// Feeds every stdout line of <paramref name="p"/> to <paramref name="onLine"/> until the process exits (at most
        /// <paramref name="timeoutMs"/>). Waits for the exit, not for the end of the pipe: a process started meanwhile by
        /// another thread of the game can inherit the pipe's write end and keep it open long after this one is done
        /// (that held a finished download "at 100 %" until the boombox's decoder closed).
        /// </summary>
        public static void ReadLinesUntilExit(Process p, Action<string> onLine, int timeoutMs)
        {
            var done = new System.Threading.ManualResetEventSlim(false);
            p.OutputDataReceived += (_, e) =>
            {
                if (e.Data == null) { done.Set(); return; }
                onLine(e.Data);
            };
            p.BeginOutputReadLine();
            var sw = Stopwatch.StartNew();
            while (!p.WaitForExit(100) && sw.ElapsedMilliseconds < timeoutMs) { }
            done.Wait(1500); // the last lines written before the exit
            try { p.CancelOutputRead(); } catch { /* already finished */ }
        }

        /// <summary>Process start info without a shell; every argument is passed as data (no injection).</summary>
        public static ProcessStartInfo Psi(string exe, params string[] args)
        {
            var psi = new ProcessStartInfo(exe)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = false,
            };
            var sb = new System.Text.StringBuilder();
            foreach (var a in args)
            {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(Quote(a));
            }
            psi.Arguments = sb.ToString();
            return psi;
        }

        /// <summary>Windows command-line quoting (CommandLineToArgvW rules): every argument stays one argument.</summary>
        public static string Quote(string a)
        {
            if (a.Length > 0 && a.IndexOfAny(new[] { ' ', '\t', '"' }) < 0) return a;
            var sb = new System.Text.StringBuilder("\"");
            int bs = 0;
            foreach (char c in a)
            {
                if (c == '\\') { bs++; continue; }
                if (c == '"') { sb.Append('\\', bs * 2 + 1); sb.Append('"'); bs = 0; continue; }
                sb.Append('\\', bs);
                bs = 0;
                sb.Append(c);
            }
            sb.Append('\\', bs * 2);
            sb.Append('"');
            return sb.ToString();
        }

        /// <summary>Kills a process with its children (yt-dlp.exe unpacks itself into a child process).</summary>
        public static void KillTree(Process p)
        {
            if (p == null) return;
            try
            {
                if (p.HasExited) return;
                var k = new ProcessStartInfo("taskkill", "/T /F /PID " + p.Id) { UseShellExecute = false, CreateNoWindow = true };
                Process.Start(k)?.WaitForExit(2000);
            }
            catch { /* already gone */ }
            try { if (!p.HasExited) p.Kill(); } catch { /* gone */ }
        }
    }
}
