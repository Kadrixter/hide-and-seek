using System.Collections;
using HideAndSeek.Game.MiniGames.Pool;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>-testpool: one own turn (ball in hand, the shot finder, the stress spots), the stroke and the cue ball in hand.</summary>
    public sealed partial class MiniGameProbe
    {
        /// <summary>One own turn: k = shot number in this game.</summary>
        IEnumerator PoolTurn(PoolTable pool, int place, int k)
        {
            // Stress: the host hands out the cue ball a moment after the turn starts - wait for it.
            for (float w = 0f; _stress > 0 && !pool.State.Break && !pool.State.BallInHand && w < 2.5f; w += Time.deltaTime) yield return null;
            var s = pool.State;
            int seat = PoolState.TeamOf(place);
            string tag = $"g{_poolGame}_s{k:00}";
            _turnsPlayed++;
            Log($"{tag}: my turn (place {place}, team {(seat == 0 ? 'A' : 'B')}, shot {s.ShotNo + 1})");
            if (_stress > 0 && s.BallInHand && !s.Break)
            {
                yield return PoolStressTurn(pool, place, tag);
                yield break;
            }
            Vector2 cue = s.Pos[0];
            bool eightSetup = false;
            if (s.BallInHand)
            {
                if (!s.Break) Log($"{tag}: ball in hand, cue ball in the return before picking it up: {pool.ShownInPocket(0)} ({pool.TestReturnInfo()})");
                Vector2 spot;
                if (s.Break) spot = new Vector2(PoolState.KitchenX, 0.11f);
                else if (_poolGame == 1 && k >= _eightAt && PoolEightSpot(s, out spot)) eightSetup = true;
                else if (!PoolHandSpot(s, seat, out spot)) spot = PoolState.HeadSpot2;
                yield return PoolPlaceHand(pool, spot, tag);
                cue = PoolAimCue(pool);
            }
            float angle, power = 0.5f;
            Vector2 spin = Vector2.zero;
            string kind;
            if (s.Break)
            {
                angle = Mathf.Atan2(PoolState.FootSpot2.y - cue.y, PoolState.FootSpot2.x - cue.x);
                power = 1f;
                spin = new Vector2(0f, -0.15f);
                kind = "break";
            }
            else if (eightSetup)
            {
                angle = Mathf.Atan2(s.Pos[8].y - cue.y, s.Pos[8].x - cue.x);
                power = 0.45f;
                kind = "8 too early";
            }
            else if (_poolGame == 1 && k == 5 && PoolScratchShot(s, cue, out angle))
            {
                power = 0.42f;
                kind = "scratch";
            }
            else if (PoolFindPot(s, PoolRules.Targets(s, seat), cue, out angle, out int ball, out int pocket, out float dist))
            {
                power = Mathf.Clamp(0.3f + dist * 0.22f, 0.32f, 0.75f);
                spin = new Vector2(k % 3 == 1 ? 0.25f : 0f, k % 2 == 0 ? 0.2f : -0.2f);
                kind = $"pot {ball} -> pocket {pocket}";
            }
            else
            {
                PoolNearest(s, PoolRules.Targets(s, seat), cue, out angle);
                power = 0.55f;
                kind = "safety at the nearest legal ball";
            }
            Log($"{tag}: {kind}, angle {angle * Mathf.Rad2Deg:0.0}, power {power:0.00}, spin {spin}, cue {cue}, in hand {s.BallInHand}");
            yield return PoolStroke(pool, place, angle, power, spin, tag, k < 2 || kind == "scratch" || eightSetup);
        }

        /// <summary>Aim, (top view), pull back, release; waits until the balls stopped and the result is in.</summary>
        IEnumerator PoolStroke(PoolTable pool, int place, float angle, float power, Vector2 spin, string tag, bool shots)
        {
            int shotNo = pool.State.ShotNo;
            pool.TestAim(angle, spin);
            for (float w = 0f; w < 8f && pool.TestBend(place) < 0.98f; w += Time.deltaTime)
            {
                pool.TestAim(angle, spin);
                yield return null;
            }
            yield return new WaitForSeconds(0.7f);
            Log($"{tag}: stance bend={pool.TestBend(place):0.00} aim={pool.AimAngle * Mathf.Rad2Deg:0.0}");
            if (shots)
            {
                PShot(pool, $"{Prefix()}{tag}_a_aim");
                pool.TestTopView(true);
                yield return new WaitForSeconds(0.9f);
                PShot(pool, $"{Prefix()}{tag}_b_top");
                pool.TestTopView(false);
                yield return new WaitForSeconds(0.9f);
            }
            pool.TestPull(Mathf.Clamp01(power * 0.9f + 0.1f));
            yield return new WaitForSeconds(0.6f);
            if (shots) Shot($"{Prefix()}{tag}_c_pull");
            yield return new WaitForSeconds(0.2f);
            pool.TestRelease();
            for (float w = 0f; w < 4f && !pool.Rolling; w += Time.deltaTime) yield return null;
            if (!pool.Rolling)
            {
                Log($"{tag}: the shot did not start FAIL");
                yield break;
            }
            if (shots)
            {
                yield return new WaitForSeconds(0.45f);
                Shot($"{Prefix()}{tag}_d_rolling");
                yield return new WaitForSeconds(0.6f);
                RenderTable(pool, $"{Prefix()}{tag}_e_rolling_top", new Vector3(0.05f, 2.3f, -0.4f), 55f);
            }
            for (float w = 0f; w < 30f && (pool.Rolling || pool.State.ShotNo <= shotNo); w += Time.deltaTime) yield return null;
            var s = pool.State;
            Log($"{tag}: result shot {s.ShotNo}: turn={s.Turn} groups={s.Groups} inHand={s.BallInHand} kitchen={s.Kitchen} down={s.Down:X4} phase={s.Phase} note='{s.Note}'");
            yield return new WaitForSeconds(0.8f);
            if (shots) Shot($"{Prefix()}{tag}_f_after");
        }

        /// <summary>Ball in hand: carry the cue ball over the cloth to the spot (screens), then put it down.</summary>
        IEnumerator PoolPlaceHand(PoolTable pool, Vector2 spot, string tag)
        {
            yield return new WaitForSeconds(0.8f);
            Vector2 from = pool.State.Pos[0];
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.8f)
            {
                pool.TestHold(Vector2.Lerp(from, spot, t), false);
                yield return null;
            }
            pool.TestHold(spot, false);
            yield return new WaitForSeconds(0.5f);
            Log($"{tag}: carried cue ball shown in the hand (left the return): {(!pool.ShownInPocket(0) ? "OK" : "FAIL")}");
            Shot($"{Prefix()}{tag}_0_in_hand");
            pool.TestHold(spot, true);
            yield return new WaitForSeconds(0.3f);
            pool.TestHold(null, false);
            Vector2 at = PoolAimCue(pool);
            Log($"{tag}: cue ball out of the return once picked up: {(!pool.ShownInPocket(0) ? "OK" : "FAIL")}");
            Log($"{tag}: cue ball put down at {at} (wanted {spot}) holding={pool.LocalHolding} => {(!pool.LocalHolding && (at - spot).magnitude < 0.06f ? "OK" : "FAIL")}");
        }

        void PShot(PoolTable pool, string name)
        {
            Log($"screen {name}: {pool.TestCamInfo()}");
            Shot(name);
        }

        /// <summary>Cue ball the local shooter aims from (the one in hand once put down).</summary>
        static Vector2 PoolAimCue(PoolTable pool) => pool.HandBall(out var p) ? p : pool.State.Pos[0];
    }
}
