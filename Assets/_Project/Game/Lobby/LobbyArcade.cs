using System;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Arcade cabinet screen: a tiny procedural "snake chases dots" attract mode drawn into a 64x48 texture
    /// (point filtered, emissive), plus a pulsing glow light.
    /// </summary>
    public class LobbyArcade : MonoBehaviour
    {
        public Renderer screen;
        public Light glow;

        private const int W = 64, H = 48;
        private Texture2D _tex;
        private Color32[] _px;
        private readonly Vector2Int[] _snake = new Vector2Int[18];
        private int _len = 6;
        private Vector2Int _dir = Vector2Int.right, _food;
        private float _step;
        private float _glowBase;
        private System.Random _rnd = new System.Random(5);
        private int _score;

        private void Start()
        {
            _tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            _px = new Color32[W * H];
            if (screen != null)
            {
                var m = screen.material;
                m.mainTexture = _tex;
                if (m.HasProperty("_EmissionMap")) m.SetTexture("_EmissionMap", _tex);
            }
            for (int i = 0; i < _snake.Length; i++) _snake[i] = new Vector2Int(10 - i, 20);
            _food = new Vector2Int(40, 30);
            if (glow != null) _glowBase = glow.intensity;
        }

        private Transform _stick, _ball;
        private Vector3 _stickPos, _ballPos, _pivot;
        private Quaternion _stickRot;
        private Vector2 _tilt;

        private void Update()
        {
            bool live = ArcadeState.Live && Time.time - ArcadeState.Updated < 3f;
            _step += Time.deltaTime;
            if (_step >= (live ? 0.05f : 0.08f))
            {
                _step = 0f;
                if (live) DrawLive();
                else
                {
                    Advance();
                    Draw();
                }
            }
            UpdateStick(live);
            if (glow != null) glow.intensity = _glowBase * (0.85f + 0.15f * Mathf.Sin(Time.time * 3.1f));
        }

        private void Advance()
        {
            var head = _snake[0];
            // Simple AI: turn towards the food, sometimes wiggle.
            Vector2Int want = _dir;
            if (head.x != _food.x && (_rnd.Next(4) > 0 || head.y == _food.y)) want = new Vector2Int(Math.Sign(_food.x - head.x), 0);
            else if (head.y != _food.y) want = new Vector2Int(0, Math.Sign(_food.y - head.y));
            if (want + _dir != Vector2Int.zero) _dir = want;
            for (int i = _snake.Length - 1; i > 0; i--) _snake[i] = _snake[i - 1];
            var n = head + _dir;
            n.x = (n.x + W) % W;
            n.y = (n.y - 6 + (H - 8)) % (H - 8) + 6;
            _snake[0] = n;
            if (n == _food)
            {
                _score++;
                _len = Mathf.Min(_snake.Length, _len + 2);
                if (_len >= _snake.Length) _len = 6;
                _food = new Vector2Int(_rnd.Next(4, W - 4), _rnd.Next(8, H - 4));
            }
        }

        private void Draw()
        {
            var bg = new Color32(4, 6, 18, 255);
            for (int i = 0; i < _px.Length; i++) _px[i] = bg;
            // border + score bar
            for (int x = 0; x < W; x++) { Set(x, 4, new Color32(40, 60, 160, 255)); Set(x, H - 1, new Color32(40, 60, 160, 255)); }
            for (int i = 0; i < Mathf.Min(_score % 30, 30); i++) Set(2 + i * 2, 2, new Color32(255, 220, 60, 255));
            // food blinks
            if ((Time.frameCount / 8) % 2 == 0)
            {
                Set(_food.x, _food.y, new Color32(255, 60, 90, 255));
                Set(_food.x + 1, _food.y, new Color32(255, 60, 90, 255));
                Set(_food.x, _food.y + 1, new Color32(255, 60, 90, 255));
                Set(_food.x + 1, _food.y + 1, new Color32(255, 60, 90, 255));
            }
            for (int i = 0; i < _len; i++)
            {
                byte g = (byte)(255 - i * 8);
                Set(_snake[i].x, _snake[i].y, new Color32(40, g, 90, 255));
            }
            // scanlines
            for (int y = 0; y < H; y += 2)
            for (int x = 0; x < W; x++)
            {
                var c = _px[y * W + x];
                _px[y * W + x] = new Color32((byte)(c.r * 0.75f), (byte)(c.g * 0.75f), (byte)(c.b * 0.75f), 255);
            }
            _tex.SetPixels32(_px);
            _tex.Apply(false);
        }

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
            if (live && !ArcadeState.Crashed)
                want = ArcadeState.Dir switch { 0 => Vector2.up, 1 => Vector2.right, 2 => Vector2.down, _ => Vector2.left };
            if (want != _lastWant && want != Vector2.zero) HideAndSeek.Audio.GameAudio.PlayAt(HideAndSeek.Audio.GameSfx.ArcadeStick, _ballPos, 0.7f);
            _lastWant = want;
            _tilt = Vector2.MoveTowards(_tilt, want, Time.deltaTime * 14f);
            var rot = Quaternion.Euler(_tilt.y * 20f, 0f, -_tilt.x * 20f);
            _stick.SetPositionAndRotation(_pivot + rot * (_stickPos - _pivot), rot * _stickRot);
            _ball.position = _pivot + rot * (_ballPos - _pivot);
        }

        private Vector2 _lastWant;

        private static readonly string[] Digits =
        {
            "111101101101111", "010110010010111", "111001111100111", "111001111001111", "101101111001001",
            "111100111001111", "111100111101111", "111001010010010", "111101111101111", "111101111001111",
        };

        private void DrawNumber(int value, int x, int y, Color32 c)
        {
            string str = Mathf.Max(0, value).ToString();
            for (int i = 0; i < str.Length; i++)
            {
                string g = Digits[str[i] - '0'];
                for (int r = 0; r < 5; r++)
                for (int col = 0; col < 3; col++)
                    if (g[r * 3 + col] == '1') Set(x + i * 4 + col, y + 4 - r, c);
            }
        }

        private void DrawLive()
        {
            var bg = new Color32(3, 5, 14, 255);
            for (int i = 0; i < _px.Length; i++) _px[i] = bg;
            var border = new Color32(40, 70, 190, 255);
            for (int x = 0; x < W; x++) { Set(x, 5, border); Set(x, H - 1, border); }
            for (int y = 5; y < H; y++) { Set(0, y, border); Set(W - 1, y, border); }
            DrawNumber(ArcadeState.Score, 2, 0, new Color32(255, 220, 60, 255));
            DrawNumber(ArcadeState.Best, W - 2 - 4 * Mathf.Max(1, ArcadeState.Best.ToString().Length), 0, new Color32(120, 200, 255, 255));
            void Cell(Vector2Int c, Color32 col)
            {
                int px = c.x * 2, py = 6 + c.y * 2;
                Set(px, py, col); Set(px + 1, py, col); Set(px, py + 1, col); Set(px + 1, py + 1, col);
            }
            if ((int)(Time.time * 4f) % 2 == 0 || ArcadeState.Crashed) Cell(ArcadeState.Food, new Color32(255, 60, 90, 255));
            bool blink = ArcadeState.Crashed && (int)(Time.time * 6f) % 2 == 0;
            var body = ArcadeState.Body;
            for (int i = 0; i < body.Count; i++)
            {
                byte g = (byte)Mathf.Max(90, 255 - i * 5);
                Color32 col = ArcadeState.Crashed ? (blink ? new Color32(255, 40, 40, 255) : new Color32(120, 20, 20, 255))
                    : i == 0 ? new Color32(190, 255, 170, 255) : new Color32(40, g, 90, 255);
                Cell(body[i], col);
            }
            for (int y = 0; y < H; y += 2)
            for (int x = 0; x < W; x++)
            {
                var c = _px[y * W + x];
                _px[y * W + x] = new Color32((byte)(c.r * 0.8f), (byte)(c.g * 0.8f), (byte)(c.b * 0.8f), 255);
            }
            _tex.SetPixels32(_px);
            _tex.Apply(false);
        }

        private void Set(int x, int y, Color32 c)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return;
            _px[y * W + x] = c;
        }
    }
}
