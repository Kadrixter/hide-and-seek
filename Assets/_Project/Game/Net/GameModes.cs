namespace HideAndSeek.Game.Net
{
    /// <summary>Game modes picked in the round settings (lobby board, host panel). Only ready ones can be started.</summary>
    public enum GameMode : byte
    {
        HideAndSeek = 0,
        RcRace = 1,
    }

    public static class GameModes
    {
        public struct Info
        {
            public GameMode Id;
            public string Name;
            public string Tagline;
            public bool Ready;
        }

        /// <summary>
        /// Every mode, in the order the host cycles through them. A new mode: add it to <see cref="GameMode"/> and here
        /// (Ready = false shows it as "wkrótce" and blocks the start lever), then branch on
        /// <c>MatchManager.Settings.Value.Mode</c> where the round is set up.
        /// </summary>
        public static readonly Info[] All =
        {
            new Info { Id = GameMode.HideAndSeek, Name = "Chowany", Tagline = "Szukający (jeden lub kilku) polują, reszta się chowa na parkingu.", Ready = true },
            new Info { Id = GameMode.RcRace, Name = "Wyścigi RC", Tagline = "Wyścig samochodzików RC na torze z rampami i tunelami (wersja testowa).", Ready = true },
        };

        public static Info Get(byte mode) => mode < All.Length ? All[mode] : All[0];
    }
}
