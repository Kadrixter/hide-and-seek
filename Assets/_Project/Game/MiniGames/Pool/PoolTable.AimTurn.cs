using HideAndSeek.Game.MiniGames.Pool;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>Local aim reset on every new turn / state, the first aim of a turn, the cue ball in hand and the cloth under the mouse.</summary>
    public sealed partial class PoolTable
    {
        partial void OnStateChanged()
        {
            bool fresh = State.ShotNo != _seenShot || State.Shooter != _seenTurn || State.Round != _seenRound
                         || State.Phase != _seenPhase || State.BallInHand != _seenHand;
            _seenShot = State.ShotNo;
            _seenTurn = State.Shooter;
            _seenRound = State.Round;
            _seenPhase = State.Phase;
            _seenHand = State.BallInHand;
            if (!fresh) return;
            _dragging = false;
            _pull = _syncPull = 0f;
            _awaitShot = false;
            _spin = Vector2.zero;
            _aimInit = false;
            _ptrOffset = 0f;
            _fineOn = false;
            _handMode = 0; // becomes 1 (cue ball in the hand) on the first own aiming frame with ball in hand
            _sendNow = true;
        }

        /// <summary>First aiming frame of a turn: point at the nearest ball that may be hit first.</summary>
        void EnsureAimInit()
        {
            if (_aimInit) return;
            _aimInit = true;
            int seat = LocalSeat;
            if (seat < 0) return;
            Vector2 cue = AimCue();
            ushort mask = PoolRules.Targets(State, seat);
            float best = float.MaxValue;
            for (int i = 1; i < 16; i++)
            {
                if (State.IsDown(i) || (mask & (1 << i)) == 0) continue;
                float d = (State.Pos[i] - cue).sqrMagnitude;
                if (d < best && d > 1e-6f) { best = d; _aimAngle = Mathf.Atan2(State.Pos[i].y - cue.y, State.Pos[i].x - cue.x); }
            }
            if (best == float.MaxValue) _aimAngle = cue.x < 0f ? 0f : Mathf.PI;
        }

        Vector2 StartHandPos()
        {
            Vector2 p = State.IsDown(0) ? new Vector2(PoolDims.HeadSpot.x, 0f) : State.Pos[0];
            PosWithout(p);
            return PoolSim.ClampCueBall(_scratch, (ushort)(State.Down | 1), p, State.Kitchen);
        }

        /// <summary>The balls with the cue ball at <paramref name="cue"/> (in <see cref="_scratch"/>).</summary>
        void PosWithout(Vector2 cue)
        {
            System.Array.Copy(State.Pos, _scratch, 16);
            _scratch[0] = cue;
        }

        /// <summary>Where a ray meets the plane of the ball centres (table 2D).</summary>
        bool HitCloth(Ray worldRay, out Vector2 p)
        {
            p = default;
            Vector3 o = transform.InverseTransformPoint(worldRay.origin), d = transform.InverseTransformDirection(worldRay.direction);
            if (Mathf.Abs(d.y) < 1e-5f) return false;
            float t = (PoolDims.ClothY + PoolDims.BallR - o.y) / d.y;
            if (t <= 0f) return false;
            Vector3 h = o + d * t;
            p = new Vector2(h.x, h.z);
            return true;
        }
    }
}
