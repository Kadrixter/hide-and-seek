using System.Collections;
using System.Collections.Generic;
using System.Text;
using HideAndSeek.Audio;
using HideAndSeek.Characters;
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
        private Image _blind, _crosshair;
        private RawImage _vignette, _hurt;
        private Image _dust;
        private Text _dustText;
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
        private Text _voice;
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

            _vignette = VignetteImage("DownedVignette", root, new Color(0.5f, 0f, 0f, 0.45f));
            _downText = UiKit.Label("DownText", _vignette.transform, "", 34, TextAnchor.LowerCenter, new Color(1f, 0.75f, 0.7f));
            _downText.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 170), new Vector2(0, 120));
            _downText.AddOutline();
            _vignette.gameObject.SetActive(false);

            _hurt = VignetteImage("Hurt", root, new Color(0.7f, 0f, 0f, 0f));

            // Dust in the seeker's eyes (hider's escape move): opaque tan cloud that fades out.
            _dust = UiKit.Image("DustBlind", root, new Color(0.74f, 0.66f, 0.5f, 0f));
            _dust.rectTransform.Stretch();
            _dust.raycastTarget = false;
            _dustText = UiKit.Label("DustText", _dust.transform, "", 34, TextAnchor.MiddleCenter, new Color(0.25f, 0.2f, 0.12f));
            _dustText.rectTransform.Stretch();
            _dust.gameObject.SetActive(false);

            _crosshair = UiKit.Image("Crosshair", root, new Color(1f, 1f, 1f, 0.55f));
            _crosshair.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4, 4));

            _timer = UiKit.Label("Timer", root, "", 52, TextAnchor.UpperCenter, null, FontStyle.Bold);
            _timer.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -18), new Vector2(400, 64));
            _timer.AddOutline();
            _phase = UiKit.Label("Phase", root, "", 24, TextAnchor.UpperCenter, UiKit.TextDim);
            _phase.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -82), new Vector2(900, 36));
            _phase.AddOutline();

            var listBg = UiKit.Image("PlayerListBg", root, new Color(0f, 0f, 0f, 0.45f));
            listBg.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-20, -20), new Vector2(430, 420));
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

            ReviveBars.Create(root);

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
            _toast.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 330), new Vector2(1300, 50));
            _toast.AddOutline();

            _minimap = gameObject.AddComponent<Minimap>();
            _minimap.Build(root);
        }

        private static RawImage VignetteImage(string name, Transform parent, Color color)
        {
            var rt = UiKit.Rect(name, parent);
            rt.Stretch();
            var img = rt.gameObject.AddComponent<RawImage>();
            img.texture = VignetteSprite().texture;
            img.color = color;
            img.raycastTarget = false;
            return img;
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
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 1f, d));
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            _vignetteSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
            return _vignetteSprite;
        }

        // ================================================================ static API

        /// <summary>Player currently watched in spectator mode (null when playing).</summary>
        public static NetPlayer Spectated => _instance != null && _instance._spectator != null ? _instance._spectator.Target : null;

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

        private Vector2Int _lastScreen;

        private void Update()
        {
            var scr = new Vector2Int(Screen.width, Screen.height);
            if (scr != _lastScreen)
            {
                _lastScreen = scr;
                if (_canvas != null) UiKit.RefreshTexts(_canvas.transform);
            }
            var nm = NetworkManager.Singleton;
            bool inSession = nm != null && nm.IsListening && NetPlayer.Local != null;
            _canvas.enabled = inSession && !KillCam.Active;
            if (!inSession)
            {
                if (_escMenu != null) CloseMenus();
                if (_spectator != null) { Destroy(_spectator.gameObject); _spectator = null; }
                return;
            }

            var me = NetPlayer.Local;
            var mm = MatchManager.Instance;
            var phase = mm != null ? mm.Phase.Value : MatchPhase.Lobby;
            if (TestEsc && !_testEscDone)
            {
                _testEscDone = true;
                StartCoroutine(TestEscPages());
            }
            bool inGame = phase == MatchPhase.Hiding || phase == MatchPhase.Seeking || phase == MatchPhase.Ended;

            // ---- Esc menu
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && !ConfirmDialog.ConsumesEscape)
            {
                if (Boombox.PanelOpen) Boombox.ClosePanel();
                else if (_settingsPanel != null || _escMenu != null) CloseMenus();
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
                case MatchPhase.Race:
                    _timer.text = "";
                    _phase.text = "";
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
                if (nm.IsServer && NetSession.Instance != null && NetSession.Instance.UsingSteam)
                {
                    sb.Append("Twój serwer <color=#6dff7a>Steam</color>. Znajomi: Esc > <b>Zaproś znajomych</b>\n");
                    sb.Append("albo dołączą przez listę znajomych Steam (Dołącz do gry).\n");
                }
                else if (nm.IsServer)
                {
                    sb.Append("Twój serwer LAN / UDP. Adresy do połączenia:\n");
                    foreach (var (ip, _) in LanDiscovery.LocalIPv4()) sb.Append("  ").Append(ip).Append('\n');
                }
                else if (NetSession.Instance != null && NetSession.Instance.UsingSteam)
                    sb.Append("Połączono przez <color=#6dff7a>Steam</color>.\n");
                int readyCount = 0, need = 0;
                foreach (var o in NetPlayer.All)
                {
                    if (o == null || o.OwnerClientId == NetworkManager.ServerClientId) continue;
                    need++;
                    if (o.Ready.Value) readyCount++;
                }
                if (nm.IsServer)
                    sb.Append($"\nGotowi: <b>{readyCount}/{need}</b> (gotowy = wziął latarkę z pudełka). Gdy wszyscy będą gotowi, pociągnij <color=#ff6a5a>dźwignię</color> (E).\n");
                else
                    sb.Append(me.Ready.Value
                        ? "\n<color=#6dff7a>Masz latarkę - jesteś gotowy.</color> E przy pudełku - odłóż. Czekaj, aż właściciel pokoju wystartuje.\n"
                        : "\nWeź <b>latarkę</b> z pudełka przy lustrze (E) - wtedy jesteś gotowy.\n");
                sb.Append("E na kanapie / pufie - usiądź. Tablica na ścianie - ustawienia rundy.\nEsc - menu");
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
                _vignette.color = new Color(0.55f, 0f, 0f, pulse + 0.35f * (1f - (float)(left / (mm != null ? mm.BleedOut : Tuning.BleedOutTime))));
                bool weak = LocalPlayerController.Instance != null && LocalPlayerController.Instance.CrawlExhausted;
                _downText.text = $"Wykrwawiasz się: {UiKit.FormatTime(left)}\n" + (weak
                    ? "Nie masz już sił się czołgać. Czekaj na pomoc."
                    : "Czołgaj się do kryjówki. Inny chowający może cię uratować.")
                    + (me.MedkitCount.Value > 0 ? "\n<color=#7dff8a>Masz apteczkę: przytrzymaj G, żeby się podnieść.</color>" : "");
                _downText.supportRichText = true;
            }

            _hurtLevel = Mathf.MoveTowards(_hurtLevel, 0f, Time.deltaTime * 1.6f);
            _hurt.color = new Color(0.75f, 0f, 0f, _hurtLevel * 0.7f);

            // ---- dust in the eyes
            double dustLeft = me.IsSeeker && mm != null ? me.BlindUntil.Value - mm.Now : 0d;
            bool dusted = dustLeft > 0d && me.Life.Value == LifeState.Alive;
            _dust.gameObject.SetActive(dusted);
            if (dusted)
            {
                float a = Mathf.Clamp01((float)dustLeft / 1.2f);
                float flick = 0.04f * Mathf.Sin(Time.time * 17f);
                _dust.color = new Color(0.74f + flick, 0.66f + flick, 0.5f, Mathf.Clamp01(a * 0.97f));
                _dustText.text = dustLeft > 1.2d ? $"Kurz w oczach!\n<size=22>{dustLeft:0.0} s</size>" : "";
                _dustText.supportRichText = true;
            }

            // ---- ammo / status
            if (me.HasGlock.Value && me.Held.Value == HeldItem.Glock)
            {
                bool reloading = mm != null && mm.IsServer && mm.IsReloading(me);
                _ammo.text = $"{me.Ammo.Value} / {me.SpareMags.Value * Tuning.MagSize}" + (me.Ammo.Value == 0 ? "   R - przeładuj" : "");
                _ammo.color = me.Ammo.Value == 0 ? UiKit.Danger : UiKit.Text;
            }
            else _ammo.text = me.HasGlock.Value && Hotbar.SlotOf(me, HotbarItem.Glock) >= 0 ? $"{Hotbar.SlotOf(me, HotbarItem.Glock) + 1} - glock" : "";

            var st = new StringBuilder();
            if (inGame && me.IsHider)
            {
                if (me.Wounded.Value) st.Append("<color=#f66>Ranny</color> - kolejne złapanie = śmierć\n");
                else if ((me.Severed.Value & Limb.Legs) != 0) st.Append(me.FreeStump == 0 ? "<color=#ffb070>Noga z pręta</color> - kolejny atak piłą = śmierć\n"
                    : "<color=#f66>Odcięta noga</color> - skaczesz powoli, kolejny atak piłą = śmierć\n" + (me.RodCount.Value > 0 ? $"<color=#cfd8e0>Masz pręt: {Hotbar.SlotOf(me, HotbarItem.Rod) + 1}, potem LPM - zrób z niego nogę</color>\n" : ""));
                else if (me.Limping.Value) st.Append("<color=#ffb070>Zraniony</color> - utykasz, kolejny atak = wykrwawianie\n");
                if (phase == MatchPhase.Seeking && me.Life.Value == LifeState.Alive && mm != null)
                {
                    double dl = me.DustReadyAt.Value - mm.Now;
                    st.Append(dl > 0d ? $"Kurz w oczy (X): za {UiKit.FormatTime(dl)}\n" : "<color=#e8d6a0>X - rzuć kurz w oczy szukającemu</color>\n");
                }
                if (me.MedkitCount.Value > 0) st.Append($"Apteczki: {me.MedkitCount.Value}" + (me.Wounded.Value || me.Limping.Value ?"  (przytrzymaj G)\n" : "  (H na rannym, G na sobie)\n"));
                if (me.SyringeCount.Value > 0) st.Append($"<color=#7fd7ff>Adrenalina: {me.SyringeCount.Value}</color>  (H przy martwym - wskrzesza)\n");
                int rcSlot = mm != null ? mm.RcSlotOf(me) : -1;
                if (rcSlot >= 0)
                {
                    var rcs = mm.RcSlot(rcSlot);
                    string rcWhat = rcs.Mode == RcMode.Carried ? "6 - postaw i steruj"
                        : rcs.Mode == RcMode.Parked ? "na podłodze: 6 - steruj, E przy nim - podnieś"
                        : "sterujesz (6 - wróć do postaci)";
                    st.Append($"<color=#ffd34d>Samochodzik RC</color>  ({rcWhat})\n");
                }
            }
            if (phase == MatchPhase.Seeking && me.IsHider && me.Life.Value != LifeState.Dead && mm != null &&
                mm.Settings.Value.HintMode != 0 && me.NextHintAt.Value > 0)
            {
                double left = me.NextHintAt.Value - mm.Now;
                bool soundHint = (mm.Settings.Value.HintMode & 1) != 0;
                string what = soundHint ? "Wydasz dźwięk za" : "Pokażesz się na mapie za";
                st.Append(left <= 5 ? $"<color=#ff7060>{what} {UiKit.FormatTime(left)}</color>\n" : $"{what} {UiKit.FormatTime(left)}\n");
            }
            if (inGame && me.IsSeeker && phase == MatchPhase.Seeking)
            {
                st.Append("T - latarka, kółko myszy - skupienie światła\n");
                if (me.Held.Value == HeldItem.Glock) st.Append("PPM - celowanie (mniejszy rozrzut)\n");
                if (mm != null && me.Held.Value != HeldItem.Glock)
                {
                    double al = me.AttackReadyAt.Value - mm.Now;
                    if (al > 0d) st.Append($"<color=#ffb070>Atak gotowy za {al:0.0} s</color>\n");
                }
            }
            _status.text = st.ToString();

            // ---- voice indicator
            var vc = Voice.VoiceChat.Instance;
            if (_voice == null)
            {
                _voice = UiKit.Label("VoiceInd", _canvas.transform, "", 24, TextAnchor.LowerLeft, new Color(0.5f, 1f, 0.55f));
                _voice.rectTransform.Place(Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(24, 330), new Vector2(500, 34));
                _voice.AddOutline();
            }
            _voice.text = vc != null && vc.Transmitting ? MeterText(vc.MicLevel)
                : (vc != null && LocalProfile.VoiceEnabled && !LocalProfile.VoiceOpenMic && phase == MatchPhase.Lobby ? "<color=#9a9a9a>V - mów</color>" : "");
            _voice.supportRichText = true;

            // ---- prompt
            UpdatePrompt();
            UpdateNameTags(me, phase);

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
                    if (p.OwnerClientId == NetworkManager.ServerClientId)
                        sb.Append("<color=#ffd75e>WŁAŚCICIEL</color>  ").Append(nick).Append('\n');
                    else if (phase == MatchPhase.Lobby)
                        sb.Append(p.Ready.Value ? "<color=#6dff7a>GOTOWY</color>  " : "<color=#9a9a9a>czeka</color>  ").Append(nick).Append('\n');
                    else
                        sb.Append("  ").Append(nick).Append('\n');
                    continue;
                }
                if (p.IsSeeker)
                {
                    sb.Append("<color=#ff5544>SZUKA</color>  ").Append(nick).Append('\n');
                    continue;
                }
                string state = p.Life.Value switch
                {
                    LifeState.Alive => p.Wounded.Value ? "<color=#ffb070>ranny</color>" : p.Limping.Value ? "<color=#ffd080>zraniony</color>" :"<color=#77ccff>chowa się</color>",
                    LifeState.Downed => "<color=#ff7a7a>złapany</color>",
                    _ => "<color=#888888>nie żyje</color>",
                };
                sb.Append(state).Append("  ").Append(nick).Append('\n');
            }
            return sb.ToString();
        }

        // ================================================================ name tags

        private readonly List<Text> _tags = new();
        private readonly RaycastHit[] _tagHits = new RaycastHit[8];

        /// <summary>Lobby: everyone's nick. In a round: only hiders see the nicks of other hiders (seeker sees none).</summary>
        private void UpdateNameTags(NetPlayer me, MatchPhase phase)
        {
            var cam = Camera.main;
            bool lobby = phase == MatchPhase.Lobby || phase == MatchPhase.Loading;
            bool canSee = lobby || me.IsHider;
            int used = 0;
            if (cam != null && canSee)
            {
                var canvasRt = (RectTransform)_canvas.transform;
                Vector3 camPos = cam.transform.position;
                foreach (var p in NetPlayer.All)
                {
                    if (p == null || p == me) continue;
                    if (!lobby && !p.IsHider) continue;
                    bool lying = p.Life.Value != LifeState.Alive;
                    Vector3 head = p.transform.position + Vector3.up * (lying ? 0.75f : 2.15f - 0.55f * p.Motor.CrouchAmount);
                    float dist = Vector3.Distance(camPos, head);
                    if (dist > (lobby ? 30f : 35f)) continue;
                    Vector3 sp = cam.WorldToScreenPoint(head);
                    if (sp.z <= 0.1f) continue;
                    if (Blocked(camPos, head, p.transform, me.transform)) continue;

                    if (used >= _tags.Count)
                    {
                        var t = UiKit.Label("Tag", _canvas.transform, "", 24, TextAnchor.MiddleCenter);
                        t.rectTransform.sizeDelta = new Vector2(320, 34);
                        t.supportRichText = true;
                        t.AddOutline(1.5f);
                        t.transform.SetAsFirstSibling();
                        _tags.Add(t);
                    }
                    var tag = _tags[used++];
                    tag.gameObject.SetActive(true);
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, sp, null, out Vector2 local);
                    tag.rectTransform.anchorMin = tag.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                    tag.rectTransform.anchoredPosition = local;
                    float scale = Mathf.Clamp(6f / Mathf.Max(1f, dist), 0.55f, 1.1f);
                    tag.rectTransform.localScale = Vector3.one * scale;
                    string color = lobby && p.Ready.Value ? "#6dff7a" : p.IsSeeker ? "#ff6655" : p.Life.Value == LifeState.Downed ? "#ff9a8a" : p.Life.Value == LifeState.Dead ? "#999999" : "#ffffff";
                    string suffix = !lobby && p.Life.Value == LifeState.Downed ? "  (złapany)" : !lobby && p.Wounded.Value ? "  (ranny)" : "";
                    string mic = Voice.VoiceChat.IsSpeaking(p) ? "<color=#7dff8a>((</color> " : "";
                    tag.text = $"{mic}<color={color}>{p.Nick}{suffix}</color>";
                    var c = tag.color;
                    c.a = Mathf.Clamp01((35f - dist) / 8f);
                    tag.color = c;
                }
            }
            for (int i = used; i < _tags.Count; i++) _tags[i].gameObject.SetActive(false);
        }

        /// <summary>"MÓWISZ ||||||||" with green / yellow / red zones; red = shout (heard across the garage, with echo).</summary>
        public static string MeterText(float level, int bars = 32)
        {
            var sb = new StringBuilder("MÓWISZ  ");
            int filled = Mathf.RoundToInt(level * bars);
            int yellowFrom = Mathf.RoundToInt(Voice.VoiceChat.YellowLevel * bars);
            int redFrom = Mathf.RoundToInt(Voice.VoiceChat.ShoutLevel * bars);
            for (int i = 0; i < bars; i++)
            {
                string col = i >= redFrom ? "ff4a3a" : i >= yellowFrom ? "ffd23a" : "5dff6a";
                string alpha = i < filled ? "ff" : "40";
                sb.Append("<color=#").Append(col).Append(alpha).Append(">|</color>");
            }
            if (level >= Voice.VoiceChat.ShoutLevel) sb.Append("  <color=#ff4a3a>KRZYK</color>");
            return sb.ToString();
        }

        private bool Blocked(Vector3 from, Vector3 to, Transform target, Transform self)
        {
            Vector3 d = to - from;
            int n = Physics.RaycastNonAlloc(from, d.normalized, _tagHits, d.magnitude, ~((1 << 8) | (1 << 9)), QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var t = _tagHits[i].transform;
                if (t.IsChildOf(target) || t.IsChildOf(self)) continue;
                return true;
            }
            return false;
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
            var panel = UiStyle.Panel("Panel", dim.transform, UiKit.Panel);
            panel.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            UiKit.VStack(panel.rectTransform, 10, 30).padding.top = 34;
            return dim.rectTransform;
        }

        private static readonly bool TestEsc = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-testesc") >= 0;
        private bool _testEscDone;

        /// <summary>Test switch -testesc: opens the Esc menu and its sub pages once in the lobby (for screenshots).</summary>
        private IEnumerator TestEscPages()
        {
            yield return new WaitForSeconds(3f);
            ShowEscMenu();
            yield return new WaitForSeconds(2.2f);
            ShowAudioMenu();
            yield return new WaitForSeconds(2.2f);
            ShowVoiceMenu();
            yield return new WaitForSeconds(2.2f);
            CloseMenus();
        }

        private void ShowEscMenu()
        {
            _escMenu = MenuPanel("EscMenu", new Vector2(600, SteamService.InLobby ? 928 : 858));
            var panel = _escMenu.Find("Panel");
            UiKit.Label("Title", panel, "MENU", 44, TextAnchor.MiddleCenter, null, FontStyle.Bold).Height(64);
            UiKit.Button("Resume", panel, "Wróć do gry", CloseMenus).Height(66);
            SettingsUi.SensitivitySlider(panel, v => { if (NetPlayer.Local != null) NetPlayer.Local.InputReader.MouseSensitivity = v; });
            SettingsUi.DisplayModeButton(panel);
            SettingsUi.CrouchModeButton(panel, v => { if (NetPlayer.Local != null) NetPlayer.Local.InputReader.CrouchToggleMode = v; });
            SettingsUi.DiscordSwitch(panel);
            if (SteamService.InLobby)
                UiKit.Button("Invite", panel, "Zaproś znajomych (Steam)", ShowInviteMenu, 28, new Color(0.13f, 0.32f, 0.16f)).Height(60);
            UiKit.Button("Audio", panel, "Dźwięk", ShowAudioMenu, 28).Height(60);
            UiKit.Button("Voice", panel, "Czat głosowy", ShowVoiceMenu, 28).Height(60);
            UiKit.Button("PlayerVolumes", panel, "Głośność graczy", ShowPlayerVolumeMenu, 28).Height(60);
            bool isHost = NetworkManager.Singleton.IsServer;
            string leave = isHost ? "Zamknij serwer" : "Rozłącz";
            UiKit.Button("Leave", panel, leave, () => ConfirmDialog.ConfirmLeave(isHost, () =>
            {
                CloseMenus();
                NetSession.Instance.Leave();
            }), 30, new Color(0.35f, 0.1f, 0.1f)).Height(70);
            UiKit.Button("Quit", panel, "Wyjdź z gry", ConfirmDialog.ConfirmQuit).Height(60);
        }

        /// <summary>Steam friends list with "Zaproś" buttons (+ the Steam overlay invite dialog).</summary>
        private void ShowInviteMenu()
        {
            if (_escMenu != null) Destroy(_escMenu.gameObject);
            _escMenu = MenuPanel("InviteMenu", new Vector2(720, 860));
            var panel = _escMenu.Find("Panel");
            UiKit.Label("Title", panel, "ZAPROŚ ZNAJOMYCH", 40, TextAnchor.MiddleCenter, null, FontStyle.Bold).Height(54);
            UiKit.Button("Overlay", panel, "Otwórz zaproszenia w nakładce Steam", () => SteamService.OpenInviteOverlay(), 24).Height(54);
            var info = UiKit.Label("Info", panel, "Albo zaproś bezpośrednio:", 22, TextAnchor.MiddleLeft, UiKit.TextDim);
            info.Height(30);
            var friends = SteamService.Friends();
            int shown = 0;
            foreach (var f in friends)
            {
                if (!f.Online) continue;
                if (++shown > 9) break;
                var fr = f;
                string tag = f.Playing ? "  (gra w Hide and Seek)" : "";
                UiStyle.ActionRow("Friend", panel, f.Name + tag, "Zaproś", () =>
                {
                    bool ok = SteamService.Invite(fr.Id);
                    Toast(ok ? $"Wysłano zaproszenie do {fr.Name}." : $"Nie udało się zaprosić {fr.Name}.", 3f);
                }).Height(52);
            }
            if (shown == 0) UiKit.Label("None", panel, "Brak znajomych online.", 22, TextAnchor.MiddleLeft, UiKit.TextDim).Height(30);
            UiKit.Button("Back", panel, "Wróć", () =>
            {
                Destroy(_escMenu.gameObject);
                _escMenu = null;
                ShowEscMenu();
            }, 28).Height(60);
        }

        private void ShowAudioMenu()
        {
            if (_escMenu != null) Destroy(_escMenu.gameObject);
            _escMenu = MenuPanel("AudioMenu", new Vector2(720, 900));
            var panel = _escMenu.Find("Panel");
            UiKit.Label("Title", panel, "DŹWIĘK", 40, TextAnchor.MiddleCenter, null, FontStyle.Bold).Height(54);
            SettingsUi.BuildAudio(panel);
            UiKit.Button("Back", panel, "Wróć", () =>
            {
                Prefs.Save();
                Destroy(_escMenu.gameObject);
                _escMenu = null;
                ShowEscMenu();
            }, 28).Height(58);
        }

        private void ShowVoiceMenu()
        {
            if (_escMenu != null) Destroy(_escMenu.gameObject);
            _escMenu = MenuPanel("VoiceMenu", new Vector2(760, 720));
            var panel = _escMenu.Find("Panel");
            UiKit.Label("Title", panel, "CZAT GŁOSOWY", 40, TextAnchor.MiddleCenter, null, FontStyle.Bold).Height(54);
            SettingsUi.BuildVoice(panel);
            UiKit.Label("Info", panel, "Słychać cię z bliska (do 35 m), za ścianami ciszej. Szukający też cię usłyszy.", 20, TextAnchor.MiddleCenter, UiKit.TextDim).Height(30);
            UiKit.Button("Back", panel, "Wróć", () =>
            {
                Prefs.Save();
                Destroy(_escMenu.gameObject);
                _escMenu = null;
                ShowEscMenu();
            }, 28).Height(58);
        }

        /// <summary>Esc menu page with a voice volume slider per other player (<see cref="PlayerVolumeMenu"/>).</summary>
        public void ShowPlayerVolumeMenu()
        {
            if (_escMenu != null) Destroy(_escMenu.gameObject);
            _escMenu = MenuPanel("PlayerVolumeMenu", new Vector2(760, PlayerVolumeMenu.PanelHeight()));
            var panel = _escMenu.Find("Panel");
            PlayerVolumeMenu.Build(panel);
            UiKit.Button("Back", panel, "Wróć", () =>
            {
                Prefs.Save();
                Destroy(_escMenu.gameObject);
                _escMenu = null;
                ShowEscMenu();
            }, 28).Height(58);
        }

        private void ShowSettings(bool fromBoard)
        {
            if (_settingsPanel != null) return;
            var mm = MatchManager.Instance;
            if (mm == null) return;
            bool host = NetworkManager.Singleton.IsServer;
            _settingsPanel = MenuPanel("RoundSettings", new Vector2(760, 1060 + 58 + 54));
            var panel = _settingsPanel.Find("Panel");
            var s = mm.Settings.Value;
            UiKit.Label("Title", panel, "USTAWIENIA RUNDY", 40, TextAnchor.MiddleCenter, null, FontStyle.Bold).Height(56);
            if (!host) UiKit.Label("Info", panel, "Tylko host może zmieniać ustawienia.", 24, TextAnchor.MiddleCenter, UiKit.TextDim).Height(32);

            Text Row(string label)
            {
                var t = UiKit.Label("L", panel, label, 25);
                t.Height(28);
                return t;
            }

            void Push()
            {
                if (host) mm.SetSettingsRpc(s);
            }

            // Game mode (more will come; not ready ones are marked and the lever refuses to start them).
            string ModeLabel(byte m)
            {
                var info = GameModes.Get(m);
                return "Tryb gry:  < " + info.Name + (info.Ready ? "" : "  (wkrótce)") + " >";
            }
            Button gameModeB = null;
            Text gameModeInfo = null;
            gameModeB = UiKit.Button("GameMode", panel, ModeLabel(s.Mode), () =>
            {
                s.Mode = (byte)((s.Mode + 1) % GameModes.All.Length);
                gameModeB.GetComponentInChildren<Text>().text = ModeLabel(s.Mode);
                var info = GameModes.Get(s.Mode);
                gameModeInfo.text = info.Ready ? info.Tagline : info.Tagline + " Jeszcze nie gotowy - nie da się go uruchomić.";
                gameModeInfo.color = info.Ready ? UiKit.TextDim : UiKit.Danger;
                Push();
            }, 27, new Color(0.13f, 0.2f, 0.15f, 1f));
            gameModeB.Height(54);
            gameModeB.interactable = host;
            var gmInfo = GameModes.Get(s.Mode);
            gameModeInfo = UiKit.Label("ModeInfo", panel, gmInfo.Ready ? gmInfo.Tagline : gmInfo.Tagline + " Jeszcze nie gotowy - nie da się go uruchomić.",
                20, TextAnchor.MiddleCenter, gmInfo.Ready ? UiKit.TextDim : UiKit.Danger);
            gameModeInfo.Height(28);

            var hideL = Row("");
            var hide = UiKit.Slider("Hide", panel, 10, 180, s.HideTime, true, v => { s.HideTime = v; hideL.text = $"Czas na schowanie: {UiKit.FormatTime(v)}"; Push(); });
            hide.Height(30);
            hideL.text = $"Czas na schowanie: {UiKit.FormatTime(s.HideTime)}";

            var seekL = Row("");
            var seek = UiKit.Slider("Seek", panel, 2, 30, s.SeekTime / 60f, true, v =>
            {
                s.SeekTime = v * 60f;
                s.HintStart = Mathf.Min(s.HintStart, s.SeekTime);
                seekL.text = $"Czas szukania: {v:0} min";
                Push();
            });
            seek.Height(30);
            seekL.text = $"Czas szukania: {s.SeekTime / 60f:0} min";

            var hsL = Row("");
            var hs = UiKit.Slider("HintStart", panel, 0, 30, s.HintStart / 60f, true, v =>
            {
                s.HintStart = Mathf.Min(v * 60f, s.SeekTime);
                hsL.text = $"Podpowiedzi od: {UiKit.FormatTime(s.HintStart)} szukania";
                Push();
            });
            hs.Height(30);
            hsL.text = $"Podpowiedzi od: {UiKit.FormatTime(s.HintStart)} szukania";

            var hiL = Row("");
            var hi = UiKit.Slider("HintInterval", panel, 15, 180, s.HintInterval, true, v =>
            {
                s.HintInterval = v;
                hiL.text = $"Podpowiedź co: {UiKit.FormatTime(v)}";
                Push();
            });
            hi.Height(30);
            hiL.text = $"Podpowiedź co: {UiKit.FormatTime(s.HintInterval)}";

            string ModeName(byte m) => m == 3 ? "dźwięk + minimapa" : m == 1 ? "tylko dźwięk" : m == 2 ? "tylko minimapa" : "wyłączone";
            Button modeB = null;
            modeB = UiKit.Button("Mode", panel, "Tryb podpowiedzi: " + ModeName(s.HintMode), () =>
            {
                s.HintMode = (byte)((s.HintMode + 1) % 4);
                modeB.GetComponentInChildren<Text>().text = "Tryb podpowiedzi: " + ModeName(s.HintMode);
                Push();
            }, 26);
            modeB.Height(50);

            Button glockB = null;
            glockB = UiKit.Button("Glock", panel, "Glock na ostatnie 2 min: " + (s.Glock ? "tak" : "nie"), () =>
            {
                s.Glock = !s.Glock;
                glockB.GetComponentInChildren<Text>().text = "Glock na ostatnie 2 min: " + (s.Glock ? "tak" : "nie");
                Push();
            }, 26);
            glockB.Height(50);

            string TakeText() => "Szukający zabiera przedmioty (apteczki, strzykawki, pręty): " + (s.SeekerTakesItems ? "tak" : "nie");
            Button takeB = null;
            takeB = UiKit.Button("SeekerTakes", panel, TakeText(), () =>
            {
                s.SeekerTakesItems = !s.SeekerTakesItems;
                takeB.GetComponentInChildren<Text>().text = TakeText();
                Push();
            }, 22);
            takeB.Height(50);

            var bleedL = Row("");
            var bleed = UiKit.Slider("BleedOut", panel, 20, 180, s.BleedOut > 1f ? s.BleedOut : Tuning.BleedOutTime, true, v =>
            {
                s.BleedOut = v;
                bleedL.text = $"Wykrwawianie po złapaniu: {v:0} s";
                Push();
            });
            bleed.Height(30);
            bleedL.text = $"Wykrwawianie po złapaniu: {(s.BleedOut > 1f ? s.BleedOut : Tuning.BleedOutTime):0} s";

            var medL = Row("");
            var med = UiKit.Slider("Medkits", panel, 0, 8, s.Medkits, true, v => { s.Medkits = (byte)v; medL.text = $"Apteczki na mapie: {v:0}"; Push(); });
            med.Height(30);
            medL.text = $"Apteczki na mapie: {s.Medkits}";

            var lapL = Row("");
            var lap = UiKit.Slider("Laps", panel, 1, 10, s.Laps == 0 ? 3 : s.Laps, true, v => { s.Laps = (byte)v; lapL.text = $"Wyścigi RC - okrążenia: {v:0}"; Push(); });
            lap.Height(30);
            lapL.text = $"Wyścigi RC - okrążenia: {(s.Laps == 0 ? 3 : s.Laps)}";
            lap.interactable = host;

            var syrL = Row("");
            var syr = UiKit.Slider("Syringes", panel, 0, 6, s.Syringes, true, v => { s.Syringes = (byte)v; syrL.text = $"Strzykawki z adrenaliną: {v:0}"; Push(); });
            syr.Height(30);
            syrL.text = $"Strzykawki z adrenaliną: {s.Syringes}";

            foreach (var sl in new[] { hide, seek, hs, hi, med, bleed, syr }) sl.interactable = host;
            modeB.interactable = host;
            glockB.interactable = host;
            takeB.interactable = host;

            UiKit.Button("Close", panel, "Zamknij", CloseMenus).Height(58);
        }
    }
}
