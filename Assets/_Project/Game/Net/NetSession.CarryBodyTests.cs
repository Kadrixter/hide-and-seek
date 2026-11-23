using System;
using System.Collections;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using HideAndSeek.Player;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Carrying dead bodies (3 instances: host -forcehider -autostart -autostartplayers 3 -hidetime 4 -testcarrybody,
    /// two clients; one becomes the seeker; optionally a 4th copy joining late with -bodywatch):
    ///  -testcarrybody (host, -carryshots dir): kills the other hider, waits for the ragdoll to settle, picks the body up
    ///      with B (no question), carries it a few metres, puts it down; then the seeker carries it (server call, the
    ///      body is his to pick up too) and puts it down again. Shots from a lit side camera, logs [CarryBodyTest]
    ///      (body chest vs. root, minimap body markers).
    ///  -bodywatch (late joiner, -carryshots dir): logs where it sees every dead body (chest + root) and takes a shot.
    /// </summary>
    public partial class NetSession
    {
        private void StartCarryBodyTests(Func<string, bool> has)
        {
            if (has("-testcarrybody")) StartCoroutine(CarryBodyTest());
            if (has("-bodywatch")) StartCoroutine(BodyWatch());
        }

        private static void BodyLog(string what, NetPlayer body)
        {
            var bca = body.Actions as BlockyCharacterActions;
            Debug.Log($"[CarryBodyTest] {what}: life={body.Life.Value} chest={body.ChestPosition:F2} root={body.transform.position:F2} " +
                      $"carriedBy={(body.CarriedBy != null ? body.CarriedBy.Nick : "-")} ragdoll={(bca != null && bca.IsRagdolled)} " +
                      $"settling={(bca != null && bca.IsSettlingAfterCarry)} minimapBodies={Minimap.BodyDots}");
        }

        private IEnumerator CarryBodyTest()
        {
            yield return WaitSeeking();
            var mm = MatchManager.Instance;
            var me = NetPlayer.Local;
            NetPlayer t = null, seeker = null;
            foreach (var p in NetPlayer.All)
            {
                if (p == null || p == me) continue;
                if (p.IsHider) t = p;
                else if (p.IsSeeker) seeker = p;
            }
            if (t == null) { Debug.Log("[CarryBodyTest] need a second hider"); yield break; }
            string dir = ArgOr("-carryshots", "");
            if (!string.IsNullOrEmpty(dir)) System.IO.Directory.CreateDirectory(dir);

            // A body on the floor next to us.
            Vector3 spot = me.transform.position + me.transform.forward * 2f;
            t.TeleportRpc(spot, me.transform.eulerAngles.y + 90f);
            yield return new WaitForSeconds(1.0f);
            TestLight(spot + new Vector3(0.6f, 2.4f, 0.5f), 10f, 2.4f);
            TestLight(spot + new Vector3(-1.6f, 1.3f, -1.4f), 7f, 1.1f);
            mm.TestKill(t);
            yield return new WaitForSeconds(5f);
            BodyLog("dead, settled", t);
            Func<Vector3> focus = () => me.transform.position + Vector3.up * 0.95f;
            Func<Vector3> side = () => me.transform.right * 2.7f + me.transform.forward * 1.3f + Vector3.up * 0.6f;
            yield return SideBurst(dir, "b0_body", () => t.ChestPosition, () => Vector3.right * 2.4f + Vector3.up * 1.2f, 1, 0.05f);

            // Hider picks the body up (B, no question).
            t.BodyLieFrame(out var pelvis, out var head);
            BodyCarryTimeline.CarrierSpotLying(pelvis, head, false, out var cpos, out var crot);
            me.TeleportLocal(cpos - crot * Vector3.forward * 0.5f, crot.eulerAngles.y);
            me.Motor.SetLook(crot.eulerAngles.y, -30f);
            yield return new WaitForSeconds(0.8f);
            LocalPlayerController.TestCarryB = true;
            float t0 = Time.time;
            yield return WaitCarry(me, true, 4f);
            Debug.Log($"[CarryBodyTest] lift started after {Time.time - t0:0.00} s (no question), flags dead={me.CarryVar.Value.DeadBody}");
            StartCoroutine(SideBurst(dir, "b1_lift", focus, side, 16, 0.16f));
            yield return new WaitForSeconds(BodyCarryTimeline.LiftFloor + 0.6f);
            BodyLog("held", t);
            yield return SideBurst(dir, "b1_hold_side", focus, side, 1, 0.05f);
            yield return SideBurst(dir, "b1_hold_front", focus, () => me.transform.forward * 2.8f + Vector3.up * 0.5f, 1, 0.05f);
            me.Motor.SetLook(me.Motor.LookYaw, -20f);
            yield return new WaitForSeconds(0.3f);
            if (!string.IsNullOrEmpty(dir)) ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "b1_fpp.png"));
            me.Motor.CommandProvider = () => new MotorCommand { Move = new Vector2(0f, 1f) };
            StartCoroutine(SideBurst(dir, "b1_walk", focus, side, 8, 0.25f));
            yield return new WaitForSeconds(2.0f);
            me.Motor.CommandProvider = null;
            yield return new WaitForSeconds(0.6f);
            BodyLog("carried 3 m", t);
            LocalPlayerController.TestCarryB = true;
            StartCoroutine(SideBurst(dir, "b1_putdown", focus, side, 14, 0.16f));
            yield return WaitCarry(me, false, 5f);
            yield return new WaitForSeconds(3f);
            BodyLog("put down", t);
            yield return SideBurst(dir, "b1_after", () => t.ChestPosition, () => Vector3.right * 2.4f + Vector3.up * 1.4f, 1, 0.05f);

            // The seeker carries it too.
            if (seeker != null)
            {
                t.BodyLieFrame(out pelvis, out head);
                BodyCarryTimeline.CarrierSpotLying(pelvis, head, true, out cpos, out crot);
                seeker.TeleportRpc(cpos, crot.eulerAngles.y);
                me.TeleportLocal(pelvis + Vector3.right * 3f, -90f);
                yield return new WaitForSeconds(1.2f);
                seeker.ServerStartCarry(t);
                Func<Vector3> sf = () => seeker.transform.position + Vector3.up * 0.95f;
                Func<Vector3> ss = () => seeker.transform.right * 2.7f + seeker.transform.forward * 1.3f + Vector3.up * 0.6f;
                StartCoroutine(SideBurst(dir, "b2_seeker_lift", sf, ss, 10, 0.25f));
                yield return new WaitForSeconds(BodyCarryTimeline.LiftFloor + 1.0f);
                Debug.Log($"[CarryBodyTest] seeker carries: {seeker.IsCarryingBody} held={seeker.Held.Value} speed={seeker.CarrySpeedScale:0.00}");
                BodyLog("held by the seeker", t);
                yield return SideBurst(dir, "b2_seeker_hold", sf, ss, 1, 0.05f);
                seeker.RequestCarryPutDownRpc();
                yield return new WaitForSeconds(BodyCarryTimeline.PutDownFloor + 3f);
                BodyLog("put down by the seeker", t);
            }
            Debug.Log("[CarryBodyTest] done");
        }

        private IEnumerator BodyWatch()
        {
            yield return WaitSeeking();
            yield return new WaitForSeconds(3f);
            string dir = ArgOr("-carryshots", "");
            for (int i = 0; i < 3; i++)
            {
                foreach (var p in NetPlayer.All)
                    if (p != null && p.Life.Value == LifeState.Dead) BodyLog($"late joiner sees {p.Nick}", p);
                if (i == 0 && !string.IsNullOrEmpty(dir))
                {
                    System.IO.Directory.CreateDirectory(dir);
                    foreach (var p in NetPlayer.All)
                        if (p != null && p.Life.Value == LifeState.Dead)
                        {
                            var body = p;
                            yield return SideBurst(dir, "late_body", () => body.ChestPosition, () => Vector3.right * 2.4f + Vector3.up * 1.4f, 1, 0.05f);
                            break;
                        }
                }
                yield return new WaitForSeconds(4f);
            }
        }
    }
}
