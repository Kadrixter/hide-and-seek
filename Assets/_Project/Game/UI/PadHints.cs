using System.Text.RegularExpressions;
using HideAndSeek.Game.Net;
using HideAndSeek.Player;
using UnityEngine;

namespace HideAndSeek.Game.UI
{
    /// <summary>
    /// Key names in on-screen texts become pad buttons while the pad is the last used device
    /// (<see cref="PadInput.UsingPad"/>): world prompts ("E  wsiądź" -> "□  wsiądź"), the key hints panel, the hotbar hint,
    /// toasts ("(E)" -> "(□)") and the board-game hints (their own D-pad layout, <see cref="Board"/>).
    /// Also routes the pad settings to the game's prefs file.
    /// </summary>
    public static class PadHints
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void HookPrefs()
        {
            PadInput.PrefGet = (k, d) => Prefs.GetString(k, d);
            PadInput.PrefSet = (k, v) => Prefs.SetString(k, v);
            PadInput.ReloadSettings();
            // Cross picks a wheel slice: no jump / interact on the same press (open, or closed this / last frame).
            System.Func<bool> wheel = () => EmoteWheel.ConsumesEscape || PoseWheel.ConsumesEscape;
            PlayerInputReader.PadGameplayBlocked = wheel;
            KeyOrPad.PadBlocked = wheel;
        }

        public static bool Active => PadInput.UsingPad && PadInput.Connected;

        /// <summary>Pad label for a keyboard key name as written in the game's texts (null = keep the text).</summary>
        public static string KeyLabel(string key)
        {
            switch (key)
            {
                case "E": return PadInput.Label(PadAction.Interact);
                case "F": return PadInput.Label(PadAction.ActionF);
                case "R": return PadInput.Label(PadAction.Reload);
                case "T": return PadInput.Label(PadAction.Flashlight);
                case "Q": return PadInput.Label(PadAction.Drop);
                case "G": return PadInput.Label(PadAction.Medkit);
                case "H": return PadInput.Label(PadAction.Heal);
                case "B": return PadInput.Label(PadAction.Carry);
                case "X": return PadInput.Label(PadAction.Dust);
                case "U": return PadInput.Label(PadAction.Wire);
                case "K": return PadInput.Label(PadAction.Wheel);
                case "M": return PadInput.Label(PadAction.Map);
                case "V": return PadInput.Label(PadAction.Voice);
                case "Z": return PadInput.Label(PadAction.Prone);
                case "Y": return PadInput.Label(PadAction.Yes);
                case "N": return PadInput.Label(PadAction.No);
                case "6": return PadInput.Label(PadAction.RcRemote);
                case "C": case "Ctrl": case "Ctrl / C": return PadInput.Label(PadAction.Crouch);
                case "Spacja": return PadInput.Label(PadAction.Jump);
                case "Shift": return PadInput.Label(PadAction.Sprint);
                case "Esc": case "ESC": return PadInput.Label(PadAction.Pause);
                case "LPM": return PadInput.Label(PadAction.Attack);
                case "PPM": return PadInput.Label(PadAction.Aim);
                case "LPM / PPM": return PadInput.Label(PadAction.Attack) + " / " + PadInput.Label(PadAction.Aim);
                case "LPM / F": return PadInput.Label(PadAction.Attack) + " / " + PadInput.Label(PadAction.ActionF);
                case "E / Spacja": return PadInput.Label(PadAction.Interact) + " / " + PadInput.Label(PadAction.Jump);
                case "Kółko": case "kółko myszy": return PadInput.Label(PadAction.ScrollUp) + "/" + PadInput.ButtonLabel(PadButton.DpadDown);
                case "1-5": return PadInput.Label(PadAction.SlotPrev) + " " + PadInput.Label(PadAction.SlotNext);
                case "WASD": return "lewa gałka";
                default: return null;
            }
        }

        /// <summary>Key hints panel: the key cap text.</summary>
        public static string Cap(string key) => Active ? KeyLabel(key) ?? key : key;

        /// <summary>World prompt "E  wsiądź": the key before the double space.</summary>
        public static string Prompt(string text)
        {
            if (!Active || string.IsNullOrEmpty(text)) return text;
            int gap = text.IndexOf("  ", System.StringComparison.Ordinal);
            if (gap <= 0) return Line(text);
            string pad = KeyLabel(text.Substring(0, gap));
            return (pad ?? text.Substring(0, gap)) + Line(text.Substring(gap));
        }

        /// <summary>The key token of a prompt ("E" / "□" / "L1+✕"), for the key box.</summary>
        public static string PromptKey(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            int gap = text.IndexOf("  ", System.StringComparison.Ordinal);
            if (gap <= 0) return text.Substring(0, 1);
            return text.Substring(0, gap);
        }

        private const string Keys = "LPM / PPM|LPM|PPM|Spacja|Shift|Ctrl|Esc|ESC|[EFRTQGHBXUKMVZYNC6]";

        // "(E)" or "Q - wyrzuć" / "LPM: połóż" / "E / Spacja" (a key followed by a dash, colon or slash).
        private static readonly Regex KeyToken = new Regex(
            @"(?<=\()(" + Keys + @")(?=\))|(?<=^|[\s|/])(" + Keys + @")(?=\s?[-–:]\s|\s/\s)", RegexOptions.Compiled);

        /// <summary>Any text: "Q - wyrzuć", "(E)", "LPM: połóż" with pad buttons instead.</summary>
        public static string Line(string text)
        {
            if (!Active || string.IsNullOrEmpty(text)) return text;
            return KeyToken.Replace(text, m => KeyLabel(m.Value) ?? m.Value);
        }

        private static readonly Regex BoardToken = new Regex(
            @"(?<=^|\s)(LPM przytrzymaj i odciągnij|LPM|PPM|Esc|kółko myszy|Spacja|[RCGB])(?=\s*[-–:]|\s+przytrzymaj)", RegexOptions.Compiled);

        /// <summary>Board games (chess, checkers, tic-tac-toe, pool): the menu cursor owns Cross, so the keys there differ.</summary>
        public static string Board(string text)
        {
            if (!Active || string.IsNullOrEmpty(text)) return text;
            return BoardToken.Replace(text, m =>
            {
                switch (m.Value)
                {
                    case "LPM przytrzymaj i odciągnij": return PadInput.Label(PadAction.Attack) + " przytrzymaj, prawa gałka w dół";
                    case "LPM": return PadInput.ButtonLabel(PadButton.Cross);
                    case "PPM": return PadInput.Label(PadAction.Aim);
                    case "Esc": return PadInput.ButtonLabel(PadButton.Circle);
                    case "kółko myszy": return "L2/R2";
                    case "Spacja": return PadInput.ButtonLabel(PadButton.DpadUp);
                    case "R": return PadInput.ButtonLabel(PadButton.DpadUp);
                    case "C": return PadInput.ButtonLabel(PadButton.DpadRight);
                    case "G": return PadInput.ButtonLabel(PadButton.DpadDown);
                    case "B": return PadInput.ButtonLabel(PadButton.DpadLeft);
                    default: return m.Value;
                }
            });
        }
    }
}
