using System.Collections.Generic;
using HideAndSeek.Game.Net;
using HideAndSeek.Game.Voice;
using UnityEngine;

namespace HideAndSeek.Game.UI
{
    /// <summary>
    /// Esc menu page "Głośność graczy": one slider (0-200 %) per other player in the session, for their voice chat.
    /// Remembered by nick in the player's settings file (<see cref="PlayerVoiceVolumes"/>). Works in the lobby and in a round.
    /// </summary>
    public static class PlayerVolumeMenu
    {
        public const int MaxRows = 9;

        /// <summary>Other players currently in the session (sorted by nick).</summary>
        public static List<NetPlayer> Others()
        {
            var list = new List<NetPlayer>();
            foreach (var p in NetPlayer.All)
                if (p != null && !p.IsOwner) list.Add(p);
            list.Sort((a, b) => string.Compare(a.Nick, b.Nick, System.StringComparison.OrdinalIgnoreCase));
            if (list.Count > MaxRows) list.RemoveRange(MaxRows, list.Count - MaxRows);
            return list;
        }

        /// <summary>Panel height that fits the current list.</summary>
        public static float PanelHeight() => 300f + Mathf.Max(1, Others().Count) * 78f;

        /// <summary>Fills the (vertical stack) panel; the caller adds the "Wróć" button.</summary>
        public static void Build(Transform panel)
        {
            UiKit.Label("Title", panel, "GŁOŚNOŚĆ GRACZY", 40, TextAnchor.MiddleCenter, null, FontStyle.Bold).Height(54);
            UiKit.Label("Info", panel, "Czat głosowy każdego gracza osobno, 0-200 % (zapamiętane).",
                20, TextAnchor.MiddleCenter, UiKit.TextDim).Height(30);
            var others = Others();
            if (others.Count == 0)
            {
                UiKit.Label("None", panel, "Nie ma jeszcze innych graczy.", 24, TextAnchor.MiddleCenter, UiKit.TextDim).Height(68);
            }
            var sliders = new List<(UnityEngine.UI.Slider slider, string nick)>();
            foreach (var p in others)
            {
                string nick = p.Nick;
                var s = UiStyle.SliderRow(panel, nick, 0f, PlayerVoiceVolumes.Max, PlayerVoiceVolumes.Get(nick),
                    v => PlayerVoiceVolumes.Set(nick, v), v => $"{v * 100f:0}%");
                sliders.Add((s, nick));
            }
            if (others.Count > 0)
            {
                UiKit.Button("ResetAll", panel, "Wszyscy 100%", () =>
                {
                    foreach (var (slider, nick) in sliders)
                    {
                        PlayerVoiceVolumes.Set(nick, 1f);
                        slider.value = 1f; // also refreshes the % label
                    }
                }, 24).Height(50);
            }
        }
    }
}
