using System;
using System.Collections;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using HideAndSeek.Player;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Body carry tests in the built game (3 instances: host -forcehider -autostart -autostartplayers 3 -hidetime N,
    /// two clients with -autoconsent; one of them becomes the seeker):
    ///  -testcarry (host, -carryshots dir): downs the other hider, asks him with B (auto yes), lifts him from the floor,
    ///      carries, walks, puts him down; lifts again and gets "hit" (drop); then a wounded standing hider: lift, carry
    ///      (arm round the neck) and the carried asks to be put down (Space, from -carrywatch). Third-person shots from
    ///      a lit side camera and first-person screenshots; log [CarryTest].
    ///  -carrywatch (any client, -carryshots dir): first-person screenshots while being carried; presses Space after
    ///      4 s of a conscious carry.
    /// </summary>
    public partial class NetSession
    {
        private void StartCarryTests(Func<string, bool> has)
        {
            if (has("-testcarry")) StartCoroutine(CarryTest());
            if (has("-carrywatch")) StartCoroutine(CarryWatch());
        }

        private static void CarryLog(string what, NetPlayer me, NetPlayer t)
        {
            var v = me.CarryVar.Value;
            Debug.Log($"[CarryTest] {what}: phase={v.PhaseValue} t={NetPlayer.CarryPhaseTime(v):0.00} carrying={me.IsCarryingBody} " +
                      $"target life={t.Life.Value} carriedBy={(t.CarriedBy != null ? t.CarriedBy.Nick : "-")} " +
                      $"me={me.transform.position:F2} target={t.transform.position:F2} d={Vector3.Distance(me.transform.position, t.transform.position):0.00} " +
                      $"speedScale={me.Motor.SpeedScale:0.00} burdened={me.Motor.Burdened} line='{UI.CarryConsentPrompt.LineText}' | " +
                      $"    carrier: {(me.Actions as BlockyCharacterActions)?.BodyCarryDebug} | carried: {(t.Actions as BlockyCharacterActions)?.BodyCarryDebug}");
        }

        private IEnumerator WaitCarry(NetPlayer me, bool on, float timeout)
        {
            float t0 = Time.time;
            while (me.IsCarryingBody != on && Time.time - t0 < timeout) yield return null;
        }

        private IEnumerator CarryTest()
        {
            yield return WaitSeeking();
            var mm = MatchManager.Instance;
            var me = NetPlayer.Local;
            NetPlayer t = null;
            foreach (var p in NetPlayer.All) if (p != null && p != me && p.IsHider) t = p;
            if (t == null) { Debug.Log("[CarryTest] need a second hider"); yield break; }
            string dir = ArgOr("-carryshots", "");
            if (!string.IsNullOrEmpty(dir)) System.IO.Directory.CreateDirectory(dir);

            // Everybody next to the target, lit.
            Vector3 spot = t.transform.position;
            TestLight(spot + new Vector3(0.8f, 2.4f, 0.6f), 9f, 2.2f);
            TestLight(spot + new Vector3(-1.5f, 1.2f, -1.5f), 6f, 0.9f);
            Func<Vector3> focus = () => me.transform.position + Vector3.up * 0.95f;
            Func<Vector3> side = () => me.transform.right * 2.6f + me.transform.forward * 1.2f + Vector3.up * 0.5f;
            Func<Vector3> front = () => me.transform.forward * 2.8f + Vector3.up * 0.45f;

            // ---- 1: downed, from the floor
            mm.TestDown(t);
            yield return new WaitForSeconds(2.2f);
            me.TeleportLocal(spot + t.transform.right * 1.1f, t.transform.eulerAngles.y - 90f);
            yield return new WaitForSeconds(0.8f);
            LocalPlayerController.TestCarryB = true;
            float t0 = Time.time;
            yield return WaitCarry(me, true, 6f);
            Debug.Log($"[CarryTest] lift started after {Time.time - t0:0.0} s (consent)");
            StartCoroutine(SideBurst(dir, "1_lift", focus, side, 13, 0.11f));
            CarryLog("lift", me, t);
            yield return new WaitForSeconds(BodyCarryTimeline.LiftFloor + 0.6f);
            CarryLog("hold", me, t);
            yield return SideBurst(dir, "1_hold_side", focus, side, 1, 0.05f);
            yield return SideBurst(dir, "1_hold_front", focus, front, 1, 0.05f);
            yield return SideBurst(dir, "1_hold_back", focus, () => -me.transform.forward * 2.6f + me.transform.right * 0.8f + Vector3.up * 0.6f, 1, 0.05f);
            me.Motor.SetLook(me.Motor.LookYaw, -22f);
            yield return new WaitForSeconds(0.3f);
            if (!string.IsNullOrEmpty(dir)) ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "1_fpp_down.png"));
            me.Motor.SetLook(me.Motor.LookYaw, 0f);
            yield return new WaitForSeconds(0.3f);
            if (!string.IsNullOrEmpty(dir)) ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "1_fpp_ahead.png"));

            // Bleed-out clock stands still while carried.
            float left0 = t.BleedPausedLeft.Value;
            yield return new WaitForSeconds(2f);
            Debug.Log($"[CarryTest] bleed paused: {t.BleedPaused} left {left0:0.0} -> {t.BleedPausedLeft.Value:0.0} (2 s later), shown {t.BleedLeftNow:0.0}");

            // Turning: a quick mouse flick of 120 deg, the body follows slowly (view ahead of it).
            float y0 = me.transform.eulerAngles.y;
            Vector3 focusFix = me.transform.position + Vector3.up * 0.95f;
            Vector3 camFix = me.transform.right * 2.8f + me.transform.forward * 1.5f + Vector3.up * 0.7f;
            me.Motor.SetLook(y0 + 120f, me.Motor.LookPitch);
            StartCoroutine(SideBurst(dir, "1_turn", () => focusFix, () => camFix, 10, 0.2f));
            for (int i = 0; i < 6; i++)
            {
                yield return new WaitForSeconds(0.4f);
                var turn = me.GetComponent<CarryTurn>();
                Debug.Log($"[CarryTest] turn +{(i + 1) * 0.4f:0.0} s: body {Mathf.DeltaAngle(y0, me.transform.eulerAngles.y):0} deg, view ahead {(turn != null ? turn.Offset : 0f):0} deg");
            }
            yield return new WaitForSeconds(0.6f);

            // Walk (slow, no run) a few metres.
            me.Motor.CommandProvider = () => new MotorCommand { Move = new Vector2(0f, 1f), Run = true };
            StartCoroutine(SideBurst(dir, "1_walk", focus, side, 10, 0.2f));
            yield return new WaitForSeconds(2.2f);
            CarryLog("walking (run held)", me, t);
            me.Motor.CommandProvider = null;
            yield return new WaitForSeconds(0.6f);

            // Put down (B).
            LocalPlayerController.TestCarryB = true;
            StartCoroutine(SideBurst(dir, "1_putdown", focus, side, 12, 0.12f));
            yield return new WaitForSeconds(0.2f);
            CarryLog("put down", me, t);
            yield return WaitCarry(me, false, 4f);
            yield return new WaitForSeconds(1.6f);
            CarryLog("after put down", me, t);
            yield return SideBurst(dir, "1_after", focus, side, 1, 0.05f);

            // ---- 2: lift again, the carrier gets hit -> drop
            me.TeleportLocal(t.transform.position + t.transform.right * 1.0f, t.transform.eulerAngles.y - 90f);
            yield return new WaitForSeconds(0.8f);
            LocalPlayerController.TestCarryB = true;
            yield return WaitCarry(me, true, 6f);
            yield return new WaitForSeconds(BodyCarryTimeline.LiftFloor + 0.8f);
            CarryLog("hold 2", me, t);
            StartCoroutine(SideBurst(dir, "2_drop", focus, side, 10, 0.1f));
            me.Limping.Value = true; // "hit" by the seeker
            yield return new WaitForSeconds(0.3f);
            CarryLog("drop", me, t);
            yield return WaitCarry(me, false, 4f);
            yield return new WaitForSeconds(1.6f);
            CarryLog("after drop", me, t);
            me.Limping.Value = false;

            // ---- 3: wounded but standing (conscious): lift, carry, the carried asks to be put down (Space, -carrywatch)
            t.Life.Value = LifeState.Alive;
            t.Limping.Value = true;
            t.DownedUntil.Value = 0;
            yield return new WaitForSeconds(2.0f);
            me.TeleportLocal(t.transform.position + t.transform.right * 0.9f, t.transform.eulerAngles.y - 90f);
            yield return new WaitForSeconds(0.8f);
            LocalPlayerController.TestCarryB = true;
            yield return WaitCarry(me, true, 6f);
            StartCoroutine(SideBurst(dir, "3_lift", focus, side, 11, 0.1f));
            yield return new WaitForSeconds(BodyCarryTimeline.LiftStanding + 0.6f);
            CarryLog("hold conscious", me, t);
            yield return SideBurst(dir, "3_hold_side", focus, side, 1, 0.05f);
            yield return SideBurst(dir, "3_hold_front", focus, front, 1, 0.05f);
            StartCoroutine(SideBurst(dir, "3_putdown", focus, side, 14, 0.12f));
            yield return WaitCarry(me, false, 9f);
            yield return new WaitForSeconds(1.2f);
            CarryLog("after conscious carry", me, t);

            // ---- 4: another hider carries the downed one, we treat him in the arms (needs a 4th instance)
            NetPlayer u = null;
            foreach (var p in NetPlayer.All) if (p != null && p != me && p != t && p.IsHider) u = p;
            if (u != null)
            {
                mm.TestDown(t);
                yield return new WaitForSeconds(2.0f);
                Vector3 tp = t.transform.position;
                u.TeleportRpc(tp + t.transform.right * 1.0f, t.transform.eulerAngles.y - 90f);
                me.TeleportLocal(tp + t.transform.right * 1.0f + t.transform.forward * 2.4f, t.transform.eulerAngles.y + 180f);
                yield return new WaitForSeconds(1.0f);
                u.ServerStartCarry(t); // consent skipped (tested above)
                yield return new WaitForSeconds(BodyCarryTimeline.LiftFloor + 1.0f);
                Debug.Log($"[CarryTest] 4 carried by {(t.CarriedBy != null ? t.CarriedBy.Nick : "-")}, bleed paused {t.BleedPaused}");
                me.MedkitCount.Value = 1;
                Vector3 look = t.ChestPosition - me.EyePosition;
                me.Motor.SetLook(Quaternion.LookRotation(new Vector3(look.x, 0f, look.z)).eulerAngles.y, -15f);
                yield return new WaitForSeconds(0.5f);
                Func<Vector3> hf = () => t.ChestPosition;
                Func<Vector3> hs = () => Vector3.Cross(Vector3.up, (me.transform.position - u.transform.position).normalized) * 2.6f + Vector3.up * 0.6f;
                LocalPlayerController.TestCarryHeal = true;
                StartCoroutine(SideBurst(dir, "4_heal", hf, hs, 14, 0.4f));
                yield return new WaitForSeconds(2.5f);
                Debug.Log($"[CarryTest] 4 healing: target={me.CarryHealTarget.Value} progress={me.CarryHealProgress.Value} carrierSpeed={u.CarrySpeedScale:0.00} locked={u.CarryTurnLocked}");
                if (!string.IsNullOrEmpty(dir)) ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "4_heal_fpp.png"));
                yield return new WaitForSeconds(BodyCarryTimeline.HealTime - 2.5f + 1.0f);
                LocalPlayerController.TestCarryHeal = false;
                Debug.Log($"[CarryTest] 4 after heal: life={t.Life.Value} wounded={t.Wounded.Value} medkits={me.MedkitCount.Value} carriedBy={(t.CarriedBy != null ? t.CarriedBy.Nick : "-")} bleedPaused={t.BleedPaused}");
                yield return new WaitForSeconds(1.5f);
                yield return SideBurst(dir, "4_awake", hf, hs, 1, 0.05f);
                u.RequestCarryPutDownRpc();
                yield return new WaitForSeconds(BodyCarryTimeline.PutDownStanding + 1.5f);
                Debug.Log($"[CarryTest] 4 put down: carriedBy={(t.CarriedBy != null ? t.CarriedBy.Nick : "-")} life={t.Life.Value}");
            }
            else Debug.Log("[CarryTest] 4 skipped (no third hider)");
            Debug.Log("[CarryTest] done");
        }

        private IEnumerator CarryWatch()
        {
            yield return WaitSeeking();
            string dir = ArgOr("-carryshots", "");
            if (!string.IsNullOrEmpty(dir)) System.IO.Directory.CreateDirectory(dir);
            var me = NetPlayer.Local;
            int n = 0;
            float carriedFor = 0f;
            bool asked = false;
            while (true)
            {
                yield return new WaitForSeconds(0.5f);
                if (me == null) yield break;
                var by = me.CarriedBy;
                if (by == null) { carriedFor = 0f; asked = false; continue; }
                carriedFor += 0.5f;
                var v = by.CarryVar.Value;
                Debug.Log($"[CarryWatch] me: {(me.Actions as BlockyCharacterActions)?.BodyCarryDebug} | carrier: {(by.Actions as BlockyCharacterActions)?.BodyCarryDebug}");
                Debug.Log($"[CarryWatch] carried by {by.Nick}: phase={v.PhaseValue} life={me.Life.Value} pos={me.transform.position:F2} carrier={by.transform.position:F2} cam={me.Camera.transform.position:F2} head={me.Character.Head.position:F2}");
                if (!string.IsNullOrEmpty(dir) && n < 40) ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"carried_{n++:00}.png"));
                if (!asked && carriedFor >= 4f && me.Life.Value == LifeState.Alive && v.PhaseValue == BodyCarryPhase.Hold)
                {
                    asked = true;
                    LocalPlayerController.TestCarrySpace = true;
                    Debug.Log("[CarryWatch] Space: asking to be put down");
                }
            }
        }
    }
}
