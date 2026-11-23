using HideAndSeek.Game.Net;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// Board view of the local player at a table: the rendering camera (first person, or the V third-person preview)
    /// glides from the player's eyes to a spot above the board on the player's side, pitched down at it (0.7 s, eased),
    /// and back when the player gets up. Applied only while rendering (Camera.onPreCull / onPostRender), so the
    /// camera's own transform, the flashlight and the audio listener are left alone. Mouse wheel = closer / further.
    /// </summary>
    public static class MiniGameCamera
    {
        static MiniTable _table;
        static int _seat;
        static float _w, _zoom = 1f, _lastTime = -1f;
        static bool _hooked;
        static NetPlayer _p;
        static Vector3 _savedPos;
        static Quaternion _savedRot;
        static int _savedMask;
        static Camera _overridden;

        /// <summary>Blend weight 0..1 (1 = fully over the board).</summary>
        public static float Weight => _w;
        public static bool Active => _table != null;

        /// <summary>Called every frame by the local controller (table = null when not seated).</summary>
        public static void Attach(NetPlayer p, MiniTable table, int seat)
        {
            _p = p;
            if (table != null) { _table = table; _seat = seat; }
            else if (_w <= 0.001f) _table = null;
            if (!_hooked)
            {
                _hooked = true;
                Camera.onPreCull += PreCull;
                Camera.onPostRender += PostRender;
            }
            float dt = _lastTime < 0f ? 0f : Mathf.Min(0.1f, Time.unscaledTime - _lastTime);
            _lastTime = Time.unscaledTime;
            _w = Mathf.MoveTowards(_w, table != null ? 1f : 0f, dt / 0.7f);
            if (table != null)
            {
                var mouse = Mouse.current;
                float wheel = mouse != null ? mouse.scroll.ReadValue().y : 0f;
                if (Mathf.Abs(wheel) > 0.01f) _zoom = Mathf.Clamp(_zoom - Mathf.Sign(wheel) * 0.06f, 0.78f, 1.3f);
            }
            _seat = table != null ? seat : _seat;
            if (_table == null) _zoom = 1f;
        }

        static bool Mine(Camera cam)
        {
            if (_p == null || cam == null || !cam.enabled) return false;
            if (_p.Camera == cam) return true;
            return cam.name == "ThirdPersonPreviewCamera";
        }

        static void Target(out Vector3 pos, out Quaternion rot)
        {
            _table.CameraPose(_seat, _zoom, out pos, out rot);
        }

        static void PreCull(Camera cam)
        {
            if (_table == null || _w <= 0.0001f || !Mine(cam)) return;
            var t = cam.transform;
            _overridden = cam;
            _savedPos = t.position;
            _savedRot = t.rotation;
            _savedMask = cam.cullingMask;
            Target(out var pos, out var rot);
            float k = Mathf.SmoothStep(0f, 1f, _w);
            t.SetPositionAndRotation(Vector3.Lerp(_savedPos, pos, k), Quaternion.Slerp(_savedRot, rot, k));
            // No floating first-person arms over the board (layer 9 = view model).
            if (k > 0.3f) cam.cullingMask &= ~(1 << 9);
        }

        static void PostRender(Camera cam)
        {
            if (cam != _overridden || cam == null) return;
            cam.transform.SetPositionAndRotation(_savedPos, _savedRot);
            cam.cullingMask = _savedMask;
            _overridden = null;
        }

        /// <summary>Ray through the mouse as the board view shows it.</summary>
        public static bool MouseRay(out Ray ray)
        {
            ray = default;
            var mouse = Mouse.current;
            if (_p == null || _p.Camera == null || _table == null || mouse == null) return false;
            var cam = _p.Camera;
            var t = cam.transform;
            Vector3 p0 = t.position;
            Quaternion r0 = t.rotation;
            Target(out var pos, out var rot);
            float k = Mathf.SmoothStep(0f, 1f, _w);
            t.SetPositionAndRotation(Vector3.Lerp(p0, pos, k), Quaternion.Slerp(r0, rot, k));
            ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            t.SetPositionAndRotation(p0, r0);
            return true;
        }
    }
}
