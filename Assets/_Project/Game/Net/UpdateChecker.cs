using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Asks GitHub Releases (Kadrixter/hide-and-seek) whether a newer build exists. The menu shows an update button;
    /// clicking it starts Updater.exe (next to the game) and closes the game, the updater replaces the files and
    /// starts the game again. Switches: -noupdate (no check), -fakeversion X (pretend to be version X, tests).
    /// </summary>
    public static class UpdateChecker
    {
        public const string Repo = "Kadrixter/hide-and-seek";
        /// <summary>Release list (newest first); -updateapi &lt;url&gt; overrides it (tests, also file:///).</summary>
        private static string Api
        {
            get
            {
                string[] a = Environment.GetCommandLineArgs();
                int i = Array.IndexOf(a, "-updateapi");
                return i >= 0 && i + 1 < a.Length ? a[i + 1] : "https://api.github.com/repos/" + Repo + "/releases?per_page=10";
            }
        }
        private const string GameAsset = "HideAndSeek-game.zip";

        public enum State { Idle, Checking, UpToDate, Available, Failed }

        public static State Status { get; private set; } = State.Idle;
        public static string Latest { get; private set; } = "";
        public static string Notes { get; private set; } = "";
        /// <summary>The release asks for the complete package (radio tools too).</summary>
        public static bool Full { get; private set; }
        public static event Action Changed;

        public static string CurrentVersion
        {
            get
            {
                string[] a = Environment.GetCommandLineArgs();
                int i = Array.IndexOf(a, "-fakeversion");
                return i >= 0 && i + 1 < a.Length ? a[i + 1] : Application.version;
            }
        }

        private static string GameDir => Path.GetDirectoryName(Application.dataPath) ?? ".";
        public static string UpdaterPath => Path.Combine(GameDir, "Updater.exe");
        public static bool CanUpdate => !Application.isEditor && File.Exists(UpdaterPath);

        [Serializable] private class Asset { public string name; }
        [Serializable] private class Release { public string tag_name; public string body; public bool draft; public bool prerelease; public Asset[] assets; }
        [Serializable] private class ReleaseList { public Release[] items; }

        /// <summary>Newest published release that carries the game package (same rule as Updater.exe).</summary>
        private static Release Pick(string json)
        {
            var list = JsonUtility.FromJson<ReleaseList>("{\"items\":" + json + "}");
            if (list?.items == null) return null;
            foreach (var r in list.items)
            {
                if (r == null || r.draft || r.prerelease || r.assets == null) continue;
                foreach (var a in r.assets) if (a != null && a.name == GameAsset) return r;
            }
            return null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            if (Application.isEditor) return;
            SwapPendingUpdater();
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-noupdate") >= 0) return;
            Check();
        }

        /// <summary>The updater cannot overwrite itself; it leaves Updater.exe.new, swapped here on the next start.</summary>
        private static void SwapPendingUpdater()
        {
            string pending = UpdaterPath + ".new";
            if (!File.Exists(pending)) return;
            try
            {
                if (File.Exists(UpdaterPath)) File.Delete(UpdaterPath);
                File.Move(pending, UpdaterPath);
            }
            catch (Exception e) { Debug.LogWarning($"[Update] updater swap failed: {e.Message}"); }
        }

        public static void Check()
        {
            if (Status == State.Checking) return;
            Set(State.Checking);
            var req = UnityWebRequest.Get(Api);
            req.SetRequestHeader("User-Agent", "HideAndSeek-Game");
            req.SetRequestHeader("Accept", "application/vnd.github+json");
            req.timeout = 15;
            req.SendWebRequest().completed += _ =>
            {
                try
                {
                    if (req.result != UnityWebRequest.Result.Success) { Debug.Log($"[Update] check failed: {req.error}"); Set(State.Failed); return; }
                    var r = Pick(req.downloadHandler.text);
                    if (r == null || string.IsNullOrEmpty(r.tag_name)) { Set(State.UpToDate); return; }
                    Latest = r.tag_name.TrimStart('v', 'V');
                    Notes = CleanNotes(r.body);
                    Full = IsFull(r.body);
                    bool newer = Compare(Latest, CurrentVersion) > 0;
                    Debug.Log($"[Update] current {CurrentVersion}, latest {Latest}, newer={newer}, full={Full}");
                    Set(newer ? State.Available : State.UpToDate);
                }
                finally { req.Dispose(); }
            };
        }

        /// <summary>Starts Updater.exe and quits the game (the updater waits for this process to end).</summary>
        public static bool RunUpdater()
        {
            if (!CanUpdate) return false;
            try
            {
                string args = $"--pid {Process.GetCurrentProcess().Id} --auto --run";
                if (Array.IndexOf(Environment.GetCommandLineArgs(), "-updateapi") >= 0) args += $" --api \"{Api}\"";
                Process.Start(new ProcessStartInfo(UpdaterPath, args)
                {
                    UseShellExecute = true,
                    WorkingDirectory = GameDir,
                });
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Update] cannot start updater: {e.Message}");
                return false;
            }
            Prefs.Save();
            Application.Quit();
            return true;
        }

        /// <summary>Release marked as complete: "[full]" / "[pelna]" in the notes.</summary>
        public static bool IsFull(string body)
        {
            if (string.IsNullOrEmpty(body)) return false;
            string b = body.ToLowerInvariant();
            return b.Contains("[full]") || b.Contains("[pelna]") || b.Contains("[pełna]");
        }

        private static string CleanNotes(string body)
        {
            if (string.IsNullOrEmpty(body)) return "";
            string s = body.Replace("\r", "").Replace("[full]", "").Replace("[pelna]", "").Replace("[pełna]", "").Trim();
            return s.Length > 600 ? s.Substring(0, 600) + "..." : s;
        }

        /// <summary>Compares dotted numeric versions (2026.11.21.1725): &gt;0 when a is newer.</summary>
        public static int Compare(string a, string b)
        {
            string[] x = (a ?? "").Split('.'), y = (b ?? "").Split('.');
            for (int i = 0; i < Math.Max(x.Length, y.Length); i++)
            {
                long.TryParse(i < x.Length ? x[i] : "0", out long p);
                long.TryParse(i < y.Length ? y[i] : "0", out long q);
                if (p != q) return p.CompareTo(q);
            }
            return 0;
        }

        private static void Set(State s)
        {
            Status = s;
            Changed?.Invoke();
        }
    }
}
