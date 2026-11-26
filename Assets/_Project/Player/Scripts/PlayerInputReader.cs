using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HideAndSeek.Player
{
    /// <summary>
    /// Reads local player input through code-defined InputActions (no .inputactions asset dependency)
    /// and manages the cursor lock. Other components read the polled values / subscribe to events.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public class PlayerInputReader : MonoBehaviour
    {
        [Header("Look")]
        [Tooltip("Degrees of rotation per mouse pixel.")]
        [SerializeField, Range(0.01f, 1f)] private float mouseSensitivity = 0.11f;
        [Tooltip("Smoothing time constant in seconds (0 = raw input).")]
        [SerializeField, Range(0f, 0.1f)] private float lookSmoothing = 0.012f;
        [SerializeField] private bool invertY;

        [Header("Crouch")]
        [Tooltip("If true, the crouch key toggles crouch instead of hold-to-crouch.")]
        [SerializeField] private bool crouchToggle;

        [Header("Cursor")]
        [Tooltip("Lock/hide the cursor while playing. Esc releases, click re-locks.")]
        [SerializeField] private bool manageCursor = true;

        [Header("Jump")]
        [Tooltip("How long (s) a jump press stays buffered so it can be consumed slightly before landing.")]
        [SerializeField, Range(0f, 0.3f)] private float jumpBufferTime = 0.12f;

        private InputAction _move, _look, _run, _crouch, _prone, _jump, _flashlight, _release;
        private Vector2 _smoothedLookRate;
        private bool _crouchToggled, _proneToggled;
        private float _jumpPressedTime = float.NegativeInfinity;
        private bool _inputEnabled = true;

        /// <summary>When false all gameplay input reads as neutral (e.g. seeker blindfolded during hide phase).</summary>
        public bool InputEnabled
        {
            get => _inputEnabled;
            set
            {
                if (_inputEnabled == value) return;
                _inputEnabled = value;
                if (!value)
                {
                    _smoothedLookRate = Vector2.zero;
                    _jumpPressedTime = float.NegativeInfinity;
                    LookDelta = Vector2.zero;
                }
            }
        }

        /// <summary>When true the reader locks the cursor while playing. Disable for menus.</summary>
        public bool ManageCursor
        {
            get => manageCursor;
            set
            {
                manageCursor = value;
                // Only the reader in use may free the cursor: a remote player's (disabled) reader being set up when
                // somebody joins used to unlock the cursor of everybody in the session.
                if (!value && isActiveAndEnabled) SetCursorLocked(false);
            }
        }

        public float MouseSensitivity { get => mouseSensitivity; set => mouseSensitivity = Mathf.Max(0f, value); }
        public float LookSmoothing { get => lookSmoothing; set => lookSmoothing = Mathf.Max(0f, value); }
        public bool InvertY { get => invertY; set => invertY = value; }
        public bool CrouchToggleMode { get => crouchToggle; set { crouchToggle = value; _crouchToggled = false; } }

        /// <summary>Movement input, magnitude clamped to 1 (x = strafe, y = forward).</summary>
        public Vector2 Move => _inputEnabled && _move != null ? Vector2.ClampMagnitude(_move.ReadValue<Vector2>() + PadInput.Move, 1f) : Vector2.zero;

        /// <summary>Look delta in degrees for this frame (x = yaw, y = pitch, up positive).</summary>
        public Vector2 LookDelta { get; private set; }

        public bool RunHeld => _inputEnabled && ((_run != null && _run.IsPressed()) || PadRunHeld);

        // Pad sprint: L3 is clicked (not held) on most pads, so a click keeps running until the stick is released.
        private bool _padRun;
        private bool PadRunHeld => _padRun || PadInput.Held(PadAction.Sprint);

        /// <summary>True while the player wants to crouch (hold or toggle mode).</summary>
        /// Toggled stances survive short input locks (menu, cutscenes); call <see cref="ResetStance"/> to stand up.
        public bool CrouchActive => crouchToggle ? _crouchToggled : (_inputEnabled && ((_crouch != null && _crouch.IsPressed()) || PadInput.Held(PadAction.Crouch)));

        /// <summary>Crawl (prone) toggle: Z switches it, crouch / jump leave it.</summary>
        public bool ProneActive => _proneToggled;

        /// <summary>Clears crouch and crawl toggles (death, downed, round change).</summary>
        public void ResetStance()
        {
            _crouchToggled = false;
            _proneToggled = false;
        }

        /// <summary>Clears the crawl toggle (e.g. no room to lie down, or the player became busy).</summary>
        public void CancelProne() => _proneToggled = false;

        public bool JumpHeld => _inputEnabled && ((_jump != null && _jump.IsPressed()) || PadInput.Held(PadAction.Jump));

        /// <summary>True if jump was pressed within the buffer window and not consumed yet.</summary>
        public bool JumpBuffered => _inputEnabled && Time.time - _jumpPressedTime <= jumpBufferTime;

        public bool CursorLocked => Cursor.lockState == CursorLockMode.Locked;

        /// <summary>Raised when the flashlight button is clicked (never for the click that re-locks the cursor).</summary>
        public event Action FlashlightTogglePressed;

        /// <summary>Raised on the frame jump is pressed.</summary>
        public event Action JumpPressed;

        /// <summary>Marks the buffered jump as used.</summary>
        public void ConsumeJump() => _jumpPressedTime = float.NegativeInfinity;

        public void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void Awake() => CreateActions();

        private void CreateActions()
        {
            _move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");

            _look = new InputAction("Look", InputActionType.Value, "<Mouse>/delta", expectedControlType: "Vector2");
            _run = new InputAction("Run", InputActionType.Button, "<Keyboard>/leftShift");
            _run.AddBinding("<Keyboard>/rightShift");
            _crouch = new InputAction("Crouch", InputActionType.Button, "<Keyboard>/leftCtrl");
            _crouch.AddBinding("<Keyboard>/c");
            _prone = new InputAction("Prone", InputActionType.Button, "<Keyboard>/z");
            _jump = new InputAction("Jump", InputActionType.Button, "<Keyboard>/space");
            _flashlight = new InputAction("Flashlight", InputActionType.Button, "<Mouse>/leftButton");
            _release = new InputAction("ReleaseCursor", InputActionType.Button, "<Keyboard>/escape");

            _crouch.performed += _ => OnCrouchPressed();
            _prone.performed += _ => OnPronePressed();
            _jump.performed += _ => OnJumpPressed();
            _flashlight.performed += _ => OnFlashlightClick();
            _release.performed += _ => { if (manageCursor) SetCursorLocked(false); };
        }

        private void OnCrouchPressed()
        {
            if (!_inputEnabled) return;
            if (_proneToggled)
            {
                // Crouch key while crawling: get up into a crouch.
                _proneToggled = false;
                if (crouchToggle) _crouchToggled = true;
                return;
            }
            if (crouchToggle) _crouchToggled = !_crouchToggled;
        }

        private void OnPronePressed()
        {
            if (!_inputEnabled) return;
            _proneToggled = !_proneToggled;
            if (_proneToggled) _crouchToggled = false;
        }

        private void OnJumpPressed()
        {
            if (!_inputEnabled) return;
            if (_proneToggled)
            {
                // Space while crawling stands up instead of jumping.
                _proneToggled = false;
                _crouchToggled = false;
                return;
            }
            _jumpPressedTime = Time.time;
            JumpPressed?.Invoke();
        }

        /// <summary>
        /// Set by the game: true while pad presses belong to an overlay (emote / pose wheel open or just closed: Cross picks a
        /// slice and must not jump as well).
        /// </summary>
        public static Func<bool> PadGameplayBlocked;

        /// <summary>Pad: the same press events as the keyboard actions (PadInput, rebindable), look on the right stick.</summary>
        private void PollPad()
        {
            if (!PadInput.Connected) { _padRun = false; return; }
            if (PadGameplayBlocked != null && PadGameplayBlocked()) return;
            if (PadInput.Down(PadAction.Crouch)) OnCrouchPressed();
            if (PadInput.Down(PadAction.Prone)) OnPronePressed();
            if (PadInput.Down(PadAction.Jump)) OnJumpPressed();
            if (PadInput.Down(PadAction.Sprint)) _padRun = true;
            else if (PadInput.Move.sqrMagnitude < 0.04f) _padRun = false;
            // A pad press while playing takes the cursor back (like the click that re-locks it).
            if (manageCursor && !CursorLocked && Application.isFocused && (PadInput.Down(PadAction.Attack) || PadInput.Down(PadAction.Jump)))
                SetCursorLocked(true);
        }

        private void OnFlashlightClick()
        {
            if (manageCursor && !CursorLocked)
            {
                // This click only re-captures the mouse.
                SetCursorLocked(true);
                return;
            }
            if (_inputEnabled) FlashlightTogglePressed?.Invoke();
        }

        private void OnEnable()
        {
            if (_move == null) CreateActions();
            _move.Enable(); _look.Enable(); _run.Enable(); _crouch.Enable(); _prone.Enable();
            _jump.Enable(); _flashlight.Enable(); _release.Enable();
            if (manageCursor && Application.isFocused) SetCursorLocked(true);
        }

        private void OnDisable()
        {
            _move?.Disable(); _look?.Disable(); _run?.Disable(); _crouch?.Disable(); _prone?.Disable();
            _jump?.Disable(); _flashlight?.Disable(); _release?.Disable();
            if (manageCursor) SetCursorLocked(false);
            LookDelta = Vector2.zero;
        }

        private void OnDestroy()
        {
            _move?.Dispose(); _look?.Dispose(); _run?.Dispose(); _crouch?.Dispose(); _prone?.Dispose();
            _jump?.Dispose(); _flashlight?.Dispose(); _release?.Dispose();
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            PollPad();
            Vector2 raw = Vector2.zero;
            bool lookActive = _inputEnabled && (!manageCursor || CursorLocked);
            if (lookActive && _look != null)
            {
                // The pad menu cursor's virtual mouse ("PadVirtualMouse") is not a hand on a mouse: no look from it.
                var src = _look.activeControl?.device;
                raw = src != null && PadInput.IsVirtual(src) ? Vector2.zero : _look.ReadValue<Vector2>() * mouseSensitivity;
                if (invertY) raw.y = -raw.y;
            }
            // Right stick: no locked cursor needed (only "playing", i.e. the reader manages the cursor); own sensitivity,
            // invert and acceleration (PadInput settings), not smoothed further below the stick curve.
            if (_inputEnabled && manageCursor) raw += PadInput.LookDegrees;

            if (lookSmoothing <= 0.0001f || dt <= 0f)
            {
                LookDelta = raw;
                _smoothedLookRate = dt > 0f ? raw / dt : Vector2.zero;
            }
            else
            {
                // Smooth the angular rate (not the per-frame delta) so the result is framerate independent.
                Vector2 rate = raw / dt;
                float k = 1f - Mathf.Exp(-dt / lookSmoothing);
                _smoothedLookRate = Vector2.Lerp(_smoothedLookRate, rate, k);
                LookDelta = _smoothedLookRate * dt;
            }
        }
    }
}
