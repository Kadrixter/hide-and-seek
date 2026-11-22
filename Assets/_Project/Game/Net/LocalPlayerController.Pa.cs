using UnityEngine;
using UnityEngine.InputSystem;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Local input of the parking public address. At a wall station: E takes the microphone (free one, within reach),
    /// a taken one shows "zajęty". While holding it: the left mouse button is the talk bar (read by the voice chat,
    /// works with push-to-talk and with the open microphone), E hangs it back, T still toggles the flashlight;
    /// items, attacks and other prompts wait until it is back on the hook. Lying down or walking off farther than the
    /// cord reaches lets go of it as well (the server checks the same).
    /// </summary>
    public partial class LocalPlayerController
    {
        private byte _paStation;
        private float _paNextRequest;
        private bool _paBusyPrompt, _paFarPrompt;

        /// <summary>Called before the other actions; true while the microphone is in the hand (the rest is skipped).</summary>
        private bool PaTick(Keyboard kb)
        {
            var mm = MatchManager.Instance;
            if (mm == null || !_p.HoldsPaMic) return false;
            bool leave = (kb.eKey.wasPressedThisFrame && !_ignoreEThisFrame) || _p.Motor.IsProne;
            Vector3 chest = transform.position + Vector3.up * 1.2f;
            if (Vector3.Distance(chest, PaLayout.GlandPos(mm.PaStation.Value)) > PaLayout.MaxReach) leave = true;
            if (leave)
            {
                _ignoreEThisFrame = true;
                if (Time.time >= _paNextRequest)
                {
                    _paNextRequest = Time.time + 0.4f;
                    _p.RequestPaRpc(false, mm.PaStation.Value);
                }
            }
            if (kb.tKey.wasPressedThisFrame && _p.Flashlight != null && _p.HasFlashlight) _p.Flashlight.Toggle();
            return true;
        }

        /// <summary>Prompt at the microphone hanging on a station of this level.</summary>
        private bool TryPaPrompt(Vector3 eye, Vector3 fwd, out Vector3 pos, out string text, out float dist)
        {
            pos = default;
            text = null;
            dist = 0f;
            var mm = MatchManager.Instance;
            if (mm == null || PaSystem.Instance == null || !mm.PaCanHold(_p) || _p.HoldsPaMic || _p.Motor.IsProne) return false;
            int lv = PaLayout.StationNear(transform.position, PaLayout.PromptRange);
            if (lv < 0) return false;
            Vector3 mic = PaLayout.MicHangPos(lv);
            if (!InView(eye, fwd, mic, 42f, PaSystem.Instance.transform)) return false;
            Vector3 d = transform.position - mic;
            float flat = new Vector2(d.x, d.z).magnitude;
            _paStation = (byte)lv;
            _paBusyPrompt = mm.PaHolder.Value != MatchManager.NoPaHolder;
            _paFarPrompt = flat > PaLayout.TakeRange;
            pos = mic + Vector3.up * 0.13f;
            dist = Vector3.Distance(eye, mic);
            text = _paBusyPrompt ? "E  mikrofon zajęty - ktoś mówi przez nagłośnienie"
                : _paFarPrompt ? "E  mikrofon nagłośnienia (podejdź bliżej)"
                : "E  weź mikrofon nagłośnienia";
            return true;
        }

        private void PaPromptE()
        {
            if (_paBusyPrompt)
            {
                GameHud.Toast("Mikrofon nagłośnienia jest zajęty - mówić może tylko jedna osoba naraz.", 2.5f);
                return;
            }
            if (_paFarPrompt) return;
            _ignoreEThisFrame = true;
            if (Time.time < _paNextRequest) return;
            _paNextRequest = Time.time + 0.4f;
            _p.RequestPaRpc(true, _paStation);
        }
    }
}
