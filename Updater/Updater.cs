// Hide and Seek - updater (C# 5, .NET Framework 4.8, built by Updater/build.ps1 or MultiplayerBuilder).
// Downloads the newest GitHub release and replaces the game files next to it.
//   Release assets:  HideAndSeek-game.zip  (the game without Tools)   HideAndSeek-tools.zip (ffmpeg, yt-dlp)
//   "[full]" in the release notes = the tools are downloaded too (otherwise only when missing or ticked).
// Arguments: --dir <game folder> --pid <game process to wait for> --auto (start at once) --run (start the game after)
//            --full (tools too) --api <releases url, also file:///...json for tests>
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace HideAndSeekUpdater
{
    internal class Options
    {
        public string Dir;
        public int Pid;
        public bool Auto, Run, Full;
        public string Api = "https://api.github.com/repos/Kadrixter/hide-and-seek/releases?per_page=20";

        public static Options Parse(string[] a)
        {
            var o = new Options();
            o.Dir = Path.GetDirectoryName(Application.ExecutablePath);
            for (int i = 0; i < a.Length; i++)
            {
                string k = a[i].ToLowerInvariant();
                string v = i + 1 < a.Length ? a[i + 1] : null;
                if (k == "--dir" && v != null) { o.Dir = Path.GetFullPath(v); i++; }
                else if (k == "--pid" && v != null) { int.TryParse(v, out o.Pid); i++; }
                else if (k == "--api" && v != null) { o.Api = v; i++; }
                else if (k == "--auto") o.Auto = true;
                else if (k == "--run") o.Run = true;
                else if (k == "--full") o.Full = true;
            }
            return o;
        }
    }

    internal class Asset { public string Name, Url, Sha256; public long Size; }

    internal class ReleaseInfo
    {
        public string Version = "", Notes = "";
        public bool Full;
        public Asset Game, Tools;
    }

    internal static class Program
    {
        public const string GameZip = "HideAndSeek-game.zip";
        public const string ToolsZip = "HideAndSeek-tools.zip";
        public const string GameExe = "HideAndSeek.exe";

        [STAThread]
        private static int Main(string[] args)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new UpdaterForm(Options.Parse(args)));
            return 0;
        }

        public static string LogPath
        {
            get
            {
                string d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "HideAndSeek");
                Directory.CreateDirectory(d);
                return Path.Combine(d, "updater.log");
            }
        }

        public static void Log(string s)
        {
            try { File.AppendAllText(LogPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + s + Environment.NewLine, Encoding.UTF8); }
            catch { }
        }

        public static string LocalVersion(string dir)
        {
            try
            {
                string f = Path.Combine(dir, "version.txt");
                return File.Exists(f) ? File.ReadAllText(f).Trim() : "";
            }
            catch { return ""; }
        }

        public static bool ToolsPresent(string dir)
        {
            string t = Path.Combine(dir, "Tools");
            return File.Exists(Path.Combine(t, "ffmpeg.exe")) && File.Exists(Path.Combine(t, "yt-dlp.exe"));
        }

        /// <summary>Dotted numeric compare (2026.11.21.1725): &gt;0 when a is newer.</summary>
        public static int Compare(string a, string b)
        {
            string[] x = (a ?? "").Split('.'), y = (b ?? "").Split('.');
            for (int i = 0; i < Math.Max(x.Length, y.Length); i++)
            {
                long p, q;
                long.TryParse(i < x.Length ? x[i] : "0", out p);
                long.TryParse(i < y.Length ? y[i] : "0", out q);
                if (p != q) return p.CompareTo(q);
            }
            return 0;
        }

        public static bool IsFull(string body)
        {
            string b = (body ?? "").ToLowerInvariant();
            return b.Contains("[full]") || b.Contains("[pelna]") || b.Contains("[pełna]");
        }

        // ------------------------------------------------------------ release lookup

        public static ReleaseInfo FetchRelease(string api)
        {
            string json = DownloadString(api);
            var list = new JavaScriptSerializer().DeserializeObject(json);
            var releases = list as object[];
            if (releases == null) releases = new object[] { list };
            ReleaseInfo info = null;
            foreach (var o in releases)
            {
                var r = o as Dictionary<string, object>;
                if (r == null || True(r, "draft") || True(r, "prerelease")) continue;
                Asset game = FindAsset(r, GameZip), tools = FindAsset(r, ToolsZip);
                if (info == null)
                {
                    if (game == null) continue; // a release without the game package (e.g. tools only)
                    info = new ReleaseInfo();
                    info.Version = Str(r, "tag_name").TrimStart('v', 'V');
                    info.Notes = Str(r, "body").Replace("\r", "");
                    info.Full = IsFull(info.Notes);
                    info.Game = game;
                }
                if (info.Tools == null && tools != null) info.Tools = tools; // newest tools package
                if (info.Tools != null) break;
            }
            if (info == null) throw new Exception("Brak wydania z plikiem " + GameZip + ".");
            return info;
        }

        private static bool True(Dictionary<string, object> d, string k)
        {
            object v;
            return d.TryGetValue(k, out v) && v is bool && (bool)v;
        }

        private static string Str(Dictionary<string, object> d, string k)
        {
            object v;
            return d.TryGetValue(k, out v) && v != null ? v.ToString() : "";
        }

        private static Asset FindAsset(Dictionary<string, object> r, string name)
        {
            object v;
            if (!r.TryGetValue("assets", out v) || !(v is IEnumerable)) return null;
            foreach (var o in (IEnumerable)v)
            {
                var a = o as Dictionary<string, object>;
                if (a == null || !string.Equals(Str(a, "name"), name, StringComparison.OrdinalIgnoreCase)) continue;
                var asset = new Asset { Name = name, Url = Str(a, "browser_download_url") };
                long.TryParse(Str(a, "size"), out asset.Size);
                string digest = Str(a, "digest");
                if (digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)) asset.Sha256 = digest.Substring(7).ToLowerInvariant();
                return asset;
            }
            return null;
        }

        private static WebRequest Request(string url)
        {
            var req = WebRequest.Create(url);
            var http = req as HttpWebRequest;
            if (http != null)
            {
                http.UserAgent = "HideAndSeek-Updater";
                http.Accept = "application/vnd.github+json, application/octet-stream";
                http.Timeout = 30000;
                http.ReadWriteTimeout = 60000;
                http.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            }
            return req;
        }

        private static string DownloadString(string url)
        {
            using (var resp = Request(url).GetResponse())
            using (var s = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                return s.ReadToEnd();
        }

        /// <summary>Downloads to a file, reports (done, total) bytes; checks size and SHA256 when known.</summary>
        public static void DownloadFile(Asset a, string path, Action<long, long> progress)
        {
            using (var resp = Request(a.Url).GetResponse())
            using (var src = resp.GetResponseStream())
            using (var dst = File.Create(path))
            using (var sha = SHA256.Create())
            {
                long total = a.Size > 0 ? a.Size : resp.ContentLength, done = 0;
                var buf = new byte[1 << 16];
                int n;
                while ((n = src.Read(buf, 0, buf.Length)) > 0)
                {
                    dst.Write(buf, 0, n);
                    sha.TransformBlock(buf, 0, n, null, 0);
                    done += n;
                    progress(done, total);
                }
                sha.TransformFinalBlock(buf, 0, 0);
                if (a.Size > 0 && done != a.Size) throw new Exception(a.Name + ": pobrano " + done + " z " + a.Size + " bajtów.");
                if (!string.IsNullOrEmpty(a.Sha256))
                {
                    string got = BitConverter.ToString(sha.Hash).Replace("-", "").ToLowerInvariant();
                    if (got != a.Sha256) throw new Exception(a.Name + ": zła suma SHA256 (plik uszkodzony).");
                }
            }
        }
    }

    /// <summary>Moves files with retries (the game or its crash handler may still hold a lock for a moment).</summary>
    internal static class Fs
    {
        public static bool Exists(string p) { return File.Exists(p) || Directory.Exists(p); }

        public static bool SameBytes(string a, string b)
        {
            try
            {
                if (!File.Exists(a) || !File.Exists(b) || new FileInfo(a).Length != new FileInfo(b).Length) return false;
                byte[] x = File.ReadAllBytes(a), y = File.ReadAllBytes(b);
                for (int i = 0; i < x.Length; i++) if (x[i] != y[i]) return false;
                return true;
            }
            catch { return false; }
        }

        public static void Move(string from, string to)
        {
            for (int i = 0; ; i++)
            {
                try
                {
                    if (Directory.Exists(from)) Directory.Move(from, to);
                    else File.Move(from, to);
                    return;
                }
                catch (IOException) { if (i >= 25) throw; }
                catch (UnauthorizedAccessException) { if (i >= 25) throw; }
                Thread.Sleep(400);
            }
        }

        public static void Delete(string p)
        {
            try
            {
                if (Directory.Exists(p)) Directory.Delete(p, true);
                else if (File.Exists(p)) File.Delete(p);
            }
            catch (Exception e) { Program.Log("delete failed " + p + ": " + e.Message); }
        }

        /// <summary>The folder inside an extracted zip that holds <paramref name="marker"/> (zip root or one level down).</summary>
        public static string RootWith(string dir, string marker)
        {
            if (File.Exists(Path.Combine(dir, marker))) return dir;
            foreach (var d in Directory.GetDirectories(dir))
                if (File.Exists(Path.Combine(d, marker))) return d;
            return null;
        }
    }
}
