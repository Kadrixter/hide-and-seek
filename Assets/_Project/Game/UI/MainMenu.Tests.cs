using System.Collections;
using System.IO;
using UnityEngine;

namespace HideAndSeek.Game.UI
{
    /// <summary>
    /// Test switch -menushots &lt;dir&gt;: screenshots of the main menu in the new style - pages, hovered buttons,
    /// the weapon picker (open, every weapon in the character's hands), the quit dialog; then quits.
    /// </summary>
    public partial class MainMenu
    {
        private IEnumerator MenuShots()
        {
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-menushots");
            string dir = i >= 0 && i + 1 < args.Length ? args[i + 1] : "menushots";
            Directory.CreateDirectory(dir);
            var picker = FindAnyObjectByType<MenuWeaponPicker>();

            IEnumerator Shot(string name, float wait)
            {
                yield return new WaitForSecondsRealtime(wait);
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, name + ".png"));
                Debug.Log($"[MenuShots] {name}");
                yield return null;
            }

            void Hover(Transform root, string name)
            {
                foreach (var fx in root.GetComponentsInChildren<UiTextButtonFx>(true))
                {
                    fx.ForceHover = fx.name == name;
                    if (fx.name == name) OnButtonHovered(fx.Description);
                }
            }

            yield return Shot("01_main", 3.5f);
            Hover(_main, "Find");
            yield return Shot("02_main_hover", 1.0f);
            Hover(_main, "Quit");
            yield return Shot("03_main_hover_quit", 1.0f);
            Hover(_main, "");
            OnButtonHovered(null);

            if (picker != null)
            {
                picker.SetOpen(true);
                yield return Shot("04_picker_open", 0.8f);
                foreach (var w in new[] { MenuWeapon.Knife, MenuWeapon.Chainsaw, MenuWeapon.Glock, MenuWeapon.Random })
                {
                    SetWeapon(w);
                    picker.SetOpen(false);
                    yield return Shot("05_weapon_" + w, w == MenuWeapon.Random ? 1.5f : 2.6f);
                }
            }

            Show(_character);
            yield return Shot("06_character", 1.0f);
            Show(_browser);
            yield return Shot("07_browser", 1.5f);
            Show(_options);
            yield return Shot("08_options", 1.0f);
            Show(_audio);
            yield return Shot("09_audio", 1.0f);
            Show(_voicePage);
            yield return Shot("10_voice", 1.0f);
            Show(_main);
            ConfirmDialog.ConfirmQuit();
            yield return Shot("11_quit_dialog", 1.2f);
            var dlg = FindAnyObjectByType<ConfirmDialog>();
            if (dlg != null)
                foreach (var fx in dlg.GetComponentsInChildren<UiTextButtonFx>(true)) fx.ForceHover = fx.name == "Yes";
            yield return Shot("12_quit_dialog_hover", 0.8f);
            if (dlg != null) Destroy(dlg.gameObject);
            yield return Shot("13_back", 1.0f);
            Debug.Log("[MenuShots] done");
            Application.Quit();
        }
    }
}
