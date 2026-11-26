using HideAndSeek.Audio;
using HideAndSeek.Game.Net;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using HideAndSeek.Player;

namespace HideAndSeek.Game.UI
{
    /// <summary>
    /// Yes/no prompt of <see cref="Consent"/> (generic: pair emotes, carrying...). Asked player: a panel low in the
    /// middle "&lt;nick&gt; chce się przytulić" with key caps [Y] tak / [N] nie and a draining countdown bar; Y / N answer
    /// (both keys are unused elsewhere in the game). Asking player: a small line "Czekasz na odpowiedź: &lt;nick&gt;" with
    /// the same bar; a refusal / no answer shows a toast. <see cref="AutoAccept"/> (test switch -autoconsent) says yes.
    /// </summary>
    public sealed class ConsentPrompt : MonoBehaviour
    {
        /// <summary>Tests: answer every question with yes after a short moment.</summary>
        public static bool AutoAccept;

        private static ConsentPrompt _inst;

        private RectTransform _ask, _wait;
        private CanvasGroup _askGroup, _waitGroup;
        private Text _askText, _waitText;
        private Image _askBar, _waitBar;
        private uint _askId, _waitId;
        private float _askStart, _askLen, _waitStart, _waitLen, _askAnim, _waitAnim, _answeredAt = -1f;
        private bool _askOpen, _waitOpen;

        /// <summary>A question is shown to the local player right now (tests).</summary>
        public static bool Asking => _inst != null && _inst._askOpen;
        /// <summary>The local player waits for someone's answer (tests).</summary>
        public static bool Waiting => _inst != null && _inst._waitOpen;

        public static ConsentPrompt Create(Transform canvasRoot)
        {
            var host = UiKit.Rect("ConsentPrompt", canvasRoot);
            host.Stretch();
            var c = host.gameObject.AddComponent<ConsentPrompt>();
            _inst = c;
            c.Build(host);
            return c;
        }

        private void Build(RectTransform host)
        {
            // Incoming question.
            var bg = UiKit.Image("Ask", host, new Color(0.035f, 0.04f, 0.045f, 0.82f)).Rounded();
            bg.raycastTarget = false;
            _ask = bg.rectTransform;
            _ask.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 250f), new Vector2(560f, 112f));
            UiStyle.Ring("Border", _ask, new Color(UiKit.Accent.r, UiKit.Accent.g, UiKit.Accent.b, 0.45f));
            _askGroup = bg.gameObject.AddComponent<CanvasGroup>();
            _askText = UiKit.Label("Text", _ask, "", 26, TextAnchor.MiddleCenter, UiKit.Text, FontStyle.Bold);
            _askText.rectTransform.Place(new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(-24f, 40f));
            _askText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _askText.AddOutline(1f, 0.6f);
            Key(_ask, "Y", "tak", new Vector2(-90f, 34f), UiKit.Accent);
            Key(_ask, "N", "nie", new Vector2(70f, 34f), new Color(0.95f, 0.45f, 0.4f));
            _askBar = Bar(_ask);
            _ask.gameObject.SetActive(false);

            // Own question: waiting.
            var wb = UiKit.Image("Wait", host, new Color(0.035f, 0.04f, 0.045f, 0.7f)).Rounded();
            wb.raycastTarget = false;
            _wait = wb.rectTransform;
            _wait.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 250f), new Vector2(420f, 48f));
            _waitGroup = wb.gameObject.AddComponent<CanvasGroup>();
            _waitText = UiKit.Label("Text", _wait, "", 20, TextAnchor.MiddleCenter, UiKit.Text);
            _waitText.rectTransform.Place(new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(-20f, 34f));
            _waitText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _waitText.AddOutline(1f, 0.6f);
            _waitBar = Bar(_wait);
            _wait.gameObject.SetActive(false);
        }

        private static void Key(RectTransform parent, string key, string text, Vector2 pos, Color tint)
        {
            var cap = UiKit.Image("Key" + key, parent, new Color(0.93f, 0.93f, 0.9f, 0.95f)).Rounded(true);
            cap.raycastTarget = false;
            cap.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), pos, new Vector2(34f, 32f));
            var k = UiKit.Label("K", cap.transform, key, 19, TextAnchor.MiddleCenter, new Color(0.08f, 0.08f, 0.1f), FontStyle.Bold);
            k.rectTransform.Stretch();
            var l = UiKit.Label("T", parent, text, 22, TextAnchor.MiddleLeft, tint, FontStyle.Bold);
            l.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0.5f), pos + new Vector2(24f, 0f), new Vector2(80f, 32f));
            l.AddOutline(1f, 0.6f);
        }

        private static Image Bar(RectTransform parent)
        {
            var track = UiKit.Image("Track", parent, new Color(1f, 1f, 1f, 0.08f));
            track.raycastTarget = false;
            track.rectTransform.Place(new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(-28f, 4f));
            var fill = UiKit.Image("Fill", track.transform, UiKit.Accent);
            fill.raycastTarget = false;
            fill.rectTransform.Place(new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            return fill;
        }

        // ================================================================ API (from NetPlayer RPCs)

        public static void ShowAsk(uint id, string nick, string text, float seconds, ConsentKind kind, byte arg)
        {
            if (_inst == null) return;
            _inst._askId = id;
            _inst._askOpen = true;
            _inst._askStart = Time.unscaledTime;
            _inst._askLen = Mathf.Max(1f, seconds);
            _inst._answeredAt = -1f;
            _inst._askText.text = $"{nick} {text}";
            _inst._ask.gameObject.SetActive(true);
            GameAudio.Play2D(GameSfx.UiHover, 0.8f);
            Debug.Log($"[Consent] asked #{id}: {nick} {text} ({kind}/{arg})");
        }

        public static void CloseAsk(uint id, ConsentAnswer answer)
        {
            if (_inst == null || _inst._askId != id) return;
            _inst._askOpen = false;
        }

        public static void ShowWait(uint id, string nick, float seconds)
        {
            if (_inst == null) return;
            _inst._waitId = id;
            _inst._waitOpen = true;
            _inst._waitStart = Time.unscaledTime;
            _inst._waitLen = Mathf.Max(1f, seconds);
            _inst._waitText.text = $"Czekasz na odpowiedź: {nick}";
            _inst._wait.gameObject.SetActive(true);
        }

        public static void CloseWait(uint id, ConsentAnswer answer, string nick)
        {
            if (_inst == null || _inst._waitId != id) return;
            _inst._waitOpen = false;
            if (answer == ConsentAnswer.No) { GameHud.Toast($"{nick} nie chce :/", 2.5f); GameAudio.Play2D(GameSfx.UiError, 0.5f); }
            else if (answer == ConsentAnswer.Timeout) GameHud.Toast($"{nick} nie odpowiada.", 2.5f);
        }

        // ================================================================ frame

        private void Update()
        {
            var me = NetPlayer.Local;
            if (me == null) { _askOpen = _waitOpen = false; }
            float now = Time.unscaledTime;
            if (_askOpen && me != null)
            {
                var kb = Keyboard.current;
                bool free = !LocalPlayerController.MenuOpen;
                bool yes = free && kb != null && KeyOrPad.Down(UnityEngine.InputSystem.Key.Y);
                bool no = free && kb != null && KeyOrPad.Down(UnityEngine.InputSystem.Key.N);
                if (AutoAccept && now - _askStart > 1.2f) yes = true;
                if (yes || no)
                {
                    me.AnswerConsent(_askId, yes);
                    _askOpen = false;
                    _answeredAt = now;
                    GameAudio.Play2D(yes ? GameSfx.UiClick : GameSfx.UiHover, 0.7f);
                    Debug.Log($"[Consent] answered #{_askId}: {(yes ? "tak" : "nie")}");
                }
                _askBar.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(1f - (now - _askStart) / _askLen), 1f);
            }
            if (_waitOpen) _waitBar.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(1f - (now - _waitStart) / _waitLen), 1f);

            _askAnim = Mathf.MoveTowards(_askAnim, _askOpen ? 1f : 0f, Time.unscaledDeltaTime / (_askOpen ? 0.18f : 0.25f));
            _waitAnim = Mathf.MoveTowards(_waitAnim, _waitOpen && !_askOpen ? 1f : 0f, Time.unscaledDeltaTime / 0.2f);
            Animate(_ask, _askGroup, _askAnim, _askOpen);
            Animate(_wait, _waitGroup, _waitAnim, _waitOpen);
        }

        private static void Animate(RectTransform rt, CanvasGroup g, float a, bool open)
        {
            if (a <= 0f) { if (rt.gameObject.activeSelf) rt.gameObject.SetActive(false); return; }
            if (!rt.gameObject.activeSelf) rt.gameObject.SetActive(true);
            float e = a * a * (3f - 2f * a);
            g.alpha = e;
            rt.localScale = Vector3.one * Mathf.Lerp(open ? 0.9f : 0.96f, 1f, e);
        }
    }
}
