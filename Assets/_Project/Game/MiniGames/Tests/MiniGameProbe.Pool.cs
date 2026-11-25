using System.Collections;
using HideAndSeek.Game.MiniGames.Pool;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// -testpool &lt;dir&gt; (host AND client): both walk up to the lobby pool table (host = seat A, client = seat B),
    /// the breaker places the cue ball in the kitchen and breaks, a few shots of both players (scripted shot finder,
    /// aimed through the table's own aiming state: stance, cue, pull-back, release = <see cref="PoolTable"/>.Shoot),
    /// a deliberate scratch, the cue ball in hand put down behind the 8 and the 8 potted too early (game over), both
    /// press R (the loser breaks the rematch), two shots of game 2, the client gets up (the host wins), the host too.
    /// Every finished shot logs a hash of the state on both peers ("sync shot N"), the replay's end vs the server's
    /// balls and the shown balls vs the state. Screens: fp_* = the player's own screen, side_* = a free camera.
    /// </summary>
    public sealed partial class MiniGameProbe
    {
        int _poolGame;

        IEnumerator PoolRun()
        {
            var pool = MiniGameTables.Pool;
            var me = NetPlayer.Local;
            int seat = _host ? 0 : 1;
            StartCoroutine(PoolSyncWatch(pool));
            StartCoroutine(PoolWatchOpponent(pool, seat));
            if (_host)
            {
                Log($"table at {pool.transform.position} yaw {pool.transform.eulerAngles.y:0}");
                RenderTable(pool, "00_lobby_far", new Vector3(4.2f, 2.6f, -3.4f), 60f);
                RenderTable(pool, "00_lobby_far2", new Vector3(-4.0f, 2.4f, 3.2f), 60f);
                RenderTable(pool, "00_lobby_end", new Vector3(-4.3f, 1.7f, 0.0f), 62f);
                Render("00_lamp_ceiling", pool.transform.TransformPoint(new Vector3(2.4f, 1.6f, -1.8f)), pool.transform.TransformPoint(new Vector3(0f, 2.1f, 0f)), 60f);
                RenderTable(pool, "00_table_empty", new Vector3(1.6f, 1.9f, -1.7f), 55f);
            }
            else yield return new WaitForSeconds(2.5f);
            yield return SitDown(pool, seat);
            for (float w = 0f; w < 20f && pool.State.Phase != PoolState.Playing; w += Time.deltaTime) yield return null;
            Log($"game 1: phase={pool.State.Phase} breaker seat={pool.State.Turn} round={pool.State.Round} note='{pool.State.Note}' => {(pool.State.Phase == PoolState.Playing ? "OK" : "FAIL")}");
            yield return new WaitForSeconds(1.5f);
            Shot(Prefix() + "01_seated");
            _poolGame = 1;
            yield return PoolGame(pool, seat, 16);
            yield return new WaitForSeconds(1.5f);
            var s = pool.State;
            Log($"game 1 over: phase={s.Phase} winner={s.Winner} end={s.EndReason} wins {s.WinsA}:{s.WinsB} note='{s.Note}' => {(s.Phase == PoolState.Over ? "OK" : "FAIL")}");
            PShot(pool, Prefix() + "40_game_over");
            if (_host)
            {
                RenderTable(pool, "40_over_top", new Vector3(0.05f, 2.3f, -0.4f), 55f);
                PoolPocketShots(pool, "41");
            }
            if (s.Phase != PoolState.Over) yield break;

            // Rematch: both press R, the loser breaks.
            int loser = s.Winner <= 1 ? 1 - s.Winner : -1, round = s.Round;
            yield return new WaitForSeconds(_host ? 1f : 2f);
            me.MiniRematchRpc();
            for (float w = 0f; w < 10f && !(pool.State.Phase == PoolState.Playing && pool.State.Round > round); w += Time.deltaTime) yield return null;
            Log($"rematch: phase={pool.State.Phase} round={pool.State.Round} breaker={pool.State.Turn} loser={loser} => {(pool.State.Phase == PoolState.Playing && pool.State.Turn == loser ? "OK" : "FAIL")}");
            yield return new WaitForSeconds(1f);
            Shot(Prefix() + "50_rematch");
            _poolGame = 2;
            yield return PoolGame(pool, seat, 3);
            for (float w = 0f; w < 15f && pool.Rolling; w += Time.deltaTime) yield return null;
            yield return new WaitForSeconds(2f);

            // The client gets up: the host wins, then the host gets up too.
            if (!_host)
            {
                me.MiniLeaveRpc();
                yield return new WaitForSeconds(2f);
                Log($"client left: seat={pool.LocalSeat} => {(pool.LocalSeat < 0 ? "OK" : "FAIL")}");
                Shot(Prefix() + "60_left");
                yield return new WaitForSeconds(4f);
            }
            else
            {
                for (float w = 0f; w < 10f && pool.State.Phase == PoolState.Playing; w += Time.deltaTime) yield return null;
                yield return new WaitForSeconds(0.8f);
                Log($"opponent left: phase={pool.State.Phase} end={pool.State.EndReason} winner={pool.State.Winner} note='{pool.State.Note}' => {(pool.State.EndReason == PoolState.EndLeft && pool.State.Winner == 0 ? "OK" : "FAIL")}");
                Shot(Prefix() + "60_opponent_left");
                yield return new WaitForSeconds(1.5f);
                me.MiniLeaveRpc();
                yield return new WaitForSeconds(2f);
                Log($"host left: A={pool.State.A} B={pool.State.B} phase={pool.State.Phase} => {(pool.State.A == PoolState.Nobody && pool.State.B == PoolState.Nobody && pool.State.Phase == PoolState.Waiting ? "OK" : "FAIL")}");
                Shot("61_stood_up");
                RenderTable(pool, "61_table_reset", new Vector3(1.6f, 1.9f, -1.7f), 55f);
            }
        }

        /// <summary>Plays this seat's shots until the game is over or <paramref name="maxShots"/> shots were made in it.</summary>
        IEnumerator PoolGame(PoolTable pool, int seat, int maxShots)
        {
            int start = pool.State.ShotNo, round = pool.State.Round;
            float idle = 0f;
            int lastShot = start;
            while (pool.State.Phase == PoolState.Playing && pool.State.Round == round && pool.State.ShotNo - start < maxShots)
            {
                if (pool.LocalTurn && pool.HasState)
                {
                    yield return PoolTurn(pool, seat, pool.State.ShotNo - start);
                    idle = 0f;
                    continue;
                }
                if (pool.State.ShotNo != lastShot) { lastShot = pool.State.ShotNo; idle = 0f; }
                idle += Time.deltaTime;
                if (idle > 45f)
                {
                    Log($"nothing happens for 45 s (turn={pool.State.Turn}, rolling={pool.Rolling}) FAIL");
                    yield break;
                }
                yield return null;
            }
            for (float w = 0f; w < 15f && pool.Rolling; w += Time.deltaTime) yield return null;
        }

        /// <summary>One own turn: k = shot number in this game.</summary>
        IEnumerator PoolTurn(PoolTable pool, int seat, int k)
        {
            var s = pool.State;
            string tag = $"g{_poolGame}_s{k:00}";
            Vector2 cue = s.Pos[0];
            bool eightSetup = false;
            if (s.BallInHand)
            {
                Vector2 spot;
                if (s.Break) spot = new Vector2(PoolState.KitchenX, 0.11f);
                else if (_poolGame == 1 && k >= 6 && PoolEightSpot(s, out spot)) eightSetup = true;
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
            yield return PoolStroke(pool, seat, angle, power, spin, tag, k < 2 || kind == "scratch" || eightSetup);
        }

        /// <summary>Aim, (top view), pull back, release; waits until the balls stopped and the result is in.</summary>
        IEnumerator PoolStroke(PoolTable pool, int seat, float angle, float power, Vector2 spin, string tag, bool shots)
        {
            int shotNo = pool.State.ShotNo;
            pool.TestAim(angle, spin);
            for (float w = 0f; w < 8f && pool.TestBend(seat) < 0.98f; w += Time.deltaTime)
            {
                pool.TestAim(angle, spin);
                yield return null;
            }
            yield return new WaitForSeconds(0.7f);
            Log($"{tag}: stance bend={pool.TestBend(seat):0.00} aim={pool.AimAngle * Mathf.Rad2Deg:0.0}");
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
            Shot($"{Prefix()}{tag}_0_in_hand");
            pool.TestHold(spot, true);
            yield return new WaitForSeconds(0.3f);
            pool.TestHold(null, false);
            Vector2 at = PoolAimCue(pool);
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
