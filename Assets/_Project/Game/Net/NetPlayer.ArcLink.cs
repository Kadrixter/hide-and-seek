using Unity.Collections;
using Unity.Netcode;

namespace HideAndSeek.Game.Net
{
    /// <summary>Owner -> server requests of the KABELKI arcade cabinet in the lobby (MatchManager.ArcLink).</summary>
    public partial class NetPlayer
    {
        /// <summary>This player plays at the KABELKI cabinet right now.</summary>
        public bool AtArcLink => MatchManager.Instance != null && MatchManager.Instance.ArcLinkUser.Value == NetworkObjectId;

        /// <summary>E at the cabinet (<paramref name="use"/> = true, a new puzzle) or Esc (false).</summary>
        [Rpc(SendTo.Server)]
        public void ArcLinkUseRpc(bool use) => MatchManager.Instance?.ServerArcLinkUse(this, use);

        /// <summary>The line of a colour as it is during / at the end of a drag (two-digit cells from its dot).</summary>
        [Rpc(SendTo.Server)]
        public void ArcLinkPathRpc(byte color, FixedString512Bytes cells, byte dragId, bool final) =>
            MatchManager.Instance?.ServerArcLinkPath(this, color, cells.ToString(), dragId, final);

        /// <summary>Right mouse button on a colour: wipe its line.</summary>
        [Rpc(SendTo.Server)]
        public void ArcLinkClearRpc(byte color) => MatchManager.Instance?.ServerArcLinkClear(this, color);
    }
}
