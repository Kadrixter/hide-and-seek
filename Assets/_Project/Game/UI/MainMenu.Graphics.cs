using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game.UI
{
    /// <summary>Main menu "Grafika" page (<see cref="GraphicsSettingsUi"/>).</summary>
    public partial class MainMenu
    {
        private RectTransform BuildGraphics(Transform parent)
        {
            var page = Page(parent, "Graphics");
            page.offsetMax = new Vector2(0, 40);
            UiKit.Label("T", page, "Grafika", 26).Height(50);
            GraphicsSettingsUi.Build(page);
            UiKit.Button("Back", page, "Wróć", () => { Prefs.Save(); Show(_options); }, 34).Height(56);
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-testgfxmenu");
            if (i >= 0 && i + 1 < args.Length) StartCoroutine(GfxMenuShot(page, args[i + 1]));
            return page;
        }

        /// <summary>Test switch -testgfxmenu &lt;dir&gt;: opens the "Grafika" page and takes a screenshot.</summary>
        private System.Collections.IEnumerator GfxMenuShot(RectTransform page, string dir)
        {
            yield return new WaitForSeconds(3f);
            Show(page);
            yield return new WaitForSeconds(1.5f);
            System.IO.Directory.CreateDirectory(dir);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "menu_grafika.png"));
            Debug.Log("[GfxMenu] screenshot taken");
        }
    }
}
