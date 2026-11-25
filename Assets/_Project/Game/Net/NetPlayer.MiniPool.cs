using HideAndSeek.Game.MiniGames.Pool;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>Owner -> server requests of the lobby pool table (the "net" agent owns this file).</summary>
    public partial class NetPlayer
    {
        /// <summary>Strike the cue ball. With ball in hand the cue ball is put at (cueX, cueZ) table space first.</summary>
        [Rpc(SendTo.Server)]
        public void PoolShotRpc(float angle, float power, float spinX, float spinY, float cueX, float cueZ) =>
            MatchManager.Instance?.ServerPoolShot(this, new PoolShot(angle, power, spinX, spinY), new Vector2(cueX, cueZ));
    }
}
