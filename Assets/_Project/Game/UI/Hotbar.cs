using HideAndSeek.Audio;
using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using HideAndSeek.Game.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace HideAndSeek.Game
{
    /// <summary>Kinds of items shown in the hotbar.</summary>
    public enum HotbarItem : byte { None, Medkit, Syringe, RcCar, Rod, Knife, Chainsaw, Glock }

    /// <summary>
    /// Requests from the hotbar to <see cref="LocalPlayerController"/> for this frame (left mouse button on the
    /// selected item does what the old keys do: G / H hold, 6). Written by <see cref="Hotbar"/> before the controller runs.
    /// </summary>
    public static class HotbarInput
    {
        public static bool HoldG, HoldH, Six;
    }

    /// <summary>
    /// Hotbar logic (local player): fixed slots per role, all counts come from the server-written inventory.
    /// Hider: 1 medkit, 2 adrenaline, 3 RC car, 4 rod; seeker: 1 knife, 2 chainsaw, 3 Glock.
    /// Keys 1-4 select (seeker: equips through the server, Q still cycles); left mouse button uses the selected item.
    /// Only active in a hide-and-seek round (never in the lobby, where 1-5 pick the brick colour, nor in the beta scene).
    /// </summary>
    public static class Hotbar
    {
        public const int Slots = 4;
        private static int _hiderSel;
        private static readonly int[] _lastCounts = new int[Slots];

        // Test hooks (scripted tests drive the same code path as the keyboard / mouse).
        public static int TestSelect = -1;
        public static bool TestClick, TestHold;

        public static HotbarItem ItemAt(NetPlayer p, int slot)
        {
            if (p == null) return HotbarItem.None;
            if (p.IsSeeker) return slot switch { 0 => HotbarItem.Knife, 1 => HotbarItem.Chainsaw, 2 => HotbarItem.Glock, _ => HotbarItem.None };
            return slot switch { 0 => HotbarItem.Medkit, 1 => HotbarItem.Syringe, 2 => HotbarItem.RcCar, 3 => HotbarItem.Rod, _ => HotbarItem.None };
        }

        /// <summary>How many of the item the player has (0 = empty slot).</summary>
        public static int Count(NetPlayer p, HotbarItem item)
        {
            var mm = MatchManager.Instance;
            return item switch
            {
                HotbarItem.Medkit => p.MedkitCount.Value,
                HotbarItem.Syringe => p.SyringeCount.Value,
                HotbarItem.RcCar => mm != null && mm.RcSlotOf(p) >= 0 ? 1 : 0,
                HotbarItem.Rod => p.RodCount.Value,
                HotbarItem.Knife => 1,
                HotbarItem.Chainsaw => p.HasChainsaw.Value ? 1 : 0,
                HotbarItem.Glock => p.HasGlock.Value ? 1 : 0,
                _ => 0,
            };
        }

        public static string Name(HotbarItem item) => item switch
        {
            HotbarItem.Medkit => "Apteczka",
            HotbarItem.Syringe => "Adrenalina",
            HotbarItem.RcCar => "Samochodzik RC",
            HotbarItem.Rod => "Pręt",
            HotbarItem.Knife => "Nóż",
            HotbarItem.Chainsaw => "Piła łańcuchowa",
            HotbarItem.Glock => "Glock",
            _ => "",
        };

        /// <summary>Selected slot 0..3 (seeker: follows the weapon in hand).</summary>
        public static int Selected(NetPlayer p)
        {
            if (p != null && p.IsSeeker)
                return p.Held.Value == HeldItem.Chainsaw ? 1 : p.Held.Value == HeldItem.Glock ? 2 : 0;
            return _hiderSel;
        }

        /// <summary>The hotbar is shown and takes input (round running, alive or downed, not in a cut-scene).</summary>
        public static bool Active(NetPlayer p)
        {
            var mm = MatchManager.Instance;
            if (p == null || mm == null || mm.RaceMode) return false;
            var ph = mm.Phase.Value;
            if (ph != MatchPhase.Hiding && ph != MatchPhase.Seeking) return false;
            if (SceneManager.GetActiveScene().name != NetSession.GameScene) return false;
            return p.Life.Value != LifeState.Dead && !KillCam.Active && (p.IsHider || p.IsSeeker);
        }

        /// <summary>Hint for the selected item (what the left mouse button does now).</summary>
        public static string Hint(NetPlayer p, HotbarItem item)
        {
            var lpc = LocalPlayerController.Instance;
            var prompt = lpc != null ? lpc.Prompt : PromptKind.None;
            switch (item)
            {
                case HotbarItem.Medkit:
                    if (p.Life.Value == LifeState.Downed) return "LPM przytrzymaj - podnieś się";
                    if (prompt == PromptKind.Heal) return "LPM przytrzymaj - ulecz rannego";
                    return p.Wounded.Value || p.Limping.Value ? "LPM przytrzymaj - ulecz się" : "lecz rany (LPM / G, H na rannym)";
                case HotbarItem.Syringe: return prompt == PromptKind.Resurrect ? "LPM przytrzymaj - wskrześ" : "LPM przy ciele martwego - wskrzesza";
                case HotbarItem.RcCar: return "LPM / 6 - postaw i steruj";
                case HotbarItem.Rod: return p.FreeStump != 0 ? "LPM - zrób z pręta nogę" : "proteza, gdy stracisz nogę";
                case HotbarItem.Knife: return "F - atak";
                case HotbarItem.Chainsaw: return "F przytrzymaj - gaz";
                case HotbarItem.Glock: return "LPM - strzał, R - przeładuj";
                default: return "";
            }
        }

        /// <summary>Reads keys 1-4 and the left mouse button. Called every frame before the player controller.</summary>
        public static void Tick(NetPlayer p)
        {
            HotbarInput.HoldG = HotbarInput.HoldH = HotbarInput.Six = false;
            if (!Active(p)) { _hiderSel = 0; System.Array.Clear(_lastCounts, 0, Slots); return; }
            AutoSelect(p);
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null || mouse == null || LocalPlayerController.MenuOpen || p.Busy.Value || p.RcControlling || p.RcAnimating) return;
            if (p.InputReader != null && !p.InputReader.InputEnabled) return;

            int pick = TestSelect;
            TestSelect = -1;
            for (int i = 0; i < Slots; i++)
                if (kb[Key.Digit1 + i].wasPressedThisFrame || kb[Key.Numpad1 + i].wasPressedThisFrame) pick = i;
            if (pick >= 0) Select(p, pick);

            if (!p.IsHider || ToyGunControl.Out) return; // toy slime blaster in the hands: LMB fires it (ToyGunControl)
            bool locked = p.InputReader == null || p.InputReader.CursorLocked;
            bool click = (mouse.leftButton.wasPressedThisFrame && locked) || TestClick;
            bool hold = (mouse.leftButton.isPressed && locked) || TestHold || TestClick;
            TestClick = false;
            Use(p, ItemAt(p, _hiderSel), click, hold);
        }

        private static void Select(NetPlayer p, int slot)
        {
            var item = ItemAt(p, slot);
            if (p.IsSeeker)
            {
                var mm = MatchManager.Instance;
                if (item == HotbarItem.None || mm == null || mm.Phase.Value != MatchPhase.Seeking) return;
                if (Count(p, item) == 0) { GameHud.Toast(item == HotbarItem.Chainsaw ? "Nie masz piły (leży w generatorowni, poziom -1)." : "Glock dostaniesz pod koniec rundy.", 2.5f); return; }
                var want = item == HotbarItem.Chainsaw ? HeldItem.Chainsaw : item == HotbarItem.Glock ? HeldItem.Glock : HeldItem.Knife;
                if (p.Held.Value != want) p.RequestEquipRpc(want);
                return;
            }
            if (_hiderSel != slot) GameAudio.Play2D(GameSfx.UiClick, 0.35f);
            _hiderSel = slot;
        }

        /// <summary>A newly picked up item gets selected when the current slot is empty.</summary>
        private static void AutoSelect(NetPlayer p)
        {
            if (!p.IsHider) return;
            bool curEmpty = Count(p, ItemAt(p, _hiderSel)) == 0;
            for (int i = 0; i < Slots; i++)
            {
                int c = Count(p, ItemAt(p, i));
                if (c > _lastCounts[i] && curEmpty) { _hiderSel = i; curEmpty = false; }
                _lastCounts[i] = c;
            }
        }

        private static void Use(NetPlayer p, HotbarItem item, bool click, bool hold)
        {
            if (!click && !hold) return;
            if (Count(p, item) == 0)
            {
                if (click) GameHud.Toast(item == HotbarItem.None ? "Pusty slot." : $"{Name(item)}: nie masz.", 1.5f);
                return;
            }
            var lpc = LocalPlayerController.Instance;
            var prompt = lpc != null ? lpc.Prompt : PromptKind.None;
            switch (item)
            {
                case HotbarItem.Medkit:
                    if (prompt == PromptKind.Heal && p.Life.Value == LifeState.Alive) HotbarInput.HoldH = hold;
                    else if (p.Life.Value == LifeState.Downed || p.Wounded.Value || p.Limping.Value) HotbarInput.HoldG = hold;
                    else if (click) GameHud.Toast("Nie jesteś ranny. Apteczką możesz też uleczyć rannego (spójrz na niego).", 2.5f);
                    break;
                case HotbarItem.Syringe:
                    if (prompt == PromptKind.Resurrect) HotbarInput.HoldH = hold;
                    else if (click) GameHud.Toast("Podejdź do ciała martwego chowającego i przytrzymaj LPM.", 2.5f);
                    break;
                case HotbarItem.RcCar:
                    if (click) HotbarInput.Six = true;
                    break;
                case HotbarItem.Rod:
                    if (!click || p.Life.Value != LifeState.Alive) break;
                    if (p.FreeStump == 0) GameHud.Toast("Masz obie nogi. Pręt przyda się, jeśli stracisz nogę.", 2.5f);
                    else if (!p.Motor.IsGrounded) break;
                    else p.RequestRodAttachRpc();
                    break;
            }
        }
    }
}
