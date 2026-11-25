using HideAndSeek.Game.MiniGames.Pool;

namespace HideAndSeek.Game.Net
{
    /// <summary>Test hook of the pool table (MiniGameProbe -poolstress only): the shooter gets the cue ball in hand anywhere.</summary>
    public partial class MatchManager
    {
        /// <summary>Server, between shots of a running game: ball in hand (anywhere) for the player to shoot next.</summary>
        public bool TestPoolBallInHand()
        {
            if (!IsServer || _pool.Phase != PoolState.Playing || _poolPending != null || _pool.Break) return false;
            _pool.BallInHand = true;
            _pool.Kitchen = false;
            _pool.Down = (ushort)(_pool.Down & ~1);
            _pool.Note = "Test: biała w ręce";
            PublishPool();
            return true;
        }
    }
}
