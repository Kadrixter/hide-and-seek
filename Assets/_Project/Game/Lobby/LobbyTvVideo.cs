using System;
using HideAndSeek.Game.Net;
using UnityEngine;
using UnityEngine.Video;

namespace HideAndSeek.Game
{
    /// <summary>
    /// YouTube on the lobby wall TV. E at the TV opens a panel (<c>LobbyTvVideo.Panel.cs</c>): paste a link, play / pause,
    /// stop, seek, volume (also the mouse wheel). The shared state is the TV channel (<see cref="MatchManager.LobbyTv"/>,
    /// the boombox's sync: every copy loads the video, then the server starts it for everybody at the same moment; a late
    /// joiner jumps to the current second). Every copy plays the video itself:
    /// <list type="bullet">
    /// <item>one file: the whole video (H.264 picture + AAC sound) downloaded by yt-dlp (<see cref="TvVideoCache"/>);</item>
    /// <item>picture: that file in a <see cref="VideoPlayer"/> (its own sound off) drawing into a render texture on the
    /// screen; its speed is nudged (0.9 .. 1.1) to follow the server clock, a big difference is a seek;</item>
    /// <item>sound: the same file's sound track through the boombox's audio pipeline (<see cref="RadioStream"/> +
    /// <see cref="BoomboxAudio"/>, sample-exact on the DSP clock) from a 3D source at the screen; silent until the
    /// picture is there.</item>
    /// </list>
    /// Only the normal lobby has one (<see cref="LobbyTv"/> adds it; the dead-player lobby copy keeps its own feed).
    /// Screen texts / audio / radio ducking: <c>LobbyTvVideo.Screen.cs</c>.
    /// </summary>
    public partial class LobbyTvVideo : MonoBehaviour
    {
        public const int Width = 854, Height = 480;
        public static LobbyTvVideo Instance { get; private set; }

        private LobbyTv _tv;
        private VideoPlayer _vp;
        private RenderTexture _rt;
        private string _url, _vpPath;
        private TvDownload _dl;
        private bool _prepared, _seeking, _videoError, _primed;
        private float _speed = 1f, _drift, _seekCooldown, _seekAt, _seekTarget = -1f;
        private int _seekTries;
        private RadioStream _stream;
        private string _startError;
        private int _key = int.MinValue, _reportedSeq = -1, _endSeq = -1;
        private string _error;
        private float _errorUntil, _urlAt;

        public static RadioChannel Channel => MatchManager.Instance != null ? MatchManager.Instance.LobbyTv : null;

        /// <summary>Called by the lobby's <see cref="LobbyTv"/> at its start (never in the dead-player copy).</summary>
        public static LobbyTvVideo Attach(LobbyTv tv)
        {
            if (tv == null || tv.screen == null) return null;
            var v = tv.gameObject.AddComponent<LobbyTvVideo>();
            v._tv = tv;
            v.Build();
            Instance = v;
            return v;
        }

        private void Build()
        {
            _rt = new RenderTexture(Width, Height, 0, RenderTextureFormat.ARGB32) { name = "LobbyTvVideo", filterMode = FilterMode.Bilinear };
            _rt.Create();
            Clear();
            _vp = gameObject.AddComponent<VideoPlayer>();
            _vp.playOnAwake = false;
            _vp.source = VideoSource.Url;
            _vp.renderMode = VideoRenderMode.RenderTexture;
            _vp.targetTexture = _rt;
            _vp.aspectRatio = VideoAspectRatio.FitInside;
            _vp.audioOutputMode = VideoAudioOutputMode.None;
            _vp.isLooping = false;
            _vp.skipOnDrop = true;
            _vp.waitForFirstFrame = true;
            _vp.timeUpdateMode = VideoTimeUpdateMode.DSPTime;
            _vp.prepareCompleted += _ =>
            {
                _prepared = true;
                Debug.Log($"[Tv] prepared {_vp.width}x{_vp.height} {_vp.length:0.0}s - picture ready {Time.realtimeSinceStartup - _urlAt:0.0}s after the link");
            };
            _vp.seekCompleted += _ =>
            {
                _seeking = false;
                Debug.Log($"[Tv] seek to {_seekTarget:0.00}s done: picture at {_vp.time:0.00}s");
            };
            _vp.errorReceived += (_, msg) =>
            {
                _videoError = true;
                Debug.Log("[Tv] video error: " + msg);
            };
            BuildScreen();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_panelOwner == this) ClosePanel();
            SetStream(null);
            if (_rt != null) { _rt.Release(); Destroy(_rt); }
            RadioDuck = 1f;
        }

        /// <summary>The video (or its loading / error text) owns the screen; else the CCTV loop runs.</summary>
        public bool OwnsScreen => _url != null || Time.unscaledTime < _errorUntil;
        public Texture Picture => _rt;

        // ================================================================ playback

        private void Update()
        {
            var mm = MatchManager.Instance;
            var ch = Channel;
            if (mm == null || ch == null || ch.Queue == null) { UpdateScreen(default, null); return; }
            var r = ch.State.Value;
            string url = ch.Current;

            if (url != _url)
            {
                _url = url;
                ResetVideo();
                _urlAt = Time.realtimeSinceStartup;
                if (url != null) _dl = TvVideoCache.Get(url);
            }
            // Sound: one stream per (seq, link), as the boombox: a seek / restart is a new seq.
            int key = url != null ? HashCode.Combine(r.Seq, url) : -1;
            if (key != _key)
            {
                _key = key;
                SetStream(null);
                _startError = null;
                if (url != null && (r.Playing || r.Loading)) StartStream(mm, r, url);
            }
            if (_stream == null && url != null && (r.Playing || r.Loading) && _startError == null) StartStream(mm, r, url);

            UpdateOpen(url); // LobbyTvVideo.Open.cs
            string fail = VideoFailure;
            if (fail != null && _error != fail) ShowError(fail);

            float pos = Position(mm, r);
            if (_stream != null)
            {
                double zero = r.Playing && !r.Loading ? AudioSettings.dspTime + (r.StartTime - mm.Now) : double.NaN;
                double cur = _stream.DspAtZero;
                if (double.IsNaN(zero) != double.IsNaN(cur) || (!double.IsNaN(zero) && Math.Abs(zero - cur) > 0.03)) _stream.DspAtZero = zero;
            }
            DriveVideo(r, pos);

            // Loaded the start (picture at the right frame, sound buffered) or failed: do not hold the others up.
            if (r.Loading && _reportedSeq != r.Seq && url != null && (fail != null || VideoReady && SoundReady))
            {
                _reportedSeq = r.Seq;
                ch.ReportReady(r.Seq);
                Debug.Log($"[Tv] ready for seq {r.Seq} (video {(fail ?? "ok")}, sound {(_stream != null ? "ok" : _startError ?? "-")})");
            }
            // The host ends a video for everybody: at its end, or when it cannot be played here either.
            if (IsServer && url != null && r.Playing && !r.Loading && _endSeq != r.Seq)
            {
                float len = _prepared ? (float)_vp.length : 0f;
                bool ended = len > 0f && pos >= len - 0.05f || _stream != null && _stream.Finished && !_prepared;
                if (ended || fail != null)
                {
                    _endSeq = r.Seq;
                    ch.ServerEnded(r.Seq, fail != null);
                    Debug.Log($"[Tv] end of seq {r.Seq} ({(fail ?? "finished")})");
                }
            }
            UpdateAudio(r);
            UpdateScreen(r, url);
        }

        private static bool IsServer => Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsServer;

        /// <summary>Where the video should be now (s): the common clock while playing, the held frame otherwise.</summary>
        private static float Position(MatchManager mm, in RadioState r) =>
            r.Loading ? r.LoadFrom : r.Playing ? (float)(mm.Now - r.StartTime) : r.PausedAt;

        private string VideoFailure =>
            _dl != null && _dl.Error != null ? _dl.Error : _timedOut ? "Film nie wczytał się na czas." : _videoError && !CanRetry ? "Tego filmu nie da się odtworzyć." : null;

        /// <summary>The picture is prepared and shows the frame it should.</summary>
        private bool VideoReady => _prepared && _primed && !_seeking;
        private bool SoundReady => _stream == null ? _startError != null : _stream.Ready || _stream.Error != null || _stream.DecodeEnded;

        private void DriveVideo(in RadioState r, float pos)
        {
            if (!_prepared || _videoError) return;
            float len = (float)_vp.length;
            if (len > 0f) pos = Mathf.Min(pos, len - 0.04f);
            _seekCooldown -= Time.unscaledDeltaTime;
            if (_seeking && Time.unscaledTime - _seekAt > 2f)
            {
                // No "seek completed" from the player (it happens on a paused one): go on, the clock check repeats it.
                Debug.Log($"[Tv] seek to {_seekTarget:0.00}s not confirmed, picture at {_vp.time:0.00}s");
                _seeking = false;
            }
            float at = (float)_vp.time;
            if (!r.Playing || r.Loading)
            {
                // Held frame: paused / loading (the frame every copy starts from).
                if (_vp.isPlaying) _vp.Pause();
                float hold = Mathf.Max(0f, pos);
                // The first seek after preparing also makes the decoder put that frame on the screen.
                bool off = Mathf.Abs(at - hold) > 0.06f && !(Mathf.Abs(_seekTarget - hold) < 0.01f && _seekTries >= 2);
                if (!_seeking && (off || !_primed)) Seek(hold); // twice at most: a paused player may not report the new time
                _primed = true;
                SetSpeed(1f);
                return;
            }
            if (len > 0f && pos >= len - 0.1f)
            {
                if (_vp.isPlaying) _vp.Pause(); // the end: the host switches the TV off
                return;
            }
            _primed = true;
            if (!_vp.isPlaying)
            {
                if (_seeking) return;
                if (pos < at - 0.03f && at - pos < 1.5f) return; // the common start is just ahead: hold the frame
                // Play first: a paused player does not take a new time reliably, a playing one does.
                _vp.Play();
                if (Mathf.Abs(at - pos) > 0.15f) Seek(pos + 0.1f);
                _drift = 0f;
                return;
            }
            if (_seeking) return;
            float d = at - pos; // + = the picture is ahead
            _drift = Mathf.Lerp(_drift, d, 0.08f);
            if (Mathf.Abs(d) > 0.6f && _seekCooldown <= 0f)
            {
                Debug.Log($"[Tv] resync: picture {at:0.00}s, clock {pos:0.00}s");
                Seek(pos + 0.12f); // a seek costs a moment: land a bit ahead
                _drift = 0f;
                return;
            }
            SetSpeed(Mathf.Abs(_drift) < 0.025f ? 1f : Mathf.Clamp(1f - _drift * 0.9f, 0.9f, 1.1f));
        }

        private void Seek(float t)
        {
            _seeking = true;
            _seekCooldown = 1.5f;
            _seekAt = Time.unscaledTime;
            if (Mathf.Abs(t - _seekTarget) < 0.01f) _seekTries++;
            else _seekTries = 1;
            _seekTarget = Mathf.Max(0f, t);
            _vp.time = _seekTarget;
        }

        private void SetSpeed(float s)
        {
            if (Mathf.Abs(s - _speed) < 0.01f) return;
            _speed = s;
            if (_vp.canSetPlaybackSpeed) _vp.playbackSpeed = s;
        }

        private void ResetVideo()
        {
            if (_vp.isPlaying || _prepared) _vp.Stop();
            _vp.url = "";
            _vpPath = null;
            _dl = null;
            _prepared = _seeking = _videoError = _primed = false;
            _seekTarget = -1f;
            _seekTries = 0;
            ResetOpen();
            _speed = 1f;
            _vp.playbackSpeed = 1f;
            _endSeq = -1;
            if (_url != null) { _error = null; _errorUntil = 0f; }
            Clear();
        }

        private void Clear()
        {
            var prev = RenderTexture.active;
            RenderTexture.active = _rt;
            GL.Clear(true, true, new Color(0.02f, 0.02f, 0.025f));
            RenderTexture.active = prev;
        }

        private void SetStream(RadioStream s)
        {
            if (_stream != null && _stream != s) _stream.Dispose();
            _stream = s;
            if (_out != null) _out.Stream = s;
        }

        private void StartStream(MatchManager mm, in RadioState r, string url)
        {
            // The sound track of the downloaded video file itself: picture and sound can never come from different videos.
            if (_dl == null || !_dl.Done) return;
            float pos = r.Loading ? r.LoadFrom : Mathf.Max(0f, r.Playing ? (float)(mm.Now - r.StartTime) : r.PausedAt);
            SetStream(RadioStream.StartFile(_dl.Path, pos, out _startError));
            Debug.Log($"[Tv] sound from {System.IO.Path.GetFileName(_dl.Path)} at {pos:0.0}s");
            if (_startError != null) Debug.Log("[Tv] no sound: " + _startError);
        }

        private void ShowError(string text)
        {
            _error = text;
            _errorUntil = Time.unscaledTime + 8f;
            Debug.Log("[Tv] error on screen: " + text);
        }
    }
}
