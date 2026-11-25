using HideAndSeek.Audio;
using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using HideAndSeek.Game.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace HideAndSeek.Game
{
    /// <summary>Kinds of inventory items (definitions in <see cref="ItemDefs"/>). New kinds go at the END (synced as a byte).</summary>
    public enum HotbarItem : byte { None, Medkit, Syringe, RcCar, Rod, Knife, Chainsaw, Glock, ToyGun, Phone, RcRemote, SeveredLeg, Wire,
        Fuse0, Fuse1, Fuse2, Fuse3, Fuse4, Fuse5, Fuse6, Fuse7, // fuses: one kind per mark (FuseSymbols, ItemDefs.Fuse.cs)
        FuelCan } // the seeker's chainsaw fuel canister (ItemDefs.FuelCan.cs)

    /// <summary>
    /// Requests from the hotbar to <see cref="LocalPlayerController"/> for this frame (left mouse button on the
    /// selected item does what the old keys do: G / H hold, 6). Written by <see cref="Hotbar"/> before the controller runs.
    /// </summary>
    public static class HotbarInput
    {
        public static bool HoldG, HoldH, Six;
    }

    /// <summary>
    /// Hotbar logic (local player): 5 inventory slots (<see cref="Inventory"/>), empty until something is picked up.
    /// Keys 1-5 select (the seeker takes the weapon of the slot into his hand, an empty / non-weapon slot = empty hands),
    /// Q drops one of the selected item, the left mouse button uses it. Only active in a hide-and-seek round (never in
    /// the lobby, where 1-5 pick the brick colour, nor in the race).
    /// </summary>
    public static class Hotbar
    {
        public const int Slots = Inventory.Slots;
        private static int _sel;
        private static readonly HotbarItem[] _last = new HotbarItem[Slots];
        private static HeldItem _lastHeld;
        private static float _nextEquip, _followUntil;

        // Test hooks (scripted tests drive the same code path as the keyboard / mouse).
        public static int TestSelect = -1;
        public static bool TestClick, TestHold, TestDrop;

        public static HotbarItem ItemAt(NetPlayer p, int slot) => Inventory.At(p, slot);

        /// <summary>How many of the item the player has (0 = not in the inventory).</summary>
        public static int Count(NetPlayer p, HotbarItem item) => Inventory.Count(p, item);

        public static string Name(HotbarItem item) => ItemDefs.Name(item);

        public static int SlotOf(NetPlayer p, HotbarItem item) => Inventory.SlotOf(p, item);

        /// <summary>Selected slot 0..4.</summary>
        public static int Selected(NetPlayer p) => _sel;

        /// <summary>The hotbar is shown and takes input (round running, alive or downed, not in a cut-scene).</summary>
        public static bool Active(NetPlayer p)
        {
            var mm = MatchManager.Instance;
            if (p == null || mm == null || mm.RaceMode) return false;
            var ph = mm.Phase.Value;
            if (ph != MatchPhase.Hiding && ph != MatchPhase.Seeking) return false;
            if (SceneManager.GetActiveScene().name != NetSession.GameScene) return false;
            return p.Life.Value != LifeState.Dead && !KillCam.Active && (p.IsHider || p.IsSeeker) && !DeadLobby.InWorld; // escaped: no items in the lobby
        }

        /// <summary>Hint for the selected item (what the left mouse button does now).</summary>
        public static string Hint(NetPlayer p, HotbarItem item)
        {
            var def = ItemDefs.Get(item);
            if (def == null) return "";
            if (p.IsSeeker && def.Weapon == HeldItem.None && !def.SeekerTool) return "zabrane chowającym - Q wyrzuć";
            string h = def.Hint != null ? def.Hint(p) : "";
            return def.Droppable && h.Length > 0 && h.IndexOf("Q -", System.StringComparison.Ordinal) < 0 ? h + "   |   Q - wyrzuć" : h;
        }

        /// <summary>Reads keys 1-5, Q and the left mouse button. Called every frame before the player controller.</summary>
        public static void Tick(NetPlayer p)
        {
            HotbarInput.HoldG = HotbarInput.HoldH = HotbarInput.Six = false;
            if (!Active(p)) { _sel = 0; System.Array.Clear(_last, 0, Slots); _lastHeld = HeldItem.None; TestDrop = false; return; }
            if (p.BodyCarryBusy) return; // carrying somebody / a body in the arms (no re-equipping the weapon meanwhile)
            AutoSelect(p);
            FollowHeld(p);
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null || mouse == null || LocalPlayerController.MenuOpen || p.Busy.Value || p.RcControlling || p.RcAnimating) return;
            if (p.HoldsPaMic || p.CarriesMapRadio) return; // the hand holds the public-address microphone (LMB = its talk bar) / a radio
            if (p.ExtBlocksHands) return; // fire extinguisher in both hands / foam being wiped off the face
            if (HideAndSeek.Game.MiniGames.BoardGameView.WantsCursor) return; // at the chess / tic-tac-toe table: keys and wheel belong to the board
            if (p.InputReader != null && !p.InputReader.InputEnabled) return;

            int pick = TestSelect;
            TestSelect = -1;
            for (int i = 0; i < Slots; i++)
                if (kb[Key.Digit1 + i].wasPressedThisFrame || kb[Key.Numpad1 + i].wasPressedThisFrame) pick = i;
            if (pick >= 0) Select(p, pick);

            bool drop = kb.qKey.wasPressedThisFrame || TestDrop;
            TestDrop = false;
            if (drop) Drop(p);

            bool locked = p.InputReader == null || p.InputReader.CursorLocked;
            bool click = (mouse.leftButton.wasPressedThisFrame && locked) || TestClick;
            bool hold = (mouse.leftButton.isPressed && locked) || TestHold || TestClick;
            TestClick = false;
            Use(p, ItemAt(p, _sel), click, hold);
        }

        private static void Select(NetPlayer p, int slot)
        {
            if (p.SawInHands && ItemAt(p, slot) != HotbarItem.Chainsaw)
            {
                GameHud.Toast("Piły nie schowasz - Q, żeby ją wyrzucić.", 2f);
                return;
            }
            if (_sel != slot) GameAudio.Play2D(GameSfx.UiClick, 0.35f);
            _sel = slot;
            _nextEquip = 0f;
            if (p.IsSeeker) EquipSelected(p);
        }

        /// <summary>Q: one of the selected item goes on the floor (the server checks what may be dropped).</summary>
        private static void Drop(NetPlayer p)
        {
            var item = ItemAt(p, _sel);
            var def = ItemDefs.Get(item);
            if (def == null) return;
            if (!def.Droppable) { GameHud.Toast($"{def.Name} to twoja podstawowa broń - nie da się go wyrzucić.", 2f); return; }
            if (p.Life.Value != LifeState.Alive) return;
            p.RequestDropRpc((byte)_sel);
        }

        /// <summary>Seeker: the hand follows the selected slot (weapon of the slot, else empty hands).</summary>
        private static void EquipSelected(NetPlayer p)
        {
            var mm = MatchManager.Instance;
            if (mm == null || mm.Phase.Value != MatchPhase.Seeking || p.Life.Value != LifeState.Alive || Time.time < _nextEquip) return;
            var def = ItemDefs.Get(ItemAt(p, _sel));
            var want = def != null ? def.Weapon : HeldItem.None;
            if (p.Held.Value == want) return;
            _nextEquip = Time.time + 0.5f;
            p.RequestEquipRpc(want);
        }

        /// <summary>
        /// Seeker: when the server puts a weapon in his hand (knife attack, Glock time, pickup) the selection jumps to
        /// that slot; otherwise the hand catches up with the selected slot (e.g. a weapon picked up into it).
        /// </summary>
        private static void FollowHeld(NetPlayer p)
        {
            if (!p.IsSeeker) return;
            var held = p.Held.Value;
            if (held != _lastHeld)
            {
                _lastHeld = held;
                _followUntil = held != HeldItem.None ? Time.time + 1.5f : 0f;
            }
            if (_followUntil > 0f)
            {
                // The slot layout may arrive a moment after the weapon (new pickup): wait for it, never fight the server.
                for (int i = 0; i < Slots; i++)
                {
                    var d = ItemDefs.Get(ItemAt(p, i));
                    if (d != null && d.Weapon == held) { _sel = i; _followUntil = 0f; break; }
                }
                if (Time.time < _followUntil) return;
                _followUntil = 0f;
            }
            if (!p.Busy.Value) EquipSelected(p);
        }

        /// <summary>Hider: a newly picked up item gets selected when the current slot is empty.</summary>
        private static void AutoSelect(NetPlayer p)
        {
            bool curEmpty = ItemAt(p, _sel) == HotbarItem.None;
            for (int i = 0; i < Slots; i++)
            {
                var it = ItemAt(p, i);
                if (p.IsHider && it != HotbarItem.None && _last[i] == HotbarItem.None && curEmpty) { _sel = i; curEmpty = false; }
                _last[i] = it;
            }
        }

        private static void Use(NetPlayer p, HotbarItem item, bool click, bool hold)
        {
            if (!click && !hold) return;
            var def = ItemDefs.Get(item);
            if (def == null) return; // empty slot: the left mouse button keeps its other uses (Glock, flashlight...)
            if (p.IsSeeker && !def.SeekerTool)
            {
                if (def.Weapon == HeldItem.None && click) GameHud.Toast($"{def.Name}: jako szukający tego nie użyjesz (zabrane chowającym). Q - wyrzuć.", 2.5f);
                return; // weapons: F / LPM are handled by the player controller
            }
            def.Use?.Invoke(p, click, hold);
        }
    }
}
