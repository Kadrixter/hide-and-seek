using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game.Voice
{
    /// <summary>
    /// Per-player voice volume (0..2, 1 = normal), set in the Esc menu ("Głośność graczy"). Remembered locally by nick
    /// in Documents\HideAndSeek\ustawienia.txt (keys hns.pvol.&lt;nick&gt;).
    /// </summary>
    public static class PlayerVoiceVolumes
    {
        public const float Max = 2f;
        private const string Prefix = "hns.pvol.";

        private static string Key(string nick)
        {
            nick = (nick ?? "").Trim().ToLowerInvariant();
            // keep the settings file readable: key=value per line, so no '=' / spaces / line breaks in the key
            var sb = new System.Text.StringBuilder(Prefix);
            foreach (char c in nick)
            {
                if (char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.') sb.Append(c);
                else sb.Append('%').Append(((int)c).ToString("X2"));
            }
            return sb.ToString();
        }

        public static float Get(string nick)
        {
            if (string.IsNullOrEmpty(nick)) return 1f;
            return Mathf.Clamp(Prefs.GetFloat(Key(nick), 1f), 0f, Max);
        }

        public static void Set(string nick, float volume)
        {
            if (string.IsNullOrEmpty(nick)) return;
            volume = Mathf.Clamp(volume, 0f, Max);
            if (Mathf.Abs(volume - 1f) < 0.005f) volume = 1f;
            Prefs.SetFloat(Key(nick), volume);
        }
    }
}
