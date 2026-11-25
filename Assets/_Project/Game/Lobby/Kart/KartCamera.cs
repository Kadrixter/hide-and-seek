using HideAndSeek.Game.Net;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Third-person chase camera of the lobby go-kart's local driver. Sits behind and above the kart, swinging
    /// between the nose and the direction of travel (in a drift the kart is seen sideways, the way it slides), lags a
    /// little in the turns, pulls back and widens with the speed, shakes on bumps, and never goes through a wall (sphere
    /// cast). The mouse orbits round the kart and springs back after a moment of driving. Takes the camera and the audio
    /// listener from the player (the own body is shown) and gives them back on <see cref="End"/>. A small overlay shows
    /// the controls, the speed and the drift angle / length.
    /// </summary>
    public sealed class KartCamera : MonoBehaviour
    {
        public static KartCamera Active { get; private set; }

        private NetPlayer _me;
        private LobbyKart _kart;
        private Camera _cam;
        private float _orbitYaw, _orbitPitch = 16f, _idle = 9f, _camYaw, _dist = 3f;
        private float _driftT, _lastDrift, _lastDriftShow;
        private bool _ended;
        private GUIStyle _hint, _big;
        private readonly RaycastHit[] _hits = new RaycastHit[8];

        public static KartCamera Begin(NetPlayer me, LobbyKart kart)
        {
            if (Active != null) Active.End();
            var go = new GameObject("KartCamera");
            var v = go.AddComponent<KartCamera>();
            v._me = me;
            v._kart = kart;
            v._cam = go.AddComponent<Camera>();
            if (me.Camera != null)
            {
                v._cam.CopyFrom(me.Camera);
                me.Camera.enabled = false;
                var l = me.Camera.GetComponent<AudioListener>();
                if (l != null) l.enabled = false;
            }
            v._cam.depth = (me.Camera != null ? me.Camera.depth : 0f) + 1f; // on top even if something switches the eyes back on
            v._cam.fieldOfView = 68f;
            v._cam.nearClipPlane = 0.05f;
            v._cam.cullingMask = ~(1 << 9); // everything but the first-person view model (the own body is drawn)
            go.AddComponent<AudioListener>();
            go.tag = "MainCamera";
            v._camYaw = kart.transform.eulerAngles.y;
            Active = v;
            v.Place(0f, true);
            return v;
        }

        public void End()
        {
            if (_ended) return;
            _ended = true;
            Restore();
            Destroy(gameObject);
        }

        private void Restore()
        {
            if (Active == this) Active = null;
            if (_me != null && _me.Camera != null)
            {
                _me.Camera.enabled = true;
                var l = _me.Camera.GetComponent<AudioListener>();
                if (l != null) l.enabled = true;
            }
        }

        private void OnDestroy()
        {
            if (!_ended) Restore();
        }

        private void LateUpdate()
        {
            if (_kart == null || _me == null) { End(); return; }
            float dt = Time.deltaTime;
            if (_me.Camera != null && _me.Camera.enabled) _me.Camera.enabled = false; // one view, one listener
            var mouse = Mouse.current;
            if (mouse != null && !LocalPlayerController.MenuOpen && Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 d = mouse.delta.ReadValue() * 0.1f * Mathf.Max(0.2f, LocalProfile.MouseSensitivity);
                _orbitYaw = Mathf.Clamp(_orbitYaw + d.x, -170f, 170f);
                _orbitPitch = Mathf.Clamp(_orbitPitch - d.y, -2f, 50f);
                _idle = d.sqrMagnitude > 0.01f ? 0f : _idle + dt;
            }
            else _idle += dt;
            if (Mathf.Abs(_kart.ShownSpeed) > 0.6f && _idle > 1.2f)
            {
                float k = 1f - Mathf.Exp(-2.5f * dt);
                _orbitYaw = Mathf.Lerp(_orbitYaw, 0f, k);
                _orbitPitch = Mathf.Lerp(_orbitPitch, 16f, k);
            }
            // Drift timer for the overlay.
            bool drifting = _kart.SlidingNow && Mathf.Abs(_kart.SlipAngleShown) > 10f && _kart.Phys.Speed > 2f;
            if (drifting) _driftT += dt;
            else if (_driftT > 0f)
            {
                if (_driftT > 0.5f) { _lastDrift = _driftT; _lastDriftShow = Time.time + 3f; }
                _driftT = 0f;
            }
            Place(dt, false);
        }

        private void Place(float dt, bool snap)
        {
            var kt = _kart.transform;
            Vector3 vel = _kart.Phys.Vel;
            float speed = new Vector2(vel.x, vel.z).magnitude;
            float head = kt.eulerAngles.y;
            float travel = speed > 1.2f ? Mathf.Atan2(vel.x, vel.z) * Mathf.Rad2Deg : head;
            if (_kart.Phys.ForwardSpeed < -0.3f) travel = head; // reversing: stay behind
            float baseYaw = Mathf.LerpAngle(head, travel, 0.55f);
            _camYaw = snap ? baseYaw : Mathf.LerpAngle(_camYaw, baseYaw, 1f - Mathf.Exp(-4.5f * dt));

            Quaternion orbit = Quaternion.Euler(_orbitPitch, _camYaw + _orbitYaw, 0f);
            Vector3 focus = kt.position + Vector3.up * 0.8f;
            Vector3 back = orbit * Vector3.back;
            float want = 2.9f + 0.12f * speed;
            float dist = want;
            int n = Physics.SphereCastNonAlloc(focus, 0.2f, back, _hits, want, KartCollide.LevelMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var h = _hits[i];
                if (h.collider == null || h.collider.transform.IsChildOf(kt) || h.collider.transform.IsChildOf(_me.transform)) continue;
                if (h.distance > 0f) dist = Mathf.Min(dist, h.distance - 0.05f);
            }
            dist = Mathf.Max(0.8f, dist);
            _dist = snap || dist < _dist ? dist : Mathf.MoveTowards(_dist, dist, dt * 2.5f);

            float b = _kart.Bump;
            float t = Time.time;
            var shake = Quaternion.Euler(b * 3f * Mathf.Sin(t * 47f), b * 2f * Mathf.Sin(t * 39f + 1f), b * 2.5f * Mathf.Sin(t * 31f + 2f));
            Vector3 pos = focus + back * _dist;
            transform.position = pos;
            transform.rotation = Quaternion.LookRotation(focus + kt.forward * 0.4f - pos, Vector3.up) * shake;
            if (_cam != null)
            {
                float fov = 66f + 9f * Mathf.Clamp01(speed / KartPhysics.MaxSpeed);
                _cam.fieldOfView = snap ? fov : Mathf.Lerp(_cam.fieldOfView, fov, 1f - Mathf.Exp(-3f * dt));
            }
        }

        private void OnGUI()
        {
            if (_kart == null || LocalPlayerController.MenuOpen) return;
            float s = Screen.height / 1080f;
            _hint ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.LowerCenter, richText = true };
            _big ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _hint.fontSize = Mathf.RoundToInt(22 * s);
            _big.fontSize = Mathf.RoundToInt(44 * s);
            float kmh = Mathf.Abs(_kart.Phys.Speed) * 3.6f;
            string line = $"<b>{kmh:0} km/h</b>     W / S - gaz, hamulec     A / D - skręt     Spacja - ręczny     R - na start     E - wysiądź";
            var r = new Rect(0f, Screen.height - 70f * s, Screen.width, 50f * s);
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), line, _hint);
            GUI.color = Color.white;
            GUI.Label(r, line, _hint);

            string drift = _driftT > 0.25f ? $"DRIFT  {Mathf.Abs(_kart.SlipAngleShown):0}°  {_driftT:0.0} s"
                : Time.time < _lastDriftShow ? $"Drift: {_lastDrift:0.0} s" : null;
            if (drift == null) return;
            var dr = new Rect(0f, Screen.height * 0.16f, Screen.width, 60f * s);
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.Label(new Rect(dr.x + 3f, dr.y + 3f, dr.width, dr.height), drift, _big);
            GUI.color = _driftT > 0.25f ? new Color(1f, 0.62f, 0.12f) : new Color(1f, 0.9f, 0.6f);
            GUI.Label(dr, drift, _big);
            GUI.color = Color.white;
        }
    }
}
