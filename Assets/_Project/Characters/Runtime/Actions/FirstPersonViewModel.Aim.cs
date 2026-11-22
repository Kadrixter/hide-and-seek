using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// First-person Glock aiming (RMB): the gun moves from the relaxed hip position (lower right) towards the middle of
    /// the screen with the sights just under the crosshair. The game drives <see cref="SetGlockAim"/> every frame.
    /// </summary>
    public partial class FirstPersonViewModel
    {
        static readonly Vector3 HipGunPos = new Vector3(0.16f, -0.3f, 0.78f);
        static readonly Vector3 AimGunPos = new Vector3(0.05f, -0.215f, 0.76f);

        float glockAim;

        /// <summary>0 = hip, 1 = aimed (smoothed by the caller).</summary>
        public void SetGlockAim(float weight) => glockAim = Mathf.Clamp01(weight);

        /// <summary>Right-hand gun position (rig space) for the current aim weight.</summary>
        Vector3 GlockHoldPos => Vector3.Lerp(HipGunPos, AimGunPos, ActionTimeline.SmoothStep01(glockAim));
    }
}
