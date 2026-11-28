using System.Collections.Generic;
using HideAndSeek.Game.Net;
using HideAndSeek.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HideAndSeek.Game.UI
{
    /// <summary>
    /// Free camera of the debug console (`cam0`, host only, 28.11): flies through walls (no collisions), the sound is
    /// heard where the camera is (its own AudioListener; the voices too - the server relays the players' voices to the
    /// host by the camera's position, VoiceRules.ListenPos), while the own character stays where it was (seen from the
    /// outside, it does not move). WASD fly, mouse look, Space up, Ctrl / C down, Shift fast, the wheel changes the
    /// speed. `cam0` again (or `cam`) ends it and gives the view back.
    /// </summary>
    public sealed class FreeCam : MonoBehaviour
    {
        public static FreeCam Instance { get; private set; }
        public static bool Active => Instance != null;
        /// <summary>Where the free camera hears from (valid while <see cref="Active"/>).</summary>
        public static Vector3 Position => Instance != null ? Instance.transform.position : Vector3.zero;

        private NetPlayer _me;
        private Camera _cam;
        private float _yaw, _pitch, _speed = 8f;
        private readonly List<AudioListener> _listenersOff = new();
        private readonly List<Camera> _camerasOff = new();

        public static string Toggle()
        {
            if (Instance != null) { Instance.End(); return "Wolna kamera wyłączona."; }
            var me = NetPlayer.Local;
            if (me == null) return "Najpierw wejdź do gry.";
            if (LobbyKart.LocalKart != null || LobbyKart.LocalPassengerKart != null) return "Najpierw wysiądź z gokarta.";
            var src = me.Camera != null && me.Camera.enabled ? me.Camera : Camera.main;
            var go = new GameObject("FreeCam");
            DontDestroyOnLoad(go);
            var f = go.AddComponent<FreeCam>();
            f._me = me;
            f._cam = go.AddComponent<Camera>();
            if (src != null)
            {
                f._cam.CopyFrom(src);
                go.transform.SetPositionAndRotation(src.transform.position, src.transform.rotation);
            }
            else go.transform.position = me.transform.position + Vector3.up * 1.7f;
            f._cam.depth = 100f;
            f._cam.nearClipPlane = 0.03f;
            f._cam.farClipPlane = 1500f;
            f._cam.cullingMask = ~(1 << 9); // everything but the first-person view model (the own body is drawn)
            Vector3 e = go.transform.eulerAngles;
            f._yaw = e.y;
            f._pitch = e.x > 180f ? e.x - 360f : e.x;
            // One ear: switch every other listener (and camera) off until the end.
            foreach (var l in FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude))
                if (l != null && l.enabled) { l.enabled = false; f._listenersOff.Add(l); }
            foreach (var c in FindObjectsByType<Camera>(FindObjectsInactive.Exclude))
                if (c != null && c != f._cam && c.enabled && c.targetTexture == null) { c.enabled = false; f._camerasOff.Add(c); }
            go.AddComponent<AudioListener>();
            go.tag = "MainCamera";
            me.SetCinematicBody(true); // the own body seen from outside
            Instance = f;
            return "Wolna kamera: WASD lot, mysz patrzenie, Spacja w górę, Ctrl/C w dół, Shift szybciej, kółko prędkość. cam0 - koniec.";
        }

        public void End()
        {
            if (Instance == this) Instance = null;
            foreach (var l in _listenersOff) if (l != null) l.enabled = true;
            foreach (var c in _camerasOff) if (c != null) c.enabled = true;
            _listenersOff.Clear();
            _camerasOff.Clear();
            if (_me != null) _me.SetCinematicBody(false);
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                foreach (var l in _listenersOff) if (l != null) l.enabled = true;
                foreach (var c in _camerasOff) if (c != null) c.enabled = true;
            }
        }

        private void LateUpdate()
        {
            if (_me == null || !_me.IsSpawned) { End(); return; }
            // The game may switch the eyes back on (respawn, spectator...): keep one view and one ear.
            if (_me.Camera != null && _me.Camera.enabled) { _me.Camera.enabled = false; if (!_camerasOff.Contains(_me.Camera)) _camerasOff.Add(_me.Camera); }
            var eyeL = _me.Camera != null ? _me.Camera.GetComponent<AudioListener>() : null;
            if (eyeL != null && eyeL.enabled) { eyeL.enabled = false; if (!_listenersOff.Contains(eyeL)) _listenersOff.Add(eyeL); }
            float dt = Time.unscaledDeltaTime;
            var kb = Keyboard.current;
            bool menu = LocalPlayerController.MenuOpen;
            if (!menu)
            {
                if (Cursor.lockState != CursorLockMode.Locked) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
                float scale = 0.1f * Mathf.Max(0.2f, LocalProfile.MouseSensitivity);
                Vector2 d = KeyOrPad.MouseDelta(scale) * scale;
                _yaw += d.x;
                _pitch = Mathf.Clamp(_pitch - d.y, -89f, 89f);
                var mouse = Mouse.current;
                if (mouse != null)
                {
                    float wheel = mouse.scroll.ReadValue().y;
                    if (Mathf.Abs(wheel) > 0.01f) _speed = Mathf.Clamp(_speed * (wheel > 0f ? 1.25f : 0.8f), 1f, 120f);
                }
            }
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            if (menu || kb == null) return;
            Vector3 move = Vector3.zero;
            if (kb.wKey.isPressed) move += transform.forward;
            if (kb.sKey.isPressed) move -= transform.forward;
            if (kb.dKey.isPressed) move += transform.right;
            if (kb.aKey.isPressed) move -= transform.right;
            if (kb.spaceKey.isPressed) move += Vector3.up;
            if (kb.leftCtrlKey.isPressed || kb.cKey.isPressed) move -= Vector3.up;
            float sp = _speed * (kb.leftShiftKey.isPressed ? 3.5f : 1f);
            transform.position += move.normalized * sp * dt;
        }
    }
}
