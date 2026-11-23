using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Server-side SNAKE game of one arcade cabinet. States: Ready ("press a direction") -> Playing -> Over (score /
    /// record) -> next press starts again. Turns are buffered (up to <see cref="SnakeRules.TurnBuffer"/>), a turn into
    /// the snake's own neck is ignored, the speed goes up every 5 points, a golden bonus food appears for a few seconds
    /// after every 5 foods. <see cref="MatchManager"/> runs one per cabinet and broadcasts the frames.
    /// </summary>
    public sealed class SnakeSim
    {
        public const int W = SnakeRules.W, H = SnakeRules.H;
        private static readonly Vector2Int[] Dirs = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
        private static readonly Vector2Int NoBonus = new(-1, -1);

        private readonly List<Vector2Int> _snake = new();
        private readonly List<Vector2Int> _turns = new();
        private Vector2Int _dir, _food, _bonus;
        private int _score, _grow, _eaten, _level;
        private float _clock, _bonusLeft, _deadFor, _beat;
        private readonly System.Random _rnd;

        public byte State { get; private set; } = ArcadeState.Idle;

        public SnakeSim(int seed) => _rnd = new System.Random(seed);

        public void Stop() => State = ArcadeState.Idle;

        public void Reset()
        {
            _snake.Clear();
            for (int i = 0; i < SnakeRules.StartLength; i++) _snake.Add(new Vector2Int(W / 2 - 4 - i, 6)); // lower half: the start panel covers the top
            _dir = Vector2Int.right;
            _turns.Clear();
            _score = _grow = _eaten = 0;
            _level = 1;
            State = ArcadeState.Ready;
            _deadFor = _clock = _bonusLeft = 0f;
            _bonus = NoBonus;
            _food = FreeCell(new Vector2Int(W / 2 + 5, 6));
        }

        /// <summary>A direction key. Returns the events to send now (0 = nothing to send).</summary>
        public byte Press(byte d, out bool send)
        {
            send = false;
            if (d > 3) return 0;
            var nd = Dirs[d];
            if (State == ArcadeState.Over)
            {
                if (_deadFor < 1.0f) return 0;
                Reset();
            }
            if (State == ArcadeState.Ready)
            {
                // The first press starts the game; pressing "back" (into the tail) just starts it straight ahead.
                if (nd + _dir != Vector2Int.zero) _dir = nd;
                State = ArcadeState.Playing;
                _clock = 0f;
                send = true;
                return ArcadeState.EvStart;
            }
            if (State != ArcadeState.Playing) return 0;
            Vector2Int last = _turns.Count > 0 ? _turns[^1] : _dir;
            if (nd == last || nd + last == Vector2Int.zero || _turns.Count >= SnakeRules.TurnBuffer) return 0;
            _turns.Add(nd);
            return 0;
        }

        private Vector2Int FreeCell(Vector2Int prefer)
        {
            if (prefer.x >= 0 && !_snake.Contains(prefer)) return prefer;
            for (int tries = 0; tries < 300; tries++)
            {
                var f = new Vector2Int(_rnd.Next(1, W - 1), _rnd.Next(1, H - 1));
                if (!_snake.Contains(f) && f != _food && f != _bonus) return f;
            }
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                var f = new Vector2Int(x, y);
                if (!_snake.Contains(f) && f != _food && f != _bonus) return f;
            }
            return _food;
        }

        /// <summary>Advances the game by <paramref name="dt"/>; true when a frame should be broadcast (events in <paramref name="evt"/>).</summary>
        public bool Tick(float dt, out byte evt)
        {
            evt = 0;
            if (State != ArcadeState.Playing)
            {
                if (State == ArcadeState.Over) _deadFor += dt;
                // Heartbeat so late joiners and the screens' "still alive" check keep the frame.
                _beat += dt;
                return _beat > 1f;
            }
            if (_bonus != NoBonus)
            {
                _bonusLeft -= dt;
                if (_bonusLeft <= 0f) { _bonus = NoBonus; evt |= ArcadeState.EvBonusGone; }
            }
            _clock += dt;
            float step = SnakeRules.Step(_level);
            if (_clock < step) return evt != 0;
            _clock -= step;
            if (_turns.Count > 0)
            {
                _dir = _turns[0];
                _turns.RemoveAt(0);
                evt |= ArcadeState.EvTurn;
            }
            var head = _snake[0] + _dir;
            bool grows = _grow > 0 || head == _food || head == _bonus;
            int hit = _snake.IndexOf(head);
            bool crash = head.x < 0 || head.y < 0 || head.x >= W || head.y >= H || hit >= 0 && (grows || hit < _snake.Count - 1);
            if (crash)
            {
                State = ArcadeState.Over;
                _deadFor = 0f;
                _turns.Clear();
                evt |= ArcadeState.EvDie;
                return true;
            }
            _snake.Insert(0, head);
            if (head == _food)
            {
                _score++;
                _grow++;
                _eaten++;
                evt |= ArcadeState.EvEat;
                _food = FreeCell(NoBonus);
                if (_eaten % SnakeRules.BonusEvery == 0 && _bonus == NoBonus)
                {
                    _bonus = FreeCell(NoBonus);
                    _bonusLeft = SnakeRules.BonusTime;
                    evt |= ArcadeState.EvBonusSpawn;
                }
            }
            else if (head == _bonus)
            {
                _score += SnakeRules.BonusPoints;
                _grow += 2;
                _bonus = NoBonus;
                evt |= ArcadeState.EvBonusEat;
            }
            if (_grow > 0) _grow--;
            else _snake.RemoveAt(_snake.Count - 1);
            int lvl = SnakeRules.Level(_score);
            if (lvl > _level) { _level = lvl; evt |= ArcadeState.EvLevelUp; }
            return true;
        }

        /// <summary>Frame for <see cref="MatchManager.ArcadeStateRpc"/> (resets the heartbeat).</summary>
        public void Pack(out byte[] body, out byte foodX, out byte foodY, out byte bonusX, out byte bonusY, out byte bonusTenths, out ushort score,
            out byte dir, out byte level)
        {
            _beat = 0f;
            body = new byte[_snake.Count * 2];
            for (int i = 0; i < _snake.Count; i++)
            {
                body[i * 2] = (byte)_snake[i].x;
                body[i * 2 + 1] = (byte)_snake[i].y;
            }
            Vector2Int want = _turns.Count > 0 ? _turns[^1] : _dir;
            dir = (byte)System.Array.IndexOf(Dirs, want);
            bool bonus = _bonus != NoBonus;
            foodX = (byte)_food.x;
            foodY = (byte)_food.y;
            bonusX = bonus ? (byte)_bonus.x : (byte)255;
            bonusY = bonus ? (byte)_bonus.y : (byte)255;
            bonusTenths = (byte)Mathf.Clamp(Mathf.CeilToInt(_bonusLeft * 10f), 0, 255);
            score = (ushort)_score;
            level = (byte)_level;
        }
    }
}
