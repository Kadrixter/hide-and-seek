using Unity.Collections;
using Unity.Netcode;

namespace HideAndSeek.Game.Net
{
    /// <summary>Lobby TV: the panel's requests to the server (<see cref="MatchManager.LobbyTv"/>).</summary>
    public partial class NetPlayer
    {
        [Rpc(SendTo.Server)]
        public void RequestTvRpc(RadioOp op, FixedString128Bytes url) => MatchManager.Instance?.ServerTv(this, op, url.ToString());
    }
}
