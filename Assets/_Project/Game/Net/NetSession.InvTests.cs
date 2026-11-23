using System;
using System.Collections;
using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// -testinv on both copies (host -autostart -hidetime N -forcehider, client = seeker; + -invshots &lt;folder&gt;):
    /// hider: empty hotbar, pickups into the first free slots, selection frame, Q drop (falls, lands, prompt, E back),
    /// RC car dropped and taken again, refused E on the seeker's dropped chainsaw, fills the seeker's inventory and
    /// gives the Glock, hint pings on the seeker's level / another level.
    /// seeker: knife cannot be dropped, empty slot = empty hands, saw from the shelf, drop it / take it back, map medkit
    /// taken away, full inventory -> Glock on the floor + minimap marker, frees a slot and takes the Glock, minimap pins.
    /// </summary>
    public partial class NetSession
    {
        private void StartInvTests(Func<string, bool> has)
        {
            if (has("-testinv")) StartCoroutine(InvTest());
        }

        private static string InvShots => ArgOr("-invshots", "");

        private static void InvLog(NetPlayer p, string what) =>
            Debug.Log($"[InvTest] {what}: slots={p.Inv.Value} sel={Hotbar.Selected(p) + 1} held={p.Held.Value} kinds={Inventory.Kinds(p)} " +
                      $"med={p.MedkitCount.Value} syr={p.SyringeCount.Value} rod={p.RodCount.Value} saw={p.HasChainsaw.Value} glock={p.HasGlock.Value} " +
                      $"drops={MatchManager.Instance.Drops.Count} landings={DroppedItemView.Landings}");

        private IEnumerator InvTest()
        {
            yield return WaitSeeking();
            if (NetPlayer.Local.IsSeeker) yield return InvSeeker();
            else yield return InvHider();
        }

        /// <summary>Stands 1.2 m from <paramref name="at"/> and looks at it.</summary>
        private static IEnumerator LookAt(NetPlayer me, Vector3 at, Vector3 from)
        {
            Vector3 d = at - from;
            d.y = 0f;
            if (d.sqrMagnitude < 0.01f) d = Vector3.forward;
            Vector3 stand = at - d.normalized * 1.2f;
            stand.y = at.y + 0.05f;
            if (Physics.Raycast(stand + Vector3.up * 1.2f, Vector3.down, out var hit, 3f, ~(1 << PlayerAvatarBinder.LocalBodyLayer), QueryTriggerInteraction.Ignore))
                stand.y = hit.point.y + 0.05f;
            me.TeleportLocal(stand, Quaternion.LookRotation(d).eulerAngles.y);
            Vector3 eye = stand + Vector3.up * 1.6f;
            Vector3 look = at - eye;
            me.Motor.SetLook(Quaternion.LookRotation(d).eulerAngles.y, -Mathf.Atan2(-look.y, new Vector2(look.x, look.z).magnitude) * Mathf.Rad2Deg);
            yield return new WaitForSeconds(0.9f);
        }

        /// <summary>Lit PNG that never stops the test (disk full on a busy machine).</summary>
        private static void SafeLitShot(string dir, string name, Vector3 pos, Vector3 at)
        {
            try { ItemLitShot(dir, name, pos, at); }
            catch (Exception e) { Debug.Log($"[InvTest] shot {name} failed: {e.Message}"); }
        }

        private static bool FindDrop(HotbarItem kind, out DropState d)
        {
            var mm = MatchManager.Instance;
            for (int i = 0; i < mm.Drops.Count; i++)
                if (mm.Drops[i].Kind == kind) { d = mm.Drops[i]; return true; }
            d = default;
            return false;
        }

        private IEnumerator InvHider()
        {
            var mm = MatchManager.Instance;
            var me = NetPlayer.Local;
            string dir = InvShots;
            NetPlayer seeker = null;
            foreach (var p in NetPlayer.All) if (p != null && p.IsSeeker) seeker = p;
            InvLog(me, "start (hider)");
            var ns = mm.Settings.Value;
            ns.HintMode = 0; // only the test's own pings on the seeker's minimap
            Debug.Log($"[InvTest] option off (default {NetSettings.Default.SeekerTakesItems}): seeker may take a medkit={Inventory.CanPickUp(seeker, HotbarItem.Medkit, MatchPhase.Seeking)}, his saw={Inventory.CanPickUp(seeker, HotbarItem.Chainsaw, MatchPhase.Seeking)}");
            ns.SeekerTakesItems = true; // the seeker part takes a map medkit away
            mm.Settings.Value = ns;
            yield return null;
            Debug.Log($"[InvTest] option on: seeker may take a medkit={Inventory.CanPickUp(seeker, HotbarItem.Medkit, MatchPhase.Seeking)}");
            Shot(dir, "h01_empty");

            if (mm.Medkits.Count > 0)
            {
                var m = mm.Medkits[0];
                yield return LookAt(me, m.Position + Vector3.up * 0.15f, me.transform.position);
                Debug.Log($"[InvTest] medkit prompt '{LocalPlayerController.Instance.PromptText}'");
                me.RequestPickupRpc(m.Id);
                yield return new WaitForSeconds(0.6f);
            }
            if (mm.Syringes.Count > 0)
            {
                var s = mm.Syringes[0];
                yield return LookAt(me, s.Position + Vector3.up * 0.1f, me.transform.position);
                me.RequestPickupSyringeRpc(s.Id);
                yield return new WaitForSeconds(0.6f);
            }
            if (mm.Rods.Count == 0) mm.SpawnRods(2);
            var rod = mm.Rods[0];
            yield return LookAt(me, rod.Exposed, rod.Embedded ? rod.Stand : me.transform.position);
            me.RequestPickupRodRpc(rod.Id);
            yield return new WaitForSeconds(rod.Embedded ? HideAndSeek.Characters.RodPullTimeline.Duration + 1f : 0.8f);
            InvLog(me, "after map pickups");
            Shot(dir, "h02_items");
            Hotbar.TestSelect = Hotbar.SlotOf(me, HotbarItem.Syringe);
            yield return new WaitForSeconds(0.5f);
            Shot(dir, "h03_select_syringe");

            // Q on the medkit: thrown in front, falls, lands with a sound, E takes it back.
            if (OpenSpot(out var open)) { me.TeleportLocal(open, 0f); me.Motor.SetLook(0f, -15f); yield return new WaitForSeconds(0.8f); }
            Hotbar.TestSelect = Hotbar.SlotOf(me, HotbarItem.Medkit);
            yield return null;
            int land0 = DroppedItemView.Landings;
            Hotbar.TestDrop = true;
            yield return new WaitForSeconds(0.25f);
            Shot(dir, "h04_drop_throw");
            yield return new WaitForSeconds(2.5f);
            if (FindDrop(HotbarItem.Medkit, out var md))
            {
                Vector3 at = mm.DropPosition(md);
                Debug.Log($"[InvTest] medkit drop #{md.Id} settled={md.Settled} at {at} (thrown from {me.transform.position}), landing sounds {DroppedItemView.Landings - land0}");
                InvLog(me, "after Q");
                SafeLitShot(dir, "h05_drop_floor", at + new Vector3(0.9f, 0.7f, -0.9f), at);
                yield return LookAt(me, at, me.transform.position);
                Debug.Log($"[InvTest] prompt at the dropped medkit: {LocalPlayerController.Instance.Prompt} '{LocalPlayerController.Instance.PromptText}'");
                Shot(dir, "h06_drop_prompt");
                me.RequestPickupDropRpc(md.Id);
                yield return new WaitForSeconds(0.7f);
                InvLog(me, "medkit taken back (E)");
            }
            else Debug.Log("[InvTest] FAIL: no medkit drop");

            // RC car: from the shelf, Q, E again.
            Vector3 car = RcCarShelf.CarPos(0);
            yield return LookAt(me, car, car + Quaternion.Euler(0f, RcCarShelf.Yaw, 0f) * Vector3.back * 2f);
            me.RequestRcPickupRpc(0);
            yield return new WaitForSeconds(0.8f);
            InvLog(me, $"rc picked (slotOf {mm.RcSlotOf(me)})");
            Hotbar.TestSelect = Hotbar.SlotOf(me, HotbarItem.RcCar);
            yield return null;
            Hotbar.TestDrop = true;
            yield return new WaitForSeconds(2.8f);
            if (FindDrop(HotbarItem.RcCar, out var rd))
            {
                var s0 = mm.RcSlot(0);
                Vector3 at = mm.DropPosition(rd);
                Debug.Log($"[InvTest] rc drop #{rd.Id} settled={rd.Settled} slot0 mode={s0.Mode} holder={(s0.Holder == MatchManager.RcDropHolder ? "drop" : s0.Holder.ToString())} slotOf={mm.RcSlotOf(me)}");
                SafeLitShot(dir, "h07_rc_floor", at + new Vector3(0.8f, 0.6f, -0.8f), at);
                yield return LookAt(me, at, me.transform.position);
                me.RequestPickupDropRpc(rd.Id);
                yield return new WaitForSeconds(0.7f);
                InvLog(me, $"rc taken back (slotOf {mm.RcSlotOf(me)}, mode {mm.RcSlot(0).Mode})");
            }
            else Debug.Log("[InvTest] FAIL: no rc drop");

            // Phone: E into a slot, Q puts it back on the floor as a phone (MatchManager.Phones).
            int pi = -1;
            for (int i = 0; i < mm.Phones.Count; i++) if (mm.Phones[i].Mode == PhoneMode.Floor) { pi = i; break; }
            if (pi >= 0)
            {
                var ps = mm.Phones[pi];
                yield return LookAt(me, ps.Pos + Vector3.up * 0.05f, me.transform.position);
                Debug.Log($"[InvTest] phone prompt '{LocalPlayerController.Instance.PromptText}'");
                me.RequestPickupPhoneRpc(ps.Id);
                yield return new WaitForSeconds(0.7f);
                InvLog(me, $"phone picked (has {me.HasPhone})");
                Hotbar.TestSelect = Hotbar.SlotOf(me, HotbarItem.Phone);
                yield return new WaitForSeconds(0.4f);
                Shot(dir, "h10_phone_slot");
                Hotbar.TestDrop = true;
                yield return new WaitForSeconds(0.8f);
                InvLog(me, $"phone dropped (has {me.HasPhone}, mode {mm.Phones[pi].Mode})");
            }
            else Debug.Log("[InvTest] no phone on the floor");

            // Toy gun: Q keeps the shots left, E takes it back with them.
            me.ToyAmmo.Value = 5;
            yield return new WaitForSeconds(0.4f);
            Hotbar.TestSelect = Hotbar.SlotOf(me, HotbarItem.ToyGun);
            yield return new WaitForSeconds(0.4f);
            Debug.Log($"[InvTest] toy gun in slot {Hotbar.SlotOf(me, HotbarItem.ToyGun) + 1}: out={ToyGunControl.Out} netOut={me.ToyGunOut.Value}");
            Shot(dir, "h08_toygun_slot");
            Hotbar.TestDrop = true;
            yield return new WaitForSeconds(2.6f);
            if (FindDrop(HotbarItem.ToyGun, out var td))
            {
                Debug.Log($"[InvTest] toy gun dropped: shots kept {td.Extra}, settled={td.Settled}, out={ToyGunControl.Out}");
                SafeLitShot(dir, "h09_toygun_floor", mm.DropPosition(td) + new Vector3(0.7f, 0.6f, -0.7f), mm.DropPosition(td));
                yield return LookAt(me, mm.DropPosition(td), me.transform.position);
                me.RequestPickupDropRpc(td.Id);
                yield return new WaitForSeconds(0.7f);
                InvLog(me, $"toy gun taken back (shots {me.ToyAmmo.Value})");
            }
            else Debug.Log("[InvTest] FAIL: no toy gun drop");

            // The seeker's chainsaw on the floor: no prompt for a hider, the server refuses.
            float end = Time.time + 40f;
            DropState saw = default;
            while (Time.time < end && !FindDrop(HotbarItem.Chainsaw, out saw)) yield return null;
            if (saw.Id != 0)
            {
                yield return new WaitForSeconds(1.5f);
                yield return LookAt(me, mm.DropPosition(saw), me.transform.position);
                Debug.Log($"[InvTest] hider at the dropped saw: prompt {LocalPlayerController.Instance.Prompt} '{LocalPlayerController.Instance.PromptText}'");
                mm.ServerPickupDrop(me, saw.Id);
                yield return new WaitForSeconds(0.3f);
                Debug.Log($"[InvTest] hider E on the saw: still on the floor={FindDrop(HotbarItem.Chainsaw, out _)} hider saw={me.HasChainsaw.Value}");
            }
            else Debug.Log("[InvTest] FAIL: the seeker's saw never landed");

            // Seeker takes the saw back; fill his inventory, then the Glock comes.
            end = Time.time + 30f;
            while (Time.time < end && (seeker == null || !seeker.HasChainsaw.Value || FindDrop(HotbarItem.Chainsaw, out _))) yield return null;
            yield return new WaitForSeconds(5f);
            if (seeker.MedkitCount.Value == 0) seeker.MedkitCount.Value = 1;
            seeker.SyringeCount.Value = 1;
            seeker.RodCount.Value = 1;
            yield return new WaitForSeconds(1.5f);
            InvLog(seeker, "seeker filled (server)");
            mm.TestGiveGlock();
            yield return new WaitForSeconds(0.6f);
            Debug.Log($"[InvTest] glock given with a full inventory: has={seeker.HasGlock.Value} on the floor={FindDrop(HotbarItem.Glock, out _)}");
            end = Time.time + 25f;
            while (Time.time < end && !seeker.HasGlock.Value) yield return null;
            InvLog(seeker, "seeker after taking the glock");

            // Hint pings: same level as the seeker, then another level.
            yield return new WaitForSeconds(2f);
            me.TeleportLocal(seeker.transform.position + seeker.transform.right * 3f + Vector3.up * 0.1f, 0f);
            yield return new WaitForSeconds(1.2f);
            mm.TestHintPing(me);
            Debug.Log($"[InvTest] ping on the seeker's level (floor {FloorAcoustics.FloorOf(me.transform.position.y + 0.1f)})");
            yield return new WaitForSeconds(9f);
            Vector3 sp = seeker.transform.position;
            float otherY = FloorAcoustics.FloorOf(sp.y + 0.1f) == 0 ? -3.38f : 0.03f;
            me.TeleportLocal(new Vector3(sp.x + 2f, otherY, sp.z), 0f);
            yield return new WaitForSeconds(1.2f);
            mm.TestHintPing(me);
            Debug.Log($"[InvTest] ping on another level (floor {FloorAcoustics.FloorOf(me.transform.position.y + 0.1f)}, seeker floor {FloorAcoustics.FloorOf(sp.y + 0.1f)})");
            yield return new WaitForSeconds(4f);
            Debug.Log("[InvTest] hider done");
        }

        private IEnumerator InvSeeker()
        {
            var mm = MatchManager.Instance;
            var me = NetPlayer.Local;
            string dir = InvShots;
            yield return new WaitForSeconds(0.5f);
            InvLog(me, "start (seeker)");
            Shot(dir, "s01_knife");
            Hotbar.TestSelect = 0;
            yield return null;
            Hotbar.TestDrop = true;
            yield return new WaitForSeconds(0.6f);
            InvLog(me, "Q on the knife");
            Hotbar.TestSelect = 2;
            yield return new WaitForSeconds(0.9f);
            InvLog(me, "empty slot 3 selected");
            Shot(dir, "s02_empty_hands");

            yield return LookAt(me, ChainsawShelf.Position + Vector3.up * 1.2f, ChainsawShelf.Position + new Vector3(0f, 0f, -2f));
            Debug.Log($"[InvTest] shelf prompt '{LocalPlayerController.Instance.PromptText}'");
            me.RequestPickupChainsawRpc();
            yield return new WaitForSeconds(1.8f);
            InvLog(me, "saw from the shelf");
            Shot(dir, "s03_saw");
            Hotbar.TestSelect = 0;
            yield return new WaitForSeconds(1.2f);
            InvLog(me, "slot 1");
            Hotbar.TestSelect = Hotbar.SlotOf(me, HotbarItem.Chainsaw);
            yield return new WaitForSeconds(1.6f);
            InvLog(me, "saw slot");

            if (OpenSpot(out var open)) { me.TeleportLocal(open, 90f); me.Motor.SetLook(90f, -15f); yield return new WaitForSeconds(0.8f); }
            Hotbar.TestDrop = true;
            yield return new WaitForSeconds(0.3f);
            Shot(dir, "s04_saw_throw");
            yield return new WaitForSeconds(2.6f);
            InvLog(me, "saw dropped");
            if (FindDrop(HotbarItem.Chainsaw, out var sd))
            {
                Vector3 at = mm.DropPosition(sd);
                Debug.Log($"[InvTest] saw drop settled={sd.Settled} at {at}, seeker map marks={Minimap.SeekerDropMarks}");
                SafeLitShot(dir, "s05_saw_floor", at + new Vector3(1f, 0.8f, -1f), at);
                Shot(dir, "s06_saw_minimap");
                // the hider checks that it cannot take it (after its RC car, phone and toy gun steps)
                yield return new WaitForSeconds(26f);
                yield return LookAt(me, mm.DropPosition(sd), me.transform.position);
                Debug.Log($"[InvTest] seeker prompt at his saw: '{LocalPlayerController.Instance.PromptText}'");
                me.RequestPickupDropRpc(sd.Id);
                yield return new WaitForSeconds(1.2f);
                InvLog(me, "saw taken back");
            }
            else Debug.Log("[InvTest] FAIL: no saw drop");

            // Take a medkit away from the hiders (seeker pickup).
            for (int i = 0; i < mm.Medkits.Count; i++)
            {
                var m = mm.Medkits[i];
                if (m.Taken) continue;
                yield return LookAt(me, m.Position + Vector3.up * 0.15f, me.transform.position);
                Debug.Log($"[InvTest] seeker medkit prompt '{LocalPlayerController.Instance.PromptText}'");
                me.RequestPickupRpc(m.Id);
                yield return new WaitForSeconds(0.8f);
                InvLog(me, "seeker took a map medkit");
                break;
            }

            float end = Time.time + 20f;
            while (Time.time < end && Inventory.Kinds(me) < 5) yield return null;
            yield return new WaitForSeconds(0.6f);
            InvLog(me, "full");
            Shot(dir, "s07_full");
            end = Time.time + 20f;
            DropState gd = default;
            while (Time.time < end && !FindDrop(HotbarItem.Glock, out gd)) yield return null;
            yield return new WaitForSeconds(3f);
            FindDrop(HotbarItem.Glock, out gd);
            Vector3 gp = mm.DropPosition(gd);
            Debug.Log($"[InvTest] glock on the floor #{gd.Id} settled={gd.Settled} at {gp} ({Vector3.Distance(gp, me.transform.position):0.0} m from me), map marks={Minimap.SeekerDropMarks}");
            Shot(dir, "s08_glock_minimap");
            SafeLitShot(dir, "s09_glock_floor", gp + new Vector3(0.7f, 0.6f, -0.7f), gp);
            yield return LookAt(me, gp, me.transform.position);
            Debug.Log($"[InvTest] glock prompt (full) '{LocalPlayerController.Instance.PromptText}'");
            me.RequestPickupDropRpc(gd.Id);
            yield return new WaitForSeconds(0.6f);
            InvLog(me, "E on the glock while full");
            Hotbar.TestSelect = Hotbar.SlotOf(me, HotbarItem.Medkit);
            yield return new WaitForSeconds(0.3f);
            Hotbar.TestDrop = true;
            yield return new WaitForSeconds(1.5f);
            me.RequestPickupDropRpc(gd.Id);
            yield return new WaitForSeconds(1.5f);
            InvLog(me, "glock taken after dropping the medkit");
            Shot(dir, "s10_glock_in_hand");

            // Hint pings from the hider: shown on my level only.
            end = Time.time + 16f;
            int last = -1, n = 0;
            while (Time.time < end)
            {
                int v = Minimap.VisiblePins * 10 + Minimap.HiddenPins;
                if (v != last)
                {
                    Debug.Log($"[InvTest] pins visible {Minimap.VisiblePins}, hidden (other level) {Minimap.HiddenPins} (my floor {FloorAcoustics.FloorOf(me.transform.position.y + 0.1f)})");
                    Shot(dir, $"s11_pins_{n++}_{v}");
                    last = v;
                }
                yield return new WaitForSeconds(0.25f);
            }
            Debug.Log("[InvTest] seeker done");
        }
    }
}
