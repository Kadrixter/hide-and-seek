using HideAndSeek.Audio;
using HideAndSeek.Player;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>Local player preferences (Prefs: Documents\HideAndSeek\ustawienia.txt).</summary>
    public static class LocalProfile
    {
        /// <summary>Per-process nick (command line), so several copies on one PC do not share it through Prefs.</summary>
        public static string SessionNick;

        public static string Nick
        {
            get => SessionNick ?? Prefs.GetString("hns.nick", "Gracz" + Random.Range(10, 99));
            set => Prefs.SetString("hns.nick", Sanitize(value));
        }

        public static int Skin
        {
            get => Mathf.Clamp(Prefs.GetInt("hns.skin", 0), 0, 4);
            set => Prefs.SetInt("hns.skin", Mathf.Clamp(value, 0, 4));
        }

        /// <summary>Minecraft nick whose skin is used (empty = built-in skin).</summary>
        /// <summary>Per-process override (command line tests).</summary>
        public static string SessionMcNick;

        public static string McNick
        {
            get => SessionMcNick ?? Prefs.GetString("hns.mcnick", "");
            set => Prefs.SetString("hns.mcnick", (value ?? "").Trim());
        }

        public static string LastIp
        {
            get => Prefs.GetString("hns.ip", "127.0.0.1");
            set => Prefs.SetString("hns.ip", value);
        }

        public static float MouseSensitivity
        {
            get => Prefs.GetFloat("hns.sens", 0.11f);
            set => Prefs.SetFloat("hns.sens", value);
        }

        /// <summary>Show the game status in Discord (rich presence).</summary>
        public static bool DiscordStatus
        {
            get => Prefs.GetInt("hns.discord", 1) != 0;
            set => Prefs.SetInt("hns.discord", value ? 1 : 0);
        }

        /// <summary>Crouch key mode: true = press to switch (default), false = hold.</summary>
        public static bool CrouchToggle
        {
            get => Prefs.GetInt("hns.crouchToggle", 1) != 0;
            set => Prefs.SetInt("hns.crouchToggle", value ? 1 : 0);
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
            get => (FullScreenMode)Prefs.GetInt("hns.display", (int)FullScreenMode.FullScreenWindow);
            set
            {
                Prefs.SetInt("hns.display", (int)value);
                ApplyDisplay();
            }
        }

        /// <summary>Mode + resolution from the settings (default: native, borderless) - see <see cref="UI.DisplayDefaults"/>.</summary>
        public static void ApplyDisplay() => UI.DisplayDefaults.Apply();

        // ---------------------------------------------------------------- audio

        private static float Get(string key, float def) => Prefs.GetFloat(key, def);

        public static float MasterVolume { get => Get("hns.master", 1f); set { Prefs.SetFloat("hns.master", value); ApplyAudio(); } }
        public static float SfxVolume { get => Get("hns.sfx", 1f); set { Prefs.SetFloat("hns.sfx", value); ApplyAudio(); } }
        public static float MusicVolume { get => Get("hns.music", 0.7f); set { Prefs.SetFloat("hns.music", value); ApplyAudio(); } }
        public static float FootstepVolume { get => Get("hns.steps", 1f); set { Prefs.SetFloat("hns.steps", value); ApplyAudio(); } }
        public static float WeaponVolume { get => Get("hns.weapons", 1f); set { Prefs.SetFloat("hns.weapons", value); ApplyAudio(); } }
        public static float VoiceVolume { get => Get("hns.voice", 1f); set { Prefs.SetFloat("hns.voice", value); ApplyAudio(); } }
        public static float AmbientVolume { get => Get("hns.ambient", 1f); set { Prefs.SetFloat("hns.ambient", value); ApplyAudio(); } }
        public static float UiVolume { get => Get("hns.ui", 0.8f); set { Prefs.SetFloat("hns.ui", value); ApplyAudio(); } }

        // ---------------------------------------------------------------- voice chat

        public static bool VoiceEnabled
        {
            get => Prefs.GetInt("hns.voice.on", 1) == 1;
            set => Prefs.SetInt("hns.voice.on", value ? 1 : 0);
        }

        /// <summary>false = push-to-talk (V), true = open mic with voice activation.</summary>
        public static bool VoiceOpenMic
        {
            get => Prefs.GetInt("hns.voice.open", 0) == 1;
            set => Prefs.SetInt("hns.voice.open", value ? 1 : 0);
        }

        public static string MicDevice
        {
            get => Prefs.GetString("hns.voice.device", "");
            set => Prefs.SetString("hns.voice.device", value ?? "");
        }

        public static float ChatVolume { get => Get("hns.voice.vol2", 1f); set => Prefs.SetFloat("hns.voice.vol2", value); }
        public static float MicGain { get => Get("hns.voice.gain", 1.5f); set => Prefs.SetFloat("hns.voice.gain", value); }
        /// <summary>Voice-activation level 0..1 (open mic).</summary>
        public static float MicThreshold { get => Get("hns.voice.thr2", 0.35f); set => Prefs.SetFloat("hns.voice.thr2", value); }

        public static bool Occlusion
        {
            get => Prefs.GetInt("hns.occlusion", 1) == 1;
            set { Prefs.SetInt("hns.occlusion", value ? 1 : 0); ApplyAudio(); }
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
