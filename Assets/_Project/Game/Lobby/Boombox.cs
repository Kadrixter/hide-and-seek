using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using HideAndSeek.Game.Net;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Lobby boombox on the crate by the exit door. E opens its panel (<c>Boombox.Panel.cs</c>): paste YouTube links into
    /// the queue, play / pause, next / previous, loop (queue / one song), volume, bass boost. Every copy downloads the
    /// songs itself in the best quality (<see cref="RadioCache"/>; the whole queue ahead, in the background), decodes the
    /// file (<see cref="RadioStream"/>) and plays it through <see cref="BoomboxAudio"/>.
    /// <para>Start of a track: the server marks it Loading, every copy buffers it from 0:00 and reports ready, then the
    /// server sets a common start a moment ahead and everybody starts from the beginning together
    /// (<c>MatchManager.Radio.cs</c>). A copy that is still loading joins at the current position.</para>
    /// Model: <c>Boombox.Model.cs</c>, speakers / LEDs / display: <c>Boombox.Visuals.cs</c>.
    /// </summary>
    public partial class Boombox : MonoBehaviour
    {
        public static readonly Vector3 Position = LobbyLayout.Boombox;
        public const float Yaw = LobbyLayout.BoomboxYaw;
        public static Boombox Instance { get; private set; }
        public Vector3 AimPoint => transform.position + Vector3.up * 0.16f;

        // ---- playback
        private AudioSource _src;
        private BoomboxAudio _out;
        private RadioStream _stream, _next;
        private string _nextUrl;
        private int _key = int.MinValue;
        private int _streamSeq = -1, _reportedSeq = -1;
        private string _status = "";
        private bool _endReported;
        private int _retries;
        private string _startError;
        private float _prefetchAt;

        // ---- titles (each copy resolves them itself)
        private static readonly Dictionary<string, (string title, float duration)> Info = new();
        private static readonly HashSet<string> Resolving = new();

        public static void Ensure()
        {
            if (Instance != null) return;
            var go = new GameObject("Boombox");
            go.transform.SetPositionAndRotation(Position, Quaternion.Euler(0f, Yaw, 0f));
            Instance = go.AddComponent<Boombox>();
            Instance.Build();
            Instance.BuildAudio();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_out != null) _out.Stream = null;
            _stream?.Dispose();
            _stream = null;
            _next?.Dispose();
            _next = null;
            if (_panelOwner == this) ClosePanel();
        }

        private void BuildAudio()
        {
            _src = gameObject.AddComponent<AudioSource>();
            _src.spatialBlend = 0.75f;
            _src.rolloffMode = AudioRolloffMode.Linear;
            _src.minDistance = 3f;
            _src.maxDistance = 22f;
            _src.dopplerLevel = 0f;
            _src.priority = 40;
            _out = gameObject.AddComponent<BoomboxAudio>(); // sets the constant clip the filter multiplies
            _src.Play();
        }

        // ================================================================ playback

        private void Update()
        {
            var mm = MatchManager.Instance;
            var ch = Channel;
            if (mm == null || ch == null || ch.Queue == null) return;
            var r = ch.State.Value;
            string url = ch.Current;
            for (int i = 0; i < ch.Count; i++) ResolveInfo(ch.At(i));

            // New track: take the prefetched stream when it is this one, else start it. Pause keeps the stream (the
            // reader just stops), so resuming is instant. Keyed on the link, not the index: removing an earlier queue
            // entry shifts the index of the song that keeps playing.
            int key = url != null ? HashCode.Combine(r.Seq, url) : -1;
            if (key != _key)
            {
                _key = key;
                SetStream(null);
                _endReported = false;
                _retries = 0;
                if (url != null && _next != null && _nextUrl == url && _next.Error == null && (r.Loading || !r.Playing) && r.LoadFrom <= 0f && r.PausedAt <= 0f)
                {
                    SetStream(_next);
                    _next = null;
                    _nextUrl = null;
                    _streamSeq = r.Seq;
                    Debug.Log("[Radio] prefetched stream taken over");
                }
                else if (url != null && (r.Playing || r.Loading)) StartStream(mm, r, url);
            }
            if (_stream == null && url != null && r.Playing && _startError == null) StartStream(mm, r, url);

            if (_stream == null && _startError != null && r.Loading && _reportedSeq != r.Seq)
            {
                _reportedSeq = r.Seq; // no yt-dlp / ffmpeg here: do not hold the others up
                ch.ReportReady(r.Seq);
            }
            if (_stream != null)
            {
                _out.Bass = r.Bass / 100f;
                // When the track position 0 is heard on this machine's audio clock (NaN = loading / paused). Only real
                // changes are passed on: the server clock estimate wobbles by a few ms from frame to frame.
                double zero = r.Playing && !r.Loading ? AudioSettings.dspTime + (r.StartTime - mm.Now) : double.NaN;
                double cur = _stream.DspAtZero;
                if (double.IsNaN(zero) != double.IsNaN(cur) || (!double.IsNaN(zero) && Math.Abs(zero - cur) > 0.03)) _stream.DspAtZero = zero;

                if (r.Loading && _reportedSeq != r.Seq && (_stream.Ready || _stream.Error != null || _stream.DecodeEnded))
                {
                    _reportedSeq = r.Seq;
                    ch.ReportReady(r.Seq);
                    Debug.Log($"[Radio] ready for seq {r.Seq}: buffered {_stream.BufferedSeconds:0.0}s");
                }

                float dur = TryInfo(url ?? "", out var di) ? di.duration : 0f;
                bool earlyEnd = _stream.Finished && dur > 0f && _stream.Position < dur - 5f;
                bool failed = _stream.Error != null || _stream.Empty || earlyEnd;
                if (failed && _retries < 2 && url != null && r.Playing)
                {
                    // Transient YouTube / pipe hiccup: try again from where the clock is now.
                    _retries++;
                    Debug.Log($"[Radio] stream failed ({_stream.Error ?? "no data"}), retry {_retries}");
                    StartStream(mm, r, url);
                }
                else if ((_stream.Finished || failed) && !_endReported && r.Playing && !r.Loading && NetworkSessionIsServer())
                {
                    _endReported = true;
                    ch.ServerEnded(_streamSeq, failed);
                }
            }
            _status = StatusText(r, url);
            _src.volume = r.Volume / 100f * 0.9f * VolumeScale; // the level from before 22.11

            // Prefetch the next entry (held at 0:00) so "next" / the end of a track switch at once: the same song when
            // it loops, the first one after the last when the queue loops.
            string nextUrl = NextUrl(ch, r, url);
            if (nextUrl != _nextUrl || (_next != null && _next.Error != null))
            {
                _next?.Dispose();
                _next = null;
                _nextUrl = nextUrl;
                if (nextUrl != null && RadioTools.YtDlp != null) _next = RadioStream.Start(nextUrl, 0f, out _);
            }
            // The rest of the queue downloads in the background, one song at a time.
            if (Time.unscaledTime >= _prefetchAt && RadioTools.YtDlp != null)
            {
                _prefetchAt = Time.unscaledTime + 1f;
                for (int i = 0; i < ch.Count; i++) RadioCache.Get(ch.At(i), false);
            }
            UpdateVisuals(r, url);
        }

        private static string NextUrl(RadioChannel ch, in RadioState r, string url)
        {
            if (url != null && r.Loop == RadioLoop.One) return url;
            int n = r.Index + 1;
            if (n >= ch.Count && r.Loop == RadioLoop.Queue) n = 0;
            return n >= 0 && n < ch.Count ? ch.At(n) : null;
        }

        private string StatusText(in RadioState r, string url)
        {
            if (url == null) return Channel != null && Channel.Count > 0 ? "Koniec kolejki - wybierz utwór albo dodaj nowy" : "Kolejka pusta";
            if (_stream == null && _startError != null) return _startError;
            if (_stream != null && _stream.Error != null) return _stream.Error;
            if (_stream != null && _stream.Downloading && r.Playing) return _stream.DownloadProgress <= 0f ? "Przygotowanie pobierania..." : $"Pobieranie {_stream.DownloadProgress * 100f:0}%...";
            if (r.Loading) return r.Playing ? $"Ładowanie... (gotowi {r.Ready}/{Mathf.Max(1, (int)r.Peers)})" : "Pauza";
            if (!r.Playing) return "Pauza";
            if (_stream == null || !_stream.Playing) return "Ładowanie...";
            return "";
        }

        /// <summary>Loading on this copy or on the server (display / panel).</summary>
        public bool IsLoading
        {
            get
            {
                var ch = Channel;
                if (ch == null) return false;
                var r = ch.State.Value;
                return r.Playing && (r.Loading || _stream == null || !_stream.Playing && _stream.Error == null);
            }
        }

        private void SetStream(RadioStream s)
        {
            if (_stream != null && _stream != s) _stream.Dispose();
            _stream = s;
            if (_out != null) _out.Stream = s;
        }

        private void StartStream(MatchManager mm, RadioState r, string url)
        {
            // Loading: from the start. Already playing (joined late, retry): from where the clock is.
            float pos = r.Loading ? r.LoadFrom : Mathf.Max(0f, r.Playing ? (float)(mm.Now - r.StartTime) : r.PausedAt);
            SetStream(RadioStream.Start(url, pos, out _startError));
            _streamSeq = r.Seq;
        }

        private static bool NetworkSessionIsServer() => Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsServer;

        /// <summary>Title + duration for the queue (yt-dlp --print, once per link, background).</summary>
        private static bool TryInfo(string url, out (string title, float duration) info)
        {
            lock (Info) return Info.TryGetValue(url, out info);
        }

        private static void ResolveInfo(string url)
        {
            if (string.IsNullOrEmpty(url) || Resolving.Contains(url)) return;
            string ytdlp = RadioTools.YtDlp;
            if (ytdlp == null) return;
            Resolving.Add(url);
            new Thread(() =>
            {
                string title = url;
                float dur = 0f;
                try
                {
                    var psi = RadioTools.Psi(ytdlp, "--no-warnings", "--no-playlist", "--skip-download", "--encoding", "utf-8",
                        "--print", "%(duration)s", "--print", "%(title)s", "--", url);
                    psi.StandardOutputEncoding = System.Text.Encoding.UTF8;
                    using var p = Process.Start(psi);
                    p.ErrorDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) Debug.Log("[Radio] info: " + e.Data); };
                    p.BeginErrorReadLine();
                    string d = p.StandardOutput.ReadLine();
                    string t = p.StandardOutput.ReadLine();
                    p.WaitForExit(30000);
                    float.TryParse(d, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out dur);
                    if (!string.IsNullOrWhiteSpace(t)) title = t.Trim();
                    Debug.Log($"[Radio] info: '{title}' {dur:0}s");
                }
                catch (Exception e) { title = url + " (" + e.Message + ")"; }
                lock (Info) Info[url] = (title, dur);
            }) { IsBackground = true }.Start();
        }

        // ================================================================ tests

        public static string TestUrl;
        /// <summary>Music is coming out of the boombox right now (tests).</summary>
        public bool MusicAudible => _stream != null && _stream.Playing && _out != null && _out.Level > 0.005f;
        public float PlayheadSeconds => _stream != null ? _stream.Position : -1f;
        public BoomboxAudio Output => _out;

        /// <summary>Test log line: server state, stream state, level.</summary>
        public string DebugState
        {
            get
            {
                var ch = Channel;
                var r = ch != null ? ch.State.Value : default;
                float pos = ch != null ? ch.Position(r) : 0f;
                return $"queue={(ch != null ? ch.Count : 0)} idx={r.Index} seq={r.Seq} playing={r.Playing} loading={r.Loading} " +
                       $"ready={r.Ready}/{r.Peers} pos={pos:0.00} vol={r.Volume} bass={r.Bass} | stream={(_stream != null)} " +
                       $"playhead={(_stream?.Position ?? -1f):0.00} buffered={(_stream?.BufferedSeconds ?? 0f):0.0}s out={(_stream?.Playing ?? false)} " +
                       $"underruns={(_stream?.Underruns ?? 0)} level={(_out != null ? _out.Level : 0f):0.00} kick={(_out != null ? _out.Kick : 0f):0.00} " +
                       $"loop={r.Loop} dl={(_stream != null ? _stream.DownloadProgress : -1f):0.00} err={_stream?.Error ?? "-"} status='{_status}'";
            }
        }
    }
}
