using HideAndSeek.Game.Net;
using HideAndSeek.Map;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using HideAndSeek.Player;

namespace HideAndSeek.Game.UI
{
    /// <summary>
    /// The bonnet lockpick mini-game of the local player (MatchManager.CarBatteries, <see cref="NetPlayer.HoodWork"/> =
    /// Lockpick) and the progress bar while the bonnet is lifted. The latch has 3-5 pins (more on dusty cars,
    /// <see cref="BatteryTuning.PinsFor"/>); for each pin the wire tip sweeps along the slot and the player sets it with
    /// Space / LMB / E while the tip is over the green notch (narrower and faster on dusty cars). A miss breaks the wire:
    /// the server takes one piece and the game starts over (or closes when no wire is left). Esc stops (no wire lost).
    /// The game runs locally; the server gets the result (and checks the time it took). Runs before the HUD so Esc
    /// never opens the game menu meanwhile. While it is open <see cref="LocalPlayerController.MenuOpen"/> holds the body
    /// still. Tests: <see cref="TestAuto"/> plays perfectly, <see cref="TestFailNext"/> misses the next pin on purpose.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public sealed class LockpickUi : MonoBehaviour
    {
        private static LockpickUi _inst;
        private static int _escFrame = -1;

        /// <summary>Esc was used by the lockpick this frame (the game menu must not open).</summary>
        public static bool ConsumesEscape => _escFrame == Time.frameCount;
        /// <summary>The mini-game is on screen.</summary>
        public static bool Active { get; private set; }
        /// <summary>Tests: set each pin as soon as the tip is over the notch / miss the next pin.</summary>
        public static bool TestAuto, TestFailNext;
        /// <summary>Tests / logs: tries started and lost on this peer.</summary>
        public static int Tries, Losses;

        private const int MaxPins = BatteryTuning.PinsMax;

        private Canvas _canvas;
        private RectTransform _panel, _track, _zone, _marker, _openBar;
        private Image _panelImg, _openFill;
        private Text _title, _help, _status, _openText;
        private readonly Image[] _pins = new Image[MaxPins];
        private readonly float[] _zoneAt = new float[MaxPins];

        private bool _menuHeld;
        private int _hood = -1, _pinCount, _pin, _shownWire = -1, _shownPin = -1, _shownSecs = -1;
        private double _start = -1d;
        private float _pos, _dir = 1f, _speed, _width, _openedAt, _failFlash, _setFlash;
        private bool _pending;

        public static void Ensure()
        {
            if (_inst != null) return;
            var go = new GameObject("LockpickUi");
            DontDestroyOnLoad(go);
            _inst = go.AddComponent<LockpickUi>();
        }

        private void Awake() => Build();

        private void OnDestroy()
        {
            if (_menuHeld) LocalPlayerController.MenuOpen = false;
            if (_inst == this) _inst = null;
        }

        private void Build()
        {
            _canvas = UiKit.CreateCanvas("LockpickCanvas", 70, transform);
            var root = _canvas.transform;

            _panel = UiStyle.Panel("LockPanel", root, new Color(0.05f, 0.055f, 0.06f, 0.93f)).rectTransform;
            _panel.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(720f, 380f));
            _panelImg = _panel.GetComponent<Image>();
            _title = UiKit.Label("Title", _panel, "Zamek maski", 34, TextAnchor.MiddleCenter, UiKit.Text, FontStyle.Bold);
            _title.rectTransform.Place(new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(0f, 44f));
            _help = UiKit.Label("Help", _panel, "SPACJA / LPM - ustaw zapadkę, gdy drut jest nad zielonym wcięciem.   Esc - przerwij", 20, TextAnchor.MiddleCenter, UiKit.TextDim);
            _help.rectTransform.Place(new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(-40f, 30f));

            // pins of the latch (rise and turn green when set)
            for (int i = 0; i < MaxPins; i++)
            {
                var pin = UiKit.Image("Pin" + i, _panel, new Color(0.5f, 0.5f, 0.52f));
                pin.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(46f, 80f));
                _pins[i] = pin;
            }
            // slot: dark track, green notch, the wire tip
            _track = UiKit.Image("Track", _panel, new Color(0.13f, 0.13f, 0.15f)).rectTransform;
            _track.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -50f), new Vector2(600f, 34f));
            _zone = UiKit.Image("Notch", _track, new Color(0.25f, 0.75f, 0.32f, 0.9f)).rectTransform;
            _zone.Place(new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60f, 0f));
            _marker = UiKit.Image("WireTip", _track, new Color(0.92f, 0.93f, 0.95f)).rectTransform;
            _marker.Place(new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6f, 18f));
            _status = UiKit.Label("Status", _panel, "", 24, TextAnchor.MiddleCenter, UiKit.Text);
            _status.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -100f), new Vector2(0f, 32f));
            _title.AddOutline(1.5f, 0.6f);

            // lifting the bonnet: a bar under the crosshair
            _openBar = UiKit.Image("OpenBar", root, new Color(0f, 0f, 0f, 0.6f)).rectTransform;
            _openBar.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -150f), new Vector2(360f, 18f));
            _openFill = UiKit.Image("Fill", _openBar, new Color(0.95f, 0.75f, 0.2f, 0.95f));
            _openFill.rectTransform.Stretch(2f);
            _openFill.type = Image.Type.Filled;
            _openFill.fillMethod = Image.FillMethod.Horizontal;
            _openFill.sprite = UiStyle.RoundSmall;
            _openText = UiKit.Label("Text", _openBar, "", 22, TextAnchor.MiddleCenter, UiKit.Text);
            _openText.rectTransform.Place(new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(0f, 28f));
            _openText.AddOutline(1.5f, 0.8f);

            _panel.gameObject.SetActive(false);
            _openBar.gameObject.SetActive(false);
        }

        private void Update()
        {
            var me = NetPlayer.Local;
            var mm = MatchManager.Instance;
            var w = me != null ? me.HoodWork.Value : HoodWorkState.None;
            bool picking = mm != null && w.Active && w.Mode == HoodWorkMode.Lockpick;
            bool opening = mm != null && w.Active && w.Mode == HoodWorkMode.Opening;

            // ---- lifting the bonnet: progress bar
            _openBar.gameObject.SetActive(opening);
            if (opening)
            {
                float t = Mathf.Clamp01((float)(mm.Now - w.Start) / Mathf.Max(0.1f, w.Duration));
                _openFill.fillAmount = t;
                int secs = Mathf.CeilToInt(Mathf.Max(0f, w.Duration * (1f - t)) * 10f);
                if (secs != _shownSecs)
                {
                    _shownSecs = secs;
                    _openText.text = $"Podnosisz maskę... {secs / 10f:0.0} s";
                }
            }

            // ---- the lockpick
            if (!picking)
            {
                if (Active) Close();
                return;
            }
            if (!Active || w.Hood != _hood || w.Start != _start) Open(w, mm);
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            float dt = Time.unscaledDeltaTime;
            _failFlash = Mathf.Max(0f, _failFlash - dt);
            _setFlash = Mathf.Max(0f, _setFlash - dt);

            bool esc = (KeyOrPad.Down(Key.Escape) || PadInput.Cancel) && !ConfirmDialog.ConsumesEscape; // pad: Options / Circle
            if (esc)
            {
                _escFrame = Time.frameCount;
                me.HoodStopRpc();
                Close();
                return;
            }
            if (!_pending)
            {
                _pos += _dir * _speed * dt;
                if (_pos > 1f) { _pos = 2f - _pos; _dir = -1f; }
                if (_pos < 0f) { _pos = -_pos; _dir = 1f; }
                bool inZone = Mathf.Abs(_pos - _zoneAt[_pin]) <= _width * 0.5f;
                bool press = Time.unscaledTime - _openedAt > 0.3f && (KeyOrPad.Down(Key.Space) || KeyOrPad.Down(Key.E) || // pad: Cross / Square / R2
                                                                      KeyOrPad.LmbDown);
                if (TestFailNext && !inZone && Time.unscaledTime - _openedAt > 0.4f) { press = true; TestFailNext = false; }
                else if (TestAuto && inZone && Time.unscaledTime - _openedAt > 0.35f + _pin * 0.2f) press = true;
                if (press) Press(me, inZone);
            }
            Draw(me);
        }

        private void Open(HoodWorkState w, MatchManager mm)
        {
            var h = MatchManager.HoodAt(w.Hood);
            float dust = h != null ? h.Dust : 0.5f;
            bool retry = Active && w.Hood == _hood;
            _hood = w.Hood;
            _start = w.Start;
            _pinCount = BatteryTuning.PinsFor(dust);
            _pin = 0;
            _pending = false;
            _pos = 0f;
            _dir = 1f;
            _speed = Mathf.Lerp(0.85f, 1.45f, dust);
            _width = Mathf.Lerp(0.2f, 0.11f, dust);
            var rnd = new System.Random((int)(w.Start * 1000d) ^ (w.Hood * 7919));
            for (int i = 0; i < MaxPins; i++) _zoneAt[i] = 0.18f + (float)rnd.NextDouble() * 0.64f;
            _openedAt = Time.unscaledTime;
            _shownPin = _shownWire = -1;
            Tries++;
            if (!Active)
            {
                Active = true;
                _panel.gameObject.SetActive(true);
                if (!LocalPlayerController.MenuOpen) { LocalPlayerController.MenuOpen = true; _menuHeld = true; }
            }
            _status.text = retry ? "Nowy kawałek drutu - jeszcze raz." : (h != null && h.Dust >= 0.7f ? "Zardzewiały zamek - ciężko chodzi." : "");
            Debug.Log($"[Lockpick] {(retry ? "retry" : "open")} hood #{w.Hood} dust {dust:0.0}: {_pinCount} pins, speed {_speed:0.00}, notch {_width:0.00}");
        }

        private void Press(NetPlayer me, bool inZone)
        {
            var h = MatchManager.HoodAt(_hood);
            Vector3 at = h != null ? h.FrontWorld : me.transform.position;
            if (!inZone)
            {
                Losses++;
                _pending = true;
                _failFlash = 0.6f;
                _status.text = "Drut pękł!";
                me.LockpickResultRpc(false, (byte)_pin);
                Debug.Log($"[Lockpick] miss at pin {_pin + 1}/{_pinCount} (tip {_pos:0.00}, notch {_zoneAt[_pin]:0.00} ±{_width * 0.5f:0.00})");
                return;
            }
            WireSounds.Twist(at);
            _setFlash = 0.25f;
            _pin++;
            if (_pin >= _pinCount)
            {
                _pending = true;
                _status.text = "Zamek puścił!";
                WireSounds.Pop(at);
                me.LockpickResultRpc(true, (byte)_pin);
                Debug.Log($"[Lockpick] all {_pinCount} pins set");
                return;
            }
            _pos = _dir > 0f ? 0f : 1f; // the next pin: the tip comes back from the end
            _speed *= 1.08f;
        }

        private void Draw(NetPlayer me)
        {
            float tw = _track.rect.width;
            _zone.anchoredPosition = new Vector2(_zoneAt[Mathf.Min(_pin, MaxPins - 1)] * tw, 0f);
            _zone.sizeDelta = new Vector2(_width * tw, 0f);
            _marker.anchoredPosition = new Vector2(_pos * tw, 0f);
            _panelImg.color = Color.Lerp(new Color(0.05f, 0.055f, 0.06f, 0.93f), new Color(0.45f, 0.06f, 0.04f, 0.95f), _failFlash / 0.6f);
            if (_pin != _shownPin)
            {
                _shownPin = _pin;
                float gap = 64f, x0 = -(gap * (_pinCount - 1)) * 0.5f;
                for (int i = 0; i < MaxPins; i++)
                {
                    var p = _pins[i];
                    bool used = i < _pinCount;
                    p.gameObject.SetActive(used);
                    if (!used) continue;
                    bool set = i < _pin;
                    p.rectTransform.anchoredPosition = new Vector2(x0 + i * gap, set ? 46f : 20f);
                    p.color = set ? new Color(0.3f, 0.85f, 0.4f) : i == _pin ? new Color(0.95f, 0.8f, 0.3f) : new Color(0.45f, 0.45f, 0.48f);
                }
            }
            int wire = me.WireCount.Value;
            if (wire != _shownWire)
            {
                _shownWire = wire;
                _title.text = $"Zamek maski   (drut: {wire})";
            }
        }

        private void Close()
        {
            Active = false;
            _hood = -1;
            _start = -1d;
            _panel.gameObject.SetActive(false);
            if (_menuHeld)
            {
                LocalPlayerController.MenuOpen = false;
                _menuHeld = false;
            }
        }
    }
}
