using System;
using System.Collections;
using System.IO;
using System.Text;
using HideAndSeek.Game.Net;
using HideAndSeek.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace HideAndSeek.Game.UI
{
    /// <summary>
    /// Pad-only menu test (-testpadui &lt;dir&gt;; start in the main menu, e.g. -batchmode -nographics -nosteam -nodiscord
    /// -nodiscovery -port N -testpadui out): adds a virtual Gamepad (the only input device) and drives it with state
    /// events. Main menu: the stick moves the <see cref="PadCursor"/> onto "Ustawienia" and Cross opens it, R1 / R1 / L1
    /// flip the settings pages, the D-pad steps a slider, Circle goes back twice; "Postać": Cross on the nick field opens
    /// the <see cref="PadKeyboard"/>, the D-pad + Cross type "Padówka", Options = OK; then "Serwer LAN / UDP" hosts. In
    /// the lobby: Options opens the Esc menu, Circle closes it, Share opens the emote wheel, the right stick picks a
    /// slice, Cross plays it. Logs "[PadUiTest] ... PASS / FAIL", a RESULT line, writes the log to &lt;dir&gt;/padui.txt
    /// (IMGUI is not drawn into camera targets and batchmode has no screen: state is logged instead of pictures), the
    /// player's own nick is put back, then quits.
    /// </summary>
    public sealed class PadUiTest : MonoBehaviour
    {
        private Gamepad _pad;
        private GamepadState _state;
        private int _fails;
        private readonly StringBuilder _log = new();
        private string _emoteLine;

        private void Start() => StartCoroutine(Run());

        private void Log(string s)
        {
            Debug.Log("[PadUiTest] " + s);
            _log.AppendLine(s);
        }

        private string Check(string what, bool ok)
        {
            if (!ok) _fails++;
            Log($"{what} -> {(ok ? "PASS" : "FAIL")}");
            return ok ? "PASS" : "FAIL";
        }

        // ================================================================ pad driving

        private void Apply()
        {
            InputSystem.QueueStateEvent(_pad, _state);
        }

        private IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        private IEnumerator Press(GamepadButton b, int hold = 2)
        {
            _state = _state.WithButton(b, true);
            Apply();
            yield return Frames(hold);
            _state = _state.WithButton(b, false);
            Apply();
            yield return Frames(3);
        }

        private void Stick(Vector2 right)
        {
            _state.rightStick = right;
            Apply();
        }

        /// <summary>Steers the pad cursor with the right stick onto a screen point (closed loop).</summary>
        private IEnumerator SteerTo(Func<Vector2> target, float timeout = 8f)
        {
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < timeout)
            {
                // Target read every frame (a page shown this frame still gets its layout); stick values above the
                // Input System dead zone (0.125) so the cursor never stalls short of the target.
                Vector2 d = target() - PadCursor.Position;
                if (d.magnitude < 4f) break;
                Vector2 s = Vector2.ClampMagnitude(d / 120f, 1f);
                if (s.magnitude < 0.45f) s = s.normalized * 0.45f;
                Stick(s);
                yield return null;
            }
            Stick(Vector2.zero);
            yield return Frames(3);
        }

        private static T FindActive<T>(string name) where T : Component
        {
            foreach (var c in FindObjectsByType<T>(FindObjectsSortMode.None))
                if (c.name == name && c.gameObject.activeInHierarchy) return c;
            return null;
        }

        private IEnumerator ClickNamed(string name)
        {
            var b = FindActive<Selectable>(name);
            if (b == null) { Log($"no active control \"{name}\""); yield break; }
            yield return Frames(2);
            yield return SteerTo(() => b != null ? PadCursor.ScreenRect(b).center : PadCursor.Position);
            var under = PadCursor.SelectableUnder(PadCursor.Position);
            Log($"cursor on \"{(under != null ? under.name : "-")}\" (wanted {name}) at {PadCursor.Position}");
            yield return Press(GamepadButton.South);
            yield return Frames(4);
        }

        private IEnumerator WaitFor(Func<bool> cond, float timeout)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!cond() && Time.realtimeSinceStartup - t0 < timeout) yield return null;
        }

        /// <summary>Moves the keyboard highlight with the D-pad to a key and presses Cross.</summary>
        private IEnumerator TypeKey(string key)
        {
            if (!PadKeyboard.Locate(key, out int row, out int col)) { Log($"no key {key}"); yield break; }
            for (int guard = 0; guard < 40 && (PadKeyboard.Row != row || PadKeyboard.Col != col); guard++)
            {
                var b = PadKeyboard.Row > row ? GamepadButton.DpadUp : PadKeyboard.Row < row ? GamepadButton.DpadDown
                    : PadKeyboard.Col > col ? GamepadButton.DpadLeft : GamepadButton.DpadRight;
                yield return Press(b);
            }
            yield return Press(GamepadButton.South);
        }

        // ================================================================ run

        private IEnumerator Run()
        {
            string dir = Arg("-testpadui");
            Application.logMessageReceived += OnLog;
            string oldNick = LocalProfile.Nick;
            yield return WaitFor(() => MainMenu.CurrentPage == "Main", 60f);
            yield return new WaitForSecondsRealtime(1.5f);
            Log($"main menu: page {MainMenu.CurrentPage}, screen {Screen.width}x{Screen.height}, devices {InputSystem.devices.Count}");

            _pad = InputSystem.AddDevice<Gamepad>("PadUiTestGamepad");
            PadInput.ForcedPad = _pad;
            _state = new GamepadState();
            Apply();
            yield return Frames(3);
            yield return Press(GamepadButton.DpadUp); // any pad input: the pad becomes the active device
            yield return WaitFor(() => PadCursor.Active, 3f);
            Check($"pad cursor shown (using pad {PadInput.UsingPad}, cursor at {PadCursor.Position})", PadCursor.Active);

            // ---- settings with the cursor, pages with L1 / R1
            yield return ClickNamed("Options");
            Check($"Cross on \"Ustawienia\" opens settings (page {MainMenu.CurrentPage})", MainMenu.CurrentPage == "Options");
            yield return Press(GamepadButton.RightShoulder);
            string p1 = MainMenu.CurrentPage;
            yield return Press(GamepadButton.RightShoulder);
            string p2 = MainMenu.CurrentPage;
            yield return Press(GamepadButton.LeftShoulder);
            string p3 = MainMenu.CurrentPage;
            Check($"R1, R1, L1 -> {p1}, {p2}, {p3}", p1 == "Audio" && p2 == "Voice" && p3 == "Audio");

            // ---- change a value: cursor onto the first slider, D-pad right steps it
            Slider slider = null;
            foreach (var s in FindObjectsByType<Slider>(FindObjectsSortMode.None))
                if (s.gameObject.activeInHierarchy && s.IsInteractable() && (slider == null || PadCursor.ScreenRect(s).center.y > PadCursor.ScreenRect(slider).center.y)) slider = s;
            if (slider != null)
            {
                float before = slider.value;
                yield return SteerTo(() => PadCursor.ScreenRect(slider).center);
                yield return Press(before <= slider.minValue + 0.0001f ? GamepadButton.DpadRight : GamepadButton.DpadLeft);
                float after = slider.value;
                Check($"D-pad on slider \"{slider.transform.parent?.name}\": {before:0.000} -> {after:0.000}", Mathf.Abs(after - before) > 0.0001f);
                slider.value = before; // leave the setting as it was
            }
            else Check("slider found on the audio page", false);

            yield return Press(GamepadButton.East);
            string b1 = MainMenu.CurrentPage;
            yield return Press(GamepadButton.East);
            string b2 = MainMenu.CurrentPage;
            Check($"Circle, Circle -> {b1}, {b2}", b1 == "Options" && b2 == "Main");

            // ---- nick with the on-screen keyboard
            yield return ClickNamed("Character");
            Check($"Cross on \"Postać\" (page {MainMenu.CurrentPage})", MainMenu.CurrentPage == "Character");
            yield return ClickNamed("Nick");
            yield return WaitFor(() => PadKeyboard.IsOpen, 2f);
            if (Check($"Cross on the nick field opens the keyboard (\"{PadKeyboard.Title}\", text \"{PadKeyboard.Text}\")", PadKeyboard.IsOpen) == "FAIL")
            {
                // No blind D-pad / Cross presses in the menu (they would click around, even "Wyjdź").
                Log("RESULT: stopped early, keyboard did not open");
                Finish(dir, oldNick);
                yield break;
            }
            for (int i = 0; i < 20 && PadKeyboard.Text.Length > 0; i++) yield return Press(GamepadButton.West); // Square = backspace
            if (!PadKeyboard.ShiftOn) yield return Press(GamepadButton.LeftStick); // L3 = Shift
            foreach (char ch in "padówka") yield return TypeKey(ch.ToString());
            Log($"typed \"{PadKeyboard.Text}\"");
            yield return Press(GamepadButton.Start); // Options = OK
            var nick = FindActive<InputField>("Nick");
            Check($"keyboard closed, nick field \"{(nick != null ? nick.text : "?")}\", profile \"{LocalProfile.Nick}\"",
                !PadKeyboard.IsOpen && nick != null && nick.text == "Padówka" && LocalProfile.Nick == "Padówka");
            yield return Press(GamepadButton.East);
            Check($"Circle -> {MainMenu.CurrentPage}", MainMenu.CurrentPage == "Main");

            // ---- host
            yield return ClickNamed("Host");
            yield return WaitFor(() => NetPlayer.Local != null && NetPlayer.Local.IsSpawned && CursorPolicy.Active, 90f);
            Check($"hosted with the pad (local player {(NetPlayer.Local != null ? NetPlayer.Local.Nick : "-")})", NetPlayer.Local != null);
            yield return new WaitForSecondsRealtime(3f);

            // ---- Esc menu: Options opens, Circle closes
            yield return Press(GamepadButton.Start);
            yield return Frames(5);
            bool opened = LocalPlayerController.MenuOpen;
            Log($"after Options: menu {opened}, pad cursor {PadCursor.Active}");
            yield return Press(GamepadButton.East);
            yield return Frames(5);
            Check($"Options opens the Esc menu ({opened}), Circle closes it ({!LocalPlayerController.MenuOpen})", opened && !LocalPlayerController.MenuOpen);

            // ---- emote wheel: tap Share (stays open), right stick to a slice, Cross plays it
            yield return Frames(10);
            yield return Press(GamepadButton.Select, 2);
            yield return Frames(5);
            bool wheel = EmoteWheel.IsOpen;
            int n = EmoteWheel.SliceCount;
            Vector2 aim = n > 0 ? RingSector.Dir(2 * 360f / n) : Vector2.up;
            Stick(aim);
            yield return Frames(10);
            _emoteLine = null;
            yield return Press(GamepadButton.South);
            Stick(Vector2.zero);
            yield return Frames(10);
            Check($"Share opens the emote wheel ({wheel}, {n} slices), stick + Cross picks: \"{_emoteLine}\", wheel closed {!EmoteWheel.IsOpen}",
                wheel && _emoteLine != null && !EmoteWheel.IsOpen);

            // ---- everything closed
            yield return new WaitForSecondsRealtime(1f);
            Check($"all closed (menu {LocalPlayerController.MenuOpen}, dialog {ConfirmDialog.IsOpen}, keyboard {PadKeyboard.IsOpen}, wheel {EmoteWheel.IsOpen})",
                !LocalPlayerController.MenuOpen && !ConfirmDialog.IsOpen && !PadKeyboard.IsOpen && !EmoteWheel.IsOpen);

            Log(_fails == 0 ? "RESULT: all checks PASS" : $"RESULT: {_fails} check(s) FAIL");
            Finish(dir, oldNick);
        }

        /// <summary>Puts the player's nick back, writes the log file and quits.</summary>
        private void Finish(string dir, string oldNick)
        {
            LocalProfile.Nick = oldNick; // the test must not keep the player's nick
            Prefs.Save();
            if (!string.IsNullOrEmpty(dir))
            {
                try
                {
                    Directory.CreateDirectory(dir);
                    File.WriteAllText(Path.Combine(dir, "padui.txt"), _log.ToString());
                }
                catch (Exception e) { Debug.LogWarning($"[PadUiTest] log file: {e.Message}"); }
            }
            Application.logMessageReceived -= OnLog;
            PadInput.ForcedPad = null;
            Application.Quit();
        }

        private void OnLog(string msg, string stack, LogType type)
        {
            if (msg.StartsWith("[EmoteWheel] picked", StringComparison.Ordinal)) _emoteLine = msg;
        }

        private static string Arg(string key)
        {
            var a = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(a, key);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
        }
    }
}
