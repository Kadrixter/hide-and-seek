using HideAndSeek.Audio;
using HideAndSeek.Game.Net;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace HideAndSeek.Game
{
    /// <summary>
    /// The dead player's body in the lobby copy: a first-person camera (with the audio listener) on a character
    /// controller. Starts with the scripted walk-in (<see cref="DeadLobbyWalkIn"/>: through the white void, the door
    /// opens, in, the door closes), then WASD / Shift / mouse like in the lobby. E: sit on a sofa or pouf, the lobby
    /// switches and toys (<see cref="DeadLobbyInteract"/>). The pose is sent to the other dead players in
    /// <see cref="NetPlayer.LobbyGhost"/> (lobby-local, ~15 times a second while it changes).
    /// </summary>
    public sealed class DeadLobbyBody : MonoBehaviour
    {
        public const float EyeHeight = 1.62f, WalkSpeed = 3f, RunSpeed = 5.2f;

        public Camera Cam { get; private set; }
        public bool WalkInDone => _walk == null || _walk.Done;
        /// <summary>Lobby seat the body sits on (LobbySeat index), -1 standing.</summary>
        public int Seat { get; private set; } = -1;
        public Vector3 Eye => Cam != null ? Cam.transform.position : transform.position + Vector3.up * EyeHeight;
        public Vector3 Forward => Cam != null ? Cam.transform.forward : transform.forward;

        private NetPlayer _me;
        private CharacterController _cc;
        private DeadLobbyWalkIn _walk;
        private float _yaw, _pitch, _vy, _bob, _seatYaw;
        private Vector3 _vel;
        private float _nextSend;

        /// <summary>Test hook: puts the body at a lobby-local point looking at another one (ends the walk-in).</summary>
        public void TestPlace(Vector3 localFeet, Vector3 localLookAt)
        {
            _walk = null;
            StandUp(false);
            Teleport(DeadLobbyWorld.ToWorld(localFeet));
            Vector3 d = DeadLobbyWorld.ToWorld(localLookAt) - (transform.position + Vector3.up * EyeHeight);
            _yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            _pitch = Mathf.Clamp(Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg, -80f, 80f);
        }

        public static DeadLobbyBody Create(DeadLobbyWorld world, NetPlayer me)
        {
            var go = new GameObject("DeadLobbyBody");
            SceneManager.MoveGameObjectToScene(go, world.Scene);
            go.layer = 2; // Ignore Raycast: the own prompts never hit the own capsule
            var b = go.AddComponent<DeadLobbyBody>();
            b._me = me;
            b._cc = go.AddComponent<CharacterController>();
            b._cc.height = 1.8f;
            b._cc.radius = 0.3f;
            b._cc.center = new Vector3(0f, 0.9f, 0f);
            b._cc.stepOffset = 0.35f;
            b._cc.enabled = false;
            var eye = new GameObject("Eye");
            eye.transform.SetParent(go.transform, false);
            eye.transform.localPosition = new Vector3(0f, EyeHeight, 0f);
            b.Cam = eye.AddComponent<Camera>();
            if (me != null && me.Camera != null) b.Cam.CopyFrom(me.Camera);
            b.Cam.targetTexture = null;
            b.Cam.nearClipPlane = 0.05f;
            b.Cam.farClipPlane = 80f;
            b.Cam.enabled = false;
            eye.AddComponent<AudioListener>().enabled = false;
            b._walk = new DeadLobbyWalkIn(world, b);
            b._walk.Pose(0f, out var p, out b._yaw, out b._pitch);
            go.transform.position = p;
            Debug.Log($"[DeadLobby] body created at {DeadLobbyWorld.ToLocal(p)} (lobby-local), walk-in {DeadLobbyWalkIn.Duration:0.0} s");
            return b;
        }

        public void Dispose()
        {
            if (this != null && gameObject != null) Destroy(gameObject);
        }

        /// <summary>Every frame (DeadLobby.LateUpdate): the lobby view owns the screen and the ears, or hands them over.</summary>
        public void ApplyView(bool mine)
        {
            if (Cam == null) return;
            if (mine)
            {
                if (_me != null && _me.Camera != null && _me.Camera.enabled)
                {
                    _me.Camera.enabled = false;
                    var l = _me.Camera.GetComponent<AudioListener>();
                    if (l != null) l.enabled = false;
                }
                if (!Cam.enabled) { Cam.enabled = true; Cam.tag = "MainCamera"; }
                var own = Cam.GetComponent<AudioListener>();
                if (!own.enabled) own.enabled = true;
            }
            else if (Cam.enabled)
            {
                Cam.enabled = false;
                Cam.tag = "Untagged";
                Cam.GetComponent<AudioListener>().enabled = false;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (_walk != null && !_walk.Done)
            {
                _walk.Tick(dt, out var p, out _yaw, out _pitch);
                transform.position = p;
                if (_walk.Done)
                {
                    _cc.enabled = true;
                    Debug.Log($"[DeadLobby] walk-in done at {DeadLobbyWorld.ToLocal(p)}");
                }
                PoseCamera(0f);
                Send(LobbyGhostState.WalkingIn);
                return;
            }
            bool control = DeadLobby.InLobby && !LocalPlayerController.MenuOpen;
            Look(control);
            if (Seat >= 0) SeatedTick(control);
            else Move(control, dt);
            if (control) DeadLobbyInteract.Tick(this);
            Send(DeadLobby.Watching ? LobbyGhostState.Watching : LobbyGhostState.InLobby);
        }

        private void Look(bool control)
        {
            var mouse = Mouse.current;
            if (control)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            if (!control || mouse == null) return;
            float sens = LocalProfile.MouseSensitivity;
            Vector2 d = mouse.delta.ReadValue() * sens;
            bool invert = _me != null && _me.InputReader != null && _me.InputReader.InvertY;
            _yaw += d.x;
            _pitch = Mathf.Clamp(_pitch + (invert ? -d.y : d.y), -85f, 85f);
        }

        private void Move(bool control, float dt)
        {
            var kb = Keyboard.current;
            Vector2 input = Vector2.zero;
            bool run = false;
            if (control && kb != null)
            {
                input.x = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
                input.y = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
                run = kb.leftShiftKey.isPressed;
            }
            input = Vector2.ClampMagnitude(input, 1f);
            Quaternion yawRot = Quaternion.Euler(0f, _yaw, 0f);
            Vector3 want = yawRot * new Vector3(input.x, 0f, input.y) * (run ? RunSpeed : WalkSpeed);
            float acc = want.sqrMagnitude > 0.01f ? 18f : 22f;
            _vel = Vector3.MoveTowards(_vel, want, acc * dt);
            if (_cc.isGrounded && _vy < 0f) _vy = -2f;
            else _vy = Mathf.Max(_vy - 22f * dt, -30f);
            if (_cc.enabled) _cc.Move((_vel + Vector3.up * _vy) * dt);
            // Fell out of the room somehow: back to the middle.
            if (transform.position.y < DeadLobby.Offset.y - 5f) Teleport(DeadLobbyWorld.ToWorld(new Vector3(1.5f, 0.05f, -0.6f)));
            float speed = new Vector2(_vel.x, _vel.z).magnitude;
            _bob += speed * dt * 2.2f;
            PoseCamera(speed);
        }

        private void PoseCamera(float speed)
        {
            float bob = Seat < 0 ? Mathf.Sin(_bob * Mathf.PI) * 0.035f * Mathf.Clamp01(speed / WalkSpeed) : 0f;
            if (Seat < 0) Cam.transform.localPosition = new Vector3(0f, EyeHeight + bob, 0f);
            Cam.transform.rotation = Quaternion.Euler(-_pitch, _yaw, 0f);
        }

        // ================================================================ seats

        /// <summary>Sits down on lobby seat <paramref name="index"/> (E on a sofa cushion / pouf).</summary>
        public void SitOn(int index)
        {
            var s = LobbySeat.Get(index);
            if (s == null) return;
            Seat = index;
            _vel = Vector3.zero;
            _cc.enabled = false;
            transform.position = s.transform.position;
            _seatYaw = s.transform.eulerAngles.y;
            _yaw = _seatYaw;
            _pitch = 0f;
            float top = s.Shape != null ? s.Shape.TopY : 0.45f;
            Cam.transform.position = s.transform.position - s.transform.forward * 0.32f + Vector3.up * (top + 0.8f);
            GameAudio.PlayAt(GameSfx.UiClick, s.AimPoint, 0.4f);
        }

        private void SeatedTick(bool control)
        {
            var kb = Keyboard.current;
            _yaw = _seatYaw + Mathf.Clamp(Mathf.DeltaAngle(_seatYaw, _yaw), -110f, 110f);
            PoseCamera(0f);
            if (control && kb != null && (kb.spaceKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame)) StandUp(true);
        }

        public void StandUp(bool sound)
        {
            if (Seat < 0) return;
            var s = LobbySeat.Get(Seat);
            Seat = -1;
            Vector3 at = s != null ? s.transform.position + s.transform.forward * 0.25f : transform.position;
            Teleport(at);
            if (sound) GameAudio.PlayAt(GameSfx.UiClick, at + Vector3.up, 0.3f);
        }

        private void Teleport(Vector3 feet)
        {
            _cc.enabled = false;
            transform.position = feet + Vector3.up * 0.03f;
            _vel = Vector3.zero;
            _vy = 0f;
            _cc.enabled = _walk == null || _walk.Done;
            PoseCamera(0f);
        }

        // ================================================================ network

        private LobbyGhostState _sent;

        private void Send(byte mode)
        {
            if (_me == null || !_me.IsSpawned || !_me.IsOwner || Time.unscaledTime < _nextSend) return;
            var st = new LobbyGhostState
            {
                Mode = mode,
                Seat = (sbyte)Seat,
                Pos = DeadLobbyWorld.ToLocal(transform.position),
                Yaw = Mathf.Repeat(_yaw, 360f),
                Pitch = _pitch,
            };
            bool moved = (st.Pos - _sent.Pos).sqrMagnitude > 0.0004f || Mathf.Abs(Mathf.DeltaAngle(st.Yaw, _sent.Yaw)) > 1f ||
                         Mathf.Abs(st.Pitch - _sent.Pitch) > 2f || st.Mode != _sent.Mode || st.Seat != _sent.Seat;
            if (!moved) return;
            _nextSend = Time.unscaledTime + 1f / 15f;
            _sent = st;
            _me.LobbyGhost.Value = st;
        }
    }
}
