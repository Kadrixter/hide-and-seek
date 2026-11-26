using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HideAndSeek.Player
{
    /// <summary>
    /// "Keyboard / mouse OR pad" reads for gameplay code: <c>KeyOrPad.Down(Key.E)</c> is true for E on the keyboard or the
    /// pad button bound to the action of that key (<see cref="ActionFor"/>: E = Interact = Square by default). WASD /
    /// arrows map to the left stick, the mouse buttons to R2 / L2, the wheel to L1 + D-pad up / down, the mouse delta
    /// to the right stick. Callers that need a different pad action in a context ask <see cref="PadInput"/> directly.
    /// </summary>
    public static class KeyOrPad
    {
        /// <summary>
        /// Set by the game: true while pad presses belong to an overlay (emote / pose wheel open or closing this frame);
        /// the pad half of every read is then off (keyboard / mouse unaffected).
        /// </summary>
        public static Func<bool> PadBlocked;

        private static bool Blocked => PadBlocked != null && PadBlocked();

        /// <summary>The pad action standing for a keyboard key (null = stick direction or no pad equivalent).</summary>
        public static PadAction? ActionFor(Key k)
        {
            switch (k)
            {
                case Key.Space: return PadAction.Jump;
                case Key.LeftCtrl: case Key.RightCtrl: case Key.C: return PadAction.Crouch;
                case Key.Z: return PadAction.Prone;
                case Key.LeftShift: case Key.RightShift: return PadAction.Sprint;
                case Key.E: return PadAction.Interact;
                case Key.F: return PadAction.ActionF;
                case Key.R: return PadAction.Reload;
                case Key.T: return PadAction.Flashlight;
                case Key.Q: return PadAction.Drop;
                case Key.G: return PadAction.Medkit;
                case Key.H: return PadAction.Heal;
                case Key.B: return PadAction.Carry;
                case Key.X: return PadAction.Dust;
                case Key.U: return PadAction.Wire;
                case Key.K: return PadAction.Wheel;
                case Key.M: return PadAction.Map;
                case Key.V: return PadAction.Voice;
                case Key.Digit6: case Key.Numpad6: return PadAction.RcRemote;
                case Key.Y: return PadAction.Yes;
                case Key.N: return PadAction.No;
                case Key.Escape: return PadAction.Pause;
                default: return null;
            }
        }

        private static PadDir? DirFor(Key k)
        {
            switch (k)
            {
                case Key.W: case Key.UpArrow: return PadDir.Up;
                case Key.S: case Key.DownArrow: return PadDir.Down;
                case Key.A: case Key.LeftArrow: return PadDir.Left;
                case Key.D: case Key.RightArrow: return PadDir.Right;
                default: return null;
            }
        }

        private static bool KbDown(Key k) { var kb = Keyboard.current; return kb != null && k != Key.None && kb[k].wasPressedThisFrame; }
        private static bool KbHeld(Key k) { var kb = Keyboard.current; return kb != null && k != Key.None && kb[k].isPressed; }
        private static bool KbUp(Key k) { var kb = Keyboard.current; return kb != null && k != Key.None && kb[k].wasReleasedThisFrame; }

        /// <summary>Key pressed this frame, or its pad equivalent.</summary>
        public static bool Down(Key k)
        {
            if (KbDown(k)) return true;
            var d = DirFor(k);
            if (Blocked) return false;
            if (d != null) return PadInput.StickDown(d.Value);
            var a = ActionFor(k);
            return a != null && PadInput.Down(a.Value);
        }

        /// <summary>Key held, or its pad equivalent.</summary>
        public static bool Held(Key k)
        {
            if (KbHeld(k)) return true;
            var d = DirFor(k);
            if (Blocked) return false;
            if (d != null) return PadInput.StickHeld(d.Value);
            var a = ActionFor(k);
            return a != null && PadInput.Held(a.Value);
        }

        /// <summary>Key released this frame, or its pad equivalent.</summary>
        public static bool Up(Key k)
        {
            if (KbUp(k)) return true;
            if (Blocked) return false;
            var a = ActionFor(k);
            return a != null && PadInput.Up(a.Value);
        }

        // ---- mouse

        public static bool LmbDown { get { var m = Mouse.current; return (m != null && m.leftButton.wasPressedThisFrame) || (!Blocked && PadInput.Down(PadAction.Attack)); } }
        public static bool LmbHeld { get { var m = Mouse.current; return (m != null && m.leftButton.isPressed) || (!Blocked && PadInput.Held(PadAction.Attack)); } }
        public static bool LmbUp { get { var m = Mouse.current; return (m != null && m.leftButton.wasReleasedThisFrame) || PadInput.Up(PadAction.Attack); } }
        public static bool RmbDown { get { var m = Mouse.current; return (m != null && m.rightButton.wasPressedThisFrame) || (!Blocked && PadInput.Down(PadAction.Aim)); } }
        public static bool RmbHeld { get { var m = Mouse.current; return (m != null && m.rightButton.isPressed) || (!Blocked && PadInput.Held(PadAction.Aim)); } }

        /// <summary>Mouse wheel (y, 120 per notch) plus one notch per pad ScrollUp / ScrollDown press (L1 + D-pad up / down).</summary>
        public static float ScrollY
        {
            get
            {
                var m = Mouse.current;
                float y = m != null ? m.scroll.ReadValue().y : 0f;
                if (PadInput.Down(PadAction.ScrollUp)) y += 120f;
                if (PadInput.Down(PadAction.ScrollDown)) y -= 120f;
                return y;
            }
        }

        /// <summary>
        /// Mouse delta in pixels plus the right stick converted to "pixels" (stick degrees / <paramref name="degPerPixel"/>,
        /// the caller's own mouse scale, so the stick turns at the pad look speed whatever the caller multiplies by).
        /// </summary>
        public static Vector2 MouseDelta(float degPerPixel = 0.11f)
        {
            var m = Mouse.current;
            Vector2 d = m != null ? m.delta.ReadValue() : Vector2.zero;
            // The menu cursor's virtual mouse already moves with the sticks: do not add them twice.
            if (degPerPixel > 1e-5f && !PadInput.IsVirtual(m)) d += PadInput.LookDegrees / degPerPixel;
            return d;
        }

        /// <summary>Digital axis from keys (-1..1) plus the matching left stick axis (analog, W/S = y, A/D = x).</summary>
        public static float Axis(Key negative, Key positive, Key negativeAlt = Key.None, Key positiveAlt = Key.None)
        {
            float v = (KbHeld(positive) || KbHeld(positiveAlt) ? 1f : 0f) - (KbHeld(negative) || KbHeld(negativeAlt) ? 1f : 0f);
            var d = DirFor(positive);
            if (d == PadDir.Up) v += PadInput.Move.y;
            else if (d == PadDir.Right) v += PadInput.Move.x;
            return Mathf.Clamp(v, -1f, 1f);
        }

        /// <summary>
        /// Vehicle input: W / S (arrows) or R2 gas / L2 brake-reverse (analog) or the left stick up / down, A / D (arrows)
        /// or the left stick to steer.
        /// </summary>
        public static void Drive(out float throttle, out float steer)
        {
            throttle = (KbHeld(Key.W) || KbHeld(Key.UpArrow) ? 1f : 0f) - (KbHeld(Key.S) || KbHeld(Key.DownArrow) ? 1f : 0f);
            steer = (KbHeld(Key.D) || KbHeld(Key.RightArrow) ? 1f : 0f) - (KbHeld(Key.A) || KbHeld(Key.LeftArrow) ? 1f : 0f);
            if (PadInput.Connected)
            {
                float t = PadInput.TriggerR - PadInput.TriggerL;
                if (Mathf.Abs(t) < 0.06f) t = 0f;
                if (t == 0f && Mathf.Abs(PadInput.Move.y) > 0.5f) t = PadInput.Move.y; // stick up / down works too
                throttle = Mathf.Clamp(throttle + t, -1f, 1f);
                steer = Mathf.Clamp(steer + PadInput.Move.x, -1f, 1f);
            }
        }
    }
}
