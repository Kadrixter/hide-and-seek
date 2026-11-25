using System.Collections;
using HideAndSeek.Core;
using HideAndSeek.Map;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Chainsaw nerf test (-testsawnerf &lt;dir&gt; on the host = seeker: -forceseeker -autostart -autostartplayers 2, and on a
    /// hider client): fuel canisters on the map and the seeker's minimap, a full saw = 12, -30% speed at full throttle,
    /// the red saw on the hider's minimap (same level yes, other level no), the engine stalls while blinded, the hider
    /// wrests the saw from a foamed and from a knocked-out seeker (the client presses E at the real prompt), a saw / a
    /// canister thrown away by a hider is hidden from the seeker's minimap, the canister fills the saw up.
    /// Logs: [SawNerfTest] ... PASS / FAIL (client lines say "client").
    /// </summary>
    public partial class NetSession
    {
        private static void SnLog(string s) => Debug.Log("[SawNerfTest] " + s);

        private void StartSawNerfTests(System.Func<string, bool> has)
        {
            if (!has("-testsawnerf")) return;
            string dir = ArgOr("-testsawnerf", "");
            StartCoroutine(SawNerfTest(dir));
        }

        private IEnumerator SawNerfTest(string dir)
        {
            yield return WaitSeeking();
            yield return Manager.IsServer ? SawNerfHost(dir) : SawNerfClient(dir);
        }

        private static int CountDrops(MatchManager mm, HotbarItem kind, out int id, out byte extra, out Vector3 at)
        {
            int n = 0;
            id = -1; extra = 0; at = default;
            for (int i = 0; i < mm.Drops.Count; i++)
            {
                if (mm.Drops[i].Kind != kind) continue;
                n++;
                id = mm.Drops[i].Id;
                extra = mm.Drops[i].Extra;
                at = mm.DropPosition(mm.Drops[i]);
            }
            return n;
        }

        private IEnumerator SawNerfHost(string dir)
        {
            var mm = MatchManager.Instance;
            var me = NetPlayer.Local;
            var h = SfHider();
            if (!me.IsSeeker || h == null) { SnLog("need the host as seeker (-forceseeker) and a hider client"); yield break; }
            int cans = CountDrops(mm, HotbarItem.FuelCan, out _, out _, out _);
            yield return new WaitForSeconds(1f);
            SnLog($"canisters dealt={mm.FuelCansDealt} on the floor={cans} seeker map marks={Minimap.SeekerDropMarks} -> {Pf(cans == SawTuning.Cans && Minimap.SeekerDropMarks >= cans)}");
            Shot(dir, "h_a_minimap_cans");

            // Saw from the shelf: 12.
            TestLight(ChainsawShelf.Position + new Vector3(0f, 2.4f, -1.2f), 7f, 1.6f);
            Vector3 stand = ChainsawShelf.Position + new Vector3(0f, 0.05f, -1.4f);
            me.TeleportLocal(stand, 0f);
            me.Motor.SetLook(0f, 10f);
            yield return new WaitForSeconds(1.2f);
            int[] rolls = new int[5];
            for (int i = 0; i < 20000; i++) rolls[MatchManager.RollSawTries(Random.value)]++;
            SnLog($"pull-start rolls of 20000: 1 try {rolls[1] / 200f:0.0}%, 2 tries {rolls[2] / 200f:0.0}%, 4 tries {rolls[4] / 200f:0.0}% -> " +
                  $"{Pf(Mathf.Abs(rolls[2] / 200f - 25f) < 1.5f && Mathf.Abs(rolls[4] / 200f - 10f) < 1.2f && rolls[3] == 0)}");
            MatchManager.ForcedSawTries = 4;
            LocalPlayerController.TestSawRev = true; // F held the whole time: nothing until the engine catches
            mm.ServerPickupChainsaw(me);
            float tp = Time.time;
            bool revEarly = false, shotPull = false;
            while (!me.SawReady && Time.time - tp < 5f)
            {
                if (me.SawRev.Value) revEarly = true;
                if (!shotPull && Time.time - tp > 1.25f) { shotPull = true; Shot(dir, "h_a2_pull_start"); }
                yield return null;
            }
            float took = Time.time - tp;
            yield return new WaitForSeconds(0.4f);
            SnLog($"4-try pull-start: tries={me.SawAttempts.Value} ready after {took:0.00} s (expected ~{HideAndSeek.Characters.BlockyProps.ChainsawFireAt(4) + SawTuning.PullDelay:0.00}), rev before ready={revEarly}, rev after={me.SawRev.Value} -> " +
                  $"{Pf(me.SawAttempts.Value == 4 && took > 2.2f && took < 3.1f && !revEarly && me.SawRev.Value)}");
            LocalPlayerController.TestSawRev = false;
            MatchManager.ForcedSawTries = 0;
            yield return new WaitForSeconds(0.5f);
            SnLog($"shelf saw: fuel={me.SawFuel.Value:0.0} full={SawFuel.Full} -> {Pf(me.HasChainsaw.Value && me.SawFuel.Value > 10f)}");
            SnLog($"saw in hands, idle: sawScale={me.SawSpeedScale:0.00} motor={me.Motor.SpeedScale:0.00} -> {Pf(Mathf.Abs(me.SawSpeedScale - 0.85f) < 0.01f && me.Motor.SpeedScale < 0.86f)}");
            int knifeSlot = Inventory.SlotOf(me, HotbarItem.Knife);
            if (knifeSlot >= 0) Hotbar.TestSelect = knifeSlot;
            yield return new WaitForSeconds(0.8f);
            mm.ServerEquip(me, HeldItem.Knife);
            yield return new WaitForSeconds(0.5f);
            SnLog($"put the saw away (key {knifeSlot + 1} + server equip): held={me.Held.Value} -> {Pf(me.Held.Value == HeldItem.Chainsaw)}");

            // Hider on the same level, 6 m away: rev 2.5 s (client counts the red saw on its minimap).
            h.TeleportRpc(stand + Vector3.back * 6f, 0f);
            yield return new WaitForSeconds(1.5f);
            SnLog($"PHASE same-level rev (hider dy={h.transform.position.y - me.transform.position.y:0.0})");
            LocalPlayerController.TestSawRev = true;
            yield return new WaitForSeconds(1.2f);
            SnLog($"rev: running={me.SawRunning} sawScale={me.SawSpeedScale:0.00} combat={me.CombatSpeedScale:0.00} motor={me.Motor.SpeedScale:0.00} -> {Pf(me.SawRunning && Mathf.Abs(me.SawSpeedScale - 0.85f) < 0.01f && me.Motor.SpeedScale < 0.86f)}");
            yield return new WaitForSeconds(1.3f);
            LocalPlayerController.TestSawRev = false;
            yield return new WaitForSeconds(0.5f);
            SnLog($"idle again: sawScale={me.SawSpeedScale:0.00} motor={me.Motor.SpeedScale:0.00} -> {Pf(Mathf.Abs(me.SawSpeedScale - 0.85f) < 0.01f)}");
            yield return new WaitForSeconds(1.5f);

            // Hider on another level: rev again (nothing on its minimap).
            int myLv = LevelLighting.LevelAt(me.transform.position.y + 0.3f);
            int otherLv = (myLv + 1) % MapInfo.Levels;
            var spots = FuseSpots.RoadSpots(otherLv);
            if (spots.Count > 0)
            {
                h.TeleportRpc(spots[spots.Count / 2], 0f);
                yield return new WaitForSeconds(1.5f);
                SnLog($"PHASE other-level rev (seeker level {myLv}, hider level {otherLv}, dy={h.transform.position.y - me.transform.position.y:0.0})");
                LocalPlayerController.TestSawRev = true;
                yield return new WaitForSeconds(2.5f);
                LocalPlayerController.TestSawRev = false;
                yield return new WaitForSeconds(1.8f);
            }
            else SnLog($"no road spot on level {otherLv} - other-level part skipped");

            // Blinded: the engine dies, back after the dust.
            me.BlindUntil.Value = mm.Now + 2.0;
            LocalPlayerController.TestSawRev = true;
            yield return new WaitForSeconds(0.6f);
            SnLog($"blinded + F: stalled={me.SawStalled} sawRev={me.SawRev.Value} -> {Pf(me.SawStalled && !me.SawRev.Value)}");
            yield return new WaitForSeconds(1.6f);
            float tb = Time.time;
            while (!me.SawReady && Time.time - tb < 4f) yield return null;
            yield return new WaitForSeconds(0.4f);
            SnLog($"dust gone + F: stalled={me.SawStalled} new pull-start {me.SawAttempts.Value} tries, ready after {Time.time - tb - 0.4f:0.0} s, sawRev={me.SawRev.Value} -> {Pf(!me.SawStalled && me.SawRev.Value)}");
            LocalPlayerController.TestSawRev = false;
            yield return new WaitForSeconds(0.5f);

            // Foamed: the hider in front of him presses E.
            h.TeleportRpc(stand + Vector3.forward * 1.1f, 180f);
            yield return new WaitForSeconds(1.5f);
            SnLog("PHASE wrest foam");
            me.FoamStart.Value = mm.Now;
            me.FoamUntil.Value = mm.Now + 7.0;
            float t0 = Time.time;
            while (!h.HasChainsaw.Value && Time.time - t0 < 7f) yield return null;
            yield return new WaitForSeconds(0.5f);
            string hint = Hotbar.Hint(h, HotbarItem.Chainsaw);
            SnLog($"foamed seeker: hider has={h.HasChainsaw.Value} seeker has={me.HasChainsaw.Value} held={me.Held.Value} after {Time.time - t0:0.0} s, wrests={mm.SawWrests} -> {Pf(h.HasChainsaw.Value && !me.HasChainsaw.Value && me.Held.Value != HeldItem.Chainsaw)}");
            SnLog($"hider hint '{hint}' sawRev={h.SawRev.Value} running={h.SawRunning} -> {Pf(hint.Contains("nie odpalisz") && !h.SawRunning)}");
            me.FoamUntil.Value = 0;

            // The hider hides it: not on the seeker's minimap.
            mm.ServerDropSlot(h, (byte)Mathf.Max(0, Inventory.SlotOf(h, HotbarItem.Chainsaw)));
            yield return new WaitForSeconds(3f);
            int canNow = CountDrops(mm, HotbarItem.FuelCan, out _, out _, out _);
            CountDrops(mm, HotbarItem.Chainsaw, out int sawId, out byte sawExtra, out Vector3 sawAt);
            SnLog($"hider dropped the saw: drop #{sawId} extra={sawExtra} byHider={SawFuel.ByHider(sawExtra)} seeker marks={Minimap.SeekerDropMarks} (canisters {canNow}) -> {Pf(sawId >= 0 && SawFuel.ByHider(sawExtra) && Minimap.SeekerDropMarks == canNow)}");
            Shot(dir, "h_b_minimap_saw_hidden");
            me.TeleportLocal(sawAt + new Vector3(0f, 0f, -0.8f), 0f);
            yield return new WaitForSeconds(0.6f);
            mm.ServerPickupDrop(me, sawId);
            yield return new WaitForSeconds(0.8f);
            SnLog($"seeker took it back: has={me.HasChainsaw.Value} fuel={me.SawFuel.Value:0.0} -> {Pf(me.HasChainsaw.Value && me.SawFuel.Value > 0f)}");
            me.Held.Value = HeldItem.Chainsaw;
            me.TeleportLocal(stand, 0f);
            yield return new WaitForSeconds(0.8f);

            // Knocked out by a leg: the hider takes it again.
            h.TeleportRpc(stand + Vector3.forward * 1.1f, 180f);
            yield return new WaitForSeconds(1.5f);
            SnLog("PHASE wrest stun");
            me.ServerStartStun();
            t0 = Time.time;
            while (!h.HasChainsaw.Value && Time.time - t0 < 8f) yield return null;
            SnLog($"knocked-out seeker: hider has={h.HasChainsaw.Value} seeker has={me.HasChainsaw.Value} after {Time.time - t0:0.0} s -> {Pf(h.HasChainsaw.Value && !me.HasChainsaw.Value)}");
            yield return new WaitForSeconds(1f);
            me.StunAt.Value = 0d;
            yield return new WaitForSeconds(0.8f);
            mm.ServerDropSlot(h, (byte)Mathf.Max(0, Inventory.SlotOf(h, HotbarItem.Chainsaw)));
            yield return new WaitForSeconds(3f);
            CountDrops(mm, HotbarItem.Chainsaw, out sawId, out _, out sawAt);
            me.TeleportLocal(sawAt + new Vector3(0f, 0f, -0.8f), 0f);
            yield return new WaitForSeconds(0.6f);
            mm.ServerPickupDrop(me, sawId);
            yield return new WaitForSeconds(0.8f);
            SnLog($"saw back with the seeker: has={me.HasChainsaw.Value} busy={me.Busy.Value} fuel={me.SawFuel.Value:0.0}");

            // A canister: the hider takes one (hides it), the seeker fills the saw with the other.
            CountDrops(mm, HotbarItem.FuelCan, out int canId, out _, out Vector3 canAt);
            if (canId < 0) { SnLog("no canister left - FAIL"); yield break; }
            ItemLitShot(dir, "h_c_canister_floor", canAt + new Vector3(0.8f, 0.7f, -0.6f), canAt);
            yield return new WaitForSeconds(0.4f);
            int canId2 = -1;
            for (int i = 0; i < mm.Drops.Count; i++)
                if (mm.Drops[i].Kind == HotbarItem.FuelCan && mm.Drops[i].Id != canId) { canId2 = mm.Drops[i].Id; h.TeleportRpc(mm.DropPosition(mm.Drops[i]) + Vector3.back * 0.8f, 0f); }
            if (canId2 >= 0)
            {
                yield return new WaitForSeconds(1.2f);
                mm.ServerPickupDrop(h, canId2);
                yield return new WaitForSeconds(0.8f);
                SnLog($"hider took a canister: cans={h.FuelCans.Value} hint '{Hotbar.Hint(h, HotbarItem.FuelCan)}' -> {Pf(h.FuelCans.Value == 1)}");
                mm.ServerDropSlot(h, (byte)Mathf.Max(0, Inventory.SlotOf(h, HotbarItem.FuelCan)));
                yield return new WaitForSeconds(3f);
                bool hidden = false;
                for (int i = 0; i < mm.Drops.Count; i++)
                    if (mm.Drops[i].Kind == HotbarItem.FuelCan && SawFuel.ByHider(mm.Drops[i].Extra)) hidden = true;
                SnLog($"hider hid the canister: hidden={hidden} seeker marks={Minimap.SeekerDropMarks} (expected 1) -> {Pf(hidden && Minimap.SeekerDropMarks == 1)}");
            }
            me.TeleportLocal(canAt + new Vector3(0f, 0f, -0.8f), 0f);
            yield return new WaitForSeconds(0.6f);
            mm.ServerPickupDrop(me, canId);
            yield return new WaitForSeconds(0.8f);
            int slot = Inventory.SlotOf(me, HotbarItem.FuelCan);
            yield return new WaitForSeconds(1.2f);
            me.Motor.SetLook(0f, 0f);
            float before = me.SawFuel.Value;
            hint = Hotbar.Hint(me, HotbarItem.Chainsaw);
            SnLog($"seeker canister: cans={me.FuelCans.Value} slot={slot} held={me.Held.Value} saw hint '{hint}' fuel={before:0.0} -> {Pf(me.FuelCans.Value == 1 && me.Held.Value == HeldItem.Chainsaw && hint.Contains("R - zatankuj"))}");
            Shot(dir, "h_d_saw_hint_refuel");
            yield return new WaitForSeconds(0.3f);
            mm.ServerRefuelFromHands(me); // = R with the saw in the hands
            yield return new WaitForSeconds(1.2f);
            SnLog($"refuelled: fuel {before:0.0} -> {me.SawFuel.Value:0.0}, cans={me.FuelCans.Value} -> {Pf(Mathf.Abs(me.SawFuel.Value - SawFuel.Full) < 0.01f && me.FuelCans.Value == 0)}");
            Shot(dir, "h_e_refuelled");
            SnLog("done");
        }

        private IEnumerator SawNerfClient(string dir)
        {
            float end = Time.time + 170f, nextLog = Time.time + 5f, nextPress = 0f;
            int sameN = 0, sameShown = 0, otherN = 0, otherShown = 0, presses = 0;
            bool shotMap = false;
            while (Time.time < end)
            {
                var me = NetPlayer.Local;
                var lpc = LocalPlayerController.Instance;
                if (me == null || lpc == null) { yield return null; continue; }
                foreach (var s in NetPlayer.All)
                {
                    if (s == null || !s.IsSeeker || !s.SawRunning) continue;
                    bool same = LevelLighting.LevelAt(s.transform.position.y + 0.3f) == LevelLighting.LevelAt(me.transform.position.y + 0.3f);
                    bool shown = Minimap.SawAlarmMarks > 0;
                    if (same) { sameN++; if (shown) sameShown++; }
                    else { otherN++; if (shown) otherShown++; }
                    if (same && shown && !shotMap) { shotMap = true; Shot(dir, "c_a_minimap_red_saw"); }
                }
                if (lpc.Prompt == PromptKind.SawWrest && Time.time >= nextPress && presses < 4)
                {
                    SnLog($"client prompt '{lpc.PromptText}'");
                    Shot(dir, $"c_b_wrest_prompt_{presses}");
                    yield return new WaitForSeconds(0.5f);
                    LocalPlayerController.TestWrestE = true;
                    presses++;
                    nextPress = Time.time + 3f;
                    yield return new WaitForSeconds(1f);
                    Shot(dir, $"c_c_after_wrest_{presses}");
                    SnLog($"client after E: has saw={me.HasChainsaw.Value} hint '{Hotbar.Hint(me, HotbarItem.Chainsaw)}'");
                }
                if (Time.time >= nextLog)
                {
                    nextLog = Time.time + 5f;
                    SnLog($"client minimap: same level running {sameN} (shown {sameShown}), other level running {otherN} (shown {otherShown}) -> " +
                          $"{Pf(sameN > 0 && sameShown >= sameN * 0.85f && otherShown == 0)}");
                }
                yield return new WaitForSeconds(0.2f);
            }
        }
    }
}
