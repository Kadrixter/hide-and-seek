using HideAndSeek.Game.Net;
using HideAndSeek.Game.UI;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>Esc menu "Pad" pages (<see cref="PadSettingsUi"/>): settings + test, then "Przyciski pada" (rebinding).</summary>
    public partial class GameHud
    {
        private void ShowPadMenu()
        {
            if (_escMenu != null) Destroy(_escMenu.gameObject);
            _escMenu = MenuPanel("PadMenu", new Vector2(760, PadSettingsUi.PanelHeight));
            var panel = _escMenu.Find("Panel");
            UiKit.Label("Title", panel, "PAD", 40, TextAnchor.MiddleCenter, null, FontStyle.Bold).Height(54);
            PadSettingsUi.Build(panel, ShowPadBindMenu);
            UiKit.Button("Back", panel, "Wróć", () =>
            {
                Prefs.Save();
                Destroy(_escMenu.gameObject);
                _escMenu = null;
                ShowGameSettings();
            }, 28).Height(54);
        }

        private void ShowPadBindMenu()
        {
            if (_escMenu != null) Destroy(_escMenu.gameObject);
            _escMenu = MenuPanel("PadBindMenu", new Vector2(860, PadSettingsUi.BindPanelHeight));
            var panel = _escMenu.Find("Panel");
            UiKit.Label("Title", panel, "PRZYCISKI PADA", 40, TextAnchor.MiddleCenter, null, FontStyle.Bold).Height(54);
            PadSettingsUi.BuildBindings(panel);
            UiKit.Button("Back", panel, "Wróć", () =>
            {
                Prefs.Save();
                Destroy(_escMenu.gameObject);
                _escMenu = null;
                ShowPadMenu();
            }, 28).Height(54);
        }

        /// <summary>Tests (-testpad): opens the pad page / the bindings page of the Esc menu.</summary>
        public static void TestOpenPadPage(bool bindings)
        {
            if (_instance == null) return;
            if (bindings) _instance.ShowPadBindMenu();
            else _instance.ShowPadMenu();
        }

        /// <summary>Tests: closes every menu (like "Wróć do gry").</summary>
        public static void TestCloseMenus() => _instance?.CloseMenus();

        /// <summary>Tests: the name of the open menu page (null = none).</summary>
        public static string TestMenuName => _instance != null && _instance._escMenu != null ? _instance._escMenu.name : null;
    }
}
