using HideAndSeek.Game.Net;
using HideAndSeek.Game.UI;
using HideAndSeek.Game.Voice;
using UnityEngine;
using UnityEngine.UI;

namespace HideAndSeek.Game
{
    /// <summary>Bottom line of the lobby board: the voice chat mode and delay chosen by the host (<see cref="VoiceMode"/>).</summary>
    public partial class LobbySettingsBoard
    {
        private Text _voiceLine;

        private void RefreshVoice(NetSettings s)
        {
            if (_voiceLine == null)
                _voiceLine = Lbl((RectTransform)_canvas.transform, "", 520, 996, 600, 46, 25, UiKit.TextDim, FontStyle.Bold, TextAnchor.MiddleCenter);
            byte mode = s.VoiceMode == VoiceMode.LowLatency ? VoiceMode.LowLatency : VoiceMode.Classic;
            _voiceLine.text = "GŁOS: " + VoiceMode.Describe(mode, s.VoiceDelayMs).ToUpperInvariant();
            _voiceLine.color = mode == VoiceMode.LowLatency ? UiKit.ButtonHover : UiKit.TextDim;
        }
    }
}
