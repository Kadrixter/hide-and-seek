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
        private RectTransform _main, _browser, _options, _connecting;
        private Text _message, _skinLabel, _serverList, _connectingText;
        private InputField _nick, _ip;
        private RectTransform _listRoot;
        private BlockyCharacter _preview;
        private float _nextListRefresh;
        private float _connectStarted;

        private void Start()
        {
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
            var sub = UiKit.Label("Sub", side.transform, "chowany w ciemnościach", 30, TextAnchor.UpperLeft, UiKit.TextDim);
            sub.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(64, -150), new Vector2(-120, 40));

            _message = UiKit.Label("Message", side.transform, NetSession.LastMessage ?? "", 26, TextAnchor.LowerLeft, new Color(1f, 0.6f, 0.5f));
            _message.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(60, 40), new Vector2(-120, 80));
            NetSession.LastMessage = null;

            _main = BuildMain(side.transform);
            _browser = BuildBrowser(side.transform);
            _options = BuildOptions(side.transform);
            _connecting = BuildConnecting(root);
            Show(_main);

            BuildPreview();
        }

        private RectTransform Page(Transform parent, string name)
        {
            var rt = UiKit.Rect(name, parent);
            rt.Place(new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(-120, -420));
            UiKit.VStack(rt, 14);
            return rt;
        }

        private void Show(RectTransform page)
        {
            foreach (var p in new[] { _main, _browser, _options }) p.gameObject.SetActive(p == page);
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

            UiKit.Rect("Gap", page).Height(16);
            UiKit.Button("Host", page, "Stwórz serwer", Host, 32, new Color(0.13f, 0.32f, 0.16f)).Height(76);
            UiKit.Button("Find", page, "Szukaj serwerów", () => Show(_browser), 32).Height(76);
            UiKit.Button("Options", page, "Ustawienia", () => Show(_options), 30).Height(66);
            UiKit.Button("Quit", page, "Wyjdź", Application.Quit, 30).Height(66);
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
            UiKit.Label("SensL", page, "Czułość myszy", 24).Height(30);
            UiKit.Slider("Sens", page, 0.02f, 0.4f, LocalProfile.MouseSensitivity, false, v => LocalProfile.MouseSensitivity = v).Height(36);
            UiKit.Label("MasterL", page, "Głośność ogólna", 24).Height(30);
            UiKit.Slider("Master", page, 0f, 1f, LocalProfile.MasterVolume, false, v => LocalProfile.MasterVolume = v).Height(36);
            UiKit.Label("SfxL", page, "Efekty", 24).Height(30);
            UiKit.Slider("Sfx", page, 0f, 1f, LocalProfile.SfxVolume, false, v => LocalProfile.SfxVolume = v).Height(36);
            UiKit.Label("MusicL", page, "Muzyka i otoczenie", 24).Height(30);
            UiKit.Slider("Music", page, 0f, 1f, LocalProfile.MusicVolume, false, v => LocalProfile.MusicVolume = v).Height(36);
            UiKit.Rect("Gap", page).Height(20);
            UiKit.Button("Back", page, "Wróć", () => { PlayerPrefs.Save(); Show(_main); }, 28).Height(60);
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
            PlayerPrefs.Save();
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

        private void ChangeSkin(int d)
        {
            LocalProfile.Skin = (LocalProfile.Skin + d + 5) % 5;
            UpdateSkinLabel();
            RefreshPreview();
        }

        private void UpdateSkinLabel() => _skinLabel.text = $"Skin {LocalProfile.Skin + 1} / 5";

        // ================================================================ preview

        private void BuildPreview()
        {
            if (previewRoot == null) return;
            _preview = BlockyCharacterFactory.Create(previewRoot, LocalProfile.Skin, LocalProfile.Nick);
            _preview.SetFirstPersonMode(false);
        }

        private void RefreshPreview()
        {
            if (_preview != null) _preview.SetSkin(LocalProfile.Skin, LocalProfile.Nick);
        }

        private void Update()
        {
            if (previewRoot != null) previewRoot.Rotate(0f, 18f * Time.deltaTime, 0f);

            if (_browser.gameObject.activeSelf)
            {
                LanDiscovery.Tick();
                if (Time.unscaledTime >= _nextListRefresh)
                {
                    _nextListRefresh = Time.unscaledTime + 0.5f;
                    RebuildList();
                }
            }

            if (_connecting.gameObject.activeSelf)
            {
                var nm = NetworkManager.Singleton;
                float t = Time.unscaledTime - _connectStarted;
                if (nm == null || !nm.IsListening)
                {
                    if (t > 0.5f)
                    {
                        _connecting.gameObject.SetActive(false);
                        if (!string.IsNullOrEmpty(NetSession.LastMessage)) _message.text = NetSession.LastMessage;
                    }
                }
                else if (t > 12f && !nm.IsConnectedClient)
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
            if (keys.SequenceEqual(_shownKeys)) return;
            _shownKeys.Clear();
            _shownKeys.AddRange(keys);

            for (int i = _listRoot.childCount - 1; i >= 0; i--) Destroy(_listRoot.GetChild(i).gameObject);
            _serverList.text = servers.Count == 0 ? "Szukam serwerów..." : $"Znalezione: {servers.Count}";
            foreach (var s in servers.Take(5))
            {
                string state = s.InLobby ? "lobby" : "gra trwa";
                var srv = s;
                var b = UiKit.Button("Server", _listRoot, $"{s.Name}    {s.Players} graczy    {state}    {s.Address}", () => Join($"{srv.Address}:{srv.Port}"), 24);
                b.interactable = s.InLobby;
                b.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
                b.Height(58);
            }
        }

        private void OnDestroy() => LanDiscovery.StopSearch();
    }
}
