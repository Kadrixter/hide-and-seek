using System;
using System.Globalization;
using System.Text.RegularExpressions;
using HideAndSeek.Game.Net;

namespace HideAndSeek.Game
{
    /// <summary>
    /// A pasted YouTube link for the lobby TV: any form (watch?v= with &amp;list= / &amp;pp= / &amp;t=, youtu.be, Shorts,
    /// music.youtube.com, embed, live) becomes the short canonical "https://www.youtube.com/watch?v=ID" (the same cache
    /// key on every copy, never over the 120 characters a link may have), plus the start time of a t= / start= parameter.
    /// </summary>
    public static class TvLink
    {
        private static readonly Regex TimeRx = new(@"^(?:(\d+)h)?(?:(\d+)m)?(?:(\d+(?:\.\d+)?)s?)?$");

        /// <summary>False when it is not a YouTube video link.</summary>
        public static bool TryNormalize(string pasted, out string url, out float start)
        {
            url = null;
            start = 0f;
            if (string.IsNullOrWhiteSpace(pasted)) return false;
            string s = pasted.Trim();
            if (!s.StartsWith("http", StringComparison.OrdinalIgnoreCase)) s = "https://" + s;
            if (!Uri.TryCreate(s, UriKind.Absolute, out var u)) return false;
            string h = u.Host.ToLowerInvariant();
            if (h != "youtu.be" && h != "youtube.com" && !h.EndsWith(".youtube.com")) return false;
            string id = RadioCache.VideoId(s);
            if (id == null) return false;
            url = "https://www.youtube.com/watch?v=" + id;
            foreach (var part in u.Query.TrimStart('?').Split('&'))
            {
                int eq = part.IndexOf('=');
                if (eq <= 0) continue;
                string k = part.Substring(0, eq), v = Uri.UnescapeDataString(part.Substring(eq + 1));
                if (k == "t" || k == "start") start = Seconds(v);
            }
            if (u.Fragment.StartsWith("#t=")) start = Seconds(u.Fragment.Substring(3));
            return MatchManager.IsYoutubeUrl(url);
        }

        /// <summary>"90", "90s", "1m30s", "1h2m3s" -> seconds (0 when it cannot be read).</summary>
        private static float Seconds(string v)
        {
            var m = TimeRx.Match((v ?? "").Trim().ToLowerInvariant());
            if (!m.Success) return 0f;
            float t = 0f;
            if (m.Groups[1].Success) t += 3600f * int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            if (m.Groups[2].Success) t += 60f * int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            if (m.Groups[3].Success) t += float.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
            return Math.Max(0f, Math.Min(t, 6f * 3600f));
        }
    }
}
