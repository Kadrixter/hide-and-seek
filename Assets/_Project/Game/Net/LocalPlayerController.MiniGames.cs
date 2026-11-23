using HideAndSeek.Core;
using HideAndSeek.Game.MiniGames;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Lobby board games on the local player: the "E  usiądź" prompt at a free seat, and while seated: no walking or
    /// looking around, the cursor free for the board, the character controller off (the stool sits under the table
    /// edge), own body drawn. Keys and clicks are handled by <see cref="MiniGameLocal"/>, the view by <see cref="MiniGameCamera"/>.
    /// </summary>
    public partial class LocalPlayerController
    {
        private int _miniGame = -1, _miniSeat = -1;
        private bool _miniWasSeated;

        /// <summary>FindPrompt: nearest free seat at a table in front of the player.</summary>
        private bool MiniGamePrompt(Vector3 eye, Vector3 fwd, out Vector3 pos, out string text, out float dist)
        {
            bool ok = MiniGameTables.TryPrompt(eye, fwd, (c, t) => InView(eye, fwd, c, 40f, t), out _miniGame, out _miniSeat, out pos, out text, out dist);
            if (ok && (_p.Seat.Value >= 0 || _p.Busy.Value)) ok = false;
            return ok;
        }

        private void MiniGamePromptE()
        {
            if (_miniGame >= 0 && _miniSeat >= 0) _p.MiniSitRpc((byte)_miniGame, (byte)_miniSeat);
        }

        /// <summary>Update: true while the local player sits at a board-game table (everything else is skipped).</summary>
        private bool MiniGameTick(Keyboard kb, Mouse mouse)
        {
            var mm = MatchManager.Instance;
            var table = MiniGameTables.LocalTable(out int seat);
            bool seated = table != null && mm != null && mm.Phase.Value == MatchPhase.Lobby && _p.Life.Value == LifeState.Alive;
            MiniGameCamera.Attach(_p, seated ? table : null, seat);
            if (!seated)
            {
                if (_miniWasSeated)
                {
                    _miniWasSeated = false;
                    _p.SetBoardBodyShown(false);
                }
                return false;
            }
            if (!_miniWasSeated)
            {
                _miniWasSeated = true;
                _p.SetBoardBodyShown(true);
                MiniGameLocal.Ensure();
            }
            _p.Motor.MovementEnabled = false;
            _p.Motor.LookEnabled = false;
            _p.InputReader.ManageCursor = false;
            if (!Cursor.visible) Cursor.visible = true;
            if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
            var cc = _p.GetComponent<CharacterController>();
            if (cc != null && cc.enabled) cc.enabled = false;
            if (_p.Flashlight != null) _p.Flashlight.ListenToInput = false;
            return true;
        }
    }
}
