using HideAndSeek.Characters;
using HideAndSeek.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Local input of the fire extinguisher. Hider at a security-room bracket (or at one lying on the floor): E takes it.
    /// In the hands (outside the inventory - the hotbar is off, 1-5 only remind that it does not fit): the first left
    /// click pulls the pin, holding the left button sprays, Q puts it down (back on its bracket when standing next to
    /// it, else on the floor), T still toggles the flashlight. A foamed seeker: nothing but walking and the flashlight
    /// until the foam is wiped off (attacks are refused by the server as well).
    /// </summary>
    public partial class LocalPlayerController
    {
        /// <summary>Test hooks (-testextinguisher): hold the trigger / request a put-down.</summary>
        public static bool TestExtSpray, TestExtDrop;

        private int _extPromptIdx = -1;
        private float _extNextRequest, _extPinAt = -99f, _extEmptyToast;

        /// <summary>Called before the other actions; true while the extinguisher (or the foam) owns the hands.</summary>
        private bool ExtTick(Keyboard kb, Mouse mouse)
        {
            var mm = MatchManager.Instance;
            if (mm == null) return false;
            if (_p.IsSeeker && _p.Foamed)
            {
                if (_p.SawRev.Value) _p.SawRev.Value = false;
                if (kb.tKey.wasPressedThisFrame && _p.Flashlight != null && _p.HasFlashlight) _p.Flashlight.Toggle();
                return true;
            }
            int idx = mm.ExtHeldBy(_p);
            if (idx < 0)
            {
                if (_p.ExtSpray.Value) _p.ExtSpray.Value = false;
                return false;
            }
            var st = mm.Extinguishers[idx];
            bool trigger = (mouse.leftButton.isPressed && _p.InputReader.CursorLocked) || TestExtSpray;
            bool click = (mouse.leftButton.wasPressedThisFrame && _p.InputReader.CursorLocked) || (TestExtSpray && !_p.ExtSpray.Value);
            if (!st.Unlocked && click && Time.time >= _extNextRequest)
            {
                _extNextRequest = Time.time + 0.6f;
                _extPinAt = Time.time;
                _p.RequestExtRpc(MatchManager.ExtUnlock, (byte)idx);
            }
            bool pinAnim = Time.time - _extPinAt < ExtTimeline.SprayAfterUnlock;
            bool spray = trigger && st.Unlocked && st.Charge > 0 && !pinAnim && !_p.Motor.IsProne;
            if (_p.ExtSpray.Value != spray) _p.ExtSpray.Value = spray;
            if (click && st.Unlocked && st.Charge == 0 && Time.time >= _extEmptyToast)
            {
                _extEmptyToast = Time.time + 2.5f;
                GameHud.Toast("Gaśnica jest pusta - Q, żeby ją odłożyć.", 2.2f);
            }
            if ((kb.qKey.wasPressedThisFrame || TestExtDrop) && Time.time >= _extNextRequest)
            {
                TestExtDrop = false;
                _extNextRequest = Time.time + 0.5f;
                _p.RequestExtRpc(MatchManager.ExtPutDown, (byte)idx);
            }
            for (int i = 0; i < 5; i++)
                if (kb[Key.Digit1 + i].wasPressedThisFrame && Time.time >= _extEmptyToast)
                {
                    _extEmptyToast = Time.time + 2f;
                    GameHud.Toast("Gaśnica nie mieści się do ekwipunku - trzymasz ją w rękach. Q - odłóż.", 2.5f);
                }
            if (kb.tKey.wasPressedThisFrame && _p.Flashlight != null && _p.HasFlashlight) _p.Flashlight.Toggle();
            SawWrestWithExt(kb, mm.Phase.Value); // E: grab the saw of the seeker just foamed (LocalPlayerController.SawWrest.cs)
            return true;
        }

        /// <summary>Prompt at an extinguisher on its bracket or on the floor (hiders only).</summary>
        private bool TryExtPrompt(Vector3 eye, Vector3 fwd, out Vector3 pos, out string text, out float dist)
        {
            pos = default;
            text = null;
            dist = float.MaxValue;
            _extPromptIdx = -1;
            var mm = MatchManager.Instance;
            if (mm == null || ExtinguisherSystem.Instance == null || !mm.ExtCanHold(_p) || mm.ExtHeldBy(_p) >= 0 || _p.Motor.IsProne) return false;
            for (int i = 0; i < mm.Extinguishers.Count; i++)
            {
                var st = mm.Extinguishers[i];
                if (st.Where == ExtState.InHands) continue;
                Vector3 at;
                if (st.Where == ExtState.OnWall)
                {
                    ExtinguisherSystem.HookPose(i, out var hp, out _);
                    at = hp + Vector3.up * 0.45f;
                }
                else at = st.Pos + Vector3.up * 0.12f;
                float d = Vector3.Distance(eye, at);
                Vector3 feet = st.Where == ExtState.OnWall ? SecurityRooms.ExtinguisherSpot(i) : st.Pos;
                Vector3 fd = transform.position - feet;
                if (d >= dist || new Vector2(fd.x, fd.z).magnitude > ExtTuning.PromptRange || Mathf.Abs(fd.y) > 1.3f) continue;
                if (!InView(eye, fwd, at, 40f, ExtinguisherSystem.Instance.transform)) continue;
                dist = d;
                pos = at;
                _extPromptIdx = i;
                bool far = new Vector2(fd.x, fd.z).magnitude > ExtTuning.TakeRange;
                text = far ? "E  gaśnica (podejdź bliżej)" : st.Charge == 0 ? "E  weź gaśnicę (pusta)" : "E  weź gaśnicę";
            }
            return _extPromptIdx >= 0;
        }

        private void ExtPromptE()
        {
            if (_extPromptIdx < 0 || Time.time < _extNextRequest) return;
            _ignoreEThisFrame = true;
            _extNextRequest = Time.time + 0.5f;
            _p.RequestExtRpc(MatchManager.ExtTake, (byte)_extPromptIdx);
        }
    }
}
