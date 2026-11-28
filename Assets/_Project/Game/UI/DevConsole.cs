using System.Collections.Generic;
using HideAndSeek.Game.Net;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HideAndSeek.Game.UI
{
    /// <summary>
    /// In-game debug console (28.11): ` (the key under Esc) opens / closes it, Esc closes it. A dark panel over the top of
    /// the screen with the output and an input line: Enter runs the line, Up / Down go through the history, Tab completes a
    /// nick. While it is open the player does not move (<see cref="LocalPlayerController.MenuOpen"/>) and the cursor is
    /// free. Commands: <see cref="DevConsoleCommands"/> (`help` lists them). Works in the menu too (commands that need a
    /// game say so).
    /// </summary>
    public sealed class DevConsole : MonoBehaviour
    {
        private const int MaxLines = 200;
        private const string InputName = "DevConsoleInput";

        public static bool IsOpen { get; private set; }
        /// <summary>Esc closes the console instead of opening the game menu (GameHud).</summary>
        public static bool ConsumesEscape => IsOpen || _closedFrame == Time.frameCount;
        private static int _closedFrame = -1;

        private static DevConsole _inst;
        private static readonly List<string> Lines = new();
        private readonly List<string> _history = new();
        private int _histAt = -1;
        private string _input = "";
        private Vector2 _scroll;
        private bool _focus, _scrollDown, _heldMenu;
        private GUIStyle _text, _field;
        private Texture2D _bg;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (_inst != null) return;
            var go = new GameObject("DevConsole");
            DontDestroyOnLoad(go);
            _inst = go.AddComponent<DevConsole>();
            Print("Konsola: wpisz <b>help</b>, żeby zobaczyć komendy. ` - zamknij.");
        }

        /// <summary>Adds a line to the output (rich text allowed).</summary>
        public static void Print(string line)
        {
            Lines.Add(line);
            if (Lines.Count > MaxLines) Lines.RemoveRange(0, Lines.Count - MaxLines);
            if (_inst != null) _inst._scrollDown = true;
            Debug.Log("[Console] " + System.Text.RegularExpressions.Regex.Replace(line, "<.*?>", ""));
        }

        public static void Clear() => Lines.Clear();

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.backquoteKey.wasPressedThisFrame)
            {
                if (IsOpen) Close();
                else if (!LocalPlayerController.MenuOpen && IsHost) Open(); // only the server / lobby creator; not over another panel
            }
            else if (IsOpen && kb.escapeKey.wasPressedThisFrame) Close();
            if (IsOpen && !IsHost) Close(); // the session ended / became a client
            if (IsOpen && !LocalPlayerController.MenuOpen) LocalPlayerController.MenuOpen = true; // something closed its panel under us
        }

        /// <summary>The console belongs to whoever created the server / lobby (the host); players who joined cannot open it.</summary>
        public static bool IsHost => Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsServer;

        private void Open()
        {
            IsOpen = true;
            _focus = true;
            _scrollDown = true;
            _histAt = -1;
            _heldMenu = true;
            LocalPlayerController.MenuOpen = true;
            var me = NetPlayer.Local;
            if (me != null && me.InputReader != null) me.InputReader.SetCursorLocked(false);
        }

        private void Close()
        {
            IsOpen = false;
            _closedFrame = Time.frameCount;
            _input = "";
            if (_heldMenu) LocalPlayerController.MenuOpen = false;
            _heldMenu = false;
            var me = NetPlayer.Local;
            if (me != null && me.InputReader != null) me.InputReader.SetCursorLocked(true);
        }

        private void Run(string line)
        {
            line = line.Trim();
            if (line.Length == 0) return;
            if (_history.Count == 0 || _history[_history.Count - 1] != line) _history.Add(line);
            _histAt = -1;
            Print("<color=#8fd0ff>> " + line + "</color>");
            try { DevConsoleCommands.Execute(line); }
            catch (System.Exception e)
            {
                Print("<color=#ff7a70>Błąd: " + e.Message + "</color>");
                Debug.LogException(e);
            }
        }

        private void OnGUI()
        {
            if (!IsOpen) return;
            float s = Mathf.Max(0.75f, Screen.height / 1080f);
            if (_text == null)
            {
                _bg = new Texture2D(1, 1);
                _bg.SetPixel(0, 0, Color.white);
                _bg.Apply();
                _text = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true, font = Font.CreateDynamicFontFromOSFont("Consolas", 16) };
                _field = new GUIStyle(GUI.skin.textField) { font = _text.font };
            }
            _text.fontSize = Mathf.RoundToInt(17 * s);
            _field.fontSize = Mathf.RoundToInt(18 * s);
            _text.normal.textColor = new Color(0.88f, 0.9f, 0.92f);

            float h = Screen.height * 0.42f, pad = 10f * s, lineH = 30f * s;
            GUI.color = new Color(0.03f, 0.035f, 0.045f, 0.9f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, h), _bg);
            GUI.color = new Color(0.3f, 0.75f, 1f, 0.8f);
            GUI.DrawTexture(new Rect(0f, h - 2f, Screen.width, 2f), _bg);
            GUI.color = Color.white;

            // Keys first (before the text field eats them).
            var e = Event.current;
            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                {
                    string line = _input;
                    _input = "";
                    Run(line);
                    e.Use();
                    _focus = true;
                }
                else if (e.keyCode == KeyCode.UpArrow && _history.Count > 0)
                {
                    _histAt = _histAt < 0 ? _history.Count - 1 : Mathf.Max(0, _histAt - 1);
                    _input = _history[_histAt];
                    e.Use();
                }
                else if (e.keyCode == KeyCode.DownArrow && _histAt >= 0)
                {
                    _histAt++;
                    if (_histAt >= _history.Count) { _histAt = -1; _input = ""; }
                    else _input = _history[_histAt];
                    e.Use();
                }
                else if (e.keyCode == KeyCode.Tab)
                {
                    _input = DevConsoleCommands.Complete(_input);
                    e.Use();
                }
                else if (e.keyCode == KeyCode.BackQuote || e.character == '`' || e.character == '~')
                {
                    e.Use(); // the toggle key never lands in the line
                }
            }

            var outRect = new Rect(pad, pad, Screen.width - pad * 2f, h - lineH - pad * 3f);
            float w = outRect.width - 20f * s;
            float total = 0f;
            var heights = new float[Lines.Count];
            for (int i = 0; i < Lines.Count; i++) { heights[i] = _text.CalcHeight(new GUIContent(Lines[i]), w); total += heights[i]; }
            if (_scrollDown) { _scroll.y = Mathf.Max(0f, total - outRect.height); _scrollDown = false; }
            _scroll = GUI.BeginScrollView(outRect, _scroll, new Rect(0f, 0f, w, Mathf.Max(total, outRect.height)));
            float y = 0f;
            for (int i = 0; i < Lines.Count; i++)
            {
                GUI.Label(new Rect(0f, y, w, heights[i]), Lines[i], _text);
                y += heights[i];
            }
            GUI.EndScrollView();

            GUI.SetNextControlName(InputName);
            _input = GUI.TextField(new Rect(pad, h - lineH - pad, Screen.width - pad * 2f, lineH), _input ?? "", 300, _field);
            _input = _input.Replace("`", "").Replace("~", "");
            if (_focus)
            {
                GUI.FocusControl(InputName);
                _focus = false;
            }
        }
    }
}
