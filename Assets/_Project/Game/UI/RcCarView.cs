using HideAndSeek.Game.Net;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Local driver's view from the RC car: from the driver's seat inside the cockpit (RallyModel.DriverEye - through
    /// the windscreen over the dashboard, steering wheel in front; a load on the roof never blocks it; mouse looks
    /// around, springs back while driving),
    /// speed / bump shake, and an FPV-style overlay (REC dot, radio signal bars, static when the signal gets weak,
    /// controls). Takes the camera and the audio listener from the player and gives them back on <see cref="End"/>.
    /// </summary>
    public class RcCarView : MonoBehaviour
    {
        public static RcCarView Active { get; private set; }
        /// <summary>The car this view rides in.</summary>
        public RcCar Car => _car;

        private NetPlayer _me;
        private RcCar _car;
        private Camera _cam;
        private float _lookYaw, _lookPitch, _idleLook, _age;
        private bool _ended;
        private Texture2D _noise, _white;
        private float _nextNoise;
        private GUIStyle _style, _small;

        public static RcCarView Begin(NetPlayer me, RcCar car)
        {
            if (Active != null) Active.End();
            var go = new GameObject("RcCarCamera");
            var v = go.AddComponent<RcCarView>();
            v._me = me;
            v._car = car;
            v._cam = go.AddComponent<Camera>();
            if (me.Camera != null)
            {
                v._cam.CopyFrom(me.Camera);
                me.Camera.enabled = false;
                var l = me.Camera.GetComponent<AudioListener>();
                if (l != null) l.enabled = false;
            }
            v._cam.fieldOfView = 80f;
            v._cam.nearClipPlane = 0.004f; // the cockpit is a few centimetres round the eye
            v._cam.cullingMask = ~0; // the own (first-person hidden) body is visible from the car
            go.AddComponent<AudioListener>();
            go.tag = "MainCamera";
            Active = v;
            v.Place(1f);
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
            if (_noise != null) Destroy(_noise);
            if (_white != null) Destroy(_white);
        }

        private void LateUpdate()
        {
            if (_car == null || _me == null) { End(); return; }
            _age += Time.deltaTime;
            var mouse = Mouse.current;
            if (mouse != null && !LocalPlayerController.MenuOpen && Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 d = mouse.delta.ReadValue() * 0.1f * Mathf.Max(0.2f, LocalProfile.MouseSensitivity);
                _lookYaw = Mathf.Clamp(_lookYaw + d.x, -120f, 120f);
                _lookPitch = Mathf.Clamp(_lookPitch - d.y, -35f, 40f);
                _idleLook = d.sqrMagnitude > 0.01f ? 0f : _idleLook + Time.deltaTime;
            }
            // Look springs back to the front once driving and not using the mouse.
            if (Mathf.Abs(_car.Speed) > 0.4f && _idleLook > 0.6f)
            {
                float k = 1f - Mathf.Exp(-3f * Time.deltaTime);
                _lookYaw = Mathf.Lerp(_lookYaw, 0f, k);
                _lookPitch = Mathf.Lerp(_lookPitch, 0f, k);
            }
            Place(Mathf.Clamp01(Time.deltaTime * 60f));
        }

        private void Place(float k)
        {
            var root = _car.Parts.Root;
            float sp = Mathf.Abs(_car.Speed) / 5.8f;
            float shake = 0.18f * sp + 1.8f * _car.Bump;
            float t = Time.time;
            var jitter = Quaternion.Euler(shake * Mathf.Sin(t * 53f), shake * 0.6f * Mathf.Sin(t * 41f + 1f), shake * 0.8f * Mathf.Sin(t * 37f + 2f));
            // Driver's seat; the Yaris easter egg has no cockpit model (no steering wheel part): about the same place.
            Vector3 eye = _car.Parts.SteeringWheel != null ? HideAndSeek.Characters.RallyModel.DriverEye : new Vector3(-0.022f, 0.137f, -0.03f);
            transform.position = root.TransformPoint(eye);
            transform.rotation = root.rotation * Quaternion.Euler(3f + _lookPitch, _lookYaw, 0f) * jitter;
            if (_cam != null) _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, 80f + 8f * sp * (_car.Boost ? 1f : 0.5f), 1f - Mathf.Exp(-4f * Time.deltaTime));
        }

        // ================================================================ overlay

        private float Signal()
        {
            if (_me == null || _car == null) return 1f;
            Vector3 d = _car.transform.position - _me.transform.position;
            d.y *= 2f; // floors in between hurt the signal
            return Mathf.Clamp01(1f - d.magnitude / MatchManager.RcRangeOf(_car.Index)); // wire antenna: MatchManager.RcWire
        }

        private void OnGUI()
        {
            if (_car == null || Event.current.type != EventType.Repaint) return;
            if (_white == null) { _white = new Texture2D(1, 1); _white.SetPixel(0, 0, Color.white); _white.Apply(); }
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(Screen.height / 34f), fontStyle = FontStyle.Bold };
                _small = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(Screen.height / 50f), alignment = TextAnchor.LowerCenter };
            }
            float w = Screen.width, h = Screen.height;
            float sig = Signal();

            // Static: always a hint of it, heavy when the signal is weak; full snow for a moment when the feed starts.
            float snow = Mathf.Max(0.04f, Mathf.Clamp01((0.3f - sig) / 0.3f) * 0.85f, Mathf.Clamp01(1f - _age / 0.35f));
            if (_noise == null) _noise = new Texture2D(160, 90, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            if (Time.time >= _nextNoise)
            {
                _nextNoise = Time.time + 0.05f;
                var px = new Color32[160 * 90];
                for (int i = 0; i < px.Length; i++)
                {
                    byte v = (byte)Random.Range(0, 256);
                    px[i] = new Color32(v, v, v, 255);
                }
                _noise.SetPixels32(px);
                _noise.Apply(false);
            }
            GUI.color = new Color(1f, 1f, 1f, snow);
            GUI.DrawTexture(new Rect(0, 0, w, h), _noise);
            // Scanlines.
            GUI.color = new Color(0f, 0f, 0f, 0.12f);
            for (float y = 0; y < h; y += 4f) GUI.DrawTexture(new Rect(0, y, w, 1f), _white);
            // Corner brackets.
            GUI.color = new Color(1f, 1f, 1f, 0.75f);
            float m = h * 0.05f, L = h * 0.06f, th = Mathf.Max(2f, h / 360f);
            foreach (var (x, y, sx, sy) in new[] { (m, m, 1f, 1f), (w - m, m, -1f, 1f), (m, h - m, 1f, -1f), (w - m, h - m, -1f, -1f) })
            {
                GUI.DrawTexture(new Rect(sx > 0 ? x : x - L, sy > 0 ? y : y - th, L, th), _white);
                GUI.DrawTexture(new Rect(sx > 0 ? x : x - th, sy > 0 ? y : y - L, th, L), _white);
            }
            // REC dot + label.
            bool blink = Mathf.Repeat(Time.time, 1f) < 0.6f;
            GUI.color = new Color(1f, 0.15f, 0.12f, blink ? 0.95f : 0.25f);
            float dot = h * 0.022f;
            GUI.DrawTexture(new Rect(m + h * 0.02f, m + h * 0.025f, dot, dot), _white);
            GUI.color = Color.white;
            GUI.Label(new Rect(m + h * 0.05f, m + h * 0.012f, w * 0.4f, h * 0.06f), "RC CAM", _style);
            // Signal bars.
            for (int i = 0; i < 4; i++)
            {
                float bh = h * (0.015f + 0.01f * i), bw = h * 0.011f;
                bool on = sig > i * 0.25f + 0.02f;
                GUI.color = on ? (sig < 0.3f ? new Color(1f, 0.35f, 0.25f, 0.95f) : new Color(0.6f, 1f, 0.6f, 0.95f)) : new Color(1f, 1f, 1f, 0.2f);
                GUI.DrawTexture(new Rect(w - m - h * 0.08f + i * bw * 1.6f, m + h * 0.06f - bh, bw, bh), _white);
            }
            GUI.color = Color.white;
            string spd = $"{Mathf.Abs(_car.Speed) * 3.6f:0} km/h";
            GUI.Label(new Rect(w - m - h * 0.3f, h - m - h * 0.08f, h * 0.28f, h * 0.06f), spd, _style);
            if (sig < 0.3f)
            {
                GUI.color = new Color(1f, 0.4f, 0.3f, blink ? 1f : 0.5f);
                GUI.Label(new Rect(w * 0.5f - h * 0.3f, h * 0.2f, h * 0.6f, h * 0.06f), "SŁABY SYGNAŁ", _style);
            }
            DrawCharge(w, h, m, blink);
            GUI.color = new Color(1f, 1f, 1f, 0.85f);
            GUI.Label(new Rect(0, h - m - h * 0.06f, w, h * 0.05f),
                "W A S D - jazda    Shift - turbo    R - prąd    H - klakson    T - światła    mysz - rozglądanie    6 - wróć do postaci", _small);
            GUI.color = Color.white;
        }

        /// <summary>Energising (R): charge bar with the seconds left, then the cooldown; blue flicker on the edges while charged.</summary>
        private void DrawCharge(float w, float h, float m, bool blink)
        {
            var mm = MatchManager.Instance;
            if (mm == null || _car.Index < 0) return;
            int i = _car.Index;
            float left = mm.RcChargeLeft(i), cd = mm.RcChargeCooldown(i);
            // Bottom right, above the speed (bottom left is the minimap).
            float bw = h * 0.3f, bh = h * 0.018f, x = w - m - bw, y = h - m - h * 0.2f;
            string label;
            float fill;
            Color col;
            if (left > 0f)
            {
                label = $"PRĄD  {left:0.0} s";
                fill = left / RcPower.ChargeTime;
                col = new Color(0.55f, 0.8f, 1f, 0.95f);
                // Blue flicker on the screen edges (the camera sits in the arcs).
                float e = 0.12f + 0.18f * Random.value;
                GUI.color = new Color(0.45f, 0.7f, 1f, e);
                float th = h * 0.012f;
                GUI.DrawTexture(new Rect(0, 0, w, th), _white);
                GUI.DrawTexture(new Rect(0, h - th, w, th), _white);
                GUI.DrawTexture(new Rect(0, 0, th, h), _white);
                GUI.DrawTexture(new Rect(w - th, 0, th, h), _white);
            }
            else if (cd > 0f)
            {
                label = $"PRĄD za {Mathf.CeilToInt(cd)} s";
                fill = 1f - cd / RcPower.Cooldown;
                col = new Color(1f, 1f, 1f, 0.55f);
            }
            else
            {
                label = "R - PRĄD gotowy";
                fill = 1f;
                col = new Color(0.55f, 0.8f, 1f, blink ? 0.95f : 0.7f);
            }
            GUI.color = new Color(0f, 0f, 0f, 0.45f);
            GUI.DrawTexture(new Rect(x, y, bw, bh), _white);
            GUI.color = col;
            GUI.DrawTexture(new Rect(x, y, bw * Mathf.Clamp01(fill), bh), _white);
            GUI.Label(new Rect(x, y - h * 0.055f, bw + m, h * 0.05f), label, _style);
        }
    }
}
