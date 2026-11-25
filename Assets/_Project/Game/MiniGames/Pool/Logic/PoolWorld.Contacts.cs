using UnityEngine;

namespace HideAndSeek.Game.MiniGames.Pool
{
    /// <summary>Contacts of <see cref="PoolWorld"/>: ball-ball, cushions / jaws, pockets, events and safety nets.</summary>
    internal sealed partial class PoolWorld
    {
        /// <summary>Ball-ball restitution (equal masses, no throw).</summary>
        const float EBall = 0.95f;
        /// <summary>Cushion friction (side spin grip) and the extra loss of the jaws.</summary>
        const float MuCushion = 0.2f, JawLoss = 0.85f;
        /// <summary>Sound events: minimum gap per pair / per ball (s) and minimum impact speed (m/s).</summary>
        const float SoundGap = 0.05f, SoundMin = 0.02f;

        readonly float[] lastPair = new float[N * N], lastRail = new float[N];
        readonly bool[] railed = new bool[N];

        /// <summary>Cushion restitution: livelier for soft hits (0.82), deader for hard ones (0.74 from 3 m/s).</summary>
        static float CushionE(float speed) => 0.82f - 0.08f * Mathf.Clamp01(speed / 3f);

        void AddEvent(byte kind, int a, int b, float speed, Vector2 at)
        {
            res.Events.Add(new PoolEvent { T = Mathf.Max(0f, time), Kind = kind, A = a, B = b, Speed = speed, At = at });
        }

        /// <summary>Balls a and b (a &lt; b) touch: impulse along the line of centres, spins untouched.</summary>
        void Collide(int a, int b, float te)
        {
            Vector2 d = p[b] - p[a];
            float len = d.magnitude;
            Vector2 n = len > 1e-9f ? d / len : Vector2.right;
            float rel = Vector2.Dot(v[a] - v[b], n);
            if (rel <= 0f) return;
            if (a == 0 && res.FirstHit < 0) res.FirstHit = b;
            int cn = Cluster(a, b);
            if (cn > 2)
            {
                // Touching balls (the rack, a frozen pair): the impact runs through all of them at once.
                SolveCluster(cn, te);
                return;
            }
            float j = 0.5f * (1f + EBall) * rel;
            v[a] -= n * j;
            v[b] += n * j;
            int k = a * N + b;
            if (rel >= SoundMin && te - lastPair[k] >= SoundGap)
            {
                lastPair[k] = te;
                res.Events.Add(new PoolEvent { T = te, Kind = PoolEvent.BallBall, A = a, B = b, Speed = rel, At = p[a] + n * R });
            }
        }

        /// <summary>
        /// Ball i hits a cushion / jaw with normal n (towards the ball): speed-dependent restitution along n, cushion
        /// friction along it (side spin turns into a sideways kick), the rolling spin into the cushion is absorbed.
        /// </summary>
        void Bounce(int i, Vector2 n, bool jaw, float te)
        {
            float vin = -Vector2.Dot(v[i], n);
            if (vin <= 0f) return;
            float e = CushionE(vin) * (jaw ? JawLoss : 1f);
            float jn = (1f + e) * vin;
            Vector2 t = new Vector2(-n.y, n.x);
            float slip = Vector2.Dot(v[i], t) - R * wz[i];
            float jt = Mathf.Clamp(slip / 3.5f, -MuCushion * jn, MuCushion * jn);
            v[i] += n * jn - t * jt;
            wz[i] += 2.5f * jt / R;
            w[i] -= n * Vector2.Dot(w[i], n);
            railed[i] = true;
            if (res.FirstHit >= 0) res.RailAfterContact = true;
            if (vin >= SoundMin && te - lastRail[i] >= SoundGap)
            {
                lastRail[i] = te;
                res.Events.Add(new PoolEvent { T = te, Kind = jaw ? PoolEvent.Jaw : PoolEvent.Cushion, A = i, B = -1, Speed = vin, At = p[i] - n * R });
            }
        }

        /// <summary>Safety net: pushes apart balls that still overlap (and makes approaching ones bounce).</summary>
        void Separate()
        {
            const float min = D - 1e-5f;
            for (int i = 0; i < N; i++)
            {
                if (!Active(i)) continue;
                for (int j = i + 1; j < N; j++)
                {
                    if (!Active(j)) continue;
                    Vector2 d = p[j] - p[i];
                    float d2 = d.sqrMagnitude;
                    if (d2 >= min * min) continue;
                    float len = Mathf.Sqrt(d2);
                    Vector2 n = len > 1e-7f ? d / len : Vector2.right;
                    float corr = (D - len) * 0.5f;
                    p[i] -= n * corr;
                    p[j] += n * corr;
                    float rel = Vector2.Dot(v[i] - v[j], n);
                    if (rel > 0f)
                    {
                        float k = 0.5f * (1f + EBall) * rel;
                        v[i] -= n * k;
                        v[j] += n * k;
                    }
                }
            }
        }

        /// <summary>A ball whose centre is over a hole drops.</summary>
        void Pockets()
        {
            for (int i = 0; i < N; i++)
            {
                if (!Active(i)) continue;
                int k = PoolRails.InHole(p[i]);
                if (k >= 0) Pocket(i, k);
            }
        }

        void Pocket(int i, int k) => Pocket(i, k, time);

        void Pocket(int i, int k, float te)
        {
            if (!Active(i)) return;
            float sp = v[i].magnitude;
            down |= (ushort)(1 << i);
            Stop(i);
            res.Pocketed.Add(i);
            res.PocketOf.Add(k);
            res.Events.Add(new PoolEvent { T = te, Kind = PoolEvent.Pocket, A = i, B = k, Speed = sp, At = p[i] });
        }

        /// <summary>Safety net: NaN guard; a ball past the cushions far from any pocket is put back on the cloth, behind the bed it drops.</summary>
        void Guard()
        {
            for (int i = 0; i < N; i++)
            {
                if (!Active(i)) continue;
                if (PoolRails.Bad(p[i]))
                {
                    p[i] = PoolRails.Bad(prev[i]) ? Vector2.zero : prev[i];
                    Stop(i);
                }
                if (PoolRails.Bad(v[i]) || PoolRails.Bad(w[i]) || float.IsNaN(wz[i]) || float.IsInfinity(wz[i])) Stop(i);
                Vector2 q = p[i];
                const float tol = 1e-4f; // a ball resting on a cushion sits at the limit give or take rounding
                if (Mathf.Abs(q.x) <= PoolRails.FieldX + tol && Mathf.Abs(q.y) <= PoolRails.FieldY + tol) continue;
                int k = PoolRails.NearestHole(q, out float dist);
                if (Mathf.Abs(q.x) > PoolRails.BedX || Mathf.Abs(q.y) > PoolRails.BedZ)
                {
                    Pocket(i, k);
                    continue;
                }
                if (dist < PoolRails.Zone[k]) continue; // in the jaws of a pocket: allowed
                Vector2 vi = v[i];
                bool turned = false;
                if (q.x > PoolRails.FieldX) { q.x = PoolRails.FieldX; if (vi.x > 0f) { vi.x *= -0.5f; turned = true; } }
                if (q.x < -PoolRails.FieldX) { q.x = -PoolRails.FieldX; if (vi.x < 0f) { vi.x *= -0.5f; turned = true; } }
                if (q.y > PoolRails.FieldY) { q.y = PoolRails.FieldY; if (vi.y > 0f) { vi.y *= -0.5f; turned = true; } }
                if (q.y < -PoolRails.FieldY) { q.y = -PoolRails.FieldY; if (vi.y < 0f) { vi.y *= -0.5f; turned = true; } }
                p[i] = q;
                if (!turned) continue;
                v[i] = vi;
                w[i] = vi;
            }
        }
    }
}
