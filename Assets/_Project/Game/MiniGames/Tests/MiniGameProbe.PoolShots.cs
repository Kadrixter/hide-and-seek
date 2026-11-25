using System.Collections;
using HideAndSeek.Game.MiniGames.Pool;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>-testpool: the scripted shot finder, the sync log and the screens of the other player.</summary>
    public sealed partial class MiniGameProbe
    {
        const float PHalfL = PoolDims.PlayL * 0.5f, PHalfW = PoolDims.PlayW * 0.5f;

        /// <summary>Aim points of the 6 pockets (a little inside the mouths).</summary>
        static Vector2 PocketAim(int k)
        {
            int sx = k % 3 - 1, sz = k < 3 ? -1 : 1;
            return sx == 0 ? new Vector2(0f, sz * (PHalfW + 0.03f)) : new Vector2(sx * (PHalfL + 0.012f), sz * (PHalfW + 0.012f));
        }

        /// <summary>Is the segment a-b clear of every ball on the table except <paramref name="skip1"/> / <paramref name="skip2"/>?</summary>
        static bool Clear(PoolState s, Vector2 a, Vector2 b, int skip1, int skip2)
        {
            Vector2 d = b - a;
            float l2 = Mathf.Max(1e-8f, d.sqrMagnitude);
            for (int j = 0; j < 16; j++)
            {
                if (j == skip1 || j == skip2 || s.IsDown(j)) continue;
                float t = Mathf.Clamp01(Vector2.Dot(s.Pos[j] - a, d) / l2);
                if ((a + d * t - s.Pos[j]).magnitude < PoolDims.BallD * 1.05f) return false;
            }
            return true;
        }

        /// <summary>Easiest pot for a ball of <paramref name="mask"/> (cut under 55 deg, both paths clear).</summary>
        static bool PoolFindPot(PoolState s, ushort mask, Vector2 cue, out float angle, out int ball, out int pocket, out float dist)
        {
            angle = 0f;
            ball = pocket = -1;
            dist = 0f;
            float best = float.MaxValue;
            for (int b = 1; b < 16; b++)
            {
                if (s.IsDown(b) || (mask & (1 << b)) == 0) continue;
                for (int k = 0; k < 6; k++)
                {
                    Vector2 pk = PocketAim(k), dir = (pk - s.Pos[b]).normalized;
                    Vector2 ghost = s.Pos[b] - dir * PoolDims.BallD;
                    Vector2 aim = ghost - cue;
                    if (aim.magnitude < 0.02f) continue;
                    float cut = Vector2.Angle(aim, dir);
                    if (cut > 55f || !Clear(s, s.Pos[b], pk, b, 0) || !Clear(s, cue, ghost, 0, b)) continue;
                    float a = Mathf.Atan2(aim.y, aim.x);
                    var tmp = (Vector2[])s.Pos.Clone();
                    tmp[0] = cue;
                    if (!PoolSim.Predict(tmp, s.Down, cue, a, out _, out int hit, out _, out _) || hit != b) continue;
                    float score = cut * 0.02f + aim.magnitude + (pk - s.Pos[b]).magnitude;
                    if (score >= best) continue;
                    best = score;
                    angle = a;
                    ball = b;
                    pocket = k;
                    dist = aim.magnitude + (pk - s.Pos[b]).magnitude;
                }
            }
            return ball > 0;
        }

        static void PoolNearest(PoolState s, ushort mask, Vector2 cue, out float angle)
        {
            angle = 0f;
            float best = float.MaxValue;
            for (int b = 1; b < 16; b++)
            {
                if (s.IsDown(b) || (mask & (1 << b)) == 0) continue;
                float d = (s.Pos[b] - cue).sqrMagnitude;
                if (d >= best) continue;
                best = d;
                angle = Mathf.Atan2(s.Pos[b].y - cue.y, s.Pos[b].x - cue.x);
            }
        }

        /// <summary>The cue ball straight into a pocket with nothing in the way.</summary>
        static bool PoolScratchShot(PoolState s, Vector2 cue, out float angle)
        {
            angle = 0f;
            float best = float.MaxValue;
            for (int k = 0; k < 6; k++)
            {
                Vector2 pk = PocketAim(k);
                if (!Clear(s, cue, pk, 0, -1)) continue;
                float d = (pk - cue).magnitude;
                if (d >= best) continue;
                best = d;
                angle = Mathf.Atan2(pk.y - cue.y, pk.x - cue.x);
            }
            return best < float.MaxValue;
        }

        /// <summary>Ball in hand: a spot 30 cm behind the 8 on its line to a free pocket.</summary>
        static bool PoolEightSpot(PoolState s, out Vector2 spot)
        {
            spot = default;
            if (s.IsDown(8)) return false;
            for (int k = 0; k < 6; k++)
            {
                Vector2 pk = PocketAim(k), dir = (pk - s.Pos[8]).normalized;
                Vector2 p = s.Pos[8] - dir * 0.3f;
                if (!Clear(s, s.Pos[8], pk, 8, 0) || !Clear(s, p, s.Pos[8], 0, 8)) continue;
                var tmp = (Vector2[])s.Pos.Clone();
                if (!PoolSim.CanPlaceCueBall(tmp, (ushort)(s.Down | 1), p, s.Kitchen)) continue;
                // The cue ball must reach the 8 first (not a jaw, when the 8 hangs in a pocket mouth).
                tmp[0] = p;
                if (!PoolSim.Predict(tmp, (ushort)(s.Down & ~1), p, Mathf.Atan2(dir.y, dir.x), out _, out int hit, out _, out _) || hit != 8) continue;
                spot = p;
                return true;
            }
            return false;
        }

        /// <summary>Ball in hand: a spot 35 cm behind an own ball on its line to a free pocket.</summary>
        static bool PoolHandSpot(PoolState s, int seat, out Vector2 spot)
        {
            spot = default;
            ushort mask = PoolRules.Targets(s, seat);
            for (int b = 1; b < 16; b++)
            {
                if (s.IsDown(b) || (mask & (1 << b)) == 0) continue;
                for (int k = 0; k < 6; k++)
                {
                    Vector2 pk = PocketAim(k), dir = (pk - s.Pos[b]).normalized;
                    Vector2 p = s.Pos[b] - dir * 0.35f;
                    if (s.Kitchen && p.x > PoolState.KitchenX) continue;
                    if (!Clear(s, s.Pos[b], pk, b, 0) || !Clear(s, p, s.Pos[b], 0, b)) continue;
                    if (!PoolSim.CanPlaceCueBall(s.Pos, (ushort)(s.Down | 1), p, s.Kitchen)) continue;
                    spot = p;
                    return true;
                }
            }
            return false;
        }

        static long PoolHash(PoolState s)
        {
            long h = s.Down * 31L + s.Turn * 7L + s.Groups * 3L + (s.BallInHand ? 1 : 0);
            for (int i = 0; i < 16; i++)
                unchecked { h = h * 1000003L + Mathf.RoundToInt(s.Pos[i].x / PoolState.QStep) * 17L + Mathf.RoundToInt(s.Pos[i].y / PoolState.QStep); }
            return h;
        }

        /// <summary>Every finished shot: state hash, replay end vs the server's balls, shown balls vs the state.</summary>
        IEnumerator PoolSyncWatch(PoolTable pool)
        {
            PoolSimResult rep = null;
            int seen = pool.State.ShotNo;
            while (true)
            {
                if (pool.ReplayResult != null) rep = pool.ReplayResult;
                var s = pool.State;
                if (s.ShotNo != seen && !pool.Rolling)
                {
                    seen = s.ShotNo;
                    float repDiff = 0f;
                    if (rep != null)
                        for (int i = 1; i < 16; i++)
                            if (i != 8 && !s.IsDown(i)) repDiff = Mathf.Max(repDiff, (rep.FinalPos[i] - s.Pos[i]).magnitude);
                    yield return new WaitForSeconds(1.5f);
                    float shownDiff = 0f;
                    for (int i = 0; i < 16; i++)
                        if (!s.IsDown(i) && pool.ShownPos[i] != Vector2.zero) shownDiff = Mathf.Max(shownDiff, (pool.ShownPos[i] - s.Pos[i]).magnitude);
                    Log($"sync shot {s.ShotNo}: hash={PoolHash(s)} replay-vs-state {repDiff * 1000f:0.000} mm, shown-vs-state {shownDiff * 1000f:0.000} mm, dur {(rep != null ? rep.Duration : -1f):0.0} s => {(repDiff < 0.0001f && shownDiff < 0.5f ? "OK" : "FAIL")}");
                    rep = null;
                }
                yield return null;
            }
        }

        /// <summary>
        /// While the other player aims and strikes: what others see of him (his stance, cue, pull-back, the stroke) and
        /// my own screen; while I aim: the other one waiting with his cue.
        /// </summary>
        IEnumerator PoolWatchOpponent(PoolTable pool, int seat)
        {
            int opp = 1 - seat, lastAim = -1, lastStrike = -1, lastWait = -1, n = 0;
            while (n < 14)
            {
                var s = pool.State;
                var other = NetPlayer.Find(pool.SeatPlayer(opp));
                if (other != null && s.Phase == PoolState.Playing)
                {
                    Vector3 root = other.transform.position;
                    Vector3 toT = pool.Centre - root;
                    toT.y = 0f;
                    toT.Normalize();
                    Vector3 side = Vector3.Cross(Vector3.up, toT);
                    if (s.Turn == opp && !pool.Rolling && pool.TestBend(opp) > 0.97f && lastAim != s.ShotNo)
                    {
                        lastAim = s.ShotNo;
                        n++;
                        yield return new WaitForSeconds(0.3f);
                        Render($"{Prefix()}opp_{s.ShotNo:00}_aim_side", root + side * 1.7f + toT * 0.4f + Vector3.up * 1.25f, root + Vector3.up * 0.85f + toT * 0.3f, 50f);
                        Render($"{Prefix()}opp_{s.ShotNo:00}_aim_front", root + toT * 2.9f + Vector3.up * 1.6f, root + Vector3.up * 0.9f, 45f);
                        PShot(pool, $"{Prefix()}opp_{s.ShotNo:00}_watching");
                    }
                    if (pool.TestShownPull(opp) > 0.3f && lastStrike != s.ShotNo)
                    {
                        lastStrike = s.ShotNo;
                        Render($"{Prefix()}opp_{s.ShotNo:00}_pull", root + side * 1.6f + toT * 0.6f + Vector3.up * 1.15f, root + Vector3.up * 0.8f + toT * 0.5f, 50f);
                        for (float w = 0f; w < 3f && !pool.Rolling; w += Time.deltaTime) yield return null;
                        yield return new WaitForSeconds(0.04f);
                        Render($"{Prefix()}opp_{s.ShotNo:00}_strike", root + side * 1.6f + toT * 0.6f + Vector3.up * 1.15f, root + Vector3.up * 0.8f + toT * 0.5f, 50f);
                    }
                    if (s.Turn == seat && pool.LocalTurn && lastWait != s.ShotNo && pool.TestBend(seat) > 0.97f)
                    {
                        lastWait = s.ShotNo;
                        Vector3 back = (root - pool.Centre);
                        back.y = 0f;
                        Render($"{Prefix()}wait_{s.ShotNo:00}_opponent", pool.Centre + Vector3.up * 0.9f - back.normalized * 0.6f, root + Vector3.up * 0.9f, 55f);
                    }
                }
                yield return null;
            }
        }

        /// <summary>The ball return: the tray under the foot rail, a gully from low down at both sides, the pockets.</summary>
        void PoolPocketShots(PoolTable pool, string tag)
        {
            RenderTable(pool, $"{tag}_pocket_corner", new Vector3(1.55f, 0.95f, 1.05f), 40f);
            Render($"{tag}_return_tray", pool.transform.TransformPoint(new Vector3(1.68f, 0.7f, -0.45f)), pool.transform.TransformPoint(new Vector3(1.19f, 0.465f, -0.05f)), 38f);
            Render($"{tag}_return_foot", pool.transform.TransformPoint(new Vector3(2.3f, 0.85f, 0.5f)), pool.transform.TransformPoint(new Vector3(1.12f, 0.5f, 0f)), 42f);
            Render($"{tag}_return_low_a", pool.transform.TransformPoint(new Vector3(0.3f, 0.4f, -2.1f)), pool.transform.TransformPoint(new Vector3(0.2f, 0.5f, -0.4f)), 60f);
            Render($"{tag}_return_low_b", pool.transform.TransformPoint(new Vector3(0.3f, 0.4f, 2.1f)), pool.transform.TransformPoint(new Vector3(0.2f, 0.5f, 0.4f)), 60f);
        }
    }
}
