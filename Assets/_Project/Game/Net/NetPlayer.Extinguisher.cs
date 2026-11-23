using HideAndSeek.Characters;
using HideAndSeek.Core;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Fire extinguisher state of a player. Hider: holds one (from <see cref="MatchManager.Extinguishers"/>), sprays it
    /// (<see cref="ExtSpray"/>, written by the owner for an instant start on every peer). Seeker: covered in foam
    /// between <see cref="FoamStart"/> and <see cref="FoamUntil"/> (server time): he cannot attack (knife, chainsaw,
    /// Glock - checked by the server in MatchManager.ServerKnife / ServerChainsaw / ServerShoot through
    /// <see cref="FoamBlocksAttack"/>), is slowed (<see cref="FoamSpeedScale"/>), puts the weapon away and wipes his face.
    /// Kept apart from the other "cannot attack" effects (dust: BlindUntil, goo: GooUntil).
    /// </summary>
    public partial class NetPlayer
    {
        public readonly NetworkVariable<bool> ExtSpray = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public readonly NetworkVariable<double> FoamStart = new();
        public readonly NetworkVariable<double> FoamUntil = new();

        /// <summary>Index of the extinguisher in this player's hands (-1 = none).</summary>
        public int ExtIndex => MatchManager.Instance != null ? MatchManager.Instance.ExtHeldBy(this) : -1;
        public bool HoldsExtinguisher => ExtIndex >= 0;

        /// <summary>Seconds of foam left (0 = clean).</summary>
        public float FoamLeft
        {
            get
            {
                var mm = MatchManager.Instance;
                return mm == null ? 0f : Mathf.Max(0f, (float)(FoamUntil.Value - mm.Now));
            }
        }

        /// <summary>Seconds since the foam hit (valid while <see cref="Foamed"/>).</summary>
        public float FoamElapsed
        {
            get
            {
                var mm = MatchManager.Instance;
                return mm == null ? 0f : Mathf.Max(0f, (float)(mm.Now - FoamStart.Value));
            }
        }

        public bool Foamed => FoamLeft > 0f && Life.Value == LifeState.Alive;

        /// <summary>No knife / chainsaw / Glock: foam in the eyes, plus the moment the weapon needs to come back out.</summary>
        public bool FoamBlocksAttack
        {
            get
            {
                var mm = MatchManager.Instance;
                return mm != null && FoamUntil.Value > 0 && mm.Now < FoamUntil.Value + 0.35;
            }
        }

        /// <summary>Hands taken by the extinguisher (hider) or the wiping (seeker): no hotbar, no item use.</summary>
        public bool ExtBlocksHands => HoldsExtinguisher || (IsSeeker && Foamed);

        /// <summary>Movement multiplier from the foam: stumbling at the hit, slowed, recovering in the last 1.2 s.</summary>
        public float FoamSpeedScale
        {
            get
            {
                if (!Foamed) return 1f;
                float left = FoamLeft, e = FoamElapsed;
                float s = Mathf.Lerp(1f, ExtTuning.FoamSpeed, Mathf.Clamp01(left / 1.2f));
                if (e < 0.5f) s *= Mathf.Lerp(0.3f, 1f, e / 0.5f);
                return s;
            }
        }

        /// <summary>Owner: take (<see cref="MatchManager.ExtTake"/> + index), put down, or pull the pin of the held one.</summary>
        [Rpc(SendTo.Server)]
        public void RequestExtRpc(byte op, byte index) => MatchManager.Instance?.ServerExt(this, op, index);

        /// <summary>Everyone: this player pulls the safety pin of the extinguisher in his hands.</summary>
        [Rpc(SendTo.Everyone)]
        public void ExtPinRpc()
        {
            (Actions as BlockyCharacterActions)?.PlayExtPinPull();
            if (IsOwner) (ViewModel as FirstPersonViewModel)?.PlayExtPinPull();
            ExtinguisherSystem.Instance?.OnPinPulled(this);
        }

        /// <summary>Everyone: this seeker just got the foam in the face (sounds, splash; the state itself is in the variables).</summary>
        [Rpc(SendTo.Everyone)]
        public void FoamHitRpc(ulong holderId) => ExtinguisherSystem.Instance?.OnFoamHit(this, Find(holderId));
    }
}
