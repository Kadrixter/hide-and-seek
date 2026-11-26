using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game.UI
{
    /// <summary>Main menu "Pad" pages (<see cref="PadSettingsUi"/>): settings + test, then "Przyciski pada".</summary>
    public partial class MainMenu
    {
        private void BuildPadPages(Transform parent)
        {
            _padPage = Page(parent, "Pad");
            _padPage.offsetMax = new Vector2(0, 60); // long page: starts higher, footer hidden
            UiKit.Label("T", _padPage, "Pad", 26).Height(44);
            PadSettingsUi.Build(_padPage, () => Show(_padBindPage));
            UiKit.Button("Back", _padPage, "Wróć", () => { Prefs.Save(); Show(_options); }, 34).Height(54);

            _padBindPage = Page(parent, "PadBindings");
            _padBindPage.offsetMax = new Vector2(0, 60);
            UiKit.Label("T", _padBindPage, "Przyciski pada", 26).Height(44);
            PadSettingsUi.BuildBindings(_padBindPage);
            UiKit.Button("Back", _padBindPage, "Wróć", () => { Prefs.Save(); Show(_padPage); }, 34).Height(54);
            _padPage.gameObject.SetActive(false);
            _padBindPage.gameObject.SetActive(false);
        }
    }
}
