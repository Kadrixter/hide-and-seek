using System;

namespace HideAndSeek.Game
{
    /// <summary>Lobby TV downloads: yt-dlp's errors as short Polish messages for the screen.</summary>
    public static partial class TvVideoCache
    {
        /// <summary>yt-dlp's error line -> a short Polish message for the screen.</summary>
        private static string Explain(string err)
        {
            string e = (err ?? "").ToLowerInvariant();
            if (e.Contains("private video")) return "Ten film jest prywatny.";
            if (e.Contains("does not pass filter") || e.Contains("live event") || e.Contains("is live")) return "Transmisje na żywo nie są obsługiwane.";
            if (e.Contains("confirm your age") || e.Contains("age-restricted") || e.Contains("inappropriate")) return "Film z ograniczeniem wiekowym - nie da się go odtworzyć.";
            if (e.Contains("not a bot") || e.Contains("http error 429")) return "YouTube zablokował pobieranie - spróbuj za chwilę.";
            if (e.Contains("requested format is not available")) return "Brak wersji filmu, którą da się odtworzyć.";
            if (e.Contains("unavailable") || e.Contains("not available") || e.Contains("removed") || e.Contains("incomplete youtube id")) return "Film jest niedostępny.";
            if (e.Contains("unable to download") || e.Contains("getaddrinfo") || e.Contains("timed out")) return "Brak połączenia z YouTube.";
            return "Nie udało się wczytać filmu.";
        }

        private sealed class TvError : Exception
        {
            public readonly string Detail;
            public TvError(string message, string detail = null) : base(message) { Detail = detail; }
        }
    }
}
