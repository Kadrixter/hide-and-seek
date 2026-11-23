using HideAndSeek.Audio;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// A third hider treats a carried player with a medkit (H held for <see cref="BodyCarryTimeline.HealTime"/> s,
    /// LocalPlayerController.CarryHeal.cs): the healer steps to the side of the body, the carrier stands still, the
    /// healer's hands work on the belly of the body in the carrier's arms (BlockyCharacterActions.CarryHeal.cs), the
    /// bandage grows on it. Owner-written target + progress (like the other hold animations), the server checks and
    /// applies the result: downed -> back on (wounded, the carry goes on, now awake), wounded / limping -> healed.
    /// Also: carrying stops the bleed-out clock (<see cref="BleedPausedLeft"/>).
    /// </summary>
    public partial class NetPlayer
    {
        public readonly NetworkVariable<ulong> CarryHealTarget = new(ulong.MaxValue, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public readonly NetworkVariable<byte> CarryHealProgress = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        /// <summary>Server: bleed-out seconds left while the clock is stopped by a carry (negative = running).</summary>
        public readonly NetworkVariable<float> BleedPausedLeft = new(-1f);

        private float _chShownT;
        private ulong _chLastTarget = ulong.MaxValue;

        /// <summary>Owner's own hold time (set by the local controller).</summary>
        internal float CarryHealLocalT;

        public bool HealingCarried => CarryHealTarget.Value != ulong.MaxValue;

        /// <summary>The bleed-out clock stands still (somebody carries this downed player).</summary>
        public bool BleedPaused => BleedPausedLeft.Value >= 0f && Life.Value == LifeState.Downed;

        /// <summary>Seconds of bleeding left now (paused value while carried).</summary>
        public double BleedLeftNow
        {
            get
            {
                if (BleedPaused) return BleedPausedLeft.Value;
                var mm = MatchManager.Instance;
                return DownedUntil.Value - (mm != null ? mm.Now : 0d);
            }
        }

        /// <summary>The player treating <paramref name="patient"/> right now (null = nobody).</summary>
        public static NetPlayer CarryHealerOf(NetPlayer patient)
        {
            if (patient == null) return null;
            foreach (var o in All)
                if (o != null && o != patient && o.CarryHealTarget.Value == patient.NetworkObjectId) return o;
            return null;
        }

        /// <summary>The carrier may neither walk nor turn: somebody is treating the body in his arms.</summary>
        public bool CarryTurnLocked
        {
            get
            {
                var v = CarryVar.Value;
                return v.Active && CarryHealerOf(Find(v.Target)) != null;
            }
        }

        /// <summary>Seconds into the treatment shown by this healer (owner exact, others smoothed).</summary>
        public float CarryHealShownT => _chShownT;

        /// <summary>Every peer, every frame (from UpdateBodyCarry).</summary>
        private void UpdateCarryHeal()
        {
            var bca = Actions as BlockyCharacterActions;
            if (IsOwner && HealingCarried && (!IsAliveHider || Busy.Value))
            {
                CarryHealTarget.Value = ulong.MaxValue; // died / caught while treating
                CarryHealProgress.Value = 0;
            }
            ulong tid = CarryHealTarget.Value;
            if (tid != _chLastTarget) { _chShownT = 0f; _chLastTarget = tid; }
            var patient = tid != ulong.MaxValue ? Find(tid) : null;
            float target = CarryHealProgress.Value / 255f * BodyCarryTimeline.HealTime;
            _chShownT = IsOwner ? CarryHealLocalT : Mathf.MoveTowards(_chShownT, target, Time.deltaTime * 1.5f + Mathf.Abs(target - _chShownT) * 0.2f);
            if (patient == null || patient.Character == null || !patient.BodyCarriedNow)
            {
                bca?.SetCarryHealing(-1f, null);
                return;
            }
            bca?.SetCarryHealing(_chShownT, patient.Character.Torso);
            bca?.SetCarried(0, null); // the kit's roll is in the hand instead of the item
            if (IsOwner) (ViewModel as FirstPersonViewModel)?.SetCarried(0, null);
        }

        /// <summary>Patient side (every peer): the bandage grows while being treated.</summary>
        private void UpdateCarryHealPatient()
        {
            var healer = CarryHealerOf(this);
            if (healer == null) return;
            float wrap = ActionTimeline.Remap01(healer.CarryHealShownT, BlockyCharacterActions.CarryHealPad, BlockyCharacterActions.CarryHealWrapEnd);
            bool hurt = (Wounded.Value || Limping.Value) && Life.Value == LifeState.Alive;
            (Actions as BlockyCharacterActions)?.SetBandaged(Mathf.Max(hurt ? 1f : 0f, wrap));
        }

        /// <summary>Owner of the healer: the treatment is done.</summary>
        [Rpc(SendTo.Server)]
        public void RequestCarriedHealRpc(ulong targetId)
        {
            var t = Find(targetId);
            if (t == null || !IsAliveHider || Busy.Value || MedkitCount.Value == 0 || t.Busy.Value || !t.BodyCarriedNow || !t.IsHider) return;
            if (Vector3.Distance(transform.position, t.transform.position) > Tuning.RescueRange + 1.5f) return;
            if (t.Life.Value == LifeState.Downed)
            {
                MedkitCount.Value--;
                t.Life.Value = LifeState.Alive;
                t.Wounded.Value = true;
                t.Limping.Value = false;
                t.DownedUntil.Value = 0;
                t.BleedPausedLeft.Value = -1f;
                t.Held.Value = HeldItem.None;
                t.ToastRpc($"{Nick} opatrzył cię na rękach {CarriedByNick(t)}. Spacja - zejdź na ziemię.", 5f);
            }
            else if (t.Life.Value == LifeState.Alive && (t.Wounded.Value || t.Limping.Value))
            {
                MedkitCount.Value--;
                t.Wounded.Value = false;
                t.Limping.Value = false;
                t.ToastRpc($"{Nick} opatrzył twoje rany.", 4f);
            }
            else return;
            t.SfxRpc(GameSfx.MedkitUse, 1f);
            ToastRpc($"Opatrzone: {t.Nick}.", 3f);
            Debug.Log($"[Carry] {Nick} treated {t.Nick} in the arms of {CarriedByNick(t)} (now {t.Life.Value}, wounded {t.Wounded.Value})");
        }

        private static string CarriedByNick(NetPlayer t)
        {
            var c = t.CarriedBy;
            return c != null ? c.Nick : "?";
        }

        /// <summary>Server, every frame on every player: stops / restarts this player's bleed-out clock.</summary>
        private void ServerBleedPause()
        {
            var mm = MatchManager.Instance;
            if (mm == null) return;
            bool downed = Life.Value == LifeState.Downed;
            bool paused = BleedPausedLeft.Value >= 0f;
            bool carried = CarriedBy != null && mm.Phase.Value == MatchPhase.Seeking;
            if (downed && carried && !paused && DownedUntil.Value > 0d)
            {
                float left = Mathf.Max(1f, (float)(DownedUntil.Value - mm.Now));
                BleedPausedLeft.Value = left;
                DownedUntil.Value = double.MaxValue * 0.25; // MatchManager.UpdateDowned never reaches it
                Debug.Log($"[Carry] {Nick}: bleed-out paused at {left:0.0} s");
            }
            else if (paused && (!downed || !carried))
            {
                if (downed) DownedUntil.Value = mm.Now + BleedPausedLeft.Value;
                Debug.Log($"[Carry] {Nick}: bleed-out {(downed ? "runs again" : "pause cleared")} ({BleedPausedLeft.Value:0.0} s left)");
                BleedPausedLeft.Value = -1f;
            }
        }
    }
}
