using System.Collections.Generic;
using System.Linq;
using HideAndSeek.Audio;
using HideAndSeek.Characters;
using HideAndSeek.Game.Net;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace HideAndSeek.Game.UI
{
    /// <summary>Main menu: nick + skin (live 3D preview), host, server browser with LAN search, join by IP, options.</summary>
    public class MainMenu : MonoBehaviour
    {
        [SerializeField] private Transform previewRoot;

        private Canvas _canvas;
        private RectTransform _main, _browser, _options, _audio, _voicePage, _connecting;
        private Text _message, _skinLabel, _serverList, _connectingText;
        private InputField _nick, _ip, _mcNick;
        private Text _mcStatus;
        private RectTransform _listRoot;
        private BlockyCharacter _preview;
        private float _nextListRefresh;
        private float _connectStarted;

        private static MainMenu _instance;

        /// <summary>Shows a message on the main menu (e.g. a failed Steam join from an invite).</summary>
        public static void ShowMessage(string text)
        {
            if (_instance == null) return;
            _instance._connecting.gameObject.SetActive(false);
            _instance._message.text = text ?? "";
        }

        /// <summary>Shows the "connecting" overlay (Steam joins started outside the menu buttons).</summary>
        public static void ShowConnecting()
        {
            if (_instance == null) return;
            _instance._connecting.gameObject.SetActive(true);
            _instance._connectingText.text = "Łączenie przez Steam...";
            _instance._connectStarted = Time.unscaledTime;
        }

        private Text _steamStatus;
        private readonly List<SteamService.LobbyInfo> _steamLobbies = new();
        private float _nextSteamSearch;
        private bool _steamSearching;

        private void Start()
        {
            _instance = this;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            LocalProfile.ApplyAudio();
            AudioEnvironment.Apply(AudioEnvironmentKind.Menu, true);

            _canvas = UiKit.CreateCanvas("MenuCanvas", 0);
            var root = _canvas.transform;

            var side = UiKit.Image("Side", root, new Color(0.02f, 0.022f, 0.026f, 0.86f));
            side.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(700, 0));

            var title = UiKit.Label("Title", side.transform, "HIDE AND SEEK", 62, TextAnchor.UpperLeft, null, FontStyle.Bold);
            title.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(60, -60), new Vector2(-120, 100));
            title.AddDrop(4f, 0.6f);
            var sub = UiKit.Label("Sub", side.transform, "chowany w ciemnościach", 30, TextAnchor.UpperLeft, UiKit.TextDim);
            sub.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(64, -150), new Vector2(-120, 40));
            var line = UiKit.Image("TitleLine", side.transform, UiKit.Accent).Rounded(true);
            line.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(64, -196), new Vector2(160, 5));
            var edge = UiKit.Image("Edge", side.transform, new Color(1f, 1f, 1f, 0.05f));
            edge.rectTransform.Place(new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), Vector2.zero, new Vector2(2, 0));

            _steamStatus = UiKit.Label("Steam", side.transform, SteamStatusText(), 20, TextAnchor.LowerLeft, UiKit.TextDim);
            _steamStatus.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(60, 14), new Vector2(-120, 28));
            _steamStatus.supportRichText = true;
            _message = UiKit.Label("Message", side.transform, NetSession.LastMessage ?? "", 26, TextAnchor.LowerLeft, new Color(1f, 0.6f, 0.5f));
            _message.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(60, 40), new Vector2(-120, 80));
            NetSession.LastMessage = null;

            var ver = UiKit.Label("Version", root, "wersja " + Application.version, 20, TextAnchor.LowerRight, UiKit.TextDim);
            ver.rectTransform.Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-20, 14), new Vector2(500, 30));
            UpdateBanner.Create(root);

            _main = BuildMain(side.transform);
            _browser = BuildBrowser(side.transform);
            _options = BuildOptions(side.transform);
            _audio = BuildAudio(side.transform);
            _voicePage = BuildVoice(side.transform);
            _connecting = BuildConnecting(root);
            Show(_main);

            BuildPreview();
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-testmenu") >= 0) StartCoroutine(TestPages());
        }

        /// <summary>Test switch -testmenu: walks through the pages (for screenshots).</summary>
        private System.Collections.IEnumerator TestPages()
        {
            foreach (var p in new[] { _main, _options, _audio, _voicePage, _main })
            {
                Show(p);
                yield return new WaitForSeconds(2.2f);
            }
        }

        private RectTransform Page(Transform parent, string name)
        {
            var rt = UiKit.Rect(name, parent);
            rt.Place(new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(-120, -420));
            UiKit.VStack(rt, 10);
            return rt;
        }

        private void Show(RectTransform page)
        {
            foreach (var p in new[] { _main, _browser, _options, _audio, _voicePage }) p.gameObject.SetActive(p == page);
            if (page == _browser) LanDiscovery.StartSearch();
            else LanDiscovery.StopSearch();
        }

        // ================================================================ pages

        private RectTransform BuildMain(Transform parent)
        {
            var page = Page(parent, "Main");
            UiKit.Label("NickL", page, "Nick", 24, TextAnchor.MiddleLeft, UiKit.TextDim).Height(30);
            _nick = UiKit.Input("Nick", page, LocalProfile.Nick, "Twój nick");
            _nick.characterLimit = 16;
            _nick.onEndEdit.AddListener(v =>
            {
                LocalProfile.Nick = v;
                _nick.text = LocalProfile.Nick;
                RefreshPreview();
            });
            _nick.Height(58);

            var skinRow = UiKit.Rect("SkinRow", page);
            skinRow.Height(58);
            var h = skinRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 10;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            var prev = UiKit.Button("Prev", skinRow, "<", () => ChangeSkin(-1));
            prev.gameObject.AddComponent<LayoutElement>().preferredWidth = 70;
            _skinLabel = UiKit.Label("Skin", skinRow, "", 28, TextAnchor.MiddleCenter);
            _skinLabel.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            var next = UiKit.Button("Next", skinRow, ">", () => ChangeSkin(1));
            next.gameObject.AddComponent<LayoutElement>().preferredWidth = 70;
            UpdateSkinLabel();

            // Minecraft skin by nick
            var mcRow = UiKit.Rect("McRow", page);
            mcRow.Height(52);
            var mh = mcRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            mh.spacing = 10;
            mh.childControlWidth = true;
            mh.childControlHeight = true;
            mh.childForceExpandWidth = false;
            _mcNick = UiKit.Input("McNick", mcRow, LocalProfile.McNick, "Skin z Minecrafta: wpisz nick", 24);
            _mcNick.characterLimit = 16;
            _mcNick.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            var fetch = UiKit.Button("Fetch", mcRow, "Pobierz", () => FetchMcSkin(_mcNick.text), 24);
            fetch.gameObject.AddComponent<LayoutElement>().preferredWidth = 130;
            _mcStatus = UiKit.Label("McStatus", page, "", 20, TextAnchor.MiddleLeft, UiKit.TextDim);
            _mcStatus.Height(24);

            UiKit.Rect("Gap", page).Height(6);
            var steamHost = UiKit.Button("HostSteam", page, "Stwórz serwer Steam", HostSteam, 30, new Color(0.13f, 0.32f, 0.16f));
            steamHost.Height(70);
            steamHost.interactable = SteamService.Ready;
            UiKit.Button("Host", page, "Stwórz serwer LAN / UDP", Host, 26).Height(58);
            UiKit.Button("Find", page, "Szukaj serwerów", () => Show(_browser), 30).Height(66);
            UiKit.Button("Options", page, "Ustawienia", () => Show(_options), 28).Height(58);
            UiKit.Button("Quit", page, "Wyjdź", ConfirmDialog.ConfirmQuit, 28).Height(58);
            return page;
        }

        private RectTransform BuildBrowser(Transform parent)
        {
            var page = Page(parent, "Browser");
            UiKit.Label("T", page, "SERWERY", 36, TextAnchor.MiddleLeft, null, FontStyle.Bold).Height(46);
            _serverList = UiKit.Label("Searching", page, "Szukam serwerów...", 24, TextAnchor.MiddleLeft, UiKit.TextDim);
            _serverList.Height(32);
            _listRoot = UiKit.Rect("List", page);
            UiKit.VStack(_listRoot, 8);
            _listRoot.Height(330);

            UiKit.Label("IpL", page, "Albo połącz po adresie IP", 24, TextAnchor.MiddleLeft, UiKit.TextDim).Height(30);
            _ip = UiKit.Input("Ip", page, LocalProfile.LastIp, "np. 192.168.1.10");
            _ip.Height(58);
            UiKit.Button("Join", page, "Połącz", () => Join(_ip.text), 30, new Color(0.13f, 0.32f, 0.16f)).Height(66);
            UiKit.Button("Back", page, "Wróć", () => Show(_main), 28).Height(60);
            return page;
        }

        private RectTransform BuildOptions(Transform parent)
        {
            var page = Page(parent, "Options");
            UiKit.Label("T", page, "USTAWIENIA", 36, TextAnchor.MiddleLeft, null, FontStyle.Bold).Height(46);
            SettingsUi.SensitivitySlider(page);
            SettingsUi.DisplayModeButton(page);
            SettingsUi.ResolutionButton(page);
            SettingsUi.CrouchModeButton(page);
            SettingsUi.DiscordSwitch(page);
            UiKit.Button("Audio", page, "Dźwięk", () => Show(_audio), 28).Height(60);
            UiKit.Button("Voice", page, "Czat głosowy", () => Show(_voicePage), 28).Height(60);
            UiKit.Rect("Gap", page).Height(20);
            UiKit.Button("Back", page, "Wróć", () => { Prefs.Save(); Show(_main); }, 28).Height(60);
            return page;
        }

        private RectTransform BuildVoice(Transform parent)
        {
            var page = UiKit.Rect("Voice", parent);
            page.Place(new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -70), new Vector2(-120, -280));
            UiKit.VStack(page, 6);
            UiKit.Label("T", page, "CZAT GŁOSOWY", 34, TextAnchor.MiddleLeft, null, FontStyle.Bold).Height(42);
            SettingsUi.BuildVoice(page);
            UiKit.Label("Info", page, "Mikrofon działa po wejściu na serwer.", 20, TextAnchor.MiddleLeft, UiKit.TextDim).Height(28);
            UiKit.Button("Back", page, "Wróć", () => { Prefs.Save(); Show(_options); }, 26).Height(52);
            return page;
        }

        private RectTransform BuildAudio(Transform parent)
        {
            var page = UiKit.Rect("Audio", parent);
            page.Place(new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -70), new Vector2(-120, -280));
            UiKit.VStack(page, 6);
            UiKit.Label("T", page, "DŹWIĘK", 34, TextAnchor.MiddleLeft, null, FontStyle.Bold).Height(42);
            SettingsUi.BuildAudio(page);
            UiKit.Button("Back", page, "Wróć", () => { Prefs.Save(); Show(_options); }, 26).Height(52);
            return page;
        }

        private RectTransform BuildConnecting(Transform root)
        {
            var dim = UiKit.Image("Connecting", root, new Color(0, 0, 0, 0.75f));
            dim.rectTransform.Stretch();
            _connectingText = UiKit.Label("Text", dim.transform, "", 36, TextAnchor.MiddleCenter);
            _connectingText.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(1200, 100));
            var cancel = UiKit.Button("Cancel", dim.transform, "Anuluj", () =>
            {
                NetSession.Instance.Leave();
                _connecting.gameObject.SetActive(false);
            }, 28);
            cancel.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(260, 60));
            dim.gameObject.SetActive(false);
            return dim.rectTransform;
        }

        // ================================================================ actions

        private void SaveNick()
        {
            LocalProfile.Nick = _nick.text;
            Prefs.Save();
        }

        private void Host()
        {
            SaveNick();
            LanDiscovery.StopSearch();
            if (!NetSession.Instance.Host())
            {
                _message.text = NetSession.LastMessage;
                NetSession.LastMessage = null;
            }
        }

        private static string SteamStatusText() => SteamService.Ready
            ? $"<color=#6dff7a>Steam:</color> {SteamService.PersonaName}  (zaproszenia od znajomych działają)"
            : $"<color=#ff8a7a>Steam niedostępny</color> ({SteamService.Error}) - działa tylko LAN / UDP";

        private void HostSteam()
        {
            SaveNick();
            LanDiscovery.StopSearch();
            _connecting.gameObject.SetActive(true);
            _connectingText.text = "Tworzenie lobby Steam...";
            _connectStarted = Time.unscaledTime;
            NetSession.Instance.HostSteam();
        }

        private void JoinSteam(ulong lobbyId, string name)
        {
            SaveNick();
            LanDiscovery.StopSearch();
            _connecting.gameObject.SetActive(true);
            _connectingText.text = $"Łączenie z {name} przez Steam...";
            _connectStarted = Time.unscaledTime;
            NetSession.Instance.JoinSteamLobby(lobbyId);
        }

        private async void SearchSteam()
        {
            if (_steamSearching || !SteamService.Ready) return;
            _steamSearching = true;
            try
            {
                var found = await SteamService.FindLobbies();
                _steamLobbies.Clear();
                foreach (var l in found)
                    if (l.Id != (SteamService.CurrentLobby.HasValue ? SteamService.CurrentLobby.Value.Id.Value : 0)) _steamLobbies.Add(l);
            }
            finally { _steamSearching = false; }
        }

        private void Join(string address)
        {
            SaveNick();
            LanDiscovery.StopSearch();
            _connecting.gameObject.SetActive(true);
            _connectingText.text = $"Łączenie z {address}...";
            _connectStarted = Time.unscaledTime;
            if (!NetSession.Instance.Join(address))
            {
                _connecting.gameObject.SetActive(false);
                _message.text = NetSession.LastMessage;
            }
        }

        private void FetchMcSkin(string nick)
        {
            nick = (nick ?? "").Trim();
            if (nick.Length == 0)
            {
                LocalProfile.McNick = "";
                Prefs.Save();
                _mcStatus.text = "Używasz skina z gry.";
                if (_preview != null) _preview.SetCustomSkin(null);
                UpdateSkinLabel();
                return;
            }
            _mcStatus.text = $"Pobieram skin {nick}...";
            MinecraftSkins.Get(nick, (tex, err) =>
            {
                if (this == null) return;
                if (tex == null)
                {
                    _mcStatus.text = err ?? "Nie udało się pobrać skina.";
                    return;
                }
                LocalProfile.McNick = nick;
                Prefs.Save();
                _mcStatus.text = $"Skin {nick} ustawiony :]  (puste pole + Pobierz = skin z gry)";
                if (_preview != null) _preview.SetCustomSkin(tex);
                UpdateSkinLabel();
            });
        }

        private void ChangeSkin(int d)
        {
            if (!string.IsNullOrEmpty(LocalProfile.McNick))
            {
                // Arrows switch back to the built-in skins.
                LocalProfile.McNick = "";
                if (_mcNick != null) _mcNick.text = "";
                if (_preview != null) _preview.SetCustomSkin(null);
                _mcStatus.text = "Używasz skina z gry.";
                UpdateSkinLabel();
                return;
            }
            LocalProfile.Skin = (LocalProfile.Skin + d + 5) % 5;
            UpdateSkinLabel();
            RefreshPreview();
        }

        private void UpdateSkinLabel() => _skinLabel.text = string.IsNullOrEmpty(LocalProfile.McNick)
            ? $"Skin {LocalProfile.Skin + 1} / 5"
            : $"Minecraft: {LocalProfile.McNick}";

        // ================================================================ preview

        private void BuildPreview()
        {
            if (previewRoot == null) return;
            _preview = BlockyCharacterFactory.Create(previewRoot, LocalProfile.Skin, LocalProfile.Nick);
            _preview.SetFirstPersonMode(false);
            if (!string.IsNullOrEmpty(LocalProfile.McNick)) FetchMcSkin(LocalProfile.McNick);
            _previewYaw = previewRoot.eulerAngles.y;
            StartCoroutine(PoseCycle());
        }

        private float _previewYaw;
        private AudioSource _menuLoop;

        /// <summary>Menu character: stands in a 3/4 pose and shows off the gear in a loop instead of spinning.</summary>
        private System.Collections.IEnumerator PoseCycle()
        {
            var a = _preview != null ? _preview.Actions : null;
            if (a == null) yield break;
            var wait = new System.Func<float, WaitForSeconds>(s => new WaitForSeconds(s));
            Transform at = previewRoot;
            void Sfx(GameSfx id, float v = 0.8f) => GameAudio.PlayAt(id, at.position + Vector3.up * 1.1f, v);
            while (true)
            {
                yield return wait(4.5f);
                a.SetHeldItem(HideAndSeek.Core.HeldItem.Knife);
                Sfx(GameSfx.KnifeDraw);
                yield return wait(0.9f);
                Sfx(GameSfx.KnifeSwing, 0.5f);
                yield return wait(0.5f);
                Sfx(GameSfx.KnifeSwing, 0.4f);
                yield return wait(1.4f);
                a.SetHeldItem(HideAndSeek.Core.HeldItem.None);
                yield return wait(4f);
                a.SetHeldItem(HideAndSeek.Core.HeldItem.Glock);
                Sfx(GameSfx.GlockEquip);
                yield return wait(1.2f);
                a.PlayReload();
                Sfx(GameSfx.GlockReload);
                yield return wait(2.4f);
                a.SetHeldItem(HideAndSeek.Core.HeldItem.None);
                yield return wait(4f);
                a.SetHeldItem(HideAndSeek.Core.HeldItem.Chainsaw);
                Debug.Log($"[MainMenu] preview chainsaw at t={Time.time:0.0}");
                Sfx(GameSfx.ChainsawStart, 0.7f);
                var idle = _menuLoop = GameAudio.StartLoop(GameSfx.ChainsawIdle, at, 0.5f);
                yield return wait(1.6f);
                a.SetRevving(true);
                GameAudio.StopLoop(idle, 0.1f);
                var rev = _menuLoop = GameAudio.StartLoop(GameSfx.ChainsawRev, at, 0.6f);
                yield return wait(1.5f);
                a.SetRevving(false);
                GameAudio.StopLoop(rev, 0.3f);
                idle = _menuLoop = GameAudio.StartLoop(GameSfx.ChainsawIdle, at, 0.5f);
                yield return wait(1.4f);
                GameAudio.StopLoop(idle, 0.4f);
                _menuLoop = null;
                a.SetHeldItem(HideAndSeek.Core.HeldItem.None);
            }
        }

        private void RefreshPreview()
        {
            if (_preview != null) _preview.SetSkin(LocalProfile.Skin, LocalProfile.Nick);
        }

        private Vector2Int _lastScreen;
        private float _refreshAt = 0.4f, _refreshAt2 = 1.5f;

        /// <summary>Re-renders the menu texts when the window size changed (startup display mode) - they were blurry.</summary>
        private void RefreshTextsIfNeeded()
        {
            var size = new Vector2Int(Screen.width, Screen.height);
            if (size != _lastScreen)
            {
                _lastScreen = size;
                _refreshAt = Time.unscaledTime + 0.1f;
            }
            float t = Time.unscaledTime;
            if ((_refreshAt > 0f && t >= _refreshAt) || (_refreshAt2 > 0f && t >= _refreshAt2))
            {
                if (t >= _refreshAt) _refreshAt = -1f;
                if (t >= _refreshAt2) _refreshAt2 = -1f;
                if (_canvas != null) UiKit.RefreshTexts(_canvas.transform);
            }
        }

        private void Update()
        {
            RefreshTextsIfNeeded();
            // Slow, small sway around the 3/4 pose (no full spin: shadows stay calm).
            if (previewRoot != null && _preview != null)
                previewRoot.rotation = Quaternion.Euler(0f, _previewYaw + 7f * Mathf.Sin(Time.time * 0.35f), 0f);

            if (_browser.gameObject.activeSelf)
            {
                if (Time.unscaledTime >= _nextSteamSearch)
                {
                    _nextSteamSearch = Time.unscaledTime + 4f;
                    SearchSteam();
                }
                LanDiscovery.Tick();
                if (Time.unscaledTime >= _nextListRefresh)
                {
                    _nextListRefresh = Time.unscaledTime + 0.5f;
                    RebuildList();
                }
            }

            if (_steamStatus != null && Time.frameCount % 60 == 0) _steamStatus.text = SteamStatusText();
            if (_connecting.gameObject.activeSelf)
            {
                var nm = NetworkManager.Singleton;
                float t = Time.unscaledTime - _connectStarted;
                if (nm == null || !nm.IsListening)
                {
                    if (t > 0.5f && !NetSession.Instance.SteamBusy)
                    {
                        _connecting.gameObject.SetActive(false);
                        if (!string.IsNullOrEmpty(NetSession.LastMessage)) _message.text = NetSession.LastMessage;
                    }
                }
                else if (t > (NetSession.Instance.UsingSteam ? 25f : 12f) && !nm.IsConnectedClient)
                {
                    NetSession.Instance.Leave("Serwer nie odpowiada.");
                    _connecting.gameObject.SetActive(false);
                    _message.text = NetSession.LastMessage;
                }
            }
        }

        private readonly List<string> _shownKeys = new();

        private void RebuildList()
        {
            var servers = LanDiscovery.Servers.OrderBy(s => s.Name).ToList();
            var keys = servers.Select(s => $"{s.Address}:{s.Port}|{s.Players}|{s.InLobby}|{s.Name}").ToList();
            keys.AddRange(_steamLobbies.Select(l => $"steam{l.Id}|{l.Members}|{l.InLobby}|{l.Name}"));
            keys.Add(_steamSearching ? "searching" : "idle");
            if (keys.SequenceEqual(_shownKeys)) return;
            _shownKeys.Clear();
            _shownKeys.AddRange(keys);

            for (int i = _listRoot.childCount - 1; i >= 0; i--) Destroy(_listRoot.GetChild(i).gameObject);
            int total = servers.Count + _steamLobbies.Count;
            _serverList.text = total == 0 ? (SteamService.Ready ? "Szukam serwerów (Steam + LAN)..." : "Szukam serwerów w sieci LAN...") : $"Znalezione: {total}";
            int shown = 0;
            foreach (var l in _steamLobbies)
            {
                if (shown >= 5) break;
                shown++;
                string state = l.InLobby ? "lobby" : "gra trwa";
                string friend = l.Friend ? "  [znajomy]" : "";
                string ver = l.Version != Application.version ? "  (inna wersja)" : "";
                var lobby = l;
                var b = UiKit.Button("SteamLobby", _listRoot, $"STEAM   {(string.IsNullOrEmpty(l.Name) ? "?" : l.Name)}{friend}    {l.Members}/{l.Max}    {state}{ver}", () => JoinSteam(lobby.Id, lobby.Name), 24);
                b.interactable = l.InLobby && l.Version == Application.version;
                b.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
                b.Height(58);
            }
            foreach (var s in servers.Take(5 - shown))
            {
                string state = s.InLobby ? "lobby" : "gra trwa";
                var srv = s;
                var b = UiKit.Button("Server", _listRoot, $"LAN   {s.Name}    {s.Players} graczy    {state}    {s.Address}", () => Join($"{srv.Address}:{srv.Port}"), 24);
                b.interactable = s.InLobby;
                b.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
                b.Height(58);
            }
        }

        private void OnDestroy()
        {
            LanDiscovery.StopSearch();
            // Leaving the menu (host / join) while the preview runs the chainsaw: stop its engine loop.
            if (_menuLoop != null)
            {
                Debug.Log("[MainMenu] stopping preview chainsaw loop");
                GameAudio.StopLoop(_menuLoop, 0.1f);
            }
            _menuLoop = null;
        }
    }
}
