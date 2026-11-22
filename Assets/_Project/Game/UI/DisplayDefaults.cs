using System;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game.UI
{
    /// <summary>
    /// Window size at startup and the "Rozdzielczość" setting.
    /// Default (no choice saved in Documents\HideAndSeek\ustawienia.txt): the monitor's native resolution, borderless
    /// full screen (FullScreenWindow). A saved choice (hns.display / hns.res) wins. Applied on every start, because Unity
    /// restores the last window size from the registry (HKCU\Software\Kadrixter\Hide and Seek, "Screenmanager ...") -
    /// e.g. a 960x540 test window would otherwise stick.
    /// Unity's own switches (-screen-width / -screen-height / -screen-fullscreen / -window-mode / -popupwindow) always
    /// win: when any is given nothing is changed (tests rely on that).
    /// </summary>
    public static class DisplayDefaults
    {
        private const string ResKey = "hns.res";
        private static readonly string[] UnitySwitches =
            { "-screen-width", "-screen-height", "-screen-fullscreen", "-window-mode", "-popupwindow", "-batchmode", "-nographics" };

        /// <summary>True when the window size comes from the command line (then settings never touch it at startup).</summary>
        public static bool FromCommandLine
        {
            get
            {
                var args = Environment.GetCommandLineArgs();
                foreach (var a in args)
                foreach (var s in UnitySwitches)
                    if (string.Equals(a, s, StringComparison.OrdinalIgnoreCase)) return true;
                return false;
            }
        }

        /// <summary>Saved resolution choice; (0, 0) = native.</summary>
        public static Vector2Int Chosen
        {
            get
            {
                string v = Prefs.GetString(ResKey, "");
                int x = v.IndexOf('x');
                if (x > 0 && int.TryParse(v.Substring(0, x), out int w) && int.TryParse(v.Substring(x + 1), out int h) && w >= 640 && h >= 360)
                    return new Vector2Int(w, h);
                return Vector2Int.zero;
            }
            set
            {
                Prefs.SetString(ResKey, value.x > 0 && value.y > 0 ? $"{value.x}x{value.y}" : "");
                Apply();
            }
        }

        /// <summary>Native size of the monitor the window is on (its desktop resolution).</summary>
        public static Vector2Int Native
        {
            get
            {
                // Desktop size, not the biggest of Screen.resolutions: GPU "super resolution" (DSR / VSR) lists virtual
                // modes above the panel's native one, and a borderless window always runs at the desktop size anyway.
                var di = Screen.mainWindowDisplayInfo;
                if (di.width > 0 && di.height > 0) return new Vector2Int(di.width, di.height);
                var r = Screen.currentResolution;
                return new Vector2Int(r.width, r.height);
            }
        }

        /// <summary>Resolutions to offer in the settings: native first, then the monitor's modes (no duplicates, >= 1280x720).</summary>
        public static Vector2Int[] Options()
        {
            var list = new System.Collections.Generic.List<Vector2Int> { Vector2Int.zero };
            var native = Native;
            foreach (var r in Screen.resolutions)
            {
                var v = new Vector2Int(r.width, r.height);
                if (v.x < 1280 || v.y < 720 || v.x > native.x || v.y > native.y || list.Contains(v)) continue;
                list.Add(v);
            }
            list.Sort((a, b) => a == Vector2Int.zero ? -1 : b == Vector2Int.zero ? 1 : (b.x * b.y).CompareTo(a.x * a.y));
            return list.ToArray();
        }

        public static string Label(Vector2Int r)
        {
            var n = Native;
            return r == Vector2Int.zero ? $"natywna ({n.x}x{n.y})" : $"{r.x}x{r.y}";
        }

        /// <summary>Applies the saved mode + resolution (or the defaults: native, borderless).</summary>
        public static void Apply()
        {
            var mode = LocalProfile.DisplayMode;
            var native = Native;
            var chosen = Chosen;
            int w, h;
            if (chosen != Vector2Int.zero)
            {
                w = Mathf.Min(chosen.x, native.x);
                h = Mathf.Min(chosen.y, native.y);
            }
            else if (mode == FullScreenMode.Windowed)
            {
                w = Mathf.Max(800, Mathf.Min(1600, native.x - 160));
                h = Mathf.Max(450, Mathf.Min(900, native.y - 160));
            }
            else
            {
                w = native.x;
                h = native.y;
            }
            Screen.SetResolution(w, h, mode);
            Debug.Log($"[Display] apply {w}x{h} {mode} (native {native.x}x{native.y}, saved choice: " +
                      $"{(Prefs.HasKey("hns.display") ? "mode" : "-")}/{(chosen != Vector2Int.zero ? $"{chosen.x}x{chosen.y}" : "-")})");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Startup()
        {
            if (Application.isEditor) return;
            if (FromCommandLine)
            {
                Debug.Log($"[Display] size from the command line, left alone: {Screen.width}x{Screen.height} {Screen.fullScreenMode}");
            }
            else Apply();
            var go = new GameObject("[DisplayLog]");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<DisplayLog>();
        }
    }

    /// <summary>Writes the real window size to the log once it has settled (tests read it).</summary>
    public class DisplayLog : MonoBehaviour
    {
        private float _at;

        private void Start() => _at = Time.unscaledTime + 3f;

        private void Update()
        {
            if (Time.unscaledTime < _at) return;
            var n = DisplayDefaults.Native;
            Debug.Log($"[Display] Screen.width/height = {Screen.width}x{Screen.height}, mode {Screen.fullScreenMode}, native {n.x}x{n.y}, " +
                      $"current {Screen.currentResolution.width}x{Screen.currentResolution.height}");
            Destroy(gameObject);
        }
    }
}
