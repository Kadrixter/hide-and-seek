using Unity.Collections;
using Unity.Netcode;

namespace HideAndSeek.Game.Net
{
    /// <summary>Radio found on the parking: requests to the server (lift / put down / music) and the carrier test.</summary>
    public partial class NetPlayer
    {
        /// <summary>This player has a map radio on the shoulder (also while lifting it).</summary>
        public bool CarriesMapRadio => MatchManager.Instance != null && MatchManager.Instance.MapRadioOf(this) >= 0;

        [Rpc(SendTo.Server)]
        public void RequestMapRadioPickRpc(byte radio) => MatchManager.Instance?.ServerMapRadioPick(this, radio);

        [Rpc(SendTo.Server)]
        public void RequestMapRadioPutRpc() => MatchManager.Instance?.ServerMapRadioPut(this);

        [Rpc(SendTo.Server)]
        public void RequestMapRadioRpc(byte radio, RadioOp op, FixedString128Bytes url) => MatchManager.Instance?.ServerMapRadioOp(this, radio, op, url.ToString());
    }
}
