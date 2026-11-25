using System;
using System.Collections;
using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Chainsaw option and fuel tests (host = seeker: -forceseeker -autostart -autostartplayers 2 -hidetime N, + a hider client):
    ///  -testsawfuel &lt;dir&gt;: saw from the shelf is full (20), holding F burns 1/s, the hint shows "paliwo X/20", a dropped
    ///      and picked up saw keeps its fuel, at 10/s it runs dry (toast), an empty saw does not start and does not cut.
    ///  -testnosaw &lt;dir&gt;: lobby option "Piła łańcuchowa: wyłączona" (settings panel + board photo), in the round the
    ///      shelf is empty, no prompt, the pickup is refused.
    /// Logs: [SawFuelTest] ... PASS / FAIL.
    /// </summary>
    public partial class NetSession
    {
        private static void SfLog(string s) => Debug.Log("[SawFuelTest] " + s);
        private static string Pf(bool ok) => ok ? "PASS" : "FAIL";

        private void StartSawFuelTests(Func<string, bool> has)
        {
            if (has("-testsawfuel")) StartCoroutine(SawFuelTest(ArgOr("-testsawfuel", "")));
            if (has("-testnosaw")) StartCoroutine(NoSawTest(ArgOr("-testnosaw", "")));
            if (has("-testdropsafe")) StartCoroutine(DropSafeTest(ArgOr("-testdropsafe", ""))); // NetSession.DropSafeTests.cs
            if (has("-testrc4")) StartCoroutine(Rc4Test(ArgOr("-testrc4", ""))); // NetSession.Rc4Tests.cs
        }

        private static NetPlayer SfHider()
        {
            foreach (var p in NetPlayer.All) if (p != null && p.IsHider) return p;
            return null;
        }

        private IEnumerator SawFuelTest(string dir)
        {
            yield return WaitSeeking();
            var mm = MatchManager.Instance;
            var me = NetPlayer.Local;
            if (!me.IsSeeker || !Manager.IsServer) { SfLog("need the host as seeker (-forceseeker)"); yield break; }
            SfLog($"settings: NoSaw={mm.Settings.Value.NoSaw} SawUse={mm.Settings.Value.SawUseRate} taken={mm.ChainsawTaken.Value}");
            TestLight(ChainsawShelf.Position + new Vector3(0f, 2.4f, -1.2f), 7f, 1.6f);

            // 1. From the shelf: full.
            me.TeleportLocal(ChainsawShelf.Position + new Vector3(0f, 0.05f, -1.4f), 0f);
            me.Motor.SetLook(0f, 15f);
            yield return new WaitForSeconds(1.5f);
            var lpc = LocalPlayerController.Instance;
            SfLog($"shelf prompt: {lpc.Prompt} '{lpc.PromptText}' -> {Pf(lpc.Prompt == PromptKind.ChainsawPickup)}");
            Shot(dir, "saw_a_shelf_prompt");
            mm.ServerPickupChainsaw(me);
            yield return new WaitForSeconds(1.0f);
            SfLog($"picked up: has={me.HasChainsaw.Value} held={me.Held.Value} fuel={me.SawFuel.Value:0.0} -> {Pf(me.HasChainsaw.Value && Mathf.Abs(me.SawFuel.Value - SawFuel.Full) < 0.01f)}");

            // 2. Hold F 3 s at 1/s.
            LocalPlayerController.TestSawRev = true;
            yield return new WaitForSeconds(1.5f);
            Shot(dir, "saw_b_revving");
            yield return new WaitForSeconds(1.5f);
            LocalPlayerController.TestSawRev = false;
            yield return new WaitForSeconds(0.5f);
            float f1 = me.SawFuel.Value;
            string hint = Hotbar.Hint(me, HotbarItem.Chainsaw);
            SfLog($"after 3 s of F: fuel={f1:0.0} (expected ~17) hint='{hint}' -> {Pf(f1 > 16.2f && f1 < 17.8f && hint.Contains("paliwo"))}");
            Shot(dir, "saw_c_hint_fuel");

            // 3. Drop it and take it back: same fuel.
            int slot = Inventory.SlotOf(me, HotbarItem.Chainsaw);
            mm.ServerDropSlot(me, (byte)Mathf.Max(0, slot));
            yield return new WaitForSeconds(3f);
            int id = -1; byte extra = 0; Vector3 at = default;
            for (int i = 0; i < mm.Drops.Count; i++)
                if (mm.Drops[i].Kind == HotbarItem.Chainsaw) { id = mm.Drops[i].Id; extra = mm.Drops[i].Extra; at = mm.DropPosition(mm.Drops[i]); }
            SfLog($"dropped: slot={slot} drop #{id} extra={extra} -> fuel {SawFuel.Unpack(extra):0.0} has={me.HasChainsaw.Value} taken={mm.ChainsawTaken.Value}");
            ItemLitShot(dir, "saw_d_on_floor", at + new Vector3(0.9f, 0.9f, -0.7f), at);
            me.TeleportLocal(at + new Vector3(0f, 0f, -0.8f), 0f);
            yield return new WaitForSeconds(0.5f);
            mm.ServerPickupDrop(me, id);
            yield return new WaitForSeconds(0.8f);
            SfLog($"picked up from the floor: has={me.HasChainsaw.Value} fuel={me.SawFuel.Value:0.0} (was {f1:0.0}) -> {Pf(me.HasChainsaw.Value && Mathf.Abs(me.SawFuel.Value - f1) < 0.15f)}");
            slot = Inventory.SlotOf(me, HotbarItem.Chainsaw);
            if (slot >= 0) Hotbar.TestSelect = slot;
            yield return new WaitForSeconds(0.8f);
            if (me.Held.Value != HeldItem.Chainsaw) me.Held.Value = HeldItem.Chainsaw;

            // 4. Faster use (slider 10/s): runs dry.
            var s = mm.Settings.Value;
            s.SawUse = 10;
            mm.Settings.Value = s;
            yield return new WaitForSeconds(0.3f);
            LocalPlayerController.TestSawRev = true;
            float t0 = Time.time;
            while (me.SawFuel.Value > 0f && Time.time - t0 < 6f) yield return null;
            float took = Time.time - t0;
            yield return new WaitForSeconds(0.4f);
            Shot(dir, "saw_e_empty_toast");
            SfLog($"ran dry after {took:0.0} s at 10/s (expected ~{f1 / 10f:0.0}): fuel={me.SawFuel.Value:0.0} sawRev={me.SawRev.Value} (expected False) -> {Pf(me.SawFuel.Value <= 0f && !me.SawRev.Value && took < 3f)}");
            yield return new WaitForSeconds(1.5f);
            SfLog($"F still held on an empty saw: sawRev={me.SawRev.Value} -> {Pf(!me.SawRev.Value)}");

            // 5. Empty saw on a hider: no cut.
            var h = SfHider();
            if (h != null)
            {
                h.TeleportRpc(me.transform.position + me.transform.forward * 1.0f, me.transform.eulerAngles.y + 180f);
                yield return new WaitForSeconds(1.2f);
                me.AttackReadyAt.Value = 0;
                mm.ServerChainsaw(me, h);
                yield return new WaitForSeconds(1.5f);
                SfLog($"empty saw on {h.Nick}: life={h.Life.Value} severed={h.Severed.Value} busy={h.Busy.Value} -> {Pf(h.Life.Value == LifeState.Alive && h.Severed.Value == 0)}");
            }
            LocalPlayerController.TestSawRev = false;
            hint = Hotbar.Hint(me, HotbarItem.Chainsaw);
            SfLog($"empty hint '{hint}' -> {Pf(hint.Contains("bez paliwa"))}");
            Shot(dir, "saw_f_empty_hint");
            s = mm.Settings.Value;
            s.SawUse = 1;
            mm.Settings.Value = s;
            SfLog("done");
        }

        private IEnumerator NoSawTest(string dir)
        {
            while (MatchManager.Instance == null || NetPlayer.Local == null) yield return null;
            var mm = MatchManager.Instance;
            if (!Manager.IsServer) yield break;
            var s = mm.Settings.Value;
            s.NoSaw = true;
            mm.SetSettingsRpc(s);
            yield return new WaitForSeconds(1.0f);
            SfLog($"lobby: NoSaw={mm.Settings.Value.NoSaw}");
            GameHud.OpenSettingsPanel();
            yield return new WaitForSeconds(0.8f);
            Shot(dir, "nosaw_a_settings_panel");
            yield return new WaitForSeconds(0.4f); // the PNG is written at the end of the frame
            GameHud.CloseAllMenus();
            var board = LobbySettingsBoard.Instance;
            if (board != null) ItemLitShot(dir, "nosaw_b_board", board.transform.position - board.transform.forward * 3.1f + Vector3.up * 0.1f, board.transform.position);

            yield return WaitSeeking();
            var me = NetPlayer.Local;
            SfLog($"round: NoSaw={mm.Settings.Value.NoSaw} taken={mm.ChainsawTaken.Value} -> {Pf(mm.ChainsawTaken.Value)}");
            if (!me.IsSeeker) { SfLog("host is not the seeker - only the state checked"); yield break; }
            TestLight(ChainsawShelf.Position + new Vector3(0f, 2.4f, -1.2f), 7f, 1.6f);
            me.TeleportLocal(ChainsawShelf.Position + new Vector3(0f, 0.05f, -1.4f), 0f);
            me.Motor.SetLook(0f, 30f);
            yield return new WaitForSeconds(1.5f);
            var lpc = LocalPlayerController.Instance;
            SfLog($"shelf prompt: {lpc.Prompt} '{lpc.PromptText}' -> {Pf(lpc.Prompt != PromptKind.ChainsawPickup)}");
            Shot(dir, "nosaw_c_empty_shelf");
            mm.ServerPickupChainsaw(me);
            yield return new WaitForSeconds(1.0f);
            SfLog($"pickup try: has={me.HasChainsaw.Value} -> {Pf(!me.HasChainsaw.Value)}");
            // Nobody has it and no drop: the lost-saw check must not put it back on the shelf.
            yield return new WaitForSeconds(3f);
            SfLog($"after 3 s: taken={mm.ChainsawTaken.Value} -> {Pf(mm.ChainsawTaken.Value)}");
            SfLog("done");
        }
    }
}
