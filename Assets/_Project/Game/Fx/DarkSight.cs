using HideAndSeek.Game.Net;
using HideAndSeek.Map;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Minimal sight in the dark: a very faint, short-range light just above the local player's eyes, so that
    /// without a flashlight the floor, a wall or a car within ~3 m is barely readable (dark grey, no colour pop)
    /// and everything further away stays black. Local only (other players never see it), only in the garage
    /// (<see cref="LevelLighting"/> present) and only while the player's own camera renders - the spectator's night
    /// vision, the kill cam and the RC car view have their own lighting. Hiders and the seeker get the same.
    /// Test override: <c>-darksight 0</c> switches it off, <c>-darksight 1.5</c> scales it.
    /// </summary>
    public sealed class DarkSight : MonoBehaviour
    {
        public const float Range = 6f, Intensity = 1.1f;
        private static readonly Color Tint = new Color(0.78f, 0.82f, 0.9f);

        private Light _light;
        private float _scale = 1f;

        public static DarkSight Instance { get; private set; }
        public bool IsOn => _light != null && _light.enabled;
        /// <summary>Strength multiplier (0 = off), for tests.</summary>
        public float Scale { get => _scale; set => _scale = Mathf.Max(0f, value); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (Instance != null) return;
            var go = new GameObject("DarkSight");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<DarkSight>();
        }

        private void Awake()
        {
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-darksight");
            if (i >= 0 && i + 1 < args.Length && float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float s))
                _scale = Mathf.Max(0f, s);
        }

        private void LateUpdate()
        {
            var me = NetPlayer.Local;
            var cam = me != null ? me.Camera : null;
            bool on = _scale > 0f && LevelLighting.Instance != null && cam != null && cam.enabled && cam.gameObject.activeInHierarchy;
            if (!on)
            {
                if (_light != null) _light.enabled = false;
                return;
            }
            if (_light == null)
            {
                _light = new GameObject("DarkSightLight").AddComponent<Light>();
                _light.transform.SetParent(transform, false);
                _light.type = LightType.Point;
                _light.shadows = LightShadows.None;
                _light.renderMode = LightRenderMode.ForcePixel;   // as a vertex light it would smear over the big floor triangles
                _light.cullingMask = ~(1 << PlayerAvatarBinder.LocalBodyLayer);
            }
            _light.range = Range;
            _light.intensity = Intensity * _scale;
            _light.color = Tint;
            // a little above the eyes: tops of things catch slightly more light than faces, no flat "camera flash" look
            _light.transform.position = cam.transform.position + Vector3.up * 0.35f;
            _light.enabled = true;
        }
    }
}
