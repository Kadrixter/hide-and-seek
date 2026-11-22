using System;
using System.Collections.Generic;
using HideAndSeek.Audio;
using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>Who may pick an item up (from the map or after somebody dropped it).</summary>
    [Flags]
    public enum ItemUsers : byte { None = 0, Hider = 1, Seeker = 2, Both = Hider | Seeker }

    /// <summary>
    /// One kind of inventory item: name, who can carry it, stack size, how its amount is read from the player's state,
    /// how the server gives / takes one, what the left mouse button does and what it looks like when dropped.
    /// <para>
    /// Adding a new item (e.g. a toy gun or a phone):
    /// 1. add a value at the END of <see cref="HotbarItem"/>;
    /// 2. keep its state on the player as before (e.g. a <c>NetworkVariable&lt;byte&gt;</c> in a NetPlayer partial);
    /// 3. register an <see cref="ItemDef"/> (see <c>ItemDefs.Builtin.cs</c>): <see cref="Count"/>, <see cref="Give"/>,
    ///    <see cref="Take"/> on that state, plus <see cref="Use"/> / <see cref="Hint"/> / <see cref="Model"/>;
    /// 4. draw an icon in <see cref="HotbarIcons"/>;
    /// 5. before the server adds one to a player (pickup), check <see cref="Inventory.HasRoomFor"/>.
    /// The slot layout, Q (drop), E on dropped items, the hotbar and the minimap then work on their own.
    /// </para>
    /// </summary>
    public sealed class ItemDef
    {
        public delegate bool TakeFn(NetPlayer p, out byte extra);

        public HotbarItem Item;
        public string Name;
        public ItemUsers Users = ItemUsers.Hider;
        /// <summary>How many fit in one slot (the same kind always stacks in one slot).</summary>
        public int MaxStack = 1;
        /// <summary>Q throws it on the floor (the seeker's knife cannot be dropped).</summary>
        public bool Droppable = true;
        /// <summary>Seeker weapon taken into the hand when the slot is selected (None = empty hands).</summary>
        public HeldItem Weapon = HeldItem.None;
        /// <summary>Sound when a dropped one hits the floor.</summary>
        public GameSfx DropSfx = GameSfx.RcPlace;
        public float DropVolume = 0.7f;
        /// <summary>Faint light on a dropped one so it can be found in the dark garage (alpha 0 = none).</summary>
        public Color Glow = new Color(1f, 1f, 1f, 0f);
        /// <summary>Shown on the seeker's minimap when lying on the floor (seeker weapons).</summary>
        public bool SeekerMapMarker;

        /// <summary>Amount the player has (0 = not in the inventory). Read on every peer.</summary>
        public Func<NetPlayer, int> Count = _ => 0;
        /// <summary>Server: one more for the player (extra = payload kept by a dropped item). False = refused.</summary>
        public Func<NetPlayer, byte, bool> Give;
        /// <summary>Server: one less (for a drop). False = cannot drop it now (nothing is spawned).</summary>
        public TakeFn Take;
        /// <summary>Client: what the left mouse button does with the selected slot (shown above the hotbar).</summary>
        public Func<NetPlayer, string> Hint = _ => "";
        /// <summary>Client: left mouse button on the selected slot (click = pressed this frame, hold = held down).</summary>
        public Action<NetPlayer, bool, bool> Use;
        /// <summary>World model of a dropped one, built under the given root (any pivot: it is centred automatically).</summary>
        public Func<Transform, GameObject> Model;
    }

    /// <summary>Registry of all inventory items (built-in ones in <c>ItemDefs.Builtin.cs</c>).</summary>
    public static partial class ItemDefs
    {
        private static readonly Dictionary<HotbarItem, ItemDef> Map = new();
        private static readonly List<ItemDef> List = new();
        private static bool _init;

        /// <summary>All items in registration order (a new pickup of several kinds at once fills slots in this order).</summary>
        public static IReadOnlyList<ItemDef> All
        {
            get
            {
                EnsureBuiltins();
                return List;
            }
        }

        public static ItemDef Get(HotbarItem item)
        {
            EnsureBuiltins();
            return Map.TryGetValue(item, out var d) ? d : null;
        }

        /// <summary>Adds or replaces an item definition (call before the round starts, e.g. from a static constructor).</summary>
        public static void Register(ItemDef def)
        {
            EnsureBuiltins();
            if (def == null || def.Item == HotbarItem.None) return;
            if (Map.TryGetValue(def.Item, out var old)) List[List.IndexOf(old)] = def;
            else List.Add(def);
            Map[def.Item] = def;
        }

        public static int Count(NetPlayer p, HotbarItem item)
        {
            if (p == null || item == HotbarItem.None) return 0;
            var d = Get(item);
            return d != null && d.Count != null ? Mathf.Max(0, d.Count(p)) : 0;
        }

        public static string Name(HotbarItem item) => Get(item)?.Name ?? "";

        private static void EnsureBuiltins()
        {
            if (_init) return;
            _init = true;
            foreach (var d in Builtins())
            {
                Map[d.Item] = d;
                List.Add(d);
            }
        }

        private static void Toast(string text, float seconds = 2.5f) => GameHud.Toast(text, seconds);
    }
}
