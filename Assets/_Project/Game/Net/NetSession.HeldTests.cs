using System;
using System.Collections;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// -testheld on both copies (host -autostart -hidetime N -forcehider, client = seeker; + -heldshots &lt;folder&gt;):
    /// the selected inventory item in the hand.
    /// hider (host): takes a medkit, syringe, rod, toy gun and the RC car, then swaps the toy gun for a phone; selects
    /// every slot in turn: first-person shot in the middle of the switch and after it, own body in third person from
    /// the side and from the front, log of what the body / the arms show; a medkit heal started with LMB (the carried
    /// medkit must go away, the animation's own one comes); gives the seeker the Glock.
    /// seeker (client): his slots in turn (knife, chainsaw from the shelf, Glock, empty) with the same shots, then films
    /// the hider from the side every time the item in the hider's hand changes (synced through NetPlayer.HandSlot).
    /// </summary>
    public partial class NetSession
    {
        private void StartHeldTests(Func<string, bool> has)
        {
            if (has("-testheld")) StartCoroutine(HeldTest());
        }

        private static string HeldShots => ArgOr("-heldshots", "");

        private static void HeldLog(NetPlayer p, string what)
        {
            var bca = p.Actions as BlockyCharacterActions;
            var fpv = p.IsOwner ? p.ViewModel as FirstPersonViewModel : null;
            Debug.Log($"[HeldTest] {p.Nick} {what}: slots={p.Inv.Value} handSlot={p.HandSlot.Value} selected={p.SelectedItem} hand={p.HandItem} " +
                      $"held={p.Held.Value} toyOut={p.ToyGunOut.Value} body={(bca != null ? ((HotbarItem)bca.CarriedShown).ToString() : "-")} " +
                      $"w={(bca != null ? bca.CarryWeight : 0f):0.00} arms={(fpv != null ? ((HotbarItem)fpv.CarriedShown).ToString() : "-")} busy={p.Busy.Value}");
        }

        /// <summary>Own body in third person, lit, from the right side and from the front-left (then back to first person).</summary>
        private static IEnumerator HeldBodyShots(NetPlayer p, string dir, string name)
        {
            if (p.IsOwner) p.SetCinematicBody(true);
            yield return null;
            Vector3 at = p.transform.position + Vector3.up * 1.0f;
            SafeLitShot(dir, name + "_side", at + p.transform.right * 2.5f + p.transform.forward * 0.35f + Vector3.up * 0.1f, at - Vector3.up * 0.1f);
            SafeLitShot(dir, name + "_front", at + p.transform.forward * 2.6f - p.transform.right * 0.9f + Vector3.up * 0.25f, at - Vector3.up * 0.1f);
            if (p.IsOwner) p.SetCinematicBody(false);
        }

        /// <summary>Selects <paramref name="slot"/>, shoots the switch half way and the settled hand (first + third person).</summary>
        private IEnumerator HeldSlot(NetPlayer me, int slot, string dir, string tag)
        {
            Hotbar.TestSelect = slot;
            yield return new WaitForSeconds(0.12f);
            Shot(dir, $"{tag}_switch");
            HeldLog(me, $"switching to slot {slot + 1} ({Hotbar.ItemAt(me, slot)})");
            yield return new WaitForSeconds(0.9f);
            HeldLog(me, $"slot {slot + 1} ({Hotbar.ItemAt(me, slot)})");
            Shot(dir, $"{tag}_fpp");
            yield return new WaitForSeconds(0.2f);
            yield return HeldBodyShots(me, dir, $"{tag}_tpp");
            yield return new WaitForSeconds(0.3f);
        }

        private IEnumerator HeldTest()
        {
            yield return WaitSeeking();
            if (NetPlayer.Local.IsSeeker) yield return HeldSeeker();
            else yield return HeldHider();
        }

        private IEnumerator HeldHider()
        {
            var mm = MatchManager.Instance;
            var me = NetPlayer.Local;
            string dir = HeldShots;
            HeldLog(me, "start (hider)");
            if (mm.IsServer) HeldGiveItems(mm, me); // a client hider gets them from the host (HeldSeeker)
            else yield return new WaitForSeconds(1.5f);
            Vector3 car = RcCarShelf.CarPos(0);
            yield return LookAt(me, car, car + Quaternion.Euler(0f, RcCarShelf.Yaw, 0f) * Vector3.back * 2f);
            me.RequestRcPickupRpc(0);
            yield return new WaitForSeconds(0.8f);
            if (OpenSpot(out var open)) { me.TeleportLocal(open, 0f); me.Motor.SetLook(0f, 0f); }
            yield return new WaitForSeconds(0.8f);
            HeldLog(me, "items");

            Hotbar.TestSelect = Hotbar.SlotOf(me, HotbarItem.Medkit);
            yield return new WaitForSeconds(0.8f);
            for (int i = 0; i < Hotbar.Slots; i++)
            {
                var item = Hotbar.ItemAt(me, i);
                yield return HeldSlot(me, i, dir, $"h{i + 1}_{item}");
            }

            // Toy gun -> phone (sixth kind).
            Hotbar.TestSelect = Hotbar.SlotOf(me, HotbarItem.ToyGun);
            yield return new WaitForSeconds(0.5f);
            Hotbar.TestDrop = true;
            yield return new WaitForSeconds(1.5f);
            int pi = -1;
            for (int i = 0; i < mm.Phones.Count; i++) if (mm.Phones[i].Mode == PhoneMode.Floor) { pi = i; break; }
            if (pi < 0 && mm.IsServer)
            {
                mm.SpawnPhones(1);
                yield return new WaitForSeconds(0.3f);
                for (int i = 0; i < mm.Phones.Count; i++) if (mm.Phones[i].Mode == PhoneMode.Floor) { pi = i; break; }
            }
            if (pi >= 0)
            {
                var ps = mm.Phones[pi];
                yield return LookAt(me, ps.Pos + Vector3.up * 0.05f, me.transform.position);
                me.RequestPickupPhoneRpc(ps.Id);
                yield return new WaitForSeconds(0.8f);
                if (OpenSpot(out open)) { me.TeleportLocal(open, 0f); me.Motor.SetLook(0f, 0f); }
                yield return new WaitForSeconds(0.8f);
                int slot = Hotbar.SlotOf(me, HotbarItem.Phone);
                if (slot >= 0) yield return HeldSlot(me, slot, dir, "h6_Phone");
                else Debug.Log("[HeldTest] FAIL: phone not in the inventory");
            }
            else Debug.Log("[HeldTest] no phone on the floor");

            // Fast switching back and forth (no item may get stuck in the hand).
            for (int k = 0; k < 6; k++)
            {
                Hotbar.TestSelect = k % Hotbar.Slots;
                yield return new WaitForSeconds(0.07f);
            }
            yield return new WaitForSeconds(1f);
            HeldLog(me, "after fast switching");

            // Medkit heal with LMB: the carried medkit goes away, the animation shows its own.
            Hotbar.TestSelect = Hotbar.SlotOf(me, HotbarItem.Medkit);
            yield return new WaitForSeconds(0.8f);
            if (mm.IsServer) me.Limping.Value = true; // a client hider limps from the start (HeldGiveItems)
            yield return new WaitForSeconds(0.3f);
            float end = Time.time + 1.6f;
            bool shot = false;
            while (Time.time < end)
            {
                Hotbar.TestHold = true;
                if (!shot && Time.time > end - 0.4f)
                {
                    shot = true;
                    HeldLog(me, "healing");
                    Shot(dir, "h7_heal_fpp");
                    Vector3 at = me.transform.position + Vector3.up * 0.7f;
                    SafeLitShot(dir, "h7_heal_tpp", at + me.transform.right * 1.7f + Vector3.up * 0.3f, at);
                }
                yield return null;
            }
            Hotbar.TestHold = false;
            yield return new WaitForSeconds(1.5f);
            HeldLog(me, "heal released");
            Debug.Log("[HeldTest] hider done");
        }

        /// <summary>Server: the hider's items straight into the state (the RC car and the phone come through their pickups).</summary>
        private static void HeldGiveItems(MatchManager mm, NetPlayer hider)
        {
            mm.TestGiveGlock(); // the seeker's part shows it
            if (hider.MedkitCount.Value == 0) hider.MedkitCount.Value = 1;
            if (hider.SyringeCount.Value == 0) hider.SyringeCount.Value = 1;
            if (hider.RodCount.Value == 0) hider.RodCount.Value = 1;
            hider.ToyAmmo.Value = 6;
            if (!hider.IsLocalPlayer) hider.Limping.Value = true; // the heal at the end of the client's part needs a wound
        }

        private IEnumerator HeldSeeker()
        {
            var me = NetPlayer.Local;
            string dir = HeldShots;
            var mm0 = MatchManager.Instance;
            if (mm0.IsServer)
                foreach (var p in NetPlayer.All) if (p != null && p.IsHider) { HeldGiveItems(mm0, p); break; }
            StartCoroutine(HeldWatchHider(dir)); // films the hider's hand from the start (in parallel with the own slots)
            yield return new WaitForSeconds(0.5f);
            HeldLog(me, "start (seeker)");
            yield return LookAt(me, ChainsawShelf.Position + Vector3.up * 1.2f, ChainsawShelf.Position + new Vector3(0f, 0f, -2f));
            me.RequestPickupChainsawRpc();
            yield return new WaitForSeconds(2f);
            float end = Time.time + 10f;
            while (Time.time < end && !me.HasGlock.Value) yield return null;
            if (OpenSpot(out var open)) { me.TeleportLocal(open, 90f); me.Motor.SetLook(90f, 0f); }
            yield return new WaitForSeconds(1f);
            HeldLog(me, "items");
            for (int i = 0; i < Hotbar.Slots; i++)
            {
                var item = Hotbar.ItemAt(me, i);
                yield return HeldSlot(me, i, dir, $"s{i + 1}_{item}");
            }
            Hotbar.TestSelect = 0;
            yield return new WaitForSeconds(80f);
            Debug.Log("[HeldTest] seeker done");
        }

        /// <summary>Every change of the item in the hider's hand, filmed from his side (what the other players see).</summary>
        private IEnumerator HeldWatchHider(string dir)
        {
            yield return new WaitForSeconds(1f);
            NetPlayer hider = null;
            foreach (var p in NetPlayer.All) if (p != null && p.IsHider) hider = p;
            if (hider == null) { Debug.Log("[HeldTest] no hider to watch"); yield break; }
            var last = (HotbarItem)255;
            int n = 0;
            float end = Time.time + 100f;
            while (Time.time < end)
            {
                var now = hider.HandItem;
                if (now != last)
                {
                    last = now;
                    yield return new WaitForSeconds(0.6f);
                    HeldLog(hider, "remote view");
                    Vector3 at = hider.transform.position + Vector3.up * 1.0f;
                    SafeLitShot(dir, $"w{n++:00}_{hider.HandItem}", at + hider.transform.right * 2.5f + hider.transform.forward * 0.4f + Vector3.up * 0.15f, at - Vector3.up * 0.1f);
                }
                yield return null;
            }
        }
    }
}
