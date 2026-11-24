using HideAndSeek.Core;
using HideAndSeek.Game.Net;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// One place for other systems to ask about the lobby board games (chess table, tic-tac-toe board).
    /// <see cref="WantsCursor"/>: the local player sits / stands at a board and plays with the mouse, so the cursor
    /// must stay free (a cursor-locking policy should treat this like an open menu).
    /// </summary>
    public static class BoardGameView
    {
        /// <summary>True while the local player is at a board-game table in the lobby (the mouse picks pieces / cells) or works on the dot link panel.</summary>
        public static bool WantsCursor
        {
            get
            {
                if (HideAndSeek.Game.DotLink.DotLinkLocal.Active) return true; // dot link panel at the exit gate (mouse draws lines)
                var me = NetPlayer.Local;
                var mm = MatchManager.Instance;
                if (me == null || mm == null || mm.Phase.Value != MatchPhase.Lobby || me.Life.Value != LifeState.Alive) return false;
                return MiniGameTables.LocalTable(out _) != null;
            }
        }
    }
}
