using System.Globalization;
using HideAndSeek.Game.Net;
using Unity.Collections;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Boombox panel, right column: the queue. Each row: number (bars on the playing one), thumbnail, title, length and
    /// download state; a click plays it (<see cref="RadioOp.Jump"/>), the X under the mouse removes it
    /// (<see cref="RadioOp.Remove"/>). The mouse wheel scrolls.
    /// </summary>
    public partial class Boombox
    {
        private float _scroll;
        private bool _scrollToCurrent, _scrollToEnd;

        private void QueueList(NetPlayer me, RadioChannel ch, Rect col, float s)
        {
            var ev = Event.current;
            var r = ch.State.Value;
            int n = ch.Count;

            // ---- header: count and total length
            GUI.Label(new Rect(col.x, col.y, 300 * s, 34 * s), "Kolejka", _song);
            float total = 0f;
            int knownLen = 0;
            for (int i = 0; i < n; i++)
                if (TryInfo(ch.At(i), out var qi) && qi.duration > 0f) { total += qi.duration; knownLen++; }
            string head = $"{n} / {MatchManager.RadioMaxQueue}" + (knownLen > 0 ? $"   {RadioUi.Time(total)}" : "");
            GUI.Label(new Rect(col.xMax - 260 * s, col.y, 260 * s, 34 * s), head, _tinyRight);

            var list = new Rect(col.x, col.y + 46 * s, col.width, col.height - 46 * s);
            float rowH = 66 * s, content = n * rowH;
            float maxScroll = Mathf.Max(0f, content - list.height);
            if (_scrollToCurrent && n > 0) { _scroll = Mathf.Max(0, r.Index - 1) * rowH; _scrollToCurrent = false; }
            if (_scrollToEnd && ev.type == EventType.Repaint) { _scroll = maxScroll; _scrollToEnd = false; }
            if (ev.type == EventType.ScrollWheel && list.Contains(ev.mousePosition)) { _scroll += ev.delta.y * 22f * s; ev.Use(); }
            _scroll = Mathf.Clamp(_scroll, 0f, maxScroll);

            if (n == 0)
            {
                RadioUi.Fill(list, new Color(1f, 1f, 1f, 0.025f), 16 * s);
                var c = new Rect(list.x, list.center.y - 40 * s, list.width, 34 * s);
                var st = new GUIStyle(_qTitle) { alignment = TextAnchor.MiddleCenter, normal = { textColor = RadioUi.Dim } };
                GUI.Label(c, "Kolejka jest pusta", st);
                st.fontSize = _tiny.fontSize;
                GUI.Label(new Rect(c.x, c.yMax + 4 * s, c.width, 26 * s), "Wklej link z YouTube na dole - piosenka pobierze się w najlepszej jakości.", st);
                return;
            }

            GUI.BeginGroup(list);
            bool listHover = new Rect(0, 0, list.width, list.height).Contains(ev.mousePosition);
            int first = Mathf.Max(0, Mathf.FloorToInt(_scroll / rowH)), last = Mathf.Min(n - 1, Mathf.CeilToInt((_scroll + list.height) / rowH));
            float sbw = maxScroll > 0f ? 10 * s : 0f;
            int removeAt = -1, jumpTo = -1;
            for (int i = first; i <= last; i++)
            {
                var row = new Rect(0, i * rowH - _scroll, list.width - sbw, rowH - 6 * s);
                bool cur = i == r.Index, hover = listHover && row.Contains(ev.mousePosition);
                if (cur) RadioUi.Fill(row, RadioUi.AccentDim, 12 * s);
                else if (hover) RadioUi.Fill(row, new Color(1f, 1f, 1f, 0.05f), 12 * s);
                if (cur) RadioUi.Fill(new Rect(row.x, row.y + 10 * s, 4 * s, row.height - 20 * s), RadioUi.Accent, 2 * s);

                // number, or bouncing bars on the playing song
                var numR = new Rect(row.x + 6 * s, row.y, 38 * s, row.height);
                if (cur && r.Playing && !IsLoading) Bars(numR, s);
                else
                {
                    _num.normal.textColor = cur ? RadioUi.Accent : i < r.Index ? new Color(1f, 1f, 1f, 0.35f) : RadioUi.Dim;
                    GUI.Label(numR, (i + 1).ToString(CultureInfo.InvariantCulture), _num);
                    _num.normal.textColor = RadioUi.Dim;
                }

                // thumbnail
                string u = ch.At(i);
                var th = new Rect(numR.xMax + 6 * s, row.y + 6 * s, (row.height - 12 * s) * 16f / 9f, row.height - 12 * s);
                RadioUi.Fill(th, RadioUi.Card, 6 * s);
                var tex = RadioThumbs.Get(u);
                if (tex != null && ev.type == EventType.Repaint) GUI.DrawTextureWithTexCoords(th, tex, RadioThumbs.Crop);
                else if (tex == null) RadioUi.Draw(new Rect(th.center.x - th.height * 0.4f, th.center.y - th.height * 0.4f, th.height * 0.8f, th.height * 0.8f), RadioUi.Vinyl(), new Color(1f, 1f, 1f, 0.6f));
                if (i < r.Index && !cur) RadioUi.Draw(th, RadioUi.White, new Color(0.07f, 0.075f, 0.09f, 0.45f));

                // title + second line
                float tx = th.xMax + 14 * s, tw = row.xMax - tx - (hover ? 48 * s : 12 * s);
                bool known = TryInfo(u, out var info);
                _qTitle.normal.textColor = cur ? Color.white : i < r.Index ? new Color(1f, 1f, 1f, 0.55f) : RadioUi.Text;
                GUI.Label(new Rect(tx, row.y + 6 * s, tw, 28 * s), RadioUi.Fit(known ? info.title : u, _qTitle, tw), _qTitle);
                _qTitle.normal.textColor = RadioUi.Text;
                DownloadLine(new Rect(tx, row.y + 32 * s, tw, 22 * s), u, known ? info.duration : 0f, s);

                // remove (declared before the row button: it takes the click first)
                if (hover)
                {
                    var x = new Rect(row.xMax - 42 * s, row.center.y - 17 * s, 34 * s, 34 * s);
                    if (IconButton(x, "close", RadioUi.Dim, 0.5f)) removeAt = i;
                }
                if (GUI.Button(row, GUIContent.none, GUIStyle.none) && !cur) jumpTo = i;
            }
            if (sbw > 0f)
            {
                float vh = list.height * list.height / content, vy = (list.height - vh) * (_scroll / maxScroll);
                RadioUi.Fill(new Rect(list.width - 5 * s, vy, 4 * s, vh), new Color(1f, 1f, 1f, 0.18f), 2 * s);
            }
            GUI.EndGroup();

            if (removeAt >= 0) ch.Request(me, RadioOp.Remove, new FixedString128Bytes(removeAt.ToString(CultureInfo.InvariantCulture)));
            else if (jumpTo >= 0) ch.Request(me, RadioOp.Jump, new FixedString128Bytes(jumpTo.ToString(CultureInfo.InvariantCulture)));
        }

        /// <summary>"3:45   pobrane" / "pobieranie 42%" / "czeka na pobranie" / "błąd pobierania".</summary>
        private void DownloadLine(Rect r, string url, float dur, float s)
        {
            var d = RadioCache.Peek(url);
            string len = dur > 0f ? RadioUi.Time(dur) : "";
            string st;
            Color c;
            if (d == null || !d.Started && !d.Done) { st = "czeka na pobranie"; c = RadioUi.Dim; }
            else if (d.Done) { st = "pobrane"; c = RadioUi.Accent; }
            else if (d.Error != null) { st = "błąd pobierania"; c = RadioUi.Bad; }
            else { st = d.Progress <= 0f ? "przygotowanie pobierania..." : $"pobieranie {d.Progress * 100f:0}%"; c = RadioUi.Warn; }
            GUI.Label(new Rect(r.x, r.y, 70 * s, r.height), len, _tiny);
            float dx = r.x + (len.Length > 0 ? 70 * s : 0f);
            float dot = 8 * s;
            RadioUi.Fill(new Rect(dx, r.center.y - dot * 0.5f, dot, dot), c, dot * 0.5f);
            _tiny.normal.textColor = c;
            GUI.Label(new Rect(dx + dot + 7 * s, r.y, r.width - (dx - r.x) - dot - 7 * s, r.height), st, _tiny);
            _tiny.normal.textColor = RadioUi.Dim;
        }

        /// <summary>Three bars that move with the music (the playing song's row).</summary>
        private void Bars(Rect r, float s)
        {
            float lvl = _out != null ? Mathf.Clamp01(_out.Level * 1.6f) : 0.5f, bw = 5 * s, gap = 4 * s, maxH = 22 * s;
            float x0 = r.center.x - (3 * bw + 2 * gap) * 0.5f;
            for (int k = 0; k < 3; k++)
            {
                float a = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * (5.1f + k * 1.7f) + k * 1.3f));
                float bh = Mathf.Max(4 * s, maxH * a * (0.45f + 0.55f * lvl));
                RadioUi.Fill(new Rect(x0 + k * (bw + gap), r.center.y + maxH * 0.5f - bh, bw, bh), RadioUi.Accent, 1.5f * s);
            }
        }
    }
}
