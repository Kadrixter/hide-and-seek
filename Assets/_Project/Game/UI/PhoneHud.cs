using System;
using HideAndSeek.Audio;
using HideAndSeek.Game.Net;
using HideAndSeek.Game.UI;
using HideAndSeek.Game.Voice;
using UnityEngine;
using UnityEngine.UI;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Small "E" key cap drawn above an inventory slot while the phone in the pocket rings (E = throw it down and
    /// stomp on it). Attach it to any slot rect; it pulses and hides itself.
    /// </summary>
    public class PhoneRingBadge : MonoBehaviour
    {
        private Func<bool> _show;
        private RectTransform _rt;
        private CanvasGroup _group;
        private float _a;

        /// <summary>
        /// Creates the badge over the top edge of <paramref name="slot"/>. <paramref name="show"/> = when to show it
        /// (default: the local player's phone rings and E would smash it, <see cref="PhoneControls.SmashOffered"/>).
        /// </summary>
        public static PhoneRingBadge Attach(RectTransform slot, Func<bool> show = null)
        {
            var rt = UiKit.Rect("PhoneRingE", slot);
            rt.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(30f, 30f));
            var bg = rt.gameObject.AddComponent<Image>();
            bg.sprite = UiStyle.RoundSmall;
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.97f, 0.97f, 0.95f, 1f);
            bg.raycastTarget = false;
            var ring = UiStyle.Ring("Ring", rt, new Color(1f, 0.42f, 0.72f, 1f), true);
            ring.raycastTarget = false;
            var key = UiKit.Label("Key", rt, "E", 20, TextAnchor.MiddleCenter, new Color(0.1f, 0.1f, 0.12f), FontStyle.Bold);
            key.rectTransform.Stretch();
            var b = rt.gameObject.AddComponent<PhoneRingBadge>();
            b._rt = rt;
            b._show = show ?? (() => PhoneControls.SmashOffered(NetPlayer.Local));
            b._group = rt.gameObject.AddComponent<CanvasGroup>();
            b._group.alpha = 0f;
            return b;
        }

        /// <summary>True while the badge is shown (tests).</summary>
        public bool Shown => _a > 0.5f;

        private void Update()
        {
            bool on = _show != null && _show();
            _a = Mathf.MoveTowards(_a, on ? 1f : 0f, Time.unscaledDeltaTime * 8f);
            _group.alpha = _a;
            float pulse = on ? 1f + 0.12f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 7f)) : 1f;
            _rt.localScale = Vector3.one * pulse * Mathf.Lerp(0.6f, 1f, _a);
        }
    }

    /// <summary>
    /// Toy phone HUD (own canvas): a line above the microphone meter "Telefon: łączy z X osobami" (only with a phone,
    /// alive), the talker's own squelch click when talking through it, and - until the phone sits in a real inventory
    /// slot - a small stand-alone phone slot right of the hotbar with the ringing "E" badge (<see cref="StandaloneSlot"/>).
    /// </summary>
    public class PhoneHud : MonoBehaviour
    {
        /// <summary>Show the stand-alone phone slot (turn off once the inventory shows the phone itself).</summary>
        public static bool StandaloneSlot = false; // the phone has a real hotbar slot now (badge: HotbarHud)

        public static PhoneHud Instance { get; private set; }
        /// <summary>Current link line (tests).</summary>
        public static string LinkText { get; private set; } = "";

        private Text _link;
        private RectTransform _slot;
        private Image _icon;
        private CanvasGroup _slotGroup;
        private PhoneRingBadge _badge;
        private bool _wasTalking;
        private static AudioClip _click;

        public PhoneRingBadge Badge => _badge;

        public static void Ensure()
        {
            if (Instance != null) return;
            var go = new GameObject("[PhoneHud]");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<PhoneHud>();
        }

        private void Awake()
        {
            var canvas = UiKit.CreateCanvas("PhoneHudCanvas", 9, transform);
            _link = UiKit.Label("PhoneLink", canvas.transform, "", 21, TextAnchor.LowerLeft, new Color(0.95f, 0.75f, 0.88f));
            _link.rectTransform.Place(Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(24, 366), new Vector2(900, 30));
            _link.supportRichText = true;
            _link.AddOutline();

            var bg = UiKit.Image("PhoneSlot", canvas.transform, new Color(0.09f, 0.1f, 0.115f, 0.82f));
            bg.sprite = UiStyle.RoundGradient;
            bg.type = Image.Type.Sliced;
            bg.raycastTarget = false;
            _slot = bg.rectTransform;
            _slot.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(270f, 22f), new Vector2(64f, 64f));
            UiStyle.Ring("Ring", _slot, new Color(1f, 0.42f, 0.72f, 0.5f));
            _icon = UiKit.Image("Icon", _slot, Color.white);
            _icon.raycastTarget = false;
            _icon.preserveAspect = true;
            _icon.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(44f, 44f));
            _slotGroup = _slot.gameObject.AddComponent<CanvasGroup>();
            _slotGroup.alpha = 0f;
            _badge = PhoneRingBadge.Attach(_slot);
        }

        private void Update()
        {
            var me = NetPlayer.Local;
            bool inRound = me != null && Hotbar.Active(me) && me.IsHider;
            bool has = inRound && me.HasPhone;

            _icon.sprite = HotbarIcons.Phone();
            _slotGroup.alpha = Mathf.MoveTowards(_slotGroup.alpha, StandaloneSlot && has ? 1f : 0f, Time.unscaledDeltaTime * 6f);
            if (has && me.PhoneRinging)
                _icon.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Repeat(Time.unscaledTime, 0.75f) < 0.45f ? 9f * Mathf.Sin(Time.unscaledTime * 60f) : 0f);
            else _icon.rectTransform.localRotation = Quaternion.identity;

            string text = "";
            bool talking = false;
            if (has && me.Life.Value != HideAndSeek.Core.LifeState.Dead)
            {
                int n = VoiceRules.LocalPhoneLinks();
                var vc = VoiceChat.Instance;
                talking = n > 0 && vc != null && vc.Transmitting;
                string who = n == 1 ? "1 osobą" : n + " osobami";
                text = n == 0 ? "<color=#ff8fc8>Telefon</color> - brak połączenia (nikt inny nie ma telefonu)"
                    : talking ? $"<color=#ff8fc8>Mówisz przez telefon</color> - słyszy cię {(n == 1 ? "1 osoba" : n + " osoby")}"
                    : $"<color=#ff8fc8>Telefon</color> łączy z {who} - V mówi przez niego";
            }
            if (_link.text != text) _link.text = text;
            LinkText = text;
            if (talking != _wasTalking) GameAudio.PlayClip2D(Click, 0.3f, GameSfx.UiClick);
            _wasTalking = talking;
        }

        /// <summary>Short squelch "pyk" for the own push-to-talk.</summary>
        private static AudioClip Click
        {
            get
            {
                if (_click != null) return _click;
                int rate = 22050, n = rate / 14;
                var d = new float[n];
                var rng = new System.Random(7);
                for (int i = 0; i < n; i++)
                {
                    float env = Mathf.Exp(-i / (rate * 0.012f));
                    d[i] = (i < 40 ? (i % 2 == 0 ? 0.8f : -0.5f) : 0f) + (float)(rng.NextDouble() * 2 - 1) * 0.35f * env;
                }
                _click = AudioClip.Create("phone-click", n, 1, rate, false);
                _click.SetData(d, 0);
                return _click;
            }
        }
    }
}
