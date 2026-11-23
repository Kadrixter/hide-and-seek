using Unity.Netcode;

namespace HideAndSeek.Game.Net
{
    /// <summary>Pulling a rod out of rubble: the owner asks to stop (moved, jumped, attacked); see MatchManager.RodPull.</summary>
    public partial class NetPlayer
    {
        [Rpc(SendTo.Server)]
        public void RequestRodPullCancelRpc() => MatchManager.Instance?.ServerCancelRodPull(this);
    }
}
