using System;
using System.Collections;
using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Energised RC car / boost / steering / transmitter hand-over test. 3 copies: host -forcehider -autostart
    /// -autostartplayers 3 -hidetime N -testrcpower &lt;dir&gt;, two clients -rcpowerwatch &lt;dir&gt; (one becomes the seeker,
    /// the other the second hider). Host: A) boost on a long straight (measured vs 1.08 x the seeker's sprint) and the
    /// turn rate at full lock (boost / crawl); B) R = energise, the seeker and the second hider are put in front of the
    /// car, it drives into them (both electrocuted once, no second zap of the same player in one charge), cooldown;
    /// C) park, Q = transmitter on the floor, the second hider picks it up. Clients: the seeker measures his own sprint,
    /// everybody logs the zaps / arcs they see, the second hider steers the handed-over car on from where it stood.
    /// </summary>
    public partial class NetSession
    {
        private void StartRcPowerTests(Func<string, bool> has)
        {
            if (has("-testrcpower")) StartCoroutine(RcPowerTest(ArgOr("-testrcpower", "")));
            if (has("-rcpowerwatch")) StartCoroutine(RcPowerWatch(ArgOr("-rcpowerwatch", "")));
        }

        private static string PowerState(MatchManager mm, int i)
        {
            var car = RcCar.Peek(i);
            return $"slot{i}: {mm.RcSlot(i).Mode} holder={mm.RcSlot(i).Holder} charged={mm.RcChargeLeft(i):0.0}s cd={mm.RcChargeCooldown(i):0.0}s " +
                   $"view={(car != null ? car.View.ToString() : "-")} pos={(car != null ? car.transform.position.ToString("F2") : "-")} speed={(car != null ? car.Speed : 0f):0.00} " +
                   $"fx={(car != null && car.GetComponent<RcChargeFx>() != null && car.GetComponentInChildren<LineRenderer>() != null)}";
        }

        private IEnumerator RcPowerTest(string dir)
        {
            yield return WaitSeeking();
            var mm = MatchManager.Instance;
            var me = NetPlayer.Local;
            NetPlayer seeker = null, h2 = null;
            foreach (var p in NetPlayer.All)
            {
                if (p == me) continue;
                if (p.IsSeeker) seeker = p;
                else if (p.IsHider) h2 = p;
            }
            if (!me.IsHider || seeker == null || Manager == null || !Manager.IsServer) { Debug.Log("[RcPowerTest] need host hider + seeker (+ second hider)"); yield break; }
            Debug.Log($"[RcPowerTest] roles: me={me.Nick} seeker={seeker.Nick} h2={(h2 != null ? h2.Nick : "-")}");
            if (!FindRun(0, out var spot, out float yaw, out float len)) { Debug.Log("[RcPowerTest] no straight found"); yield break; }
            Debug.Log($"[RcPowerTest] straight at {spot} yaw {yaw:0} free {len:0.0} m");
            TestLight(spot + Vector3.up * 2.6f, 9f, 0.9f);
            var car = RcCar.Get(0);
            Vector3 Fwd() => Quaternion.Euler(0f, car.Yaw, 0f) * Vector3.forward;

            // ---- A) boost speed + steering
            var shelfRot = Quaternion.Euler(0f, RcCarShelf.Yaw, 0f);
            me.Motor.Teleport(RcCarShelf.Position + shelfRot * new Vector3(-0.3f, 0f, -1.15f), RcCarShelf.Yaw);
            yield return new WaitForSeconds(0.8f);
            mm.ServerRcPickup(me, 0);
            yield return PlaceCar(me, spot, yaw);
            Debug.Log($"[RcPowerTest] placed | {PowerState(mm, 0)} ctrl={me.RcControlling}");
            LocalPlayerController.TestRcActive = true;
            LocalPlayerController.TestRcBoost = true;
            LocalPlayerController.TestRcInput = new Vector2(0f, 1f);
            float runFor = Mathf.Clamp((len - 4f) / 6.3f + 0.6f, 1.6f, 3f);
            float t0 = Time.time, maxSpeed = 0f;
            Vector3 p0 = car.transform.position;
            float tMark = -1f;
            Vector3 pMark = p0;
            while (Time.time - t0 < runFor)
            {
                maxSpeed = Mathf.Max(maxSpeed, car.Speed);
                if (tMark < 0f && Time.time - t0 >= runFor - 0.8f) { tMark = Time.time; pMark = car.transform.position; }
                yield return null;
            }
            Vector3 dp = car.transform.position - pMark;
            dp.y = 0f;
            float measured = dp.magnitude / Mathf.Max(0.01f, Time.time - tMark);
            float sprint = RcPower.SeekerSprint(me), target = RcPower.BoostSpeed(me);
            Debug.Log($"[RcPowerTest] BOOST measured {measured:0.00} m/s (car.Speed max {maxSpeed:0.00}) target {target:0.00} = {RcPower.BoostOverSeeker} x seeker sprint {sprint:0.00} " +
                      $"(+{(measured / sprint - 1f) * 100f:0.0}%), walk top {me.Motor.WalkSpeed:0.00}, run {Vector3.Distance(p0, car.transform.position):0.0} m");
            // Full lock at boost speed.
            LocalPlayerController.TestRcInput = new Vector2(1f, 1f);
            yield return new WaitForSeconds(0.35f);
            float y0 = car.Yaw, ts = Time.time, sp0 = car.Speed;
            yield return new WaitForSeconds(0.4f);
            float rateFast = Mathf.Abs(Mathf.DeltaAngle(y0, car.Yaw)) / (Time.time - ts);
            Debug.Log($"[RcPowerTest] STEER boost: {rateFast:0} deg/s at {sp0:0.00}-{car.Speed:0.00} m/s (old formula {OldYawRate(car.Speed, 5.8f):0} deg/s), wheels {car.SteerAngle:0.0} deg");
            LocalPlayerController.TestRcBoost = false;
            LocalPlayerController.TestRcInput = Vector2.zero;
            yield return new WaitForSeconds(1.4f);
            LocalPlayerController.TestRcInput = new Vector2(1f, 0.3f);
            yield return new WaitForSeconds(0.8f);
            y0 = car.Yaw;
            ts = Time.time;
            yield return new WaitForSeconds(0.8f);
            float rateSlow = Mathf.Abs(Mathf.DeltaAngle(y0, car.Yaw)) / (Time.time - ts);
            Debug.Log($"[RcPowerTest] STEER crawl: {rateSlow:0} deg/s at {car.Speed:0.00} m/s (old formula {OldYawRate(car.Speed, 5.8f):0} deg/s), wheels {car.SteerAngle:0.0} deg, radius {car.Speed / Mathf.Max(0.01f, rateSlow * Mathf.Deg2Rad):0.00} m");
            LocalPlayerController.TestRcInput = Vector2.zero;
            yield return new WaitForSeconds(0.8f);
            LocalPlayerController.TestSix = true;
            yield return new WaitForSeconds(1.8f);

            // ---- B) energise, drive into the seeker and the second hider
            me.Motor.Teleport(car.transform.position - Fwd() * 0.8f, car.Yaw);
            yield return new WaitForSeconds(0.6f);
            mm.ServerRcPickup(me, 0);
            yield return PlaceCar(me, spot, yaw);
            LocalPlayerController.TestRcCharge = true;
            yield return new WaitForSeconds(0.4f);
            Debug.Log($"[RcPowerTest] R: {PowerState(mm, 0)}");
            Shot(dir, "power_fp_arcs");
            StartCoroutine(SideBurst(dir, "power_arcs", () => car.transform.position + Vector3.up * 0.1f, () => Quaternion.Euler(0f, car.Yaw, 0f) * new Vector3(0.55f, 0.32f, 0.6f), 5, 0.12f));
            LocalPlayerController.TestRcCharge = true; // second press while charged: ignored
            Vector3 c = car.transform.position, f = Fwd(), side = Quaternion.Euler(0f, 90f, 0f) * f;
            float face = Quaternion.LookRotation(-f).eulerAngles.y;
            seeker.TeleportRpc(c + f * 1.5f, face);
            if (h2 != null) h2.TeleportRpc(c + f * 3f - side * 2.5f, face);
            yield return new WaitForSeconds(1.0f);
            double sAt = 0d, hAt = 0d;

            // 1. Into the seeker (frames for a short clip from the side).
            if (!string.IsNullOrEmpty(dir))
            {
                Vector3 mid = c + f * 1.0f + Vector3.up * 0.75f;
                StartCoroutine(SideBurst(System.IO.Path.Combine(dir, "vid"), "f", () => mid, () => side * 3.6f + f * 0.3f + Vector3.up * 0.35f, 70, 0.04f));
            }
            LocalPlayerController.TestRcInput = new Vector2(0f, 0.45f);
            t0 = Time.time;
            while (Time.time - t0 < 3f && seeker.ZapAt.Value <= 0d) yield return null;
            LocalPlayerController.TestRcInput = Vector2.zero;
            sAt = seeker.ZapAt.Value;
            Debug.Log($"[RcPowerTest] seeker zapped={(sAt > 0d)} after {Time.time - t0:0.00}s busy={seeker.Busy.Value} zapT={seeker.ZapTime:0.00} | {PowerState(mm, 0)}");
            var sk = seeker;
            StartCoroutine(SideBurst(dir, "power_zap_seeker", () => sk.transform.position + Vector3.up * 0.9f, () => sk.transform.rotation * new Vector3(1.3f, 0.25f, 1.6f), 6, 0.15f));
            yield return new WaitForSeconds(1.0f);

            // 2. The seeker (still shaking) steps aside, the second hider in front of the car.
            seeker.TeleportRpc(car.transform.position + Fwd() * 0.6f - side * 2.5f, face);
            if (h2 != null)
            {
                h2.TeleportRpc(car.transform.position + Fwd() * 0.95f, face);
                yield return new WaitForSeconds(0.6f);
                LocalPlayerController.TestRcInput = new Vector2(0f, 0.45f);
                t0 = Time.time;
                while (Time.time - t0 < 2.5f && h2.ZapAt.Value <= 0d) yield return null;
                LocalPlayerController.TestRcInput = Vector2.zero;
                hAt = h2.ZapAt.Value;
                Debug.Log($"[RcPowerTest] hider zapped={(hAt > 0d)} after {Time.time - t0:0.00}s busy={h2.Busy.Value} | {PowerState(mm, 0)}");
                var hh = h2;
                StartCoroutine(SideBurst(dir, "power_zap_hider", () => hh.transform.position + Vector3.up * 0.9f, () => hh.transform.rotation * new Vector3(-1.3f, 0.25f, 1.6f), 6, 0.15f));
                yield return new WaitForSeconds(1.0f);
                h2.TeleportRpc(car.transform.position + Fwd() * 2f - side * 2.5f, face);
            }
            Debug.Log($"[RcPowerTest] ZAPS seeker={(sAt > 0d)} hider={(hAt > 0d)} | {PowerState(mm, 0)}");

            // 3. The seeker again, after his zap ended, while the car is still charged: no second zap.
            while (seeker.Zapped) yield return null;
            seeker.TeleportRpc(car.transform.position + Fwd() * 0.8f, face);
            yield return new WaitForSeconds(0.5f);
            LocalPlayerController.TestRcInput = new Vector2(0f, 0.4f);
            yield return new WaitForSeconds(0.9f);
            LocalPlayerController.TestRcInput = Vector2.zero;
            Debug.Log($"[RcPowerTest] RE-ZAP check (charged {mm.RcChargeLeft(0):0.0}s): seeker ZapAt same={(seeker.ZapAt.Value == sAt)} zapped={seeker.Zapped} busy={seeker.Busy.Value} | {PowerState(mm, 0)}");
            seeker.TeleportRpc(car.transform.position - side * 3f, face); // out of the way for the hand-over drive
            while (mm.RcCharged(0)) yield return null;
            yield return new WaitForSeconds(0.3f);
            Debug.Log($"[RcPowerTest] charge over: {PowerState(mm, 0)} seekerBusy={seeker.Busy.Value}");
            LocalPlayerController.TestRcCharge = true;
            yield return new WaitForSeconds(0.5f);
            Debug.Log($"[RcPowerTest] COOLDOWN: R again -> charged={mm.RcCharged(0)} cd={mm.RcChargeCooldown(0):0.0}s");

            // ---- C) hand-over: park, transmitter on the floor, the second hider takes it
            LocalPlayerController.TestRcActive = false;
            LocalPlayerController.TestSix = true;
            yield return new WaitForSeconds(1.8f);
            Vector3 parked = car.transform.position;
            Debug.Log($"[RcPowerTest] parked for hand-over at {parked:F2} | {PowerState(mm, 0)}");
            if (h2 == null) { Debug.Log("[RcPowerTest] no second hider: hand-over skipped"); yield break; }
            me.Motor.Teleport(parked - Fwd() * 6f + Vector3.up * 0.05f, car.Yaw); // walks off a few metres first
            yield return new WaitForSeconds(0.6f);
            Hotbar.TestSelect = Hotbar.SlotOf(me, HotbarItem.RcRemote);
            yield return new WaitForSeconds(0.4f);
            Hotbar.TestDrop = true;
            yield return new WaitForSeconds(1.6f);
            int dropId = -1;
            Vector3 dropPos = me.transform.position;
            for (int i = 0; i < mm.Drops.Count; i++) if (mm.Drops[i].Kind == HotbarItem.RcRemote) { dropId = mm.Drops[i].Id; dropPos = mm.Drops[i].Pos; }
            Debug.Log($"[RcPowerTest] Q: drop #{dropId} at {dropPos:F2} | {PowerState(mm, 0)}");
            h2.TeleportRpc(dropPos + me.transform.right * 0.7f, me.transform.eulerAngles.y);
            yield return new WaitForSeconds(1.2f);
            if (dropId >= 0) mm.ServerPickupDrop(h2, dropId);
            yield return new WaitForSeconds(0.5f);
            Debug.Log($"[RcPowerTest] h2 picked up: holder is h2={mm.RcSlot(0).Holder == h2.NetworkObjectId} remoteSlot={mm.RcRemoteSlotOf(h2)} | {PowerState(mm, 0)}");
            t0 = Time.time;
            while (Time.time - t0 < 10f && !(mm.RcSlot(0).Mode == RcMode.Driving && mm.RcSlot(0).Holder == h2.NetworkObjectId)) yield return null;
            Vector3 startSeen = car.transform.position;
            Debug.Log($"[RcPowerTest] HANDOVER: h2 drives={mm.RcSlot(0).Mode == RcMode.Driving} from {mm.RcSlot(0).Pos:F2}, car here {startSeen:F2}, gap to parked {Vector3.Distance(mm.RcSlot(0).Pos, parked):0.00} m, shelf {Vector3.Distance(startSeen, RcCarShelf.CarPos(0)):0.0} m away");
            StartCoroutine(SideBurst(dir, "power_handover", () => car.transform.position + Vector3.up * 0.1f, () => new Vector3(0.9f, 1.1f, -1.4f), 6, 0.5f));
            yield return new WaitForSeconds(5f);
            Debug.Log($"[RcPowerTest] HANDOVER after 5 s: moved {Vector3.Distance(parked, car.transform.position):0.00} m from the parked spot | {PowerState(mm, 0)}");
            yield return new WaitForSeconds(4f);
            Debug.Log($"[RcPowerTest] done | {PowerState(mm, 0)}");
        }

        private static float OldYawRate(float v, float run) =>
            Mathf.Abs(v) / RcCar.Wheelbase * Mathf.Tan(32f * Mathf.Lerp(1f, 0.5f, Mathf.Abs(v) / run) * Mathf.Deg2Rad) * Mathf.Rad2Deg;

        /// <summary>Stand at <paramref name="spot"/> facing <paramref name="yaw"/>, key 6: the car goes down, the car view comes on.</summary>
        private static IEnumerator PlaceCar(NetPlayer me, Vector3 spot, float yaw)
        {
            me.Motor.Teleport(spot - Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * 0.5f, yaw);
            yield return new WaitForSeconds(0.8f);
            LocalPlayerController.TestSix = true;
            yield return new WaitForSeconds(2.6f);
        }
    }
}
