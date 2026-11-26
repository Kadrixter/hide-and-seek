using HideAndSeek.Game.Net;
using UnityEngine;
using UnityEngine.UI;

namespace HideAndSeek.Game.UI
{
    /// <summary>
    /// Main menu layout (text-button style): vignette + left shade over the 3D scene, spaced logo, a text list on
    /// the left with a grey description of the hovered item, footer (nick, Steam, version), the pages.
    /// </summary>
    public partial class MainMenu
    {
        private Text _description, _playingAs;
        private RectTransform _footer;
        private string _descTarget;
        private float _descAlpha;

        private static readonly Color Dim = new Color(1f, 1f, 1f, 0.5f);

        private void BuildChrome(Transform root)
        {
            MenuChrome.VignetteImage(root, 0.55f);
            MenuChrome.Shade("Shade", root, 0.95f).raycastTarget = false;
            _canvas.gameObject.AddComponent<MenuFontScope>().StyleTitles = true;
            _canvas.gameObject.AddComponent<HideUnderDialog>();

            float x = MenuChrome.ColumnX;
            var logo = UiKit.Label("Logo", root, UiFonts.Spaced("Hide and Seek"), 38, TextAnchor.UpperLeft, new Color(1f, 1f, 1f, 0.92f));
            logo.font = UiFonts.Bold;
            logo.horizontalOverflow = HorizontalWrapMode.Overflow;
            logo.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -96), new Vector2(900, 50));
            logo.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.6f);
            var line = UiKit.Image("LogoLine", root, UiKit.ButtonHover);
            line.raycastTarget = false;
            line.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(x + 2, -156), new Vector2(48, 3));
            var sub = UiKit.Label("Sub", root, "chowany w ciemnościach", 21, TextAnchor.UpperLeft, new Color(1f, 1f, 1f, 0.42f));
            sub.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(x + 66, -144), new Vector2(600, 30));

            _column = UiKit.Rect("Column", root);
            _column.anchorMin = new Vector2(0, 0);
            _column.anchorMax = new Vector2(0, 1);
            _column.pivot = new Vector2(0, 1);
            _column.offsetMin = new Vector2(x, 250);
            _column.offsetMax = new Vector2(x + 640, -230);

            // footer: message, playing as, Steam, version (hidden on the long sound / voice pages)
            _footer = UiKit.Rect("Footer", root);
            _footer.Stretch();
            root = _footer;
            _message = UiKit.Label("Message", root, NetSession.LastMessage ?? "", 22, TextAnchor.LowerLeft, new Color(1f, 0.6f, 0.5f));
            _message.rectTransform.Place(Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(x, 150), new Vector2(900, 80));
            NetSession.LastMessage = null;
            _playingAs = UiKit.Label("PlayingAs", root, "", 21, TextAnchor.LowerLeft, Dim);
            _playingAs.supportRichText = true;
            _playingAs.rectTransform.Place(Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(x, 104), new Vector2(900, 30));
            _steamStatus = UiKit.Label("Steam", root, SteamStatusText(), 18, TextAnchor.LowerLeft, new Color(1f, 1f, 1f, 0.42f));
            _steamStatus.supportRichText = true;
            _steamStatus.rectTransform.Place(Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(x, 74), new Vector2(1100, 28));
            var ver = UiKit.Label("Version", root, "WERSJA " + Application.version, 16, TextAnchor.LowerLeft, new Color(1f, 1f, 1f, 0.3f));
            ver.rectTransform.Place(Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(x, 46), new Vector2(600, 26));
            UpdatePlayingAs();
        }

        private void UpdatePlayingAs()
        {
            if (_playingAs == null) return;
            string skin = string.IsNullOrEmpty(LocalProfile.McNick) ? $"skin {LocalProfile.Skin + 1}" : $"skin {LocalProfile.McNick}";
            _playingAs.text = $"GRASZ JAKO  <color=#ffffff>{LocalProfile.Nick}</color>   ·   {skin}";
        }

        // ================================================================ pages

        private RectTransform BuildMain(Transform parent)
        {
            var page = Page(parent, "Main");
            const int size = 46;
            const float h = 74f;
            var steam = UiKit.Button("HostSteam", page, "Graj przez Steam", HostSteam, size)
                .Describe(SteamService.Ready ? "Załóż serwer i zaproś znajomych ze Steam" : null);
            steam.Height(h);
            steam.interactable = SteamService.Ready;
            UiKit.Button("Host", page, "Serwer LAN / UDP", Host, size).Describe("Załóż serwer w sieci lokalnej albo po adresie IP").Height(h);
            UiKit.Button("Find", page, "Szukaj serwerów", () => Show(_browser), size).Describe("Dołącz do gry znajomych - Steam, LAN albo po IP").Height(h);
            UiKit.Button("Character", page, "Postać", () => Show(_character), size).Describe("Nick, skin i skin z Minecrafta").Height(h);
            UiKit.Button("Options", page, "Ustawienia", () => Show(_options), size).Describe("Mysz, obraz, dźwięk i mikrofon").Height(h);
            UiKit.Button("Quit", page, "Wyjdź", ConfirmDialog.ConfirmQuit, size, UiKit.Danger).Describe("Do zobaczenia :]").Height(h);
            _description = UiKit.Label("Description", page, "", 22, TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, 0f));
            _description.Height(48);
            return page;
        }

        private RectTransform BuildCharacter(Transform parent)
        {
            var page = Page(parent, "Character");
            UiKit.Label("T", page, "Postać", 26).Height(56);
            UiKit.Label("NickL", page, "Nick", 22, TextAnchor.MiddleLeft, UiKit.TextDim).Height(30);
            _nick = UiKit.Input("Nick", page, LocalProfile.Nick, "Twój nick");
            _nick.characterLimit = 16;
            _nick.onEndEdit.AddListener(v =>
            {
                LocalProfile.Nick = v;
                _nick.text = LocalProfile.Nick;
                RefreshPreview();
                UpdatePlayingAs();
            });
            _nick.Height(58);

            UiKit.Label("SkinL", page, "Skin", 22, TextAnchor.MiddleLeft, UiKit.TextDim).Height(34);
            var skinRow = Row(page, 58);
            var prev = UiKit.BoxButton("Prev", skinRow, "<", () => ChangeSkin(-1));
            prev.gameObject.AddComponent<LayoutElement>().preferredWidth = 70;
            _skinLabel = UiKit.Label("Skin", skinRow, "", 26, TextAnchor.MiddleCenter);
            _skinLabel.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            var next = UiKit.BoxButton("Next", skinRow, ">", () => ChangeSkin(1));
            next.gameObject.AddComponent<LayoutElement>().preferredWidth = 70;
            UpdateSkinLabel();

            var mcRow = Row(page, 54);
            _mcNick = UiKit.Input("McNick", mcRow, LocalProfile.McNick, "Skin z Minecrafta: wpisz nick", 24);
            _mcNick.characterLimit = 16;
            _mcNick.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            var fetch = UiKit.BoxButton("Fetch", mcRow, "Pobierz", () => FetchMcSkin(_mcNick.text), 24);
            fetch.gameObject.AddComponent<LayoutElement>().preferredWidth = 140;
            _mcStatus = UiKit.Label("McStatus", page, "", 20, TextAnchor.MiddleLeft, UiKit.TextDim);
            _mcStatus.Height(30);
            UiKit.Rect("Gap", page).Height(14);
            UiKit.Button("Back", page, "Wróć", () => { SaveNick(); UpdatePlayingAs(); Show(_main); }, 34).Height(58);
            return page;
        }

        private static RectTransform Row(Transform page, float height)
        {
            var row = UiKit.Rect("Row", page);
            row.Height(height).flexibleHeight = 0f; // the stretched page must not grow the row
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 10;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            return row;
        }

        private RectTransform BuildBrowser(Transform parent)
        {
            var page = Page(parent, "Browser");
            UiKit.Label("T", page, "Serwery", 26).Height(56);
            _serverList = UiKit.Label("Searching", page, "Szukam serwerów...", 22, TextAnchor.MiddleLeft, UiKit.TextDim);
            _serverList.Height(32);
            _listRoot = UiKit.Rect("List", page);
            UiKit.VStack(_listRoot, 8);
            _listRoot.Height(330);
            UiKit.Label("IpL", page, "Albo połącz po adresie IP", 22, TextAnchor.MiddleLeft, UiKit.TextDim).Height(30);
            _ip = UiKit.Input("Ip", page, LocalProfile.LastIp, "np. 192.168.1.10");
            _ip.Height(58);
            UiKit.Button("Join", page, "Połącz", () => Join(_ip.text), 34).Height(58);
            UiKit.Button("Back", page, "Wróć", () => Show(_main), 34).Height(58);
            return page;
        }

        private RectTransform BuildOptions(Transform parent)
        {
            var page = Page(parent, "Options");
            UiKit.Label("T", page, "Ustawienia", 26).Height(56);
            SettingsUi.SensitivitySlider(page);
            SettingsUi.DisplayModeButton(page);
            SettingsUi.ResolutionButton(page);
            SettingsUi.CrouchModeButton(page);
            SettingsUi.DiscordSwitch(page);
            UiKit.Rect("Gap", page).Height(6);
            UiKit.Button("Audio", page, "Dźwięk", () => Show(_audio), 34).Height(58);
            UiKit.Button("Voice", page, "Czat głosowy", () => Show(_voicePage), 34).Height(58);
            UiKit.Button("Graphics", page, "Grafika", () => Show(_graphics), 34).Height(58);
            UiKit.Button("Pad", page, "Pad (PS4 / PS5)", () => Show(_padPage), 34).Height(58); // MainMenu.PadSettings.cs
            UiKit.Button("Back", page, "Wróć", () => { Prefs.Save(); Show(_main); }, 34).Height(58);
            return page;
        }

        private RectTransform BuildVoice(Transform parent)
        {
            var page = Page(parent, "Voice");
            page.offsetMax = new Vector2(0, 40); // long page: starts higher, footer hidden
            UiKit.Label("T", page, "Czat głosowy", 26).Height(50);
            SettingsUi.BuildVoice(page);
            UiKit.Label("Info", page, "Mikrofon działa po wejściu na serwer.", 20, TextAnchor.MiddleLeft, UiKit.TextDim).Height(28);
            UiKit.Button("Back", page, "Wróć", () => { Prefs.Save(); Show(_options); }, 34).Height(56);
            return page;
        }

        private RectTransform BuildAudio(Transform parent)
        {
            var page = Page(parent, "Audio");
            page.offsetMax = new Vector2(0, 40);
            UiKit.Label("T", page, "Dźwięk", 26).Height(50);
            SettingsUi.BuildAudio(page);
            UiKit.Button("Back", page, "Wróć", () => { Prefs.Save(); Show(_options); }, 34).Height(56);
            return page;
        }

        private RectTransform BuildConnecting(Transform root)
        {
            var dim = UiKit.Image("Connecting", root, new Color(0, 0, 0, 0.78f));
            dim.rectTransform.Stretch();
            _connectingText = UiKit.Label("Text", dim.transform, "", 34, TextAnchor.MiddleCenter);
            _connectingText.font = UiFonts.SemiBold;
            _connectingText.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(1200, 100));
            var cancel = UiKit.Button("Cancel", dim.transform, "Anuluj", () =>
            {
                NetSession.Instance.Leave();
                _connecting.gameObject.SetActive(false);
            }, 34).Align(TextAnchor.MiddleCenter);
            cancel.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -50), new Vector2(300, 64));
            if (LoadingScreen.Available)
            {
                // The loading screen shows the connection; this overlay only keeps the menu's timeout logic going.
                var g = dim.gameObject.AddComponent<CanvasGroup>();
                g.alpha = 0f;
                g.blocksRaycasts = false;
                g.interactable = false;
            }
            dim.gameObject.SetActive(false);
            return dim.rectTransform;
        }

        // ================================================================ hover description

        private void OnButtonHovered(string desc)
        {
            if (!string.IsNullOrEmpty(desc)) _descTarget = desc;
            else _descTarget = null;
        }

        private void UpdateDescription()
        {
            if (_description == null) return;
            float target = _descTarget != null ? 0.55f : 0f;
            if (_descTarget != null && _description.text != _descTarget)
            {
                // new text: fade out first, then swap
                _descAlpha = Mathf.MoveTowards(_descAlpha, 0f, Time.unscaledDeltaTime * 6f);
                if (_descAlpha <= 0.001f || _description.text.Length == 0) _description.text = _descTarget;
            }
            else _descAlpha = Mathf.MoveTowards(_descAlpha, target, Time.unscaledDeltaTime * 4f);
            _description.color = new Color(1f, 1f, 1f, _descAlpha);
        }
    }
}
