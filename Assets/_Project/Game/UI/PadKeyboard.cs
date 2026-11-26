using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HideAndSeek.Game.UI
{
    /// <summary>
    /// On-screen keyboard for the pad (IMGUI, bottom of the screen): Polish letters, digits, URL symbols on Shift,
    /// space, backspace, paste and OK. D-pad / left stick move the highlight, Cross types, Square = backspace,
    /// Triangle = space, L3 / R3 = Shift, L1 / R1 = caret left / right, Options = OK, Circle = cancel (the field gets
    /// its old text back). The mouse can click the keys too.
    ///
    /// Opens by itself when a text field gets focus while the pad is the active device: uGUI InputFields (nick, IP,
    /// Minecraft nick in the main menu) through the EventSystem selection, IMGUI fields (radio / TV link, Claude
    /// survey) through <see cref="Field"/>, called just before their GUI.TextField / TextArea.
    /// </summary>
    [DefaultExecutionOrder(-940)]
    public sealed class PadKeyboard : MonoBehaviour
    {
        private const int Cols = 12;

        private sealed class Key
        {
            public string Lower, Upper, Label;
            public int Col, Span = 1;
            public Action Act;
        }

        private static PadKeyboard _inst;
        private static int _closedFrame = -10;

        public static bool IsOpen => _inst != null && _inst._open;

        /// <summary>The keyboard is open or closed this frame: Cross / Circle / Options belong to it (menus skip them).</summary>
        public static bool ConsumesInput => IsOpen || _closedFrame >= Time.frameCount - 1;

        /// <summary>Text currently in the keyboard (tests).</summary>
        public static string Text => _inst != null ? _inst._text : "";

        /// <summary>Title of the open keyboard (tests / logs).</summary>
        public static string Title => _inst != null ? _inst._title : "";

        private bool _open, _shift;
        private string _text = "", _original = "", _title = "";
        private int _max = 100, _caret;
        private Action<string> _change, _done;
        private InputField _field;
        private int _row = 1, _col;
        private Key[][] _rows;
        private GUIStyle _keyStyle, _textStyle, _titleStyle, _hintStyle;
        private int _styleSize;

        // IMGUI fields: one result is handed back to the field it belongs to.
        private static string _imguiId, _doneId, _doneText;

        private void Awake()
        {
            _inst = this;
            BuildRows();
        }

        // ================================================================ public

        /// <summary>Opens the keyboard. <paramref name="change"/> gets every edit (live preview), <paramref name="done"/> the final text (the original one after a cancel).</summary>
        public static void Open(string title, string text, int maxLength, Action<string> change, Action<string> done)
        {
            if (_inst == null) return;
            var k = _inst;
            if (k._open) k.Finish(false);
            k._open = true;
            k._title = title ?? "";
            k._original = text ?? "";
            k._text = k._original;
            k._caret = k._text.Length;
            k._max = maxLength > 0 ? maxLength : 100;
            k._change = change;
            k._done = done;
            k._shift = k._text.Length == 0; // first letter upper-case, like a phone
            k._row = 1;
            k._col = 0;
            PadCursor.Frozen = true;
            Debug.Log($"[PadKeyboard] open \"{k._title}\" ({k._text.Length} chars)");
        }

        /// <summary>
        /// IMGUI text field hook, called right before the field's GUI.TextField / TextArea with the same rect:
        /// <c>_url = PadKeyboard.Field("RadioUrl", rect, _url, 200, "Link z YouTube");</c>. A pad click on the rect opens
        /// the keyboard (the click is used, the field does not take the focus); while it is open the field shows the
        /// typed text, after OK / cancel the field gets the final text.
        /// </summary>
        public static string Field(string id, Rect r, string value, int maxLength, string title)
        {
            if (_doneId == id)
            {
                _doneId = null;
                return _doneText;
            }
            if (IsOpen && _imguiId == id) return _inst._text;
            var e = Event.current;
            if (_inst != null && PadCursor.InPadPass && e != null && e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition))
            {
                e.Use();
                GUIUtility.keyboardControl = 0;
                _imguiId = id;
                Open(title, value, maxLength, null, t =>
                {
                    _doneId = id;
                    _doneText = t;
                    _imguiId = null;
                });
            }
            return value;
        }

        /// <summary>Tests: presses a key as Cross would (by its lower-case / upper-case text or label, e.g. "ó", "OK", "Shift").</summary>
        public static bool TestPress(string key)
        {
            if (!IsOpen) return false;
            var k = _inst;
            foreach (var row in k._rows)
            foreach (var x in row)
                if (x.Lower == key || x.Upper == key || x.Label == key)
                {
                    k.Press(x);
                    return true;
                }
            return false;
        }

        /// <summary>Tests: the key under the highlight (its lower-case text or label).</summary>
        public static string Highlighted => _inst != null && _inst._open ? (_inst.At(_inst._row, _inst._col)?.Lower ?? _inst.At(_inst._row, _inst._col)?.Label) : null;

        /// <summary>Tests: row / column of the key with this text (for steering the highlight with the D-pad).</summary>
        public static bool Locate(string key, out int row, out int col)
        {
            row = col = -1;
            if (_inst == null) return false;
            for (int r = 0; r < _inst._rows.Length; r++)
            foreach (var x in _inst._rows[r])
                if (x.Lower == key || x.Upper == key || x.Label == key)
                {
                    row = r;
                    col = x.Col;
                    return true;
                }
            return false;
        }

        public static int Row => _inst != null ? _inst._row : -1;
        public static int Col => _inst != null ? _inst._col : -1;
        public static bool ShiftOn => _inst != null && _inst._shift;

        // ================================================================ layout

        private void BuildRows()
        {
            Key[] Chars(string lower, string upper)
            {
                var r = new Key[lower.Length];
                for (int i = 0; i < lower.Length; i++) r[i] = new Key { Lower = lower[i].ToString(), Upper = upper[i].ToString(), Col = i };
                return r;
            }
            _rows = new[]
            {
                Chars("1234567890-=", "!@#&?/:_()+%"),
                Chars("qwertyuiopąę", "QWERTYUIOPĄĘ"),
                Chars("asdfghjklłśó", "ASDFGHJKLŁŚÓ"),
                Chars("zxcvbnmćńźż.", "ZXCVBNMĆŃŹŻ,"),
                new[]
                {
                    new Key { Label = "Shift", Col = 0, Span = 2, Act = () => _shift = !_shift },
                    new Key { Label = "Wklej", Col = 2, Span = 2, Act = Paste },
                    new Key { Label = "Spacja", Col = 4, Span = 4, Act = () => Insert(" ") },
                    new Key { Label = "Usuń", Col = 8, Span = 2, Act = Backspace },
                    new Key { Label = "OK", Col = 10, Span = 2, Act = () => Finish(true) },
                },
            };
        }

        private Key At(int row, int col)
        {
            if (row < 0 || row >= _rows.Length) return null;
            foreach (var k in _rows[row])
                if (col >= k.Col && col < k.Col + k.Span) return k;
            return null;
        }

        // ================================================================ frame

        private void Update()
        {
            if (!_open)
            {
                WatchInputFields();
                return;
            }
            PadCursor.Frozen = true;
            if (_field != null && !_field.isActiveAndEnabled) { Finish(false); return; }

            var nav = PadUiInput.Nav;
            if (nav.y != 0) _row = (_row - nav.y + _rows.Length) % _rows.Length;
            if (nav.x != 0)
            {
                var cur = At(_row, _col);
                int c = nav.x > 0 ? (cur != null ? cur.Col + cur.Span : _col + 1) : (cur != null ? cur.Col - 1 : _col - 1);
                _col = (c + Cols) % Cols;
                var landed = At(_row, _col);
                if (landed != null && nav.x < 0) _col = landed.Col;
            }
            if (PadUiInput.Cross) { var k = At(_row, _col); if (k != null) Press(k); }
            else if (PadUiInput.Square) Backspace();
            else if (PadUiInput.Triangle) Insert(" ");
            else if (PadUiInput.L3 || PadUiInput.R3) _shift = !_shift;
            else if (PadUiInput.L1) _caret = Mathf.Max(0, _caret - 1);
            else if (PadUiInput.R1) _caret = Mathf.Min(_text.Length, _caret + 1);
            else if (PadUiInput.Options) Finish(true);
            else if (PadUiInput.Circle) Finish(false);
        }

        /// <summary>uGUI InputField selected (clicked with the pad cursor): open the keyboard for it.</summary>
        private void WatchInputFields()
        {
            if (!PadCursor.PadMode || _closedFrame >= Time.frameCount - 2) return;
            var es = EventSystem.current;
            var go = es != null ? es.currentSelectedGameObject : null;
            var f = go != null ? go.GetComponent<InputField>() : null;
            if (f == null || !f.interactable || !f.isActiveAndEnabled) return;
            f.DeactivateInputField();
            es.SetSelectedGameObject(null);
            _field = f;
            string title = f.placeholder is Text ph && !string.IsNullOrEmpty(ph.text) ? ph.text : f.name;
            Open(title, f.text, f.characterLimit > 0 ? f.characterLimit : 100, t => { if (f != null) f.text = t; }, t =>
            {
                if (f == null) return;
                f.text = t;
                f.onEndEdit?.Invoke(t);
            });
        }

        private void Press(Key k)
        {
            if (k.Act != null) { k.Act(); return; }
            Insert(_shift ? k.Upper : k.Lower);
            if (_shift && char.IsLetter(k.Lower[0])) _shift = false; // one-shot Shift for letters
        }

        private void Insert(string s)
        {
            if (string.IsNullOrEmpty(s) || _text.Length + s.Length > _max) return;
            _caret = Mathf.Clamp(_caret, 0, _text.Length);
            _text = _text.Insert(_caret, s);
            _caret += s.Length;
            _change?.Invoke(_text);
        }

        private void Backspace()
        {
            _caret = Mathf.Clamp(_caret, 0, _text.Length);
            if (_caret == 0) return;
            _text = _text.Remove(_caret - 1, 1);
            _caret--;
            _change?.Invoke(_text);
        }

        private void Paste()
        {
            string clip = (GUIUtility.systemCopyBuffer ?? "").Replace("\r", "").Replace("\n", " ").Trim();
            if (clip.Length > _max - _text.Length) clip = clip.Substring(0, Mathf.Max(0, _max - _text.Length));
            Insert(clip);
        }

        private void Finish(bool ok)
        {
            if (!_open) return;
            _open = false;
            _closedFrame = Time.frameCount;
            PadCursor.Frozen = false;
            string result = ok ? _text : _original;
            var done = _done;
            var change = _change;
            _done = _change = null;
            _field = null;
            if (!ok) change?.Invoke(result);
            done?.Invoke(result);
            Debug.Log($"[PadKeyboard] {(ok ? "OK" : "cancel")}: \"{result}\"");
        }

        private void OnDisable()
        {
            if (_open) Finish(false);
        }

        // ================================================================ drawing

        private void OnGUI()
        {
            if (!_open) return;
            GUI.depth = -9000;
            float s = Mathf.Min(Screen.height / 1080f, Screen.width / 1920f);
            Styles(s);
            float keyW = 86f * s, keyH = 64f * s, gap = 8f * s, pad = 24f * s;
            float w = Cols * keyW + (Cols - 1) * gap + 2 * pad;
            float h = pad + 40f * s + 70f * s + 16f * s + _rows.Length * (keyH + gap) + 34f * s + pad;
            float x = Mathf.Round((Screen.width - w) * 0.5f), y = Mathf.Round(Screen.height - h - 30f * s);

            RadioUi.Fill(new Rect(x - 4 * s, y + 6 * s, w + 8 * s, h + 8 * s), new Color(0f, 0f, 0f, 0.45f), 26 * s);
            RadioUi.Fill(new Rect(x, y, w, h), new Color(1f, 1f, 1f, 0.1f), 22 * s);
            RadioUi.Fill(new Rect(x + 1, y + 1, w - 2, h - 2), RadioUi.PanelBg, 21 * s);

            float cy = y + pad;
            GUI.Label(new Rect(x + pad, cy, w - 2 * pad, 36f * s), _title.ToUpperInvariant(), _titleStyle);
            cy += 40f * s;
            var tr = new Rect(x + pad, cy, w - 2 * pad, 62f * s);
            RadioUi.Fill(tr, new Color(RadioUi.Accent.r, RadioUi.Accent.g, RadioUi.Accent.b, 0.7f), 14 * s);
            RadioUi.Fill(new Rect(tr.x + 1.5f * s, tr.y + 1.5f * s, tr.width - 3 * s, tr.height - 3 * s), RadioUi.Card, 13 * s);
            bool blink = Mathf.Repeat(Time.unscaledTime, 1f) < 0.55f;
            int caret = Mathf.Clamp(_caret, 0, _text.Length);
            string shown = _text.Substring(0, caret) + (blink ? "|" : " ") + _text.Substring(caret);
            shown = RadioUi.Fit(shown, _textStyle, tr.width - 32f * s);
            GUI.Label(new Rect(tr.x + 16f * s, tr.y, tr.width - 32f * s, tr.height), shown, _textStyle);
            cy += 70f * s + 16f * s;

            for (int r = 0; r < _rows.Length; r++)
            {
                foreach (var k in _rows[r])
                {
                    var kr = new Rect(x + pad + k.Col * (keyW + gap), cy, k.Span * keyW + (k.Span - 1) * gap, keyH);
                    bool sel = r == _row && _col >= k.Col && _col < k.Col + k.Span;
                    bool hover = kr.Contains(Event.current.mousePosition);
                    bool lit = k.Label == "Shift" && _shift;
                    Color bg = sel ? new Color(RadioUi.Accent.r, RadioUi.Accent.g, RadioUi.Accent.b, 0.95f)
                        : lit ? new Color(RadioUi.Accent.r, RadioUi.Accent.g, RadioUi.Accent.b, 0.35f)
                        : hover ? RadioUi.CardHover : RadioUi.Card;
                    RadioUi.Fill(kr, bg, 10 * s);
                    _keyStyle.normal.textColor = sel ? new Color(0.05f, 0.08f, 0.06f) : RadioUi.Text;
                    string label = k.Label ?? (_shift ? k.Upper : k.Lower);
                    GUI.Label(kr, label, _keyStyle);
                    if (GUI.Button(kr, GUIContent.none, GUIStyle.none))
                    {
                        _row = r;
                        _col = k.Col;
                        Press(k);
                    }
                }
                cy += keyH + gap;
            }
            GUI.Label(new Rect(x + pad, cy + 2f * s, w - 2 * pad, 30f * s),
                "Krzyżyk - wpisz    Kwadrat - usuń    Trójkąt - spacja    L3 - Shift    L1 / R1 - kursor    Options - OK    Kółko - anuluj", _hintStyle);
        }

        private void Styles(float s)
        {
            int size = Mathf.RoundToInt(30f * s);
            if (_keyStyle != null && _styleSize == size) return;
            _styleSize = size;
            int F(float v) => Mathf.Max(8, Mathf.RoundToInt(v * s));
            _keyStyle = RadioUi.Label(F(28), RadioUi.Text, FontStyle.Bold, TextAnchor.MiddleCenter);
            _textStyle = RadioUi.Label(F(28), RadioUi.Text);
            _titleStyle = RadioUi.Label(F(20), RadioUi.Dim, FontStyle.Bold);
            _hintStyle = RadioUi.Label(F(17), RadioUi.Dim, FontStyle.Normal, TextAnchor.MiddleCenter);
        }
    }
}
