using HideAndSeek.Game.Net;
using HideAndSeek.Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Last tile of the lobby board: how many killers (seekers) the round gets, with the limit for the players in the
    /// room now (players - 1, at least one has to hide); red when the setting is too high (the lever refuses to start).
    /// In the RC race mode the same tile shows the laps.
    /// </summary>
    public partial class LobbySettingsBoard
    {
        private Text _lastTitle;

        private void RefreshLastTile(NetSettings s, int players)
        {
            if (s.Mode == (byte)GameMode.RcRace)
            {
                if (_lastTitle != null) _lastTitle.text = "TOR RC";
                Set(7, $"{(s.Laps == 0 ? 3 : s.Laps)} okr.", "tor RC: rampy, tunele", 1f);
                _value[7].color = UiKit.Text;
                if (_bar[7] != null) _bar[7].color = UiKit.Accent;
                return;
            }
            int want = s.SeekerCount, max = MatchManager.MaxSeekersFor(players);
            bool tooMany = want > max;
            if (_lastTitle != null) _lastTitle.text = "ZABÓJCY";
            Set(7, want.ToString(), tooMany ? $"za dużo! max {max} przy {players} graczach" : $"max {max} przy {players} graczach",
                Mathf.Clamp01(want / (float)Mathf.Max(1, max)));
            _value[7].color = tooMany ? MedRed : UiKit.SeekerColor;
            _sub[7].color = tooMany ? MedRed : UiKit.TextDim;
            if (_bar[7] != null) _bar[7].color = tooMany ? MedRed : UiKit.SeekerColor;
        }
    }
}
