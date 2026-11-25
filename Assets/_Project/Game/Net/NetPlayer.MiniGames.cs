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

        /// <summary>Pool: "Gotowy" on / off in the join menu.</summary>
        [Rpc(SendTo.Server)]
        public void PoolReadyRpc(bool ready) => MatchManager.Instance?.ServerPoolReady(this, ready);

        [Rpc(SendTo.Server)]
        public void ChessMoveRpc(byte from, byte to, byte promo) => MatchManager.Instance?.ServerChessMove(this, from, to, promo);

        [Rpc(SendTo.Server)]
        public void ChessClockRpc() => MatchManager.Instance?.ServerChessClock(this);

        /// <summary>Chess table: pick the game (ChessState.KindChess / KindCheckers).</summary>
        [Rpc(SendTo.Server)]
        public void BoardKindRpc(byte kind) => MatchManager.Instance?.ServerBoardKind(this, kind);

        /// <summary>Chess table, checkers: the move's path of square names ("c3e5c7").</summary>
        [Rpc(SendTo.Server)]
        public void CheckersMoveRpc(Unity.Collections.FixedString64Bytes path) => MatchManager.Instance?.ServerCheckersMove(this, path.ToString());

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
                // The first-person arms have their own camera (it follows the eyes, not the board view): off at the table.
                ViewModel?.SetVisible(false);
            }
            // The first-person arms stay off the whole time: their own camera would draw them over the board view
            // (anything that turns them back on - an emote, a state change - is undone here every frame).
            var vmCam = (ViewModel as HideAndSeek.Characters.FirstPersonViewModel)?.ViewModelCamera;
            if (vmCam != null && vmCam.enabled) ViewModel.SetVisible(false);
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

        private bool _ghost, _ghostArm = true;
        private readonly System.Collections.Generic.Dictionary<Renderer, UnityEngine.Rendering.ShadowCastingMode> _ghostOriginal = new();

        /// <summary>
        /// This peer only: another player's body is drawn as shadow only, apart from the right arm (used while the
        /// other player draws on the chalkboard right in front of the local board camera; the hand and chalk stay).
        /// </summary>
        internal void SetGhostBody(bool on) => SetGhostBody(on, true);

        /// <summary><see cref="SetGhostBody(bool)"/>; <paramref name="keepArm"/> false = the right arm goes too (pool table: a body right in front of the camera).</summary>
        internal void SetGhostBody(bool on, bool keepArm)
        {
            if (Character == null || on == _ghost && keepArm == _ghostArm) return;
            if (_ghost && on) SetGhostBody(false, _ghostArm);
            _ghost = on;
            _ghostArm = keepArm;
            if (on)
            {
                foreach (var r in Character.GetComponentsInChildren<Renderer>(true))
                {
                    if (keepArm && r.transform.IsChildOf(Character.ShoulderRight) || r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly) continue;
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
            ViewModel?.SetVisible(_vmVisible);
        }
    }
}
