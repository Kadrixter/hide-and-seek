using System.Collections;
using System.Collections.Generic;
using HideAndSeek.Core;
using HideAndSeek.Player;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// -rcpowerwatch &lt;dir&gt; (clients of -testrcpower): the seeker measures his own sprint on a free straight; every copy
    /// logs the energised car and each zap it sees (photos: the arcs from the side, the own view while electrocuted);
    /// the second hider, once the host's transmitter is his, steers the car on from where it was parked.
    /// </summary>
    public partial class NetSession
    {
        private IEnumerator RcPowerWatch(string dir)
        {
            yield return WaitSeeking();
            var mm = MatchManager.Instance;
            var me = NetPlayer.Local;
            Debug.Log($"[RcPowerWatch] I am {me.Nick} ({(me.IsSeeker ? "seeker" : "hider")})");
            if (me.IsSeeker) yield return SeekerSprint(me);

            var zapped = new Dictionary<ulong, double>();
            bool arcsSeen = false, handover = false;
            float end = Time.time + 150f;
            while (Time.time < end && mm != null && mm.Phase.Value == MatchPhase.Seeking)
            {
                var car = RcCar.Peek(0);
                if (!arcsSeen && car != null && mm.RcCharged(0) && car.GetComponent<RcChargeFx>() != null)
                {
                    arcsSeen = true;
                    Debug.Log($"[RcPowerWatch] energised car seen here: {PowerState(mm, 0)}");
                    var cc = car;
                    StartCoroutine(SideBurst(dir, $"watch_arcs_{me.Nick}", () => cc.transform.position + Vector3.up * 0.1f,
                        () => Quaternion.Euler(0f, cc.Yaw, 0f) * new Vector3(-0.6f, 0.35f, 0.5f), 3, 0.15f));
                }
                foreach (var p in NetPlayer.All)
                {
                    if (p == null || p.ZapAt.Value <= 0d) continue;
                    if (zapped.TryGetValue(p.NetworkObjectId, out double at) && at == p.ZapAt.Value) continue;
                    zapped[p.NetworkObjectId] = p.ZapAt.Value;
                    Debug.Log($"[RcPowerWatch] ZAP seen here on {p.Nick} ({(p.IsSeeker ? "seeker" : "hider")}) zapT={p.ZapTime:0.00} busy={p.Busy.Value}");
                    if (p == me) StartCoroutine(FpBurst(dir, $"watch_zapped_fp_{me.Nick}", 3, 0.25f));
                }
                if (me.IsHider && !handover && mm.RcRemoteSlotOf(me) == 0 && mm.RcSlot(0).Mode == RcMode.Parked)
                {
                    handover = true;
                    yield return HandoverDrive(mm, me, dir);
                }
                yield return new WaitForSeconds(0.1f);
            }
        }

        /// <summary>Second hider: the picked-up transmitter selected, 6 = steer, a few metres on, park.</summary>
        private IEnumerator HandoverDrive(MatchManager mm, NetPlayer me, string dir)
        {
            var car = RcCar.Peek(0);
            Vector3 before = car.transform.position;
            Debug.Log($"[RcPowerWatch] transmitter is mine: car parked at {before:F2} (slot pos {mm.RcSlot(0).Pos:F2}), I stand {Vector3.Distance(me.transform.position, before):0.0} m away");
            yield return new WaitForSeconds(0.6f);
            Hotbar.TestSelect = Hotbar.SlotOf(me, HotbarItem.RcRemote);
            yield return new WaitForSeconds(0.4f);
            LocalPlayerController.TestSix = true;
            yield return new WaitForSeconds(2.4f);
            Vector3 start = car.transform.position;
            Debug.Log($"[RcPowerWatch] HANDOVER steering: ctrl={me.RcControlling} view={car.View} start {start:F2}, {Vector3.Distance(start, before):0.00} m from the parked spot");
            LocalPlayerController.TestRcActive = true;
            LocalPlayerController.TestRcInput = new Vector2(0.3f, 1f);
            yield return new WaitForSeconds(1.6f);
            Shot(dir, $"watch_handover_fp_{me.Nick}");
            LocalPlayerController.TestRcInput = Vector2.zero;
            yield return new WaitForSeconds(0.8f);
            Debug.Log($"[RcPowerWatch] HANDOVER drove {Vector3.Distance(start, car.transform.position):0.00} m: {PowerState(mm, 0)}");
            LocalPlayerController.TestRcActive = false;
            LocalPlayerController.TestSix = true;
            yield return new WaitForSeconds(1.8f);
            Debug.Log($"[RcPowerWatch] HANDOVER parked again: {PowerState(mm, 0)} remoteSlot={mm.RcRemoteSlotOf(me)}");
        }

        /// <summary>Seeker: sprint (Shift + W) along a free straight, speed over the last second.</summary>
        private IEnumerator SeekerSprint(NetPlayer me)
        {
            if (!FindRun(1, out var spot, out float yaw, out float len)) { Debug.Log("[RcPowerWatch] no straight for the sprint"); yield break; }
            me.TeleportLocal(spot, yaw);
            yield return new WaitForSeconds(0.8f);
            me.Motor.CommandProvider = () => new MotorCommand { Move = new Vector2(0f, 1f), Run = true };
            float runFor = Mathf.Clamp((len - 3f) / 5.8f, 1.5f, 3f), t0 = Time.time, tMark = -1f;
            Vector3 pMark = me.transform.position;
            while (Time.time - t0 < runFor)
            {
                if (tMark < 0f && Time.time - t0 >= runFor - 1f) { tMark = Time.time; pMark = me.transform.position; }
                yield return null;
            }
            Vector3 d = me.transform.position - pMark;
            d.y = 0f;
            float v = d.magnitude / Mathf.Max(0.01f, Time.time - tMark);
            me.Motor.CommandProvider = null;
            Debug.Log($"[RcPowerWatch] SEEKER SPRINT measured {v:0.00} m/s (motor RunSpeed {me.Motor.RunSpeed:0.00}, scale {me.Motor.SpeedScale:0.00}, free {len:0.0} m) " +
                      $"-> RC boost target {RcPower.BoostSpeed(me):0.00} m/s");
        }

        /// <summary>
        /// The <paramref name="rank"/>-th longest free straight on the parking levels (deterministic: every copy finds
        /// the same ones): floor under the spot, nothing in the way at car height and at chest height.
        /// </summary>
        private static bool FindRun(int rank, out Vector3 spot, out float yaw, out float len)
        {
            var found = new List<(Vector3 p, float yaw, float len)>();
            int mask = ~((1 << PlayerAvatarBinder.LocalBodyLayer) | (1 << 9) | (1 << RcCar.CarLayer) | GameLayers.RagdollMask);
            foreach (float level in new[] { 0f, -3.4f, -6.8f })
                for (float x = -75f; x <= 75f; x += 7.5f)
                    for (float z = -52f; z <= 52f; z += 6.5f)
                    {
                        var from = new Vector3(x, level + 1.2f, z);
                        if (!Physics.Raycast(from, Vector3.down, out var floor, 1.6f, mask, QueryTriggerInteraction.Ignore) || floor.normal.y < 0.95f) continue;
                        Vector3 p = floor.point;
                        if (Physics.CheckCapsule(p + Vector3.up * 0.4f, p + Vector3.up * 1.6f, 0.4f, mask, QueryTriggerInteraction.Ignore)) continue;
                        float best = 0f, bestYaw = 0f;
                        for (int i = 0; i < 16; i++)
                        {
                            float y = i * 22.5f;
                            Vector3 dir = Quaternion.Euler(0f, y, 0f) * Vector3.forward;
                            float low = Physics.SphereCast(p + Vector3.up * 0.3f, 0.22f, dir, out var h1, 30f, mask, QueryTriggerInteraction.Ignore) ? h1.distance : 30f;
                            float high = Physics.SphereCast(p + Vector3.up * 1.0f, 0.35f, dir, out var h2, 30f, mask, QueryTriggerInteraction.Ignore) ? h2.distance : 30f;
                            float free = Mathf.Min(low, high);
                            // The floor has to go on (no ramp down / hole half way).
                            Vector3 mid = p + dir * Mathf.Min(free, 14f) * 0.8f + Vector3.up * 0.5f;
                            if (!Physics.Raycast(mid, Vector3.down, out var mf, 1.2f, mask, QueryTriggerInteraction.Ignore) || Mathf.Abs(mf.point.y - p.y) > 0.15f) free = Mathf.Min(free, 6f);
                            if (free > best) { best = free; bestYaw = y; }
                        }
                        found.Add((p, bestYaw, best));
                    }
            found.Sort((a, b) => b.len.CompareTo(a.len));
            // Distinct spots (the seeker's straight is not the car's).
            var picked = new List<(Vector3 p, float yaw, float len)>();
            foreach (var f in found)
            {
                bool near = false;
                foreach (var q in picked) if ((q.p - f.p).sqrMagnitude < 25f * 25f) near = true;
                if (!near) picked.Add(f);
                if (picked.Count > rank) break;
            }
            spot = default;
            yaw = 0f;
            len = 0f;
            if (picked.Count <= rank) return false;
            (spot, yaw, len) = picked[rank];
            Debug.Log($"[RcPowerTest] run #{rank}: {spot:F1} yaw {yaw:0} free {len:0.0} m (of {found.Count} spots)");
            return true;
        }
    }
}
