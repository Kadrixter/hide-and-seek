using Unity.Collections;
using Unity.Netcode;

namespace HideAndSeek.Game.Net
{
    /// <summary>Owner -> server requests of the dot link panel at the exit gate (MatchManager.DotLink).</summary>
    public partial class NetPlayer
    {
        /// <summary>This player works on the dot link panel right now.</summary>
        public bool AtDotLink => MatchManager.Instance != null && MatchManager.Instance.DotLinkGateOf(this) >= 0;

        /// <summary>E at the panel (<paramref name="use"/> = true) or Esc (false).</summary>
        [Rpc(SendTo.Server)]
        public void DotLinkUseRpc(bool use) => MatchManager.Instance?.ServerDotLinkUse(this, use);

        /// <summary>The line of a colour as it is during / at the end of a drag (two-digit cells from its dot).</summary>
        [Rpc(SendTo.Server)]
        public void DotLinkPathRpc(byte color, FixedString512Bytes cells, byte dragId, bool final) =>
            MatchManager.Instance?.ServerDotLinkPath(this, color, cells.ToString(), dragId, final);

        /// <summary>Right mouse button on a colour: wipe its line.</summary>
        [Rpc(SendTo.Server)]
        public void DotLinkClearRpc(byte color) => MatchManager.Instance?.ServerDotLinkClear(this, color);
    }
}
