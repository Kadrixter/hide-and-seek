using HideAndSeek.Characters;
using Unity.Netcode;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Metal rod (peg leg) state of a player: rods carried in the inventory and legs already fitted with one,
    /// both written by the server. The fitting animation is started on every client by <see cref="RodAttachRpc"/>.
    /// </summary>
    public partial class NetPlayer
    {
        /// <summary>Rods in the inventory (picked up next to shelving / fences).</summary>
        public readonly NetworkVariable<byte> RodCount = new();
        /// <summary>Legs (Limb bits) replaced by a rod.</summary>
        public readonly NetworkVariable<byte> RodLegs = new();

        /// <summary>A cut-off leg without a rod (Limb.LegR / LegL), 0 when none.</summary>
        public int FreeStump
        {
            get
            {
                int free = Severed.Value & Limb.Legs & ~RodLegs.Value;
                return (free & Limb.LegR) != 0 ? Limb.LegR : (free & Limb.LegL) != 0 ? Limb.LegL : 0;
            }
        }

        [Rpc(SendTo.Server)]
        public void RequestPickupRodRpc(int id) => MatchManager.Instance?.ServerPickupRod(this, id);

        [Rpc(SendTo.Server)]
        public void RequestRodAttachRpc() => MatchManager.Instance?.ServerRodAttach(this);

        /// <summary>Everyone: this player sits down and fits a rod onto the stump of <paramref name="limb"/>.</summary>
        [Rpc(SendTo.Everyone)]
        public void RodAttachRpc(byte limb)
        {
            (Actions as BlockyCharacterActions)?.PlayRodAttach(limb);
            GetComponent<RodLegDriver>()?.OnAttachStarted();
        }

        /// <summary>Everyone: the fitting was interrupted (hit / downed); the rod stays in the inventory.</summary>
        [Rpc(SendTo.Everyone)]
        public void RodAttachCancelRpc() => (Actions as BlockyCharacterActions)?.CancelRodAttach();
    }
}
