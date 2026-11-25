using System;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Wall TV showing the parking garage CCTV feeds (images rendered from the real map at build time):
    /// switches camera every few seconds with a static burst, slight jitter, live clock and a blinking REC dot.
    /// In the lobby it can also play a YouTube video (<see cref="LobbyTvVideo"/>, added at the start; the dead-player lobby
    /// copy disables this component before its start and shows its own feed): while the video owns the screen the CCTV
    /// loop stands still.
    /// </summary>
    public class LobbyTv : MonoBehaviour
    {
        public Renderer screen;
        public Texture2D[] feeds;
        public string[] labels;
        public TextMesh overlay;
        public Light glow;

        private int _index;
        private float _next, _static;
        private Material _mat;
        private Texture2D _noise;
        private Color32[] _noisePx;
        private LobbyTvVideo _video;
        private bool _owned;

        /// <summary>The screen's own material instance (the video draws into it while it owns the screen).</summary>
        public Material ScreenMaterial => _mat;

        private void Start()
        {
            if (screen != null) _mat = screen.material;
            _noise = new Texture2D(64, 36, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            _noisePx = new Color32[64 * 36];
            Show(0);
            _next = Time.time + 6f;
            if (gameObject.scene.name != DeadLobby.SceneName) _video = LobbyTvVideo.Attach(this);
        }

        private void Show(int i)
        {
            if (feeds == null || feeds.Length == 0 || _mat == null) return;
            _index = (i % feeds.Length + feeds.Length) % feeds.Length;
            _mat.mainTexture = feeds[_index];
            _static = 0.25f;
        }

        private void Update()
        {
            if (_mat == null) return;
            if (_video != null && _video.OwnsScreen)
            {
                _owned = true;
                return;
            }
            if (_owned)
            {
                _owned = false;
                _static = 0f;
                Show(_index);
            }
            if (Time.time >= _next)
            {
                _next = Time.time + 6f;
                Show(_index + 1);
            }
            if (_static > 0f)
            {
                _static -= Time.deltaTime;
                for (int i = 0; i < _noisePx.Length; i++)
                {
                    byte v = (byte)UnityEngine.Random.Range(0, 230);
                    _noisePx[i] = new Color32(v, v, v, 255);
                }
                _noise.SetPixels32(_noisePx);
                _noise.Apply(false);
                _mat.mainTexture = _noise;
                if (_static <= 0f) _mat.mainTexture = feeds[_index];
            }
            // tiny vertical jitter now and then
            _mat.mainTextureOffset = new Vector2(0f, UnityEngine.Random.value < 0.02f ? UnityEngine.Random.Range(-0.01f, 0.01f) : 0f);
            if (overlay != null)
            {
                string label = labels != null && _index < labels.Length ? labels[_index] : $"CAM {_index + 1}";
                bool rec = (int)(Time.time * 1.5f) % 2 == 0;
                overlay.text = $"{label}\n{DateTime.Now:HH:mm:ss}   {(rec ? "REC" : "   ")}";
            }
            if (glow != null) glow.intensity = 0.55f + 0.08f * Mathf.PerlinNoise(Time.time * 4f, 0f);
        }
    }
}
