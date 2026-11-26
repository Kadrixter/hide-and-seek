using UnityEngine;
using HideAndSeek.Player;
using UnityEngine.InputSystem;

namespace HideAndSeek.Game.UI
{
    /// <summary>
    /// Fixed (not rebindable) pad reads for the menu / UI layer (PadCursor, PadKeyboard, PadUi): buttons pressed this
    /// frame, sticks, and direction presses with key-repeat, on the pad <see cref="PadInput"/> picked (PS4 / PS5 pads are
    /// DualShock / DualSense; the names are the PlayStation ones: Cross = south, Circle = east, Square = west,
    /// Triangle = north, Options = start). Gameplay input is not here (PadInput / KeyOrPad, rebindable).
    /// </summary>
    public static class PadUiInput
    {
        private const float RepeatDelay = 0.38f, RepeatEvery = 0.085f, StickNav = 0.6f;

        private static int _frame = -1;
        private static Vector2Int _navHeld, _dpadHeld;
        private static float _navNext, _dpadNext;
        private static Vector2Int _nav, _dpad;

        public static Gamepad Pad => PadInput.Pad != null ? PadInput.Pad : Gamepad.current;
        public static bool Connected => Pad != null;

        public static bool Cross => Pressed(Pad?.buttonSouth);
        public static bool Circle => Pressed(Pad?.buttonEast);
        public static bool Square => Pressed(Pad?.buttonWest);
        public static bool Triangle => Pressed(Pad?.buttonNorth);
        public static bool L1 => Pressed(Pad?.leftShoulder);
        public static bool R1 => Pressed(Pad?.rightShoulder);
        public static bool Options => Pressed(Pad?.startButton);
        public static bool Share => Pressed(Pad?.selectButton);
        public static bool R3 => Pressed(Pad?.rightStickButton);
        public static bool L3 => Pressed(Pad?.leftStickButton);

        public static bool CrossHeld => Held(Pad?.buttonSouth);
        public static bool SquareHeld => Held(Pad?.buttonWest);
        public static bool TriangleHeld => Held(Pad?.buttonNorth);
        public static bool L1Held => Held(Pad?.leftShoulder);
        public static bool CrossReleased => Pad != null && Pad.buttonSouth.wasReleasedThisFrame;

        public static float L2 => Pad != null ? Pad.leftTrigger.ReadValue() : 0f;
        public static float R2 => Pad != null ? Pad.rightTrigger.ReadValue() : 0f;
        public static Vector2 LeftStick => Pad != null ? Pad.leftStick.ReadValue() : Vector2.zero;
        public static Vector2 RightStick => Pad != null ? Pad.rightStick.ReadValue() : Vector2.zero;

        /// <summary>D-pad or left stick as one step per press, repeating while held (x: -1 left / +1 right, y: +1 up / -1 down).</summary>
        public static Vector2Int Nav { get { Step(); return _nav; } }

        /// <summary>D-pad only, with repeat (the left stick moves the cursor at the same time, so snapping uses this).</summary>
        public static Vector2Int Dpad { get { Step(); return _dpad; } }

        /// <summary>Any pad button / stick / trigger used this frame (the pad becomes the "last used device").</summary>
        public static bool AnyActivity
        {
            get
            {
                var p = Pad;
                if (p == null || !p.wasUpdatedThisFrame) return false;
                if (p.leftStick.ReadValue().sqrMagnitude > 0.09f || p.rightStick.ReadValue().sqrMagnitude > 0.09f) return true;
                if (p.leftTrigger.ReadValue() > 0.3f || p.rightTrigger.ReadValue() > 0.3f) return true;
                return p.buttonSouth.wasPressedThisFrame || p.buttonEast.wasPressedThisFrame || p.buttonWest.wasPressedThisFrame
                       || p.buttonNorth.wasPressedThisFrame || p.leftShoulder.wasPressedThisFrame || p.rightShoulder.wasPressedThisFrame
                       || p.startButton.wasPressedThisFrame || p.selectButton.wasPressedThisFrame || p.dpad.ReadValue().sqrMagnitude > 0.25f
                       || p.leftStickButton.wasPressedThisFrame || p.rightStickButton.wasPressedThisFrame;
            }
        }

        private static bool Pressed(UnityEngine.InputSystem.Controls.ButtonControl b) => b != null && b.wasPressedThisFrame && !PadInput.CaptureOwnsPad; // rebinding in the Pad settings: the press is a binding
        private static bool Held(UnityEngine.InputSystem.Controls.ButtonControl b) => b != null && b.isPressed;

        private static void Step()
        {
            if (_frame == Time.frameCount) return;
            _frame = Time.frameCount;
            var p = Pad;
            Vector2 d = p != null ? p.dpad.ReadValue() : Vector2.zero;
            Vector2 s = p != null ? p.leftStick.ReadValue() : Vector2.zero;
            var dpadDir = Dir(d, 0.5f);
            var stickDir = Dir(s, StickNav);
            _dpad = Repeat(dpadDir, ref _dpadHeld, ref _dpadNext);
            _nav = Repeat(dpadDir != Vector2Int.zero ? dpadDir : stickDir, ref _navHeld, ref _navNext);
        }

        private static Vector2Int Dir(Vector2 v, float threshold)
        {
            if (v.magnitude < threshold) return Vector2Int.zero;
            return Mathf.Abs(v.x) > Mathf.Abs(v.y) ? new Vector2Int(v.x > 0f ? 1 : -1, 0) : new Vector2Int(0, v.y > 0f ? 1 : -1);
        }

        private static Vector2Int Repeat(Vector2Int dir, ref Vector2Int held, ref float next)
        {
            float t = Time.unscaledTime;
            if (dir == Vector2Int.zero) { held = Vector2Int.zero; return Vector2Int.zero; }
            if (dir != held) { held = dir; next = t + RepeatDelay; return dir; }
            if (t >= next) { next = t + RepeatEvery; return dir; }
            return Vector2Int.zero;
        }
    }
}
