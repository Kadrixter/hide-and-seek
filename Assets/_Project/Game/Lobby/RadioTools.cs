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

        private static void Search()
        {
            if (_searched) return;
            _searched = true;
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
            Debug.Log($"[Radio] tools: yt-dlp={_ytdlp ?? "-"} ffmpeg={_ffmpeg ?? "-"}");
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
