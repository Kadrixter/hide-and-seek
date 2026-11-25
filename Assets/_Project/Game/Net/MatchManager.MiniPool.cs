using HideAndSeek.Game.MiniGames;
using HideAndSeek.Game.MiniGames.Pool;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Lobby pool table (8-ball), refereed by the server. The whole table is one text network variable
    /// (<see cref="PoolNet"/> = <see cref="PoolState.Encode"/>), so late joiners get the balls at once. A shot: the
    /// shooter sends <see cref="NetPlayer.PoolShotRpc"/>, the server checks it, simulates it (<see cref="PoolSim.Run"/>,
    /// deterministic) and sends the stroke plus the exact ball positions before it to every client
    /// (<see cref="PoolShotClientRpc"/>); each client runs the same simulation and replays it. Only after the shot's
    /// duration (+ <see cref="PoolSettlePad"/>) does the server apply the rules and publish the new state, and only then
    /// it takes the next stroke. A player leaving / disconnecting ends the game (the other one wins), the round
    /// starting resets the table, R from both players after a game racks again (the loser breaks).
    /// </summary>
    public partial class MatchManager
    {
        public const byte GamePool = 2;
        /// <summary>Extra wait after the simulated shot before the result is published (the clients finish their replay first).</summary>
        private const float PoolSettlePad = 0.3f;

        public readonly NetworkVariable<FixedString512Bytes> PoolNet = new();

        private readonly PoolState _pool = new();
        private PoolSimResult _poolPending;
        private float _poolSettleAt;
        private int _poolBreaker;

        public PoolState PoolServerState => _pool;
        /// <summary>Server: a stroke is being rolled out (no next stroke until it is published).</summary>
        public bool PoolShotPending => _poolPending != null;
        /// <summary>Server / host: the last accepted shot (the host's table reuses it instead of simulating twice).</summary>
        public PoolSimResult PoolLastResult { get; private set; }
        public int PoolLastShotNo { get; private set; } = -1;

        private void PublishPool()
        {
            string s = _pool.Encode() ?? "";
            if (System.Text.Encoding.UTF8.GetByteCount(s) > 500 && !string.IsNullOrEmpty(_pool.Note))
            {
                Debug.LogWarning($"[Pool] state too long ({s.Length} chars), the note is dropped: {_pool.Note}");
                _pool.Note = "";
                s = _pool.Encode() ?? "";
            }
            // The server keeps exactly what the clients decode (positions quantised the same), so every peer
            // simulates the next shot from the same numbers.
            var check = new PoolState();
            if (PoolState.TryDecode(s, check)) PoolState.TryDecode(s, _pool);
            try { PoolNet.Value = new FixedString512Bytes(s); }
            catch (System.Exception e) { Debug.LogError($"[Pool] cannot publish the state ({s.Length} chars): {e.Message}"); }
        }

        /// <summary>A fresh rack (seats, series score, game / shot counters kept), cue ball in hand in the kitchen for the break.</summary>
        private void RackPool()
        {
            ulong a = _pool.A, b = _pool.B;
            byte wa = _pool.WinsA, wb = _pool.WinsB, phase = _pool.Phase, turn = _pool.Turn;
            int round = _pool.Round, shot = _pool.ShotNo;
            _pool.Rack(new System.Random(unchecked(System.Environment.TickCount * 31 + round * 7919)));
            _pool.A = a;
            _pool.B = b;
            _pool.WinsA = wa;
            _pool.WinsB = wb;
            _pool.Phase = phase;
            _pool.Turn = turn;
            _pool.Round = round + 1;
            _pool.ShotNo = shot;
            _pool.Groups = PoolState.Open;
            _pool.Break = true;
            _pool.BallInHand = true;
            _pool.Kitchen = true;
            _pool.Winner = 255;
            _pool.EndReason = PoolState.EndNone;
            _pool.Rematch = 0;
            _pool.Note = "";
        }

        private void ResetPool(bool clearSeats)
        {
            _poolPending = null;
            if (clearSeats)
            {
                _pool.A = _pool.B = PoolState.Nobody;
                _pool.WinsA = _pool.WinsB = 0;
            }
            _pool.Phase = PoolState.Waiting;
            RackPool(); // a tidy table for the next players
            PublishPool();
        }

        private void StartPool(int breaker)
        {
            _poolPending = null;
            _poolBreaker = breaker;
            RackPool();
            _pool.Phase = PoolState.Playing;
            _pool.Turn = (byte)breaker;
            _pool.Note = $"Rozbija {PoolNick(breaker)}";
            PublishPool();
            Debug.Log($"[Pool] game {_pool.Round}: A={NickOf(_pool.A)} B={NickOf(_pool.B)}, breaks {(breaker == 0 ? 'A' : 'B')}, series {_pool.WinsA}:{_pool.WinsB}");
        }

        private string PoolNick(int seat)
        {
            string n = NickOf(_pool.Seat(seat));
            return string.IsNullOrEmpty(n) ? (seat == 0 ? "gracz 1" : "gracz 2") : n;
        }

        /// <summary>ServerMiniSit, pool branch: takes the seat (false = taken); both seats taken = a new pair starts.</summary>
        private bool PoolSit(NetPlayer pl, byte seat)
        {
            if (_pool.Seat(seat) != PoolState.Nobody) return false;
            if (seat == 0) _pool.A = pl.NetworkObjectId; else _pool.B = pl.NetworkObjectId;
            _pool.Rematch = 0;
            if (_pool.A != PoolState.Nobody && _pool.B != PoolState.Nobody)
            {
                _pool.WinsA = _pool.WinsB = 0; // new pair of players
                StartPool(Random.Range(0, 2));
            }
            else PublishPool();
            return true;
        }

        /// <summary>ServerMiniRematch, pool branch: both players press R after a game, the loser breaks the new rack.</summary>
        private void PoolRematch(int seat)
        {
            if (seat < 0 || _pool.Phase != PoolState.Over || _pool.A == PoolState.Nobody || _pool.B == PoolState.Nobody) return;
            _pool.Rematch |= (byte)(1 << seat);
            if (_pool.Rematch == 3) StartPool(_pool.Winner <= 1 ? 1 - _pool.Winner : 1 - _poolBreaker);
            else PublishPool();
        }

        private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

        /// <summary>Server: a shot from <paramref name="pl"/> (cue ball put at <paramref name="cuePos"/> first when he has ball in hand).</summary>
        public void ServerPoolShot(NetPlayer pl, PoolShot shot, Vector2 cuePos)
        {
            if (pl == null || !IsServer) return;
            int seat = _pool.SeatOf(pl.NetworkObjectId);
            string why = Phase.Value != MatchPhase.Lobby ? "not in the lobby"
                : _pool.Phase != PoolState.Playing ? "no game running"
                : seat < 0 ? "not at the table"
                : seat != _pool.Turn ? "not his turn"
                : _poolPending != null ? "balls still rolling"
                : !Finite(shot.Angle) || !Finite(shot.Power) || !Finite(shot.SpinX) || !Finite(shot.SpinY) || shot.Power <= 0f || shot.Power > 1.05f ? "bad stroke"
                : null;
            if (why == null && _pool.BallInHand)
            {
                if (!Finite(cuePos.x) || !Finite(cuePos.y)) why = "bad cue ball spot";
                else if (!PoolSim.CanPlaceCueBall(_pool.Pos, _pool.Down, cuePos, _pool.Kitchen)) why = $"the cue ball cannot go to {cuePos}";
            }
            if (why != null)
            {
                Debug.Log($"[Pool] shot from {pl.Nick} refused: {why}");
                return;
            }
            shot.Power = Mathf.Clamp(shot.Power, 0.01f, 1f);
            var spin = Vector2.ClampMagnitude(new Vector2(shot.SpinX, shot.SpinY), 1f);
            shot.SpinX = spin.x;
            shot.SpinY = spin.y;
            shot = PoolSim.Quantize(shot); // what the simulation uses anyway; the clients get the same numbers
            if (_pool.BallInHand)
            {
                _pool.Pos[0] = cuePos;
                _pool.Down = (ushort)(_pool.Down & ~1);
            }
            var before = (Vector2[])_pool.Pos.Clone();
            ushort down = _pool.Down;
            PoolSimResult r;
            try { r = PoolSim.Run((Vector2[])before.Clone(), down, shot); }
            catch (System.Exception e)
            {
                Debug.LogError($"[Pool] simulation failed: {e}");
                return;
            }
            if (r == null) return;
            _pool.ShotNo++;
            _poolPending = r;
            PoolLastResult = r;
            PoolLastShotNo = _pool.ShotNo;
            float dur = Finite(r.Duration) ? Mathf.Clamp(r.Duration, 0f, 60f) : 0f;
            _poolSettleAt = Time.unscaledTime + dur + PoolSettlePad;
            PoolShotClientRpc(new PoolShotMsg
            {
                ShotNo = _pool.ShotNo, Seat = (byte)seat, Angle = shot.Angle, Power = shot.Power, SpinX = shot.SpinX, SpinY = shot.SpinY,
                Down = down, Pos = before,
            });
            Debug.Log($"[Pool] shot {_pool.ShotNo} by {pl.Nick}: angle {shot.Angle * Mathf.Rad2Deg:0.0}, power {shot.Power:0.00}, spin ({shot.SpinX:0.00}; {shot.SpinY:0.00}), {dur:0.0} s");
        }

        /// <summary>Applies the rolled-out shot: rules, next turn / game over, then (optionally) publishes.</summary>
        private void FinishPoolShot(bool publish)
        {
            var r = _poolPending;
            if (r == null) return;
            _poolPending = null;
            try { PoolRules.Apply(_pool, r, PoolNick(0), PoolNick(1)); }
            catch (System.Exception e)
            {
                Debug.LogError($"[Pool] rules failed: {e}");
                System.Array.Copy(r.FinalPos, _pool.Pos, 16);
                _pool.Down = r.FinalDown;
                _pool.Turn = (byte)(1 - _pool.Turn);
            }
            if (_pool.Phase == PoolState.Over)
            {
                _pool.Rematch = 0;
                Debug.Log($"[Pool] game {_pool.Round} over: winner {_pool.Winner}, reason {_pool.EndReason}, series {_pool.WinsA}:{_pool.WinsB}");
            }
            if (publish) PublishPool();
        }

        /// <summary>A player got up / disconnected: a running game is lost by him, the seat is freed.</summary>
        private void PoolSeatLeft(int seat, bool disconnected)
        {
            if (seat < 0) return;
            string nick = PoolNick(seat);
            FinishPoolShot(false);
            if (_pool.Phase == PoolState.Playing)
            {
                _pool.Phase = PoolState.Over;
                _pool.EndReason = PoolState.EndLeft;
                _pool.Winner = (byte)(1 - seat);
                _pool.Note = disconnected ? $"{nick} rozłączył się" : $"{nick} odszedł od stołu";
            }
            if (seat == 0) _pool.A = PoolState.Nobody; else _pool.B = PoolState.Nobody;
            _pool.WinsA = _pool.WinsB = 0;
            _pool.Rematch = 0;
            if (_pool.A == PoolState.Nobody && _pool.B == PoolState.Nobody) ResetPool(true);
            else PublishPool();
        }

        private void PoolServerTick()
        {
            for (int seat = 0; seat < 2; seat++)
            {
                ulong id = _pool.Seat(seat);
                if (id != PoolState.Nobody && NetPlayer.Find(id) == null) PoolSeatLeft(seat, true);
            }
            if (_poolPending != null && Time.unscaledTime >= _poolSettleAt) FinishPoolShot(true);
        }

        /// <summary>All clients: replay this stroke (the table runs the same simulation from the positions sent).</summary>
        [Rpc(SendTo.ClientsAndHost)]
        private void PoolShotClientRpc(PoolShotMsg msg) => MiniGameTables.Pool?.OnShotMessage(msg);
    }

    /// <summary>One stroke for the clients: the counter, who, the stroke, and the exact balls before it.</summary>
    public struct PoolShotMsg : INetworkSerializable
    {
        public int ShotNo;
        public byte Seat;
        public float Angle, Power, SpinX, SpinY;
        public ushort Down;
        public Vector2[] Pos;

        public PoolShot Shot => new PoolShot(Angle, Power, SpinX, SpinY);

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref ShotNo);
            s.SerializeValue(ref Seat);
            s.SerializeValue(ref Angle);
            s.SerializeValue(ref Power);
            s.SerializeValue(ref SpinX);
            s.SerializeValue(ref SpinY);
            s.SerializeValue(ref Down);
            if (s.IsReader || Pos == null || Pos.Length != 16)
            {
                var old = Pos;
                Pos = new Vector2[16];
                if (!s.IsReader && old != null) System.Array.Copy(old, Pos, Mathf.Min(16, old.Length));
            }
            for (int i = 0; i < 16; i++) s.SerializeValue(ref Pos[i]);
        }
    }
}
