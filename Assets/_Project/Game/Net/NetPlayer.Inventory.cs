using Unity.Netcode;

namespace HideAndSeek.Game.Net
{
    /// <summary>Inventory of a player: slot layout (server-written) and the requests for Q (drop) and E on a dropped item.</summary>
    public partial class NetPlayer
    {
        /// <summary>Which item kind sits in each hotbar slot (1-5). See <see cref="Inventory"/>.</summary>
        public readonly NetworkVariable<InvSlots> Inv = new();

        [Rpc(SendTo.Server)]
        public void RequestDropRpc(byte slot) => MatchManager.Instance?.ServerDropSlot(this, slot);

        [Rpc(SendTo.Server)]
        public void RequestPickupDropRpc(int id) => MatchManager.Instance?.ServerPickupDrop(this, id);
    }
}
