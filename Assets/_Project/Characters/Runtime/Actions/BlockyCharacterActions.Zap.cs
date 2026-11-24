using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Electrocuted by an energised RC car: jolts up rigid and shakes like the fork-in-the-socket shock
    /// (<see cref="ElectrocutedPose"/>), then relaxes back into the normal stance. Driven by server time.
    /// </summary>
    public partial class BlockyCharacterActions
    {
        float zapT = -1f, zapDur = 2f;

        /// <summary><paramref name="t"/> = seconds since the hit (&lt; 0 = not electrocuted), <paramref name="duration"/> = whole zap.</summary>
        public void SetZap(float t, float duration)
        {
            EnsureInit();
            zapDur = Mathf.Max(0.5f, duration);
            zapT = t >= 0f && t < zapDur ? t : -1f;
        }

        public bool ZapActive => zapT >= 0f;

        CharacterPose ZapPose(in CharacterPose current, float t)
        {
            float w = ActionTimeline.SmoothStep01(Mathf.Clamp01(t / 0.1f)) *
                      (1f - ActionTimeline.SmoothStep01(Mathf.Clamp01((t - (zapDur - 0.4f)) / 0.4f)));
            var r = CharacterPose.Lerp(current, ElectrocutedPose(), w);
            r.PropR = current.PropR;
            return r;
        }
    }
}
