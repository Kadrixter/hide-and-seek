using System.Collections;
using System.IO;
using HideAndSeek.Game.Net;
using HideAndSeek.Game.UI;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Esc menu in the text-button style: the game keeps running behind a live blur (<see cref="MenuBlur"/>), a left
    /// shade and a transparent column with upper-case text buttons; settings live on their own page.
    /// </summary>
    public partial class GameHud
    {
        /// <summary>Full-screen menu root: blur request, left shade, menu font; the column is the child "Panel".</summary>
        private RectTransform MenuRoot(string name, Vector2 size)
        {
            // Own canvas above the whole HUD (the HUD canvases are faded out while it lives: HudHider).
            var canvas = UiKit.CreateCanvas(name, 400, transform);
            var dim = canvas.gameObject.AddComponent<Image>();
            dim.color = Color.black;
            dim.sprite = MenuChrome.LeftShade;
            dim.raycastTarget = true;
            dim.gameObject.AddComponent<MenuBlurRequest>();
            dim.gameObject.AddComponent<MenuFontScope>();
            dim.gameObject.AddComponent<HideUnderDialog>();
            dim.gameObject.AddComponent<HudHider>();
            MenuChrome.VignetteImage(dim.transform, 0.45f);
            MenuChrome.Column("Panel", dim.transform, size);
            return dim.rectTransform;
        }

        private void ShowEscMenu()
        {
            bool invite = SteamService.InLobby;
            _escMenu = MenuPanel("EscMenu", new Vector2(640, invite ? 620 : 550));
            var panel = _escMenu.Find("Panel");
            UiKit.Label("Title", panel, "Menu", 26).Height(56);
            UiKit.Button("Resume", panel, "Wróć do gry", CloseMenus, 40).Height(68);
            if (invite) UiKit.Button("Invite", panel, "Zaproś znajomych", ShowInviteMenu, 40).Height(68);
            UiKit.Button("Settings", panel, "Ustawienia", ShowGameSettings, 40).Height(68);
            UiKit.Button("PlayerVolumes", panel, "Głośność graczy", ShowPlayerVolumeMenu, 40).Height(68);
            bool isHost = NetworkManager.Singleton.IsServer;
            UiKit.Button("Leave", panel, isHost ? "Zamknij serwer" : "Rozłącz", () => ConfirmDialog.ConfirmLeave(isHost, () =>
            {
                CloseMenus();
                NetSession.Instance.Leave();
            }), 40, UiKit.Danger).Height(68);
            UiKit.Button("Quit", panel, "Wyjdź z gry", ConfirmDialog.ConfirmQuit, 40, UiKit.Danger).Height(68);

            var hint = UiKit.Label("Hint", _escMenu, "ESC - WRÓĆ DO GRY", 18, TextAnchor.LowerLeft, new Color(1f, 1f, 1f, 0.42f));
            hint.rectTransform.Place(Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(MenuChrome.ColumnX, 48f), new Vector2(600f, 30f));
        }

        /// <summary>"Ustawienia" page of the Esc menu: mouse, window, crouch, Discord, sound and voice pages.</summary>
        private void ShowGameSettings()
        {
            if (_escMenu != null) Destroy(_escMenu.gameObject);
            _escMenu = MenuPanel("SettingsMenu", new Vector2(680, 822));
            var panel = _escMenu.Find("Panel");
            UiKit.Label("Title", panel, "Ustawienia", 26).Height(56);
            SettingsUi.SensitivitySlider(panel, v => { if (NetPlayer.Local != null) NetPlayer.Local.InputReader.MouseSensitivity = v; });
            SettingsUi.DisplayModeButton(panel);
            SettingsUi.ResolutionButton(panel);
            SettingsUi.CrouchModeButton(panel, v => { if (NetPlayer.Local != null) NetPlayer.Local.InputReader.CrouchToggleMode = v; });
            SettingsUi.DiscordSwitch(panel);
            UiKit.Rect("Gap", panel).Height(6);
            UiKit.Button("Audio", panel, "Dźwięk", ShowAudioMenu, 34).Height(58);
            UiKit.Button("Voice", panel, "Czat głosowy", ShowVoiceMenu, 34).Height(58);
            UiKit.Button("Graphics", panel, "Grafika", ShowGraphicsMenu, 34).Height(58); // GameHud.Graphics.cs
            UiKit.Button("Back", panel, "Wróć", () =>
            {
                Prefs.Save();
                Destroy(_escMenu.gameObject);
                _escMenu = null;
                ShowEscMenu();
            }, 34).Height(58);
        }

        // ================================================================ test (-testesc [-escshots dir])

        private IEnumerator EscTestRun()
        {
            string dir = Arg("-escshots");
            yield return new WaitForSeconds(3f);
            ShowEscMenu();
            yield return Shot(dir, "esc_root", 1.2f);
            HoverFirst(_escMenu, "Settings");
            yield return Shot(dir, "esc_root_hover", 0.6f);
            HoverFirst(_escMenu, "Leave");
            yield return Shot(dir, "esc_root_hover_leave", 0.6f);
            ShowGameSettings();
            yield return Shot(dir, "esc_settings", 1.0f);
            ShowAudioMenu();
            yield return Shot(dir, "esc_audio", 1.0f);
            ShowVoiceMenu();
            yield return Shot(dir, "esc_voice", 1.0f);
            ShowPlayerVolumeMenu();
            yield return Shot(dir, "esc_players", 1.0f);
            CloseMenus();
            ShowEscMenu();
            ConfirmDialog.ConfirmLeave(NetworkManager.Singleton.IsServer, () => { });
            yield return Shot(dir, "esc_confirm", 1.0f);
            var dlg = Object.FindAnyObjectByType<ConfirmDialog>();
            if (dlg != null) HoverFirst(dlg.transform, "Yes");
            yield return Shot(dir, "esc_confirm_hover", 0.6f);
            if (dlg != null) Destroy(dlg.gameObject);
            CloseMenus();
            if (_settingsPanel == null) ShowSettings(true);
            yield return Shot(dir, "round_settings", 1.2f);
            CloseMenus();
            yield return Shot(dir, "closed", 0.8f);
            Debug.Log("[EscTest] done");
        }

        private static void HoverFirst(Transform root, string name)
        {
            if (root == null) return;
            foreach (var fx in root.GetComponentsInChildren<UiTextButtonFx>(true)) fx.ForceHover = fx.name == name;
        }

        private static string Arg(string key)
        {
            var a = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(a, key);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
        }

        private static IEnumerator Shot(string dir, string name, float wait)
        {
            yield return new WaitForSecondsRealtime(wait);
            if (string.IsNullOrEmpty(dir)) yield break;
            Directory.CreateDirectory(dir);
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, name + ".png"));
            Debug.Log($"[EscTest] shot {name}");
            yield return null;
        }
    }
}
