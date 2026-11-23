using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>Owner -> server requests of the lobby board games (MatchManager.MiniGames / MiniChess / MiniTtt).</summary>
    public partial class NetPlayer
    {
        [Rpc(SendTo.Server)]
        public void MiniSitRpc(byte game, byte seat) => MatchManager.Instance?.ServerMiniSit(this, game, seat);

        [Rpc(SendTo.Server)]
        public void MiniLeaveRpc() => MatchManager.Instance?.ServerMiniLeave(this);

        [Rpc(SendTo.Server)]
        public void MiniRematchRpc() => MatchManager.Instance?.ServerMiniRematch(this);

        [Rpc(SendTo.Server)]
        public void ChessMoveRpc(byte from, byte to, byte promo) => MatchManager.Instance?.ServerChessMove(this, from, to, promo);

        [Rpc(SendTo.Server)]
        public void ChessClockRpc() => MatchManager.Instance?.ServerChessClock(this);

        [Rpc(SendTo.Server)]
        public void TttMoveRpc(byte cell) => MatchManager.Instance?.ServerTttMove(this, cell);

        /// <summary>Own body drawn while at a board-game table (camera above the board), back to normal afterwards.</summary>
        internal void SetBoardBodyShown(bool on)
        {
            if (!IsOwner || Character == null) return;
            if (on)
            {
                Character.SetFirstPersonMode(false);
                Character.SetHeadHidden(true); // the camera hangs right behind the head
                // Only the arms are drawn for the own camera (the leaning torso would cover the board); shadows stay.
                _boardHidden.Clear();
                foreach (var r in Character.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.transform.IsChildOf(Character.ShoulderRight) || r.transform.IsChildOf(Character.ShoulderLeft)) continue;
                    if (r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly) continue;
                    _boardHidden.Add((r, r.shadowCastingMode));
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                }
            }
            else
            {
                foreach (var (r, mode) in _boardHidden) if (r != null) r.shadowCastingMode = mode;
                _boardHidden.Clear();
                ApplyOwnBodyMode();
            }
        }

        private readonly System.Collections.Generic.List<(Renderer r, UnityEngine.Rendering.ShadowCastingMode mode)> _boardHidden = new();
    }
}
