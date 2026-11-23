using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game.UI
{
    /// <summary>Main menu "Grafika" page (<see cref="GraphicsSettingsUi"/>).</summary>
    public partial class MainMenu
    {
        private RectTransform BuildGraphics(Transform parent)
        {
            var page = UiKit.Rect("Graphics", parent);
            page.Place(new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(-120, -200));
            UiKit.VStack(page, 5);
            UiKit.Label("T", page, "GRAFIKA", 34, TextAnchor.MiddleLeft, null, FontStyle.Bold).Height(42);
            GraphicsSettingsUi.Build(page);
            UiKit.Button("Back", page, "Wróć", () => { Prefs.Save(); Show(_options); }, 26).Height(52);
            return page;
        }
    }
}
