using UnityEngine;

namespace HideAndSeek.Game.MiniGames.Pool
{
    /// <summary>
    /// Working state of one simulated shot (used by <see cref="PoolSim.Run"/>). Fixed step of 1/960 s (16 per frame);
    /// inside a step the balls fly straight and every contact is found by its exact time (ball-ball, cushion, jaw),
    /// so nothing can tunnel. Cloth friction between the steps: sliding (mu 0.2) until the ball rolls, then rolling
    /// resistance (mu 0.011 + a small speed drag). Spin: w = "rolling velocity" of the contact point (w == v means
    /// pure rolling, so follow / draw come out of the sliding phase), wz = side spin (rad/s, + = counter-clockwise from
    /// above = right english), used at the cushions. No randomness, no frame time: every peer gets the same shot.
    /// </summary>
    internal sealed partial class PoolWorld
    {
        public const int N = 16, StepsPerFrame = 16;
        public const float Dt = PoolSimResult.FrameDt / StepsPerFrame;
        const float R = PoolDims.BallR, D = PoolDims.BallD, G = 9.81f;
        /// <summary>
        /// Cloth: sliding friction 0.2, rolling resistance 0.011 plus a small speed-proportional loss (nap, air) - a ball
        /// rolling at 1 m/s runs about 3.5 m (2026-11-25: longer, livelier rolls than the first tuning).
        /// </summary>
        const float MuSlide = 0.2f, MuRoll = 0.011f, Drag = 0.035f, SpinDecay = 40f;
        /// <summary>Ball speed below which a rolling ball stops (m/s).</summary>
        const float StopV = 0.005f;
        /// <summary>Spin per unit tip offset: a tip at half the radius gives 1.25 x the rolling spin (0.8 = natural roll).</summary>
        const float TipSpin = 1.25f;
        /// <summary>Hard cap of contacts resolved inside one step (a tight cluster may chain many).</summary>
        const int MaxEvents = 256;

        readonly PoolSimResult res;
        readonly Vector2[] p = new Vector2[N], v = new Vector2[N], w = new Vector2[N], prev = new Vector2[N];
        readonly float[] wz = new float[N];
        ushort down;
        float time;

        public PoolWorld(PoolSimResult r, Vector2[] pos, ushort startDown)
        {
            res = r;
            down = startDown;
            for (int i = 0; i < N; i++)
            {
                Vector2 q = pos != null && i < pos.Length ? pos[i] : Vector2.zero;
                p[i] = new Vector2(PoolState.Quant(q.x), PoolState.Quant(q.y));
            }
            for (int i = 0; i < lastPair.Length; i++) lastPair[i] = -1f;
            for (int i = 0; i < N; i++) lastRail[i] = -1f;
        }

        bool Active(int i) => (down & (1 << i)) == 0;
        bool Moves(int i) => v[i].x != 0f || v[i].y != 0f;
        bool Live(int i) => Moves(i) || w[i].x != 0f || w[i].y != 0f;

        void Stop(int i)
        {
            v[i] = Vector2.zero;
            w[i] = Vector2.zero;
            wz[i] = 0f;
        }

        /// <summary>The cue strikes the cue ball (speed from the power, spin from the tip offset).</summary>
        public void Strike(PoolShot s)
        {
            if (!Active(0)) return;
            float v0 = PoolSim.ShotSpeed(s.Power);
            float ang = float.IsNaN(s.Angle) || float.IsInfinity(s.Angle) ? 0f : s.Angle;
            Vector2 d = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
            float sx = Clean(s.SpinX), sy = Clean(s.SpinY), m = sx * sx + sy * sy;
            if (m > 1f)
            {
                m = Mathf.Sqrt(m);
                sx /= m;
                sy /= m;
            }
            v[0] = d * v0;
            w[0] = d * (TipSpin * sy * v0);
            wz[0] = TipSpin * sx * v0 / R;
            AddEvent(PoolEvent.Strike, 0, -1, v0, p[0]);
        }

        static float Clean(float x) => float.IsNaN(x) || float.IsInfinity(x) ? 0f : Mathf.Clamp(x, -1f, 1f);

        public bool AnyMoving()
        {
            for (int i = 0; i < N; i++)
                if (Active(i) && Live(i)) return true;
            return false;
        }

        public void StopAll()
        {
            for (int i = 0; i < N; i++) Stop(i);
        }

        public void AddFrame()
        {
            res.Frames.Add((Vector2[])p.Clone());
            res.DownMask.Add(down);
        }

        public void Finish()
        {
            System.Array.Copy(p, res.FinalPos, N);
            res.FinalDown = down;
            int n = 0;
            for (int i = 1; i < N; i++)
                if (railed[i]) n++;
            res.BallsToRail = n;
            res.Duration = Mathf.Max(0, res.Frames.Count - 1) * PoolSimResult.FrameDt;
        }

        /// <summary>One fixed step: friction, then straight flight with every contact resolved at its exact time.</summary>
        public void Step()
        {
            System.Array.Copy(p, prev, N);
            Friction(Dt);
            float left = Dt;
            int events = 0;
            while (left > 0f)
            {
                float best = left;
                int kind = 0, a = -1, b = -1;
                Vector2 n = Vector2.zero;
                bool jaw = false;
                if (events < MaxEvents)
                    for (int i = 0; i < N; i++)
                    {
                        if (!Active(i)) continue;
                        bool mi = Moves(i);
                        for (int j = i + 1; j < N; j++)
                        {
                            if (!Active(j) || (!mi && !Moves(j))) continue;
                            float t = PoolRails.Toi(p[j] - p[i], v[j] - v[i], D, best);
                            if (t < 0f || (kind != 0 && t >= best)) continue;
                            best = t;
                            kind = 1;
                            a = i;
                            b = j;
                        }
                        if (!mi || !PoolRails.NearRail(p[i], v[i], best)) continue;
                        if (PoolRails.Hit(p[i], v[i], ref best, out Vector2 cn, out bool cj))
                        {
                            kind = 2;
                            a = i;
                            n = cn;
                            jaw = cj;
                        }
                        for (int k = 0; k < 6; k++)
                        {
                            // The centre crossing the edge of a hole: the ball drops right there.
                            float t = PoolRails.Toi(p[i] - PoolRails.Hole[k], v[i], PoolRails.HoleR[k], best);
                            if (t < 0f || (kind != 0 && t >= best)) continue;
                            best = t;
                            kind = 3;
                            a = i;
                            b = k;
                        }
                    }
                if (kind == 0)
                {
                    Advance(left);
                    break;
                }
                Advance(best);
                left -= best;
                events++;
                float te = time + (Dt - left);
                if (kind == 1) Collide(a, b, te);
                else if (kind == 2) Bounce(a, n, jaw, te);
                else Pocket(a, b, te);
            }
            time += Dt;
            Separate();
            Pockets();
            Guard();
        }

        void Advance(float t)
        {
            if (t <= 0f) return;
            for (int i = 0; i < N; i++)
                if (Active(i) && Moves(i)) p[i] += v[i] * t;
        }

        /// <summary>Cloth friction over <paramref name="dt"/>: sliding until rolling, then rolling resistance; side spin decays.</summary>
        void Friction(float dt)
        {
            float slideDv = 3.5f * MuSlide * G * dt;
            for (int i = 0; i < N; i++)
            {
                if (!Active(i)) continue;
                if (!Live(i))
                {
                    wz[i] = 0f;
                    continue;
                }
                Vector2 vi = v[i], wi = w[i], u = vi - wi;
                float us = u.magnitude;
                if (us > 1e-5f)
                {
                    // Sliding: the contact point slips; friction slows the slip at 3.5 mu g until the ball rolls.
                    float f = us > slideDv ? 1f : us / slideDv;
                    Vector2 dv = u * (-MuSlide * G * dt * f / us);
                    vi += dv;
                    wi -= 2.5f * dv;
                    if (f < 1f) wi = vi;
                    if (vi.sqrMagnitude < StopV * StopV && wi.sqrMagnitude < StopV * StopV)
                    {
                        Stop(i);
                        continue;
                    }
                }
                else
                {
                    float sp = vi.magnitude, dec = (MuRoll * G + Drag * sp) * dt;
                    if (sp - dec < StopV)
                    {
                        Stop(i);
                        continue;
                    }
                    vi *= (sp - dec) / sp;
                    wi = vi;
                }
                v[i] = vi;
                w[i] = wi;
                float dz = SpinDecay * dt;
                wz[i] = Mathf.Abs(wz[i]) <= dz ? 0f : wz[i] - Mathf.Sign(wz[i]) * dz;
            }
        }
    }
}
