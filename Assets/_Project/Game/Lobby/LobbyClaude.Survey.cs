using System.Collections;
using System.Text;
using HideAndSeek.Game.Net;
using UnityEngine;
using UnityEngine.Networking;

namespace HideAndSeek.Game
{
    /// <summary>
    /// The little survey Claude asks (E at Claude): 1) the game's name (can be skipped), 2) what to add / change.
    /// Anything typed goes to the Discord webhook with the player's nick; a skipped first question is marked as skipped.
    /// Drawn with <see cref="RadioUi"/> like the boombox / TV panels; local only.
    /// </summary>
    public partial class LobbyClaude
    {
        private const string Webhook = "https://discord.com/api/webhooks/1575506694195257415/_7SG_3Dy6qaQuwAG_SB28avwXZXnXqK_OthP9nLSv0Wlh1QvBpH7qmJ-B7oDJ3qvgyXE";
        private const string Q1 = "Jak twoim zdaniem powinna nazywać się gra?";
        private const string Q2 = "Co twoim zdaniem powinno być dodane/zmienione w tej grze?";
        private const string Q2Hint = "wizualnie / technicznie";
        private const int MaxAnswer = 800;
        private const float SendCooldown = 60f;
        private static readonly Color Orange = new(0.85f, 0.47f, 0.34f, 1f);

        public static bool SurveyOpen => _surveyOwner != null;
        private static LobbyClaude _surveyOwner;
        private static float _lastSent = -999f;

        private int _step; // 0 = name, 1 = changes, 2 = sending / result
        private string _a1 = "", _a2 = "", _result;
        private bool _skipped1, _sending, _ok;
        private float _openedAt, _stylesFor = -1f;
        private GUIStyle _title, _sub, _q, _small, _btnText, _btnDark, _field, _area;

        public static void OpenSurvey()
        {
            var c = Instance;
            if (c == null) return;
            c.Poke();
            _surveyOwner = c;
            c._step = 0;
            c._a1 = c._a2 = "";
            c._skipped1 = c._sending = c._ok = false;
            c._result = null;
            c._openedAt = Time.unscaledTime;
            LocalPlayerController.MenuOpen = true;
            var me = NetPlayer.Local;
            if (me != null && me.InputReader != null) me.InputReader.SetCursorLocked(false);
        }

        public static void CloseSurvey()
        {
            if (_surveyOwner == null) return;
            _surveyOwner = null;
            LocalPlayerController.MenuOpen = false;
            var me = NetPlayer.Local;
            if (me != null && me.InputReader != null) me.InputReader.SetCursorLocked(true);
        }

        private void Styles(float s)
        {
            if (Mathf.Approximately(s, _stylesFor) && _title != null) return;
            _stylesFor = s;
            int F(float v) => Mathf.Max(8, Mathf.RoundToInt(v * s));
            _title = RadioUi.Label(F(31), RadioUi.Text, FontStyle.Bold);
            _sub = RadioUi.Label(F(17), RadioUi.Dim);
            _q = RadioUi.Label(F(24), RadioUi.Text, FontStyle.Bold);
            _q.wordWrap = true;
            _small = RadioUi.Label(F(17), RadioUi.Dim);
            _small.wordWrap = true;
            _btnText = RadioUi.Label(F(19), RadioUi.Text, FontStyle.Bold, TextAnchor.MiddleCenter);
            _btnDark = RadioUi.Label(F(19), new Color(0.12f, 0.05f, 0.03f), FontStyle.Bold, TextAnchor.MiddleCenter);
            _field = new GUIStyle(GUI.skin.textField)
            {
                fontSize = F(20), alignment = TextAnchor.MiddleLeft, padding = new RectOffset(F(16), F(16), 0, 0), border = new RectOffset(0, 0, 0, 0),
                normal = { background = null, textColor = RadioUi.Text }, hover = { background = null, textColor = RadioUi.Text },
                focused = { background = null, textColor = RadioUi.Text }, active = { background = null, textColor = RadioUi.Text },
            };
            _area = new GUIStyle(_field) { alignment = TextAnchor.UpperLeft, wordWrap = true, padding = new RectOffset(F(16), F(16), F(12), F(12)) };
        }

        private void OnGUI()
        {
            if (_surveyOwner != this) return;
            if (UI.PadCursor.ImguiPass(OnGUI)) return; // pad cursor: hover + clicks
            var mm = MatchManager.Instance;
            if (mm == null || NetPlayer.Local == null || mm.Phase.Value != MatchPhase.Lobby) { CloseSurvey(); return; }
            float s = Mathf.Min(Screen.height / 1080f, Screen.width / 1340f);
            Styles(s);
            GUI.depth = -50;
            float w = 900f * s, h = 520f * s, x = Mathf.Round((Screen.width - w) * 0.5f), y = Mathf.Round((Screen.height - h) * 0.5f), p = 30f * s;
            RadioUi.Draw(new Rect(0, 0, Screen.width, Screen.height), RadioUi.White, new Color(0f, 0f, 0f, 0.42f));
            RadioUi.Fill(new Rect(x - 4 * s, y + 6 * s, w + 8 * s, h + 8 * s), new Color(0f, 0f, 0f, 0.4f), 28 * s);
            RadioUi.Fill(new Rect(x, y, w, h), new Color(1f, 1f, 1f, 0.08f), 24 * s);
            RadioUi.Fill(new Rect(x + 1, y + 1, w - 2, h - 2), RadioUi.PanelBg, 23 * s);
            RadioUi.Fill(new Rect(x + 24 * s, y + 1, w - 48 * s, 3 * s), new Color(Orange.r, Orange.g, Orange.b, 0.6f), 2 * s);

            // ---- header: a tiny Claude face as the badge
            var badge = new Rect(x + p, y + p - 2 * s, 44 * s, 44 * s);
            RadioUi.Fill(badge, Orange, 10 * s);
            RadioUi.Fill(new Rect(badge.x + 12 * s, badge.y + 12 * s, 5 * s, 13 * s), new Color(0.07f, 0.05f, 0.05f), 2 * s);
            RadioUi.Fill(new Rect(badge.xMax - 17 * s, badge.y + 12 * s, 5 * s, 13 * s), new Color(0.07f, 0.05f, 0.05f), 2 * s);
            GUI.Label(new Rect(x + p + 58 * s, y + p - 6 * s, 600 * s, 38 * s), "CLAUDE", _title);
            string stepTxt = _step == 0 ? "Pytanie 1 z 2" : _step == 1 ? "Pytanie 2 z 2" : "Ankieta";
            GUI.Label(new Rect(x + p + 58 * s, y + p + 30 * s, 600 * s, 22 * s), "Krótka ankieta o grze - " + stepTxt, _sub);
            if (CloseButton(new Rect(x + w - p - 40 * s, y + p - 2 * s, 40 * s, 40 * s), s)) { CloseSurvey(); return; }
            RadioUi.Draw(new Rect(x + p, y + 92 * s, w - 2 * p, Mathf.Max(1f, s)), RadioUi.White, new Color(1f, 1f, 1f, 0.06f));

            float top = y + 116 * s, iw = w - 2 * p;
            var ev = Event.current;
            bool enter = ev.type == EventType.KeyDown && (ev.keyCode == KeyCode.Return || ev.keyCode == KeyCode.KeypadEnter);
            bool fresh = Time.unscaledTime - _openedAt < 0.25f; // the E that opened the panel must not land in the field
            float bw = 190 * s, bh = 56 * s, by = y + h - p - bh;

            if (_step == 0)
            {
                GUI.Label(new Rect(x + p, top, iw, 70 * s), Q1, _q);
                var f = new Rect(x + p, top + 86 * s, iw, 60 * s);
                FieldFrame(f, "ClaudeQ1", s);
                GUI.SetNextControlName("ClaudeQ1");
                _a1 = UI.PadKeyboard.Field("ClaudeQ1", f, _a1, 120, "Twoja propozycja nazwy"); // pad: on-screen keyboard
                _a1 = GUI.TextField(f, _a1 ?? "", 120, _field);
                if (fresh) _a1 = "";
                if (string.IsNullOrEmpty(_a1) && GUI.GetNameOfFocusedControl() != "ClaudeQ1")
                    GUI.Label(new Rect(f.x + 16 * s, f.y, f.width - 32 * s, f.height), "Wpisz swoją propozycję nazwy...", _small);
                if (fresh) GUI.FocusControl("ClaudeQ1");
                if (Button(new Rect(x + p, by, bw, bh), "Pomiń", false, s)) { _skipped1 = true; _a1 = ""; NextStep(); }
                if (Button(new Rect(x + w - p - bw, by, bw, bh), "Dalej", true, s) || (enter && !fresh))
                {
                    _skipped1 = string.IsNullOrWhiteSpace(_a1);
                    NextStep();
                    if (enter) ev.Use();
                }
            }
            else if (_step == 1)
            {
                GUI.Label(new Rect(x + p, top, iw, 36 * s), Q2, _q);
                GUI.Label(new Rect(x + p, top + 38 * s, iw, 24 * s), Q2Hint + " - pisz śmiało, czytam wszystko", _small);
                var f = new Rect(x + p, top + 72 * s, iw, 190 * s);
                FieldFrame(f, "ClaudeQ2", s);
                GUI.SetNextControlName("ClaudeQ2");
                _a2 = UI.PadKeyboard.Field("ClaudeQ2", f, _a2, MaxAnswer, "Co dodać albo poprawić w grze?"); // pad: on-screen keyboard
                _a2 = GUI.TextArea(f, _a2 ?? "", MaxAnswer, _area);
                if (string.IsNullOrEmpty(_a2) && GUI.GetNameOfFocusedControl() != "ClaudeQ2")
                    GUI.Label(new Rect(f.x + 16 * s, f.y + 12 * s, f.width - 32 * s, 30 * s), "Np. nowa mapa, poprawki, błędy, pomysły...", _small);
                GUI.Label(new Rect(x + p, f.yMax + 6 * s, iw, 22 * s), $"{(_a2 ?? "").Length}/{MaxAnswer}", _small);
                bool any = !string.IsNullOrWhiteSpace(_a2) || !_skipped1;
                if (Button(new Rect(x + p, by, bw, bh), "Wstecz", false, s)) { _step = 0; GUI.FocusControl("ClaudeQ1"); }
                if (Button(new Rect(x + w - p - bw, by, bw, bh), any ? "Wyślij" : "Zamknij", true, s))
                {
                    if (any) Send();
                    else { CloseSurvey(); return; }
                }
            }
            else
            {
                string msg = _sending ? "Wysyłam..." : _result ?? "";
                _q.normal.textColor = _sending ? RadioUi.Text : _ok ? new Color(0.55f, 0.9f, 0.6f) : RadioUi.Bad;
                GUI.Label(new Rect(x + p, top + 40 * s, iw, 80 * s), msg, _q);
                _q.normal.textColor = RadioUi.Text;
                if (!_sending && _ok) GUI.Label(new Rect(x + p, top + 120 * s, iw, 60 * s), "Twoja odpowiedź trafiła do twórcy gry.", _small);
                if (!_sending && Button(new Rect(x + w - p - bw, by, bw, bh), "Zamknij", true, s)) { CloseSurvey(); return; }
                if (!_sending && !_ok && Button(new Rect(x + p, by, bw, bh), "Wstecz", false, s)) _step = 1;
            }
        }

        private void NextStep()
        {
            _step = 1;
            GUI.FocusControl("ClaudeQ2");
        }

        private void Send()
        {
            _step = 2;
            if (Time.unscaledTime - _lastSent < SendCooldown)
            {
                _ok = false;
                _result = $"Ankieta poszła przed chwilą. Spróbuj ponownie za {Mathf.CeilToInt(SendCooldown - (Time.unscaledTime - _lastSent))} s.";
                return;
            }
            StartCoroutine(Post());
        }

        private IEnumerator Post()
        {
            _sending = true;
            string nick = string.IsNullOrWhiteSpace(LocalProfile.Nick) ? "(bez nicku)" : LocalProfile.Nick.Trim();
            string a1 = (_a1 ?? "").Trim(), a2 = (_a2 ?? "").Trim();
            var sb = new StringBuilder();
            sb.Append("**Ankieta od: ").Append(nick).Append("**  (wersja ").Append(UpdateChecker.CurrentVersion).Append(")\n\n");
            sb.Append("**1. ").Append(Q1).Append("**\n");
            sb.Append(_skipped1 || a1.Length == 0 ? "*pominięte*" : Quote(a1)).Append("\n\n");
            sb.Append("**2. ").Append(Q2).Append(" (").Append(Q2Hint).Append(")**\n");
            sb.Append(a2.Length == 0 ? "*brak odpowiedzi*" : Quote(a2));
            string content = sb.ToString();
            if (content.Length > 1990) content = content.Substring(0, 1990) + "...";
            string json = "{\"username\":\"Claude - ankieta\",\"content\":\"" + Json(content) + "\",\"allowed_mentions\":{\"parse\":[]}}";
            using var req = new UnityWebRequest(Webhook, "POST")
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = 15,
            };
            req.SetRequestHeader("Content-Type", "application/json");
            yield return req.SendWebRequest();
            _sending = false;
            _ok = req.result == UnityWebRequest.Result.Success;
            if (_ok)
            {
                _lastSent = Time.unscaledTime;
                _result = "Dzięki! Wysłane :]";
                Poke();
            }
            else
            {
                _result = "Nie udało się wysłać (" + (string.IsNullOrEmpty(req.error) ? "błąd sieci" : req.error) + ").";
                Debug.LogWarning($"[LobbyClaude] survey webhook failed: {req.responseCode} {req.error} {req.downloadHandler?.text}");
            }
        }

        /// <summary>Discord quote block, one "> " per line.</summary>
        private static string Quote(string s) => "> " + s.Replace("\r", "").Replace("\n", "\n> ");

        private static string Json(string s)
        {
            var sb = new StringBuilder(s.Length + 16);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        private static void FieldFrame(Rect f, string name, float s)
        {
            bool focused = GUI.GetNameOfFocusedControl() == name;
            RadioUi.Fill(f, focused ? new Color(Orange.r, Orange.g, Orange.b, 0.75f) : new Color(1f, 1f, 1f, 0.1f), 14 * s);
            RadioUi.Fill(new Rect(f.x + 1.5f * s, f.y + 1.5f * s, f.width - 3 * s, f.height - 3 * s), RadioUi.Card, 13 * s);
        }

        private bool Button(Rect r, string text, bool accent, float s)
        {
            bool hover = r.Contains(Event.current.mousePosition);
            Color bg = accent ? (hover ? Color.Lerp(Orange, Color.white, 0.18f) : Orange) : hover ? RadioUi.CardHover : RadioUi.Card;
            RadioUi.Fill(r, bg, 14 * s);
            GUI.Label(r, text, accent ? _btnDark : _btnText);
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        private static bool CloseButton(Rect r, float s)
        {
            bool hover = r.Contains(Event.current.mousePosition);
            if (hover) RadioUi.Fill(r, new Color(1f, 1f, 1f, 0.08f), r.width * 0.5f);
            float iw = r.width * 0.62f;
            RadioUi.Draw(new Rect(r.center.x - iw * 0.5f, r.center.y - iw * 0.5f, iw, iw), RadioUi.Icon("close"), hover ? Color.white : RadioUi.Dim);
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }
    }
}
