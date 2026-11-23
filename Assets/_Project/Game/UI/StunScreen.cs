using HideAndSeek.Characters;
using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using UnityEngine;
using UnityEngine.UI;

namespace HideAndSeek.Game.UI
{
    /// <summary>
    /// Screen of the knocked-out seeker (<see cref="StunTimeline"/>): it goes black fast as he faints, stays black while
    /// he lies there and comes back slowly through a blur and double vision (<see cref="DrunkVision"/> on the camera).
    /// Sound keeps playing all the time, only muffled a little (low-pass on the active listener) while out. Over the HUD,
    /// under the menu (softened while the menu is open).
    /// </summary>
    public sealed class StunScreen : MonoBehaviour
    {
        private static StunScreen _instance;
        private Image _black;
        private Text _label;
        private float _t = -1f, _shownT = -1f;
        private Camera _cam;
        private DrunkVision _fx;
        private AudioLowPassFilter _lp;
        private AudioListener _lpOn;

        /// <summary>Owner, every frame: <paramref name="t"/> = seconds since the knock-out, &lt; 0 = none.</summary>
        public static void Show(float t, Camera cam)
        {
            if (_instance == null)
            {
                if (t < 0f) return;
                var go = new GameObject("[StunScreen]");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<StunScreen>();
            }
            _instance._t = t;
            if (cam != null) _instance._cam = cam;
        }

        /// <summary>Test: 0..1 black right now.</summary>
        public static float Black => _instance != null && _instance._black != null ? _instance._black.color.a : 0f;

        private void Awake()
        {
            var canvas = UiKit.CreateCanvas("StunCanvas", 12, transform);
            _black = UiKit.Image("StunBlack", canvas.transform, new Color(0f, 0f, 0f, 0f));
            _black.raycastTarget = false;
            _black.rectTransform.Stretch();
            _label = UiKit.Label("StunLabel", canvas.transform, "ogłuszony...", 26, TextAnchor.MiddleCenter, new Color(0.55f, 0.52f, 0.5f, 0f), FontStyle.Italic);
            _label.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -60f), new Vector2(600f, 40f));
        }

        private void LateUpdate()
        {
            float t = _t;
            // Keeps the blur's tail a moment after the controls are back.
            if (t >= 0f) _shownT = t;
            else if (_shownT >= 0f) _shownT = _shownT < StunTimeline.Duration - 0.2f ? -1f : _shownT + Time.deltaTime;
            float s = _shownT;
            if (s > StunTimeline.BlurEnd) s = _shownT = -1f;

            float dark = StunTimeline.Darkness(s);
            if (LocalPlayerController.MenuOpen) dark *= 0.55f;
            _black.color = new Color(0f, 0f, 0f, dark);
            float label = s >= 0f ? StunTimeline.Seg(s, StunTimeline.DarkFull, StunTimeline.DarkFull + 0.6f) * (1f - StunTimeline.Seg(s, StunTimeline.LightStart - 0.5f, StunTimeline.LightStart)) : 0f;
            _label.color = new Color(0.55f, 0.52f, 0.5f, 0.55f * label);

            float blur = StunTimeline.Blur(s);
            if (blur > 0.002f && _cam != null)
            {
                if (_fx == null)
                {
                    _fx = _cam.GetComponent<DrunkVision>();
                    if (_fx == null) _fx = _cam.gameObject.AddComponent<DrunkVision>();
                }
                _fx.Amount = 0.75f * blur;
                _fx.Faint = blur;
                _fx.Clock = Time.time;
                _fx.Seed = 3.1f;
                _fx.enabled = true;
            }
            else if (_fx != null)
            {
                _fx.Amount = 0f;
                _fx.Faint = 0f;
                _fx.enabled = false;
                _fx = null;
            }

            // Muffled hearing while out (everything still plays, voice chat included).
            float muffle = s >= 0f ? Mathf.Clamp01(StunTimeline.Darkness(s) * 1.1f) : 0f;
            var listener = ActiveListener.Find();
            if (muffle > 0.001f && listener != null)
            {
                if (_lpOn != listener)
                {
                    if (_lp != null) _lp.enabled = false;
                    _lp = listener.GetComponent<AudioLowPassFilter>();
                    if (_lp == null) _lp = listener.gameObject.AddComponent<AudioLowPassFilter>();
                    _lpOn = listener;
                }
                _lp.enabled = true;
                _lp.cutoffFrequency = Mathf.Lerp(22000f, 1500f, muffle);
                _lp.lowpassResonanceQ = 1f;
            }
            else if (_lp != null && _lp.enabled)
            {
                _lp.cutoffFrequency = 22000f;
                _lp.enabled = false;
            }
        }
    }
}
