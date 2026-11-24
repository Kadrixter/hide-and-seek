using HideAndSeek.Game.Net;
using HideAndSeek.Game.UI;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Big screen on the lobby wall with the round settings: game mode header, a grid of tiles (times with bars,
    /// hint mode chips, Glock on / off, medkit and syringe icons, bleed-out, killer count / RC laps) and the player list
    /// with ready lights. Drawn as a world-space UI canvas in front of the board. E opens the settings (host edits, others view).
    /// </summary>
    public partial class LobbySettingsBoard : MonoBehaviour
    {
        public static LobbySettingsBoard Instance { get; private set; }

        [SerializeField] private Vector2 screenSize = new Vector2(3.5f, 2.06f);

        private const float W = 1800f, H = 1060f;
        private const int MaxChips = 10;

        private static readonly Color ScreenBg = new Color(0.03f, 0.035f, 0.045f, 1f);
        private static readonly Color Tile = new Color(0.085f, 0.095f, 0.11f, 1f);
        private static readonly Color TileEdge = new Color(1f, 1f, 1f, 0.07f);
        private static readonly Color BarBg = new Color(1f, 1f, 1f, 0.08f);
        private static readonly Color Gold = new Color(1f, 0.78f, 0.25f, 1f);
        private static readonly Color Off = new Color(0.35f, 0.37f, 0.4f, 1f);
        private static readonly Color MedRed = new Color(0.92f, 0.28f, 0.25f, 1f);
        private static readonly Color SyrBlue = new Color(0.35f, 0.7f, 1f, 1f);

        private Canvas _canvas;
        private Text _modeName, _modeTag, _status, _soon, _hint;
        private Image _modeBar;
        private Text[] _value = new Text[8], _sub = new Text[8];
        private Image[] _bar = new Image[8];
        private Image _chipSound, _chipMap;
        private Text _chipSoundT, _chipMapT;
        private Image[] _medIcons = new Image[10], _syrIcons = new Image[6];
        private Image _glockPill, _takePill;
        private Text _glockPillT, _takePillT;
        private Image[] _pChip = new Image[MaxChips], _pDot = new Image[MaxChips];
        private Text[] _pName = new Text[MaxChips], _pState = new Text[MaxChips];
        private Text _more;
        private float _nextRefresh;

        private void OnEnable() => Instance = this;
        private void OnDisable() { if (Instance == this) Instance = null; }

        private void Start() => Build();

        private void OnDestroy()
        {
            if (_canvas != null) Destroy(_canvas.gameObject);
        }

        // ================================================================ build

        private void Build()
        {
            var go = new GameObject("BoardScreen", typeof(RectTransform));
            go.transform.SetParent(transform.parent, false);
            // The board faces the room (-Z); the screen sits just in front of it.
            go.transform.SetPositionAndRotation(transform.position - transform.forward * 0.036f, transform.rotation);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 2.5f;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(W, H);
            float s = screenSize.x / W;
            rt.localScale = new Vector3(s, screenSize.y / H, s);

            var bg = UiKit.Image("Bg", rt, ScreenBg).Rounded();
            bg.rectTransform.Stretch();
            bg.raycastTarget = false;
            var head = UiKit.Image("Header", rt, new Color(0.06f, 0.07f, 0.08f, 1f)).Rounded();
            At(head.rectTransform, 14, 14, W - 28, 214);
            Scanlines(rt);

            // ---- header
            _modeBar = UiKit.Image("ModeBar", rt, UiKit.Accent).Rounded(true);
            At(_modeBar.rectTransform, 40, 36, 12, 166);
            Lbl(rt, "TRYB GRY", 74, 28, 600, 38, 28, UiKit.TextDim, FontStyle.Bold);
            _modeName = Lbl(rt, "CHOWANY", 70, 72, 1000, 92, 76, UiKit.Accent, FontStyle.Bold);
            _modeTag = Lbl(rt, "", 74, 170, 1050, 38, 27, UiKit.Text);
            Lbl(rt, "USTAWIENIA RUNDY", 1060, 40, 700, 46, 36, UiKit.Text, FontStyle.Bold, TextAnchor.MiddleRight);
            var pill = UiKit.Image("StatusPill", rt, new Color(1f, 1f, 1f, 0.07f)).Rounded();
            At(pill.rectTransform, 1300, 98, 460, 64);
            _status = Lbl(pill.rectTransform, "", 0, 0, 460, 64, 30, UiKit.Text, FontStyle.Bold, TextAnchor.MiddleCenter);
            _soon = Lbl(rt, "", 1060, 172, 700, 40, 26, UiKit.TextDim, FontStyle.Italic, TextAnchor.MiddleRight);
            var line = UiKit.Image("Divider", rt, new Color(1f, 1f, 1f, 0.09f));
            At(line.rectTransform, 44, 236, W - 88, 3);

            // ---- tiles (4 x 2)
            string[] titles = { "CHOWANIE", "SZUKANIE", "PODPOWIEDZI", "GLOCK", "APTECZKI", "ADRENALINA", "WYKRWAWIANIE", "ZABÓJCY" };
            const float gx = 44f, gy = 262f, gap = 24f;
            float tw = (W - gx * 2f - gap * 3f) / 4f, th = 252f;
            for (int i = 0; i < 8; i++)
            {
                float x = gx + (i % 4) * (tw + gap), y = gy + (i / 4) * (th + gap);
                var tile = UiKit.Image("Tile" + i, rt, Tile).Rounded();
                At(tile.rectTransform, x, y, tw, th);
                var edge = UiStyle.Ring("Edge", tile.transform, TileEdge);
                edge.rectTransform.Stretch();
                var tr = tile.rectTransform;
                var title = Lbl(tr, titles[i], 26, 16, tw - 40, 36, 26, UiKit.TextDim, FontStyle.Bold);
                if (i == 7) _lastTitle = title; // killers, or laps in the RC race (LobbySettingsBoard.Seekers)
                _value[i] = Lbl(tr, "", 24, 52, tw - 40, 86, 70, UiKit.Text, FontStyle.Bold);
                _sub[i] = Lbl(tr, "", 26, 140, tw - 44, 40, 25, UiKit.TextDim);
                if (i == 2 || i == 3 || i == 4 || i == 5) continue;
                var bb = UiKit.Image("BarBg", tr, BarBg).Rounded(true);
                At(bb.rectTransform, 26, 200, tw - 52, 16);
                _bar[i] = UiKit.Image("Bar", bb.rectTransform, i == 6 ? MedRed : UiKit.Accent).Rounded(true);
                At(_bar[i].rectTransform, 0, 0, tw - 52, 16);
            }
            float chipY = 192f;
            _chipSound = Chip(TileRt(2), 26, chipY, 150, out _chipSoundT, "DŹWIĘK");
            _chipMap = Chip(TileRt(2), 190, chipY, 130, out _chipMapT, "MAPA");
            _glockPill = Chip(TileRt(3), 26, chipY, 140, out _glockPillT, "");
            _takePill = Chip(TileRt(3), 180, chipY, 200, out _takePillT, ""); // seeker takes the hiders' items
            for (int i = 0; i < _medIcons.Length; i++)
            {
                _medIcons[i] = UiKit.Image("Med" + i, TileRt(4), MedRed).Rounded(true);
                At(_medIcons[i].rectTransform, 26 + i * 34, 196, 26, 26);
                var cross = UiKit.Label("X", _medIcons[i].rectTransform, "+", 26, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
                cross.rectTransform.Stretch();
            }
            for (int i = 0; i < _syrIcons.Length; i++)
            {
                _syrIcons[i] = UiKit.Image("Syr" + i, TileRt(5), SyrBlue).Rounded(true);
                At(_syrIcons[i].rectTransform, 26 + i * 40, 202, 32, 14);
            }

            // ---- players
            Lbl(rt, "GRACZE", 48, 806, 400, 40, 28, UiKit.TextDim, FontStyle.Bold);
            const float cw = 334f, ch = 62f;
            for (int i = 0; i < MaxChips; i++)
            {
                float x = 44f + (i % 5) * (cw + 10f), y = 850f + (i / 5) * (ch + 10f);
                _pChip[i] = UiKit.Image("Player" + i, rt, Tile).Rounded();
                At(_pChip[i].rectTransform, x, y, cw, ch);
                _pDot[i] = UiKit.Image("Dot", _pChip[i].rectTransform, Off);
                _pDot[i].sprite = UiStyle.Circle;
                At(_pDot[i].rectTransform, 16, 19, 24, 24);
                _pName[i] = Lbl(_pChip[i].rectTransform, "", 52, 0, cw - 160, ch, 28, UiKit.Text, FontStyle.Bold);
                _pState[i] = Lbl(_pChip[i].rectTransform, "", cw - 150, 0, 136, ch, 21, UiKit.TextDim, FontStyle.Bold, TextAnchor.MiddleRight);
            }
            _more = Lbl(rt, "", 44, 994, 600, 40, 24, UiKit.TextDim);
            _hint = Lbl(rt, "", 1000, 996, 756, 46, 30, UiKit.Accent, FontStyle.Bold, TextAnchor.MiddleRight);

            foreach (var t in go.GetComponentsInChildren<Graphic>(true)) t.raycastTarget = false;
            Refresh();
        }

        private RectTransform TileRt(int i) => (RectTransform)_canvas.transform.Find("Tile" + i);

        private static void At(RectTransform rt, float x, float y, float w, float h)
            => rt.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -y), new Vector2(w, h));

        private static Text Lbl(RectTransform parent, string text, float x, float y, float w, float h, int size, Color color,
            FontStyle style = FontStyle.Normal, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            var t = UiKit.Label("T", parent, text, size, anchor, color, style);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            At(t.rectTransform, x, y, w, h);
            return t;
        }

        private static Image Chip(RectTransform parent, float x, float y, float w, out Text label, string text)
        {
            var img = UiKit.Image("Chip", parent, Off).Rounded();
            At(img.rectTransform, x, y, w, 40);
            label = Lbl(img.rectTransform, text, 0, 0, w, 40, 22, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            return img;
        }

        /// <summary>Faint horizontal lines over the whole screen (display look).</summary>
        private static void Scanlines(RectTransform parent)
        {
            var tex = new Texture2D(1, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
            tex.SetPixels32(new[] { new Color32(0, 0, 0, 40), new Color32(0, 0, 0, 0), new Color32(0, 0, 0, 0), new Color32(0, 0, 0, 0) });
            tex.Apply();
            var raw = UiKit.Rect("Scanlines", parent).gameObject.AddComponent<RawImage>();
            raw.texture = tex;
            raw.uvRect = new Rect(0f, 0f, 1f, H / 3f);
            raw.rectTransform.Stretch(6f);
            raw.raycastTarget = false;
        }

        // ================================================================ refresh

        private void Update()
        {
            if (_canvas == null || Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + 0.25f;
            Refresh();
        }

        private void Refresh()
        {
            var mm = MatchManager.Instance;
            if (mm == null || _canvas == null) return;
            var s = mm.Settings.Value;

            var mode = GameModes.Get(s.Mode);
            _modeName.text = mode.Name.ToUpperInvariant();
            _modeName.color = mode.Ready ? UiKit.Accent : Gold;
            _modeBar.color = _modeName.color;
            _modeTag.text = mode.Ready ? mode.Tagline : mode.Tagline + "  (wkrótce - nie da się jeszcze uruchomić)";
            string soon = "";
            foreach (var m in GameModes.All)
                if (!m.Ready && m.Id != mode.Id) soon += (soon.Length > 0 ? ", " : "") + m.Name;
            _soon.text = soon.Length > 0 ? "Wkrótce: " + soon : "";

            int players = 0, ready = 0, others = 0;
            foreach (var p in NetPlayer.All)
            {
                if (p == null) continue;
                players++;
                if (p.OwnerClientId == NetworkManager.ServerClientId) continue;
                others++;
                if (p.Ready.Value) ready++;
            }
            _status.text = $"GRACZE {players}   GOTOWI {ready}/{others}";

            // Times.
            Set(0, UiKit.FormatTime(s.HideTime), "na schowanie się", s.HideTime / 180f);
            Set(1, UiKit.FormatTime(s.SeekTime), "czas szukania", s.SeekTime / 1800f);
            bool sound = (s.HintMode & 1) != 0, map = (s.HintMode & 2) != 0;
            Set(2, s.HintMode == 0 ? "brak" : "co " + UiKit.FormatTime(s.HintInterval),
                s.HintMode == 0 ? "wyłączone" : "od " + UiKit.FormatTime(s.HintStart) + " szukania", 0f);
            SetChip(_chipSound, _chipSoundT, sound, "DŹWIĘK");
            SetChip(_chipMap, _chipMapT, map, "MAPA");
            Set(3, s.Glock ? "TAK" : "NIE", s.Glock ? (s.NoSaw ? "ostatnie 2 min, bez piły" : "szukający, ostatnie 2 min")
                : s.NoSaw ? "tylko nóż (bez piły)" : "tylko nóż i piła", 0f);
            _value[3].color = s.Glock ? UiKit.Accent : MedRed;
            SetChip(_glockPill, _glockPillT, s.Glock, s.Glock ? "WŁĄCZONY" : "WYŁĄCZONY");
            SetChip(_takePill, _takePillT, s.SeekerTakesItems, s.SeekerTakesItems ? "ZABIERA RZECZY" : "NIE ZABIERA");
            Set(4, s.Medkits.ToString(), "na mapie", 0f);
            for (int i = 0; i < _medIcons.Length; i++) _medIcons[i].enabled = i < s.Medkits;
            foreach (var t in _medIcons) t.GetComponentInChildren<Text>().enabled = t.enabled;
            Set(5, s.Syringes.ToString(), "strzykawki (wskrzeszenie)", 0f);
            for (int i = 0; i < _syrIcons.Length; i++) _syrIcons[i].enabled = i < s.Syringes;
            float bleed = s.BleedOut > 1f ? s.BleedOut : 78f;
            Set(6, $"{bleed:0} s", "po złapaniu", bleed / 180f);
            RefreshLastTile(s, players);
            RefreshVoice(s); // LobbySettingsBoard.Voice

            // Players.
            int n = 0;
            foreach (var p in NetPlayer.All)
            {
                if (p == null) continue;
                if (n >= MaxChips) { n++; continue; }
                bool owner = p.OwnerClientId == NetworkManager.ServerClientId;
                bool r = p.Ready.Value;
                _pChip[n].enabled = true;
                _pDot[n].enabled = true;
                _pDot[n].color = owner ? Gold : r ? UiKit.Accent : Off;
                _pName[n].text = Trim(p.Nick, 14);
                _pState[n].text = owner ? "WŁAŚCICIEL" : r ? "GOTOWY" : "czeka";
                _pState[n].color = owner ? Gold : r ? UiKit.Accent : UiKit.TextDim;
                n++;
            }
            for (int i = n; i < MaxChips; i++)
            {
                _pChip[i].enabled = false;
                _pDot[i].enabled = false;
                _pName[i].text = "";
                _pState[i].text = "";
            }
            _more.text = n > MaxChips ? $"+ {n - MaxChips} więcej" : n == 0 ? "Brak graczy" : "";
            bool host = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
            _hint.text = host ? "[E]  zmień ustawienia" : "[E]  podgląd ustawień";
        }

        private void Set(int i, string value, string sub, float bar)
        {
            _value[i].text = value;
            _sub[i].text = sub;
            if (_bar[i] == null) return;
            var rt = _bar[i].rectTransform;
            float full = ((RectTransform)rt.parent).sizeDelta.x;
            rt.sizeDelta = new Vector2(Mathf.Max(16f, full * Mathf.Clamp01(bar)), rt.sizeDelta.y);
        }

        private static void SetChip(Image chip, Text label, bool on, string text)
        {
            chip.color = on ? new Color(UiKit.Accent.r * 0.55f, UiKit.Accent.g * 0.55f, UiKit.Accent.b * 0.55f, 1f) : new Color(0.2f, 0.21f, 0.24f, 1f);
            label.text = text;
            label.color = on ? Color.white : UiKit.TextDim;
        }

        private static string Trim(string s, int max) => string.IsNullOrEmpty(s) ? "?" : s.Length <= max ? s : s.Substring(0, max - 1) + ".";
    }
}
