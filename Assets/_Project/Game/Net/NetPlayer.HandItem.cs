using HideAndSeek.Characters;
using HideAndSeek.Core;
using Unity.Netcode;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// The selected inventory item in the hand, for everybody: the owner shares the selected hotbar slot
    /// (<see cref="HandSlot"/>, late joiners and spectators get it with the rest of the state), every peer looks up the
    /// item in that slot of the server's <see cref="Inv"/> and its <see cref="ItemDef.Hand"/> grip and passes it to the
    /// third-person body and (owner) the first-person arms. Seeker weapons keep their own path (Held), the toy gun its
    /// own (ToyGunOut); this covers every item with a grip. Hidden while the hands are busy or the player is not up.
    /// </summary>
    public partial class NetPlayer
    {
        /// <summary>Selected hotbar slot + 1 (0 = hotbar off), written by the owner.</summary>
        public readonly NetworkVariable<byte> HandSlot = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        /// <summary>Item in the selected slot while the hotbar is in use (None otherwise) - the same on every peer.</summary>
        public HotbarItem SelectedItem
        {
            get
            {
                int slot = HandSlot.Value - 1;
                var mm = MatchManager.Instance;
                if (slot < 0 || mm == null || mm.RaceMode || Life.Value != LifeState.Alive || !(IsHider || IsSeeker)) return HotbarItem.None;
                var ph = mm.Phase.Value;
                if (ph != MatchPhase.Hiding && ph != MatchPhase.Seeking) return HotbarItem.None;
                return Inventory.At(this, slot);
            }
        }

        /// <summary>Item shown in the hand by the generic grip right now (None = nothing / busy / own view).</summary>
        public HotbarItem HandItem { get; private set; }

        /// <summary>Called from Update on every peer.</summary>
        private void UpdateHandItem()
        {
            if (IsOwner)
            {
                byte want = Hotbar.Active(this) ? (byte)(Hotbar.Selected(this) + 1) : (byte)0;
                if (HandSlot.Value != want) HandSlot.Value = want;
            }

            var item = SelectedItem;
            var def = item != HotbarItem.None ? ItemDefs.Get(item) : null;
            HandGrip grip = def?.Hand;
            // Weapons (and anything the seeker draws through Held) have their own hold; busy hands put the item away.
            if (def != null && def.Weapon != HeldItem.None) grip = null;
            if (Busy.Value || Seat.Value >= 0 || RcAnimating || EmoteHandsBusy) grip = null;
            HandItem = grip != null ? item : HotbarItem.None;
            int key = grip != null ? (int)item : 0;
            (Actions as BlockyCharacterActions)?.SetCarried(key, grip);
            if (IsOwner) (ViewModel as FirstPersonViewModel)?.SetCarried(key, grip);
        }
    }
}
