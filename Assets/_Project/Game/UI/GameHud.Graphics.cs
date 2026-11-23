using HideAndSeek.Game.Net;
using HideAndSeek.Game.UI;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>Esc menu "Grafika" page (<see cref="GraphicsSettingsUi"/>).</summary>
    public partial class GameHud
    {
        private void ShowGraphicsMenu()
        {
            if (_escMenu != null) Destroy(_escMenu.gameObject);
            _escMenu = MenuPanel("GraphicsMenu", new Vector2(760, GraphicsSettingsUi.PanelHeight));
            var panel = _escMenu.Find("Panel");
            UiKit.Label("Title", panel, "GRAFIKA", 40, TextAnchor.MiddleCenter, null, FontStyle.Bold).Height(54);
            GraphicsSettingsUi.Build(panel);
            UiKit.Button("Back", panel, "Wróć", () =>
            {
                Prefs.Save();
                Destroy(_escMenu.gameObject);
                _escMenu = null;
                ShowGameSettings();
            }, 28).Height(58);
        }
    }
}
