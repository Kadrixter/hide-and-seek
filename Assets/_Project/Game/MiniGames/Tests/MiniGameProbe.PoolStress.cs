using System.Collections;
using HideAndSeek.Game.MiniGames.Pool;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// -poolstress N: awkward shots from every side. The host hands the next shooter the cue ball (ball in hand anywhere,
    /// <see cref="MatchManager.TestPoolBallInHand"/>); the shooter puts it on the next spot of <see cref="StressSpots"/>
    /// (frozen on a cushion, in a corner, by a side pocket's jaw, in the middle of the table out of easy reach) and
    /// shoots softly in the listed direction, so the stance has to deal with the rail, the corners and long reaches.
    /// </summary>
    public sealed partial class MiniGameProbe
    {
        static readonly (Vector2 spot, float deg)[] StressSpots =
        {
            (new Vector2(0f, -0.466f), 90f), (new Vector2(0f, -0.466f), 0f), (new Vector2(0.94f, -0.44f), 135f),
            (new Vector2(-0.94f, 0.44f), -45f), (new Vector2(-0.96f, 0f), 0f), (new Vector2(0.96f, 0.1f), 180f),
            (new Vector2(0f, 0f), 90f), (new Vector2(0.3f, 0.1f), -30f), (new Vector2(0.12f, 0.46f), -100f),
            (new Vector2(-0.5f, 0.3f), 200f), (new Vector2(0.6f, -0.2f), 60f), (new Vector2(0f, 0f), 180f),
            (new Vector2(-0.7f, -0.4f), 10f), (new Vector2(0.9f, 0f), 90f), (new Vector2(-0.2f, 0.2f), -135f),
            (new Vector2(0.95f, 0.45f), 225f), (new Vector2(0.45f, 0.466f), -170f), (new Vector2(-0.96f, -0.3f), 30f),
        };

        int _stressDone;

        /// <summary>Host: ball in hand for the shooter before each of the next <see cref="_stress"/> shots.</summary>
        IEnumerator PoolStressHost(PoolTable pool)
        {
            int lastShot = int.MinValue;
            var mm = MatchManager.Instance;
            while (_stressDone < _stress)
            {
                var s = pool.State;
                if (s.Phase == PoolState.Playing && !s.Break && !pool.Rolling && !mm.PoolShotPending && s.ShotNo != lastShot && !s.BallInHand)
                {
                    yield return new WaitForSeconds(0.3f);
                    if (pool.State.ShotNo == s.ShotNo && mm.TestPoolBallInHand())
                    {
                        lastShot = s.ShotNo;
                        _stressDone++;
                    }
                }
                else if (s.BallInHand && s.ShotNo != lastShot && !s.Break) { lastShot = s.ShotNo; _stressDone++; }
                yield return null;
            }
            Log($"stress: {_stressDone} shots with the cue ball handed out");
        }

        /// <summary>Own stress shot: the cue ball onto the next awkward spot, a soft stroke in the listed direction.</summary>
        IEnumerator PoolStressTurn(PoolTable pool, int place, string tag)
        {
            var s = pool.State;
            var (spot, deg) = StressSpots[(s.ShotNo + 1) % StressSpots.Length];
            yield return PoolPlaceHand(pool, spot, tag);
            Vector2 cue = PoolAimCue(pool);
            float angle = deg * Mathf.Deg2Rad;
            Log($"{tag}: stress spot {spot} -> placed {cue}, aim {deg:0} deg");
            yield return PoolStroke(pool, place, angle, 0.16f, Vector2.zero, tag, true);
        }
    }
}
