using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Seeker movement penalties of the fight, as one extra multiplier on top of the other speed modifiers
    /// (applied where the motor's speed scale is set):
    /// - after a melee attack / a finished kill the seeker moves at <see cref="Tuning.AttackSlowScale"/> for
    ///   <see cref="Tuning.AttackSlowHold"/> s and then smoothly returns to full speed over <see cref="Tuning.AttackSlowFade"/> s,
    ///   so the hider gets a moment to run away. Derived from the synced <see cref="AttackReadyAt"/>
    ///   (the server sets it to now + cooldown when an attack starts / a kill ends), no extra network state.
    /// - aiming the Glock (RMB) slows walking a little.
    /// - the chainsaw in the hands: -15% (<see cref="SawSpeedScale"/>, NetPlayer.SawNerf.cs).
    /// </summary>
    public partial class NetPlayer
    {
        /// <summary>Product of all fight-related speed penalties (1 = none).</summary>
        public float CombatSpeedScale => AttackSlowScale * SawSpeedScale * BatterySpeedScale * (GlockAiming.Value && Held.Value == HeldItem.Glock ? Tuning.GlockAimSpeedScale : 1f); // + the heavy car battery (NetPlayer.CarBattery.cs)

        /// <summary>Seconds since the seeker's last attack / kill (negative or huge when none).</summary>
        public double SinceLastAttack
        {
            get
            {
                var mm = MatchManager.Instance;
                if (mm == null || AttackReadyAt.Value <= 0d) return double.MaxValue;
                return mm.Now - (AttackReadyAt.Value - Tuning.AttackCooldown);
            }
        }

        public float AttackSlowScale
        {
            get
            {
                if (!IsSeeker || Busy.Value) return 1f;
                double since = SinceLastAttack;
                if (since < 0d || since > Tuning.AttackSlowHold + Tuning.AttackSlowFade) return 1f;
                float k = Mathf.Clamp01((float)(since - Tuning.AttackSlowHold) / Tuning.AttackSlowFade);
                k = k * k * (3f - 2f * k);
                return Mathf.Lerp(Tuning.AttackSlowScale, 1f, k);
            }
        }
    }
}
