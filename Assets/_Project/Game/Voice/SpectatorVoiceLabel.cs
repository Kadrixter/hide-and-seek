using HideAndSeek.Game.Net;
using HideAndSeek.Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HideAndSeek.Game.Voice
{
    /// <summary>
    /// Small HUD line for the dead during a round: their voice goes only to the observer channel (other dead players),
    /// the living cannot hear them. Sits just above the microphone meter, bottom left. Own canvas (the main HUD stays untouched).
    /// </summary>
    public class SpectatorVoiceLabel : MonoBehaviour
    {
        private Text _label;

        /// <summary>Text currently shown (empty when hidden) - for tests.</summary>
        public static string Current { get; private set; } = "";

        private void Update()
        {
            var me = NetPlayer.Local;
            bool show = me != null && VoiceRules.OnDeadChannel(me);
            if (show && _label == null) Build();
            if (_label == null) return;
            string text = "";
            if (show)
            {
                var vc = VoiceChat.Instance;
                bool talking = vc != null && vc.Transmitting;
                text = talking
                    ? "<color=#c9a8ff>Mówisz na kanale obserwatorów</color> - słyszą cię tylko martwi"
                    : "<color=#b49ae0>Kanał obserwatorów</color> - słyszą cię tylko martwi, ty słyszysz wszystkich";
            }
            if (_label.text != text) _label.text = text;
            Current = text;
        }

        private void Build()
        {
            var canvas = UiKit.CreateCanvas("SpectatorVoiceCanvas", 11, transform);
            _label = UiKit.Label("ObserverChannel", canvas.transform, "", 21, TextAnchor.LowerLeft, new Color(0.82f, 0.82f, 0.86f));
            _label.rectTransform.Place(Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(24, 366), new Vector2(900, 30));
            _label.supportRichText = true;
            _label.raycastTarget = false;
            _label.AddOutline();
        }
    }
}
