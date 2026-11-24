using System;
using System.Collections;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// "Pilot RC" tests (prepared, run only on request). -testremote on the host (-forcehider -autostart) with a seeker
    /// client (-remotewatch): car picked up (slot = car), put down (same slot = transmitter), park, transmitter selected
    /// (in the hands), LMB = steer again, Q = transmitter on the floor (car can't be taken by others), picked up again,
    /// then the seeker stomps on the car while it is driven (out of the car view, up, throw, wreck, slot freed) and the
    /// second car is shot while parked (throw right away). -remoteshots &lt;dir&gt; = PNGs (first person + a side camera),
    /// -remotewav &lt;dir&gt; = the synthesised crash as WAV. The watcher logs the slots / wrecks and photographs the throws.
    /// </summary>
    public partial class NetSession
    {
        private void StartRcRemoteTests(Func<string, bool> has)
        {
            if (has("-testremote")) StartCoroutine(RemoteTest());
            if (has("-remotewatch")) StartCoroutine(RemoteWatch());
            StartRcPowerTests(has); // energised car, boost, steering, hand-over (NetSession.RcPowerTests.cs)
            StartRcCargoTests(has); // the car picking items up + fuse minimap markers (NetSession.RcCargoTests.cs)
            if (ArgOr("-remotewav", "") is string wav && wav.Length > 0)
            {
                System.IO.Directory.CreateDirectory(wav);
                WriteWav(System.IO.Path.Combine(wav, "rc_remote_crash.wav"), RcRemoteSounds.Crash);
                Debug.Log($"[RemoteTest] wav written to {wav}");
            }
        }

        private static string RemoteShots => ArgOr("-remoteshots", "");

        private static string RemoteState(MatchManager mm, NetPlayer me, int i)
        {
            var s = mm.RcSlot(i);
            var w = RcRemoteFx.Instance != null ? RcRemoteFx.Instance.Wreck(i) : null;
            return $"slot{i}: {s.Mode} holder={s.Holder} remote={s.Remote} at={s.RemotePos} | inv={me.Inv.Value} remoteSlot={mm.RcRemoteSlotOf(me)} " +
                   $"carSlot={mm.RcCarriedSlotOf(me)} out={me.RcRemoteOut.Value} anim={me.RcAnimating} busy={me.Busy.Value} wreck={(w != null)}";
        }

        private IEnumerator RemoteTest()
        {
            yield return WaitSeeking();
            var mm = MatchManager.Instance;
            var me = NetPlayer.Local;
            NetPlayer seeker = null;
            foreach (var p in NetPlayer.All) if (p.IsSeeker) seeker = p;
            if (!me.IsHider || seeker == null || Manager == null || !Manager.IsServer) { Debug.Log("[RemoteTest] need host hider + seeker"); yield break; }
            string dir = RemoteShots;
            TestLight(RcCarShelf.Position + new Vector3(-1.5f, 2.6f, 0f), 9f, 1.6f);
            TestLight(new Vector3(-14f, -1f, 54.5f), 12f, 1.8f);
            Func<Vector3> body = () => me.transform.position + Vector3.up * 0.9f;

            // 1. Car from the shelf: slot = "Samochodzik RC".
            var shelfRot = Quaternion.Euler(0f, RcCarShelf.Yaw, 0f);
            me.Motor.Teleport(RcCarShelf.Position + shelfRot * new Vector3(-0.3f, 0f, -1.15f), RcCarShelf.Yaw);
            yield return new WaitForSeconds(0.8f);
            mm.ServerRcPickup(me, 0);
            yield return new WaitForSeconds(0.5f);
            int carSlot = Hotbar.SlotOf(me, HotbarItem.RcCar);
            Debug.Log($"[RemoteTest] picked up: car in slot {carSlot + 1} | {RemoteState(mm, me, 0)}");
            Hotbar.TestSelect = carSlot;

            // 2. Put down (LMB on the car = 6): the same slot becomes "Pilot RC".
            me.Motor.Teleport(new Vector3(-13.6f, -3.4f, 54.6f), 270f);
            yield return new WaitForSeconds(0.8f);
            Hotbar.TestClick = true;
            yield return new WaitForSeconds(2.4f);
            Debug.Log($"[RemoteTest] driving: pilot slot {Hotbar.SlotOf(me, HotbarItem.RcRemote) + 1} (car was {carSlot + 1}) | {RemoteState(mm, me, 0)}");
            LocalPlayerController.TestRcActive = true;
            LocalPlayerController.TestRcInput = new Vector2(0.3f, 1f);
            yield return new WaitForSeconds(1.0f);
            LocalPlayerController.TestRcInput = Vector2.zero;
            yield return new WaitForSeconds(0.5f);

            // 3. Park: the transmitter stays in the inventory, selected = in the hands.
            LocalPlayerController.TestSix = true;
            yield return new WaitForSeconds(1.6f);
            Hotbar.TestSelect = Hotbar.SlotOf(me, HotbarItem.RcRemote);
            yield return new WaitForSeconds(0.8f);
            Debug.Log($"[RemoteTest] parked, pilot in hands: hint='{Hotbar.Hint(me, HotbarItem.RcRemote)}' | {RemoteState(mm, me, 0)}");
            Shot(dir, "remote_fp_hold");
            StartCoroutine(SideBurst(dir, "remote_tp_hold", body, () => me.transform.rotation * new Vector3(0.9f, 0.4f, 1.6f), 2, 0.3f));
            yield return new WaitForSeconds(1.0f);

            // 4. LMB with the transmitter = steer again; park again.
            Hotbar.TestClick = true;
            yield return new WaitForSeconds(1.8f);
            Debug.Log($"[RemoteTest] LMB resume: ctrl={me.RcControlling} | {RemoteState(mm, me, 0)}");
            LocalPlayerController.TestSix = true;
            yield return new WaitForSeconds(1.8f);

            // 5. Q: transmitter on the floor (the car can't be taken without it), E picks it up again.
            Hotbar.TestSelect = Hotbar.SlotOf(me, HotbarItem.RcRemote);
            yield return new WaitForSeconds(0.3f);
            Hotbar.TestDrop = true;
            yield return new WaitForSeconds(1.5f);
            int dropId = -1;
            for (int i = 0; i < mm.Drops.Count; i++) if (mm.Drops[i].Kind == HotbarItem.RcRemote) dropId = mm.Drops[i].Id;
            Debug.Log($"[RemoteTest] Q: drop #{dropId} | {RemoteState(mm, me, 0)}");
            mm.ServerRcPickup(me, 0); // refused: its transmitter lies on the floor
            yield return new WaitForSeconds(0.3f);
            if (dropId >= 0) mm.ServerPickupDrop(me, dropId);
            yield return new WaitForSeconds(0.5f);
            Debug.Log($"[RemoteTest] picked the transmitter up again | {RemoteState(mm, me, 0)}");

            // 6. Steering when the seeker stomps on the car: out of the car view, up, smash the transmitter.
            Hotbar.TestSelect = Hotbar.SlotOf(me, HotbarItem.RcRemote);
            LocalPlayerController.TestSix = true;
            yield return new WaitForSeconds(1.8f);
            var car = RcCar.Peek(0);
            Vector3 cp = car.transform.position;
            seeker.TeleportRpc(cp + new Vector3(1.2f, 0f, 0.3f), Quaternion.LookRotation(-new Vector3(1.2f, 0f, 0.3f)).eulerAngles.y);
            yield return new WaitForSeconds(0.6f);
            mm.ServerRcStomp(seeker, 0);
            float t0 = Time.time;
            bool shotThrow = false;
            while (Time.time - t0 < 6f)
            {
                var s = mm.RcSlot(0);
                if (!shotThrow && s.Remote == RcRemoteFate.Thrown)
                {
                    shotThrow = true;
                    Debug.Log($"[RemoteTest] throw starts {Time.time - t0:0.00}s after the stomp | {RemoteState(mm, me, 0)}");
                    StartCoroutine(SideBurst(dir, "remote_tp_throw", body, () => me.transform.rotation * new Vector3(1.4f, 0.3f, 1.2f), 8, 0.12f));
                    StartCoroutine(FpBurst(dir, "remote_fp_throw", 6, 0.15f));
                }
                yield return new WaitForSeconds(0.25f);
            }
            Debug.Log($"[RemoteTest] after the stomp | {RemoteState(mm, me, 0)} move={me.Motor.MovementEnabled}");
            var w = RcRemoteFx.Instance != null ? RcRemoteFx.Instance.Wreck(0) : null;
            if (w != null) StartCoroutine(SideBurst(dir, "remote_wreck", () => w.transform.position, () => new Vector3(0.5f, 0.9f, -0.6f), 1, 0.1f));
            LocalPlayerController.TestRcActive = false;
            yield return new WaitForSeconds(1f);

            // 7. Second car, parked, shot with the Glock: the transmitter is smashed right away.
            me.Motor.Teleport(RcCarShelf.Position + shelfRot * new Vector3(0.3f, 0f, -1.1f), RcCarShelf.Yaw);
            yield return new WaitForSeconds(0.6f);
            mm.ServerRcPickup(me, 1);
            me.Motor.Teleport(new Vector3(-13.6f, -3.4f, 53.0f), 270f);
            yield return new WaitForSeconds(0.6f);
            LocalPlayerController.TestSix = true;
            yield return new WaitForSeconds(2.4f);
            LocalPlayerController.TestSix = true;
            yield return new WaitForSeconds(1.8f);
            Debug.Log($"[RemoteTest] car 2 parked | {RemoteState(mm, me, 1)}");
            Vector3 c2 = RcCar.Peek(1).transform.position;
            bool hit = mm.TestShootRc(seeker, 1, c2 + Vector3.up * 0.1f);
            for (int i = 0; i < 12; i++)
            {
                yield return new WaitForSeconds(0.25f);
                if (i == 3) Debug.Log($"[RemoteTest] shot={hit} | {RemoteState(mm, me, 1)}");
            }
            Debug.Log($"[RemoteTest] done | {RemoteState(mm, me, 0)} || {RemoteState(mm, me, 1)}");
        }

        /// <summary>Screenshots of the local view (first person).</summary>
        private IEnumerator FpBurst(string dir, string prefix, int count, float every)
        {
            for (int i = 0; i < count; i++)
            {
                yield return new WaitForSeconds(every);
                Shot(dir, $"{prefix}_{i:00}");
            }
        }

        /// <summary>-remotewatch (other peer): the slots / transmitters / wrecks as seen here, photos of each throw.</summary>
        private IEnumerator RemoteWatch()
        {
            yield return WaitSeeking();
            var mm = MatchManager.Instance;
            var seen = new bool[RcCar.Count];
            for (int k = 0; k < 140; k++)
            {
                yield return new WaitForSeconds(0.5f);
                for (int i = 0; i < RcCar.Count; i++)
                {
                    var s = mm.RcSlot(i);
                    var holder = NetPlayer.Find(s.Holder);
                    var w = RcRemoteFx.Instance != null ? RcRemoteFx.Instance.Wreck(i) : null;
                    if (k % 4 == 0 || (s.Remote == RcRemoteFate.Thrown && !seen[i]))
                        Debug.Log($"[RemoteWatch] slot{i}: {s.Mode} remote={s.Remote} holderOut={(holder != null ? holder.RcRemoteOut.Value.ToString() : "-")} wreck={(w != null)}");
                    if (s.Remote == RcRemoteFate.Thrown && !seen[i] && holder != null)
                    {
                        seen[i] = true;
                        var h = holder;
                        StartCoroutine(SideBurst(RemoteShots, $"watch_throw{i}", () => h.transform.position + Vector3.up * 0.9f,
                            () => h.transform.rotation * new Vector3(-1.5f, 0.3f, 1.3f), 8, 0.12f));
                    }
                }
            }
        }
    }
}
