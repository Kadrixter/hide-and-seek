using UnityEngine;

namespace HideAndSeek.Characters
{
    public partial class BlockyCharacterActions
    {
        /// <summary>
        /// Drunk from the vending machine (0..1): swaying stance, staggering gait, arms out for balance
        /// (<see cref="BlockyCharacterAnimator.drunk"/>). <paramref name="clock"/> and <paramref name="seed"/> drive the
        /// shared sway signal (<see cref="HideAndSeek.Core.Drunkenness.Sample"/>), the same one the owner's camera uses.
        /// Full-body actions (sitting, crawling, the vending purchase...) blend over it as usual.
        /// </summary>
        public void SetDrunk(float amount, float clock, float seed)
        {
            EnsureInit();
            if (animator == null) return;
            animator.drunk = Mathf.Clamp01(amount);
            animator.drunkClock = clock;
            animator.drunkSeed = seed;
        }
    }
}
