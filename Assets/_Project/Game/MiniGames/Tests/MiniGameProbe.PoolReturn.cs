using System.Collections;
using HideAndSeek.Game.MiniGames.Pool;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// -testpool: the ball return on both peers. Whenever a ball goes down: screens of it falling through its pocket's
    /// cage, rolling down the gully (from low down at that side) and lying in the tray; the queue of both gullies is
    /// logged once everything has come to rest (the same on host and client, see "return settled").
    /// </summary>
    public sealed partial class MiniGameProbe
    {
        IEnumerator PoolReturnWatch(PoolTable pool)
        {
            ushort seen = pool.ShownDown;
            int round = pool.State.Round, shots = 0, settled = -1;
            float quiet = 0f;
            while (true)
            {
                if (pool.State.Round != round) { round = pool.State.Round; seen = pool.ShownDown; }
                ushort now = pool.ShownDown;
                ushort added = (ushort)(now & ~seen);
                seen = now;
                if (added != 0 && shots < 5)
                {
                    int ball = 0;
                    while ((added & (1 << ball)) == 0) ball++;
                    shots++;
                    StartCoroutine(PoolReturnShots(pool, ball, shots));
                }
                if (added != 0) quiet = 0f;
                quiet += Time.deltaTime;
                // Once nothing new went down for 7 s (every ball at rest), log the queues once per shot.
                if (quiet > 7f && settled != pool.State.ShotNo && !pool.Rolling)
                {
                    settled = pool.State.ShotNo;
                    Log($"return settled after shot {pool.State.ShotNo}: {pool.TestReturnInfo()}");
                }
                yield return null;
            }
        }

        /// <summary>Ball <paramref name="ball"/> has just gone down: falling, on the rods, in the tray.</summary>
        IEnumerator PoolReturnShots(PoolTable pool, int ball, int n)
        {
            int pocket = pool.TestReturnPocket(ball);
            if (pocket < 0) yield break;
            Vector3 c = PoolTableModel.PocketCentre(pocket);
            float sz = pocket < 3 ? -1f : 1f, sx = c.x < -0.1f ? -1f : c.x > 0.1f ? 1f : 0f;
            Transform t = pool.transform;
            yield return new WaitForSeconds(0.24f);
            Render($"{Prefix()}ret{n}_b{ball}_a_drop", t.TransformPoint(new Vector3(c.x + sx * 0.45f + 0.25f, 0.62f, sz * 1.15f)), t.TransformPoint(new Vector3(c.x, 0.56f, c.z)), 42f);
            Log($"return {n}: ball {ball} down pocket {pocket}: {pool.TestReturnInfo()}");
            yield return new WaitForSeconds(0.9f);
            var b = pool.BallTransform(ball);
            Vector3 at = b != null ? b.localPosition : new Vector3(0f, 0.5f, sz * 0.56f);
            Render($"{Prefix()}ret{n}_b{ball}_b_rails", t.TransformPoint(new Vector3(at.x - 0.25f, 0.4f, sz * 1.5f)), t.TransformPoint(at), 50f);
            Render($"{Prefix()}ret{n}_b{ball}_c_rails_close", t.TransformPoint(new Vector3(at.x + 0.35f, 0.42f, at.z + sz * 0.55f)), t.TransformPoint(at), 40f);
            Log($"return {n}: ball {ball} at {at.ToString("F3")} (1.1 s): {pool.TestReturnInfo()}");
            yield return new WaitForSeconds(5f);
            Render($"{Prefix()}ret{n}_b{ball}_d_tray", t.TransformPoint(new Vector3(1.68f, 0.7f, -0.45f)), t.TransformPoint(new Vector3(1.19f, 0.465f, -0.05f)), 38f);
            Log($"return {n}: ball {ball} at {(b != null ? b.localPosition.ToString("F3") : "-")} (6.1 s): {pool.TestReturnInfo()}");
        }
    }
}
