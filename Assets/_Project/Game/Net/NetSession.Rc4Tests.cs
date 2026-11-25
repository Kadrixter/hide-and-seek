using System.Collections;
using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// -testrc4 &lt;dir&gt; (host -forcehider -autostart -autostartplayers 2 -hidetime N + a seeker client): all four RC cars
    /// one after another - prompt at its place on the shelf (two on the middle board, two on the top one), picked up, put
    /// down, driven (cars 2 and 3 drive over a medkit: load on the roof), energised (R), parked, then the seeker stomps
    /// on it: wreck, the transmitter smashed (its wreck on the floor), the load falls off. Logs: [Rc4Test] ... PASS / FAIL.
    /// </summary>
    public partial class NetSession
    {
        private static void R4Log(string s) => Debug.Log("[Rc4Test] " + s);

        private IEnumerator Rc4Test(string dir)
        {
            yield return WaitSeeking();
            var mm = MatchManager.Instance;
            var me = NetPlayer.Local;
            NetPlayer seeker = null;
            foreach (var p in NetPlayer.All) if (p != null && p.IsSeeker) seeker = p;
            if (!me.IsHider || seeker == null || !Manager.IsServer) { R4Log("need host hider + seeker"); yield break; }
            TestLight(RcCarShelf.Position + new Vector3(-1.5f, 2.6f, 0f), 9f, 1.6f);
            TestLight(new Vector3(-14f, -1f, 53f), 14f, 1.8f);
            var shelfRot = Quaternion.Euler(0f, RcCarShelf.Yaw, 0f);
            Vector3 mid = (RcCarShelf.CarPos(0) + RcCarShelf.CarPos(3)) * 0.5f;
            ItemLitShot(dir, "rc4_a_shelf", mid + shelfRot * new Vector3(0.5f, 0.1f, -1.6f), mid);
            for (int i = 0; i < RcCar.Count; i++) R4Log($"start {RcState(mm, i)} shelfPos={RcCarShelf.CarPos(i)}");

            int ok = 0;
            for (int i = 0; i < RcCar.Count; i++)
            {
                // 1. In front of its place on the shelf, looking at it: prompt, E.
                Vector3 cp = RcCarShelf.CarPos(i);
                Vector3 stand = RcCarShelf.Position + shelfRot * new Vector3(i % 2 == 0 ? -0.3f : 0.3f, 0f, -1.15f);
                me.TeleportLocal(stand, RcCarShelf.Yaw);
                Vector3 aim = cp + Vector3.up * 0.1f - (stand + Vector3.up * 1.62f);
                me.Motor.SetLook(RcCarShelf.Yaw, -Mathf.Atan2(aim.y, new Vector2(aim.x, aim.z).magnitude) * Mathf.Rad2Deg);
                yield return new WaitForSeconds(1.2f);
                var lpc = LocalPlayerController.Instance;
                bool prompt = lpc.Prompt == PromptKind.RcPickup;
                R4Log($"car {i}: shelf prompt {lpc.Prompt} '{lpc.PromptText}'");
                if (i == 2) Shot(dir, "rc4_b_top_shelf_prompt");
                me.RequestRcPickupRpc((byte)i);
                yield return new WaitForSeconds(0.6f);
                bool carried = mm.RcSlot(i).Mode == RcMode.Carried && mm.RcSlotOf(me) == i;
                R4Log($"car {i}: picked up {RcState(mm, i)} -> {(carried ? "PASS" : "FAIL")}");
                if (!carried) continue;

                // 2. Down on open floor, drive 1.5 s (cars 2 / 3 over a medkit), R.
                Vector3 floor = new Vector3(-13.6f, -3.4f, 56.2f - 1.6f * i);
                me.TeleportLocal(floor, 270f);
                yield return new WaitForSeconds(0.8f);
                LocalPlayerController.TestSix = true;
                yield return new WaitForSeconds(2.4f);
                var car = RcCar.Peek(i);
                bool driving = mm.RcSlot(i).Mode == RcMode.Driving && car != null;
                if (!driving) { R4Log($"car {i}: not driving {RcState(mm, i)} - FAIL"); continue; }
                Vector3 start = car.transform.position;
                if (i >= 2)
                {
                    Vector3 f = car.transform.forward;
                    f.y = 0f;
                    mm.TestSpawnDrop(HotbarItem.Medkit, start + f.normalized * 1.3f);
                    yield return new WaitForSeconds(1.2f);
                }
                LocalPlayerController.TestRcActive = true;
                LocalPlayerController.TestRcInput = new Vector2(0f, 1f);
                yield return new WaitForSeconds(0.7f);
                LocalPlayerController.TestRcCharge = true;
                yield return new WaitForSeconds(0.8f);
                LocalPlayerController.TestRcInput = Vector2.zero;
                yield return new WaitForSeconds(0.8f);
                Shot(dir, $"rc4_c_car{i}_cockpit");
                float moved = Vector3.Distance(start, car.transform.position);
                var cargo = mm.RcCargoOf(i);
                float charge = mm.RcChargeElapsed(i);
                Vector3 cpos = car.transform.position;
                ItemLitShot(dir, $"rc4_d_car{i}_side", cpos + car.transform.right * 1.1f + Vector3.up * 0.6f - car.transform.forward * 0.4f, cpos + Vector3.up * 0.1f);
                LocalPlayerController.TestRcActive = false;
                bool driveOk = moved > 0.8f && charge >= 0f && (i < 2 || cargo == HotbarItem.Medkit);
                R4Log($"car {i}: moved {moved:0.00} m, cargo={cargo}, charged {charge:0.0} s ago -> {(driveOk ? "PASS" : "FAIL")}");

                // 3. Park (6): the transmitter stays in the hand.
                LocalPlayerController.TestSix = true;
                yield return new WaitForSeconds(2.0f);
                bool parked = mm.RcSlot(i).Mode == RcMode.Parked && mm.RcRemoteSlotOf(me) == i;
                R4Log($"car {i}: parked {RcState(mm, i)} remoteSlot={mm.RcRemoteSlotOf(me)} -> {(parked ? "PASS" : "FAIL")}");

                // 4. The seeker stomps on it: wreck, the transmitter smashed, the load off the roof.
                Vector3 pos = mm.TestRcWhere(i);
                Vector3 sp = pos + new Vector3(0.9f, 0.05f, 0f);
                seeker.TeleportRpc(sp, 270f);
                yield return new WaitForSeconds(1.2f);
                int drops0 = mm.Drops.Count;
                mm.ServerRcStomp(seeker, i);
                yield return new WaitForSeconds(6.5f);
                var s = mm.RcSlot(i);
                bool wreck = s.Mode == RcMode.Wreck;
                bool remoteWreck = RcRemoteFx.Instance != null && RcRemoteFx.Instance.Wreck(i) != null;
                bool loadOff = i < 2 || (mm.RcCargoOf(i) == HotbarItem.None && mm.Drops.Count > drops0);
                bool free = mm.RcRemoteSlotOf(me) < 0 && mm.RcSlotOf(me) < 0;
                bool all = prompt && driveOk && parked && wreck && remoteWreck && loadOff && free;
                if (all) ok++;
                R4Log($"car {i}: stomped {RcState(mm, i)} remote={s.Remote} remoteWreck={remoteWreck} loadOff={loadOff} handsFree={free} prompt={prompt} -> {(all ? "PASS" : "FAIL")}");
                if (remoteWreck) { var w = RcRemoteFx.Instance.Wreck(i).transform.position; ItemLitShot(dir, $"rc4_e_car{i}_wrecks", w + new Vector3(1.2f, 1.2f, -1.2f), (w + pos) * 0.5f); }
                seeker.TeleportRpc(sp + new Vector3(6f, 0f, 0f), 90f);
            }
            me.TeleportLocal(new Vector3(-10.5f, -3.4f, 53.5f), 270f);
            me.Motor.SetLook(270f, 20f);
            yield return new WaitForSeconds(1f);
            Shot(dir, "rc4_f_all_wrecks");
            R4Log($"summary: {ok}/{RcCar.Count} cars PASS");
        }
    }
}
