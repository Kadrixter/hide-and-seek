using HideAndSeek.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using HideAndSeek.Player;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Own view during an emote: the player camera glides out of the eyes to a third-person orbit (behind first, then
    /// slowly round to a three-quarter front view) and back into the eyes at the end. Emotes are for showing the body;
    /// in first person a hug or a dance is just arms flailing at the screen edge, and the body would have to be
    /// re-animated separately for the first-person arms. The mouse orbits the camera (the body itself does not turn).
    /// <para>
    /// The real player camera is moved (Camera.main, nameplates and the image effects stay right) only for rendering:
    /// it is placed in LateUpdate after the head bob and the flashlight, and put back into the eyes right after it has
    /// rendered, so the flashlight, prompts and raycasts keep working from the eyes.
    /// </para>
    /// </summary>
    [DefaultExecutionOrder(130)]
    public sealed class EmoteCamera : MonoBehaviour
    {
        private NetPlayer _p;
        private Transform _cam;
        private Camera _camera;
        private bool _wanted, _moved, _bodyShown;
        private float _w, _yaw, _pitch, _autoYaw, _userT, _dist;
        private Vector3 _restPos;
        private Quaternion _restRot;
        private readonly RaycastHit[] _hits = new RaycastHit[8];

        /// <summary>0 = first person, 1 = fully out.</summary>
        public float Weight => _w;

        /// <summary>Pair emote: the other player (the camera frames both).</summary>
        public Transform Focus { get; set; }

        /// <summary>Body low (sitting on the floor): lower orbit.</summary>
        public bool Low { get; set; }

        /// <summary>Tests: state as text.</summary>
        public string Describe() => $"w={_w:0.00} yaw={Mathf.DeltaAngle(transform.eulerAngles.y, _yaw):0} dist={_dist:0.00} user={_userT:0.0}";

        public void SetWanted(bool on)
        {
            if (on && !_wanted)
            {
                // Drift round to the three-quarter front of the current facing; from the eyes start behind the head.
                _autoYaw = transform.eulerAngles.y + 155f;
                _userT = 0f;
                if (_w <= 0f)
                {
                    _yaw = transform.eulerAngles.y;
                    _pitch = 12f;
                    _dist = 0.6f;
                }
            }
            _wanted = on;
        }

        private void Awake()
        {
            _p = GetComponent<NetPlayer>();
            Camera.onPostRender += OnAnyPostRender;
        }

        private void OnDestroy()
        {
            Camera.onPostRender -= OnAnyPostRender;
            Restore();
            if (_bodyShown && _p != null) _p.SetCinematicBody(false);
        }

        private void LateUpdate()
        {
            if (_p == null || _p.Camera == null) return;
            float dt = Time.deltaTime;
            _w = Mathf.MoveTowards(_w, _wanted ? 1f : 0f, dt / (_wanted ? 0.6f : 0.5f));
            bool body = _w > 0.12f;
            if (body != _bodyShown)
            {
                _bodyShown = body;
                _p.SetCinematicBody(body);
            }
            if (_w <= 0f) return;

            Restore(); // not rendered last frame (camera off): start from the eyes again
            _cam = _p.Camera.transform;
            _camera = _p.Camera;
            _restPos = _cam.localPosition;
            _restRot = _cam.localRotation;

            // Mouse orbits; without input the camera drifts round to the three-quarter front view.
            var mouse = Mouse.current;
            if (mouse != null && _p.InputReader != null && !LocalPlayerController.MenuOpen && (Cursor.lockState == CursorLockMode.Locked || PadInput.Look != Vector2.zero)
                && !UI.EmoteWheel.IsOpen)
            {
                Vector2 d = KeyOrPad.MouseDelta(_p.InputReader.MouseSensitivity) * _p.InputReader.MouseSensitivity; // + right stick
                if (_p.InputReader.InvertY) d.y = -d.y;
                if (d.sqrMagnitude > 0.01f) _userT = 2.5f;
                _yaw += d.x;
                _pitch = Mathf.Clamp(_pitch - d.y, -15f, 55f);
            }
            _userT -= dt;
            if (_userT <= 0f) _yaw = Mathf.LerpAngle(_yaw, _autoYaw, 1f - Mathf.Exp(-dt * 1.6f * Mathf.Clamp01(_w * 1.5f)));

            Vector3 root = transform.position;
            Vector3 focus = root + Vector3.up * (Low ? 0.7f : 1.15f);
            float want = Low ? 2.3f : 2.7f;
            if (Focus != null)
            {
                focus = (root + Focus.position) * 0.5f + Vector3.up * 1.1f;
                want = 3.1f;
            }
            Quaternion orbit = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 back = orbit * Vector3.back;
            float dist = want;
            int n = Physics.SphereCastNonAlloc(focus, 0.22f, back, _hits, want, ObstacleMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var h = _hits[i];
                if (h.collider == null || h.collider.transform.IsChildOf(transform)) continue;
                if (h.distance > 0f) dist = Mathf.Min(dist, h.distance - 0.05f);
            }
            dist = Mathf.Max(0.35f, dist);
            _dist = dist < _dist ? dist : Mathf.MoveTowards(_dist, dist, dt * 3f);
            Vector3 orbitPos = focus + back * _dist;
            Quaternion orbitRot = Quaternion.LookRotation(focus - orbitPos, Vector3.up);

            float e = _w * _w * (3f - 2f * _w);
            _cam.SetPositionAndRotation(Vector3.Lerp(_cam.position, orbitPos, e), Quaternion.Slerp(_cam.rotation, orbitRot, e));
            _moved = true;
        }

        private static int ObstacleMask => ~((1 << PlayerAvatarBinder.LocalBodyLayer) | GameLayers.RagdollMask | (1 << 2) | (1 << 9));

        private void OnAnyPostRender(Camera c)
        {
            if (_moved && c == _camera) Restore();
        }

        private void Restore()
        {
            if (!_moved || _cam == null) return;
            _cam.localPosition = _restPos;
            _cam.localRotation = _restRot;
            _moved = false;
        }
    }
}
