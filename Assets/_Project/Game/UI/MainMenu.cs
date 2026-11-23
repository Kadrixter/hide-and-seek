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
    public partial class MainMenu : MonoBehaviour
    {
        [SerializeField] private Transform previewRoot;

        private Canvas _canvas;
        private RectTransform _main, _character, _browser, _options, _audio, _voicePage, _connecting, _column;
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
            BuildChrome(root);          // MainMenu.Pages.cs: vignette, shade, logo, footer, column
            UpdateBanner.Create(root);

            _main = BuildMain(_column);
            _character = BuildCharacter(_column);
            _browser = BuildBrowser(_column);
            _options = BuildOptions(_column);
            _audio = BuildAudio(_column);
            _voicePage = BuildVoice(_column);
            _connecting = BuildConnecting(root);
            Show(_main);
            MenuWeaponPicker.Create(root, this); // bottom right: weapon of the menu character
            UiTextButtonFx.Hovered += OnButtonHovered;

            BuildPreview();
            if (OutroOnStart)
            {
                // "Wyjdź z gry" from the Esc menu: the session is gone, say goodbye here and quit
                OutroOnStart = false;
                StartCoroutine(Outro(true));
                return;
            }
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-menushots") >= 0) StartCoroutine(MenuShots()); // MainMenu.Tests.cs
            else if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-testmenu") >= 0) StartCoroutine(TestPages());
        }

        /// <summary>Test switch -testmenu: walks through the pages (for screenshots).</summary>
        private System.Collections.IEnumerator TestPages()
        {
            foreach (var p in new[] { _main, _character, _options, _audio, _voicePage, _main })
            {
                Show(p);
                yield return new WaitForSeconds(2.2f);
            }
        }

        private RectTransform Page(Transform parent, string name)
        {
            var rt = UiKit.Rect(name, parent);
            rt.Stretch();
            UiKit.VStack(rt, 6);
            return rt;
        }

        private void Show(RectTransform page)
        {
            foreach (var p in new[] { _main, _character, _browser, _options, _audio, _voicePage }) p.gameObject.SetActive(p == page);
            if (page == _browser) LanDiscovery.StartSearch();
            else LanDiscovery.StopSearch();
            OnButtonHovered(null);
            if (_footer != null) _footer.gameObject.SetActive(page != _audio && page != _voicePage);
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
            UpdateDescription();
            // Slow, small sway around the 3/4 pose (no full spin: shadows stay calm).
            if (previewRoot != null && _preview != null)
                previewRoot.rotation = Quaternion.Euler(0f, _previewYaw + (OutroPlaying ? 0f : 7f * Mathf.Sin(Time.time * 0.35f)), 0f);

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
                var b = UiKit.BoxButton("SteamLobby", _listRoot, $"STEAM   {(string.IsNullOrEmpty(l.Name) ? "?" : l.Name)}{friend}    {l.Members}/{l.Max}    {state}{ver}", () => JoinSteam(lobby.Id, lobby.Name), 24);
                b.interactable = l.InLobby && l.Version == Application.version;
                b.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
                b.Height(58);
            }
            foreach (var s in servers.Take(5 - shown))
            {
                string state = s.InLobby ? "lobby" : "gra trwa";
                var srv = s;
                var b = UiKit.BoxButton("Server", _listRoot, $"LAN   {s.Name}    {s.Players} graczy    {state}    {s.Address}", () => Join($"{srv.Address}:{srv.Port}"), 24);
                b.interactable = s.InLobby;
                b.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
                b.Height(58);
            }
        }

        private void OnDestroy()
        {
            UiTextButtonFx.Hovered -= OnButtonHovered;
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
