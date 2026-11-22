using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// SNAKE arcade cabinet in the lobby: draws the screen (live game from <see cref="ArcadeState"/>, the same on every
    /// peer, or the attract demo while nobody plays) into a 256x192 point-filtered texture at 30 fps, moves the joystick,
    /// pulses the glow light and owns the "do not block the player" helpers (<see cref="ArcadeKeepOut"/>).
    /// </summary>
    public class LobbyArcade : MonoBehaviour
    {
        public Renderer screen;
        public Light glow;

        public static LobbyArcade Instance { get; private set; }

        private Texture2D _tex;
        private readonly SnakeScreen _screen = new();
        private readonly SnakeAttract _attract = new();
        private readonly SnakeView _live = new();
        private float _redraw;
        private float _glowBase;

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            _tex = new Texture2D(SnakeScreen.TexW, SnakeScreen.TexH, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "SnakeScreen" };
            if (screen != null)
            {
                var m = screen.material;
                m.mainTexture = _tex;
                if (m.HasProperty("_EmissionMap")) m.SetTexture("_EmissionMap", _tex);
            }
            if (glow != null) _glowBase = glow.intensity;
            gameObject.AddComponent<ArcadeKeepOut>();
            ArcadeArt.Apply(transform);
            FindButtons();
        }

        /// <summary>Somebody is at the cabinet and the server is still sending (stale after 3 s, e.g. host gone).</summary>
        public static bool LiveGame => ArcadeState.Live && Time.time - ArcadeState.Updated < 3f;

        private void Update()
        {
            bool live = LiveGame;
            if (!live) _attract.Tick(Time.deltaTime);
            SnakeSounds.Tick();
            _redraw += Time.deltaTime;
            if (_redraw >= 1f / 30f)
            {
                _redraw = 0f;
                _screen.Draw(live ? FillLive() : AttractView(), Time.time);
                _tex.SetPixels32(_screen.Pixels);
                _tex.Apply(false);
            }
            UpdateStick(live);
            UpdateButtons(live);
            if (glow != null)
            {
                float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 3.1f);
                if (live && ArcadeState.State == ArcadeState.Over) pulse *= 0.8f + 0.2f * Mathf.Sin(Time.time * 12f);
                glow.intensity = _glowBase * pulse;
            }
        }

        private SnakeView AttractView()
        {
            _attract.View.Best = ArcadeState.Best;
            _attract.View.StateAge = 0f;
            return _attract.View;
        }

        private SnakeView FillLive()
        {
            var v = _live;
            v.Body.Clear();
            v.Body.AddRange(ArcadeState.Body);
            v.Food = ArcadeState.Food;
            v.HasBonus = ArcadeState.HasBonus;
            v.Bonus = ArcadeState.Bonus;
            v.BonusLeft = ArcadeState.BonusRemaining;
            v.Score = ArcadeState.Score;
            v.Best = ArcadeState.BestAtStart;
            v.Level = ArcadeState.Level;
            v.Dir = ArcadeState.Dir;
            v.State = ArcadeState.State;
            v.StateAge = Time.time - ArcadeState.StateSince;
            v.NewRecord = ArcadeState.NewRecord;
            v.Popups = ArcadeState.Popups;
            // The head looks where the snake is heading (the joystick may already show a queued turn).
            if (v.Body.Count > 1)
            {
                var d = v.Body[0] - v.Body[1];
                v.Dir = d == Vector2Int.up ? 0 : d == Vector2Int.right ? 1 : d == Vector2Int.down ? 2 : 3;
            }
            return v;
        }

        // ================================================================ buttons

        private struct Btn
        {
            public Transform Cap, Dome;
            public Vector3 CapPos, DomePos;
            public Material Mat;
            public Color Emission;
            public float Press;
        }

        private Btn[] _buttons = System.Array.Empty<Btn>();
        private int _seenSerial, _eats;

        private void FindButtons()
        {
            var list = new System.Collections.Generic.List<Btn>();
            foreach (Transform t in transform)
            {
                if (t.name != "Button" && t.name != "StartButton") continue;
                var cap = t.Find("Cap");
                var dome = t.Find("Dome");
                if (cap == null || dome == null) continue;
                var r = cap.GetComponent<Renderer>();
                var m = r != null ? r.material : null;
                if (m != null) dome.GetComponent<Renderer>().sharedMaterial = m;
                list.Add(new Btn
                {
                    Cap = cap, Dome = dome, CapPos = cap.localPosition, DomePos = dome.localPosition, Mat = m,
                    Emission = m != null && m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") : Color.black,
                });
            }
            _buttons = list.ToArray();
        }

        /// <summary>Buttons glow softly and are pressed in on game events (start, eating, bonus).</summary>
        private void UpdateButtons(bool live)
        {
            if (_buttons.Length == 0) return;
            if (live && ArcadeState.EventSerial != _seenSerial)
            {
                _seenSerial = ArcadeState.EventSerial;
                byte e = ArcadeState.LastEvents;
                if ((e & ArcadeState.EvStart) != 0 && _buttons.Length > 4) _buttons[4].Press = 1f;
                if ((e & ArcadeState.EvEat) != 0) _buttons[_eats++ % Mathf.Min(4, _buttons.Length)].Press = 1f;
                if ((e & (ArcadeState.EvBonusEat | ArcadeState.EvLevelUp)) != 0)
                    for (int i = 0; i < Mathf.Min(4, _buttons.Length); i++) _buttons[i].Press = 1f;
            }
            for (int i = 0; i < _buttons.Length; i++)
            {
                ref var b = ref _buttons[i];
                b.Press = Mathf.MoveTowards(b.Press, 0f, Time.deltaTime * 5f);
                float down = Mathf.Clamp01(b.Press * 1.6f) * 0.006f;
                b.Cap.localPosition = b.CapPos - Vector3.up * down;
                b.Dome.localPosition = b.DomePos - Vector3.up * down;
                if (b.Mat != null)
                {
                    float pulse = live ? 1f : 0.85f + 0.15f * Mathf.Sin(Time.time * 2f + i);
                    b.Mat.SetColor("_EmissionColor", b.Emission * (pulse + 2.2f * b.Press));
                }
            }
        }

        // ================================================================ joystick

        private Transform _stick, _ball;
        private Vector3 _stickPos, _ballPos, _pivot;
        private Quaternion _stickRot;
        private Vector2 _tilt, _lastWant;

        /// <summary>Joystick follows the snake's direction while somebody plays.</summary>
        private void UpdateStick(bool live)
        {
            if (_stick == null)
            {
                _stick = transform.Find("Stick");
                _ball = transform.Find("StickBall");
                if (_stick == null || _ball == null) return;
                _stickPos = _stick.position;
                _ballPos = _ball.position;
                _stickRot = _stick.rotation;
                _pivot = _stickPos - Vector3.up * _stick.lossyScale.y;
            }
            Vector2 want = Vector2.zero;
            if (live && ArcadeState.State == ArcadeState.Playing)
                want = ArcadeState.Dir switch { 0 => Vector2.up, 1 => Vector2.right, 2 => Vector2.down, _ => Vector2.left };
            if (want != _lastWant && want != Vector2.zero) HideAndSeek.Audio.GameAudio.PlayAt(HideAndSeek.Audio.GameSfx.ArcadeStick, _ballPos, 0.7f);
            _lastWant = want;
            _tilt = Vector2.MoveTowards(_tilt, want, Time.deltaTime * 14f);
            var rot = Quaternion.Euler(_tilt.y * 20f, 0f, -_tilt.x * 20f);
            _stick.SetPositionAndRotation(_pivot + rot * (_stickPos - _pivot), rot * _stickRot);
            _ball.position = _pivot + rot * (_ballPos - _pivot);
        }
    }
}
