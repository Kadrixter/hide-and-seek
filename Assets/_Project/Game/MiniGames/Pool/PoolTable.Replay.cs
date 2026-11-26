using HideAndSeek.Game.MiniGames.Pool;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// Replay of a stroke on every peer. The server sends the stroke with the exact balls before it
    /// (<see cref="PoolShotMsg"/>); the same deterministic <see cref="PoolSim.Run"/> runs here (the host reuses the
    /// server's result) and the frames are played back at 60 per second, interpolated. A ball whose pocketed bit comes
    /// up drops into its pocket and down the ball return, the events sound where they happen. When the frames are over the balls wait
    /// for the server's state with this shot's number (it is published <c>Duration + 0.3 s</c> after the stroke), then
    /// <see cref="Rolling"/> ends and the balls settle onto it. A newer shot / a new rack / a lost state ends the replay.
    /// </summary>
    public sealed partial class PoolTable
    {
        /// <summary>Longest wait for the result after the frames ran out before the table stops waiting.</summary>
        const float ResultTimeout = 4f;

        PoolSimResult _rep;
        float _repT;
        int _repShot = -1, _repRound, _repEvent;
        bool _repFramesDone;
        /// <summary>Shot number the shown balls belong to (states older than this are not shown).</summary>
        int _shownShot = int.MinValue;
        /// <summary>Sum of the ball speeds in the current replay frame (m/s), for the rolling sound.</summary>
        float _rollSpeed;
        Vector2 _rollAt;

        /// <summary>Seat that struck the shot being replayed (-1 = none).</summary>
        public int ReplaySeat { get; private set; } = -1;
        /// <summary>Seconds since the replayed stroke.</summary>
        public float ReplayTime => _rep != null ? _repT : 0f;
        /// <summary>Simulated shot being replayed (null = none).</summary>
        public PoolSimResult ReplayResult => _rep;

        /// <summary>A stroke from the server (MatchManager.PoolShotClientRpc).</summary>
        public void OnShotMessage(PoolShotMsg msg)
        {
            if (msg.Pos == null || msg.Pos.Length < 16) return;
            if (_hasState && msg.ShotNo <= State.ShotNo) return; // its result is already here
            if (msg.ShotNo <= _repShot) return;
            if (Rolling) EndReplay(true);
            PoolSimResult r = null;
            var mm = MatchManager.Instance;
            if (mm != null && mm.IsServer && mm.PoolLastShotNo == msg.ShotNo) r = mm.PoolLastResult;
            if (r == null)
            {
                try { r = PoolSim.Run((Vector2[])msg.Pos.Clone(), msg.Down, msg.Shot); }
                catch (System.Exception e)
                {
                    Debug.LogError($"[Pool] replay of shot {msg.ShotNo} failed: {e}");
                    return;
                }
            }
            if (r == null || r.Frames.Count == 0) return;
            // Start from exactly the balls the server used.
            for (int i = 0; i < 16; i++)
            {
                if ((msg.Down & (1 << i)) != 0)
                {
                    if (_ret[i] < 0) PutInReturn(i, NearestPocket(msg.Pos[i]), false, CurrentLocal(i), _retClock);
                    continue;
                }
                if (_ret[i] >= 0)
                {
                    TakeOutOfReturn(i);
                    _pop[i] = 1f;
                    _hasPlaced[i] = false;
                }
                if ((msg.Pos[i] - ShownPos[i]).sqrMagnitude > 0.01f) _hasPlaced[i] = false;
                ShownPos[i] = msg.Pos[i];
            }
            HeldCuePos = null;
            _rep = r;
            _repT = 0f;
            _repShot = msg.ShotNo;
            _repRound = State.Round;
            _repEvent = 0;
            _repFramesDone = false;
            _shownShot = msg.ShotNo;
            ReplaySeat = msg.Seat;
            Rolling = true;
            // The state still is the one before this shot (the result is published after it): Break = this is the break.
            StrikeSound(msg.Power, msg.Pos[0], _hasState && State.Break && msg.Down == 0);
            OnShotStarted(msg.Seat, msg.Shot);
        }

        void TickReplay(float dt)
        {
            _rollSpeed = 0f;
            if (!Rolling) return;
            if (_rep == null) { EndReplay(false); return; }
            // A new rack or a later shot already published: this replay is out of date.
            if (_hasState && (State.Round != _repRound || State.ShotNo > _repShot)) { EndReplay(true); return; }
            _repT += dt;
            var frames = _rep.Frames;
            int n = frames.Count;
            float end = (n - 1) * PoolSimResult.FrameDt;
            float ft = Mathf.Min(_repT, end) / PoolSimResult.FrameDt;
            int f0 = Mathf.Clamp(Mathf.FloorToInt(ft), 0, n - 1), f1 = Mathf.Min(f0 + 1, n - 1);
            float k = Mathf.Clamp01(ft - f0);
            ShowFrame(f0, f1, k);
            PlayEventsUpTo(_repT);
            if (_repT >= end) _repFramesDone = true;
            if (_repFramesDone && (_hasState && State.ShotNo >= _repShot || _repT > end + ResultTimeout)) EndReplay(false);
        }

        /// <summary>Balls at frame f0..f1 (k between); balls whose pocketed bit came up start dropping into their pocket.</summary>
        void ShowFrame(int f0, int f1, float k)
        {
            var a = _rep.Frames[f0];
            var b = _rep.Frames[f1];
            ushort mask = f0 < _rep.DownMask.Count ? _rep.DownMask[f0] : (ushort)0;
            Vector2 sum = Vector2.zero;
            float speedSum = 0f;
            for (int i = 0; i < 16; i++)
            {
                if (_ret[i] >= 0) continue;
                if ((mask & (1 << i)) != 0)
                {
                    PutInReturn(i, PocketOf(i, a[i]), _hasPlaced[i], CurrentLocal(i), ReplayDropTime(i));
                    continue;
                }
                Vector2 p = Vector2.Lerp(a[i], b[i], k);
                ShownPos[i] = p;
                float v = (b[i] - a[i]).magnitude / PoolSimResult.FrameDt;
                if (v > 0.01f)
                {
                    speedSum += v;
                    sum += p * v;
                }
            }
            _rollSpeed = speedSum;
            if (speedSum > 0.01f) _rollAt = sum / speedSum;
        }

        /// <summary>Pocket ball <paramref name="ball"/> went into in this shot (from the sim's list, else the nearest one).</summary>
        int PocketOf(int ball, Vector2 at)
        {
            if (_rep != null)
                for (int j = 0; j < _rep.Pocketed.Count && j < _rep.PocketOf.Count; j++)
                    if (_rep.Pocketed[j] == ball && _rep.PocketOf[j] >= 0 && _rep.PocketOf[j] < 6) return _rep.PocketOf[j];
            return NearestPocket(at);
        }

        /// <summary>Stops the replay; <paramref name="jump"/> = put every ball at the shot's end at once (pocketed ones into the tray; those already in the return roll on).</summary>
        void EndReplay(bool jump)
        {
            if (jump && _rep != null)
            {
                var fin = _rep.FinalPos;
                for (int i = 0; i < 16; i++)
                {
                    if (_ret[i] >= 0) continue;
                    if ((_rep.FinalDown & (1 << i)) != 0) PutInReturn(i, PocketOf(i, fin[i]), false, CurrentLocal(i), _retClock);
                    else if (fin != null && i < fin.Length)
                    {
                        ShownPos[i] = fin[i];
                        _hasPlaced[i] = false;
                    }
                }
            }
            _rep = null;
            _repFramesDone = false;
            ReplaySeat = -1;
            Rolling = false;
        }
    }
}
