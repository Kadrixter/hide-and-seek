using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Lobby TV video: opening the downloaded file in the <see cref="UnityEngine.Video.VideoPlayer"/>. It is given as a
    /// file:/// URL with forward slashes (a bare "C:\..." path is read as a URL with the scheme "c" and refused), then as
    /// a plain path; a file the player still refuses is re-encoded once (<see cref="TvVideoCache.Repair"/>). The loading
    /// line shows the overall progress (download of picture + sound, conversion, opening); when it stands still for
    /// <see cref="StallTimeout"/> s the video counts as failed (a Polish message on the screen, the host switches it off).
    /// </summary>
    public partial class LobbyTvVideo
    {
        public const float StallTimeout = 45f;
        private const int UrlForms = 2;

        private int _form;
        private bool _timedOut;
        private float _lastProgress = -1f, _progressAt;

        /// <summary>The player refused the file but another URL form / the re-encoded copy is still to be tried.</summary>
        private bool CanRetry => _dl != null && _dl.Done && (_form + 1 < UrlForms || !_dl.Key.EndsWith("_x"));

        /// <summary>0..1 of getting the video (picture + sound in one file) ready on this copy.</summary>
        public float OverallProgress
        {
            get
            {
                if (_url == null) return 0f;
                float v = _dl != null ? _dl.Progress : 0f;
                if (_dl != null && _dl.Done && !_prepared) v = 0.97f; // opening the file
                return Mathf.Clamp01(v);
            }
        }

        private static string FileUrl(string path, int form) => form == 0 ? "file:///" + path.Replace('\\', '/') : path.Replace('\\', '/');

        private void UpdateOpen(string url)
        {
            if (_dl == null || url == null) return;
            if (_dl.Done && _vpPath != _dl.Path)
            {
                _vpPath = _dl.Path;
                _form = 0;
                Open();
            }
            if (_videoError && _dl.Done)
            {
                if (_form + 1 < UrlForms)
                {
                    _form++;
                    _videoError = false;
                    Open();
                }
                else if (!_dl.Key.EndsWith("_x"))
                {
                    Debug.Log("[Tv] the player refused the file - re-encoding it");
                    _dl = TvVideoCache.Repair(_dl);
                    _videoError = false;
                    _vp.Stop();
                    _vpPath = null;
                    _progressAt = Time.unscaledTime;
                }
            }
            // Stall guard: no progress (download, conversion, opening) for a long time = a failure.
            if (_prepared || _timedOut || _dl.Error != null) { _progressAt = Time.unscaledTime; return; }
            float p = OverallProgress;
            if (p > _lastProgress + 0.001f)
            {
                _lastProgress = p;
                _progressAt = Time.unscaledTime;
            }
            else if (Time.unscaledTime - _progressAt > (_dl.Key.EndsWith("_x") ? 2f * StallTimeout : StallTimeout))
            {
                _timedOut = true;
                Debug.Log($"[Tv] loading stalled at {p * 100f:0}% for {StallTimeout:0}s - giving up");
            }
        }

        private void Open()
        {
            _prepared = _primed = _seeking = false;
            _vp.Stop();
            _vp.url = FileUrl(_vpPath, _form);
            Debug.Log($"[Tv] opening {_vp.url}");
            _vp.Prepare();
        }

        private void ResetOpen()
        {
            _form = 0;
            _timedOut = false;
            _lastProgress = -1f;
            _progressAt = Time.unscaledTime;
        }
    }
}
