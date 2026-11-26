using System;
using HideAndSeek.Audio;
using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using HideAndSeek.Player;

namespace HideAndSeek.Game.UI
{
    /// <summary>
    /// Body carry HUD (own canvas):
    /// <list type="bullet">
    /// <item>the question for the carried player: "&lt;nick&gt; chce cię podnieść i nieść - [Y] tak / [N] nie" with a 10 s bar
    /// (no answer = no);</item>
    /// <item>one status line under the crosshair: the "B przytrzymaj" hint with its hold bar, "waiting for the answer",
    /// "you carry X - B puts down", "X carries you - Space asks to be put down".</item>
    /// </list>
    /// Separate from the generic prompts so it can later move onto a shared consent system unchanged (keys Y / N).
    /// </summary>
    public class CarryConsentPrompt : MonoBehaviour
    {
        private static CarryConsentPrompt _instance;

        /// <summary>Question currently shown (tests).</summary>
        public static bool AskOpen => _instance != null && _instance._ask != null;
        /// <summary>Status line text (tests).</summary>
        public static string LineText { get; private set; } = "";

        /// <summary>Set every frame by the local controller: hint above the hold bar (empty = none) and hold progress 0..1.</summary>
        public static string Hint = "";
        public static float HintProgress;

        private Action<bool> _ask;
        private float _askUntil, _askTotal, _autoAt;
        private string _waitNick;
        private float _waitUntil;

        private Image _panel, _bar, _holdBg, _holdBar;
        private Text _title, _keys, _line;

        public static void Ensure()
        {
            if (_instance != null) return;
            var go = new GameObject("[CarryConsentPrompt]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<CarryConsentPrompt>();
        }

        /// <summary>Shows the question; <paramref name="answer"/> gets true / false exactly once.</summary>
        public static void Ask(string nick, float seconds, bool auto, Action<bool> answer)
        {
            Ensure();
            var i = _instance;
            if (i._ask != null) i.Close(false);
            i._ask = answer;
            i._askTotal = Mathf.Max(1f, seconds);
            i._askUntil = Time.unscaledTime + i._askTotal;
            i._autoAt = auto ? Time.unscaledTime + 1.2f : -1f;
            i._title.text = $"<b>{nick}</b> chce cię podnieść i nieść na rękach";
            i._panel.gameObject.SetActive(true);
            GameAudio.Play2D(GameSfx.UiClick, 0.6f);
            Debug.Log($"[Carry] question from {nick} ({seconds:0} s{(auto ? ", auto yes" : "")})");
        }

        /// <summary>Carrier: the question went out, waiting for the answer.</summary>
        public static void Waiting(string nick, float seconds)
        {
            Ensure();
            _instance._waitNick = nick;
            _instance._waitUntil = Time.unscaledTime + seconds;
        }

        /// <summary>Carrier: the answer came (either way).</summary>
        public static void Answered()
        {
            if (_instance != null) _instance._waitUntil = 0f;
        }

        private void Awake()
        {
            var canvas = UiKit.CreateCanvas("CarryCanvas", 12, transform);
            _panel = UiStyle.Panel("Ask", canvas.transform, new Color(0.07f, 0.08f, 0.09f, 0.9f));
            _panel.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(640f, 118f));
            _title = UiKit.Label("Title", _panel.transform, "", 25, TextAnchor.MiddleCenter);
            _title.supportRichText = true;
            _title.rectTransform.Place(new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(-24f, 36f));
            _keys = UiKit.Label("Keys", _panel.transform, "<color=#7ee08a>[Y]  tak</color>          <color=#ff8a7a>[N]  nie</color>", 24, TextAnchor.MiddleCenter);
            _keys.supportRichText = true;
            _keys.rectTransform.Place(new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -52f), new Vector2(-24f, 34f));
            var track = UiKit.Image("Track", _panel.transform, new Color(1f, 1f, 1f, 0.12f));
            track.rectTransform.Place(new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(-40f, 6f));
            _bar = UiKit.Image("Bar", track.transform, new Color(0.95f, 0.85f, 0.55f, 0.95f));
            _bar.rectTransform.Place(Vector2.zero, new Vector2(1f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            _panel.gameObject.SetActive(false);

            _line = UiKit.Label("Line", canvas.transform, "", 21, TextAnchor.MiddleCenter, new Color(0.95f, 0.92f, 0.85f));
            _line.supportRichText = true;
            _line.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -118f), new Vector2(900f, 30f));
            _line.AddOutline();
            _holdBg = UiKit.Image("HoldBg", canvas.transform, new Color(0f, 0f, 0f, 0.55f));
            _holdBg.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -140f), new Vector2(160f, 6f));
            _holdBar = UiKit.Image("Hold", _holdBg.transform, new Color(0.95f, 0.85f, 0.55f, 1f));
            _holdBar.rectTransform.Place(Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
        }

        private void Update()
        {
            if (_ask != null)
            {
                float left = _askUntil - Time.unscaledTime;
                _bar.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(left / _askTotal), 1f);
                var kb = Keyboard.current;
                bool menu = LocalPlayerController.MenuOpen;
                if (_autoAt > 0f && Time.unscaledTime >= _autoAt) Close(true);
                else if (kb != null && !menu && KeyOrPad.Down(Key.Y)) Close(true);
                else if (kb != null && !menu && KeyOrPad.Down(Key.N)) Close(false);
                else if (left <= 0f || NetPlayer.Local == null) Close(false);
            }

            var me = NetPlayer.Local;
            string text = "";
            if (me != null)
            {
                var carried = me.CarriedPlayer;
                var by = me.CarriedBy;
                if (carried != null) text = carried.Life.Value == LifeState.Dead
                    ? $"Niesiesz ciało: <b>{carried.Nick}</b>   -   <color=#ffd27a>B</color> odłóż"
                    : $"Niesiesz: <b>{carried.Nick}</b>   -   <color=#ffd27a>B</color> odłóż";
                else if (by != null && me.Life.Value == LifeState.Dead) text = $"<b>{by.Nick}</b> niesie twoje ciało";
                else if (by != null) text = $"<b>{by.Nick}</b> niesie cię na rękach   -   <color=#ffd27a>Spacja</color> poproś o odłożenie";
                else if (Time.unscaledTime < _waitUntil) text = $"Czekasz, aż <b>{_waitNick}</b> się zgodzi...";
                else if (!string.IsNullOrEmpty(Hint)) text = Hint;
            }
            if (_line.text != text) _line.text = text;
            LineText = text;
            bool hold = !string.IsNullOrEmpty(Hint) && HintProgress > 0.001f && text == Hint;
            if (_holdBg.gameObject.activeSelf != hold) _holdBg.gameObject.SetActive(hold);
            if (hold) _holdBar.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(HintProgress), 1f);
        }

        private void Close(bool yes)
        {
            var a = _ask;
            _ask = null;
            _panel.gameObject.SetActive(false);
            GameAudio.Play2D(GameSfx.UiClick, 0.45f);
            Debug.Log($"[Carry] answer: {(yes ? "yes" : "no")}");
            try { a?.Invoke(yes); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }
}
