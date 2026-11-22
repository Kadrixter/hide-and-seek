using HideAndSeek.Characters;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// First-person view while cutting the security room crate with the chainsaw: the camera is guided down onto the
    /// crate and follows the cut as it goes down (the third-person head does the same in the action layer).
    /// </summary>
    public partial class NetPlayer
    {
        /// <summary>Wanted camera pitch (degrees, negative = down) at time <paramref name="t"/> of the cut.</summary>
        private static float CrateSawViewPitch(float t)
        {
            float cut = BlockyCharacterActions.CrateCutProgress(t);
            float start = Mathf.Clamp01(t / 0.45f);
            float end = Mathf.Clamp01((t - (BlockyCharacterActions.CrateSawBreakTime + 0.2f)) / 0.5f);
            float pitch = Mathf.Lerp(-14f, -24f, start) - 14f * cut;
            return Mathf.Lerp(pitch, -18f, end);
        }
    }
}
