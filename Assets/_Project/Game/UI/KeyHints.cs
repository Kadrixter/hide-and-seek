using System.Collections.Generic;
using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using UnityEngine;
using UnityEngine.UI;

namespace HideAndSeek.Game.UI
{
    /// <summary>
    /// Fixed key hints ("T - latarka", "X - kurz", "M - skala minimapy", "Esc - menu"...) are not on screen all the time:
    /// each set shows up for <see cref="ShowFor"/> s when its situation starts (entering the lobby, start of hiding, start of
    /// seeking, first time the glock is in hand, start of spectating) and then fades out. Each set once per round.
    /// Contextual hints at objects (the world "E ..." prompt), the hotbar hint and warnings stay where they are.
    /// Panel on the left edge, above the minimap / voice line.
    /// </summary>
    public sealed class KeyHints : MonoBehaviour
    {
        public const float ShowFor = 15f;
        private const float FadeIn = 0.35f, FadeOut = 2f;

        private RectTransform _panel;
        private CanvasGroup _group;
        private readonly List<(Image cap, Text key, Text text)> _rows = new();
        private readonly HashSet<string> _shown = new();
        private string _current;
        private float _since = -100f;
        private MatchPhase _lastPhase = (MatchPhase)255;

        /// <summary>Seconds left of the current set (tests).</summary>
        public static float Remaining { get; private set; }

        public static KeyHints Create(Transform canvasRoot)
        {
            var bg = UiKit.Image("KeyHints", canvasRoot, new Color(0.035f, 0.04f, 0.045f, 0.55f)).Rounded();
            bg.raycastTarget = false;
            var rt = bg.rectTransform;
            rt.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0), new Vector2(22, 10), new Vector2(420, 60));
            UiStyle.Ring("Border", rt, UiStyle.Border);
            var h = bg.gameObject.AddComponent<KeyHints>();
            h._panel = rt;
            h._group = bg.gameObject.AddComponent<CanvasGroup>();
            h._group.alpha = 0f;
            h._group.blocksRaycasts = false;
            return h;
        }

        private void LateUpdate()
        {
            var me = NetPlayer.Local;
            var mm = MatchManager.Instance;
            if (me == null || mm == null) { _group.alpha = 0f; return; }
            var phase = mm.Phase.Value;
            if (phase != _lastPhase)
            {
                if (phase == MatchPhase.Lobby || phase == MatchPhase.Hiding) _shown.Clear(); // new round
                _lastPhase = phase;
            }

            string want = Situation(me, phase);
            if (want != null && want != _current && !_shown.Contains(want))
            {
                _shown.Add(want);
                _current = want;
                _since = Time.unscaledTime;
                Fill(want, me);
            }

            float age = Time.unscaledTime - _since;
            Remaining = Mathf.Max(0f, ShowFor - age);
            bool stillValid = _current != null && (want == _current || age < 4f); // situation over (e.g. died): short linger
            float a = Mathf.Clamp01(age / FadeIn) * Mathf.Clamp01((ShowFor - age) / FadeOut);
            if (!stillValid) a = Mathf.Min(a, _group.alpha - Time.unscaledDeltaTime * 2f);
            _group.alpha = Mathf.Clamp01(a);
            if (_group.alpha <= 0f && age > ShowFor) _current = null;
        }

        /// <summary>Which hint set fits now (null = none).</summary>
        private static string Situation(NetPlayer me, MatchPhase phase)
        {
            if (me.HoldsPaMic) return "pa";
            switch (phase)
            {
                case MatchPhase.Lobby:
                    return "lobby";
                case MatchPhase.Hiding:
                    return me.IsHider && me.Life.Value == LifeState.Alive ? "hide" : null;
                case MatchPhase.Seeking:
                    if (me.Life.Value == LifeState.Dead) return "spectate";
                    if (me.IsSeeker) return me.Held.Value == HeldItem.Glock ? "glock" : "seek";
                    return me.Life.Value == LifeState.Alive ? "dust" : null;
                case MatchPhase.Ended:
                    return me.Life.Value == LifeState.Dead ? "spectate" : null;
                default:
                    return null;
            }
        }

        private static readonly List<(string key, string text)> _tmp = new();

        private void Fill(string set, NetPlayer me)
        {
            _tmp.Clear();
            bool ptt = LocalProfile.VoiceEnabled && !LocalProfile.VoiceOpenMic;
            switch (set)
            {
                case "lobby":
                    _tmp.Add(("E", "usiądź na kanapie / pufie"));
                    _tmp.Add(("E", "tablica na ścianie - ustawienia rundy"));
                    if (ptt) _tmp.Add(("V", "mów (przytrzymaj)"));
                    _tmp.Add(("Esc", "menu i ustawienia"));
                    break;
                case "hide":
                    _tmp.Add((LocalProfile.CrouchToggle ? "Ctrl / C" : "Ctrl", "kucanie"));
                    _tmp.Add(("Z", "czołganie"));
                    _tmp.Add(("T", "latarka"));
                    _tmp.Add(("M", "skala minimapy"));
                    if (ptt) _tmp.Add(("V", "mów - szukający też usłyszy"));
                    break;
                case "dust":
                    _tmp.Add(("X", "rzuć kurz w oczy szukającemu (z bliska)"));
                    _tmp.Add(("H", "przytrzymaj przy rannym - pomóż"));
                    break;
                case "seek":
                    _tmp.Add(("T", "latarka"));
                    _tmp.Add(("Kółko", "skupienie światła"));
                    _tmp.Add(("LPM / F", "atak wybraną bronią"));
                    _tmp.Add(("M", "skala minimapy"));
                    break;
                case "glock":
                    _tmp.Add(("PPM", "celowanie (mniejszy rozrzut)"));
                    _tmp.Add(("R", "przeładuj"));
                    break;
                case "spectate":
                    _tmp.Add(("LPM / PPM", "zmiana obserwowanego gracza"));
                    break;
                case "pa":
                    _tmp.Add(("LPM", "przytrzymaj - mów przez głośniki na całym parkingu"));
                    _tmp.Add(("E", "odwieś mikrofon"));
                    break;
            }

            const float rowH = 34f, pad = 10f;
            while (_rows.Count < _tmp.Count) _rows.Add(MakeRow(_panel));
            float maxW = 0f;
            for (int i = 0; i < _rows.Count; i++)
            {
                var (cap, key, text) = _rows[i];
                bool on = i < _tmp.Count;
                cap.gameObject.SetActive(on);
                text.gameObject.SetActive(on);
                if (!on) continue;
                key.text = _tmp[i].key;
                text.text = _tmp[i].text;
                float capW = Mathf.Max(30f, key.preferredWidth + 16f);
                float y = -pad - i * rowH;
                cap.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(pad, y - 2f), new Vector2(capW, 28f));
                text.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(pad + capW + 10f, y), new Vector2(380f, 32f));
                maxW = Mathf.Max(maxW, pad + capW + 10f + text.preferredWidth + pad + 4f);
            }
            _panel.sizeDelta = new Vector2(Mathf.Max(200f, maxW), pad * 2f + _tmp.Count * rowH - 4f);
        }

        private static (Image, Text, Text) MakeRow(RectTransform parent)
        {
            var cap = UiKit.Image("Key", parent, new Color(0.93f, 0.93f, 0.9f, 0.95f)).Rounded(true);
            cap.raycastTarget = false;
            var key = UiKit.Label("K", cap.transform, "", 17, TextAnchor.MiddleCenter, new Color(0.08f, 0.08f, 0.1f), FontStyle.Bold);
            key.rectTransform.Stretch();
            key.horizontalOverflow = HorizontalWrapMode.Overflow;
            var text = UiKit.Label("Text", parent, "", 20, TextAnchor.MiddleLeft, UiKit.Text);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.AddOutline(1f, 0.6f);
            return (cap, key, text);
        }
    }
}
