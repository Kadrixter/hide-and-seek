using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game.UI
{
    /// <summary>Pad: L1 / R1 flip through the main menu settings pages (Ustawienia, Dźwięk, Czat głosowy, Grafika).</summary>
    public partial class MainMenu
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RegisterPadTabs() => PadUi.TabHandlers.Add(dir => _instance != null && _instance.PadTab(dir));

        /// <summary>Tests: the page shown now ("Options", "Audio", "Voice", "Graphics", "Main"...).</summary>
        public static string CurrentPage
        {
            get
            {
                if (_instance == null) return null;
                foreach (var p in new[] { _instance._main, _instance._character, _instance._browser, _instance._options, _instance._audio, _instance._voicePage, _instance._graphics, _instance._padPage, _instance._padBindPage })
                    if (p != null && p.gameObject.activeSelf) return p.name;
                return null;
            }
        }

        private bool PadTab(int dir)
        {
            var pages = new[] { _options, _audio, _voicePage, _graphics, _padPage }; // _padPage: MainMenu.PadSettings.cs
            int cur = -1;
            for (int i = 0; i < pages.Length; i++) if (pages[i] != null && pages[i].gameObject.activeSelf) cur = i;
            int next = PadUi.NextPage(cur, pages.Length, dir);
            if (next < 0) return false;
            Prefs.Save();
            Show(pages[next]);
            Debug.Log($"[PadUi] settings page -> {pages[next].name}");
            return true;
        }
    }
}
