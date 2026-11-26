using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;

namespace HideAndSeek.Player
{
    public enum PadKind { None, DualShock4, DualSense, Other }

    /// <summary>Physical pad buttons in PlayStation naming (a generic / XInput pad maps by position: Cross = A...).</summary>
    public enum PadButton : byte
    {
        None, Cross, Circle, Square, Triangle, L1, R1, L2, R2, L3, R3,
        DpadUp, DpadDown, DpadLeft, DpadRight, Options, Share, Touchpad, Home,
    }

    /// <summary>
    /// Rebindable gameplay actions; each one stands for one keyboard / mouse function of the game (key in brackets).
    /// Order = order in the settings list. Never reorder: tests and saved prefs use the names, the list uses the order.
    /// </summary>
    public enum PadAction
    {
        Jump,       // Space (also handbrake, stand up, carried: ask to be put down)
        Crouch,     // Ctrl / C (race: bumper, chess: clock)
        Prone,      // Z (bricks: undo)
        Sprint,     // Shift (RC car / race: boost, pool: fine aim)
        Interact,   // E (every "E ..." prompt, hold-E actions, kart exit, pool: rotate)
        ActionF,    // F (chainsaw throttle, stomp, crate saw, dead lobby TV)
        Reload,     // R (reload, refuel, kart / race reset, radio panel, RC charge, rematch)
        Flashlight, // T (also RC car lights)
        Drop,       // Q (drop item, put down extinguisher / radio, pool: rotate)
        SlotPrev,   // 1-5 (hotbar / brick colour: previous)
        SlotNext,   // 1-5 (hotbar / brick colour: next)
        Attack,     // LMB (use item, shoot, spray, talk bar, cycle spectated player)
        Aim,        // RMB (glock aim, cancel, cycle back)
        Medkit,     // G (hold)
        Heal,       // H (hold: help / heal / resurrect; vehicles: horn)
        Carry,      // B (body carry, pool: hand mode)
        Dust,       // X
        Wire,       // U (RC antenna swap)
        Wheel,      // K (emote / pose wheel)
        Map,        // M (minimap scale)
        Voice,      // V (push to talk)
        Camera,     // V in the kart (first / third person)
        RcRemote,   // 6 (RC car)
        ScrollUp,   // mouse wheel up (flashlight focus, zoom, wheel pages)
        ScrollDown, // mouse wheel down
        Yes,        // Y (consent prompts)
        No,         // N
        Pause,      // Esc (menu)
    }

    /// <summary>One pad binding: a button, optionally with L1 held (the L1 layer). L1 itself is the layer modifier.</summary>
    [Serializable]
    public struct PadBinding : IEquatable<PadBinding>
    {
        public PadButton Button;
        public bool L1;

        public PadBinding(PadButton button, bool l1 = false) { Button = button; L1 = l1; }

        public bool Equals(PadBinding o) => Button == o.Button && L1 == o.L1;
        public override bool Equals(object obj) => obj is PadBinding b && Equals(b);
        public override int GetHashCode() => ((int)Button << 1) | (L1 ? 1 : 0);
        public override string ToString() => (L1 ? "L1+" : "") + Button;

        public static bool TryParse(string s, out PadBinding b)
        {
            b = default;
            if (string.IsNullOrEmpty(s)) return false;
            bool l1 = s.StartsWith("L1+", StringComparison.Ordinal);
            if (l1) s = s.Substring(3);
            if (!Enum.TryParse(s, out PadButton btn) || btn == PadButton.None) return false;
            b = new PadBinding(btn, l1);
            return true;
        }
    }

    public enum PadDir { Up, Down, Left, Right }

    /// <summary>
    /// Game pad facade (DualShock 4 / DualSense over USB or Bluetooth, any other <see cref="Gamepad"/> such as an XInput
    /// pad from DS4Windows). Polled once per frame by <see cref="PadInputRunner"/> (execution order -10000), so
    /// <see cref="Down"/> / <see cref="Up"/> are stable for the whole frame. Gameplay reads actions
    /// (<see cref="PadAction"/>, rebindable, saved in prefs "hns.pad.bind.&lt;Action&gt;"); menus read the fixed helpers
    /// (<see cref="Confirm"/> = Cross, <see cref="Cancel"/> = Circle, <see cref="NavUp"/>... with auto-repeat).
    /// Keyboard + mouse keep working at the same time (see <see cref="KeyOrPad"/>).
    /// </summary>
    public static class PadInput
    {
        // ================================================================ persistence hooks

        /// <summary>Pref storage (the game routes these to its Prefs file; default PlayerPrefs).</summary>
        public static Func<string, string, string> PrefGet = (k, d) => PlayerPrefs.GetString(k, d);
        public static Action<string, string> PrefSet = (k, v) => PlayerPrefs.SetString(k, v);

        public const string PrefPrefix = "hns.pad.";

        // ================================================================ settings

        private static bool _loaded;
        private static float _deadzone = 0.15f, _sensX = 1f, _sensY = 1f;
        private static bool _invertY, _rumbleOn = true;

        /// <summary>Radial stick dead zone (0.02..0.45).</summary>
        public static float Deadzone { get { Load(); return _deadzone; } set { Load(); _deadzone = Mathf.Clamp(value, 0.02f, 0.45f); Save("deadzone", _deadzone); } }
        /// <summary>Look speed multiplier, horizontal (0.2..3).</summary>
        public static float SensX { get { Load(); return _sensX; } set { Load(); _sensX = Mathf.Clamp(value, 0.2f, 3f); Save("sensX", _sensX); } }
        /// <summary>Look speed multiplier, vertical (0.2..3).</summary>
        public static float SensY { get { Load(); return _sensY; } set { Load(); _sensY = Mathf.Clamp(value, 0.2f, 3f); Save("sensY", _sensY); } }
        public static bool InvertY { get { Load(); return _invertY; } set { Load(); _invertY = value; PrefSet(PrefPrefix + "invertY", value ? "1" : "0"); } }
        public static bool RumbleEnabled { get { Load(); return _rumbleOn; } set { Load(); _rumbleOn = value; PrefSet(PrefPrefix + "rumble", value ? "1" : "0"); if (!value) StopRumble(); } }

        /// <summary>Degrees per second at full stick deflection (before the sensitivity multipliers).</summary>
        public const float BaseYawSpeed = 210f, BasePitchSpeed = 150f;

        // ================================================================ bindings

        public static readonly int ActionCount = Enum.GetValues(typeof(PadAction)).Length;
        private static readonly PadBinding[] _bind = new PadBinding[Enum.GetValues(typeof(PadAction)).Length];

        /// <summary>Default PlayStation layout. Every action has a unique button (or L1 + button).</summary>
        public static PadBinding Default(PadAction a)
        {
            switch (a)
            {
                case PadAction.Jump: return new PadBinding(PadButton.Cross);
                case PadAction.Crouch: return new PadBinding(PadButton.Circle);
                case PadAction.Interact: return new PadBinding(PadButton.Square);
                case PadAction.Reload: return new PadBinding(PadButton.Triangle);
                case PadAction.Attack: return new PadBinding(PadButton.R2);
                case PadAction.Aim: return new PadBinding(PadButton.L2);
                case PadAction.ActionF: return new PadBinding(PadButton.R1);
                case PadAction.Sprint: return new PadBinding(PadButton.L3);
                case PadAction.Voice: return new PadBinding(PadButton.R3);
                case PadAction.Flashlight: return new PadBinding(PadButton.DpadUp);
                case PadAction.Drop: return new PadBinding(PadButton.DpadDown);
                case PadAction.SlotPrev: return new PadBinding(PadButton.DpadLeft);
                case PadAction.SlotNext: return new PadBinding(PadButton.DpadRight);
                case PadAction.Pause: return new PadBinding(PadButton.Options);
                case PadAction.Wheel: return new PadBinding(PadButton.Share);
                case PadAction.Map: return new PadBinding(PadButton.Touchpad);
                case PadAction.Carry: return new PadBinding(PadButton.Cross, true);
                case PadAction.Prone: return new PadBinding(PadButton.Circle, true);
                case PadAction.Heal: return new PadBinding(PadButton.Square, true);
                case PadAction.Medkit: return new PadBinding(PadButton.Triangle, true);
                case PadAction.Dust: return new PadBinding(PadButton.R1, true);
                case PadAction.Wire: return new PadBinding(PadButton.R2, true);
                case PadAction.Camera: return new PadBinding(PadButton.L2, true);
                case PadAction.ScrollUp: return new PadBinding(PadButton.DpadUp, true);
                case PadAction.ScrollDown: return new PadBinding(PadButton.DpadDown, true);
                case PadAction.No: return new PadBinding(PadButton.DpadLeft, true);
                case PadAction.Yes: return new PadBinding(PadButton.DpadRight, true);
                case PadAction.RcRemote: return new PadBinding(PadButton.Share, true);
                default: return default;
            }
        }

        public static PadBinding GetBinding(PadAction a) { Load(); return _bind[(int)a]; }

        /// <summary>
        /// Sets a binding; an action that already had it gets this action's old binding (swap). L1 alone is the layer
        /// modifier and cannot be bound. Returns the action that was swapped (or null).
        /// </summary>
        public static PadAction? SetBinding(PadAction a, PadBinding b)
        {
            Load();
            if (b.Button == PadButton.None || b.Button == PadButton.L1) return null;
            PadAction? swapped = null;
            var old = _bind[(int)a];
            for (int i = 0; i < ActionCount; i++)
            {
                if (i == (int)a || !_bind[i].Equals(b)) continue;
                _bind[i] = old;
                SaveBinding((PadAction)i);
                swapped = (PadAction)i;
            }
            _bind[(int)a] = b;
            SaveBinding(a);
            return swapped;
        }

        public static void ResetBindings()
        {
            Load();
            for (int i = 0; i < ActionCount; i++) { _bind[i] = Default((PadAction)i); SaveBinding((PadAction)i); }
        }

        /// <summary>Re-reads settings and bindings from the prefs (after the pref hooks changed).</summary>
        public static void ReloadSettings() { _loaded = false; Load(); }

        private static void SaveBinding(PadAction a) => PrefSet(PrefPrefix + "bind." + a, _bind[(int)a].ToString());
        private static void Save(string key, float v) => PrefSet(PrefPrefix + key, v.ToString("R", System.Globalization.CultureInfo.InvariantCulture));

        private static float LoadF(string key, float def)
        {
            string s = PrefGet(PrefPrefix + key, "");
            return float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float f) ? f : def;
        }

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            try
            {
                _deadzone = Mathf.Clamp(LoadF("deadzone", 0.15f), 0.02f, 0.45f);
                _sensX = Mathf.Clamp(LoadF("sensX", 1f), 0.2f, 3f);
                _sensY = Mathf.Clamp(LoadF("sensY", 1f), 0.2f, 3f);
                _invertY = PrefGet(PrefPrefix + "invertY", "0") == "1";
                _rumbleOn = PrefGet(PrefPrefix + "rumble", "1") != "0";
                for (int i = 0; i < ActionCount; i++)
                {
                    var a = (PadAction)i;
                    _bind[i] = PadBinding.TryParse(PrefGet(PrefPrefix + "bind." + a, ""), out var b) && b.Button != PadButton.L1 ? b : Default(a);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Pad] settings read failed: {e.Message}");
                for (int i = 0; i < ActionCount; i++) _bind[i] = Default((PadAction)i);
            }
        }

        // ================================================================ device

        /// <summary>Tests: use this pad instead of <see cref="Gamepad.current"/>.</summary>
        public static Gamepad ForcedPad;

        /// <summary>The pad read this frame (null = none connected).</summary>
        public static Gamepad Pad { get; private set; }
        public static bool Connected => Pad != null;
        public static PadKind Kind { get; private set; }
        /// <summary>Display name of the pad ("DualSense", "DualShock 4", product name...).</summary>
        public static string Name { get; private set; } = "";
        public static bool HasTouchpad => Pad is DualShockGamepad;

        /// <summary>True while the pad was the last device used (prompts show pad buttons).</summary>
        public static bool UsingPad { get; private set; }
        public static event Action<bool> UsingPadChanged;

        // ================================================================ per frame state

        private const int BtnCount = 19; // PadButton values
        private const int DirBase = BtnCount; // left stick directions (4) after the buttons
        private static readonly bool[] _now = new bool[BtnCount + 4], _prev = new bool[BtnCount + 4], _blocked = new bool[BtnCount + 4];
        private static Vector2 _ls, _rs, _move, _lookDeg;
        private static float _lt, _rt, _accelT;
        private static int _frame = -1;
        private static readonly float[] _navNext = new float[4];
        private static readonly bool[] _nav = new bool[4];

        /// <summary>Left stick after the dead zone (magnitude 0..1). Use for walking.</summary>
        public static Vector2 Move => _move;
        /// <summary>Raw sticks (no dead zone).</summary>
        public static Vector2 LeftStickRaw => _ls;
        public static Vector2 RightStickRaw => _rs;
        /// <summary>Right stick after dead zone and response curve (magnitude 0..1, no sensitivity).</summary>
        public static Vector2 Look { get; private set; }
        /// <summary>Look rotation for this frame in degrees (x = yaw, y = pitch up positive): sensitivity, invert Y, acceleration.</summary>
        public static Vector2 LookDegrees => _lookDeg;
        /// <summary>Analog triggers 0..1.</summary>
        public static float TriggerL => _lt;
        public static float TriggerR => _rt;

        /// <summary>Called by <see cref="PadInputRunner"/> first thing every frame.</summary>
        public static void Tick()
        {
            if (_frame == Time.frameCount) return;
            _frame = Time.frameCount;
            Load();
            Array.Copy(_now, _prev, _now.Length);

            var pad = ForcedPad != null && ForcedPad.added ? ForcedPad : Gamepad.current;
            if (pad != null && !pad.added) pad = null;
            if (pad != Pad) OnPadChanged(pad);

            if (pad == null)
            {
                Array.Clear(_now, 0, _now.Length);
                _ls = _rs = _move = _lookDeg = Vector2.zero;
                Look = Vector2.zero;
                _lt = _rt = 0f;
            }
            else
            {
                _ls = pad.leftStick.ReadValue();
                _rs = pad.rightStick.ReadValue();
                _lt = pad.leftTrigger.ReadValue();
                _rt = pad.rightTrigger.ReadValue();
                _now[(int)PadButton.Cross] = pad.buttonSouth.isPressed;
                _now[(int)PadButton.Circle] = pad.buttonEast.isPressed;
                _now[(int)PadButton.Square] = pad.buttonWest.isPressed;
                _now[(int)PadButton.Triangle] = pad.buttonNorth.isPressed;
                _now[(int)PadButton.L1] = pad.leftShoulder.isPressed;
                _now[(int)PadButton.R1] = pad.rightShoulder.isPressed;
                _now[(int)PadButton.L2] = _lt > (_prev[(int)PadButton.L2] ? 0.25f : 0.4f);
                _now[(int)PadButton.R2] = _rt > (_prev[(int)PadButton.R2] ? 0.25f : 0.4f);
                _now[(int)PadButton.L3] = pad.leftStickButton.isPressed;
                _now[(int)PadButton.R3] = pad.rightStickButton.isPressed;
                _now[(int)PadButton.DpadUp] = pad.dpad.up.isPressed;
                _now[(int)PadButton.DpadDown] = pad.dpad.down.isPressed;
                _now[(int)PadButton.DpadLeft] = pad.dpad.left.isPressed;
                _now[(int)PadButton.DpadRight] = pad.dpad.right.isPressed;
                _now[(int)PadButton.Options] = pad.startButton.isPressed;
                _now[(int)PadButton.Share] = pad.selectButton.isPressed;
                var ds = pad as DualShockGamepad;
                _now[(int)PadButton.Touchpad] = ds != null && ds.touchpadButton != null && ds.touchpadButton.isPressed;
                _now[(int)PadButton.Home] = HomeButton(pad);

                // Left stick as four digital directions (hysteresis): arcade steering, menus.
                Dir(PadDir.Up, _ls.y); Dir(PadDir.Down, -_ls.y); Dir(PadDir.Left, -_ls.x); Dir(PadDir.Right, _ls.x);

                float dz = _deadzone;
                _move = Radial(_ls, dz, 1f);
                Look = Radial(_rs, dz, 1.7f);
                float dt = Time.unscaledDeltaTime;
                // Acceleration: held near the edge sideways for a moment -> turn up to 1.8x faster (quick 180s).
                if (Mathf.Abs(Look.x) > 0.9f) _accelT += dt; else _accelT = 0f;
                float accel = 1f + 0.8f * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((_accelT - 0.3f) / 0.5f));
                var deg = new Vector2(Look.x * BaseYawSpeed * _sensX * accel, Look.y * BasePitchSpeed * _sensY);
                if (_invertY) deg.y = -deg.y;
                _lookDeg = deg * Mathf.Min(dt, 0.1f);
            }

            // Blocked buttons (BlockUntilRelease) unblock once released.
            for (int i = 0; i < _blocked.Length; i++) if (_blocked[i] && !_now[i]) _blocked[i] = false;

            TickNav();
            TickUsing(pad);
            TickRumble();
            TickCapture();
        }

        private static bool HomeButton(Gamepad pad)
        {
            if (pad is DualShock4GamepadHID d4) return d4.playStationButton != null && d4.playStationButton.isPressed;
            if (pad is DualSenseGamepadHID d5) return d5.playStationButton != null && d5.playStationButton.isPressed;
            return false;
        }

        private static void Dir(PadDir d, float v)
        {
            int i = DirBase + (int)d;
            _now[i] = v > (_prev[i] ? 0.35f : 0.6f);
        }

        private static Vector2 Radial(Vector2 v, float dz, float curve)
        {
            float m = v.magnitude;
            if (m <= dz) return Vector2.zero;
            float n = Mathf.Clamp01((m - dz) / (1f - dz - 0.02f));
            return v / m * Mathf.Pow(n, curve);
        }

        private static void OnPadChanged(Gamepad pad)
        {
            StopRumble();
            Pad = pad;
            Array.Clear(_now, 0, _now.Length);
            Array.Clear(_blocked, 0, _blocked.Length);
            if (pad == null) { Kind = PadKind.None; Name = ""; SetUsing(false); return; }
            Kind = pad is DualSenseGamepadHID ? PadKind.DualSense
                : pad is DualShockGamepad ? PadKind.DualShock4
                : PadKind.Other;
            string product = pad.description.product;
            Name = Kind == PadKind.DualSense ? "DualSense (PS5)"
                : Kind == PadKind.DualShock4 ? "DualShock 4 (PS4)"
                : !string.IsNullOrEmpty(product) ? product : pad.displayName;
            Debug.Log($"[Pad] using {Name} ({pad.layout}, {pad.name})");
        }

        // ---- last used device
        private static void TickUsing(Gamepad pad)
        {
            bool padUsed = false;
            if (pad != null)
            {
                for (int i = 1; i < BtnCount; i++) if (_now[i] && !_prev[i]) { padUsed = true; break; }
                if (_ls.sqrMagnitude > 0.16f || _rs.sqrMagnitude > 0.16f) padUsed = true;
            }
            if (padUsed) { SetUsing(true); return; }
            var kb = Keyboard.current;
            var m = Mouse.current;
            bool kbm = (kb != null && !IsVirtual(kb) && kb.anyKey.wasPressedThisFrame)
                       || (m != null && !IsVirtual(m) && (m.leftButton.wasPressedThisFrame || m.rightButton.wasPressedThisFrame || m.delta.ReadValue().sqrMagnitude > 36f));
            if (kbm) SetUsing(false);
        }

        private static void SetUsing(bool v)
        {
            if (UsingPad == v) return;
            UsingPad = v;
            UsingPadChanged?.Invoke(v);
        }

        // ================================================================ actions

        private static bool BtnNow(PadButton b) => _now[(int)b] && !_blocked[(int)b];
        private static bool BtnPrev(PadButton b) => _prev[(int)b];

        private static bool Held(PadBinding b, bool[] state, bool checkBlock)
        {
            if (b.Button == PadButton.None) return false;
            bool l1 = state[(int)PadButton.L1];
            if (b.Button == PadButton.Touchpad && !HasTouchpad) // pad without a touchpad (XInput): L1 + Options instead
                return l1 && state[(int)PadButton.Options] && !(checkBlock && _blocked[(int)PadButton.Options]);
            if (!state[(int)b.Button] || (checkBlock && _blocked[(int)b.Button])) return false;
            return b.L1 == l1;
        }

        private static PadButton KeyButton(PadBinding b) => b.Button == PadButton.Touchpad && !HasTouchpad ? PadButton.Options : b.Button;

        /// <summary>The action's button went down this frame (with the right L1 layer).</summary>
        public static bool Down(PadAction a)
        {
            if (Pad == null) return false;
            var b = GetBinding(a);
            var k = KeyButton(b);
            return Held(b, _now, true) && !_prev[(int)k];
        }

        /// <summary>The action's button is held (with the right L1 layer).</summary>
        public static bool Held(PadAction a) => Pad != null && Held(GetBinding(a), _now, true);

        /// <summary>The action stopped being held this frame.</summary>
        public static bool Up(PadAction a) => Pad != null && !Held(GetBinding(a), _now, true) && Held(GetBinding(a), _prev, false);

        /// <summary>Raw button state, no layer logic (menus, test view).</summary>
        public static bool ButtonHeld(PadButton b) => Pad != null && b != PadButton.None && _now[(int)b];
        public static bool ButtonDown(PadButton b) => Pad != null && b != PadButton.None && BtnNow(b) && !BtnPrev(b);
        public static bool ButtonUp(PadButton b) => Pad != null && b != PadButton.None && !_now[(int)b] && _prev[(int)b];

        /// <summary>Left stick pushed past the threshold this frame in that direction (digital use of the stick).</summary>
        public static bool StickDown(PadDir d) { int i = DirBase + (int)d; return Pad != null && _now[i] && !_prev[i] && !_blocked[i]; }
        public static bool StickHeld(PadDir d) { int i = DirBase + (int)d; return Pad != null && _now[i] && !_blocked[i]; }

        /// <summary>
        /// Every button held right now is ignored until released (e.g. Cross closed a menu: no jump on the same press).
        /// </summary>
        public static void BlockUntilRelease()
        {
            for (int i = 0; i < _now.Length; i++) if (_now[i]) _blocked[i] = true;
        }

        // ================================================================ fixed helpers for menus (not rebindable)

        /// <summary>Cross: confirm / activate.</summary>
        public static bool Confirm => ButtonDown(PadButton.Cross);
        /// <summary>Circle: back / cancel.</summary>
        public static bool Cancel => ButtonDown(PadButton.Circle);
        /// <summary>Options: menu button.</summary>
        public static bool Menu => ButtonDown(PadButton.Options);
        /// <summary>Square / Triangle as extra menu buttons.</summary>
        public static bool Square => ButtonDown(PadButton.Square);
        public static bool Triangle => ButtonDown(PadButton.Triangle);
        public static bool ShoulderL => ButtonDown(PadButton.L1);
        public static bool ShoulderR => ButtonDown(PadButton.R1);
        /// <summary>D-pad or left stick, true on the press and then auto-repeating while held.</summary>
        public static bool NavUp => _nav[(int)PadDir.Up];
        public static bool NavDown => _nav[(int)PadDir.Down];
        public static bool NavLeft => _nav[(int)PadDir.Left];
        public static bool NavRight => _nav[(int)PadDir.Right];
        /// <summary>Any pad button pressed this frame.</summary>
        public static bool AnyButtonDown
        {
            get
            {
                if (Pad == null) return false;
                for (int i = 1; i < BtnCount; i++) if (_now[i] && !_prev[i]) return true;
                return false;
            }
        }

        private static void TickNav()
        {
            float t = Time.unscaledTime;
            for (int d = 0; d < 4; d++)
            {
                var db = d == 0 ? PadButton.DpadUp : d == 1 ? PadButton.DpadDown : d == 2 ? PadButton.DpadLeft : PadButton.DpadRight;
                bool held = (_now[(int)db] && !_blocked[(int)db]) || (_now[DirBase + d] && !_blocked[DirBase + d]);
                bool was = _prev[(int)db] || _prev[DirBase + d];
                _nav[d] = false;
                if (!held) continue;
                if (!was) { _nav[d] = true; _navNext[d] = t + 0.4f; }
                else if (t >= _navNext[d]) { _nav[d] = true; _navNext[d] = t + 0.09f; }
            }
        }

        // ================================================================ rumble

        private static float _rumbleUntil;
        private static bool _rumbling;

        /// <summary>Vibration (0..1 low / high frequency motor) for some seconds; no-op when disabled or no pad.</summary>
        public static void Rumble(float low, float high, float seconds)
        {
            if (Pad == null || !RumbleEnabled) return;
            try
            {
                Pad.SetMotorSpeeds(Mathf.Clamp01(low), Mathf.Clamp01(high));
                _rumbling = true;
                _rumbleUntil = Time.unscaledTime + Mathf.Max(0.02f, seconds);
            }
            catch (Exception e) { Debug.LogWarning($"[Pad] rumble failed: {e.Message}"); }
        }

        public static void StopRumble()
        {
            if (!_rumbling) return;
            _rumbling = false;
            try { Pad?.ResetHaptics(); } catch { /* device gone */ }
        }

        private static void TickRumble()
        {
            if (_rumbling && Time.unscaledTime >= _rumbleUntil) StopRumble();
        }

        // ================================================================ rebinding capture

        /// <summary>Action waiting for a button press (settings tab), or null.</summary>
        public static PadAction? Capturing { get; private set; }

        private static int _captureEndFrame = -10;

        /// <summary>
        /// A capture runs (or ended this / last frame): menu code must not react to pad buttons (the press is a binding).
        /// </summary>
        public static bool CaptureOwnsPad => Capturing != null || _captureEndFrame >= Time.frameCount - 1;
        public static float CaptureStarted { get; private set; }
        public const float CaptureTimeout = 6f;
        /// <summary>Result of the last capture: the swapped action (or null) - for the settings text.</summary>
        public static string LastCaptureMessage { get; private set; } = "";

        /// <summary>Starts waiting for the next pad button (L1 held = L1 layer). Esc, the PS button or 6 s cancel.</summary>
        public static void BeginCapture(PadAction a)
        {
            Capturing = a;
            CaptureStarted = Time.unscaledTime;
            LastCaptureMessage = "";
            BlockUntilRelease(); // the press that started the capture is not captured
        }

        public static void CancelCapture()
        {
            if (Capturing == null) return;
            Capturing = null;
            _captureEndFrame = Time.frameCount;
            LastCaptureMessage = "Anulowano.";
        }

        private static void TickCapture()
        {
            if (Capturing == null) return;
            var kb = Keyboard.current;
            if ((kb != null && kb.escapeKey.wasPressedThisFrame) || Time.unscaledTime - CaptureStarted > CaptureTimeout ||
                ButtonDown(PadButton.Home))
            {
                CancelCapture();
                return;
            }
            if (Pad == null) return;
            for (int i = 1; i < BtnCount; i++)
            {
                var b = (PadButton)i;
                if (b == PadButton.L1 || b == PadButton.Home || !BtnNow(b) || BtnPrev(b)) continue;
                var a = Capturing.Value;
                var nb = new PadBinding(b, _now[(int)PadButton.L1]);
                var swapped = SetBinding(a, nb);
                Capturing = null;
                _captureEndFrame = Time.frameCount;
                LastCaptureMessage = swapped != null
                    ? $"{ActionName(a)}: {Label(nb)}. Zamiana: {ActionName(swapped.Value)} ma teraz {Label(GetBinding(swapped.Value))}."
                    : $"{ActionName(a)}: {Label(nb)}.";
                BlockUntilRelease(); // the captured press does nothing else
                Debug.Log($"[Pad] bound {a} = {nb}" + (swapped != null ? $" (swapped {swapped})" : ""));
                return;
            }
        }

        // ================================================================ labels

        /// <summary>Short label of a button for the current pad ("✕", "R2", "Options"; Xbox names on other pads).</summary>
        public static string ButtonLabel(PadButton b)
        {
            bool ps = Kind != PadKind.Other;
            switch (b)
            {
                case PadButton.Cross: return ps ? "✕" : "A";
                case PadButton.Circle: return ps ? "○" : "B";
                case PadButton.Square: return ps ? "□" : "X";
                case PadButton.Triangle: return ps ? "△" : "Y";
                case PadButton.L1: return ps ? "L1" : "LB";
                case PadButton.R1: return ps ? "R1" : "RB";
                case PadButton.L2: return ps ? "L2" : "LT";
                case PadButton.R2: return ps ? "R2" : "RT";
                case PadButton.L3: return ps ? "L3" : "LS";
                case PadButton.R3: return ps ? "R3" : "RS";
                case PadButton.DpadUp: return "↑";
                case PadButton.DpadDown: return "↓";
                case PadButton.DpadLeft: return "←";
                case PadButton.DpadRight: return "→";
                case PadButton.Options: return ps ? "Options" : "Menu";
                case PadButton.Share: return Kind == PadKind.DualSense ? "Create" : ps ? "Share" : "View";
                case PadButton.Touchpad: return ps ? "Touchpad" : "LB+Menu";
                case PadButton.Home: return ps ? "PS" : "Home";
                default: return "";
            }
        }

        /// <summary>Long Polish name of a button (settings / test view).</summary>
        public static string ButtonName(PadButton b)
        {
            switch (b)
            {
                case PadButton.Cross: return "Krzyżyk ✕";
                case PadButton.Circle: return "Kółko ○";
                case PadButton.Square: return "Kwadrat □";
                case PadButton.Triangle: return "Trójkąt △";
                case PadButton.DpadUp: return "Krzyżak ↑";
                case PadButton.DpadDown: return "Krzyżak ↓";
                case PadButton.DpadLeft: return "Krzyżak ←";
                case PadButton.DpadRight: return "Krzyżak →";
                default: return ButtonLabel(b);
            }
        }

        public static string Label(PadBinding b)
        {
            if (b.Button == PadButton.Touchpad && !HasTouchpad && Pad != null) return Kind == PadKind.Other ? "LB+Menu" : "L1+Options";
            return (b.L1 ? ButtonLabel(PadButton.L1) + "+" : "") + ButtonLabel(b.Button);
        }

        /// <summary>Label of the action's current pad button ("□", "L1+✕").</summary>
        public static string Label(PadAction a) => Label(GetBinding(a));

        /// <summary>Polish name of an action (settings list).</summary>
        public static string ActionName(PadAction a)
        {
            switch (a)
            {
                case PadAction.Jump: return "Skok / wstań / ręczny";
                case PadAction.Crouch: return "Kucanie";
                case PadAction.Prone: return "Czołganie";
                case PadAction.Sprint: return "Bieg / dopalacz";
                case PadAction.Interact: return "Interakcja (E)";
                case PadAction.ActionF: return "Akcja F (piła, zdeptanie)";
                case PadAction.Reload: return "Przeładuj / tankuj / reset (R)";
                case PadAction.Flashlight: return "Latarka (T)";
                case PadAction.Drop: return "Wyrzuć / odłóż (Q)";
                case PadAction.SlotPrev: return "Poprzedni przedmiot";
                case PadAction.SlotNext: return "Następny przedmiot";
                case PadAction.Attack: return "Użyj / atak (LPM)";
                case PadAction.Aim: return "Celuj / anuluj (PPM)";
                case PadAction.Medkit: return "Apteczka (G)";
                case PadAction.Heal: return "Pomóż / klakson (H)";
                case PadAction.Carry: return "Nieś na rękach (B)";
                case PadAction.Dust: return "Rzuć kurz (X)";
                case PadAction.Wire: return "Antena pilota (U)";
                case PadAction.Wheel: return "Emotki / pozy (K)";
                case PadAction.Map: return "Skala minimapy (M)";
                case PadAction.Voice: return "Mów (V)";
                case PadAction.Camera: return "Kamera gokarta (V)";
                case PadAction.RcRemote: return "Autko RC (6)";
                case PadAction.ScrollUp: return "Kółko myszy w górę";
                case PadAction.ScrollDown: return "Kółko myszy w dół";
                case PadAction.Yes: return "Zgoda (Y)";
                case PadAction.No: return "Odmowa (N)";
                case PadAction.Pause: return "Menu (Esc)";
                default: return a.ToString();
            }
        }

        // ================================================================ fallback devices

        /// <summary>
        /// Devices made by code, not hands: the idle fallback keyboard / mouse and the menu cursor's virtual mouse
        /// ("PadVirtualMouse", moved by the sticks) - never "the player switched to the mouse".
        /// </summary>
        public static bool IsVirtual(InputDevice d) => d != null && (d.name.StartsWith("PadFallback", StringComparison.Ordinal) || d.name.StartsWith("PadVirtual", StringComparison.Ordinal));

        /// <summary>
        /// Without any keyboard / mouse (pad-only PC, batchmode tests) <see cref="Keyboard.current"/> / <see cref="Mouse.current"/>
        /// would be null and lots of code bails out: add idle virtual ones so the game runs with the pad alone.
        /// </summary>
        public static void EnsureFallbackDevices()
        {
            try
            {
                if (Keyboard.current == null) { InputSystem.AddDevice<Keyboard>("PadFallbackKeyboard"); Debug.Log("[Pad] no keyboard: added an idle virtual one"); }
                if (Mouse.current == null) { InputSystem.AddDevice<Mouse>("PadFallbackMouse"); Debug.Log("[Pad] no mouse: added an idle virtual one"); }
            }
            catch (Exception e) { Debug.LogWarning($"[Pad] fallback devices failed: {e.Message}"); }
        }
    }
}
