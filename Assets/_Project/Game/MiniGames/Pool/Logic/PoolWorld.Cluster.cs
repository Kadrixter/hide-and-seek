using UnityEngine;

namespace HideAndSeek.Game.MiniGames.Pool
{
    /// <summary>
    /// Impacts into touching balls (the rack on the break, frozen balls). Pairwise collisions one after another send
    /// the whole impulse down one straight chain of the triangle (the break spreads two corner balls and nothing
    /// else), so a hit on a ball that touches others is solved at once for the whole group: a short soft-contact run
    /// (linear springs with the damping of <see cref="EBall"/>, contact time <see cref="SoftT"/>) on copies of the
    /// balls, whose final velocities are taken over; the positions stay where they were (the impact is instantaneous
    /// for the rest of the shot). Deterministic like everything else here.
    /// </summary>
    internal sealed partial class PoolWorld
    {
        /// <summary>Balls closer than this (gap, m) count as touching for the group.</summary>
        const float NearGap = 0.0005f;
        /// <summary>Contact time of one ball-ball spring contact (s) and substeps per contact.</summary>
        const float SoftT = 3e-4f;
        const int SoftSub = 24, SoftMax = 6000;
        /// <summary>The run ends when no pair of the group will touch within this time (s).</summary>
        const float SoftQuiet = 2e-4f;

        readonly int[] cl = new int[N];
        readonly Vector2[] cp = new Vector2[N], cv = new Vector2[N];
        readonly float[] clImp = new float[N * N], clRest = new float[N * N];
        /// <summary>Pair checks left for the soft runs of this shot (a cap on the cost; afterwards plain pairwise hits).</summary>
        int softBudget = 1500000;

        bool InCluster(int ball, int n)
        {
            for (int k = 0; k < n; k++)
                if (cl[k] == ball) return true;
            return false;
        }

        /// <summary>Group of balls touching a or b (directly or through each other); returns its size.</summary>
        int Cluster(int a, int b)
        {
            int n = 0;
            cl[n++] = a;
            cl[n++] = b;
            if (softBudget <= 0) return 2;
            const float lim = (D + NearGap) * (D + NearGap);
            for (int k = 0; k < n; k++)
                for (int j = 0; j < N; j++)
                {
                    if (!Active(j) || InCluster(j, n)) continue;
                    if ((p[j] - p[cl[k]]).sqrMagnitude < lim) cl[n++] = j;
                }
            return n;
        }

        void SolveCluster(int n, float te)
        {
            float h = SoftT / SoftSub, w = Mathf.PI / SoftT;
            // Unit masses: effective mass 0.5 per pair.
            float k = 0.5f * w * w;
            float lnE = Mathf.Log(EBall), zeta = -lnE / Mathf.Sqrt(Mathf.PI * Mathf.PI + lnE * lnE);
            float c = 2f * zeta * Mathf.Sqrt(k * 0.5f);
            for (int i = 0; i < n; i++)
            {
                cp[i] = p[cl[i]];
                cv[i] = v[cl[i]];
            }
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                {
                    clImp[i * N + j] = 0f;
                    // Balls already overlapping a hair (network rounding) rest at their distance: no spring kick.
                    clRest[i * N + j] = Mathf.Min(D, (cp[j] - cp[i]).magnitude);
                }
            for (int s = 0; s < SoftMax; s++)
            {
                softBudget -= n * (n - 1) / 2;
                bool touching = false;
                for (int i = 0; i < n; i++)
                    for (int j = i + 1; j < n; j++)
                    {
                        Vector2 d = cp[j] - cp[i];
                        float d2 = d.sqrMagnitude, rest = clRest[i * N + j];
                        if (d2 >= rest * rest) continue;
                        float len = Mathf.Sqrt(d2);
                        Vector2 nn = len > 1e-9f ? d / len : Vector2.right;
                        float f = k * (rest - len) + c * Vector2.Dot(cv[i] - cv[j], nn);
                        touching = true;
                        if (f <= 0f) continue;
                        Vector2 imp = nn * (f * h);
                        cv[i] -= imp;
                        cv[j] += imp;
                        clImp[i * N + j] += f * h;
                    }
                for (int i = 0; i < n; i++) cp[i] += cv[i] * h;
                if (!touching && !SoonContact(n)) break;
            }
            for (int i = 0; i < n; i++)
            {
                int b = cl[i];
                if (PoolRails.Bad(cv[i])) continue;
                // Speed taken over; a ball pushed from rest starts sliding (no spin yet).
                v[b] = cv[i];
            }
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                {
                    float imp = clImp[i * N + j];
                    if (imp < SoundMin) continue;
                    int a = Mathf.Min(cl[i], cl[j]), b = Mathf.Max(cl[i], cl[j]), pk = a * N + b;
                    if (te - lastPair[pk] < SoundGap) continue;
                    lastPair[pk] = te;
                    res.Events.Add(new PoolEvent { T = te, Kind = PoolEvent.BallBall, A = a, B = b, Speed = imp, At = (p[a] + p[b]) * 0.5f });
                }
        }

        /// <summary>Will two balls of the group touch within <see cref="SoftQuiet"/>?</summary>
        bool SoonContact(int n)
        {
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                    if (PoolRails.Toi(cp[j] - cp[i], cv[j] - cv[i], D, SoftQuiet) >= 0f) return true;
            return false;
        }
    }
}
