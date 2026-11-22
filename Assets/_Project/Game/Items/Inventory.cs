using System;
using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// The 5-slot inventory (hotbar keys 1-5) of hiders and the seeker. The amounts live in the items' own state
    /// (medkit count, chainsaw flag, RC car slot, ...); the server keeps <see cref="NetPlayer.Inv"/> - which kind is in
    /// which slot - in step with them: a new kind goes to the first free slot, an emptied kind leaves its slot.
    /// The same kind always stacks in its one slot (up to <see cref="ItemDef.MaxStack"/>).
    /// </summary>
    public static class Inventory
    {
        public const int Slots = InvSlots.Count;
        public const string FullText = "Ekwipunek pełny (5/5). Wybierz coś i wyrzuć (Q).";

        public static int Count(NetPlayer p, HotbarItem item) => ItemDefs.Count(p, item);

        /// <summary>Item in a slot (None when empty or the amount already dropped to 0).</summary>
        public static HotbarItem At(NetPlayer p, int slot)
        {
            if (p == null || slot < 0 || slot >= Slots) return HotbarItem.None;
            var item = p.Inv.Value[slot];
            return Count(p, item) > 0 ? item : HotbarItem.None;
        }

        public static int SlotOf(NetPlayer p, HotbarItem item)
        {
            if (p == null) return -1;
            int i = p.Inv.Value.IndexOf(item);
            return i >= 0 && Count(p, item) > 0 ? i : -1;
        }

        /// <summary>Slot the item is in, or the slot it will land in when picked up now (-1 = no room).</summary>
        public static int PredictSlot(NetPlayer p, HotbarItem item)
        {
            if (p == null) return -1;
            int i = p.Inv.Value.IndexOf(item);
            if (i >= 0) return i;
            for (int s = 0; s < Slots; s++) if (At(p, s) == HotbarItem.None) return s;
            return -1;
        }

        /// <summary>Number of different kinds the player carries (= slots in use, also ones not laid out yet).</summary>
        public static int Kinds(NetPlayer p)
        {
            int n = 0;
            foreach (var d in ItemDefs.All) if (d.Count != null && d.Count(p) > 0) n++;
            return n;
        }

        /// <summary>One more of <paramref name="item"/> fits (its stack is not full, or a new kind and a slot is free).</summary>
        public static bool HasRoomFor(NetPlayer p, HotbarItem item)
        {
            var d = ItemDefs.Get(item);
            if (p == null || d == null) return false;
            int c = Count(p, item);
            return c > 0 ? c < d.MaxStack : Kinds(p) < Slots;
        }

        /// <summary>Who may pick it up: alive hiders in the round, the seeker only while seeking.</summary>
        public static bool CanPickUp(NetPlayer p, HotbarItem item, MatchPhase phase)
        {
            var d = ItemDefs.Get(item);
            if (p == null || d == null || p.Life.Value != LifeState.Alive) return false;
            if (phase != MatchPhase.Hiding && phase != MatchPhase.Seeking) return false;
            if (p.IsHider) return (d.Users & ItemUsers.Hider) != 0;
            if (p.IsSeeker) return phase == MatchPhase.Seeking && (d.Users & ItemUsers.Seeker) != 0;
            return false;
        }

        /// <summary>E prompt for an item on the floor: the normal text, or why it cannot be taken.</summary>
        public static string PickupText(NetPlayer p, HotbarItem item, string normal)
        {
            if (HasRoomFor(p, item)) return normal;
            return Count(p, item) > 0 ? "E  (masz już max)" : "E  (ekwipunek pełny - Q wyrzuć coś)";
        }

        /// <summary>
        /// Server, every frame: slots follow the amounts. A kind that ran out leaves its slot; a new kind takes the first
        /// free slot; a kind that does not fit any more (should not happen, pickups check the room) is spilled on the floor.
        /// </summary>
        public static void ServerReconcile(NetPlayer p, Action<NetPlayer, HotbarItem> spill)
        {
            var inv = p.Inv.Value;
            var before = inv;
            for (int i = 0; i < Slots; i++)
            {
                var it = inv[i];
                if (it != HotbarItem.None && (ItemDefs.Get(it) == null || Count(p, it) <= 0)) inv[i] = HotbarItem.None;
            }
            foreach (var d in ItemDefs.All)
            {
                if (d.Count == null || d.Count(p) <= 0 || inv.IndexOf(d.Item) >= 0) continue;
                int free = inv.FirstFree();
                if (free >= 0) inv[free] = d.Item;
                else spill?.Invoke(p, d.Item);
            }
            if (!inv.Equals(before)) p.Inv.Value = inv;
        }
    }
}
