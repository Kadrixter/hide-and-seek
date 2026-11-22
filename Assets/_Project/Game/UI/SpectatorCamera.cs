using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HideAndSeek.Game
{
    /// <summary>Third-person orbit camera for dead players. LMB / RMB cycles through players still in the game.</summary>
    public class SpectatorCamera : MonoBehaviour
    {
        public NetPlayer Target { get; private set; }

        private Camera _cam;
        private NetPlayer _me;
        private float _yaw, _pitch = 18f;
        private int _index;
        private Vector3 _vel;

        public void Begin(NetPlayer me)
        {
            _me = me;
            _cam = gameObject.AddComponent<Camera>();
            if (me.Camera != null)
            {
                _cam.CopyFrom(me.Camera);
                me.Camera.enabled = false;
                var l = me.Camera.GetComponent<AudioListener>();
                if (l != null) l.enabled = false;
            }
            gameObject.AddComponent<AudioListener>();
            gameObject.AddComponent<NightVision>(); // the dead see in the dark, in grey
            gameObject.tag = "MainCamera";
            transform.position = me.EyePosition;
            Cycle(0);
        }

        public void End()
        {
            if (_ended) return;
            _ended = true;
            if (_me != null && _me.Camera != null)
            {
                _me.Camera.enabled = true;
                var l = _me.Camera.GetComponent<AudioListener>();
                if (l != null) l.enabled = true;
            }
        }

        private bool _ended;

        /// <summary>
        /// Destroyed without <see cref="End"/> (the HUD dropping it when the session ends, any scene change): the own camera
        /// and listener still come back. Before, a spectator destroyed with the map left the player's camera off in the
        /// lobby - the picture stood still ("the game froze") although the game ran on.
        /// </summary>
        private void OnDestroy()
        {
            if (_ended) return;
            Debug.LogWarning("[LobbyReturn] spectator camera destroyed without End - own camera turned back on");
            End();
        }

        private void Cycle(int dir)
        {
            var list = NetPlayer.All;
            if (list.Count == 0) return;
            int n = list.Count;
            int step = dir == 0 ? 1 : dir;
            int start = dir == 0 ? _index - 1 : _index;
            for (int i = 1; i <= n; i++)
            {
                int idx = ((start + step * i) % n + n) % n;
                var p = list[idx];
                if (p != null && p != _me && p.Life.Value != LifeState.Dead)
                {
                    _index = idx;
                    Target = p;
                    _yaw = p.transform.eulerAngles.y;
                    return;
                }
            }
            Target = null;
        }

        private void LateUpdate()
        {
            var mouse = Mouse.current;
            if (Target == null || !Target.IsSpawned || Target.Life.Value == LifeState.Dead) Cycle(1);
            if (mouse != null && !LocalPlayerController.MenuOpen)
            {
                if (mouse.leftButton.wasPressedThisFrame) Cycle(1);
                if (mouse.rightButton.wasPressedThisFrame) Cycle(-1);
                Vector2 d = mouse.delta.ReadValue() * 0.15f;
                _yaw += d.x;
                _pitch = Mathf.Clamp(_pitch - d.y, -10f, 70f);
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            if (Target == null) return;

            Vector3 pivot = Target.transform.position + Vector3.up * (Target.Life.Value == LifeState.Downed ? 0.6f : 1.5f);
            Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 desired = pivot - rot * Vector3.forward * 3.4f;
            Vector3 dir = desired - pivot;
            if (Physics.SphereCast(pivot, 0.2f, dir.normalized, out var hit, dir.magnitude, ~((1 << PlayerAvatarBinder.LocalBodyLayer) | GameLayers.RagdollMask), QueryTriggerInteraction.Ignore))
                desired = pivot + dir.normalized * Mathf.Max(0.4f, hit.distance - 0.1f);
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref _vel, 0.08f);
            transform.rotation = Quaternion.LookRotation(pivot - transform.position);
        }
    }
}
