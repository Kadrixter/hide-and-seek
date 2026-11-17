using UnityEngine;
using UnityEngine.InputSystem;

namespace HideAndSeek.Player
{
    /// <summary>Minimal OnGUI debug overlay (speed, state, crouch, flashlight). Toggle with F1.</summary>
    public class PlayerDebugHud : MonoBehaviour
    {
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private Flashlight flashlight;
        [SerializeField] private bool visible = true;
        [SerializeField] private bool showCrosshair = true;

        private float _fps;
        private GUIStyle _style;
        private readonly System.Text.StringBuilder _sb = new System.Text.StringBuilder(256);

        public bool Visible { get => visible; set => visible = value; }

        private void Awake()
        {
            if (motor == null) motor = FindAnyObjectByType<PlayerMotor>();
            if (flashlight == null && motor != null) flashlight = motor.GetComponentInChildren<Flashlight>(true);
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f1Key.wasPressedThisFrame) visible = !visible;
            float dt = Time.unscaledDeltaTime;
            if (dt > 0f) _fps = Mathf.Lerp(_fps, 1f / dt, 1f - Mathf.Exp(-3f * dt));
        }

        private void OnGUI()
        {
            if (showCrosshair)
            {
                var c = new Rect(Screen.width * 0.5f - 1f, Screen.height * 0.5f - 1f, 2f, 2f);
                GUI.color = new Color(1f, 1f, 1f, 0.5f);
                GUI.DrawTexture(c, Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
            if (!visible || motor == null) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true };
                _style.normal.textColor = new Color(0.85f, 1f, 0.85f);
            }

            _sb.Clear();
            _sb.AppendLine("<b>PLAYER DEBUG</b>  (F1)");
            _sb.AppendFormat("FPS        {0:0}\n", _fps);
            _sb.AppendFormat("State      {0}\n", motor.State);
            _sb.AppendFormat("Speed      {0:0.00} m/s\n", motor.HorizontalSpeed);
            _sb.AppendFormat("Vert vel   {0:0.00} m/s\n", motor.Velocity.y);
            _sb.AppendFormat("Grounded   {0}\n", motor.IsGrounded);
            _sb.AppendFormat("Running    {0}\n", motor.IsRunning);
            _sb.AppendFormat("Crouch     {0:0.00} ({1})\n", motor.CrouchAmount, motor.IsCrouching ? "crouching" : "standing");
            _sb.AppendFormat("Pitch      {0:0.0}\n", motor.LookPitch);
            _sb.AppendFormat("Step phase {0:0.00}\n", motor.StepPhase % 2f);
            if (flashlight != null) _sb.AppendFormat("Flashlight {0} ({1:0.00})\n", flashlight.IsOn ? "ON" : "off", flashlight.Level);
            if (motor.Input != null) _sb.AppendFormat("Input      {0}, cursor {1}\n", motor.Input.InputEnabled ? "enabled" : "DISABLED", motor.Input.CursorLocked ? "locked" : "free");
            _sb.Append("WASD move, Shift run, Ctrl/C crouch, Space jump, LMB light, Esc cursor");

            var rect = new Rect(10f, 10f, 520f, 300f);
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(6f, 6f, 470f, 262f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(rect, _sb.ToString(), _style);
        }
    }
}
