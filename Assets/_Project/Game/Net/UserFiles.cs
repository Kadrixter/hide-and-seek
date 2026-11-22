using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Player files live in Documents\HideAndSeek (created on the first start): settings, records, skin cache.
    /// The game folder holds only the game itself, so the updater can replace it freely.
    /// </summary>
    public static class UserFiles
    {
        private static string _root;

        /// <summary>Documents\HideAndSeek (falls back to persistentDataPath when Documents is not available).</summary>
        public static string Root
        {
            get
            {
                if (_root != null) return _root;
                string docs = null;
                try { docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments); } catch { /* no profile */ }
                _root = string.IsNullOrEmpty(docs) ? Application.persistentDataPath : Path.Combine(docs, "HideAndSeek");
                try { Directory.CreateDirectory(_root); }
                catch (Exception e)
                {
                    Debug.LogWarning($"[UserFiles] cannot create {_root}: {e.Message}");
                    _root = Application.persistentDataPath;
                }
                return _root;
            }
        }

        /// <summary>A sub folder of <see cref="Root"/> (created on demand).</summary>
        public static string Dir(string name)
        {
            string d = Path.Combine(Root, name);
            Directory.CreateDirectory(d);
            return d;
        }
    }

    /// <summary>
    /// Drop-in replacement for PlayerPrefs, stored as Documents\HideAndSeek\ustawienia.txt (key=value lines).
    /// Values missing from the file are taken once from the old PlayerPrefs (registry), so nothing is lost.
    /// Main thread only, like PlayerPrefs.
    /// </summary>
    public static class Prefs
    {
        private const string FileName = "ustawienia.txt";
        private static Dictionary<string, string> _values;
        private static bool _dirty;

        private static string FilePath => Path.Combine(UserFiles.Root, FileName);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            Load();
            Application.quitting += Save;
        }

        private static void Load()
        {
            if (_values != null) return;
            _values = new Dictionary<string, string>();
            try
            {
                if (!File.Exists(FilePath)) return;
                foreach (string line in File.ReadAllLines(FilePath, Encoding.UTF8))
                {
                    if (line.Length == 0 || line[0] == '#') continue;
                    int eq = line.IndexOf('=');
                    if (eq > 0) _values[line.Substring(0, eq).Trim()] = line.Substring(eq + 1);
                }
            }
            catch (Exception e) { Debug.LogWarning($"[Prefs] read failed: {e.Message}"); }
        }

        private static bool TryGet(string key, out string v)
        {
            Load();
            return _values.TryGetValue(key, out v);
        }

        private static void Set(string key, string v)
        {
            Load();
            if (_values.TryGetValue(key, out var old) && old == v) return;
            _values[key] = v;
            _dirty = true;
        }

        public static bool HasKey(string key) => TryGet(key, out _) || PlayerPrefs.HasKey(key);

        public static string GetString(string key, string def = "")
        {
            if (TryGet(key, out var v)) return v;
            if (!PlayerPrefs.HasKey(key)) return def;
            v = PlayerPrefs.GetString(key, def);
            Set(key, v);
            return v;
        }

        public static int GetInt(string key, int def = 0)
        {
            if (TryGet(key, out var v)) return int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i) ? i : def;
            if (!PlayerPrefs.HasKey(key)) return def;
            int r = PlayerPrefs.GetInt(key, def);
            SetInt(key, r);
            return r;
        }

        public static float GetFloat(string key, float def = 0f)
        {
            if (TryGet(key, out var v)) return float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ? f : def;
            if (!PlayerPrefs.HasKey(key)) return def;
            float r = PlayerPrefs.GetFloat(key, def);
            SetFloat(key, r);
            return r;
        }

        public static void SetString(string key, string value) => Set(key, (value ?? "").Replace("\r", "").Replace("\n", " "));
        public static void SetInt(string key, int value) => Set(key, value.ToString(CultureInfo.InvariantCulture));
        public static void SetFloat(string key, float value) => Set(key, value.ToString("R", CultureInfo.InvariantCulture));

        /// <summary>Writes the file (atomically) when something changed.</summary>
        public static void Save()
        {
            if (!_dirty || _values == null) return;
            try
            {
                var sb = new StringBuilder("# Hide and Seek - ustawienia gracza\n");
                var keys = new List<string>(_values.Keys);
                keys.Sort(StringComparer.Ordinal);
                foreach (var k in keys) sb.Append(k).Append('=').Append(_values[k]).Append('\n');
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, sb.ToString(), new UTF8Encoding(false));
                if (File.Exists(FilePath)) File.Replace(tmp, FilePath, null);
                else File.Move(tmp, FilePath);
                _dirty = false;
            }
            catch (Exception e) { Debug.LogWarning($"[Prefs] save failed: {e.Message}"); }
        }
    }
}
