using System.Collections;
using System.Collections.Generic;
using HideAndSeek.Audio;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Lobby easter eggs: the fork on the coffee table (into a wall socket: short circuit, electrocution, respawn)
    /// and the playable SNAKE arcade (server-simulated, state broadcast to everybody).
    /// </summary>
    public partial class MatchManager
    {
        public readonly NetworkVariable<ulong> ForkHolder = new(ulong.MaxValue);
        public readonly NetworkVariable<ulong> ArcadePlayer = new(ulong.MaxValue);

        public static readonly Vector3 ForkTablePos = new Vector3(-1.05f, 0.455f, 0.48f);
        public const float ForkTableYaw = 32f;

        /// <summary>Wall sockets in the lobby: plate centre and the direction it faces (into the room).</summary>
        public static readonly Vector3[] OutletPos =
        {
            new Vector3(-0.3f, 0.3f, 5.985f), new Vector3(-7.985f, 0.3f, -1.4f), new Vector3(7.985f, 0.3f, 1.8f), new Vector3(-2.6f, 0.3f, -5.985f),
        };
        public static readonly Vector3[] OutletNormal = { Vector3.back, Vector3.right, Vector3.left, Vector3.forward };

        public static readonly Vector3 ArcadeStand = new Vector3(6.0f, 0f, 4.6f);
        public static readonly Vector3 ArcadeStickBall = new Vector3(5.85f, 1.17f, 5.05f);
        public static readonly Vector3 ArcadeButtons = new Vector3(6.1f, 1.1f, 5.06f);
        public static readonly Vector3 ArcadeScreen = new Vector3(6.0f, 1.36f, 5.18f);

        public static bool InLobbyScene => SceneManager.GetActiveScene().name == NetSession.LobbyScene;

        private GameObject _tableFork;

        // ================================================================ per frame (all peers)

        private void UpdateLobbyFun()
        {
            bool lobby = Phase.Value == MatchPhase.Lobby && InLobbyScene;
            if (lobby) { LobbyBricks.Ensure(); Boombox.Ensure(); }
            // Fork lying on the table while nobody holds it.
            if (lobby && _tableFork == null)
            {
                var prop = BlockyProps.Create(HeldItem.Fork, null, 0);
                _tableFork = prop.gameObject;
                _tableFork.name = "TableFork";
                _tableFork.transform.SetPositionAndRotation(ForkTablePos, Quaternion.Euler(0f, ForkTableYaw, 0f));
            }
            if (_tableFork != null)
            {
                bool show = lobby && ForkHolder.Value == ulong.MaxValue;
                if (_tableFork.activeSelf != show) _tableFork.SetActive(show);
            }

            if (!IsServer) return;
            if (!lobby)
            {
                if (ForkHolder.Value != ulong.MaxValue) ForkHolder.Value = ulong.MaxValue;
                if (ArcadePlayer.Value != ulong.MaxValue) StopArcade();
                return;
            }
            if (ForkHolder.Value != ulong.MaxValue && NetPlayer.Find(ForkHolder.Value) == null) ForkHolder.Value = ulong.MaxValue;
            if (ArcadePlayer.Value != ulong.MaxValue)
            {
                var ap = NetPlayer.Find(ArcadePlayer.Value);
                if (ap == null || Vector3.Distance(ap.transform.position, ArcadeStand) > 1.5f) StopArcade();
                else SnakeTick();
            }
        }

        // ================================================================ fork

        public void ServerPickupFork(NetPlayer pl)
        {
            if (pl == null || Phase.Value != MatchPhase.Lobby || ForkHolder.Value != ulong.MaxValue || pl.Busy.Value) return;
            if (Vector3.Distance(pl.transform.position, ForkTablePos) > 2.4f) return;
            ForkHolder.Value = pl.NetworkObjectId;
            pl.Held.Value = HeldItem.Fork;
            pl.SfxRpc(GameSfx.UiClick, 0.6f);
        }

        public void ServerForkOutlet(NetPlayer pl, int idx)
        {
            if (pl == null || Phase.Value != MatchPhase.Lobby || ForkHolder.Value != pl.NetworkObjectId || pl.Busy.Value) return;
            if (idx < 0 || idx >= OutletPos.Length) return;
            if (Vector3.Distance(pl.transform.position, OutletPos[idx]) > 2.2f) return;
            StartCoroutine(ShockRoutine(pl, idx));
        }

        private IEnumerator ShockRoutine(NetPlayer pl, int idx)
        {
            pl.Busy.Value = true;
            Vector3 n = OutletNormal[idx];
            Vector3 stand = OutletPos[idx] + n * 0.62f;
            stand.y = 0f;
            float yaw = Quaternion.LookRotation(-n).eulerAngles.y;
            pl.TeleportRpc(stand, yaw);
            yield return new WaitForSeconds(0.1f);
            pl.ShockRpc((byte)idx);
            yield return new WaitForSeconds(ShockTimeline.Respawn);
            if (pl == null || !pl.IsSpawned) yield break;
            ForkHolder.Value = ulong.MaxValue;
            pl.Held.Value = HeldItem.None;
            var spawns = SpawnPoints.Lobby();
            if (spawns.Count > 0)
            {
                var sp = spawns[Random.Range(0, spawns.Count)];
                pl.TeleportRpc(sp.position, sp.eulerAngles.y);
            }
            pl.Busy.Value = false;
        }

        // ================================================================ exit door

        private bool _exitBusy;

        public void ServerExitDoor(NetPlayer pl)
        {
            if (pl == null || Phase.Value != MatchPhase.Lobby || !InLobbyScene || _exitBusy || pl.Busy.Value || pl.Seat.Value >= 0) return;
            if (ForkHolder.Value == pl.NetworkObjectId || ArcadePlayer.Value == pl.NetworkObjectId) return;
            var door = LobbyExitDoor.Instance;
            Vector3 handle = door != null ? door.HandleWorld : new Vector3(4.66f, 1f, -5.93f);
            if (Vector3.Distance(pl.transform.position + Vector3.up, handle) > 2.6f) return;
            StartCoroutine(ExitDoorRoutine(pl));
        }

        private IEnumerator ExitDoorRoutine(NetPlayer pl)
        {
            _exitBusy = true;
            pl.Busy.Value = true;
            pl.TeleportRpc(LobbyExitDoor.StandPos, LobbyExitDoor.StandYaw);
            yield return new WaitForSeconds(0.1f);
            if (pl != null && pl.IsSpawned) pl.ExitDoorRpc();
            yield return new WaitForSeconds(ExitTimeline.Respawn);
            _exitBusy = false;
            if (pl == null || !pl.IsSpawned) yield break;
            var spawns = SpawnPoints.Lobby();
            if (spawns.Count > 0)
            {
                var sp = spawns[Random.Range(0, spawns.Count)];
                pl.TeleportRpc(sp.position, sp.eulerAngles.y);
            }
            pl.Busy.Value = false;
        }

        // ================================================================ arcade (SNAKE)

        public const int SnakeW = 32, SnakeH = 21;
        private readonly List<Vector2Int> _snake = new();
        private Vector2Int _snakeDir, _snakeNext, _snakeFood;
        private int _snakeScore;
        private float _snakeClock;
        private byte _snakeState; // 0 playing, 1 crashed (showing score)
        private float _snakeDeadFor;
        private readonly System.Random _snakeRnd = new();

        private static readonly Vector2Int[] Dirs = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

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
            SendSnake(3);
        }

        public void ServerArcadeDir(NetPlayer pl, byte d)
        {
            if (pl == null || ArcadePlayer.Value != pl.NetworkObjectId || d > 3) return;
            var nd = Dirs[d];
            if (_snakeState == 1 && _snakeDeadFor > 1.2f)
            {
                ResetSnake();
                SendSnake(3);
                return;
            }
            if (nd + _snakeDir != Vector2Int.zero) _snakeNext = nd;
        }

        private void StopArcade()
        {
            ArcadePlayer.Value = ulong.MaxValue;
            ArcadeStateRpc(System.Array.Empty<byte>(), 0, 0, 0, 0, 2, 0);
        }

        private void ResetSnake()
        {
            _snake.Clear();
            for (int i = 0; i < 4; i++) _snake.Add(new Vector2Int(10 - i, SnakeH / 2));
            _snakeDir = _snakeNext = Vector2Int.right;
            _snakeScore = 0;
            _snakeState = 0;
            _snakeDeadFor = 0f;
            _snakeClock = 0f;
            PlaceFood();
        }

        private void PlaceFood()
        {
            for (int tries = 0; tries < 200; tries++)
            {
                var f = new Vector2Int(_snakeRnd.Next(1, SnakeW - 1), _snakeRnd.Next(1, SnakeH - 1));
                if (!_snake.Contains(f)) { _snakeFood = f; return; }
            }
        }

        private void SnakeTick()
        {
            if (_snakeState == 1)
            {
                _snakeDeadFor += Time.deltaTime;
                if (_snakeDeadFor > 6f) { ResetSnake(); SendSnake(3); }
                return;
            }
            _snakeClock += Time.deltaTime;
            float step = Mathf.Max(0.065f, 0.14f - _snakeScore * 0.004f);
            if (_snakeClock < step) return;
            _snakeClock -= step;
            _snakeDir = _snakeNext;
            var head = _snake[0] + _snakeDir;
            byte evt = 0;
            bool crash = head.x < 0 || head.y < 0 || head.x >= SnakeW || head.y >= SnakeH || _snake.IndexOf(head) is int hit && hit >= 0 && hit < _snake.Count - 1;
            if (crash)
            {
                _snakeState = 1;
                _snakeDeadFor = 0f;
                evt = 2;
            }
            else
            {
                _snake.Insert(0, head);
                if (head == _snakeFood)
                {
                    _snakeScore++;
                    evt = 1;
                    PlaceFood();
                }
                else _snake.RemoveAt(_snake.Count - 1);
            }
            SendSnake(evt);
        }

        private void SendSnake(byte evt)
        {
            var body = new byte[_snake.Count * 2];
            for (int i = 0; i < _snake.Count; i++)
            {
                body[i * 2] = (byte)_snake[i].x;
                body[i * 2 + 1] = (byte)_snake[i].y;
            }
            byte dir = (byte)System.Array.IndexOf(Dirs, _snakeNext);
            ArcadeStateRpc(body, (byte)_snakeFood.x, (byte)_snakeFood.y, (ushort)_snakeScore, dir, _snakeState, evt);
        }

        /// <summary>Snake state for every client's arcade screen. state: 0 playing, 1 crashed, 2 nobody playing.</summary>
        [Rpc(SendTo.Everyone)]
        private void ArcadeStateRpc(byte[] body, byte foodX, byte foodY, ushort score, byte dir, byte state, byte evt)
        {
            ArcadeState.Apply(body, foodX, foodY, score, dir, state);
            if (evt == 1) GameAudio.PlayAt(GameSfx.ArcadeEat, ArcadeScreen, 1f);
            else if (evt == 2) GameAudio.PlayAt(GameSfx.ArcadeDie, ArcadeScreen, 1f);
            else if (evt == 3) GameAudio.PlayAt(GameSfx.ArcadeStart, ArcadeScreen, 1f);
        }
    }

    /// <summary>Latest SNAKE state received from the server (drawn by <see cref="LobbyArcade"/>).</summary>
    public static class ArcadeState
    {
        public static bool Live;
        public static readonly List<Vector2Int> Body = new();
        public static Vector2Int Food;
        public static int Score;
        public static int Dir;
        private const string BestKey = "hns.snake.best";
        private static int _best = -1;

        /// <summary>Best SNAKE score seen on this computer, kept between game restarts.</summary>
        public static int Best
        {
            get
            {
                if (_best < 0) _best = PlayerPrefs.GetInt(BestKey, 0);
                return _best;
            }
            private set
            {
                _best = value;
                PlayerPrefs.SetInt(BestKey, value);
                PlayerPrefs.Save();
            }
        }
        public static bool Crashed;
        public static float Updated;

        public static void Apply(byte[] body, byte fx, byte fy, ushort score, byte dir, byte state)
        {
            Live = state != 2;
            Crashed = state == 1;
            Body.Clear();
            if (body != null)
                for (int i = 0; i + 1 < body.Length; i += 2) Body.Add(new Vector2Int(body[i], body[i + 1]));
            Food = new Vector2Int(fx, fy);
            Score = score;
            if (score > Best) Best = score;
            Dir = dir;
            Updated = Time.time;
        }
    }
}
