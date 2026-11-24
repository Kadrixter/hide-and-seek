using System.Globalization;
using HideAndSeek.Game.Net;
using Unity.Collections;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Boombox panel, left column: art (YouTube thumbnail, or a spinning record), title, state, a seek bar that can be
    /// dragged (on release every copy jumps to that moment, <see cref="RadioOp.Seek"/>), transport, the loop button
    /// (off -> queue -> one song) and the volume / bass sliders.
    /// </summary>
    public partial class Boombox
    {
        private enum Drag : byte { None, Seek, Volume, Bass }
        private Drag _drag;
        private float _dragValue, _sentAt, _holdUntil, _seekShown = -1f, _seekUntil, _spin;
        private int _sentValue = -1;

        private void NowPlaying(MatchManager mm, NetPlayer me, RadioChannel ch, Rect col, float s)
        {
            var ev = Event.current;
            var r = ch.State.Value;
            string url = ch.Current;
            (string title, float dur) inf = default;
            bool known = url != null && TryInfo(url, out inf);
            float dur = known ? inf.dur : 0f;
            float pos = ch.Position(r);
            if (Time.unscaledTime < _seekUntil && Mathf.Abs(pos - _seekShown) > 1.5f) pos = _seekShown; // the server has not moved yet
            pos = Mathf.Max(0f, pos);
            float cy = col.y;

            // ---- art
            var art = new Rect(col.x, cy, col.width, col.width * 9f / 16f);
            RadioUi.Fill(art, RadioUi.Card, 16 * s);
            var thumb = url != null ? RadioThumbs.Get(url) : null;
            if (thumb != null && ev.type == EventType.Repaint)
                GUI.DrawTextureWithTexCoords(new Rect(art.x + 1, art.y + 1, art.width - 2, art.height - 2), thumb, RadioThumbs.Crop);
            else
            {
                if (r.Playing && !IsLoading) _spin = (_spin + Time.unscaledDeltaTime * 70f) % 360f;
                float vs = art.height * 0.78f;
                var vr = new Rect(art.center.x - vs * 0.5f, art.center.y - vs * 0.5f, vs, vs);
                var m = GUI.matrix;
                GUIUtility.RotateAroundPivot(_spin, vr.center);
                RadioUi.Draw(vr, RadioUi.Vinyl(), url != null ? Color.white : new Color(1f, 1f, 1f, 0.35f));
                GUI.matrix = m;
            }
            // A pill on the art while the song downloads / loads.
            string pill = null;
            if (url != null && _stream != null && _stream.Downloading) pill = _stream.DownloadProgress <= 0f ? "POBIERANIE..." : $"POBIERANIE {_stream.DownloadProgress * 100f:0}%";
            else if (url != null && r.Playing && IsLoading) pill = r.Loading && r.Peers > 1 ? $"ŁADOWANIE  {r.Ready}/{r.Peers}" : "ŁADOWANIE";
            else if (url != null && !r.Playing) pill = "PAUZA";
            if (pill != null)
            {
                float pw = _tiny.CalcSize(new GUIContent(pill)).x + 26 * s;
                var pr = new Rect(art.x + 14 * s, art.yMax - 44 * s, pw, 30 * s);
                RadioUi.Fill(pr, new Color(0f, 0f, 0f, 0.72f), 15 * s);
                _tiny.normal.textColor = pill == "PAUZA" ? RadioUi.Text : RadioUi.Warn;
                _tiny.alignment = TextAnchor.MiddleCenter;
                GUI.Label(pr, pill, _tiny);
                _tiny.alignment = TextAnchor.MiddleLeft;
                _tiny.normal.textColor = RadioUi.Dim;
            }
            cy = art.yMax + 16 * s;

            // ---- title + state
            string title = url == null ? "Nic nie gra" : known ? inf.title : "Wczytywanie tytułu...";
            GUI.Label(new Rect(col.x, cy, col.width, 34 * s), RadioUi.Fit(title, _song, col.width), _song);
            cy += 36 * s;
            string state = url == null ? "Dodaj link z YouTube na dole panelu." : !string.IsNullOrEmpty(_status) ? _status : "Teraz gra";
            bool error = _stream != null && _stream.Error != null || _stream == null && _startError != null;
            _small.normal.textColor = error ? RadioUi.Bad : url != null && string.IsNullOrEmpty(_status) ? RadioUi.Accent : url != null && IsLoading ? RadioUi.Warn : RadioUi.Dim;
            GUI.Label(new Rect(col.x, cy, col.width, 26 * s), RadioUi.Fit(state, _small, col.width), _small);
            _small.normal.textColor = RadioUi.Dim;
            cy += 36 * s;

            // ---- seek bar
            bool canSeek = url != null && dur > 1f;
            var hit = new Rect(col.x, cy - 8 * s, col.width, 26 * s);
            float frac = dur > 0f ? Mathf.Clamp01(pos / dur) : 0f;
            if (_drag == Drag.Seek) frac = _dragValue;
            bool hover = hit.Contains(ev.mousePosition) && canSeek;
            if (canSeek && ev.type == EventType.MouseDown && ev.button == 0 && hover) { _drag = Drag.Seek; _dragValue = FracAt(hit, ev); ev.Use(); }
            else if (_drag == Drag.Seek && ev.type == EventType.MouseDrag) { _dragValue = FracAt(hit, ev); ev.Use(); }
            else if (_drag == Drag.Seek && ev.rawType == EventType.MouseUp)
            {
                _drag = Drag.None;
                float to = Mathf.Min(_dragValue * dur, dur - 1f);
                ch.Request(me, RadioOp.Seek, new FixedString128Bytes(to.ToString("0.00", CultureInfo.InvariantCulture)));
                _seekShown = to;
                _seekUntil = Time.unscaledTime + 2f;
                ev.Use();
            }
            float th = (hover || _drag == Drag.Seek ? 8f : 6f) * s;
            var track = new Rect(col.x, cy + 5 * s - th * 0.5f, col.width, th);
            RadioUi.Fill(track, new Color(1f, 1f, 1f, 0.12f), th * 0.5f);
            if (canSeek && _stream != null && _stream.Downloading)
                RadioUi.Fill(new Rect(track.x, track.y, track.width * _stream.DownloadProgress, th), new Color(1f, 1f, 1f, 0.1f), th * 0.5f);
            if (frac > 0f) RadioUi.Fill(new Rect(track.x, track.y, Mathf.Max(th, track.width * frac), th), RadioUi.Accent, th * 0.5f);
            if (hover || _drag == Drag.Seek)
            {
                float k = 18 * s;
                RadioUi.Fill(new Rect(track.x + track.width * frac - k * 0.5f, track.center.y - k * 0.5f, k, k), Color.white, k * 0.5f);
            }
            cy += 18 * s;
            float shownPos = _drag == Drag.Seek ? _dragValue * dur : pos;
            GUI.Label(new Rect(col.x, cy, 120 * s, 24 * s), url != null ? RadioUi.Time(shownPos) : "--:--", _tiny);
            GUI.Label(new Rect(col.xMax - 120 * s, cy, 120 * s, 24 * s), dur > 0f ? RadioUi.Time(dur) : "--:--", _tinyRight);
            if (r.Loop != RadioLoop.Off)
            {
                _tiny.normal.textColor = RadioUi.Accent;
                _tiny.alignment = TextAnchor.MiddleCenter;
                GUI.Label(new Rect(col.x, cy, col.width, 24 * s), r.Loop == RadioLoop.One ? "Zapętlony utwór" : "Zapętlona kolejka", _tiny);
                _tiny.alignment = TextAnchor.MiddleLeft;
                _tiny.normal.textColor = RadioUi.Dim;
            }
            cy += 36 * s;

            // ---- transport: previous, play / pause, next, loop
            float big = 74 * s, sm = 56 * s, gap = 22 * s, mid = col.center.x;
            float ty = cy + big * 0.5f;
            if (IconButton(new Rect(mid - big * 0.5f - gap - sm, ty - sm * 0.5f, sm, sm), "prev", RadioUi.Text)) ch.Request(me, RadioOp.Prev, default);
            var play = new Rect(mid - big * 0.5f, cy, big, big);
            bool ph = play.Contains(ev.mousePosition);
            RadioUi.Fill(play, ph ? Color.Lerp(RadioUi.Accent, Color.white, 0.2f) : RadioUi.Accent, big * 0.5f);
            float iw = big * 0.5f;
            RadioUi.Draw(new Rect(play.center.x - iw * 0.5f + (r.Playing ? 0f : 2f * s), play.center.y - iw * 0.5f, iw, iw), RadioUi.Icon(r.Playing ? "pause" : "play"), new Color(0.04f, 0.08f, 0.05f));
            if (GUI.Button(play, GUIContent.none, GUIStyle.none)) ch.Request(me, RadioOp.Toggle, default);
            if (IconButton(new Rect(mid + big * 0.5f + gap, ty - sm * 0.5f, sm, sm), "next", RadioUi.Text)) ch.Request(me, RadioOp.Next, default);
            var loop = new Rect(col.xMax - sm, ty - sm * 0.5f, sm, sm);
            if (r.Loop != RadioLoop.Off) RadioUi.Fill(loop, RadioUi.AccentDim, sm * 0.5f);
            if (IconButton(loop, "loop", r.Loop != RadioLoop.Off ? RadioUi.Accent : RadioUi.Dim, 0.66f)) ch.Request(me, RadioOp.Loop, default);
            if (r.Loop == RadioLoop.One)
            {
                float b = 20 * s;
                var br = new Rect(loop.xMax - b * 0.9f, loop.y - b * 0.1f, b, b);
                RadioUi.Fill(br, RadioUi.Accent, b * 0.5f);
                GUI.Label(br, "1", _badge);
            }
            if (loop.Contains(ev.mousePosition))
            {
                string tip = r.Loop == RadioLoop.Off ? "Pętla: wyłączona" : r.Loop == RadioLoop.Queue ? "Pętla: cała kolejka" : "Pętla: ten utwór";
                GUI.Label(new Rect(col.xMax - 220 * s, loop.yMax + 2 * s, 220 * s, 20 * s), tip, _tinyRight);
            }
            cy += big + 26 * s;

            // ---- sliders
            Slider(me, ch, new Rect(col.x, cy, col.width, 30 * s), "Głośność", r.Volume, Drag.Volume, RadioOp.SetVolume, s);
            cy += 42 * s;
            Slider(me, ch, new Rect(col.x, cy, col.width, 30 * s), "Bas", r.Bass, Drag.Bass, RadioOp.SetBass, s);
        }

        private static float FracAt(Rect r, Event ev) => Mathf.Clamp01((ev.mousePosition.x - r.x) / Mathf.Max(1f, r.width));

        /// <summary>0..100 slider; while dragging the value is sent at most ~8 times a second and once on release.</summary>
        private void Slider(NetPlayer me, RadioChannel ch, Rect r, string label, int value, Drag which, RadioOp op, float s)
        {
            var ev = Event.current;
            float lw = 118 * s, vw = 62 * s;
            GUI.Label(new Rect(r.x, r.y, lw, r.height), label, _small);
            var hit = new Rect(r.x + lw, r.y, r.width - lw - vw, r.height);
            bool hover = hit.Contains(ev.mousePosition);
            float v = value / 100f;
            if (_drag == which || Time.unscaledTime < _holdUntil && _sentValue >= 0 && _lastSlider == which) v = _dragValue;
            if (ev.type == EventType.MouseDown && ev.button == 0 && hover) { _drag = which; _lastSlider = which; _sentValue = -1; _dragValue = FracAt(hit, ev); ev.Use(); }
            else if (_drag == which && ev.type == EventType.MouseDrag) { _dragValue = FracAt(hit, ev); ev.Use(); }
            bool release = _drag == which && ev.rawType == EventType.MouseUp;
            if (_drag == which)
            {
                int want = Mathf.RoundToInt(_dragValue * 100f);
                if (want != _sentValue && (release || Time.unscaledTime - _sentAt > 0.12f))
                {
                    _sentValue = want;
                    _sentAt = Time.unscaledTime;
                    ch.Request(me, op, new FixedString128Bytes(want.ToString(CultureInfo.InvariantCulture)));
                }
                _holdUntil = Time.unscaledTime + 0.8f;
                v = _dragValue;
            }
            if (release) { _drag = Drag.None; ev.Use(); }
            float th = 6 * s;
            var track = new Rect(hit.x + 8 * s, hit.center.y - th * 0.5f, hit.width - 16 * s, th);
            RadioUi.Fill(track, new Color(1f, 1f, 1f, 0.12f), th * 0.5f);
            if (v > 0f) RadioUi.Fill(new Rect(track.x, track.y, Mathf.Max(th, track.width * v), th), hover || _drag == which ? Color.Lerp(RadioUi.Accent, Color.white, 0.15f) : RadioUi.Accent, th * 0.5f);
            float k = (hover || _drag == which ? 20f : 16f) * s;
            RadioUi.Fill(new Rect(track.x + track.width * v - k * 0.5f, track.center.y - k * 0.5f, k, k), Color.white, k * 0.5f);
            GUI.Label(new Rect(r.xMax - vw, r.y, vw, r.height), $"{Mathf.RoundToInt(v * 100f)}%", new GUIStyle(_small) { alignment = TextAnchor.MiddleRight, normal = { textColor = RadioUi.Text } });
        }

        private Drag _lastSlider;
    }
}
