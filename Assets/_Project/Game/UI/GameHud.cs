using System.Collections;
using System.Collections.Generic;
using System.Text;
using HideAndSeek.Audio;
using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using HideAndSeek.Game.UI;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HideAndSeek.Game
{
    /// <summary>
    /// In-session HUD (persistent): timer, player list, minimap, interaction prompts, blind / downed overlays,
    /// banners, toasts, Esc menu, lobby round-settings panel and spectator camera.
    /// </summary>
    public class GameHud : MonoBehaviour
    {
        private static GameHud _instance;

        private Canvas _canvas;
        private Text _timer, _phase, _players, _lobbyInfo, _ammo, _status, _spectate, _blindText, _downText;
        private Image _blind, _vignette, _hurt, _crosshair;
        private RectTransform _prompt;
        private Text _promptText;
        private Image _promptFill;
        private RectTransform _bannerRoot;
        private Text _bannerTitle, _bannerSub;
        private CanvasGroup _bannerGroup;
        private float _bannerUntil, _bannerLen;
        private Text _toast;
        private float _toastUntil;
        private RectTransform _escMenu, _settingsPanel;
        private Minimap _minimap;
        private SpectatorCamera _spectator;
        private float _hurtLevel;

        public static void EnsureExists()
        {
            if (_instance != null) return;
            var go = new GameObject("[GameHud]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<GameHud>();
        }

        private void Awake()
        {
            _canvas = UiKit.CreateCanvas("HudCanvas", 10, transform);
            Build();
        }

        // ================================================================ build

        private void Build()
        {
            var root = _canvas.transform;

            _blind = UiKit.Image("Blind", root, Color.black);
            _blind.rectTransform.Stretch();
            _blindText = UiKit.Label("BlindText", _blind.transform, "", 40, TextAnchor.MiddleCenter);
            _blindText.rectTransform.Stretch();
            _blind.gameObject.SetActive(false);

            _vignette = UiKit.Image("DownedVignette", root, new Color(0.5f, 0f, 0f, 0.45f));
            _vignette.rectTransform.Stretch();
            _vignette.sprite = VignetteSprite();
            _downText = UiKit.Label("DownText", _vignette.transform, "", 34, TextAnchor.LowerCenter, new Color(1f, 0.75f, 0.7f));
            _downText.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 170), new Vector2(0, 120));
            _downText.AddOutline();
            _vignette.gameObject.SetActive(false);

            _hurt = UiKit.Image("Hurt", root, new Color(0.7f, 0f, 0f, 0f));
            _hurt.rectTransform.Stretch();
            _hurt.sprite = VignetteSprite();

            _crosshair = UiKit.Image("Crosshair", root, new Color(1f, 1f, 1f, 0.55f));
            _crosshair.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4, 4));

            _timer = UiKit.Label("Timer", root, "", 52, TextAnchor.UpperCenter, null, FontStyle.Bold);
            _timer.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -18), new Vector2(400, 64));
            _timer.AddOutline();
            _phase = UiKit.Label("Phase", root, "", 24, TextAnchor.UpperCenter, UiKit.TextDim);
            _phase.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -82), new Vector2(900, 36));
            _phase.AddOutline();

            var listBg = UiKit.Image("PlayerListBg", root, new Color(0f, 0f, 0f, 0.45f));
            listBg.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-20, -20), new Vector2(330, 420));
            _players = UiKit.Label("Players", listBg.transform, "", 24, TextAnchor.UpperLeft);
            _players.rectTransform.Stretch(14);
            _players.supportRichText = true;

            _lobbyInfo = UiKit.Label("LobbyInfo", root, "", 24, TextAnchor.UpperLeft);
            _lobbyInfo.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -20), new Vector2(760, 300));
            _lobbyInfo.supportRichText = true;
            _lobbyInfo.AddOutline();

            _ammo = UiKit.Label("Ammo", root, "", 40, TextAnchor.LowerRight, null, FontStyle.Bold);
            _ammo.rectTransform.Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-30, 26), new Vector2(500, 60));
            _ammo.AddOutline();
            _status = UiKit.Label("Status", root, "", 26, TextAnchor.LowerRight);
            _status.rectTransform.Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-30, 90), new Vector2(700, 120));
            _status.supportRichText = true;
            _status.AddOutline();

            _spectate = UiKit.Label("Spectate", root, "", 30, TextAnchor.LowerCenter);
            _spectate.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(900, 50));
            _spectate.AddOutline();

            // Prompt (world anchored)
            _prompt = UiKit.Rect("Prompt", root);
            _prompt.sizeDelta = new Vector2(64, 64);
            var keyBg = UiKit.Image("Key", _prompt, new Color(0.95f, 0.95f, 0.92f, 0.95f));
            keyBg.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(54, 54));
            _promptFill = UiKit.Image("Fill", keyBg.transform, new Color(0.35f, 0.85f, 0.4f, 0.85f));
            _promptFill.rectTransform.Stretch();
            _promptFill.type = Image.Type.Filled;
            _promptFill.fillMethod = Image.FillMethod.Vertical;
            _promptFill.fillAmount = 0f;
            var key = UiKit.Label("Letter", keyBg.transform, "F", 36, TextAnchor.MiddleCenter, Color.black, FontStyle.Bold);
            key.rectTransform.Stretch();
            key.name = "Letter";
            _promptText = UiKit.Label("Text", _prompt, "", 26, TextAnchor.MiddleLeft);
            _promptText.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 0.5f), new Vector2(8, 0), new Vector2(420, 50));
            _promptText.AddOutline();
            _prompt.gameObject.SetActive(false);

            // Banner
            _bannerRoot = UiKit.Rect("Banner", root);
            _bannerRoot.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 170), new Vector2(1400, 200));
            _bannerGroup = _bannerRoot.gameObject.AddComponent<CanvasGroup>();
            _bannerGroup.alpha = 0f;
            _bannerTitle = UiKit.Label("Title", _bannerRoot, "", 84, TextAnchor.MiddleCenter, null, FontStyle.Bold);
            _bannerTitle.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 110));
            _bannerTitle.AddOutline(3f);
            _bannerSub = UiKit.Label("Sub", _bannerRoot, "", 32, TextAnchor.MiddleCenter);
            _bannerSub.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 20), new Vector2(0, 60));
            _bannerSub.AddOutline();

            _toast = UiKit.Label("Toast", root, "", 30, TextAnchor.MiddleCenter);
            _toast.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 220), new Vector2(1300, 50));
            _toast.AddOutline();

            _minimap = gameObject.AddComponent<Minimap>();
            _minimap.Build(root);
        }

        private static Sprite _vignetteSprite;

        private static Sprite VignetteSprite()
        {
            if (_vignetteSprite != null) return _vignetteSprite;
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.414f;
                float a = Mathf.SmoothStep(0.35f, 1f, d);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            _vignetteSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
            return _vignetteSprite;
        }

        // ================================================================ static API

        public static void Toast(string text, float seconds)
        {
            if (_instance == null) return;
            _instance._toast.text = text;
            _instance._toastUntil = Time.unscaledTime + seconds;
        }

        public static void Banner(string title, string sub, float seconds, Color color)
        {
            if (_instance == null) return;
            _instance._bannerTitle.text = title;
            _instance._bannerTitle.color = color;
            _instance._bannerSub.text = sub;
            _instance._bannerUntil = Time.unscaledTime + seconds;
            _instance._bannerLen = seconds;
        }

        public static void Hurt(float amount)
        {
            if (_instance != null) _instance._hurtLevel = Mathf.Max(_instance._hurtLevel, amount);
        }

        /// <summary>Gun recoil on the local camera.</summary>
        public static void Kick()
        {
            var p = NetPlayer.Local;
            if (p == null) return;
            p.Motor.SetLook(p.Motor.LookYaw + Random.Range(-0.6f, 0.6f), p.Motor.LookPitch + 2.2f);
        }

        /// <summary>Victim's camera is forced to look at the attacker during the knife sequence.</summary>
        public static void BeginVictimView(NetPlayer attacker, float duration)
        {
            if (_instance != null) _instance.StartCoroutine(_instance.VictimView(attacker, duration));
        }

        private IEnumerator VictimView(NetPlayer attacker, float duration)
        {
            float t = 0f;
            var me = NetPlayer.Local;
            while (t < duration && me != null && attacker != null)
            {
                Vector3 to = attacker.EyePosition - me.EyePosition;
                float yaw = Quaternion.LookRotation(new Vector3(to.x, 0f, to.z).normalized + Vector3.forward * 1e-4f).eulerAngles.y;
                float pitch = Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg;
                float k = 1f - Mathf.Exp(-Time.deltaTime * 8f);
                me.Motor.SetLook(Mathf.LerpAngle(me.Motor.LookYaw, yaw, k), Mathf.Lerp(me.Motor.LookPitch, pitch - t * 6f, k));
                t += Time.deltaTime;
                yield return null;
            }
        }

        public static void OpenSettingsPanel()
        {
            if (_instance == null) return;
            _instance.ShowSettings(true);
        }

        // ================================================================ update

        private void Update()
        {
            var nm = NetworkManager.Singleton;
            bool inSession = nm != null && nm.IsListening && NetPlayer.Local != null;
            _canvas.enabled = inSession;
            if (!inSession)
            {
                if (_escMenu != null) CloseMenus();
                if (_spectator != null) { Destroy(_spectator.gameObject); _spectator = null; }
                return;
            }

            var me = NetPlayer.Local;
            var mm = MatchManager.Instance;
            var phase = mm != null ? mm.Phase.Value : MatchPhase.Lobby;
            bool inGame = phase == MatchPhase.Hiding || phase == MatchPhase.Seeking || phase == MatchPhase.Ended;

            // ---- Esc menu
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                if (_settingsPanel != null || _escMenu != null) CloseMenus();
                else ShowEscMenu();
            }

            // ---- timer / phase
            double remaining = mm != null ? mm.Remaining : 0;
            switch (phase)
            {
                case MatchPhase.Lobby:
                    _timer.text = "";
                    _phase.text = "";
                    break;
                case MatchPhase.Loading:
                    _timer.text = "";
                    _phase.text = "Ładowanie mapy...";
                    break;
                case MatchPhase.Hiding:
                    _timer.text = UiKit.FormatTime(remaining);
                    _timer.color = UiKit.HiderColor;
                    _phase.text = "Czas na schowanie się";
                    break;
                case MatchPhase.Seeking:
                    _timer.text = UiKit.FormatTime(remaining);
                    _timer.color = remaining < 120 ? new Color(1f, 0.4f, 0.3f) : UiKit.Text;
                    _phase.text = me.IsSeeker ? "Szukaj" : "Nie daj się złapać";
                    break;
                case MatchPhase.Ended:
                    _timer.text = "";
                    _phase.text = $"Powrót do lobby za {Mathf.CeilToInt((float)remaining)} s";
                    break;
            }

            // ---- player list
            _players.text = BuildPlayerList(phase);

            // ---- lobby info
            if (phase == MatchPhase.Lobby)
            {
                var sb = new StringBuilder();
                sb.Append("<b>LOBBY</b>\n");
                if (nm.IsServer)
                {
                    sb.Append("Twój serwer. Adresy do połączenia:\n");
                    foreach (var (ip, _) in LanDiscovery.LocalIPv4()) sb.Append("  ").Append(ip).Append('\n');
                }
                sb.Append("\nPodejdź do <color=#5f5>zielonego przycisku</color> i wciśnij <b>E</b>, aby zacząć.\n");
                sb.Append("Tablica na ścianie: ustawienia rundy (E).\nEsc - menu");
                _lobbyInfo.text = sb.ToString();
            }
            else _lobbyInfo.text = "";

            // ---- seeker blind during hiding
            bool blind = me.IsSeeker && (phase == MatchPhase.Hiding || phase == MatchPhase.Loading);
            _blind.gameObject.SetActive(blind || phase == MatchPhase.Loading);
            if (blind) _blindText.text = $"Jesteś szukającym.\nChowający się chowają...\n\n<size=72>{UiKit.FormatTime(remaining)}</size>";
            else if (phase == MatchPhase.Loading) _blindText.text = "Ładowanie...";
            _blindText.supportRichText = true;

            // ---- downed overlay
            bool downed = me.Life.Value == LifeState.Downed;
            _vignette.gameObject.SetActive(downed);
            if (downed)
            {
                double left = me.DownedUntil.Value - (mm != null ? mm.Now : 0);
                float pulse = 0.35f + 0.15f * Mathf.Sin(Time.time * 6f);
                _vignette.color = new Color(0.55f, 0f, 0f, pulse + 0.35f * (1f - (float)(left / Tuning.BleedOutTime)));
                _downText.text = $"Wykrwawiasz się: {UiKit.FormatTime(left)}\nCzołgaj się do kryjówki. Inny chowający może cię uratować.";
            }

            _hurtLevel = Mathf.MoveTowards(_hurtLevel, 0f, Time.deltaTime * 1.6f);
            _hurt.color = new Color(0.75f, 0f, 0f, _hurtLevel * 0.7f);

            // ---- ammo / status
            if (me.HasGlock.Value && me.Held.Value == HeldItem.Glock)
            {
                bool reloading = mm != null && mm.IsServer && mm.IsReloading(me);
                _ammo.text = $"{me.Ammo.Value} / {me.SpareMags.Value * Tuning.MagSize}" + (me.Ammo.Value == 0 ? "   R - przeładuj" : "");
                _ammo.color = me.Ammo.Value == 0 ? UiKit.Danger : UiKit.Text;
            }
            else _ammo.text = me.HasGlock.Value ? "Q - glock" : "";

            var st = new StringBuilder();
            if (inGame && me.IsHider)
            {
                if (me.Wounded.Value) st.Append("<color=#f66>Ranny</color> - kolejne złapanie = śmierć\n");
                if (me.HasMedkit.Value) st.Append(me.Wounded.Value ? "Apteczka: przytrzymaj G\n" : "Apteczka (H na rannym, G na sobie)\n");
            }
            if (inGame && me.IsSeeker && phase == MatchPhase.Seeking)
                st.Append("Kółko myszy - skupienie latarki\n");
            _status.text = st.ToString();

            // ---- prompt
            UpdatePrompt();

            // ---- crosshair
            _crosshair.enabled = inGame && me.Life.Value == LifeState.Alive && !blind;

            // ---- spectator
            UpdateSpectator(me, phase);

            // ---- banner / toast
            float bl = _bannerUntil - Time.unscaledTime;
            _bannerGroup.alpha = bl <= 0 ? 0f : Mathf.Clamp01(bl / 0.6f) * Mathf.Clamp01((_bannerLen - bl) / 0.25f + 0.2f);
            _toast.enabled = Time.unscaledTime < _toastUntil;

            // ---- minimap
            _minimap.SetVisible(inGame && SceneManager.GetActiveScene().name == NetSession.GameScene && !blind);
        }

        private string BuildPlayerList(MatchPhase phase)
        {
            var sb = new StringBuilder();
            sb.Append("<b>GRACZE</b>  (").Append(NetPlayer.All.Count).Append(")\n\n");
            var list = new List<NetPlayer>(NetPlayer.All);
            list.Sort((a, b) => a.Role.Value == b.Role.Value ? string.CompareOrdinal(a.Nick, b.Nick) : b.Role.Value.CompareTo(a.Role.Value));
            foreach (var p in list)
            {
                if (p == null) continue;
                string nick = p.Nick + (p.IsOwner ? " (ty)" : "");
                if (phase == MatchPhase.Lobby || phase == MatchPhase.Loading || p.Role.Value == PlayerRole.None)
                {
                    sb.Append(p.OwnerClientId == NetworkManager.ServerClientId ? "<color=#ffd75e>[host]</color> " : "  ").Append(nick).Append('\n');
                    continue;
                }
                if (p.IsSeeker)
                {
                    sb.Append("<color=#ff5544>SZUKA</color>  ").Append(nick).Append('\n');
                    continue;
                }
                string state = p.Life.Value switch
                {
                    LifeState.Alive => p.Wounded.Value ? "<color=#ffb070>ranny</color>" : "<color=#77ccff>chowa się</color>",
                    LifeState.Downed => "<color=#ff7a7a>złapany</color>",
                    _ => "<color=#888888>nie żyje</color>",
                };
                sb.Append(state).Append("  ").Append(nick).Append('\n');
            }
            return sb.ToString();
        }

        private void UpdatePrompt()
        {
            var lpc = LocalPlayerController.Instance;
            var cam = Camera.main;
            if (lpc == null || cam == null || lpc.Prompt == PromptKind.None)
            {
                _prompt.gameObject.SetActive(false);
                return;
            }
            Vector3 sp = cam.WorldToScreenPoint(lpc.PromptWorld);
            if (sp.z <= 0f)
            {
                _prompt.gameObject.SetActive(false);
                return;
            }
            _prompt.gameObject.SetActive(true);
            var canvasRt = (RectTransform)_canvas.transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, sp, null, out Vector2 local);
            _prompt.anchoredPosition = local;
            _prompt.anchorMin = _prompt.anchorMax = new Vector2(0.5f, 0.5f);
            string txt = lpc.PromptText;
            string letter = txt.Length > 0 ? txt.Substring(0, 1) : "";
            var letterText = _prompt.Find("Key/Letter").GetComponent<Text>();
            letterText.text = letter;
            _promptText.text = txt.Length > 1 ? txt.Substring(1).Trim() : "";
            _promptFill.fillAmount = lpc.HoldProgress;
            bool far = txt.Contains("bliżej");
            letterText.color = far ? new Color(0f, 0f, 0f, 0.4f) : Color.black;
            _prompt.localScale = Vector3.one * (lpc.Prompt == PromptKind.Knife && !far ? 1f + 0.08f * Mathf.Sin(Time.time * 10f) : 1f);
        }

        private void UpdateSpectator(NetPlayer me, MatchPhase phase)
        {
            bool spectate = me.Life.Value == LifeState.Dead && (phase == MatchPhase.Seeking || phase == MatchPhase.Ended);
            if (spectate && _spectator == null)
            {
                var go = new GameObject("SpectatorCamera");
                _spectator = go.AddComponent<SpectatorCamera>();
                _spectator.Begin(me);
            }
            else if (!spectate && _spectator != null)
            {
                _spectator.End();
                Destroy(_spectator.gameObject);
                _spectator = null;
            }
            _spectate.text = _spectator != null && _spectator.Target != null
                ? $"Oglądasz: {_spectator.Target.Nick}    (LPM / PPM - zmiana)"
                : "";
        }

        // ================================================================ menus

        private void CloseMenus()
        {
            if (_escMenu != null) Destroy(_escMenu.gameObject);
            if (_settingsPanel != null) Destroy(_settingsPanel.gameObject);
            _escMenu = null;
            _settingsPanel = null;
            LocalPlayerController.MenuOpen = false;
            var me = NetPlayer.Local;
            if (me != null && me.InputReader != null) me.InputReader.SetCursorLocked(true);
        }

        private RectTransform MenuPanel(string name, Vector2 size)
        {
            LocalPlayerController.MenuOpen = true;
            var me = NetPlayer.Local;
            if (me != null && me.InputReader != null) me.InputReader.SetCursorLocked(false);
            var dim = UiKit.Image(name, _canvas.transform, new Color(0, 0, 0, 0.55f));
            dim.rectTransform.Stretch();
            dim.raycastTarget = true;
            var panel = UiKit.Image("Panel", dim.transform, UiKit.Panel);
            panel.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            UiKit.VStack(panel.rectTransform, 14, 30);
            return dim.rectTransform;
        }

        private void ShowEscMenu()
        {
            _escMenu = MenuPanel("EscMenu", new Vector2(560, 560));
            var panel = _escMenu.Find("Panel");
            UiKit.Label("Title", panel, "MENU", 44, TextAnchor.MiddleCenter, null, FontStyle.Bold).Height(64);
            UiKit.Button("Resume", panel, "Wróć do gry", CloseMenus).Height(70);
            UiKit.Label("SensL", panel, "Czułość myszy", 24).Height(30);
            UiKit.Slider("Sens", panel, 0.02f, 0.4f, LocalProfile.MouseSensitivity, false, v =>
            {
                LocalProfile.MouseSensitivity = v;
                if (NetPlayer.Local != null) NetPlayer.Local.InputReader.MouseSensitivity = v;
            }).Height(36);
            UiKit.Label("VolL", panel, "Głośność", 24).Height(30);
            UiKit.Slider("Vol", panel, 0f, 1f, LocalProfile.MasterVolume, false, v => LocalProfile.MasterVolume = v).Height(36);
            string leave = NetworkManager.Singleton.IsServer ? "Zamknij serwer" : "Rozłącz";
            UiKit.Button("Leave", panel, leave, () =>
            {
                CloseMenus();
                NetSession.Instance.Leave();
            }, 30, new Color(0.35f, 0.1f, 0.1f)).Height(70);
            UiKit.Button("Quit", panel, "Wyjdź z gry", Application.Quit).Height(60);
        }

        private void ShowSettings(bool fromBoard)
        {
            if (_settingsPanel != null) return;
            var mm = MatchManager.Instance;
            if (mm == null) return;
            bool host = NetworkManager.Singleton.IsServer;
            _settingsPanel = MenuPanel("RoundSettings", new Vector2(760, 860));
            var panel = _settingsPanel.Find("Panel");
            var s = mm.Settings.Value;
            UiKit.Label("Title", panel, "USTAWIENIA RUNDY", 40, TextAnchor.MiddleCenter, null, FontStyle.Bold).Height(56);
            if (!host) UiKit.Label("Info", panel, "Tylko host może zmieniać ustawienia.", 24, TextAnchor.MiddleCenter, UiKit.TextDim).Height(32);

            Text Row(string label)
            {
                var t = UiKit.Label("L", panel, label, 26);
                t.Height(32);
                return t;
            }

            void Push()
            {
                if (host) mm.SetSettingsRpc(s);
            }

            var hideL = Row("");
            var hide = UiKit.Slider("Hide", panel, 10, 180, s.HideTime, true, v => { s.HideTime = v; hideL.text = $"Czas na schowanie: {UiKit.FormatTime(v)}"; Push(); });
            hide.Height(34);
            hideL.text = $"Czas na schowanie: {UiKit.FormatTime(s.HideTime)}";

            var seekL = Row("");
            var seek = UiKit.Slider("Seek", panel, 2, 30, s.SeekTime / 60f, true, v =>
            {
                s.SeekTime = v * 60f;
                s.HintStart = Mathf.Min(s.HintStart, s.SeekTime);
                seekL.text = $"Czas szukania: {v:0} min";
                Push();
            });
            seek.Height(34);
            seekL.text = $"Czas szukania: {s.SeekTime / 60f:0} min";

            var hsL = Row("");
            var hs = UiKit.Slider("HintStart", panel, 0, 30, s.HintStart / 60f, true, v =>
            {
                s.HintStart = Mathf.Min(v * 60f, s.SeekTime);
                hsL.text = $"Podpowiedzi od: {UiKit.FormatTime(s.HintStart)} szukania";
                Push();
            });
            hs.Height(34);
            hsL.text = $"Podpowiedzi od: {UiKit.FormatTime(s.HintStart)} szukania";

            var hiL = Row("");
            var hi = UiKit.Slider("HintInterval", panel, 15, 180, s.HintInterval, true, v =>
            {
                s.HintInterval = v;
                hiL.text = $"Podpowiedź co: {UiKit.FormatTime(v)}";
                Push();
            });
            hi.Height(34);
            hiL.text = $"Podpowiedź co: {UiKit.FormatTime(s.HintInterval)}";

            string ModeName(byte m) => m == 3 ? "dźwięk + minimapa" : m == 1 ? "tylko dźwięk" : m == 2 ? "tylko minimapa" : "wyłączone";
            Button modeB = null;
            modeB = UiKit.Button("Mode", panel, "Tryb podpowiedzi: " + ModeName(s.HintMode), () =>
            {
                s.HintMode = (byte)((s.HintMode + 1) % 4);
                modeB.GetComponentInChildren<Text>().text = "Tryb podpowiedzi: " + ModeName(s.HintMode);
                Push();
            }, 26);
            modeB.Height(56);

            Button glockB = null;
            glockB = UiKit.Button("Glock", panel, "Glock na ostatnie 2 min: " + (s.Glock ? "tak" : "nie"), () =>
            {
                s.Glock = !s.Glock;
                glockB.GetComponentInChildren<Text>().text = "Glock na ostatnie 2 min: " + (s.Glock ? "tak" : "nie");
                Push();
            }, 26);
            glockB.Height(56);

            var medL = Row("");
            var med = UiKit.Slider("Medkits", panel, 0, 8, s.Medkits, true, v => { s.Medkits = (byte)v; medL.text = $"Apteczki na mapie: {v:0}"; Push(); });
            med.Height(34);
            medL.text = $"Apteczki na mapie: {s.Medkits}";

            foreach (var sl in new[] { hide, seek, hs, hi, med }) sl.interactable = host;
            modeB.interactable = host;
            glockB.interactable = host;

            UiKit.Button("Close", panel, "Zamknij", CloseMenus).Height(64);
        }
    }
}
