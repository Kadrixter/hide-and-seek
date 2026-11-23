using System.Collections.Generic;
using HideAndSeek.Characters;
using HideAndSeek.Game.Net;
using HideAndSeek.Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HideAndSeek.Game.Cinematics
{
    /// <summary>
    /// Camera of the round intro / outro: renders over everything (the gameplay, spectator and kill cameras are
    /// switched off while it runs and switched back on after), letterbox bars, a black fade, a title line and a
    /// procedural shake. The HUD is hidden by <see cref="HudHider"/>. The AudioListener stays where it was.
    /// </summary>
    public sealed class CineCamera : MonoBehaviour
    {
        public Camera Cam { get; private set; }
        public float Bars, Fade, ShakeAmount;
        public Text Title, Sub;
        public CanvasGroup TitleGroup;
        public RectTransform Overlay;

        RectTransform _top, _bottom;
        Image _black;
        readonly List<Camera> _off = new();
        Vector3 _pos;
        Quaternion _rot = Quaternion.identity;
        float _fov = 50f, _t;

        public static CineCamera Create(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<CineCamera>();
            c.Setup();
            return c;
        }

        void Setup()
        {
            Cam = gameObject.AddComponent<Camera>();
            var main = NetPlayer.Local != null && NetPlayer.Local.Camera != null ? NetPlayer.Local.Camera : Camera.main;
            if (main != null) Cam.CopyFrom(main);
            Cam.depth = 60f;
            Cam.nearClipPlane = 0.03f;
            Cam.farClipPlane = 220f;
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = new Color(0.008f, 0.01f, 0.014f);
            Cam.cullingMask = ~(1 << 9); // no first-person arms
            Cam.enabled = true;
            _pos = transform.position;
            _rot = transform.rotation;

            var canvas = UiKit.CreateCanvas("CineOverlay", 420, transform);
            Overlay = (RectTransform)canvas.transform;
            _black = UiKit.Image("Fade", Overlay, Color.black);
            _black.rectTransform.Stretch();
            _black.raycastTarget = false;
            _top = UiKit.Image("BarTop", Overlay, Color.black).rectTransform;
            _top.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, Vector2.zero);
            _bottom = UiKit.Image("BarBottom", Overlay, Color.black).rectTransform;
            _bottom.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, Vector2.zero);

            var tr = UiKit.Rect("Title", Overlay);
            tr.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(1600, 220));
            TitleGroup = tr.gameObject.AddComponent<CanvasGroup>();
            TitleGroup.alpha = 0f;
            Title = UiKit.Label("T", tr, "", 76, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            Title.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(0, 100));
            Title.AddOutline(2f, 0.6f);
            Sub = UiKit.Label("S", tr, "", 36, TextAnchor.MiddleCenter, CineStyle.Accent);
            Sub.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -42), new Vector2(0, 50));
            Sub.AddOutline(1.5f, 0.6f);

            NetPlayer.Local?.SetCinematicBody(true);
            HudHider.Push();
            UiFonts.Apply(Overlay); // menu typeface (Montserrat) for the cinematic titles
            LateUpdate();
        }

        /// <summary>Where the camera should be this frame (shake is added on top).</summary>
        public void Set(Vector3 pos, Quaternion rot, float fov)
        {
            _pos = pos;
            _rot = rot;
            _fov = fov;
        }

        public void LookAt(Vector3 pos, Vector3 target, float fov, float roll = 0f) =>
            Set(pos, Quaternion.LookRotation(target - pos) * Quaternion.Euler(0f, 0f, roll), fov);

        void LateUpdate()
        {
            _t += Time.deltaTime;
            HudHider.Apply();
            // Keep every other camera off (spectator / kill / emote cameras may come and go meanwhile).
            foreach (var c in Camera.allCameras)
            {
                if (c == Cam || !c.enabled || c.targetTexture != null) continue;
                c.enabled = false;
                if (!_off.Contains(c)) _off.Add(c);
            }
            float s = ShakeAmount;
            Vector3 shake = Vector3.zero;
            Quaternion shakeRot = Quaternion.identity;
            if (s > 0.0005f)
            {
                float f = _t * 31f;
                shake = new Vector3(Noise(f, 1.3f), Noise(f, 7.1f), Noise(f, 3.7f)) * 0.045f * s;
                shakeRot = Quaternion.Euler(Noise(f, 11f) * 1.6f * s, Noise(f, 5f) * 1.2f * s, Noise(f, 9f) * 2.2f * s);
            }
            transform.SetPositionAndRotation(_pos + _rot * shake, _rot * shakeRot);
            Cam.fieldOfView = _fov;

            var canvasRt = Overlay;
            float scale = canvasRt.rect.height / Mathf.Max(1f, Screen.height);
            float h = Screen.height * 0.105f * Mathf.Clamp01(Bars) * scale;
            _top.sizeDelta = new Vector2(0, h);
            _bottom.sizeDelta = new Vector2(0, h);
            _black.color = new Color(0f, 0f, 0f, Mathf.Clamp01(Fade));
            if (_black.transform.GetSiblingIndex() != Overlay.childCount - 1) _black.transform.SetAsLastSibling();
            _black.enabled = Fade > 0.001f;
        }

        static float Noise(float t, float seed) => (Mathf.PerlinNoise(t, seed) - 0.5f) * 2f;

        /// <summary>Gives the screen back: the cameras it switched off come back on, the HUD is shown again.</summary>
        public void Release()
        {
            if (_released) return;
            _released = true;
            var me = NetPlayer.Local;
            foreach (var c in _off)
            {
                if (c == null) continue;
                // A dead local player keeps looking through the spectator camera, not the own eyes.
                if (me != null && c == me.Camera && me.Life.Value == HideAndSeek.Core.LifeState.Dead && FindAnyObjectByType<SpectatorCamera>() != null) continue;
                c.enabled = true;
            }
            _off.Clear();
            me?.SetCinematicBody(false);
            HudHider.Pop();
        }

        bool _released;

        void OnDestroy() => Release();
    }

    /// <summary>Colours of the cinematic texts (decision 23.11: white text, sea-green accent).</summary>
    public static class CineStyle
    {
        public static readonly Color Accent = new Color(0.247f, 0.722f, 0.659f); // #3fb8a8
        public static readonly Color SeekerAccent = new Color(0.92f, 0.3f, 0.26f);
        public static readonly Color Dim = new Color(0.78f, 0.82f, 0.82f);
    }
}
