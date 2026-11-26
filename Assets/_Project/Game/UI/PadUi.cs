using System;
using System.Collections.Generic;
using HideAndSeek.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace HideAndSeek.Game.UI
{
    /// <summary>
    /// Pad shortcuts of the menus (next to the <see cref="PadCursor"/>): Circle = Esc wherever a menu / panel / dialog /
    /// wheel is open (<see cref="Escape"/>), Options = Esc as the pause key (<see cref="Pause"/>), Cross = "yes" in a
    /// dialog when the cursor is not on one of its buttons, L1 / R1 = previous / next settings page
    /// (<see cref="SwitchTab"/>), right stick = slice of the emote / pose wheel (<see cref="WheelStick"/>). In the main
    /// menu (no Esc handling of its own) Circle clicks the visible "Wróć" / "Anuluj" button.
    /// </summary>
    [DefaultExecutionOrder(-900)]
    public sealed class PadUi : MonoBehaviour
    {
        /// <summary>Circle this frame (not taken by the on-screen keyboard). Callers check their own "is my UI open".</summary>
        public static bool Back => PadUiInput.Circle && !PadKeyboard.ConsumesInput;

        /// <summary>Options this frame: the pause key (opens / closes the Esc menu like Esc).</summary>
        public static bool Pause => PadUiInput.Options && !PadKeyboard.ConsumesInput;

        /// <summary>Cross this frame (not taken by the on-screen keyboard).</summary>
        public static bool Confirm => PadUiInput.Cross && !PadKeyboard.ConsumesInput;

        /// <summary>Esc on the keyboard or Circle / Options on the pad: drop-in for <c>kb.escapeKey.wasPressedThisFrame</c> in a UI that is open.</summary>
        public static bool Escape(Keyboard kb) => (kb != null && kb.escapeKey.wasPressedThisFrame) || Back || Pause;

        /// <summary>Cross pressed while the pad cursor is not over a uGUI control (dialogs: Cross off the buttons = "yes").</summary>
        public static bool ConfirmOffControls => Confirm && (!PadCursor.Active || PadCursor.SelectableUnder(PadCursor.Position) == null);

        /// <summary>
        /// Settings page switchers: a menu that shows settings pages registers one; it returns true when it switched
        /// (it is on a settings page). L1 = -1, R1 = +1. Not handled = the shoulder scrolls instead.
        /// </summary>
        public static readonly List<Func<int, bool>> TabHandlers = new();

        public static bool SwitchTab(int dir)
        {
            for (int i = TabHandlers.Count - 1; i >= 0; i--)
            {
                try
                {
                    if (TabHandlers[i] != null && TabHandlers[i](dir)) return true;
                }
                catch (Exception e) { Debug.LogException(e); }
            }
            return false;
        }

        /// <summary>
        /// Index of the next page in a page list for L1 / R1 (wraps). <paramref name="current"/> -1 = not on a settings page.
        /// </summary>
        public static int NextPage(int current, int count, int dir) => current < 0 || count <= 0 ? -1 : (current + dir + count) % count;

        // ================================================================ wheels

        /// <summary>
        /// Right stick for the radial wheels: the direction in wheel units (magnitude 0..1, y up) once pushed past the
        /// dead zone, else null (the mouse decides).
        /// </summary>
        public static Vector2? WheelStick
        {
            get
            {
                if (!PadUiInput.Connected) return null;
                var s = PadUiInput.RightStick;
                if (s.magnitude < 0.45f) s = PadUiInput.LeftStick;
                if (s.magnitude < 0.45f) return null;
                return s.normalized;
            }
        }

        /// <summary>Wheel button on the pad (rebindable "Emotki / pozy", Share by default) went down / is held.</summary>
        public static bool WheelDown => PadInput.Down(PadAction.Wheel);
        public static bool WheelHeld => PadInput.Held(PadAction.Wheel);

        // ================================================================ main menu back

        private static readonly string[] BackNames = { "Back", "Cancel" };

        private void Update()
        {
            if (CursorPolicy.Active || !Back || ConfirmDialog.ConsumesEscape) return;
            // Main menu / connecting overlay: Circle = the visible "Wróć" / "Anuluj" of the top-most canvas.
            Button best = null;
            int bestOrder = int.MinValue;
            foreach (var s in Selectable.allSelectablesArray)
            {
                if (s is not Button b || !b.isActiveAndEnabled || !b.IsInteractable() || Array.IndexOf(BackNames, b.name) < 0) continue;
                var c = b.GetComponentInParent<Canvas>();
                if (c == null || !c.isActiveAndEnabled) continue;
                int order = c.rootCanvas.sortingOrder;
                if (order > bestOrder) { bestOrder = order; best = b; }
            }
            if (best == null) return;
            Debug.Log($"[PadUi] Circle -> {best.name}");
            best.onClick.Invoke();
        }
    }
}
