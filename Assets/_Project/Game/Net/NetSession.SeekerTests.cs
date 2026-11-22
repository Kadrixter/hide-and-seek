using System;
using System.Collections;
using System.Collections.Generic;
using HideAndSeek.Core;
using HideAndSeek.Game.UI;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Several seekers (-testseekers on the host, best with -forceseeker; clients join with -seekwatch):
    ///  lobby: killer count too high for the players -> board + settings panel shots, the lever refuses with the reason;
    ///  count 2 -> lever starts; roles (2 seekers), spawn side by side; breathing of a hider follows the nearest seeker;
    ///  goo hits the second seeker only; the one chainsaw: first seeker takes it, the second cannot, it is dropped and
    ///  the second picks it up; Glock for both; both attack at the same time with own cooldowns; everybody down = seekers win.
    ///  -seekshots &lt;folder&gt;: PNGs of all of it. -autostartplayers N: players to wait for (default 4).
    /// </summary>
    public partial class NetSession
    {
        private void StartSeekerTests(Func<string, bool> has)
        {
            if (has("-testseekers")) StartCoroutine(SeekersTest());
            if (has("-seekwatch")) StartCoroutine(SeekersWatch());
        }

        private static string SeekShots => ArgOr("-seekshots", "");

        private static void SLog(string s) => Debug.Log("[SeekTest] " + s);

        private IEnumerator SeekersTest()
        {
            string dir = SeekShots;
            int need = int.TryParse(ArgOr("-autostartplayers", "4"), out int np) ? np : 4;
            while (MatchManager.Instance == null || NetPlayer.All.Count < need || LobbyButton.Instance == null || NetPlayer.Local == null)
                yield return new WaitForSeconds(0.5f);
            yield return new WaitForSeconds(3f);
            var mm = MatchManager.Instance;
            var me = NetPlayer.Local;
            foreach (var p in NetPlayer.All) if (p != null) p.Ready.Value = true;

            // ---- lobby: too many killers
            var s = mm.Settings.Value;
            SLog($"setting at start (-seekers): {s.SeekerCount}, players {NetPlayer.All.Count}");
            s.Seekers = (byte)NetPlayer.All.Count;
            s.HideTime = float.TryParse(ArgOr("-hidetime", "10"), out float ht) ? ht : 10f;
            mm.SetSettingsRpc(s);
            yield return new WaitForSeconds(1f);
            var board = LobbySettingsBoard.Instance;
            if (board != null)
                CamShot(dir, "seek_board_toomany", board.transform.position - board.transform.forward * 3.1f + Vector3.up * 0.1f, board.transform.position);
            GameHud.OpenSettingsPanel();
            yield return new WaitForSeconds(0.8f);
            Shot(dir, "seek_panel_toomany");
            yield return new WaitForSeconds(0.3f);
            GameHud.CloseAllMenus();
            mm.RequestLeverRpc();
            yield return new WaitForSeconds(1.2f);
            Shot(dir, "seek_lever_refused");
            SLog($"lever with {mm.Settings.Value.SeekerCount} killers / {NetPlayer.All.Count} players: phase={mm.Phase.Value} (expected Lobby)");
            yield return new WaitForSeconds(2f);

            // ---- 2 killers: start
            s = mm.Settings.Value;
            s.Seekers = 2;
            mm.SetSettingsRpc(s);
            yield return new WaitForSeconds(1f);
            if (board != null)
                CamShot(dir, "seek_board_2", board.transform.position - board.transform.forward * 3.1f + Vector3.up * 0.1f, board.transform.position);
            GameHud.OpenSettingsPanel();
            yield return new WaitForSeconds(0.8f);
            Shot(dir, "seek_panel_2");
            GameHud.CloseAllMenus();
            yield return new WaitForSeconds(0.3f);
            mm.RequestLeverRpc();
            float t0 = Time.time;
            while (mm.Phase.Value == MatchPhase.Lobby && Time.time - t0 < 20f) yield return null;
            if (mm.Phase.Value == MatchPhase.Lobby) { SLog("lever did not start - RequestStartRpc"); mm.RequestStartRpc(); }
            while (mm.Phase.Value != MatchPhase.Hiding) yield return null;
            yield return new WaitForSeconds(1.5f);

            var seekers = new List<NetPlayer>();
            var hiders = new List<NetPlayer>();
            foreach (var p in NetPlayer.All) if (p != null) (p.IsSeeker ? seekers : hiders).Add(p);
            SLog($"roles: seekers={seekers.Count} [{string.Join(", ", seekers.ConvertAll(p => p.Nick))}] hiders={hiders.Count} host seeker={me.IsSeeker}");
            if (seekers.Count >= 2)
                SLog($"seeker spawn spacing {Vector3.Distance(seekers[0].transform.position, seekers[1].transform.position):0.00} m");
            Shot(dir, "seek_hiding_blind");
            if (seekers.Count < 2 || hiders.Count < 1) { SLog("need 2 seekers + hiders - stop"); yield break; }
            var a = seekers.Contains(me) ? me : seekers[0];
            var b = seekers[0] == a ? seekers[1] : seekers[0];
            var h1 = hiders[0];
            var h2 = hiders.Count > 1 ? hiders[1] : null;
            while (mm.Phase.Value != MatchPhase.Seeking) yield return null;
            yield return new WaitForSeconds(1.5f);
            if (!OpenSpot(out var open)) open = h1.transform.position;

            // ---- ally on the minimap / name tag (host view)
            Vector3 fwd = FreeDir(open);
            a.TeleportRpc(open, Quaternion.LookRotation(fwd).eulerAngles.y);
            b.TeleportRpc(open + fwd * 5f, Quaternion.LookRotation(-fwd).eulerAngles.y);
            h1.TeleportRpc(open + Quaternion.Euler(0f, 90f, 0f) * fwd * 30f, 0f);
            if (h2 != null) h2.TeleportRpc(open - fwd * 30f, 0f);
            yield return new WaitForSeconds(1.5f);
            if (a == me) LookAt(me, b.transform.position + Vector3.up * 1.2f);
            yield return new WaitForSeconds(0.6f);
            Shot(dir, "seek_ally_view");

            // ---- breathing follows the nearest seeker: h1 4 m from B (A 30 m away), then far from both
            var sb = StressBreathing.Instance;
            a.TeleportRpc(open - fwd * 25f, 0f);
            h1.TeleportRpc(b.transform.position + Quaternion.Euler(0f, 90f, 0f) * fwd * 4f, 0f);
            yield return new WaitForSeconds(3f);
            SLog($"stress of {h1.Nick} 4 m from {b.Nick} (other seeker {Vector3.Distance(a.transform.position, h1.transform.position):0} m): {(sb != null ? sb.StressOf(h1) : -1f):0.00} (expected > 0.5)");
            h1.TeleportRpc(open + Quaternion.Euler(0f, 90f, 0f) * fwd * 40f, 0f);
            yield return new WaitForSeconds(4f);
            SLog($"stress of {h1.Nick} far from both: {(sb != null ? sb.StressOf(h1) : -1f):0.00} (expected low)");

            // ---- goo: h1 shoots, B is hit, A is not
            h1.TeleportRpc(open, Quaternion.LookRotation(fwd).eulerAngles.y);
            a.TeleportRpc(open - fwd * 4f, 0f);
            b.TeleportRpc(open + fwd * 5f, Quaternion.LookRotation(-fwd).eulerAngles.y);
            ItemDefs.Get(HotbarItem.ToyGun)?.Give(h1, 8);
            yield return new WaitForSeconds(1.5f);
            Vector3 eye = h1.transform.position + Vector3.up * 1.55f;
            mm.ServerToyShot(h1, eye, (b.transform.position + Vector3.up * 1.15f - eye).normalized);
            yield return new WaitForSeconds(1.2f);
            SLog($"goo: {b.Nick} gooed={b.Gooed} (expected True), {a.Nick} gooed={a.Gooed} (expected False)");
            if (a == me) { LookAt(me, b.transform.position + Vector3.up * 1.1f); yield return new WaitForSeconds(0.3f); Shot(dir, "seek_goo_on_ally"); }
            ItemLitShot(dir, "seek_goo_b", b.transform.position + fwd * -2.2f + Vector3.up * 1.6f + Quaternion.Euler(0f, 90f, 0f) * fwd * 0.8f, b.transform.position + Vector3.up * 1.1f);

            // ---- the one chainsaw
            Vector3 shelf = ChainsawShelf.Position + new Vector3(0f, 0.05f, -1.6f);
            a.TeleportRpc(shelf, 0f);
            b.TeleportRpc(shelf + new Vector3(0.9f, 0f, 0f), 0f);
            yield return new WaitForSeconds(1.5f);
            mm.ServerPickupChainsaw(a);
            yield return new WaitForSeconds(0.3f);
            mm.ServerPickupChainsaw(b);
            yield return new WaitForSeconds(0.3f);
            SLog($"saw: taken={mm.ChainsawTaken.Value} {a.Nick} has={a.HasChainsaw.Value}, {b.Nick} has={b.HasChainsaw.Value} (expected True/True/False)");
            int slot = Inventory.SlotOf(a, HotbarItem.Chainsaw);
            if (slot >= 0) mm.ServerDropSlot(a, (byte)slot);
            yield return new WaitForSeconds(2.5f);
            int dropId = -1;
            Vector3 dropAt = shelf;
            for (int i = 0; i < mm.Drops.Count; i++)
                if (mm.Drops[i].Kind == HotbarItem.Chainsaw) { dropId = mm.Drops[i].Id; dropAt = mm.DropPosition(mm.Drops[i]); }
            b.TeleportRpc(new Vector3(dropAt.x, shelf.y, dropAt.z) - Vector3.forward * 0.6f, 0f);
            yield return new WaitForSeconds(1.5f);
            mm.ServerPickupDrop(b, dropId);
            yield return new WaitForSeconds(0.5f);
            SLog($"saw dropped (#{dropId}) and taken: {a.Nick} has={a.HasChainsaw.Value}, {b.Nick} has={b.HasChainsaw.Value}, still taken={mm.ChainsawTaken.Value} (expected False/True/True)");

            // ---- Glock for both
            mm.TestGiveGlock();
            yield return new WaitForSeconds(1f);
            SLog($"glock: {a.Nick} {a.HasGlock.Value} ammo {a.Ammo.Value}+{a.SpareMags.Value}, {b.Nick} {b.HasGlock.Value} ammo {b.Ammo.Value}+{b.SpareMags.Value}");
            Shot(dir, "seek_glock");

            // ---- both attack at the same time (own cooldowns), then everybody down = seekers win
            while (b.Gooed) yield return null;
            Vector3 p1 = open, p2 = h2 != null && OpenSpotOther(open, out var o2) ? o2 : open + fwd * 12f;
            h1.TeleportRpc(p1, Quaternion.LookRotation(fwd).eulerAngles.y);
            a.TeleportRpc(p1 + fwd * 1.1f, Quaternion.LookRotation(-fwd).eulerAngles.y);
            if (h2 != null) h2.TeleportRpc(p2, 0f);
            b.TeleportRpc(p2 + Vector3.forward * 1.0f, 180f);
            yield return new WaitForSeconds(1.5f);
            a.AttackReadyAt.Value = 0;
            b.AttackReadyAt.Value = 0;
            b.Held.Value = HeldItem.Chainsaw;
            // First hit of each only wounds (quick slash); both at once, each with his own cooldown.
            mm.ServerKnife(a, h1);
            if (h2 != null) mm.ServerKnife(b, h2);
            yield return new WaitForSeconds(0.5f);
            SLog($"first hits: {h1.Nick} limping={h1.Limping.Value}, {(h2 != null ? h2.Nick + " limping=" + h2.Limping.Value : "-")}, " +
                 $"cooldowns {a.Nick} {a.AttackReadyAt.Value - mm.Now:0.0} s, {b.Nick} {b.AttackReadyAt.Value - mm.Now:0.0} s (expected True/True, ~7.5 s each)");
            yield return new WaitForSeconds(Tuning.AttackCooldown + 0.3f);
            h1.TeleportRpc(p1, Quaternion.LookRotation(fwd).eulerAngles.y);
            a.TeleportRpc(p1 + fwd * 1.1f, Quaternion.LookRotation(-fwd).eulerAngles.y);
            if (h2 != null) h2.TeleportRpc(p2, 0f);
            b.TeleportRpc(p2 + Vector3.forward * 1.0f, 180f);
            yield return new WaitForSeconds(1.2f);
            b.Held.Value = HeldItem.Chainsaw;
            mm.ServerKnife(a, h1);
            if (h2 != null) mm.ServerChainsaw(b, h2);
            yield return new WaitForSeconds(0.5f);
            SLog($"kill moves: {a.Nick} busy={a.Busy.Value} (knife), {b.Nick} busy={b.Busy.Value} (saw) (expected True/{h2 != null})");
            if (a == me) Shot(dir, "seek_double_attack");
            CamShot(dir, "seek_attack_a", p1 + Quaternion.Euler(0f, 90f, 0f) * fwd * 3f + Vector3.up * 1.4f, p1 + Vector3.up * 1f);
            float tEnd = Time.time + 14f;
            while (Time.time < tEnd && mm.Phase.Value == MatchPhase.Seeking) yield return null;
            double now = mm.Now;
            SLog($"after attacks: {h1.Nick}={h1.Life.Value} {(h2 != null ? h2.Nick + "=" + h2.Life.Value : "")} cooldowns {a.Nick} {a.AttackReadyAt.Value - now:0.0} s, {b.Nick} {b.AttackReadyAt.Value - now:0.0} s, kills {a.Kills.Value}/{b.Kills.Value}");
            if (mm.Phase.Value == MatchPhase.Seeking)
            {
                foreach (var h in hiders) if (h != null && h.Life.Value != LifeState.Dead) mm.TestKill(h);
                yield return new WaitForSeconds(1.5f);
            }
            SLog($"end: phase={mm.Phase.Value} winner={mm.Winner.Value} (expected Ended / Seeker)");
            yield return new WaitForSeconds(0.6f);
            Shot(dir, "seek_win");
            SLog("done");
        }

        /// <summary>Clients (-seekwatch): role, the hiding screen, own goo and the result.</summary>
        private IEnumerator SeekersWatch()
        {
            string dir = SeekShots;
            var mm = MatchManager.Instance;
            while (mm == null || mm.Phase.Value != MatchPhase.Hiding || NetPlayer.Local == null) { mm = MatchManager.Instance; yield return null; }
            var me = NetPlayer.Local;
            yield return new WaitForSeconds(2f);
            Debug.Log($"[SeekWatch] {me.Nick}: role {me.Role.Value}, seekers {MatchManager.SeekersNow()}, mates '{MatchManager.OtherSeekerNames(me)}'");
            Shot(dir, $"seekwatch_{me.Nick}_hiding");
            bool goo = false;
            while (mm.Phase.Value == MatchPhase.Hiding || mm.Phase.Value == MatchPhase.Seeking)
            {
                if (!goo && me.Gooed) { goo = true; yield return new WaitForSeconds(0.4f); Shot(dir, $"seekwatch_{me.Nick}_gooed"); }
                yield return null;
            }
            yield return new WaitForSeconds(1f);
            Debug.Log($"[SeekWatch] {me.Nick}: result winner={mm.Winner.Value}");
            Shot(dir, $"seekwatch_{me.Nick}_end");
        }

        /// <summary>Freest flat direction from <paramref name="at"/> (8 directions).</summary>
        private static Vector3 FreeDir(Vector3 at)
        {
            Vector3 best = Vector3.forward;
            float bestD = -1f;
            for (int i = 0; i < 8; i++)
            {
                Vector3 d = Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward;
                float free = Physics.SphereCast(at + Vector3.up * 1f, 0.4f, d, out var h, 12f, ~((1 << 9) | (1 << PlayerAvatarBinder.LocalBodyLayer)), QueryTriggerInteraction.Ignore) ? h.distance : 12f;
                if (free > bestD) { bestD = free; best = d; }
            }
            return best;
        }

        /// <summary>Another open hider spawn at least 15 m from <paramref name="away"/>.</summary>
        private static bool OpenSpotOther(Vector3 away, out Vector3 pos)
        {
            pos = default;
            foreach (var sp in SpawnPoints.Hider())
            {
                if (Vector3.Distance(sp.position, away) < 15f) continue;
                bool ok = true;
                for (int i = 0; i < 8 && ok; i++)
                {
                    Vector3 d = Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward;
                    if (Physics.SphereCast(sp.position + Vector3.up * 0.9f, 0.3f, d, out _, 2.5f, ~(1 << PlayerAvatarBinder.LocalBodyLayer), QueryTriggerInteraction.Ignore)) ok = false;
                }
                if (ok) { pos = sp.position + Vector3.up * 0.05f; return true; }
            }
            return false;
        }
    }
}
