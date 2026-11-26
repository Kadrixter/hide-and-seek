using HideAndSeek.Game.MiniGames;
using HideAndSeek.Game.MiniGames.Pool;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Places at the lobby pool table (server): two teams of one or two players (places 0 = A1, 1 = B1, 2 = A2,
    /// 3 = B2, see <see cref="PoolState"/>). A player takes a free place from the join menu (and may switch to
    /// another free one between games), presses "Gotowy"; a game starts when every seated player is ready (with one
    /// team empty it is a solo game: the turn stays with the seated team). During a game a free place can still be taken (the newcomer joins his team's rotation).
    /// Leaving: the team plays on with its other player; a team left with nobody loses the game (EndLeft).
    /// </summary>
    public partial class MatchManager
    {
        /// <summary>The loser of the last game breaks the next one (-1 = nobody yet: random).</summary>
        private int _poolNextBreaker = -1;
        /// <summary>
        /// Bit t = team t's next break is played by its second place (flips after every break of that team), so in a
        /// team of two the players take turns breaking. Same for the first inning of the team that does not break.
        /// </summary>
        private byte _poolBreakUp, _poolOpenUp;

        /// <summary>ServerMiniSit, pool branch: takes a free place (false = taken / not allowed).</summary>
        private bool PoolSit(NetPlayer pl, byte place)
        {
            if (place >= PoolState.Places || _pool.Place(place) != PoolState.Nobody) return false;
            _pool.SetPlace(place, pl.NetworkObjectId);
            _pool.Ready = (byte)(_pool.Ready & ~(1 << place));
            if (_pool.Phase == PoolState.Playing)
            {
                _pool.Note = $"{pl.Nick} dołącza do drużyny {TeamName(PoolState.TeamOf(place))}";
                PublishPool();
            }
            else TryStartPool();
            return true;
        }

        /// <summary>A seated player moves to another free place (only between games).</summary>
        private void PoolMove(NetPlayer pl, byte place)
        {
            int from = _pool.PlaceOf(pl.NetworkObjectId);
            if (from < 0 || place >= PoolState.Places || place == from || _pool.Place(place) != PoolState.Nobody) return;
            if (_pool.Phase == PoolState.Playing) return;
            _pool.SetPlace(from, PoolState.Nobody);
            _pool.SetPlace(place, pl.NetworkObjectId);
            _pool.Ready = (byte)(_pool.Ready & ~((1 << from) | (1 << place)));
            var table = MiniGameTables.Pool;
            if (table != null) pl.TeleportRpc(table.SeatStand(place), table.SeatYaw(place));
            PublishPool();
            Debug.Log($"[Pool] {pl.Nick} moves from place {from} to {place}");
        }

        /// <summary>"Gotowy" (on / off) of a seated player; R after a game means the same.</summary>
        public void ServerPoolReady(NetPlayer pl, bool ready)
        {
            if (pl == null || !IsServer) return;
            int place = _pool.PlaceOf(pl.NetworkObjectId);
            if (place < 0 || _pool.Phase == PoolState.Playing) return;
            if (ready) _pool.Ready |= (byte)(1 << place);
            else _pool.Ready = (byte)(_pool.Ready & ~(1 << place));
            TryStartPool();
        }

        /// <summary>
        /// Starts a game once every seated player is ready (one team alone plays solo, see PoolSoloAfterShot); else
        /// just publishes.
        /// </summary>
        private void TryStartPool()
        {
            byte occ = _pool.Occupied;
            _pool.Ready &= occ;
            bool go = _pool.Phase != PoolState.Playing && _pool.Seated > 0 && (_pool.Ready & occ) == occ;
            if (!go)
            {
                PublishPool();
                return;
            }
            int solo = PoolSoloTeam;
            int breaker = solo >= 0 ? solo : _poolNextBreaker is 0 or 1 ? _poolNextBreaker : Random.Range(0, 2);
            StartPool(breaker);
        }

        /// <summary>A player got up / disconnected: his team plays on with the other player, an empty team loses.</summary>
        private void PoolSeatLeft(int place, bool disconnected)
        {
            if (place < 0 || place >= PoolState.Places) return;
            string nick = NickOf(_pool.Place(place));
            if (string.IsNullOrEmpty(nick)) nick = "Gracz";
            int team = PoolState.TeamOf(place);
            FinishPoolShot(false);
            _pool.SetPlace(place, PoolState.Nobody);
            _pool.Ready = (byte)(_pool.Ready & ~(1 << place));
            string why = disconnected ? " - utracono połączenie" : " odchodzi od stołu";
            if (_pool.Members(team) == 0)
            {
                if (_pool.Phase == PoolState.Playing)
                {
                    _pool.Phase = PoolState.Over;
                    _pool.EndReason = PoolState.EndLeft;
                    _pool.Winner = (byte)(1 - team);
                    _pool.Note = nick + why;
                    _pool.Ready = 0;
                }
                // A new line-up: the series starts again.
                _pool.WinsA = _pool.WinsB = 0;
                _poolNextBreaker = -1;
            }
            else if (_pool.Phase == PoolState.Playing) _pool.Note = $"{nick}{why} - drużyna {TeamName(team)} gra dalej";
            if (_pool.Seated == 0) ResetPool(true);
            else if (_pool.Phase != PoolState.Playing) TryStartPool();
            else PublishPool();
        }

        private static string TeamName(int team) => team == 0 ? "A" : "B";
    }
}
