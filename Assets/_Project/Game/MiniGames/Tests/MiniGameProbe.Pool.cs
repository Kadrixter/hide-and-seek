using System.Collections;
using HideAndSeek.Game.MiniGames.Pool;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// -testpool &lt;dir&gt; (every instance): walks up to the lobby pool table, takes a place (-poolplace, default host 0 =
    /// A1, client 1 = B1), waits for -poolplayers players (default 2) and presses "Gotowy"; plays its own turns with the
    /// scripted shot finder (aimed through the table's own aiming state), a deliberate scratch, the 8 potted too early
    /// once -pooleight shots are played; then everybody presses R (the losers break) and plays a few shots of game 2.
    /// -poolleave N: this player gets up after shot N of game 1 (his team plays on; an empty team loses).
    /// -poolsitdelay S: sit down S seconds late (a late joiner takes a free place during the game).
    /// -poolstress N: the host gives every shooter ball in hand for N shots and the shooter places the cue ball on a list
    /// of awkward spots (frozen on cushions, in the corners, by the jaws) with extreme aims, at low power.
    /// Checks on every peer: state hashes ("sync shot N"), the inning rotation, the shooters' bodies vs the table, the
    /// own camera's occluders, never stuck (MiniGameProbe.PoolChecks). Screens: fp_* = own screen, side_* = free camera.
    /// </summary>
    public sealed partial class MiniGameProbe
    {
        int _poolGame, _stress, _eightAt = 6, _turnsPlayed;
        string _prefixOverride;

        static int ArgInt(string name, int def) => int.TryParse(Arg(name), out int v) ? v : def;

        IEnumerator PoolRun()
        {
            var pool = MiniGameTables.Pool;
            var me = NetPlayer.Local;
            int place = ArgInt("-poolplace", _host ? 0 : 1), expect = ArgInt("-poolplayers", 2), leaveAt = ArgInt("-poolleave", -1);
            int shots1 = ArgInt("-poolshots", 16);
            _stress = ArgInt("-poolstress", 0);
            _eightAt = ArgInt("-pooleight", 6);
            _prefixOverride = $"p{place}_";
            StartCoroutine(PoolSyncWatch(pool));
            StartCoroutine(PoolWatchOthers(pool, place));
            StartCoroutine(PoolReturnWatch(pool));
            StartCoroutine(PoolChecks(pool));
            if (_host) StartCoroutine(PoolRotationWatch(pool));
            if (_host && _stress > 0) StartCoroutine(PoolStressHost(pool));
            if (_host)
            {
                Log($"table at {pool.transform.position} yaw {pool.transform.eulerAngles.y:0}");
                RenderTable(pool, "00_lobby_far", new Vector3(4.2f, 2.6f, -3.4f), 60f);
                RenderTable(pool, "00_table_empty", new Vector3(1.6f, 1.9f, -1.7f), 55f);
            }
            yield return new WaitForSeconds(ArgInt("-poolsitdelay", _host ? 0 : 2) + place * 0.7f);
            yield return SitDown(pool, place);
            bool lateJoin = pool.State.Phase == PoolState.Playing;
            if (!lateJoin)
            {
                for (float w = 0f; w < 40f && pool.State.Seated < expect; w += Time.deltaTime) yield return null;
                Log($"lobby: seated {pool.State.Seated} of {expect}, panel shown {MiniGameHud.PoolLobbyShown} => {(pool.State.Seated >= expect && MiniGameHud.PoolLobbyShown ? "OK" : "FAIL")}");
                yield return new WaitForSeconds(0.6f + place * 0.5f);
                Shot(Prefix() + "01_lobby");
                Log($"before my ready: phase={pool.State.Phase} ready={pool.State.Ready} => {(pool.State.Phase == PoolState.Waiting ? "OK" : "FAIL")}");
                bool clicked = MiniGameHud.TestClick("ready");
                yield return new WaitForSeconds(0.6f);
                Log($"clicked Gotowy in the panel: {clicked}, my ready bit {((pool.State.Ready >> place) & 1)} or started {pool.State.Phase == PoolState.Playing} => {(clicked && (((pool.State.Ready >> place) & 1) == 1 || pool.State.Phase == PoolState.Playing) ? "OK" : "FAIL")}");
            }
            else Log($"joined a running game at place {place}: shooter={pool.State.Shooter} => OK");
            for (float w = 0f; w < 30f && pool.State.Phase != PoolState.Playing; w += Time.deltaTime) yield return null;
            Log($"game 1: phase={pool.State.Phase} breaker team={pool.State.Turn} shooter place={pool.State.Shooter} seated={pool.State.Seated} note='{pool.State.Note}' => {(pool.State.Phase == PoolState.Playing ? "OK" : "FAIL")}");
            yield return new WaitForSeconds(1.5f);
            Shot(Prefix() + "02_playing");
            _poolGame = 1;
            yield return PoolGame(pool, place, shots1, leaveAt);
            if (pool.LocalSeat < 0)
            {
                yield return PoolAfterLeave(pool);
                yield break;
            }
            yield return new WaitForSeconds(1.5f);
            var s = pool.State;
            Log($"game 1 over: phase={s.Phase} winner={s.Winner} end={s.EndReason} wins {s.WinsA}:{s.WinsB} note='{s.Note}' => {(s.Phase == PoolState.Over ? "OK" : "FAIL")}");
            Shot(Prefix() + "40_game_over");
            if (_host) PoolPocketShots(pool, "41");
            if (s.Phase != PoolState.Over) yield break;

            // Rematch: everybody presses R ("Gotowy"), the losers break.
            int losers = s.Winner <= 1 ? 1 - s.Winner : -1, round = s.Round;
            yield return new WaitForSeconds(1f + place * 0.4f);
            me.MiniRematchRpc();
            for (float w = 0f; w < 12f && !(pool.State.Phase == PoolState.Playing && pool.State.Round > round); w += Time.deltaTime) yield return null;
            Log($"rematch: phase={pool.State.Phase} round={pool.State.Round} breaker team={pool.State.Turn} losers={losers} => {(pool.State.Phase == PoolState.Playing && (losers < 0 || pool.State.Turn == losers) ? "OK" : "FAIL")}");
            yield return new WaitForSeconds(1f);
            Shot(Prefix() + "50_rematch");
            _poolGame = 2;
            yield return PoolGame(pool, place, 3, -1);
            for (float w = 0f; w < 15f && pool.Rolling; w += Time.deltaTime) yield return null;
            yield return new WaitForSeconds(2f + place * 1.5f);
            // Everybody gets up, the host last; the table ends empty and waiting.
            if (!_host)
            {
                me.MiniLeaveRpc();
                yield return PoolAfterLeave(pool);
                yield break;
            }
            for (float w = 0f; w < 20f && pool.State.Seated > 1; w += Time.deltaTime) yield return null;
            yield return new WaitForSeconds(1f);
            Shot("60_alone");
            me.MiniLeaveRpc();
            yield return new WaitForSeconds(2f);
            Log($"host left: seated={pool.State.Seated} phase={pool.State.Phase} => {(pool.State.Seated == 0 && pool.State.Phase == PoolState.Waiting ? "OK" : "FAIL")}");
            yield return PoolAfterLeave(pool);
            RenderTable(pool, "61_table_reset", new Vector3(1.6f, 1.9f, -1.7f), 55f);
        }

        /// <summary>Plays this place's shots until the game is over, <paramref name="maxShots"/> shots were made or (leaveAt) it is time to get up.</summary>
        IEnumerator PoolGame(PoolTable pool, int place, int maxShots, int leaveAt)
        {
            int start = pool.State.ShotNo, round = pool.State.Round;
            float idle = 0f;
            int lastShot = start;
            while (pool.State.Phase == PoolState.Playing && pool.State.Round == round && pool.State.ShotNo - start < maxShots)
            {
                if (leaveAt >= 0 && pool.State.ShotNo - start >= leaveAt && !pool.Rolling)
                {
                    int team = PoolState.TeamOf(place), members = pool.State.Members(team);
                    Log($"leaving mid-game after shot {pool.State.ShotNo} (team {(team == 0 ? 'A' : 'B')} has {members})");
                    Shot(Prefix() + "30_before_leave");
                    NetPlayer.Local.MiniLeaveRpc();
                    for (float w = 0f; w < 3f && pool.LocalSeat >= 0; w += Time.deltaTime) yield return null;
                    yield return new WaitForSeconds(0.5f);
                    var s = pool.State;
                    bool ok = members > 1 ? s.Phase == PoolState.Playing && s.Members(team) == members - 1 : s.Phase == PoolState.Over && s.EndReason == PoolState.EndLeft;
                    Log($"left: my place free={s.Place(place) == PoolState.Nobody} phase={s.Phase} team members={s.Members(team)} shooter of my team={s.ShooterPlace(team)} note='{s.Note}' => {(ok && pool.LocalSeat < 0 ? "OK" : "FAIL")}");
                    yield break;
                }
                if (pool.LocalTurn && pool.HasState)
                {
                    yield return PoolTurn(pool, place, pool.State.ShotNo - start);
                    idle = 0f;
                    continue;
                }
                if (pool.State.ShotNo != lastShot) { lastShot = pool.State.ShotNo; idle = 0f; }
                idle += Time.deltaTime;
                if (idle > 60f)
                {
                    Log($"nothing happens for 60 s (turn={pool.State.Turn}, shooter={pool.State.Shooter}, rolling={pool.Rolling}) FAIL");
                    yield break;
                }
                yield return null;
            }
            for (float w = 0f; w < 20f && pool.Rolling; w += Time.deltaTime) yield return null;
        }

        /// <summary>Got up: free to walk again (controller on, nothing in the way), then watch the rest from here.</summary>
        IEnumerator PoolAfterLeave(PoolTable pool)
        {
            yield return new WaitForSeconds(1.2f);
            var me = NetPlayer.Local;
            var cc = me.GetComponent<CharacterController>();
            int blocked = 0;
            foreach (var c in Physics.OverlapCapsule(me.transform.position + Vector3.up * 0.4f, me.transform.position + Vector3.up * 1.5f, 0.3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                if (c.GetComponentInParent<NetPlayer>() == null) blocked++;
            Log($"after leaving: seat={pool.LocalSeat} move={me.Motor.MovementEnabled} look={me.Motor.LookEnabled} controller={(cc != null && cc.enabled)} overlaps={blocked} at {me.transform.position} => {(pool.LocalSeat < 0 && me.Motor.MovementEnabled && cc != null && cc.enabled && blocked == 0 ? "OK" : "FAIL")}");
            RenderTable(pool, Prefix() + "70_after_leave", new Vector3(1.9f, 1.7f, -1.9f), 60f);
            // Watch until the table is empty (or 4 minutes), so the logs of every peer cover the whole run.
            for (float w = 0f; w < 240f && pool.State.Seated > 0; w += Time.deltaTime) yield return null;
        }
    }
}
