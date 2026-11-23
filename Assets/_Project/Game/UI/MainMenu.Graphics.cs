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
