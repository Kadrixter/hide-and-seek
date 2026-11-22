using System;
using System.Collections;
using System.Collections.Generic;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Ragdoll tests in the built game (NOT run automatically):
    ///  -testragdoll all|steps (host with -forcehider, + a seeker client and another hider client = the victim):
    ///      every way to die, one after another, the victim brought back between the steps. Steps (comma separated):
    ///      k4 k5 k6 k7 k8 k9 k11 k12 = kills that end dead (finishers, chainsaw), k0w k10w = knife kill of a wounded
    ///      victim (downed by the animation, then dead), fall = dead from standing, bleed = downed -> dead,
    ///      shot = wounded victim shot dead standing (push along the shot), shotdown = downed victim shot dead,
    ///      sever = arm cut off while lying, revive = adrenaline: up from the ragdoll, walk = the host walks across a
    ///      fresh body (must not be lifted or blocked, the body must not move), late = the dead state applied as for a
    ///      player joining late. "all" = every step.
    ///  -ragdollshots &lt;folder&gt;: side camera, 40 frames 0.1 s apart from the moment of death of every step.
    /// Log lines: [RagdollTest] (state after every step), [Ragdoll] (start / settle on every client).
    /// </summary>
    public partial class NetSession
    {
        private static readonly string[] AllRagdollSteps =
        {
            "k4", "k5", "k6", "k7", "k8", "k9", "k11", "k12", "k0w", "k10w",
            "fall", "bleed", "shot", "shotdown", "sever", "revive", "walk", "late",
        };

        private void StartRagdollTests(Func<string, bool> has)
        {
            string list = ArgOr("-testragdoll", "");
            if (has("-testragdoll")) StartCoroutine(RagdollTest(string.IsNullOrEmpty(list) || list.StartsWith("-") ? "all" : list));
        }

        private IEnumerator RagdollTest(string list)
        {
            yield return WaitSeeking();
            var mm = MatchManager.Instance;
            var me = NetPlayer.Local;
            NetPlayer seeker = null, victim = null;
            foreach (var p in NetPlayer.All)
            {
                if (p == null) continue;
                if (p.IsSeeker) seeker = p;
                else if (p != me) victim = p;
            }
            if (seeker == null || victim == null) { Debug.Log("[RagdollTest] need a seeker and a second hider"); yield break; }
            var steps = list == "all" ? new List<string>(AllRagdollSteps) : new List<string>(list.Split(','));
            string shots = ArgOr("-ragdollshots", "");
            Vector3 spot = victim.transform.position;
            me.TeleportRpc(spot + victim.transform.right * 5f, 0f);
            int n = 0;
            foreach (var raw in steps)
            {
                string step = raw.Trim();
                if (step.Length == 0) continue;
                n++;
                // Fresh, alive, unhurt victim on the spot; the seeker in front of it.
                victim.Life.Value = LifeState.Alive;
                victim.Severed.Value = 0;
                victim.Wounded.Value = false;
                victim.Limping.Value = false;
                victim.Busy.Value = false;
                victim.DownedUntil.Value = 0;
                seeker.Busy.Value = false;
                seeker.AttackReadyAt.Value = 0;
                seeker.Held.Value = HeldItem.Knife;
                victim.TeleportRpc(spot, 0f);
                seeker.TeleportRpc(spot + Vector3.forward * 1.4f, 180f);
                if (step != "walk") me.TeleportRpc(spot + Vector3.right * 5f, 270f);
                yield return new WaitForSeconds(2.4f);
                Debug.Log($"[RagdollTest] {n:00} {step}: start");

                float deathIn = 0f; // seconds until the victim dies (shots start a bit earlier)
                if (step.StartsWith("k") && int.TryParse(step.Substring(1).TrimEnd('w'), out int km))
                {
                    var move = (KillMove)km;
                    bool wounded = step.EndsWith("w");
                    victim.Wounded.Value = wounded;
                    victim.Limping.Value = !wounded;
                    if (move == KillMove.GlockFinisher) { mm.TestGiveGlock(); seeker.Held.Value = HeldItem.Glock; }
                    else seeker.Held.Value = KillMoves.IsSaw(move) ? HeldItem.Chainsaw : HeldItem.Knife;
                    yield return new WaitForSeconds(0.3f);
                    mm.TestKillMove(seeker, victim, move);
                    deathIn = KillMoves.VictimDuration(move) + 0.1f;
                }
                else if (step == "fall" || step == "walk" || step == "sever" || step == "late")
                {
                    mm.TestKill(victim);
                }
                else if (step == "bleed")
                {
                    mm.TestDown(victim);
                    yield return new WaitForSeconds(2.5f);
                    mm.TestKill(victim);
                }
                else if (step == "shot" || step == "revive")
                {
                    victim.Wounded.Value = true;
                    mm.TestGiveGlock();
                    seeker.Held.Value = HeldItem.Glock;
                    yield return new WaitForSeconds(0.8f);
                    mm.TestShoot(seeker, victim);
                }
                else if (step == "shotdown")
                {
                    mm.TestDown(victim);
                    mm.TestGiveGlock();
                    seeker.Held.Value = HeldItem.Glock;
                    yield return new WaitForSeconds(2.5f);
                    mm.TestShoot(seeker, victim);
                }
                else
                {
                    Debug.Log($"[RagdollTest] unknown step {step}");
                    continue;
                }

                // Film the fall (starts shortly before the death).
                if (!string.IsNullOrEmpty(shots))
                {
                    var v = victim;
                    float lead = Mathf.Max(0f, deathIn - 0.6f);
                    StartCoroutine(DelayedBurst(lead, shots, $"{n:00}_{step}",
                        () => v.Ragdoll != null && v.Ragdoll.IsActive ? v.Ragdoll.TorsoCenter : v.ChestPosition,
                        () => Vector3.Cross(Vector3.up, v.transform.forward) * 2.6f + Vector3.up * 1.3f + v.transform.forward * 0.6f));
                }
                yield return new WaitForSeconds(deathIn + 0.5f);

                if (step == "sever")
                {
                    yield return new WaitForSeconds(1.2f); // still moving: the cut comes in mid-fall
                    victim.Severed.Value = (byte)(victim.Severed.Value | Limb.ArmL);
                }
                else if (step == "walk")
                {
                    yield return new WaitForSeconds(0.95f); // the fall animation ends at 1.35 s: walk while the body still moves
                    yield return WalkAcross(me, victim);
                }
                else if (step == "late")
                {
                    yield return new WaitForSeconds(3f);
                    // As for a player joining now: the plain dead pose, the ragdoll settles from it.
                    var bca = victim.Actions as BlockyCharacterActions;
                    bca?.SetLifeStateImmediate(LifeState.Alive);
                    bca?.SetLifeStateImmediate(LifeState.Dead);
                }

                yield return new WaitForSeconds(4.5f);
                LogRagdoll(n, step, victim);

                if (step == "revive")
                {
                    victim.Life.Value = LifeState.Alive; // adrenaline (Dead -> Alive while seeking: the gasp get-up)
                    victim.Wounded.Value = true;
                    yield return new WaitForSeconds(0.1f);
                    var r = victim.Ragdoll;
                    Debug.Log($"[RagdollTest] {n:00} revive: ragdoll {(r != null && r.IsActive ? "STILL ACTIVE" : "off")} life={victim.Life.Value}");
                    yield return new WaitForSeconds(3f);
                    Debug.Log($"[RagdollTest] {n:00} revive: up, root {victim.transform.position:F2} pelvis {victim.Character.Pelvis.position:F2}");
                }
            }
            victim.Life.Value = LifeState.Alive;
            victim.Severed.Value = 0;
            Debug.Log("[RagdollTest] done");
        }

        private IEnumerator DelayedBurst(float delay, string dir, string prefix, Func<Vector3> focus, Func<Vector3> offset)
        {
            yield return new WaitForSeconds(delay);
            yield return SideBurst(dir, prefix, focus, offset, 40, 0.1f);
        }

        /// <summary>The host walks straight through the fresh body: not lifted, not blocked, the body stays where it is.</summary>
        private IEnumerator WalkAcross(NetPlayer me, NetPlayer victim)
        {
            var r = victim.Ragdoll;
            Vector3 body = r != null ? r.TorsoCenter : victim.ChestPosition;
            Vector3 dir = victim.transform.right;
            dir.y = 0f;
            dir.Normalize();
            Vector3 start = new Vector3(body.x, victim.transform.position.y, body.z) - dir * 1.6f;
            me.TeleportLocal(start + Vector3.up * 0.05f, Quaternion.LookRotation(dir).eulerAngles.y);
            yield return new WaitForSeconds(0.3f);
            var cc = me.GetComponent<CharacterController>();
            Vector3 bodyBefore = r != null ? r.TorsoCenter : body;
            float y0 = me.transform.position.y, maxLift = 0f;
            float t = 0f;
            while (t < 1.4f)
            {
                if (cc != null && cc.enabled) cc.Move((dir * 2.4f + Vector3.down * 2f) * Time.deltaTime);
                maxLift = Mathf.Max(maxLift, me.transform.position.y - y0);
                t += Time.deltaTime;
                yield return null;
            }
            float walked = Vector3.Dot(me.transform.position - start, dir);
            float moved = r != null ? Vector3.Distance(r.TorsoCenter, bodyBefore) : 0f;
            Debug.Log($"[RagdollTest] walk: walked {walked:0.00} m of {2.4f * 1.4f:0.00}, lifted {maxLift:0.000} m, body moved {moved:0.000} m " +
                      $"({(walked > 3f && maxLift < 0.05f && moved < 0.05f ? "OK" : "PROBLEM")})");
        }

        private static void LogRagdoll(int n, string step, NetPlayer victim)
        {
            var r = victim.Ragdoll;
            if (r == null) { Debug.Log($"[RagdollTest] {n:00} {step}: no ragdoll (-noragdoll?) life={victim.Life.Value}"); return; }
            Vector3 root = victim.transform.position;
            Vector3 torso = r.TorsoCenter;
            Vector3 flat = torso - root;
            flat.y = 0f;
            float low = r.LowestPoint - root.y;
            Debug.Log($"[RagdollTest] {n:00} {step}: life={victim.Life.Value} active={r.IsActive} frozen={r.IsFrozen} settle={r.SettleTime:0.00}s " +
                      $"drift={flat.magnitude:0.00}m lowest={low:0.000}m severed={victim.Severed.Value} start[{r.LastStartInfo}] " +
                      $"{(r.IsActive && r.IsFrozen && low > -0.05f ? "OK" : "PROBLEM")}");
        }
    }
}
