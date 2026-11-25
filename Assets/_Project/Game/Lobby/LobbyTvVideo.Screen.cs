using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Lobby TV video: what the screen shows around the picture (a big centred line: loading with the download progress,
    /// waiting for the others, pause, a Polish error message; the corner display: "YouTube" and the time), the 3D sound
    /// source at the screen and the boombox ducking while the TV plays.
    /// </summary>
    public partial class LobbyTvVideo
    {
        /// <summary>Factor on the lobby boombox's volume: lower while the TV plays a video (smoothed).</summary>
        public static float RadioDuck { get; private set; } = 1f;
        public const float DuckLevel = 0.3f;

        private TextMesh _status;
        private AudioSource _src;
        private BoomboxAudio _out;
        private Vector3 _center, _normal;
        private float _shownPos;

        /// <summary>Middle of the screen and its normal towards the room (prompt, panel tests).</summary>
        public Vector3 ScreenCenter => _center;
        public Vector3 ScreenNormal => _normal;
        /// <summary>Screen size in metres (the quad's scale).</summary>
        public Vector2 ScreenSize => _tv != null && _tv.screen != null ? (Vector2)_tv.screen.transform.lossyScale : Vector2.one;

        // ---- tests
        public bool Prepared => _prepared;
        public bool VideoPlaying => _vp != null && _vp.isPlaying;
        public double VideoTime => _vp != null ? _vp.time : -1d;
        public float Speed => _speed;
        public float AudioPlayhead => _stream != null ? _stream.Position : -1f;
        public bool AudioPlaying => _stream != null && _stream.Playing;
        public float AudioLevel => _out != null ? _out.Level : 0f;
        public string StatusLine => _status != null && _status.gameObject.activeSelf ? _status.text.Replace("\n", " ") : "";
        public string ErrorText => _url != null ? VideoFailure : Time.unscaledTime < _errorUntil ? _error : null;
        public string Title => _dl != null ? _dl.Title : null;

        private void BuildScreen()
        {
            var screen = _tv.screen;
            _center = screen.bounds.center;
            Vector3 n = screen.transform.forward;
            if (Vector3.Dot(n, new Vector3(0f, 1.5f, 0f) - _center) < 0f) n = -n;
            _normal = n;
            Vector2 size = ScreenSize;

            var go = new GameObject("TvStatus");
            go.transform.SetParent(transform, true);
            go.transform.SetPositionAndRotation(_center + n * 0.012f, _tv.overlay != null ? _tv.overlay.transform.rotation : screen.transform.rotation);
            _status = go.AddComponent<TextMesh>();
            _status.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _status.fontSize = 64;
            _status.fontStyle = FontStyle.Bold;
            _status.characterSize = (_tv.overlay != null ? _tv.overlay.characterSize : 0.012f) * 1.9f;
            _status.anchor = TextAnchor.MiddleCenter;
            _status.alignment = TextAlignment.Center;
            _status.color = Color.white;
            _status.text = "";
            WorldText.Setup(_status, new Vector2(size.x * 0.86f, size.y * 0.42f));
            go.SetActive(false);

            var sp = new GameObject("TvSpeaker");
            sp.transform.SetParent(transform, true);
            sp.transform.position = _center + n * 0.25f - Vector3.up * 0.3f;
            _src = sp.AddComponent<AudioSource>();
            _src.spatialBlend = 0.8f;
            _src.rolloffMode = AudioRolloffMode.Linear;
            _src.minDistance = 4f;
            _src.maxDistance = 24f;
            _src.dopplerLevel = 0f;
            _src.priority = 40;
            _src.volume = 0f;
            _out = sp.AddComponent<BoomboxAudio>(); // the constant clip its filter multiplies by the sound
            _src.Play();
        }

        /// <summary>TV volume (shared, 0..100 on the channel) times the player's own music setting; boombox ducking.</summary>
        private void UpdateAudio(in RadioState r)
        {
            float music = Mathf.Clamp01(HideAndSeek.Audio.GameAudio.MusicVolume / 0.7f);
            // Sound only with the picture: never while it is still loading / cannot be shown (it then joins on the clock).
            bool picture = _url != null && _prepared && VideoFailure == null;
            if (_src != null) _src.volume = picture ? r.Volume / 100f * 0.9f * music : 0f;
            bool audible = picture && r.Playing && !r.Loading && r.Volume > 0;
            RadioDuck = Mathf.MoveTowards(RadioDuck, audible ? DuckLevel : 1f, Time.unscaledDeltaTime * 1.5f);
        }

        private void UpdateScreen(in RadioState r, string url)
        {
            var mat = _tv != null ? _tv.ScreenMaterial : null;
            if (mat == null) return;
            if (!OwnsScreen)
            {
                if (_status.gameObject.activeSelf) _status.gameObject.SetActive(false);
                return;
            }
            if (mat.mainTexture != _rt) mat.mainTexture = _rt;
            mat.mainTextureOffset = Vector2.zero;
            var mm = MatchManager.Instance;
            if (url != null && mm != null) _shownPos = Mathf.Max(0f, Position(mm, r));

            string fail = url != null ? VideoFailure : _error;
            string line;
            Color c = Color.white;
            if (fail != null)
            {
                line = "Nie można odtworzyć\n" + fail;
                c = new Color(1f, 0.55f, 0.5f);
                if (!_prepared) Clear();
            }
            else if (!_prepared)
            {
                float p = OverallProgress;
                line = p > 0.01f ? $"Wczytywanie... {p * 100f:0}%" : "Wczytywanie...";
            }
            else if (r.Loading) line = _reportedSeq == r.Seq && r.Peers > 1 ? $"Czekam na innych... ({r.Ready}/{r.Peers})" : "Wczytywanie...";
            else if (!r.Playing) line = "PAUZA";
            else line = "";
            if (_status.text != line) _status.text = line;
            _status.color = c;
            bool show = line.Length > 0;
            if (_status.gameObject.activeSelf != show) _status.gameObject.SetActive(show);

            if (_tv.overlay != null)
            {
                float len = _prepared ? (float)_vp.length : 0f;
                _tv.overlay.text = url == null ? "YouTube" : $"YouTube   {RadioUi.Time(_shownPos)} / {(len > 0f ? RadioUi.Time(len) : "--:--")}";
            }
            if (_tv.glow != null) _tv.glow.intensity = r.Playing && _prepared ? 0.6f : 0.4f;
        }
    }
}
