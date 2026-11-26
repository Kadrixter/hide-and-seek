using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Boombox panel (E), IMGUI drawn with <see cref="RadioUi"/>: the frame, header and the link row here; now playing
    /// (art, seek bar, transport, loop, volume / bass sliders) in <c>Boombox.PanelNow.cs</c>; the queue (thumbnails,
    /// download state, jump, remove) in <c>Boombox.PanelQueue.cs</c>.
    /// </summary>
    public partial class Boombox
    {
        /// <summary>Some boombox panel is open (the lobby one or a map radio's).</summary>
        public static bool PanelOpen => _panelOwner != null;
        private static Boombox _panelOwner;

        /// <summary>Line under the panel title.</summary>
        public string PanelSubtitle = "Wspólna kolejka - słyszą wszyscy w pobliżu";

        private string _urlField = "";
        private string _note;
        private float _noteUntil;
        private float _stylesFor = -1f;
        private GUIStyle _title, _sub, _song, _small, _tiny, _tinyRight, _qTitle, _num, _btnText, _btnDark, _field, _badge;

        // ================================================================ open / close

        /// <summary>Opens the lobby boombox's panel.</summary>
        public static void OpenPanel() => OpenPanel(Instance);

        public static void OpenPanel(Boombox box)
        {
            if (box == null) return;
            _panelOwner = box;
            LocalPlayerController.MenuOpen = true;
            var me = NetPlayer.Local;
            if (me != null && me.InputReader != null) me.InputReader.SetCursorLocked(false);
            box._urlField = "";
            box._drag = Drag.None;
            box._scrollToCurrent = true;
        }

        public static void ClosePanel()
        {
            if (_panelOwner != null) _panelOwner._drag = Drag.None;
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

        // ================================================================ styles

        private static float UiScale => Mathf.Min(Screen.height / 1080f, Screen.width / 1340f);

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
            _qTitle = RadioUi.Label(F(19), RadioUi.Text);
            _num = RadioUi.Label(F(17), RadioUi.Dim, FontStyle.Normal, TextAnchor.MiddleCenter);
            _btnText = RadioUi.Label(F(19), RadioUi.Text, FontStyle.Bold, TextAnchor.MiddleCenter);
            _btnDark = RadioUi.Label(F(19), new Color(0.04f, 0.08f, 0.05f), FontStyle.Bold, TextAnchor.MiddleCenter);
            _badge = RadioUi.Label(F(14), new Color(0.04f, 0.08f, 0.05f), FontStyle.Bold, TextAnchor.MiddleCenter);
            _field = new GUIStyle(GUI.skin.textField)
            {
                fontSize = F(20), alignment = TextAnchor.MiddleLeft, padding = new RectOffset(F(16), F(16), 0, 0), border = new RectOffset(0, 0, 0, 0),
                normal = { background = null, textColor = RadioUi.Text }, hover = { background = null, textColor = RadioUi.Text },
                focused = { background = null, textColor = RadioUi.Text }, active = { background = null, textColor = RadioUi.Text },
            };
        }

        // ================================================================ panel

        private void OnGUI()
        {
            if (_panelOwner != this) return;
            if (UI.PadCursor.ImguiPass(OnGUI)) return; // pad cursor: hover + clicks
            var mm = MatchManager.Instance;
            var me = NetPlayer.Local;
            var ch = Channel;
            if (mm == null || me == null || ch == null || (PanelAllowed != null && !PanelAllowed())) { ClosePanel(); return; }
            float s = UiScale;
            Styles(s);
            GUI.depth = -50;
            float w = 1180f * s, h = 820f * s, x = Mathf.Round((Screen.width - w) * 0.5f), y = Mathf.Round((Screen.height - h) * 0.5f), p = 30f * s;
            var panel = new Rect(x, y, w, h);

            RadioUi.Draw(new Rect(0, 0, Screen.width, Screen.height), RadioUi.White, new Color(0f, 0f, 0f, 0.42f));
            RadioUi.Fill(new Rect(x - 4 * s, y + 6 * s, w + 8 * s, h + 8 * s), new Color(0f, 0f, 0f, 0.4f), 28 * s);
            RadioUi.Fill(panel, new Color(1f, 1f, 1f, 0.08f), 24 * s);
            RadioUi.Fill(new Rect(x + 1, y + 1, w - 2, h - 2), RadioUi.PanelBg, 23 * s);
            // Accent strip on the top edge.
            RadioUi.Fill(new Rect(x + 24 * s, y + 1, w - 48 * s, 3 * s), new Color(RadioUi.Accent.r, RadioUi.Accent.g, RadioUi.Accent.b, 0.55f), 2 * s);

            // ---- header
            RadioUi.Draw(new Rect(x + p, y + p - 2 * s, 44 * s, 44 * s), RadioUi.Vinyl(), Color.white);
            GUI.Label(new Rect(x + p + 58 * s, y + p - 6 * s, 700 * s, 38 * s), PanelTitle, _title);
            string sub = PanelSubtitle;
            Color subC = RadioUi.Dim;
            if (RadioTools.YtDlp == null || RadioTools.Ffmpeg == null) { sub = "Brak yt-dlp / ffmpeg (folder Tools obok gry) - radio nie zagra"; subC = RadioUi.Bad; }
            else if (Time.unscaledTime < _noteUntil && !string.IsNullOrEmpty(_note)) { sub = _note; subC = RadioUi.Warn; }
            _sub.normal.textColor = subC;
            GUI.Label(new Rect(x + p + 58 * s, y + p + 30 * s, 760 * s, 22 * s), sub, _sub);
            _sub.normal.textColor = RadioUi.Dim;
            GUI.Label(new Rect(x + w - p - 300 * s, y + p + 4 * s, 244 * s, 30 * s), "Esc - zamknij", _tinyRight);
            if (IconButton(new Rect(x + w - p - 40 * s, y + p - 2 * s, 40 * s, 40 * s), "close", RadioUi.Dim)) { ClosePanel(); return; }
            RadioUi.Draw(new Rect(x + p, y + 92 * s, w - 2 * p, Mathf.Max(1f, s)), RadioUi.White, new Color(1f, 1f, 1f, 0.06f));

            // ---- columns
            float top = y + 116 * s, bottom = y + h - p - 56 * s - 22 * s, colL = 500f * s;
            NowPlaying(mm, me, ch, new Rect(x + p, top, colL, bottom - top), s);
            float qx = x + p + colL + 34 * s;
            RadioUi.Draw(new Rect(qx - 17 * s, top, Mathf.Max(1f, s), bottom - top), RadioUi.White, new Color(1f, 1f, 1f, 0.05f));
            QueueList(me, ch, new Rect(qx, top, x + w - p - qx, bottom - top), s);

            // ---- add a link
            LinkRow(me, ch, new Rect(x + p, y + h - p - 56 * s, w - 2 * p, 56 * s), s);
        }

        private void LinkRow(NetPlayer me, RadioChannel ch, Rect r, float s)
        {
            float bw = 130 * s, gap = 12 * s;
            var field = new Rect(r.x, r.y, r.width - 2 * (bw + gap), r.height);
            bool focused = GUI.GetNameOfFocusedControl() == "RadioUrl";
            RadioUi.Fill(field, focused ? new Color(RadioUi.Accent.r, RadioUi.Accent.g, RadioUi.Accent.b, 0.7f) : new Color(1f, 1f, 1f, 0.1f), 14 * s);
            RadioUi.Fill(new Rect(field.x + 1.5f * s, field.y + 1.5f * s, field.width - 3 * s, field.height - 3 * s), RadioUi.Card, 13 * s);
            bool enter = Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
            GUI.SetNextControlName("RadioUrl");
            _urlField = UI.PadKeyboard.Field("RadioUrl", field, _urlField, 200, "Link z YouTube"); // pad: on-screen keyboard
            _urlField = GUI.TextField(field, _urlField ?? "", 200, _field);
            if (string.IsNullOrEmpty(_urlField) && !focused)
                GUI.Label(new Rect(field.x + 16 * s, field.y, field.width - 32 * s, field.height), "Wklej link z YouTube (Ctrl+V) i naciśnij Enter", _small);
            if (TextButton(new Rect(field.xMax + gap, r.y, bw, r.height), "Wklej", false, s)) _urlField = GUIUtility.systemCopyBuffer?.Trim() ?? "";
            if (TestUrl != null) { _urlField = TestUrl; TestUrl = null; enter = true; }
            if (TextButton(new Rect(field.xMax + 2 * gap + bw, r.y, bw, r.height), "Dodaj", true, s) || enter)
            {
                string u = (_urlField ?? "").Trim();
                if (MatchManager.IsYoutubeUrl(u))
                {
                    if (ch.Count >= MatchManager.RadioMaxQueue) Note($"Kolejka jest pełna ({MatchManager.RadioMaxQueue}).");
                    else
                    {
                        ch.Request(me, RadioOp.Add, new Unity.Collections.FixedString128Bytes(u));
                        _urlField = "";
                        _scrollToEnd = true;
                    }
                }
                else if (u.Length > 0) Note("To nie jest link z YouTube.");
                if (enter && Event.current.type == EventType.KeyDown) Event.current.Use();
            }
        }

        // ================================================================ controls

        /// <summary>Round icon button: a soft circle under the mouse.</summary>
        private static bool IconButton(Rect r, string icon, Color tint, float iconScale = 0.62f)
        {
            bool hover = r.Contains(Event.current.mousePosition);
            if (hover) RadioUi.Fill(r, new Color(1f, 1f, 1f, 0.08f), r.width * 0.5f);
            float iw = r.width * iconScale;
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
