using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using HideAndSeek.Game.Net;
using HideAndSeek.Game.UI;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Lobby boombox on the crate by the exit door. E opens its panel: paste YouTube links into the queue, play / pause,
    /// next / previous, volume, bass boost. Every peer streams the current track itself: yt-dlp downloads the audio to
    /// stdout, ffmpeg decodes it to raw PCM, a streaming AudioClip plays it from the boombox (3D). The start position comes
    /// from the server clock and the start-up delay is skipped, so everybody hears the same moment.
    /// </summary>
    public class Boombox : MonoBehaviour
    {
        public static readonly Vector3 Position = new Vector3(5.72f, 0.7f, -4.78f);
        public const float Yaw = -50f;
        public static Boombox Instance { get; private set; }
        public static bool PanelOpen { get; private set; }
        public Vector3 AimPoint => transform.position + Vector3.up * 0.16f;

        const int Rate = 48000;

        // ---- model
        private Transform _coneL, _coneR;
        private TextMesh _lcd;
        private Renderer[] _leds;
        private MaterialPropertyBlock _mpb;
        private float _pulse;

        // ---- playback
        private AudioSource _src;
        private RadioStream _stream;
        private int _key = int.MinValue;
        private int _streamSeq = -1;
        private string _status = "";
        private bool _endReported;

        // ---- titles (each peer resolves them itself)
        private static readonly Dictionary<string, (string title, float duration)> Info = new();
        /// <summary>yt-dlp info json per link (saved once): later starts skip the slow page extraction.</summary>
        public static readonly Dictionary<string, string> InfoJson = new();

        [Serializable] private class YtInfo { public string title; public float duration; }
        private static readonly HashSet<string> Resolving = new();

        // ---- panel
        private string _urlField = "";
        private GUIStyle _box, _btn, _label, _big, _field, _small;
        private Texture2D _bg, _bar, _barFill;

        public static void Ensure()
        {
            if (Instance != null) return;
            var go = new GameObject("Boombox");
            go.transform.SetPositionAndRotation(Position, Quaternion.Euler(0f, Yaw, 0f));
            Instance = go.AddComponent<Boombox>();
            Instance.Build();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            _stream?.Dispose();
            _stream = null;
            _next?.Dispose();
            _next = null;
            if (PanelOpen) ClosePanel();
        }

        // ================================================================ model

        private Material Mat(Color c, float gloss, bool emissive = false)
        {
            var m = new Material(Shader.Find(emissive ? "Unlit/Color" : "Standard")) { color = c };
            if (!emissive) m.SetFloat("_Glossiness", gloss);
            return m;
        }

        private Transform Part(string n, Transform parent, Vector3 lp, Vector3 size, Material m, Quaternion? rot = null)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = n;
            Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = lp;
            g.transform.localRotation = rot ?? Quaternion.identity;
            g.transform.localScale = size;
            g.GetComponent<MeshRenderer>().sharedMaterial = m;
            return g.transform;
        }

        private void Build()
        {
            // 80s boombox: black body, silver face plate, two big speakers, cassette deck, LCD, handle, antenna.
            var body = Mat(new Color(0.07f, 0.07f, 0.08f), 0.35f);
            var silver = Mat(new Color(0.62f, 0.64f, 0.68f), 0.7f);
            var grill = Mat(new Color(0.12f, 0.12f, 0.13f), 0.15f);
            var cone = Mat(new Color(0.18f, 0.18f, 0.2f), 0.25f);
            var chrome = Mat(new Color(0.85f, 0.86f, 0.9f), 0.9f);
            var red = Mat(new Color(0.85f, 0.1f, 0.08f), 0.4f);
            const float W = 0.56f, H = 0.27f, D = 0.15f;
            Part("Body", transform, new Vector3(0f, H * 0.5f, 0f), new Vector3(W, H, D), body);
            Part("Face", transform, new Vector3(0f, H * 0.5f, D * 0.5f + 0.003f), new Vector3(W - 0.02f, H - 0.03f, 0.006f), silver);
            foreach (float sx in new[] { -1f, 1f })
            {
                Vector3 c = new Vector3(sx * 0.17f, 0.125f, D * 0.5f + 0.008f);
                // Octagonal grille ring, the cone that pumps with the bass, a chrome dust cap.
                Part("Grille", transform, c, new Vector3(0.17f, 0.17f, 0.006f), grill);
                Part("Grille45", transform, c, new Vector3(0.17f, 0.17f, 0.006f), grill, Quaternion.Euler(0f, 0f, 45f));
                var cn = Part("Cone", transform, c + new Vector3(0f, 0f, 0.004f), new Vector3(0.12f, 0.12f, 0.01f), cone, Quaternion.Euler(0f, 0f, 22.5f));
                Part("Cap", cn, new Vector3(0f, 0f, 0.6f), new Vector3(0.3f, 0.3f, 0.8f), chrome);
                if (sx < 0) _coneL = cn; else _coneR = cn;
            }
            // Cassette deck + LCD + buttons + VU LEDs.
            Part("Deck", transform, new Vector3(0f, 0.09f, D * 0.5f + 0.008f), new Vector3(0.13f, 0.08f, 0.006f), grill);
            Part("Cassette", transform, new Vector3(0f, 0.09f, D * 0.5f + 0.011f), new Vector3(0.1f, 0.055f, 0.004f), Mat(new Color(0.85f, 0.75f, 0.3f), 0.3f));
            Part("Lcd", transform, new Vector3(0f, 0.2f, D * 0.5f + 0.008f), new Vector3(0.13f, 0.045f, 0.004f), Mat(new Color(0.05f, 0.12f, 0.08f), 0.6f));
            var lcdGo = new GameObject("LcdText");
            lcdGo.transform.SetParent(transform, false);
            lcdGo.transform.localPosition = new Vector3(0f, 0.2f, D * 0.5f + 0.0111f);
            lcdGo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            _lcd = lcdGo.AddComponent<TextMesh>();
            _lcd.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _lcd.GetComponent<MeshRenderer>().sharedMaterial = _lcd.font.material;
            _lcd.fontSize = 48;
            _lcd.characterSize = 0.0011f;
            _lcd.anchor = TextAnchor.MiddleCenter;
            _lcd.color = new Color(0.45f, 1f, 0.6f);
            _lcd.text = "RADIO";
            for (int i = 0; i < 5; i++)
                Part("Key", transform, new Vector3(-0.045f + i * 0.022f, H + 0.004f, 0.02f), new Vector3(0.018f, 0.01f, 0.03f), i == 0 ? red : silver);
            _leds = new Renderer[6];
            var ledMat = Mat(Color.white, 0f, true);
            for (int i = 0; i < 6; i++)
            {
                var led = Part("Led", transform, new Vector3(-0.05f + i * 0.02f, 0.235f, D * 0.5f + 0.009f), new Vector3(0.012f, 0.008f, 0.004f), ledMat);
                _leds[i] = led.GetComponent<Renderer>();
            }
            // Handle on two posts, telescopic antenna.
            Part("PostL", transform, new Vector3(-0.2f, H + 0.035f, 0f), new Vector3(0.02f, 0.07f, 0.03f), chrome);
            Part("PostR", transform, new Vector3(0.2f, H + 0.035f, 0f), new Vector3(0.02f, 0.07f, 0.03f), chrome);
            Part("Handle", transform, new Vector3(0f, H + 0.075f, 0f), new Vector3(0.42f, 0.022f, 0.032f), chrome);
            Part("Antenna", transform, new Vector3(0.24f, H + 0.17f, -0.05f), new Vector3(0.006f, 0.4f, 0.006f), chrome, Quaternion.Euler(-12f, 0f, -24f));
            _mpb = new MaterialPropertyBlock();
            var col = gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, H * 0.5f, 0f);
            col.size = new Vector3(W, H, D);

            _src = gameObject.AddComponent<AudioSource>();
            _src.spatialBlend = 0.75f;
            _src.rolloffMode = AudioRolloffMode.Linear;
            _src.minDistance = 3f;
            _src.maxDistance = 22f;
            _src.dopplerLevel = 0f;
            _src.loop = true;
            _src.clip = AudioClip.Create("BoomboxStream", Rate * 60 * 60, 2, Rate, true, OnPcmRead);
            _src.Play();
        }

        // ================================================================ playback

        private void Update()
        {
            var mm = MatchManager.Instance;
            if (mm == null || mm.RadioQueue == null) return;
            var r = mm.Radio.Value;
            string url = r.Index >= 0 && r.Index < mm.RadioQueue.Count ? mm.RadioQueue[r.Index].ToString() : null;
            for (int i = 0; i < mm.RadioQueue.Count; i++) ResolveInfo(mm.RadioQueue[i].ToString());

            // New track: take the prefetched stream when it is this one, else start it. Pause keeps the stream
            // (it just stops reading), so resuming is instant.
            int key = url != null ? HashCode.Combine(r.Seq, r.Index) : -1;
            if (key != _key)
            {
                _key = key;
                _stream?.Dispose();
                _stream = null;
                _endReported = false;
                _retries = 0;
                if (url != null && r.Playing && _next != null && _nextUrl == url && _next.Error == null)
                {
                    _stream = _next;
                    _next = null;
                    _nextUrl = null;
                    _stream.ServerStart = r.StartTime;
                    _streamSeq = r.Seq;
                    Debug.Log("[Radio] prefetched stream taken over");
                }
                else if (url != null && r.Playing) StartStream(mm, r, url);
                _status = url == null ? "Kolejka pusta" : r.Playing ? "Ładowanie..." : "Pauza";
            }
            if (_stream == null && url != null && r.Playing) StartStream(mm, r, url);

            if (_stream != null)
            {
                _stream.Bass = r.Bass / 100f;
                _stream.Paused = !r.Playing;
                if (_stream.Error != null) _status = _stream.Error;
                else if (!r.Playing) _status = "Pauza";
                else if (_stream.Started) _status = "";
                if (r.Playing && _stream.FirstDataPending && _stream.HasData)
                    _stream.SyncTo((float)(mm.Now - _stream.ServerStart)); // skip what the start-up delay cost
                if (r.Playing && _stream.Started && UnityEngine.Time.time >= _nextDrift)
                {
                    // Keep to the server clock (after a resume, a hitch, a long pause).
                    _nextDrift = UnityEngine.Time.time + 1f;
                    float want = (float)(mm.Now - r.StartTime), have = _stream.Position;
                    if (want - have > 0.35f) _stream.Skip(want - have);
                    else if (have - want > 0.5f) _stream.Hold(have - want);
                }
                float dur = TryInfo(url ?? "", out var di) ? di.duration : 0f;
                bool earlyEnd = _stream.Finished && dur > 0f && _stream.Position < dur - 5f;
                bool failed = _stream.Error != null || (_stream.Finished && !_stream.EverPlayed) || earlyEnd;
                if (failed && _retries < 2 && url != null && r.Playing)
                {
                    // Transient YouTube / pipe hiccup: try again from where the clock is now.
                    _retries++;
                    Debug.Log($"[Radio] stream failed ({_stream.Error ?? "no data"}), retry {_retries}");
                    _stream.Dispose();
                    StartStream(mm, r, url);
                }
                else if ((_stream.Finished || failed) && !_endReported && NetworkSessionIsServer())
                {
                    _endReported = true;
                    mm.ServerRadioEnded(_streamSeq);
                }
            }
            _src.volume = r.Volume / 100f * 0.9f;

            // Prefetch the next entry (paused at 0:00) so "next" / the end of a track switch at once.
            string nextUrl = r.Index + 1 >= 0 && r.Index + 1 < mm.RadioQueue.Count ? mm.RadioQueue[r.Index + 1].ToString() : null;
            if (nextUrl != _nextUrl || (_next != null && _next.Error != null))
            {
                _next?.Dispose();
                _next = null;
                _nextUrl = nextUrl;
                if (nextUrl != null && RadioTools.YtDlp != null)
                {
                    _next = RadioStream.Start(nextUrl, null, 0f, 0d, out _);
                    if (_next != null) _next.Paused = true;
                }
            }

            // Visuals: cones pump, LEDs show the level, LCD scrolls the title.
            float level = _stream != null ? _stream.Level : 0f;
            _pulse = Mathf.Max(level, Mathf.MoveTowards(_pulse, 0f, Time.deltaTime * 3f));
            float push = _pulse * (0.6f + r.Bass / 100f) * (r.Volume / 100f);
            foreach (var c in new[] { _coneL, _coneR })
                if (c != null) c.localScale = new Vector3(0.12f, 0.12f, 0.01f + 0.035f * push);
            for (int i = 0; i < _leds.Length; i++)
            {
                bool on = r.Playing && push * 7f > i;
                _mpb.SetColor("_Color", on ? (i < 4 ? new Color(0.3f, 1f, 0.35f) : new Color(1f, 0.3f, 0.15f)) : new Color(0.08f, 0.1f, 0.08f));
                _leds[i].SetPropertyBlock(_mpb);
            }
            string title = url != null && TryInfo(url, out var inf) ? inf.title : url ?? "";
            string lcd = url == null ? "-- RADIO --" : (!r.Playing ? "|| " : "> ") + title;
            if (lcd.Length > 16)
            {
                int off = (int)(Time.time * 4f) % (lcd.Length + 4);
                lcd = (lcd + "    " + lcd).Substring(off, 16);
            }
            _lcd.text = lcd;
        }

        private int _retries;
        private float _nextDrift;
        private RadioStream _next;
        private string _nextUrl;

        private void StartStream(MatchManager mm, RadioState r, string url)
        {
            float pos = Mathf.Max(0f, (float)(mm.Now - r.StartTime));
            // Always a fresh extraction: format links reused from the saved info json were refused by YouTube (403).
            _stream = RadioStream.Start(url, null, pos, r.StartTime, out string err);
            _streamSeq = r.Seq;
            _status = err ?? "Ładowanie...";
        }

        private static bool NetworkSessionIsServer() => Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsServer;

        private void OnPcmRead(float[] data)
        {
            var s = _stream;
            if (s == null) { Array.Clear(data, 0, data.Length); return; }
            s.Read(data);
        }

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

        // ================================================================ panel

        public static void OpenPanel()
        {
            if (Instance == null) return;
            PanelOpen = true;
            LocalPlayerController.MenuOpen = true;
            var me = NetPlayer.Local;
            if (me != null && me.InputReader != null) me.InputReader.SetCursorLocked(false);
            Instance._urlField = "";
        }

        public static void ClosePanel()
        {
            PanelOpen = false;
            LocalPlayerController.MenuOpen = false;
            var me = NetPlayer.Local;
            if (me != null && me.InputReader != null) me.InputReader.SetCursorLocked(true);
        }

        private static Texture2D Tex(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        private void Styles()
        {
            if (_box != null) return;
            float s = Screen.height / 1080f;
            _bg = Tex(new Color(0.06f, 0.07f, 0.09f, 0.94f));
            _bar = Tex(new Color(1f, 1f, 1f, 0.15f));
            _barFill = Tex(new Color(0.35f, 0.95f, 0.5f, 1f));
            _box = new GUIStyle(GUI.skin.box) { normal = { background = _bg } };
            _btn = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(24 * s), fontStyle = FontStyle.Bold };
            _label = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(22 * s), wordWrap = false, clipping = TextClipping.Clip };
            _small = new GUIStyle(_label) { fontSize = Mathf.RoundToInt(19 * s) };
            _big = new GUIStyle(_label) { fontSize = Mathf.RoundToInt(34 * s), fontStyle = FontStyle.Bold };
            _field = new GUIStyle(GUI.skin.textField) { fontSize = Mathf.RoundToInt(22 * s), alignment = TextAnchor.MiddleLeft };
        }

        private static string Fmt(float t) => t < 0 ? "--:--" : $"{(int)t / 60}:{(int)t % 60:00}";

        public static string TestUrl;

        /// <summary>Test log line: server state, stream state, level.</summary>
        public string DebugState
        {
            get
            {
                var mm = MatchManager.Instance;
                var r = mm != null ? mm.Radio.Value : default;
                float pos = mm != null ? (r.Playing ? (float)(mm.Now - r.StartTime) : r.PausedAt) : 0f;
                return $"queue={(mm != null ? mm.RadioQueue.Count : 0)} idx={r.Index} seq={r.Seq} playing={r.Playing} pos={pos:0.0} vol={r.Volume} bass={r.Bass} | " +
                       $"stream={(_stream != null)} started={(_stream?.Started ?? false)} level={(_stream?.Level ?? 0f):0.00} err={_stream?.Error ?? "-"} status='{_status}'";
            }
        }

        private void OnGUI()
        {
            if (!PanelOpen) return;
            var mm = MatchManager.Instance;
            var me = NetPlayer.Local;
            if (mm == null || me == null) { ClosePanel(); return; }
            Styles();
            float s = Screen.height / 1080f;
            float w = 900 * s, h = 640 * s, x = (Screen.width - w) * 0.5f, y = (Screen.height - h) * 0.5f, p = 24 * s, row = 46 * s;
            GUI.Box(new Rect(x, y, w, h), GUIContent.none, _box);
            float cy = y + p;
            GUI.Label(new Rect(x + p, cy, w, 44 * s), "BOOMBOX", _big);
            cy += 54 * s;

            var r = mm.Radio.Value;
            string url = r.Index >= 0 && r.Index < mm.RadioQueue.Count ? mm.RadioQueue[r.Index].ToString() : null;
            (string title, float dur) inf = default;
            bool known = url != null && TryInfo(url, out inf);
            float pos = r.Playing ? (float)(mm.Now - r.StartTime) : r.PausedAt;
            GUI.Label(new Rect(x + p, cy, w - 2 * p, row), url == null ? "Nic nie gra - wklej link z YouTube poniżej." : $"{(r.Playing ? "Gra" : "Pauza")}: {(known ? inf.title : url)}", _label);
            cy += row * 0.8f;
            if (url != null)
            {
                float bw = w - 2 * p - 160 * s;
                GUI.DrawTexture(new Rect(x + p, cy + 10 * s, bw, 10 * s), _bar);
                if (known && inf.dur > 0f) GUI.DrawTexture(new Rect(x + p, cy + 10 * s, bw * Mathf.Clamp01(pos / inf.dur), 10 * s), _barFill);
                GUI.Label(new Rect(x + p + bw + 12 * s, cy, 160 * s, row), $"{Fmt(pos)} / {(known && inf.dur > 0 ? Fmt(inf.dur) : "--:--")}", _small);
            }
            cy += row * 0.8f;
            if (!string.IsNullOrEmpty(_status)) GUI.Label(new Rect(x + p, cy, w - 2 * p, row), _status, _small);
            cy += row * 0.8f;

            // Transport + volume + bass.
            float bx = x + p, bwid = 150 * s, gap = 10 * s;
            if (GUI.Button(new Rect(bx, cy, bwid, row), "<< Poprz.", _btn)) me.RequestRadioRpc(RadioOp.Prev, default);
            if (GUI.Button(new Rect(bx + (bwid + gap), cy, bwid * 1.2f, row), r.Playing ? "|| Pauza" : "> Wznów", _btn)) me.RequestRadioRpc(RadioOp.Toggle, default);
            if (GUI.Button(new Rect(bx + (bwid + gap) * 2.2f, cy, bwid, row), "Nast. >>", _btn)) me.RequestRadioRpc(RadioOp.Next, default);
            cy += row + gap;
            GUI.Label(new Rect(bx, cy + 6 * s, 170 * s, row), $"Głośność {r.Volume}%", _label);
            if (GUI.Button(new Rect(bx + 190 * s, cy, 60 * s, row), "-", _btn)) me.RequestRadioRpc(RadioOp.VolDown, default);
            if (GUI.Button(new Rect(bx + 260 * s, cy, 60 * s, row), "+", _btn)) me.RequestRadioRpc(RadioOp.VolUp, default);
            GUI.Label(new Rect(bx + 380 * s, cy + 6 * s, 170 * s, row), $"Bas {r.Bass}%", _label);
            if (GUI.Button(new Rect(bx + 540 * s, cy, 60 * s, row), "-", _btn)) me.RequestRadioRpc(RadioOp.BassDown, default);
            if (GUI.Button(new Rect(bx + 610 * s, cy, 60 * s, row), "+", _btn)) me.RequestRadioRpc(RadioOp.BassUp, default);
            cy += row + gap * 2f;

            // Queue.
            GUI.Label(new Rect(bx, cy, w, row * 0.7f), $"Kolejka ({mm.RadioQueue.Count}/{MatchManager.RadioMaxQueue}):", _small);
            cy += row * 0.7f;
            int first = Mathf.Clamp(r.Index - 1, 0, Mathf.Max(0, mm.RadioQueue.Count - 5));
            for (int i = first; i < Mathf.Min(mm.RadioQueue.Count, first + 5); i++)
            {
                string u = mm.RadioQueue[i].ToString();
                string t = TryInfo(u, out var qi) ? qi.title : u;
                GUI.color = i == r.Index ? new Color(0.5f, 1f, 0.6f) : i < r.Index ? new Color(1f, 1f, 1f, 0.45f) : Color.white;
                GUI.Label(new Rect(bx, cy, w - 2 * p, row * 0.62f), $"{i + 1}. {t}", _small);
                GUI.color = Color.white;
                cy += row * 0.62f;
            }
            cy = y + h - p - row * 2f - gap;

            // Paste a link.
            GUI.Label(new Rect(bx, cy - row * 0.7f, w, row * 0.7f), "Link z YouTube (Ctrl+V):", _small);
            GUI.SetNextControlName("RadioUrl");
            _urlField = GUI.TextField(new Rect(bx, cy, w - 2 * p - 2 * (130 * s + gap), row), _urlField ?? "", 200, _field);
            if (GUI.Button(new Rect(x + w - p - 2 * 130 * s - gap, cy, 130 * s, row), "Wklej", _btn)) _urlField = GUIUtility.systemCopyBuffer?.Trim() ?? "";
            bool enter = Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
            if (TestUrl != null) { _urlField = TestUrl; TestUrl = null; enter = true; }
            if (GUI.Button(new Rect(x + w - p - 130 * s, cy, 130 * s, row), "Dodaj", _btn) || enter)
            {
                string u = (_urlField ?? "").Trim();
                if (MatchManager.IsYoutubeUrl(u))
                {
                    me.RequestRadioRpc(RadioOp.Add, new Unity.Collections.FixedString128Bytes(u));
                    _urlField = "";
                }
                else _status = "To nie jest link z YouTube.";
            }
            cy += row + gap;
            if (GUI.Button(new Rect(x + w - p - 200 * s, cy, 200 * s, row), "Zamknij (Esc)", _btn)) ClosePanel();
            GUI.Label(new Rect(bx, cy + 6 * s, w - 260 * s, row), RadioTools.YtDlp == null || RadioTools.Ffmpeg == null ? "Brak yt-dlp / ffmpeg (folder Tools obok gry)" : "", _small);
        }
    }

    /// <summary>Finds yt-dlp.exe / ffmpeg.exe: Tools next to the game (or the project), then PATH.</summary>
    public static class RadioTools
    {
        private static string _ytdlp, _ffmpeg;
        private static bool _searched;

        public static string YtDlp { get { Search(); return _ytdlp; } }
        public static string Ffmpeg { get { Search(); return _ffmpeg; } }

        private static void Search()
        {
            if (_searched) return;
            _searched = true;
            var dirs = new List<string>
            {
                Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Tools"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HideAndSeek", "Tools"),
            };
            foreach (var d in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator)) if (!string.IsNullOrWhiteSpace(d)) dirs.Add(d.Trim());
            foreach (var d in dirs)
            {
                try
                {
                    if (_ytdlp == null && File.Exists(Path.Combine(d, "yt-dlp.exe"))) _ytdlp = Path.Combine(d, "yt-dlp.exe");
                    if (_ffmpeg == null && File.Exists(Path.Combine(d, "ffmpeg.exe"))) _ffmpeg = Path.Combine(d, "ffmpeg.exe");
                }
                catch { /* bad PATH entry */ }
            }
            Debug.Log($"[Radio] tools: yt-dlp={_ytdlp ?? "-"} ffmpeg={_ffmpeg ?? "-"}");
        }

        /// <summary>Process start info without a shell; every argument is passed as data (no injection).</summary>
        public static ProcessStartInfo Psi(string exe, params string[] args)
        {
            var psi = new ProcessStartInfo(exe)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = false,
            };
            var sb = new System.Text.StringBuilder();
            foreach (var a in args)
            {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(Quote(a));
            }
            psi.Arguments = sb.ToString();
            return psi;
        }

        /// <summary>Windows command-line quoting (CommandLineToArgvW rules): every argument stays one argument.</summary>
        public static string Quote(string a)
        {
            if (a.Length > 0 && a.IndexOfAny(new[] { ' ', '\t', '"' }) < 0) return a;
            var sb = new System.Text.StringBuilder("\"");
            int bs = 0;
            foreach (char c in a)
            {
                if (c == '\\') { bs++; continue; }
                if (c == '"') { sb.Append('\\', bs * 2 + 1); sb.Append('"'); bs = 0; continue; }
                sb.Append('\\', bs);
                bs = 0;
                sb.Append(c);
            }
            sb.Append('\\', bs * 2);
            sb.Append('"');
            return sb.ToString();
        }

        /// <summary>Kills a process with its children (yt-dlp.exe unpacks itself into a child process).</summary>
        public static void KillTree(Process p)
        {
            if (p == null) return;
            try
            {
                if (p.HasExited) return;
                var k = new ProcessStartInfo("taskkill", "/T /F /PID " + p.Id) { UseShellExecute = false, CreateNoWindow = true };
                Process.Start(k)?.WaitForExit(2000);
            }
            catch { /* already gone */ }
            try { if (!p.HasExited) p.Kill(); } catch { /* gone */ }
        }
    }

    /// <summary>
    /// One playing track: yt-dlp (audio to stdout) -> ffmpeg (decode, seek, 48 kHz stereo s16le) -> ring buffer -> the
    /// boombox's streaming clip. Bass boost = low-shelf biquad at 90 Hz (up to +14 dB) with a soft limiter.
    /// </summary>
    public sealed class RadioStream : IDisposable
    {
        const int Rate = 48000, Ch = 2;
        private Process _yt, _ff;
        private readonly float[] _ring = new float[Rate * Ch * 30];
        private int _read, _write, _count;
        private readonly object _lock = new();
        private volatile bool _eof, _disposed;
        private float _startPos;
        public double ServerStart { get; set; }
        /// <summary>Paused: no samples are taken (the pipe fills up and yt-dlp waits), resume is instant.</summary>
        public volatile bool Paused;
        private long _consumedFrames;
        private int _holdSamples;
        /// <summary>Track position of the next sample that will be played (seconds).</summary>
        public float Position { get { lock (_lock) return _startPos + _consumedFrames / (float)Rate; } }

        /// <summary>Drops <paramref name="seconds"/> of buffered audio (we are behind the clock).</summary>
        public void Skip(float seconds)
        {
            lock (_lock)
            {
                int drop = Mathf.Min(Mathf.RoundToInt(seconds * Rate) * Ch, _count - _count % Ch);
                _read = (_read + drop) % _ring.Length;
                _count -= drop;
                _consumedFrames += drop / Ch;
            }
        }

        /// <summary>Plays <paramref name="seconds"/> of silence first (we are ahead of the clock).</summary>
        public void Hold(float seconds)
        {
            lock (_lock) _holdSamples = Mathf.RoundToInt(seconds * Rate) * Ch;
        }
        public volatile string Error;
        public bool HasData { get { lock (_lock) return _count > 0; } }
        public bool FirstDataPending { get; private set; } = true;
        public bool Started => !FirstDataPending;
        public bool Finished { get { lock (_lock) return _eof && _count == 0 && !FirstDataPending; } }
        public float Level { get; private set; }
        /// <summary>Some audio actually came out (an empty / broken stream is retried instead of skipped).</summary>
        public bool EverPlayed { get; private set; }
        public float Bass;

        // biquad state per channel
        private float _gainFor = -1f;
        private float _b0, _b1, _b2, _a1, _a2, _makeup = 1f;
        private readonly float[] _x1 = new float[Ch], _x2 = new float[Ch], _y1 = new float[Ch], _y2 = new float[Ch];

        public static RadioStream Start(string url, string infoJson, float pos, double serverStart, out string error)
        {
            error = null;
            if (RadioTools.YtDlp == null || RadioTools.Ffmpeg == null)
            {
                error = "Brak yt-dlp / ffmpeg - radio nie zagra.";
                return null;
            }
            var s = new RadioStream { _startPos = pos, ServerStart = serverStart };
            try { s.Launch(url, infoJson); }
            catch (Exception e)
            {
                error = "Błąd startu: " + e.Message;
                s.Dispose();
                return null;
            }
            return s;
        }

        private void Launch(string url, string infoJson)
        {
            var yt = infoJson != null && File.Exists(infoJson)
                ? RadioTools.Psi(RadioTools.YtDlp, "--no-warnings", "--no-part", "-q", "-f", "bestaudio/best", "-o", "-", "--load-info-json", infoJson)
                : RadioTools.Psi(RadioTools.YtDlp, "--no-warnings", "--no-playlist", "--no-part", "-q", "-f", "bestaudio/best", "-o", "-", "--", url);
            _yt = Process.Start(yt);
            var ff = RadioTools.Psi(RadioTools.Ffmpeg, "-loglevel", "error", "-i", "pipe:0", "-ss",
                _startPos.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture), "-vn", "-f", "s16le", "-ac", "2", "-ar", "48000", "pipe:1");
            ff.RedirectStandardInput = true;
            _ff = Process.Start(ff);
            _yt.ErrorDataReceived += (_, e) =>
            {
                if (string.IsNullOrEmpty(e.Data)) return;
                Debug.Log("[Radio] yt-dlp: " + e.Data);
                if (e.Data.Contains("ERROR")) Error = "yt-dlp: " + Short(e.Data);
            };
            _ff.ErrorDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) Debug.Log("[Radio] ffmpeg: " + e.Data); };
            _yt.BeginErrorReadLine();
            _ff.BeginErrorReadLine();
            Debug.Log($"[Radio] start {url} at {_startPos:0.0}s{(infoJson != null ? " (cached info)" : "")}");
            new Thread(Pump) { IsBackground = true, Name = "RadioPump" }.Start();
            new Thread(Decode) { IsBackground = true, Name = "RadioDecode" }.Start();
        }

        private static string Short(string s) => s.Length > 90 ? s.Substring(0, 90) + "..." : s;

        /// <summary>yt-dlp stdout -> ffmpeg stdin.</summary>
        private void Pump()
        {
            var buf = new byte[32768];
            try
            {
                var input = _yt.StandardOutput.BaseStream;
                var output = _ff.StandardInput.BaseStream;
                int n;
                while (!_disposed && (n = input.Read(buf, 0, buf.Length)) > 0) output.Write(buf, 0, n);
                output.Close();
            }
            catch (Exception) { /* killed / pipe closed */ }
        }

        /// <summary>ffmpeg stdout (s16le) -> float ring buffer (waits while the buffer is full).</summary>
        private void Decode()
        {
            var buf = new byte[16384];
            int carry = 0;
            try
            {
                var input = _ff.StandardOutput.BaseStream;
                while (!_disposed)
                {
                    int n = input.Read(buf, carry, buf.Length - carry);
                    if (n <= 0) break;
                    n += carry;
                    int samples = n / 2;
                    for (int i = 0; i < samples; i++)
                    {
                        float v = (short)(buf[i * 2] | (buf[i * 2 + 1] << 8)) / 32768f;
                        while (!_disposed)
                        {
                            lock (_lock)
                            {
                                if (_count < _ring.Length)
                                {
                                    _ring[_write] = v;
                                    _write = (_write + 1) % _ring.Length;
                                    _count++;
                                    break;
                                }
                            }
                            Thread.Sleep(10);
                        }
                    }
                    carry = n - samples * 2;
                    if (carry > 0) buf[0] = buf[n - 1];
                }
            }
            catch (Exception) { /* killed */ }
            _eof = true;
        }

        /// <summary>Main thread, first data: drop what the start-up took so we play the moment the server clock says.</summary>
        public void SyncTo(float wantPos)
        {
            FirstDataPending = false;
            float late = wantPos - _startPos;
            if (late <= 0.05f) return;
            int drop = Mathf.RoundToInt(late * Rate) * Ch;
            lock (_lock)
            {
                drop = Mathf.Min(drop, _count - _count % Ch);
                _read = (_read + drop) % _ring.Length;
                _count -= drop;
                _consumedFrames += drop / Ch;
            }
            Debug.Log($"[Radio] synced: start-up {late:0.00}s skipped");
        }

        /// <summary>Audio thread: fills the clip buffer (silence until synced / on underrun).</summary>
        public void Read(float[] data)
        {
            if (FirstDataPending || Paused) { Array.Clear(data, 0, data.Length); Level = 0f; return; }
            UpdateFilter();
            double sum = 0;
            lock (_lock)
            {
                int taken = 0;
                for (int i = 0; i < data.Length; i++)
                {
                    float v = 0f;
                    if (_holdSamples > 0) _holdSamples--;
                    else if (_count > 0)
                    {
                        v = _ring[_read];
                        _read = (_read + 1) % _ring.Length;
                        _count--;
                        taken++;
                    }
                    data[i] = v;
                }
                _consumedFrames += taken / Ch;
            }
            for (int i = 0; i < data.Length; i++)
            {
                int c = i % Ch;
                float x = data[i];
                float y = _b0 * x + _b1 * _x1[c] + _b2 * _x2[c] - _a1 * _y1[c] - _a2 * _y2[c];
                _x2[c] = _x1[c]; _x1[c] = x; _y2[c] = _y1[c]; _y1[c] = y;
                y *= _makeup;
                // Soft limiter: transparent below ~0.7, rounds off the boosted bass peaks.
                float a = Mathf.Abs(y);
                if (a > 0.7f) y = Mathf.Sign(y) * (0.7f + 0.3f * (1f - Mathf.Exp(-(a - 0.7f) / 0.3f)));
                data[i] = y;
                sum += y * y;
            }
            float rms = Mathf.Sqrt((float)(sum / Mathf.Max(1, data.Length)));
            if (rms > 0.001f) EverPlayed = true;
            Level = Mathf.Clamp01(rms * 1.7f);
        }

        private void UpdateFilter()
        {
            float g = Mathf.Clamp01(Bass) * 14f;
            if (Mathf.Abs(g - _gainFor) < 0.01f) return;
            _gainFor = g;
            // RBJ low shelf, f0 = 90 Hz, S = 1.
            double A = Math.Pow(10, g / 40.0), w0 = 2 * Math.PI * 90.0 / Rate, cs = Math.Cos(w0), sn = Math.Sin(w0);
            double alpha = sn / 2 * Math.Sqrt(2), sA = 2 * Math.Sqrt(A) * alpha;
            double b0 = A * ((A + 1) - (A - 1) * cs + sA), b1 = 2 * A * ((A - 1) - (A + 1) * cs), b2 = A * ((A + 1) - (A - 1) * cs - sA);
            double a0 = (A + 1) + (A - 1) * cs + sA, a1 = -2 * ((A - 1) + (A + 1) * cs), a2 = (A + 1) + (A - 1) * cs - sA;
            _b0 = (float)(b0 / a0); _b1 = (float)(b1 / a0); _b2 = (float)(b2 / a0); _a1 = (float)(a1 / a0); _a2 = (float)(a2 / a0);
            _makeup = Mathf.Pow(10f, -g * 0.3f / 20f);
        }

        public void Dispose()
        {
            _disposed = true;
            foreach (var p in new[] { _yt, _ff })
            {
                RadioTools.KillTree(p);
                try { p?.Dispose(); } catch { /* gone */ }
            }
            _yt = _ff = null;
        }
    }
}
