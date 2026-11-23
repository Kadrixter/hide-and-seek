using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// SNAKE on the two lobby arcades: each cabinet has its own game (<see cref="SnakeSim"/>) simulated on the server,
    /// every step broadcast to all peers (everybody sees both games on the cabinet screens). Two players can play at
    /// the same time, one per cabinet; the record is shared (<see cref="ArcadeState.Best"/>).
    /// </summary>
    public partial class MatchManager
    {
        public const int SnakeW = SnakeRules.W, SnakeH = SnakeRules.H;
        private readonly SnakeSim[] _sims = { new(1234), new(98765) };

        /// <summary>Player at cabinet <paramref name="cab"/> (ulong.MaxValue = free).</summary>
        public ulong ArcadePlayerOf(int cab) => cab == 1 ? ArcadePlayer2.Value : ArcadePlayer.Value;

        /// <summary>Cabinet the player stands at, -1 = none.</summary>
        public int ArcadeCabOf(ulong player)
        {
            if (player == ulong.MaxValue) return -1;
            if (ArcadePlayer.Value == player) return 0;
            if (ArcadePlayer2.Value == player) return 1;
            return -1;
        }

        private void SetArcadePlayer(int cab, ulong id)
        {
            if (cab == 1) ArcadePlayer2.Value = id;
            else ArcadePlayer.Value = id;
        }

        public void ServerArcade(NetPlayer pl, bool start, int cab = 0)
        {
            if (pl == null || Phase.Value != MatchPhase.Lobby || cab < 0 || cab >= ArcadeCabs.Count) return;
            int mine = ArcadeCabOf(pl.NetworkObjectId);
            if (!start)
            {
                if (mine >= 0) StopArcade(mine);
                return;
            }
            if (mine >= 0 || ArcadePlayerOf(cab) != ulong.MaxValue || pl.Busy.Value || pl.Seat.Value >= 0) return;
            if (Vector3.Distance(pl.transform.position, ArcadeCabs.Stand(cab)) > 2.5f) return;
            SetArcadePlayer(cab, pl.NetworkObjectId);
            pl.TeleportRpc(ArcadeCabs.Stand(cab), ArcadeCabs.Yaw(cab));
            _sims[cab].Reset();
            SendSnake(cab, ArcadeState.EvStart);
        }

        public void ServerArcadeDir(NetPlayer pl, byte d)
        {
            int cab = pl != null ? ArcadeCabOf(pl.NetworkObjectId) : -1;
            if (cab < 0) return;
            byte evt = _sims[cab].Press(d, out bool send);
            if (send) SendSnake(cab, evt);
        }

        private void StopArcade(int cab)
        {
            SetArcadePlayer(cab, ulong.MaxValue);
            _sims[cab].Stop();
            ArcadeStateRpc((byte)cab, System.Array.Empty<byte>(), 0, 0, 255, 255, 0, 0, 0, ArcadeState.Idle, 1, 0);
        }

        /// <summary>Server, every frame in the lobby: ends games whose player left, runs the others.</summary>
        private void ArcadeTick(bool lobby)
        {
            for (int cab = 0; cab < ArcadeCabs.Count; cab++)
            {
                ulong id = ArcadePlayerOf(cab);
                if (id == ulong.MaxValue) continue;
                var ap = lobby ? NetPlayer.Find(id) : null;
                if (ap == null || Vector3.Distance(ap.transform.position, ArcadeCabs.Stand(cab)) > 1.5f) StopArcade(cab);
                else if (_sims[cab].Tick(Time.deltaTime, out byte evt)) SendSnake(cab, evt);
            }
        }

        private void SendSnake(int cab, byte evt)
        {
            var s = _sims[cab];
            s.Pack(out var body, out byte fx, out byte fy, out byte bx, out byte by, out byte bt, out ushort score, out byte dir, out byte level);
            ArcadeStateRpc((byte)cab, body, fx, fy, bx, by, bt, score, dir, s.State, level, evt);
        }

        /// <summary>Snake state of cabinet <paramref name="cab"/> for every client (see <see cref="ArcadeState"/> for states and event flags).</summary>
        [Rpc(SendTo.Everyone)]
        private void ArcadeStateRpc(byte cab, byte[] body, byte foodX, byte foodY, byte bonusX, byte bonusY, byte bonusTenths, ushort score, byte dir, byte state, byte level, byte evt)
            => ArcadeState.Of(cab).Apply(body, foodX, foodY, bonusX, bonusY, bonusTenths, score, dir, state, level, evt);
    }
}
