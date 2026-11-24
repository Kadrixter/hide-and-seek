using HideAndSeek.Game.DotLink;
using HideAndSeek.Game.UI;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Dot link panel on the local player: the "E  podłącz kable" prompt at the exit-gate box (hiders, stage DotLink),
    /// "Panel zajęty" while somebody else works on it, and while working on it: no walking or looking around, the
    /// cursor free for the screen (mouse, Esc and the view are handled by <see cref="DotLinkLocal"/> / <see cref="DotLinkCamera"/>).
    /// </summary>
    public partial class LocalPlayerController
    {
        private bool _dlBusyPrompt;
        private float _dlNextRequest;

        /// <summary>FindPrompt: the panel in front of the player.</summary>
        private bool TryDotLinkPrompt(Vector3 eye, Vector3 fwd, out Vector3 pos, out string text, out float dist)
        {
            pos = default;
            text = null;
            dist = 0f;
            var mm = MatchManager.Instance;
            if (mm == null || !mm.DotLinkRoundPhase || mm.Escape.Value != EscapeStage.DotLink || !_p.IsHider || _p.AtDotLink) return false;
            if (!mm.DotLinkCanUse(_p) || _p.Motor.IsProne || !DotLinkLayout.TryGet(out var lay)) return false;
            dist = Vector3.Distance(eye, lay.Centre);
            if (dist > 2.2f || !InView(eye, fwd, lay.Centre, 40f, lay.Box)) return false;
            pos = lay.Centre + Vector3.up * 0.2f;
            var user = mm.DotLinkUserPlayer;
            _dlBusyPrompt = user != null;
            text = mm.DotLinkPuzzleNet.Value.Length == 0 ? "E  panel się uruchamia..."
                : _dlBusyPrompt ? $"E  panel zajęty - {user.Nick} łączy kable"
                : "E  podłącz kable (panel bramy)";
            return true;
        }

        private void DotLinkPromptE()
        {
            _ignoreEThisFrame = true;
            var mm = MatchManager.Instance;
            if (mm == null || mm.DotLinkPuzzleNet.Value.Length == 0) return;
            if (_dlBusyPrompt)
            {
                GameHud.Toast("Panel zajęty - przy panelu może pracować tylko jedna osoba naraz.", 2.5f);
                return;
            }
            if (Time.time < _dlNextRequest) return;
            _dlNextRequest = Time.time + 0.5f;
            _p.DotLinkUseRpc(true);
        }

        /// <summary>Update: true while the local player works on the panel (everything else is skipped).</summary>
        private bool DotLinkTick()
        {
            if (!_p.AtDotLink) return false;
            _p.Motor.MovementEnabled = false;
            _p.Motor.LookEnabled = false;
            _p.InputReader.ManageCursor = false;
            if (_p.Flashlight != null) _p.Flashlight.ListenToInput = false;
            return true;
        }
    }
}
