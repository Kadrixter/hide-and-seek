using UnityEngine;
using UnityEngine.InputSystem;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Local input of the security-room swivel chairs (<see cref="SecurityChairDriver"/>): E on a free chair sits down,
    /// while seated the mouse turns the chair, E / Space / a movement key gets up, T still toggles the flashlight;
    /// other actions wait until the player stands.
    /// </summary>
    public partial class LocalPlayerController
    {
        private int _chairPrompt = -1;
        private float _chairNextRequest;

        /// <summary>Called before the other actions; true while seated on a chair (the rest is skipped).</summary>
        private bool ChairTick(Keyboard kb)
        {
            if (SecurityChairDriver.LocalChair < 0) return false;
            bool stand = !_ignoreEThisFrame && (kb.eKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame ||
                         kb.wKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame);
            if (stand)
            {
                _ignoreEThisFrame = true;
                SecurityChairDriver.LeaveLocal();
            }
            if (kb.tKey.wasPressedThisFrame && _p.Flashlight != null && _p.HasFlashlight) _p.Flashlight.Toggle();
            return true;
        }

        /// <summary>Prompt at the nearest free chair in view.</summary>
        private bool TryChairPrompt(Vector3 eye, Vector3 fwd, out Vector3 pos, out string text, out float dist)
        {
            pos = default;
            text = null;
            dist = float.MaxValue;
            _chairPrompt = -1;
            var mm = MatchManager.Instance;
            var sr = SecurityRooms.Instance;
            if (mm == null || sr == null || !mm.ChairCanSit(_p) || _p.Motor.IsProne || SecurityChairDriver.LocalChair >= 0) return false;
            var st = mm.SecurityChairs.Value;
            for (int i = 0; i < sr.Chairs.Count; i++)
            {
                if (st.Sitter(i) != ChairsState.Free) continue;
                var c = sr.Chairs[i];
                Vector3 seat = c.SeatCenter;
                float d = Vector3.Distance(eye, seat);
                if (d > 2.0f || d >= dist || !InView(eye, fwd, seat, 35f, c.Root)) continue;
                dist = d;
                pos = seat + Vector3.up * 0.15f;
                _chairPrompt = i;
            }
            if (_chairPrompt < 0) return false;
            text = "E  usiądź na krześle";
            return true;
        }

        private void ChairPromptE()
        {
            if (_chairPrompt < 0 || Time.time < _chairNextRequest) return;
            _chairNextRequest = Time.time + 0.5f;
            _ignoreEThisFrame = true;
            MatchManager.Instance?.ChairRequestRpc((byte)_chairPrompt, true, (short)Mathf.RoundToInt(Mathf.Repeat(transform.eulerAngles.y, 360f)));
        }
    }
}
