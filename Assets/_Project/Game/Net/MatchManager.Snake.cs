using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// SNAKE on the lobby arcade: simulated on the server, every step broadcast to all peers (everybody sees the game
    /// on the cabinet screen). States: Ready ("press a direction") -> Playing -> Over (score / record) -> next press
    /// starts again. Turns are buffered (up to <see cref="SnakeRules.TurnBuffer"/>), a turn into the snake's own neck
    /// is ignored, the speed goes up every 5 points, a golden bonus food appears for a few seconds after every 5 foods.
    /// </summary>
    public partial class MatchManager
    {
        public const int SnakeW = SnakeRules.W, SnakeH = SnakeRules.H;
        private readonly List<Vector2Int> _snake = new();
        private readonly List<Vector2Int> _snakeTurns = new();
        private Vector2Int _snakeDir, _snakeFood, _snakeBonus;
        private int _snakeScore, _snakeGrow, _snakeEaten, _snakeLevel;
        private float _snakeClock, _snakeBonusLeft, _snakeDeadFor, _snakeBeat;
        private byte _snakeState = ArcadeState.Idle;
        private readonly System.Random _snakeRnd = new();

        private static readonly Vector2Int[] Dirs = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
        private static readonly Vector2Int NoBonus = new(-1, -1);

        public void ServerArcade(NetPlayer pl, bool start)
        {
            if (pl == null || Phase.Value != MatchPhase.Lobby) return;
            if (!start)
            {
                if (ArcadePlayer.Value == pl.NetworkObjectId) StopArcade();
                return;
            }
            if (ArcadePlayer.Value != ulong.MaxValue || pl.Busy.Value || pl.Seat.Value >= 0) return;
            if (Vector3.Distance(pl.transform.position, ArcadeStand) > 2.5f) return;
            ArcadePlayer.Value = pl.NetworkObjectId;
            pl.TeleportRpc(ArcadeStand, 0f);
            ResetSnake();
            SendSnake(ArcadeState.EvStart);
        }

        public void ServerArcadeDir(NetPlayer pl, byte d)
        {
            if (pl == null || ArcadePlayer.Value != pl.NetworkObjectId || d > 3) return;
            var nd = Dirs[d];
            if (_snakeState == ArcadeState.Over)
            {
                if (_snakeDeadFor < 1.0f) return;
                ResetSnake();
            }
            if (_snakeState == ArcadeState.Ready)
            {
                // The first press starts the game; pressing "back" (into the tail) just starts it straight ahead.
                if (nd + _snakeDir != Vector2Int.zero) _snakeDir = nd;
                _snakeState = ArcadeState.Playing;
                _snakeClock = 0f;
                SendSnake(ArcadeState.EvStart);
                return;
            }
            if (_snakeState != ArcadeState.Playing) return;
            Vector2Int last = _snakeTurns.Count > 0 ? _snakeTurns[^1] : _snakeDir;
            if (nd == last || nd + last == Vector2Int.zero || _snakeTurns.Count >= SnakeRules.TurnBuffer) return;
            _snakeTurns.Add(nd);
        }

        private void StopArcade()
        {
            ArcadePlayer.Value = ulong.MaxValue;
            _snakeState = ArcadeState.Idle;
            ArcadeStateRpc(System.Array.Empty<byte>(), 0, 0, 255, 255, 0, 0, 0, ArcadeState.Idle, 1, 0);
        }

        private void ResetSnake()
        {
            _snake.Clear();
            for (int i = 0; i < SnakeRules.StartLength; i++) _snake.Add(new Vector2Int(SnakeW / 2 - 4 - i, 6)); // lower half: the start panel covers the top
            _snakeDir = Vector2Int.right;
            _snakeTurns.Clear();
            _snakeScore = _snakeGrow = _snakeEaten = 0;
            _snakeLevel = 1;
            _snakeState = ArcadeState.Ready;
            _snakeDeadFor = _snakeClock = _snakeBonusLeft = 0f;
            _snakeBonus = NoBonus;
            _snakeFood = FreeCell(new Vector2Int(SnakeW / 2 + 5, 6));
        }

        private Vector2Int FreeCell(Vector2Int prefer)
        {
            if (prefer.x >= 0 && !_snake.Contains(prefer)) return prefer;
            for (int tries = 0; tries < 300; tries++)
            {
                var f = new Vector2Int(_snakeRnd.Next(1, SnakeW - 1), _snakeRnd.Next(1, SnakeH - 1));
                if (!_snake.Contains(f) && f != _snakeFood && f != _snakeBonus) return f;
            }
            for (int y = 0; y < SnakeH; y++)
            for (int x = 0; x < SnakeW; x++)
            {
                var f = new Vector2Int(x, y);
                if (!_snake.Contains(f) && f != _snakeFood && f != _snakeBonus) return f;
            }
            return _snakeFood;
        }

        /// <summary>Server, every frame while somebody stands at the arcade.</summary>
        private void SnakeTick()
        {
            float dt = Time.deltaTime;
            if (_snakeState != ArcadeState.Playing)
            {
                if (_snakeState == ArcadeState.Over) _snakeDeadFor += dt;
                // Heartbeat so late joiners and the screens' "still alive" check keep the frame.
                _snakeBeat += dt;
                if (_snakeBeat > 1f) SendSnake(0);
                return;
            }
            byte evt = 0;
            if (_snakeBonus != NoBonus)
            {
                _snakeBonusLeft -= dt;
                if (_snakeBonusLeft <= 0f) { _snakeBonus = NoBonus; evt |= ArcadeState.EvBonusGone; }
            }
            _snakeClock += dt;
            float step = SnakeRules.Step(_snakeLevel);
            if (_snakeClock < step)
            {
                if (evt != 0) SendSnake(evt);
                return;
            }
            _snakeClock -= step;
            if (_snakeTurns.Count > 0)
            {
                _snakeDir = _snakeTurns[0];
                _snakeTurns.RemoveAt(0);
                evt |= ArcadeState.EvTurn;
            }
            var head = _snake[0] + _snakeDir;
            bool grows = _snakeGrow > 0 || head == _snakeFood || head == _snakeBonus;
            int hit = _snake.IndexOf(head);
            bool crash = head.x < 0 || head.y < 0 || head.x >= SnakeW || head.y >= SnakeH || hit >= 0 && (grows || hit < _snake.Count - 1);
            if (crash)
            {
                _snakeState = ArcadeState.Over;
                _snakeDeadFor = 0f;
                _snakeTurns.Clear();
                SendSnake((byte)(evt | ArcadeState.EvDie));
                return;
            }
            _snake.Insert(0, head);
            if (head == _snakeFood)
            {
                _snakeScore++;
                _snakeGrow++;
                _snakeEaten++;
                evt |= ArcadeState.EvEat;
                _snakeFood = FreeCell(NoBonus);
                if (_snakeEaten % SnakeRules.BonusEvery == 0 && _snakeBonus == NoBonus)
                {
                    _snakeBonus = FreeCell(NoBonus);
                    _snakeBonusLeft = SnakeRules.BonusTime;
                    evt |= ArcadeState.EvBonusSpawn;
                }
            }
            else if (head == _snakeBonus)
            {
                _snakeScore += SnakeRules.BonusPoints;
                _snakeGrow += 2;
                _snakeBonus = NoBonus;
                evt |= ArcadeState.EvBonusEat;
            }
            if (_snakeGrow > 0) _snakeGrow--;
            else _snake.RemoveAt(_snake.Count - 1);
            int lvl = SnakeRules.Level(_snakeScore);
            if (lvl > _snakeLevel) { _snakeLevel = lvl; evt |= ArcadeState.EvLevelUp; }
            SendSnake(evt);
        }

        private void SendSnake(byte evt)
        {
            _snakeBeat = 0f;
            var body = new byte[_snake.Count * 2];
            for (int i = 0; i < _snake.Count; i++)
            {
                body[i * 2] = (byte)_snake[i].x;
                body[i * 2 + 1] = (byte)_snake[i].y;
            }
            Vector2Int want = _snakeTurns.Count > 0 ? _snakeTurns[^1] : _snakeDir;
            byte dir = (byte)System.Array.IndexOf(Dirs, want);
            bool bonus = _snakeBonus != NoBonus;
            ArcadeStateRpc(body, (byte)_snakeFood.x, (byte)_snakeFood.y, bonus ? (byte)_snakeBonus.x : (byte)255, bonus ? (byte)_snakeBonus.y : (byte)255,
                (byte)Mathf.Clamp(Mathf.CeilToInt(_snakeBonusLeft * 10f), 0, 255), (ushort)_snakeScore, dir, _snakeState, (byte)_snakeLevel, evt);
        }

        /// <summary>Snake state for every client's arcade screen (see <see cref="ArcadeState"/> for states and event flags).</summary>
        [Rpc(SendTo.Everyone)]
        private void ArcadeStateRpc(byte[] body, byte foodX, byte foodY, byte bonusX, byte bonusY, byte bonusTenths, ushort score, byte dir, byte state, byte level, byte evt)
            => ArcadeState.Apply(body, foodX, foodY, bonusX, bonusY, bonusTenths, score, dir, state, level, evt);
    }
}
