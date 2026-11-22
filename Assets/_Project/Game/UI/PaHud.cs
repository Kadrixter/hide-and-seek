using HideAndSeek.Audio;
using HideAndSeek.Game.Net;
using HideAndSeek.Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Public-address HUD (own canvas): while the local player holds the wall microphone, a line above the microphone
    /// meter (and the toy phone line) says what the keys do / that he is on air; the talk bar's own click (2D).
    /// </summary>
    public class PaHud : MonoBehaviour
    {
        public static PaHud Instance { get; private set; }
        /// <summary>Current line (tests).</summary>
        public static string LineText { get; private set; } = "";

        private Text _line;
        private bool _wasTalking;

        public static void Ensure()
        {
            if (Instance != null) return;
            var go = new GameObject("[PaHud]");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<PaHud>();
        }

        private void Awake()
        {
            var canvas = UiKit.CreateCanvas("PaHudCanvas", 9, transform);
            _line = UiKit.Label("PaLine", canvas.transform, "", 21, TextAnchor.LowerLeft, new Color(0.95f, 0.9f, 0.78f));
            _line.rectTransform.Place(Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(24, 396), new Vector2(900, 30));
            _line.supportRichText = true;
            _line.AddOutline();
        }

        private void Update()
        {
            var me = NetPlayer.Local;
            bool holding = me != null && me.HoldsPaMic;
            bool talking = holding && me.PaTransmitting;
            string text = "";
            if (holding)
            {
                bool mic = LocalProfile.VoiceEnabled && Microphone.devices.Length > 0;
                text = !mic ? "<color=#ffd27a>Nagłośnienie</color> - mikrofon wyłączony (Esc > Czat głosowy), E odwiesza"
                    : talking ? "<color=#ff6b5b>NADAJESZ</color> - słychać cię w głośnikach na wszystkich poziomach"
                    : "<color=#ffd27a>Nagłośnienie</color> - przytrzymaj LPM i mów, E odwiesza mikrofon";
            }
            if (_line.text != text) _line.text = text;
            LineText = text;
            if (talking != _wasTalking) GameAudio.PlayClip2D(talking ? PaSounds.ClickDown : PaSounds.ClickUp, 0.45f, GameSfx.UiClick);
            _wasTalking = talking;
        }
    }
}
