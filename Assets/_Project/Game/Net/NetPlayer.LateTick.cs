using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Per-frame work that has to run after the motor moved / turned the player this frame (LateUpdate; NetPlayer runs
    /// early in the LateUpdate order, before the head bob, the character animator and the view model).
    /// </summary>
    public partial class NetPlayer
    {
        private void LateUpdate()
        {
            if (!IsSpawned) return;
            float dt = Time.deltaTime;
            UpdateSeatLook(dt);
            UpdateGlockAim(dt);
        }
    }
}
