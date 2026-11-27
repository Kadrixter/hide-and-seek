using HideAndSeek.Game.DotLink;
using HideAndSeek.Game.UI;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Dot link panel on the local player: the "E  podłącz kable" prompt at the exit-gate box (hiders, stage DotLink) and
    /// "E  zagraj w KABELKI" at the lobby cabinet,
    /// "Panel zajęty" while somebody else works on it, and while working on it: no walking or looking around, the
    /// cursor free for the screen (mouse, Esc and the view are handled by <see cref="DotLinkLocal"/> / <see cref="DotLinkCamera"/>).
    /// </summary>
    public partial class LocalPlayerController
    {
        private bool _dlBusyPrompt;
        private int _dlPromptGate;
        private float _dlNextRequest;

        /// <summary>FindPrompt: the panel in front of the player.</summary>
        private bool TryDotLinkPrompt(Vector3 eye, Vector3 fwd, out Vector3 pos, out string text, out float dist)
        {
            pos = default;
            text = null;
            dist = 0f;
            var mm = MatchManager.Instance;
            if (mm == null || !mm.DotLinkRoundPhase || mm.Escape.Value < EscapeStage.DotLink || !_p.IsHider || _p.AtDotLink) return false;
            if (!mm.DotLinkCanUse(_p) || _p.Motor.IsProne || !DotLinkLayout.TryNearest(_p.transform.position, out var lay)) return false;
            if (mm.EscapeGateAtOf(lay.Gate).Value > 0d) return false; // this gate's panel is solved - its gate is open
            dist = Vector3.Distance(eye, lay.Centre);
            if (dist > 2.2f || !InView(eye, fwd, lay.Centre, 40f, lay.Box)) return false;
            pos = lay.Centre + Vector3.up * 0.2f;
            var user = mm.DotLinkUserPlayerOf(lay.Gate);
            _dlBusyPrompt = user != null;
            _dlPromptGate = lay.Gate;
            text = mm.DotLinkPuzzleOf(lay.Gate).Value.Length == 0 ? "E  panel się uruchamia..."
                : _dlBusyPrompt ? $"E  panel zajęty - {user.Nick} łączy kable"
                : $"E  podłącz kable (brama {MatchManager.GateName(lay.Gate)})";
            return true;
        }

        private void DotLinkPromptE()
        {
            _ignoreEThisFrame = true;
            var mm = MatchManager.Instance;
            if (mm == null || mm.DotLinkPuzzleOf(_dlPromptGate).Value.Length == 0) return;
            if (_dlBusyPrompt)
            {
                GameHud.Toast("Panel zajęty - przy panelu może pracować tylko jedna osoba naraz.", 2.5f);
                return;
            }
            if (Time.time < _dlNextRequest) return;
            _dlNextRequest = Time.time + 0.5f;
            _p.DotLinkUseRpc(true);
        }

        private bool _alBusyPrompt;

        /// <summary>FindPrompt: the KABELKI cabinet in the lobby (MatchManager.ArcLink).</summary>
        private bool TryArcLinkPrompt(Vector3 eye, Vector3 fwd, out Vector3 pos, out string text, out float dist)
        {
            pos = default;
            text = null;
            dist = 0f;
            var mm = MatchManager.Instance;
            var cab = DotLinkArcade.Instance;
            if (mm == null || cab == null || cab.ScreenQuad == null || _p.AtArcLink || !mm.ArcLinkCanUse(_p)) return false;
            Vector3 sp = cab.ScreenQuad.position;
            dist = Vector3.Distance(eye, sp);
            if (dist > 2.4f || !InView(eye, fwd, sp, 30f)) return false;
            pos = sp + Vector3.up * 0.3f;
            var user = mm.ArcLinkUserPlayer;
            _alBusyPrompt = user != null;
            text = _alBusyPrompt ? $"Gra {user.Nick} - patrz, jak mu idzie" : "E  zagraj w KABELKI (nowa łamigłówka)";
            return true;
        }

        private void ArcLinkPromptE()
        {
            _ignoreEThisFrame = true;
            if (_alBusyPrompt)
            {
                GameHud.Toast("Automat zajęty - poczekaj, aż skończy.", 2f);
                return;
            }
            if (Time.time < _dlNextRequest) return;
            _dlNextRequest = Time.time + 0.5f;
            _p.ArcLinkUseRpc(true);
        }

        /// <summary>Update: true while the local player works on the panel (everything else is skipped).</summary>
        private bool DotLinkTick()
        {
            if (!_p.AtDotLink && !_p.AtArcLink) return false;
            _p.Motor.MovementEnabled = false;
            _p.Motor.LookEnabled = false;
            _p.InputReader.ManageCursor = false;
            if (_p.Flashlight != null) _p.Flashlight.ListenToInput = false;
            return true;
        }
    }
}
