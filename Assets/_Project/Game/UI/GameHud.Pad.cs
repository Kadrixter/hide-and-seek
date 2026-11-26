using HideAndSeek.Game.Net;
using HideAndSeek.Game.UI;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>Pad: L1 / R1 flip through the Esc menu settings pages (Ustawienia, Dźwięk, Czat głosowy, Grafika).</summary>
    public partial class GameHud
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RegisterPadTabs() => PadUi.TabHandlers.Add(dir => _instance != null && _instance.PadTab(dir));

        private bool PadTab(int dir)
        {
            if (_escMenu == null) return false;
            string[] names = { "SettingsMenu", "AudioMenu", "VoiceMenu", "GraphicsMenu", "PadMenu" };
            System.Action[] show = { ShowGameSettings, ShowAudioMenu, ShowVoiceMenu, ShowGraphicsMenu, ShowPadMenu }; // GameHud.PadSettings.cs
            int next = PadUi.NextPage(System.Array.IndexOf(names, _escMenu.name), names.Length, dir);
            if (next < 0) return false;
            Prefs.Save();
            show[next]();
            Debug.Log($"[PadUi] settings page -> {names[next]}");
            return true;
        }
    }
}
