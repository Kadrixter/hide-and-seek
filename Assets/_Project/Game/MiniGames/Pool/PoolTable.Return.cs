using System.Collections.Generic;
using HideAndSeek.Game.MiniGames.Pool;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// Pocketed balls in the ball return (geometry: <see cref="PoolReturnTrack"/>). A ball runs into the hole, falls
    /// through the funnel and the cage onto its side's gully, bounces once on the rods and rolls down to the tray, where
    /// it stops against the divider or behind the balls already there. Everything is a pure function of the return
    /// clock: each ball keeps only its pocket and the moment it dropped (the replay's own pocket time, the same on every
    /// peer), its free run is <see cref="RollDist"/>, and a side's balls form a queue in the order they would reach the
    /// tray, each kept a ball's width behind the one before it. Taking a ball out (the cue ball picked up for ball in
    /// hand, a new rack) lets the balls behind it roll up, at most <see cref="RetMaxV"/> m/s.
    /// </summary>
    public sealed partial class PoolTable
    {
        const float FallInto = 0.12f, Gravity = 9.81f;
        /// <summary>Speed on the rods just after landing, the steady speed down the gentle slope, how fast it settles.</summary>
        const float RetV0 = 0.95f, RetVc = 0.55f, RetTau = 0.7f, RetMaxV = 1.3f;
        const byte PhaseHole = 0, PhaseFall = 1, PhaseRails = 2;

        /// <summary>Pocket a ball went into (-1 = on the cloth).</summary>
        readonly int[] _ret = new int[16];
        readonly int[] _retSeq = new int[16];
        readonly float[] _retT0 = new float[16], _retKey = new float[16], _retR = new float[16], _retSpeed = new float[16], _retLandT = new float[16];
        readonly byte[] _retPhase = new byte[16];
        readonly bool[] _retFresh = new bool[16];
        readonly Vector3[] _retFrom = new Vector3[16];
        readonly List<int> _retOrder = new(16);
        /// <summary>Return clock (seconds, advanced every frame before the replay).</summary>
        float _retClock;
        int _retSeqNext;
        /// <summary>Sum of the speeds of the balls rolling on the rods and where they roll (table space), for the sound.</summary>
        float _retRollSpeed;
        Vector3 _retRollAt;

        /// <summary>Distance a ball has rolled on the rods <paramref name="t"/> seconds after landing (free run).</summary>
        static float RollDist(float t) => RetVc * t + (RetV0 - RetVc) * RetTau * (1f - Mathf.Exp(-t / RetTau));

        /// <summary>Inverse of <see cref="RollDist"/> (bisection, deterministic).</summary>
        static float RollTime(float d)
        {
            float lo = 0f, hi = d / RetVc + 1f;
            for (int k = 0; k < 40; k++)
            {
                float mid = (lo + hi) * 0.5f;
                if (RollDist(mid) < d) lo = mid; else hi = mid;
            }
            return hi;
        }

        static float FallTime(int pocket) => Mathf.Sqrt(2f * Mathf.Max(0.01f, PoolReturnTrack.MouthY - PoolReturnTrack.Landing(pocket).y) / Gravity);

        /// <summary>Distance left to the stop from where a ball from <paramref name="pocket"/> lands.</summary>
        static float LandR(int pocket) => PoolReturnTrack.Gully(PoolReturnTrack.SideOf(pocket)).Length - PoolReturnTrack.LandU(pocket);

        /// <summary>Free state of ball i at the return clock: its phase, the distance left to the stop, seconds since landing.</summary>
        float FreeR(int i, out byte phase, out float landT)
        {
            int p = _ret[i];
            float t = _retClock - _retT0[i], fall = FallTime(p), r0 = LandR(p);
            landT = t - FallInto - fall;
            if (t < FallInto) { phase = PhaseHole; return r0; }
            if (landT < 0f) { phase = PhaseFall; return r0; }
            phase = PhaseRails;
            return Mathf.Max(0f, r0 - RollDist(landT));
        }

        /// <summary>
        /// Puts ball i into the return: dropping into <paramref name="pocket"/> from <paramref name="from"/> (table space)
        /// at return time <paramref name="t0"/>, or (<paramref name="animate"/> false) lying in the tray at once.
        /// </summary>
        void PutInReturn(int i, int pocket, bool animate, Vector3 from, float t0)
        {
            if (_ret[i] >= 0) return;
            pocket = Mathf.Clamp(pocket, 0, 5);
            _ret[i] = pocket;
            _retSeq[i] = _retSeqNext++;
            _retT0[i] = animate ? t0 : -100000f;
            _retKey[i] = _retT0[i] + FallInto + FallTime(pocket) + RollTime(LandR(pocket));
            _retFrom[i] = from;
            _retR[i] = FreeR(i, out _retPhase[i], out _retLandT[i]);
            _retSpeed[i] = 0f;
            _retFresh[i] = true;
        }

        /// <summary>Return time at which ball i dropped in the replay being shown (its pocket event), else now.</summary>
        float ReplayDropTime(int i)
        {
            if (_rep != null)
                foreach (var e in _rep.Events)
                    if (e.Kind == PoolEvent.Pocket && e.A == i) return _retClock - _repT + e.T;
            return _retClock;
        }

        void TakeOutOfReturn(int i) => _ret[i] = -1;

        int CompareQueue(int a, int b)
        {
            int c = _retKey[a].CompareTo(_retKey[b]);
            return c != 0 ? c : _retSeq[a].CompareTo(_retSeq[b]);
        }

        /// <summary>Every frame: the queue of each side (shown distance to the stop, speeds), knocks and the rolling sound.</summary>
        void TickReturn(float dt)
        {
            _retRollSpeed = 0f;
            Vector3 rollSum = Vector3.zero;
            float step = RetMaxV * Mathf.Max(dt, 1e-4f);
            for (int side = 0; side < 2; side++)
            {
                _retOrder.Clear();
                for (int i = 0; i < 16; i++)
                    if (_ret[i] >= 0 && PoolReturnTrack.SideOf(_ret[i]) == side) _retOrder.Add(i);
                _retOrder.Sort(CompareQueue);
                float prev = -1f;
                for (int k = 0; k < _retOrder.Count; k++)
                {
                    int i = _retOrder[k];
                    byte was = _retPhase[i];
                    float target = FreeR(i, out byte phase, out float landT);
                    if (phase == PhaseRails && prev >= 0f) target = Mathf.Max(target, prev + PoolDims.BallD);
                    float shown = _retFresh[i] || target >= _retR[i] - step ? target : _retR[i] - step;
                    float speed = _retFresh[i] ? 0f : Mathf.Max(0f, (_retR[i] - shown) / Mathf.Max(dt, 1e-4f));
                    if (!_retFresh[i] && phase == PhaseRails)
                    {
                        if (was == PhaseFall) ReturnLanded(i);
                        else if (_retSpeed[i] > 0.08f && speed < 0.01f) ReturnKnock(i, k == 0 && shown < 0.001f, _retSpeed[i]);
                    }
                    _retR[i] = shown;
                    _retSpeed[i] = speed;
                    _retPhase[i] = phase;
                    _retLandT[i] = landT;
                    _retFresh[i] = false;
                    prev = shown;
                    if (phase == PhaseRails && speed > 0.02f)
                    {
                        _retRollSpeed += speed;
                        rollSum += ReturnPos(i) * speed;
                    }
                }
            }
            if (_retRollSpeed > 0.01f) _retRollAt = rollSum / _retRollSpeed;
        }

        /// <summary>Where ball i in the return is drawn (table space).</summary>
        Vector3 ReturnPos(int i)
        {
            int p = _ret[i];
            Vector2 plan = PoolReturnTrack.DropPlan(p);
            Vector3 mouth = new Vector3(plan.x, PoolReturnTrack.MouthY, plan.y);
            float t = _retClock - _retT0[i];
            switch (_retPhase[i])
            {
                case PhaseHole:
                {
                    float k = Mathf.Clamp01(t / FallInto), e = 1f - (1f - k) * (1f - k);
                    var from = _retFrom[i];
                    return new Vector3(Mathf.Lerp(from.x, mouth.x, e), Mathf.Lerp(from.y, mouth.y, k * k), Mathf.Lerp(from.z, mouth.z, e));
                }
                case PhaseFall:
                {
                    float tf = Mathf.Max(0f, t - FallInto);
                    return mouth + Vector3.down * Mathf.Min(0.5f * Gravity * tf * tf, PoolReturnTrack.MouthY - PoolReturnTrack.Landing(p).y);
                }
            }
            var l = PoolReturnTrack.Gully(PoolReturnTrack.SideOf(p));
            return PoolReturnTrack.At(l, l.Length - _retR[i]) + Vector3.up * Hop(_retLandT[i]);
        }

        /// <summary>The bounce of a ball landing on the rods: one hop of a centimetre and a little one after it.</summary>
        static float Hop(float t)
        {
            if (t < 0f || t > 0.22f) return 0f;
            return t < 0.13f ? 0.010f * Mathf.Sin(Mathf.PI * t / 0.13f) : 0.0028f * Mathf.Sin(Mathf.PI * (t - 0.13f) / 0.09f);
        }

        /// <summary>
        /// Ball in hand after a scratch: the cue ball stays in the return (rolling on down to the tray) until the shooter
        /// picks it up; from that moment it leaves the return at once and is in his hand on the cloth.
        /// </summary>
        bool KeepCueInReturn()
        {
            if (!Playing || !State.BallInHand || State.Turn < 0 || State.Turn > 1) return false;
            var v = _view[State.Turn];
            return v.Player != SeatPlayer(State.Turn) || v.HandMode == 0;
        }
    }
}
