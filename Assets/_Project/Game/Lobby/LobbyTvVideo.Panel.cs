using System.Globalization;
using HideAndSeek.Game.Net;
using Unity.Collections;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Lobby TV panel (E at the TV), drawn like the boombox panel with <see cref="RadioUi"/>: a live preview of the
    /// screen, the title and state, a seek bar (on release everybody jumps there), play / pause, stop (back to the CCTV
    /// picture), volume (slider or the mouse wheel) and the link row (paste a YouTube link, "Odtwórz").
    /// </summary>
    public partial class LobbyTvVideo
    {
        /// <summary>The TV panel is open (cursor policy, Esc).</summary>
        public static bool PanelOpen => _panelOwner != null;
        private static LobbyTvVideo _panelOwner;

        private string _urlField = "", _prefetched;
        private string _note;
        private float _noteUntil, _stylesFor = -1f, _volValue, _volSentAt, _volHoldUntil, _seekShown, _seekUntil;
        private bool _dragSeek, _dragVol;
        private float _dragValue;
        private GUIStyle _title, _sub, _song, _small, _tiny, _tinyRight, _btnText, _btnDark, _field;

        public static void OpenPanel()
        {
            var tv = Instance;
            if (tv == null) return;
            _panelOwner = tv;
            LocalPlayerController.MenuOpen = true;
            var me = NetPlayer.Local;
            if (me != null && me.InputReader != null) me.InputReader.SetCursorLocked(false);
            tv._urlField = "";
            tv._dragSeek = tv._dragVol = false;
        }

        public static void ClosePanel()
        {
            _panelOwner = null;
            LocalPlayerController.MenuOpen = false;
            var me = NetPlayer.Local;
            if (me != null && me.InputReader != null) me.InputReader.SetCursorLocked(true);
        }

        private void Note(string text)
        {
            _note = text;
            _noteUntil = Time.unscaledTime + 3.5f;
        }

        private void Styles(float s)
        {
            if (Mathf.Approximately(s, _stylesFor) && _title != null) return;
            _stylesFor = s;
            int F(float v) => Mathf.Max(8, Mathf.RoundToInt(v * s));
            _title = RadioUi.Label(F(31), RadioUi.Text, FontStyle.Bold);
            _sub = RadioUi.Label(F(17), RadioUi.Dim);
            _song = RadioUi.Label(F(25), RadioUi.Text, FontStyle.Bold);
            _small = RadioUi.Label(F(18), RadioUi.Dim);
            _tiny = RadioUi.Label(F(15), RadioUi.Dim);
            _tinyRight = RadioUi.Label(F(15), RadioUi.Dim, FontStyle.Normal, TextAnchor.MiddleRight);
            _btnText = RadioUi.Label(F(19), RadioUi.Text, FontStyle.Bold, TextAnchor.MiddleCenter);
            _btnDark = RadioUi.Label(F(19), new Color(0.04f, 0.08f, 0.05f), FontStyle.Bold, TextAnchor.MiddleCenter);
            _field = new GUIStyle(GUI.skin.textField)
            {
                fontSize = F(20), alignment = TextAnchor.MiddleLeft, padding = new RectOffset(F(16), F(16), 0, 0), border = new RectOffset(0, 0, 0, 0),
                normal = { background = null, textColor = RadioUi.Text }, hover = { background = null, textColor = RadioUi.Text },
                focused = { background = null, textColor = RadioUi.Text }, active = { background = null, textColor = RadioUi.Text },
            };
        }

        private void OnGUI()
        {
            if (_panelOwner != this) return;
            if (UI.PadCursor.ImguiPass(OnGUI)) return; // pad cursor: hover + clicks
            var mm = MatchManager.Instance;
            var me = NetPlayer.Local;
            var ch = Channel;
            if (mm == null || me == null || ch == null || mm.Phase.Value != MatchPhase.Lobby) { ClosePanel(); return; }
            float s = Mathf.Min(Screen.height / 1080f, Screen.width / 1340f);
            Styles(s);
            GUI.depth = -50;
            float w = 1120f * s, h = 600f * s, x = Mathf.Round((Screen.width - w) * 0.5f), y = Mathf.Round((Screen.height - h) * 0.5f), p = 30f * s;
            RadioUi.Draw(new Rect(0, 0, Screen.width, Screen.height), RadioUi.White, new Color(0f, 0f, 0f, 0.42f));
            RadioUi.Fill(new Rect(x - 4 * s, y + 6 * s, w + 8 * s, h + 8 * s), new Color(0f, 0f, 0f, 0.4f), 28 * s);
            RadioUi.Fill(new Rect(x, y, w, h), new Color(1f, 1f, 1f, 0.08f), 24 * s);
            RadioUi.Fill(new Rect(x + 1, y + 1, w - 2, h - 2), RadioUi.PanelBg, 23 * s);
            RadioUi.Fill(new Rect(x + 24 * s, y + 1, w - 48 * s, 3 * s), new Color(RadioUi.Accent.r, RadioUi.Accent.g, RadioUi.Accent.b, 0.55f), 2 * s);

            // ---- header
            var badge = new Rect(x + p, y + p - 2 * s, 44 * s, 44 * s);
            RadioUi.Fill(badge, RadioUi.Accent, 10 * s);
            RadioUi.Draw(new Rect(badge.x + 11 * s, badge.y + 11 * s, 22 * s, 22 * s), RadioUi.Icon("play"), new Color(0.04f, 0.08f, 0.05f));
            GUI.Label(new Rect(x + p + 58 * s, y + p - 6 * s, 700 * s, 38 * s), "TELEWIZOR", _title);
            string sub = "YouTube na ekranie w lobby - oglądają wszyscy razem";
            Color subC = RadioUi.Dim;
            if (RadioTools.YtDlp == null || RadioTools.Ffmpeg == null) { sub = "Brak yt-dlp / ffmpeg (folder Tools obok gry) - film nie zagra"; subC = RadioUi.Bad; }
            else if (Time.unscaledTime < _noteUntil && !string.IsNullOrEmpty(_note)) { sub = _note; subC = RadioUi.Warn; }
            _sub.normal.textColor = subC;
            GUI.Label(new Rect(x + p + 58 * s, y + p + 30 * s, 760 * s, 22 * s), sub, _sub);
            _sub.normal.textColor = RadioUi.Dim;
            GUI.Label(new Rect(x + w - p - 300 * s, y + p + 4 * s, 244 * s, 30 * s), "Esc - zamknij", _tinyRight);
            if (IconButton(new Rect(x + w - p - 40 * s, y + p - 2 * s, 40 * s, 40 * s), "close", RadioUi.Dim)) { ClosePanel(); return; }
            RadioUi.Draw(new Rect(x + p, y + 92 * s, w - 2 * p, Mathf.Max(1f, s)), RadioUi.White, new Color(1f, 1f, 1f, 0.06f));

            // ---- preview + controls
            var r = ch.State.Value;
            string url = ch.Current;
            float top = y + 116 * s, pw = 520f * s;
            var prev = new Rect(x + p, top, pw, pw * 9f / 16f);
            RadioUi.Fill(prev, RadioUi.Card, 16 * s);
            if (url != null && Event.current.type == EventType.Repaint) GUI.DrawTexture(new Rect(prev.x + 2, prev.y + 2, prev.width - 4, prev.height - 4), _rt, ScaleMode.StretchToFill, false);
            string state = StateLine(r, url);
            if (url == null || !_prepared)
            {
                _small.alignment = TextAnchor.MiddleCenter;
                GUI.Label(prev, url == null ? "Telewizor pokazuje kamery" : state, _small);
                _small.alignment = TextAnchor.MiddleLeft;
            }
            Controls(me, ch, r, url, state, new Rect(prev.xMax + 34 * s, top, x + w - p - prev.xMax - 34 * s, prev.height), s);

            // ---- link
            LinkRow(me, ch, new Rect(x + p, y + h - p - 56 * s, w - 2 * p, 56 * s), s);
            Wheel(me, ch, r);
        }

        private string StateLine(in RadioState r, string url)
        {
            if (url == null) return "Wklej link z YouTube na dole panelu.";
            if (VideoFailure != null) return VideoFailure;
            if (!_prepared) return OverallProgress > 0.01f ? $"Wczytywanie... {OverallProgress * 100f:0}%" : "Wczytywanie...";
            if (r.Loading) return r.Peers > 1 ? $"Wczytywanie... (gotowi {r.Ready}/{r.Peers})" : "Wczytywanie...";
            return r.Playing ? "Teraz leci" : "Pauza";
        }

        private void Controls(NetPlayer me, RadioChannel ch, in RadioState r, string url, string state, Rect col, float s)
        {
            var ev = Event.current;
            float cy = col.y;
            string title = url == null ? "Nic nie leci" : _dl != null && !string.IsNullOrEmpty(_dl.Title) ? _dl.Title : "Wczytywanie tytułu...";
            GUI.Label(new Rect(col.x, cy, col.width, 34 * s), RadioUi.Fit(title, _song, col.width), _song);
            cy += 38 * s;
            _small.normal.textColor = url != null && VideoFailure != null ? RadioUi.Bad : url != null && r.Playing && !r.Loading && _prepared ? RadioUi.Accent : RadioUi.Dim;
            GUI.Label(new Rect(col.x, cy, col.width, 26 * s), RadioUi.Fit(state, _small, col.width), _small);
            _small.normal.textColor = RadioUi.Dim;
            cy += 44 * s;

            // ---- seek bar
            float dur = _prepared ? (float)_vp.length : 0f, pos = _shownPos;
            if (Time.unscaledTime < _seekUntil && Mathf.Abs(pos - _seekShown) > 1.5f) pos = _seekShown;
            bool canSeek = url != null && dur > 1f;
            var hit = new Rect(col.x, cy - 8 * s, col.width, 26 * s);
            float frac = _dragSeek ? _dragValue : dur > 0f ? Mathf.Clamp01(pos / dur) : 0f;
            bool hover = canSeek && hit.Contains(ev.mousePosition);
            if (hover && ev.type == EventType.MouseDown && ev.button == 0) { _dragSeek = true; _dragValue = FracAt(hit, ev); ev.Use(); }
            else if (_dragSeek && ev.type == EventType.MouseDrag) { _dragValue = FracAt(hit, ev); ev.Use(); }
            else if (_dragSeek && ev.rawType == EventType.MouseUp)
            {
                _dragSeek = false;
                float to = Mathf.Min(_dragValue * dur, dur - 1f);
                ch.Request(me, RadioOp.Seek, new FixedString128Bytes(to.ToString("0.00", CultureInfo.InvariantCulture)));
                _seekShown = to;
                _seekUntil = Time.unscaledTime + 2f;
                ev.Use();
            }
            float th = (hover || _dragSeek ? 8f : 6f) * s;
            var track = new Rect(col.x, cy + 5 * s - th * 0.5f, col.width, th);
            RadioUi.Fill(track, new Color(1f, 1f, 1f, 0.12f), th * 0.5f);
            if (frac > 0f) RadioUi.Fill(new Rect(track.x, track.y, Mathf.Max(th, track.width * frac), th), RadioUi.Accent, th * 0.5f);
            cy += 18 * s;
            GUI.Label(new Rect(col.x, cy, 120 * s, 24 * s), url != null ? RadioUi.Time(_dragSeek ? _dragValue * dur : pos) : "--:--", _tiny);
            GUI.Label(new Rect(col.xMax - 120 * s, cy, 120 * s, 24 * s), dur > 0f ? RadioUi.Time(dur) : "--:--", _tinyRight);
            cy += 44 * s;

            // ---- play / pause, stop
            float big = 74 * s, sm = 56 * s;
            var play = new Rect(col.x, cy, big, big);
            bool ph = play.Contains(ev.mousePosition);
            RadioUi.Fill(play, url == null ? RadioUi.Card : ph ? Color.Lerp(RadioUi.Accent, Color.white, 0.2f) : RadioUi.Accent, big * 0.5f);
            float iw = big * 0.5f;
            RadioUi.Draw(new Rect(play.center.x - iw * 0.5f + (r.Playing ? 0f : 2f * s), play.center.y - iw * 0.5f, iw, iw), RadioUi.Icon(r.Playing ? "pause" : "play"),
                url == null ? RadioUi.Dim : new Color(0.04f, 0.08f, 0.05f));
            if (GUI.Button(play, GUIContent.none, GUIStyle.none) && url != null) ch.Request(me, RadioOp.Toggle, default);
            var stop = new Rect(play.xMax + 22 * s, cy + (big - sm) * 0.5f, sm, sm);
            if (IconButton(stop, "stop", url != null ? RadioUi.Text : RadioUi.Dim) && url != null) ch.Request(me, RadioOp.Clear, default);
            if (stop.Contains(ev.mousePosition)) GUI.Label(new Rect(stop.xMax + 10 * s, stop.y, 260 * s, sm), "Stop - wróć do kamer", _tiny);
            cy += big + 30 * s;

            // ---- volume
            VolumeSlider(me, ch, r, new Rect(col.x, cy, col.width, 30 * s), s);
            GUI.Label(new Rect(col.x, cy + 34 * s, col.width, 22 * s), "Kółko myszy - głośność", _tiny);
        }

        private void VolumeSlider(NetPlayer me, RadioChannel ch, in RadioState r, Rect rr, float s)
        {
            var ev = Event.current;
            float lw = 118 * s, vw = 62 * s;
            GUI.Label(new Rect(rr.x, rr.y, lw, rr.height), "Głośność", _small);
            var hit = new Rect(rr.x + lw, rr.y, rr.width - lw - vw, rr.height);
            bool hover = hit.Contains(ev.mousePosition);
            if (hover && ev.type == EventType.MouseDown && ev.button == 0) { _dragVol = true; ev.Use(); }
            if (_dragVol && (ev.type == EventType.MouseDown || ev.type == EventType.MouseDrag)) { SetVolume(me, ch, FracAt(hit, ev) * 100f, false); ev.Use(); }
            if (_dragVol && ev.rawType == EventType.MouseUp) { _dragVol = false; SetVolume(me, ch, FracAt(hit, ev) * 100f, true); ev.Use(); }
            float v = (Time.unscaledTime < _volHoldUntil ? _volValue : r.Volume) / 100f;
            float th = 6 * s;
            var track = new Rect(hit.x + 8 * s, hit.center.y - th * 0.5f, hit.width - 16 * s, th);
            RadioUi.Fill(track, new Color(1f, 1f, 1f, 0.12f), th * 0.5f);
            if (v > 0f) RadioUi.Fill(new Rect(track.x, track.y, Mathf.Max(th, track.width * v), th), RadioUi.Accent, th * 0.5f);
            float k = (hover || _dragVol ? 20f : 16f) * s;
            RadioUi.Fill(new Rect(track.x + track.width * v - k * 0.5f, track.center.y - k * 0.5f, k, k), Color.white, k * 0.5f);
            _small.alignment = TextAnchor.MiddleRight;
            _small.normal.textColor = RadioUi.Text;
            GUI.Label(new Rect(rr.xMax - vw, rr.y, vw, rr.height), $"{Mathf.RoundToInt(v * 100f)}%", _small);
            _small.alignment = TextAnchor.MiddleLeft;
            _small.normal.textColor = RadioUi.Dim;
        }

        /// <summary>Mouse wheel anywhere over the panel: volume +-5 %.</summary>
        private void Wheel(NetPlayer me, RadioChannel ch, in RadioState r)
        {
            var ev = Event.current;
            if (ev.type != EventType.ScrollWheel || Mathf.Abs(ev.delta.y) < 0.01f) return;
            float from = Time.unscaledTime < _volHoldUntil ? _volValue : r.Volume;
            SetVolume(me, ch, Mathf.Round(from / 5f) * 5f - Mathf.Sign(ev.delta.y) * 5f, true);
            ev.Use();
        }

        /// <summary>Shows the value at once; sends it at most ~8 times a second (always when <paramref name="force"/>).</summary>
        private void SetVolume(NetPlayer me, RadioChannel ch, float value, bool force)
        {
            _volValue = Mathf.Clamp(Mathf.Round(value), 0f, 100f);
            _volHoldUntil = Time.unscaledTime + 0.8f;
            if (!force && Time.unscaledTime - _volSentAt < 0.12f) return;
            _volSentAt = Time.unscaledTime;
            ch.Request(me, RadioOp.SetVolume, new FixedString128Bytes(((int)_volValue).ToString(CultureInfo.InvariantCulture)));
        }

        private void LinkRow(NetPlayer me, RadioChannel ch, Rect r, float s)
        {
            float bw = 150 * s, gap = 12 * s;
            var field = new Rect(r.x, r.y, r.width - 2 * (bw + gap), r.height);
            bool focused = GUI.GetNameOfFocusedControl() == "TvUrl";
            RadioUi.Fill(field, focused ? new Color(RadioUi.Accent.r, RadioUi.Accent.g, RadioUi.Accent.b, 0.7f) : new Color(1f, 1f, 1f, 0.1f), 14 * s);
            RadioUi.Fill(new Rect(field.x + 1.5f * s, field.y + 1.5f * s, field.width - 3 * s, field.height - 3 * s), RadioUi.Card, 13 * s);
            bool enter = Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
            GUI.SetNextControlName("TvUrl");
            _urlField = UI.PadKeyboard.Field("TvUrl", field, _urlField, 200, "Link z YouTube"); // pad: on-screen keyboard
            _urlField = GUI.TextField(field, _urlField ?? "", 200, _field);
            // A valid link in the field already starts downloading here (the start is faster for whoever pasted it).
            if (_urlField != _prefetched && TvLink.TryNormalize(_urlField, out string pre, out _))
            {
                _prefetched = _urlField;
                TvVideoCache.Get(pre);
            }
            if (string.IsNullOrEmpty(_urlField) && !focused)
                GUI.Label(new Rect(field.x + 16 * s, field.y, field.width - 32 * s, field.height), "Wklej link z YouTube (Ctrl+V) i naciśnij Enter", _small);
            if (TextButton(new Rect(field.xMax + gap, r.y, bw, r.height), "Wklej", false, s)) _urlField = GUIUtility.systemCopyBuffer?.Trim() ?? "";
            if (TextButton(new Rect(field.xMax + 2 * gap + bw, r.y, bw, r.height), "Odtwórz", true, s) || enter)
            {
                string u = (_urlField ?? "").Trim();
                if (TvLink.TryNormalize(u, out string link, out float start)) Play(me, ch, link, start);
                else if (u.Length > 0) Note("To nie jest link do filmu z YouTube.");
                if (enter && Event.current.type == EventType.KeyDown) Event.current.Use();
            }
        }

        /// <summary>Plays <paramref name="link"/> for everybody (from <paramref name="start"/> s: a t= link).</summary>
        public static void Play(NetPlayer me, RadioChannel ch, string link, float start)
        {
            ch.Request(me, RadioOp.Replace, new FixedString128Bytes(link));
            if (start > 0.5f) ch.Request(me, RadioOp.Seek, new FixedString128Bytes(start.ToString("0.0", CultureInfo.InvariantCulture)));
            if (_panelOwner != null) _panelOwner._urlField = "";
        }

        private static float FracAt(Rect r, Event ev) => Mathf.Clamp01((ev.mousePosition.x - r.x) / Mathf.Max(1f, r.width));

        private static bool IconButton(Rect r, string icon, Color tint)
        {
            bool hover = r.Contains(Event.current.mousePosition);
            if (hover) RadioUi.Fill(r, new Color(1f, 1f, 1f, 0.08f), r.width * 0.5f);
            float iw = r.width * 0.62f;
            RadioUi.Draw(new Rect(r.center.x - iw * 0.5f, r.center.y - iw * 0.5f, iw, iw), RadioUi.Icon(icon), hover ? Color.Lerp(tint, Color.white, 0.35f) : tint);
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        private bool TextButton(Rect r, string text, bool accent, float s)
        {
            bool hover = r.Contains(Event.current.mousePosition);
            Color bg = accent ? (hover ? Color.Lerp(RadioUi.Accent, Color.white, 0.18f) : RadioUi.Accent) : hover ? RadioUi.CardHover : RadioUi.Card;
            RadioUi.Fill(r, bg, 14 * s);
            GUI.Label(r, text, accent ? _btnDark : _btnText);
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }
    }
}
