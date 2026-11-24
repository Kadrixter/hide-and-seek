using Unity.Netcode;

namespace HideAndSeek.Game.Net
{
    /// <summary>E on an RC car carrying an item (MatchManager.RcCargo.cs).</summary>
    public partial class NetPlayer
    {
        [Rpc(SendTo.Server)]
        public void RequestRcCargoTakeRpc(byte car) => MatchManager.Instance?.ServerRcCargoTake(this, car);
    }
}
