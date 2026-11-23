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

        private bool _boardBodyOn;
        private readonly System.Collections.Generic.Dictionary<Renderer, UnityEngine.Rendering.ShadowCastingMode> _boardOriginal = new();
        private Renderer[] _boardRenderers;
        private float _boardRefresh;

        /// <summary>
        /// Own body while seated at a board-game table (every frame): the camera hangs above the board right behind
        /// the head, so the body is shadows only; the right arm is drawn while it reaches over the board. Done every
        /// frame because a skin arriving late re-applies the normal render modes.
        /// </summary>
        internal void BoardBodyTick(bool armShown)
        {
            if (!IsOwner || Character == null) return;
            if (!_boardBodyOn)
            {
                _boardBodyOn = true;
                Character.SetFirstPersonMode(false);
                Character.SetHeadHidden(true);
                _boardRenderers = null;
            }
            if (_boardRenderers == null || Time.unscaledTime >= _boardRefresh)
            {
                _boardRenderers = Character.GetComponentsInChildren<Renderer>(true);
                _boardRefresh = Time.unscaledTime + 1f;
            }
            foreach (var r in _boardRenderers)
            {
                if (r == null) continue;
                bool arm = armShown && r.transform.IsChildOf(Character.ShoulderRight);
                var want = arm ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                if (r.shadowCastingMode == want) continue;
                if (!_boardOriginal.ContainsKey(r)) _boardOriginal[r] = r.shadowCastingMode;
                r.shadowCastingMode = want;
            }
        }

        private bool _ghost;
        private readonly System.Collections.Generic.Dictionary<Renderer, UnityEngine.Rendering.ShadowCastingMode> _ghostOriginal = new();

        /// <summary>
        /// This peer only: another player's body is drawn as shadow only, apart from the right arm (used while the
        /// other player draws on the chalkboard right in front of the local board camera; the hand and chalk stay).
        /// </summary>
        internal void SetGhostBody(bool on)
        {
            if (Character == null || on == _ghost) return;
            _ghost = on;
            if (on)
            {
                foreach (var r in Character.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.transform.IsChildOf(Character.ShoulderRight) || r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly) continue;
                    _ghostOriginal[r] = r.shadowCastingMode;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                }
            }
            else
            {
                foreach (var kv in _ghostOriginal) if (kv.Key != null) kv.Key.shadowCastingMode = kv.Value;
                _ghostOriginal.Clear();
            }
        }

        /// <summary>Got up from the table: render modes back to normal first person.</summary>
        internal void BoardBodyEnd()
        {
            if (!_boardBodyOn) return;
            _boardBodyOn = false;
            foreach (var kv in _boardOriginal) if (kv.Key != null) kv.Key.shadowCastingMode = kv.Value;
            _boardOriginal.Clear();
            _boardRenderers = null;
            ApplyOwnBodyMode();
        }
    }
}
