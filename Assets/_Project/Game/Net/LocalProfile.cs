using HideAndSeek.Audio;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>Local player preferences (PlayerPrefs).</summary>
    public static class LocalProfile
    {
        public static string Nick
        {
            get => PlayerPrefs.GetString("hns.nick", "Gracz" + Random.Range(10, 99));
            set => PlayerPrefs.SetString("hns.nick", Sanitize(value));
        }

        public static int Skin
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt("hns.skin", 0), 0, 4);
            set => PlayerPrefs.SetInt("hns.skin", Mathf.Clamp(value, 0, 4));
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

        public static float SfxVolume
        {
            get => PlayerPrefs.GetFloat("hns.sfx", 1f);
            set { PlayerPrefs.SetFloat("hns.sfx", value); GameAudio.SfxVolume = value; }
        }

        public static float MusicVolume
        {
            get => PlayerPrefs.GetFloat("hns.music", 0.7f);
            set { PlayerPrefs.SetFloat("hns.music", value); GameAudio.MusicVolume = value; }
        }

        public static float MasterVolume
        {
            get => PlayerPrefs.GetFloat("hns.master", 1f);
            set { PlayerPrefs.SetFloat("hns.master", value); AudioListener.volume = value; }
        }

        public static void ApplyAudio()
        {
            GameAudio.SfxVolume = SfxVolume;
            GameAudio.MusicVolume = MusicVolume;
            AudioListener.volume = MasterVolume;
        }

        public static string Sanitize(string nick)
        {
            if (string.IsNullOrWhiteSpace(nick)) return "Gracz";
            nick = nick.Trim().Replace("|", "").Replace("\n", "");
            return nick.Length > 16 ? nick.Substring(0, 16) : nick;
        }
    }
}
