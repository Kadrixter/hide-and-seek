using HideAndSeek.Characters;
using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using UnityEngine;
using UnityEngine.UI;

namespace HideAndSeek.Game.UI
{
    /// <summary>
    /// Third-person cinematic camera for kill animations: slow orbit around the seeker and the victim with
    /// letterbox bars. Normal kill variants use it for the two players involved; the finisher (last hider)
    /// is shown to everybody with a title and a closing hero shot of the seeker's taunt.
    /// The gameplay camera stops rendering while it runs (its AudioListener keeps working).
    /// </summary>
    public class KillCam : MonoBehaviour
    {
        public static bool Active => _current != null;
        private static KillCam _current;

        private NetPlayer _seeker, _victim;
        private float _duration, _t;
        private bool _finisher, _hero;
        private string _titleText;
        private Camera _cam, _hidden;
        private float _orbitStart, _orbitSpan;
        private RectTransform _barTop, _barBottom;
        private Text _title, _sub;
        private CanvasGroup _titleGroup;
        private Vector3 _smoothPos;
        private bool _hasPos;
        private readonly RaycastHit[] _hits = new RaycastHit[16];
        private Light _key, _fill;

        /// <param name="showcase">Big show for everybody (last hider): wider orbit, title, red light.</param>
        /// <param name="heroShot">End on the seeker's taunt (knife finisher).</param>
        public static void Play(NetPlayer seeker, NetPlayer victim, float duration, bool showcase, bool heroShot, string title)
        {
            if (seeker == null || victim == null) return;
            if (_current != null) _current.Stop();
            var go = new GameObject(showcase ? "KillCam (finisher)" : "KillCam");
            var kc = go.AddComponent<KillCam>();
            kc._hero = heroShot;
            kc._titleText = title ?? "";
            kc.Begin(seeker, victim, duration, showcase);
        }

        private void Begin(NetPlayer seeker, NetPlayer victim, float duration, bool finisher)
        {
            _current = this;
            _seeker = seeker;
            _victim = victim;
            _duration = duration;
            _finisher = finisher;

            _hidden = Camera.main;
            _cam = gameObject.AddComponent<Camera>();
            if (_hidden != null)
            {
                _cam.CopyFrom(_hidden);
                _hidden.enabled = false;
            }
            _cam.depth = 50f;
            _cam.fieldOfView = finisher ? 50f : 55f;
            _cam.nearClipPlane = 0.05f;
            _cam.cullingMask = ~(1 << 9) | (1 << PlayerAvatarBinder.LocalBodyLayer); // no first-person arms, own body visible

            NetPlayer.Local?.SetCinematicBody(true);

            // Orbit on the side the camera already is (less jarring), sweeping around the pair.
            Vector3 mid = Mid();
            Vector3 axis = Axis();
            Vector3 side = Vector3.Cross(Vector3.up, axis);
            Vector3 from = _hidden != null ? _hidden.transform.position - mid : side;
            float sgn = Vector3.Dot(from, side) >= 0f ? 1f : -1f;
            _orbitStart = Mathf.Atan2(side.x * sgn, side.z * sgn) * Mathf.Rad2Deg;
            _orbitSpan = (finisher ? 75f : 40f) * (Random.value < 0.5f ? -1f : 1f);

            // The garage is pitch dark: a soft key light from the camera side and a dim fill from above the pair.
            _key = new GameObject("KillCamKey").AddComponent<Light>();
            _key.transform.SetParent(transform, false);
            _key.type = LightType.Spot;
            _key.spotAngle = 70f;
            _key.range = 9f;
            _key.intensity = finisher ? 2.2f : 1.8f;
            _key.color = finisher ? new Color(1f, 0.82f, 0.75f) : new Color(1f, 0.92f, 0.85f);
            _key.shadows = LightShadows.Soft;
            _fill = new GameObject("KillCamFill").AddComponent<Light>();
            _fill.transform.SetParent(transform, false);
            _fill.type = LightType.Point;
            _fill.range = 4.5f;
            _fill.intensity = finisher ? 1.1f : 0.8f;
            _fill.color = finisher ? new Color(0.9f, 0.25f, 0.2f) : new Color(0.6f, 0.7f, 0.9f);

            BuildOverlay();
            LateUpdate();
        }

        private void BuildOverlay()
        {
            var canvas = UiKit.CreateCanvas("KillCamOverlay", 400, transform);
            var root = canvas.transform;
            _barTop = UiKit.Image("Top", root, Color.black).rectTransform;
            _barTop.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 0));
            _barBottom = UiKit.Image("Bottom", root, Color.black).rectTransform;
            _barBottom.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 0));

            var titleRoot = UiKit.Rect("Title", root);
            titleRoot.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 4), new Vector2(1400, 130));
            _titleGroup = titleRoot.gameObject.AddComponent<CanvasGroup>();
            _titleGroup.alpha = 0f;
            _title = UiKit.Label("T", titleRoot, _titleText, 64, TextAnchor.UpperCenter, new Color(0.9f, 0.12f, 0.1f), FontStyle.Bold);
            _title.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 76));
            _title.AddOutline(3f);
            _sub = UiKit.Label("S", titleRoot, _finisher ? $"{_seeker.Nick} kończy z {_victim.Nick}" : "", 28, TextAnchor.UpperCenter);
            _sub.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 48));
            _sub.AddOutline();
        }

        private Vector3 Mid()
        {
            Vector3 a = _seeker != null ? _seeker.transform.position : Vector3.zero;
            Vector3 b = _victim != null ? _victim.transform.position : a;
            return (a + b) * 0.5f + Vector3.up * 0.95f;
        }

        /// <summary>Horizontal direction seeker -> victim.</summary>
        private Vector3 Axis()
        {
            if (_seeker == null || _victim == null) return Vector3.forward;
            Vector3 d = _victim.transform.position - _seeker.transform.position;
            d.y = 0f;
            return d.sqrMagnitude > 0.001f ? d.normalized : _seeker.transform.forward;
        }

        private void LateUpdate()
        {
            if (_seeker == null || _victim == null || _cam == null)
            {
                Stop();
                return;
            }
            _t += Time.deltaTime;
            float x = Mathf.Clamp01(_t / _duration);

            Vector3 mid = Mid();
            Vector3 target = mid;
            Vector3 desired;
            if (_hero && _t >= KillMoves.FinisherTauntTime - 0.6f)
            {
                // Hero shot: low, in front of the seeker, slightly off-axis.
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((_t - (KillMoves.FinisherTauntTime - 0.6f)) / 1.2f));
                Vector3 s = _seeker.transform.position;
                Vector3 fwd = _seeker.transform.forward;
                Vector3 right = _seeker.transform.right;
                Vector3 hero = s + fwd * 2.3f + right * 0.8f + Vector3.up * 0.75f;
                Vector3 orbit = OrbitPos(mid, x);
                desired = Vector3.Lerp(orbit, hero, k);
                target = Vector3.Lerp(mid, s + Vector3.up * 1.35f, k);
            }
            else desired = OrbitPos(mid, x);

            desired = AvoidWalls(target, desired);
            if (!_hasPos) { _smoothPos = desired; _hasPos = true; }
            _smoothPos = Vector3.Lerp(_smoothPos, desired, 1f - Mathf.Exp(-Time.deltaTime * 6f));
            transform.position = _smoothPos;
            transform.rotation = Quaternion.LookRotation(target - _smoothPos);
            if (_key != null) _key.transform.rotation = Quaternion.LookRotation(target - _key.transform.position);
            if (_fill != null) _fill.transform.position = mid + Vector3.up * 1.4f - Axis() * 0.6f;

            // Letterbox in / out, title.
            float bars = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_t / 0.4f)) * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((_duration - _t) / 0.4f));
            float h = Screen.height * (_finisher ? 0.13f : 0.08f) * bars;
            var canvasRt = (RectTransform)_barTop.parent;
            float scale = canvasRt.rect.height / Mathf.Max(1f, Screen.height);
            _barTop.sizeDelta = new Vector2(0, h * scale);
            _barBottom.sizeDelta = new Vector2(0, h * scale);
            if (_finisher)
                _titleGroup.alpha = _hero
                    ? Mathf.Clamp01((_t - 0.3f) / 0.5f) * Mathf.Clamp01((2.6f - _t) / 0.6f)
                      + Mathf.Clamp01((_t - KillMoves.FinisherTauntTime) / 0.4f) * Mathf.Clamp01((_duration - 0.3f - _t) / 0.5f)
                    : Mathf.Clamp01((_t - 0.3f) / 0.5f) * Mathf.Clamp01((_duration - 0.3f - _t) / 0.5f);

            if (_t >= _duration) Stop();
        }

        private Vector3 OrbitPos(Vector3 mid, float x)
        {
            float ang = (_orbitStart + _orbitSpan * Mathf.SmoothStep(0f, 1f, x)) * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang));
            float dist = _finisher ? Mathf.Lerp(2.4f, 2.0f, x) : 2.7f;
            float height = _finisher ? Mathf.Lerp(0.15f, 0.45f, x) : 0.5f;
            return mid + dir * dist + Vector3.up * height;
        }

        /// <summary>Pulls the camera in front of walls / cars between the pair and the desired position.</summary>
        private Vector3 AvoidWalls(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            float len = d.magnitude;
            if (len < 0.01f) return to;
            int n = Physics.SphereCastNonAlloc(from, 0.2f, d / len, _hits, len, ~((1 << 8) | (1 << 9)), QueryTriggerInteraction.Ignore);
            float best = len;
            for (int i = 0; i < n; i++)
            {
                var hit = _hits[i];
                if (hit.distance <= 0f || hit.collider.GetComponentInParent<NetPlayer>() != null) continue;
                best = Mathf.Min(best, hit.distance);
            }
            return from + d / len * Mathf.Max(0.6f, best - 0.1f);
        }

        private void Stop()
        {
            if (_current == this) _current = null;
            var me = NetPlayer.Local;
            if (_hidden != null)
            {
                // A dead local player is watched through the spectator camera instead.
                bool keepOff = me != null && me.Life.Value == LifeState.Dead && _hidden == me.Camera;
                if (!keepOff) _hidden.enabled = true;
            }
            me?.SetCinematicBody(false);
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_current == this)
            {
                _current = null;
                if (_hidden != null) _hidden.enabled = true;
                NetPlayer.Local?.SetCinematicBody(false);
            }
        }
    }
}
