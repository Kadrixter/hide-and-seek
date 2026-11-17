using HideAndSeek.Audio;
using HideAndSeek.Player;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>Local player preferences (PlayerPrefs).</summary>
    public static class LocalProfile
    {
        /// <summary>Per-process nick (command line), so several copies on one PC do not share it through PlayerPrefs.</summary>
        public static string SessionNick;

        public static string Nick
        {
            get => SessionNick ?? PlayerPrefs.GetString("hns.nick", "Gracz" + Random.Range(10, 99));
            set => PlayerPrefs.SetString("hns.nick", Sanitize(value));
        }

        public static int Skin
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt("hns.skin", 0), 0, 4);
            set => PlayerPrefs.SetInt("hns.skin", Mathf.Clamp(value, 0, 4));
        }

        /// <summary>Minecraft nick whose skin is used (empty = built-in skin).</summary>
        /// <summary>Per-process override (command line tests).</summary>
        public static string SessionMcNick;

        public static string McNick
        {
            get => SessionMcNick ?? PlayerPrefs.GetString("hns.mcnick", "");
            set => PlayerPrefs.SetString("hns.mcnick", (value ?? "").Trim());
        }

        public static string LastIp
        {
            get => PlayerPrefs.GetString("hns.ip", "127.0.0.1");
            set => PlayerPrefs.SetString("hns.ip", value);
        }

        public static float MouseSensitivity
        {
            get => PlayerPrefs.GetFloat("hns.sens", 0.11f);
            set => PlayerPrefs.SetFloat("hns.sens", value);
        }

        // ---------------------------------------------------------------- display

        public static readonly FullScreenMode[] DisplayModes =
            { FullScreenMode.FullScreenWindow, FullScreenMode.ExclusiveFullScreen, FullScreenMode.Windowed };

        public static string DisplayModeName(FullScreenMode m) => m switch
        {
            FullScreenMode.FullScreenWindow => "bez ramek",
            FullScreenMode.ExclusiveFullScreen => "pełny ekran",
            _ => "okno",
        };

        public static FullScreenMode DisplayMode
        {
            get => (FullScreenMode)PlayerPrefs.GetInt("hns.display", (int)FullScreenMode.FullScreenWindow);
            set
            {
                PlayerPrefs.SetInt("hns.display", (int)value);
                ApplyDisplay();
            }
        }

        public static void ApplyDisplay()
        {
            var mode = DisplayMode;
            if (mode == FullScreenMode.Windowed)
            {
                var r = Screen.currentResolution;
                int w = Mathf.Min(1600, r.width - 160), h = Mathf.Min(900, r.height - 160);
                Screen.SetResolution(Mathf.Max(800, w), Mathf.Max(450, h), FullScreenMode.Windowed);
            }
            else
            {
                var r = Screen.currentResolution;
                Screen.SetResolution(r.width, r.height, mode);
            }
        }

        // ---------------------------------------------------------------- audio

        private static float Get(string key, float def) => PlayerPrefs.GetFloat(key, def);

        public static float MasterVolume { get => Get("hns.master", 1f); set { PlayerPrefs.SetFloat("hns.master", value); ApplyAudio(); } }
        public static float SfxVolume { get => Get("hns.sfx", 1f); set { PlayerPrefs.SetFloat("hns.sfx", value); ApplyAudio(); } }
        public static float MusicVolume { get => Get("hns.music", 0.7f); set { PlayerPrefs.SetFloat("hns.music", value); ApplyAudio(); } }
        public static float FootstepVolume { get => Get("hns.steps", 1f); set { PlayerPrefs.SetFloat("hns.steps", value); ApplyAudio(); } }
        public static float WeaponVolume { get => Get("hns.weapons", 1f); set { PlayerPrefs.SetFloat("hns.weapons", value); ApplyAudio(); } }
        public static float VoiceVolume { get => Get("hns.voice", 1f); set { PlayerPrefs.SetFloat("hns.voice", value); ApplyAudio(); } }
        public static float AmbientVolume { get => Get("hns.ambient", 1f); set { PlayerPrefs.SetFloat("hns.ambient", value); ApplyAudio(); } }
        public static float UiVolume { get => Get("hns.ui", 0.8f); set { PlayerPrefs.SetFloat("hns.ui", value); ApplyAudio(); } }

        // ---------------------------------------------------------------- voice chat

        public static bool VoiceEnabled
        {
            get => PlayerPrefs.GetInt("hns.voice.on", 1) == 1;
            set => PlayerPrefs.SetInt("hns.voice.on", value ? 1 : 0);
        }

        /// <summary>false = push-to-talk (V), true = open mic with voice activation.</summary>
        public static bool VoiceOpenMic
        {
            get => PlayerPrefs.GetInt("hns.voice.open", 0) == 1;
            set => PlayerPrefs.SetInt("hns.voice.open", value ? 1 : 0);
        }

        public static string MicDevice
        {
            get => PlayerPrefs.GetString("hns.voice.device", "");
            set => PlayerPrefs.SetString("hns.voice.device", value ?? "");
        }

        public static float ChatVolume { get => Get("hns.voice.vol2", 1f); set => PlayerPrefs.SetFloat("hns.voice.vol2", value); }
        public static float MicGain { get => Get("hns.voice.gain", 1.5f); set => PlayerPrefs.SetFloat("hns.voice.gain", value); }
        /// <summary>Voice-activation level 0..1 (open mic).</summary>
        public static float MicThreshold { get => Get("hns.voice.thr", 0.12f); set => PlayerPrefs.SetFloat("hns.voice.thr", value); }

        public static bool Occlusion
        {
            get => PlayerPrefs.GetInt("hns.occlusion", 1) == 1;
            set { PlayerPrefs.SetInt("hns.occlusion", value ? 1 : 0); ApplyAudio(); }
        }

        public static void ApplyAudio()
        {
            AudioListener.volume = MasterVolume;
            GameAudio.SfxVolume = SfxVolume;
            GameAudio.MusicVolume = MusicVolume;
            GameAudio.UiVolume = UiVolume;
            GameAudio.VoiceVolume = VoiceVolume;
            GameAudio.WeaponVolume = WeaponVolume;
            GameAudio.AmbientVolume = AmbientVolume;
            GameAudio.OcclusionEnabled = Occlusion;
            float steps = FootstepVolume;
            foreach (var fa in Object.FindObjectsByType<FootstepAudio>(FindObjectsInactive.Include))
                fa.MasterVolume = steps;
        }

        public static string Sanitize(string nick)
        {
            if (string.IsNullOrWhiteSpace(nick)) return "Gracz";
            nick = nick.Trim().Replace("|", "").Replace("\n", "");
            return nick.Length > 16 ? nick.Substring(0, 16) : nick;
        }
    }
}
