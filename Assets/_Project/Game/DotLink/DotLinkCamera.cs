using HideAndSeek.Game.Net;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HideAndSeek.Game.DotLink
{
    /// <summary>
    /// Close-up view of the local player working on the dot link panel: the rendering camera glides from the eyes to a
    /// spot straight in front of the screen (0.6 s, eased) and back when he leaves, so he sees only the screen (and is
    /// blind to whoever comes up behind him). Applied while rendering (Camera.onPreCull / onPostRender) like the board
    /// games' camera: the camera's own transform, the flashlight and the audio listener stay where the player is.
    /// Mouse wheel = a little closer / further.
    /// </summary>
    public static class DotLinkCamera
    {
        static bool _hooked, _on;
        static float _w, _zoom = 1f, _last = -1f;
        static NetPlayer _p;
        static IDotLinkTarget _target;
        static Vector3 _savedPos;
        static Quaternion _savedRot;
        static int _savedMask;
        static Camera _overridden;

        /// <summary>Blend 0..1 (1 = fully in front of the screen).</summary>
        public static float Weight => _w;

        /// <summary>Every frame from <see cref="DotLinkLocal"/> (on = the local player uses <paramref name="target"/>; off = glide back from the last one).</summary>
        public static void Attach(NetPlayer p, bool on, IDotLinkTarget target)
        {
            _p = p;
            _on = on;
            if (target != null) _target = target;
            if (!_hooked)
            {
                _hooked = true;
                Camera.onPreCull += PreCull;
                Camera.onPostRender += PostRender;
            }
            float dt = _last < 0f ? 0f : Mathf.Min(0.1f, Time.unscaledTime - _last);
            _last = Time.unscaledTime;
            _w = Mathf.MoveTowards(_w, on ? 1f : 0f, dt / 0.6f);
            // Knocked down / killed / caught in an animation: the view is handed back at once (kill camera, spectating).
            if (!on && p != null && (p.Busy.Value || p.Life.Value != HideAndSeek.Core.LifeState.Alive)) _w = 0f;
            if (on)
            {
                var mouse = Mouse.current;
                float wheel = mouse != null ? mouse.scroll.ReadValue().y : 0f;
                if (Mathf.Abs(wheel) > 0.01f) _zoom = Mathf.Clamp(_zoom - Mathf.Sign(wheel) * 0.06f, 0.8f, 1.35f);
            }
            else if (_w <= 0f) _zoom = 1f;
        }

        static bool Mine(Camera cam) => _p != null && cam != null && cam.enabled && (_p.Camera == cam || cam.name == "ThirdPersonPreviewCamera");

        static bool Target(Camera cam, out Vector3 pos, out Quaternion rot)
        {
            pos = default;
            rot = default;
            var t = _target;
            if (t == null || (t is Object o && o == null)) return false;
            return t.CameraPose(cam.fieldOfView, _zoom, out pos, out rot);
        }

        static void PreCull(Camera cam)
        {
            if (_w <= 0.0001f || !Mine(cam) || !Target(cam, out var pos, out var rot)) return;
            var t = cam.transform;
            _overridden = cam;
            _savedPos = t.position;
            _savedRot = t.rotation;
            _savedMask = cam.cullingMask;
            float k = Mathf.SmoothStep(0f, 1f, _w);
            t.SetPositionAndRotation(Vector3.Lerp(_savedPos, pos, k), Quaternion.Slerp(_savedRot, rot, k));
            if (k > 0.3f) cam.cullingMask &= ~(1 << 9); // no first-person arms in front of the screen
        }

        static void PostRender(Camera cam)
        {
            if (cam != _overridden || cam == null) return;
            cam.transform.SetPositionAndRotation(_savedPos, _savedRot);
            cam.cullingMask = _savedMask;
            _overridden = null;
        }

        /// <summary>Ray through a screen point as the close-up shows it.</summary>
        public static bool RayAt(Vector2 screenPoint, out Ray ray)
        {
            ray = default;
            if (_p == null || _p.Camera == null) return false;
            var cam = _p.Camera;
            if (!Target(cam, out var pos, out var rot)) return false;
            var t = cam.transform;
            Vector3 p0 = t.position;
            Quaternion r0 = t.rotation;
            float k = Mathf.SmoothStep(0f, 1f, _w);
            t.SetPositionAndRotation(Vector3.Lerp(p0, pos, k), Quaternion.Slerp(r0, rot, k));
            ray = cam.ScreenPointToRay(screenPoint);
            t.SetPositionAndRotation(p0, r0);
            return true;
        }

        /// <summary>Ray from the close-up camera to a world point (tests: a virtual mouse over that point).</summary>
        public static bool RayTo(Vector3 world, out Ray ray)
        {
            ray = default;
            if (_p == null || _p.Camera == null || !Target(_p.Camera, out var pos, out _)) return false;
            ray = new Ray(pos, (world - pos).normalized);
            return true;
        }
    }
}
