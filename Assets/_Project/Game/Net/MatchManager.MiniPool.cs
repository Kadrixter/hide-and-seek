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
    /// it takes the next stroke. Places, ready, leaving: MatchManager.MiniPoolSeats. The teams alternate innings; inside
    /// a team its players take the innings in turn (<see cref="PoolState.Up"/>). The round starting resets the table.
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

        public PoolState PoolServerState => _pool;
        /// <summary>Server: a stroke is being rolled out (no next stroke until it is published).</summary>
        public bool PoolShotPending => _poolPending != null;
        /// <summary>Server / host: the last accepted shot (the host's table reuses it instead of simulating twice).</summary>
        public PoolSimResult PoolLastResult { get; private set; }
        public int PoolLastShotNo { get; private set; } = -1;

        /// <summary>A change (somebody joined) waits for the shot being rolled out: its counter is already bumped, so the
        /// clients would take the old balls for the shot's result.</summary>
        private bool _poolPublishLater;

        private void PublishPool()
        {
            if (_poolPending != null)
            {
                _poolPublishLater = true;
                return;
            }
            _poolPublishLater = false;
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

        /// <summary>A fresh rack (places, series score, game / shot counters kept), cue ball in hand in the kitchen for the break.</summary>
        private void RackPool()
        {
            var keep = _pool.Clone();
            _pool.Rack(new System.Random(unchecked(System.Environment.TickCount * 31 + keep.Round * 7919)));
            _pool.A = keep.A;
            _pool.B = keep.B;
            _pool.A2 = keep.A2;
            _pool.B2 = keep.B2;
            _pool.WinsA = keep.WinsA;
            _pool.WinsB = keep.WinsB;
            _pool.Phase = keep.Phase;
            _pool.Turn = keep.Turn;
            _pool.Round = keep.Round + 1;
            _pool.ShotNo = keep.ShotNo;
            _pool.Ready = 0;
            _pool.Note = "";
        }

        private void ResetPool(bool clearSeats)
        {
            _poolPending = null;
            if (clearSeats)
            {
                for (int p = 0; p < PoolState.Places; p++) _pool.SetPlace(p, PoolState.Nobody);
                _pool.WinsA = _pool.WinsB = 0;
                _poolNextBreaker = -1;
                _poolBreakUp = _poolOpenUp = 0;
            }
            _pool.Phase = PoolState.Waiting;
            RackPool(); // a tidy table for the next players
            PublishPool();
        }

        private void StartPool(int breaker)
        {
            _poolPending = null;
            RackPool();
            _pool.Phase = PoolState.Playing;
            _pool.Turn = (byte)breaker;
            // Who of the two breaks / opens for the other team: alternates game by game inside each team.
            int other = 1 - breaker;
            _pool.Up = (byte)((_poolBreakUp & (1 << breaker)) | (_poolOpenUp & (1 << other)));
            _poolBreakUp ^= (byte)(1 << breaker);
            _poolOpenUp ^= (byte)(1 << other);
            _pool.Note = $"Rozbija {PoolNick(breaker)}";
            PublishPool();
            Debug.Log($"[Pool] game {_pool.Round}: A={NickOf(_pool.A)}+{NickOf(_pool.A2)} B={NickOf(_pool.B)}+{NickOf(_pool.B2)}, breaks {(breaker == 0 ? 'A' : 'B')} ({PoolNick(breaker)}), series {_pool.WinsA}:{_pool.WinsB}");
        }

        /// <summary>Nick of the player who plays team <paramref name="team"/>'s next inning.</summary>
        private string PoolNick(int team)
        {
            string n = NickOf(_pool.Place(_pool.ShooterPlace(team)));
            return string.IsNullOrEmpty(n) ? (team == 0 ? "drużyna A" : "drużyna B") : n;
        }

        private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

        /// <summary>Server: a shot from <paramref name="pl"/> (cue ball put at <paramref name="cuePos"/> first when he has ball in hand).</summary>
        public void ServerPoolShot(NetPlayer pl, PoolShot shot, Vector2 cuePos)
        {
            if (pl == null || !IsServer) return;
            int seat = _pool.PlaceOf(pl.NetworkObjectId);
            string why = Phase.Value != MatchPhase.Lobby ? "not in the lobby"
                : _pool.Phase != PoolState.Playing ? "no game running"
                : seat < 0 ? "not at the table"
                : seat != _pool.Shooter ? "not his turn"
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
            var sw = System.Diagnostics.Stopwatch.StartNew();
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
            Debug.Log($"[Pool] shot {_pool.ShotNo} by {pl.Nick}: angle {shot.Angle * Mathf.Rad2Deg:0.0}, power {shot.Power:0.00}, spin ({shot.SpinX:0.00}; {shot.SpinY:0.00}), {dur:0.0} s (simulated in {sw.ElapsedMilliseconds} ms)");
        }

        /// <summary>The only team with players when the other one is empty (a solo game, 2026-11-26), else -1.</summary>
        private int PoolSoloTeam => _pool.Members(0) == 0 ? (_pool.Members(1) > 0 ? 1 : -1) : _pool.Members(1) == 0 ? 0 : -1;

        /// <summary>
        /// Solo game: the turn never goes to the empty team (a miss or a foul just passes the cue to the team's other
        /// player, if it has two; a foul still gives ball in hand), the 8 potted early / with a foul is a loss.
        /// </summary>
        private void PoolSoloAfterShot(int solo)
        {
            if (_pool.Phase == PoolState.Over)
            {
                if (_pool.Winner != solo)
                    _pool.Note = (_pool.EndReason == PoolState.EndEightScratch ? "8 wbita z faulem" : "8 wbita za wcześnie") + " - przegrana, spróbuj jeszcze raz";
                return;
            }
            _pool.Turn = (byte)solo;
        }

        /// <summary>Applies the rolled-out shot: rules, next turn / game over, then (optionally) publishes.</summary>
        private void FinishPoolShot(bool publish)
        {
            var r = _poolPending;
            if (r == null) return;
            _poolPending = null;
            int before = _pool.Turn, solo = PoolSoloTeam;
            string nickA = PoolNick(0), nickB = PoolNick(1);
            if (solo == 0) nickB = nickA;
            else if (solo == 1) nickA = nickB;
            try { PoolRules.Apply(_pool, r, nickA, nickB); }
            catch (System.Exception e)
            {
                Debug.LogError($"[Pool] rules failed: {e}");
                System.Array.Copy(r.FinalPos, _pool.Pos, 16);
                _pool.Down = r.FinalDown;
                _pool.Turn = (byte)(1 - _pool.Turn);
            }
            // The team that leaves the table: its other player takes its next inning.
            if (_pool.Turn != before) _pool.Up ^= (byte)(1 << before);
            if (solo >= 0) PoolSoloAfterShot(solo);
            if (_pool.Phase == PoolState.Over)
            {
                _pool.Ready = 0;
                _poolNextBreaker = _pool.Winner <= 1 ? 1 - _pool.Winner : -1;
                Debug.Log($"[Pool] game {_pool.Round} over: winner {_pool.Winner}, reason {_pool.EndReason}, series {_pool.WinsA}:{_pool.WinsB}");
            }
            if (publish || _poolPublishLater) PublishPool();
        }

        private void PoolServerTick()
        {
            for (int place = 0; place < PoolState.Places; place++)
            {
                ulong id = _pool.Place(place);
                if (id != PoolState.Nobody && NetPlayer.Find(id) == null) PoolSeatLeft(place, true);
            }
            if (_poolPending != null && Time.unscaledTime >= _poolSettleAt) FinishPoolShot(true);
        }

        /// <summary>All clients: replay this stroke (the table runs the same simulation from the positions sent).</summary>
        [Rpc(SendTo.ClientsAndHost)]
        private void PoolShotClientRpc(PoolShotMsg msg) => MiniGameTables.Pool?.OnShotMessage(msg);
    }

    /// <summary>One stroke for the clients: the counter, who (place), the stroke, and the exact balls before it.</summary>
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
