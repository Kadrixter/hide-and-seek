using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>Board on the lobby wall: E opens the round settings (host edits, others view).</summary>
    public class LobbySettingsBoard : MonoBehaviour
    {
        public static LobbySettingsBoard Instance { get; private set; }
        [SerializeField] private TextMesh text;

        private void OnEnable() => Instance = this;
        private void OnDisable() { if (Instance == this) Instance = null; }

        private void Update()
        {
            if (text == null) return;
            var mm = MatchManager.Instance;
            if (mm == null) return;
            var s = mm.Settings.Value;
            string hint = (s.HintMode & 3) == 3 ? "dźwięk + mapa" : (s.HintMode & 1) != 0 ? "dźwięk" : (s.HintMode & 2) != 0 ? "mapa" : "brak";
            text.text = "USTAWIENIA RUNDY\n\n" +
                        $"Chowanie:   {UI.UiKit.FormatTime(s.HideTime)}\n" +
                        $"Szukanie:   {UI.UiKit.FormatTime(s.SeekTime)}\n" +
                        $"Podpowiedzi od {UI.UiKit.FormatTime(s.HintStart)}, co {UI.UiKit.FormatTime(s.HintInterval)}\n" +
                        $"Tryb: {hint}\n" +
                        $"Glock na koniec: {(s.Glock ? "tak" : "nie")}\n" +
                        $"Apteczki: {s.Medkits}\n" +
                        $"Wykrwawianie: {(s.BleedOut > 1f ? s.BleedOut : 78f):0} s\n\n" +
                        $"Graczy: {NetPlayer.All.Count}   Gotowi: {ReadyText()}\n[E]";
        }

        private static string ReadyText()
        {
            int r = 0, n = 0;
            foreach (var p in NetPlayer.All)
            {
                if (p == null || p.OwnerClientId == Unity.Netcode.NetworkManager.ServerClientId) continue;
                n++;
                if (p.Ready.Value) r++;
            }
            return $"{r}/{n}";
        }
    }
}
