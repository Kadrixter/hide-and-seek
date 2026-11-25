using System;
using System.Collections;
using System.Collections.Generic;
using HideAndSeek.Core;
using HideAndSeek.Game.Cinematics;
using HideAndSeek.Game.Escape;
using HideAndSeek.Player;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Escape run test (Game/Escape). Log lines start with [EscTest].
    ///  Host (best -host -forceseeker -autostart -hidetime 3): -testescape &lt;dir&gt; = during Seeking the stage is forced
    ///  to GateOpening, the opening is timed, the seeker (host) tries to run past the line, two hiders escape one after
    ///  the other -> the round ends, hiders win, outro; -escvariant kill = the second hider is killed after the first
    ///  got out (still a hiders' win). PNGs into &lt;dir&gt;.
    ///  -escgate 1 (host and clients): the same through the west gate.
///  Clients: -escapewatch &lt;dir&gt; = a hider walks out at its turn (first at GateOpen + 7 s, second + 20 s), then
    ///  spectator / end / outro shots.
    /// </summary>
    public partial class NetSession
    {
        private void StartEscapeTests(Func<string, bool> has)
        {
            if (has("-testescape")) StartCoroutine(EscapeTest(ArgOr("-testescape", "")));
            if (has("-escapewatch")) StartCoroutine(EscapeWatch(ArgOr("-escapewatch", "")));
        }

        private static void ELog(string s) => Debug.Log("[EscTest] " + s);

        /// <summary>-escgate N: the run goes through gate N (0 = east, 1 = west; host and clients alike).</summary>
        private static int EG => int.TryParse(ArgOr("-escgate", "0"), out int g) ? g : 0;
        private static Vector3 GW(Vector3 local) => EscapeTimeline.ToWorld(local, EG);
        private static Vector3 GL(Vector3 world) => EscapeTimeline.ToLocal(world, EG);

        private static float GateYaw(Vector3 localDir)
        {
            EscapeTimeline.Frame(out _, out var rot, EG);
            return Quaternion.LookRotation(rot * localDir).eulerAngles.y;
        }

        private static List<NetPlayer> HidersByClient()
        {
            var l = new List<NetPlayer>();
            foreach (var p in NetPlayer.All) if (p != null && p.IsHider) l.Add(p);
            l.Sort((a, b) => a.OwnerClientId.CompareTo(b.OwnerClientId));
            return l;
        }

        private IEnumerator EscapeTest(string dir)
        {
            bool kill = ArgOr("-escvariant", "") == "kill";
            MatchManager mm = null;
            while ((mm = MatchManager.Instance) == null || mm.Phase.Value != MatchPhase.Seeking || NetPlayer.Local == null) yield return null;
            yield return new WaitForSeconds(2f);
            var me = NetPlayer.Local;
            var gate = ExitGate.Get(EG);
            var hiders = HidersByClient();
            ELog($"start: gate={EG} variant={(kill ? "kill" : "both escape")} me={me.Nick} seeker={me.IsSeeker} hiders={hiders.Count} gate={(gate != null ? "ok" : "-")} stage={mm.Escape.Value} gateAt={mm.EscapeGateAt.Value:0.0}");
            if (gate == null || hiders.Count < 2 || !me.IsSeeker) { ELog("need the gate, 2 hiders and the host as the seeker - stop"); yield break; }
            ELog($"closed gate: block={(Physics.Raycast(GW(new Vector3(0f, 1f, 1f)), GW(new Vector3(0f, 1f, -1f)) - GW(new Vector3(0f, 1f, 1f)), 2f) ? "solid" : "OPEN")}");

            // Everybody near the gate, inside.
            me.TeleportLocal(GW(new Vector3(2.4f, 0.05f, 7.5f)), GateYaw(Vector3.back));
            for (int i = 0; i < hiders.Count; i++)
                hiders[i].TeleportRpc(GW(new Vector3(i == 0 ? -2f : 0.5f, 0.05f, 5.5f + i)), GateYaw(Vector3.back));
            yield return new WaitForSeconds(1f);
            LookAt(me, GW(new Vector3(0f, 1.4f, 0f)));

            mm.ServerEscapeAdvance(EscapeStage.GateOpening);
            float t0 = Time.time;
            ELog("forced stage GateOpening");
            int k = 0;
            float[] at = { 2.5f, 8f, 13f };
            while (mm.Escape.Value < EscapeStage.GateOpen && Time.time - t0 < 60f)
            {
                if (k < at.Length && Time.time - t0 >= at[k])
                {
                    LookAt(me, GW(new Vector3(0f, 1.4f, 0f)));
                    Shot(dir, $"esc_{k}_opening_{at[k]:0}s");
                    CamShot(dir, $"esc_{k}_opening_side_{at[k]:0}s", GW(new Vector3(-5.5f, 1.9f, 6.5f)), GW(new Vector3(0.5f, 1.2f, 0f)));
                    ELog($"opening t={mm.Now - mm.EscapeGateAt.Value:0.0} clearance={EscapeTimeline.Clearance(mm.Now - mm.EscapeGateAt.Value):0.00} m");
                    k++;
                }
                yield return null;
            }
            double openT = mm.Now - mm.EscapeGateAt.Value;
            ELog($"stage GateOpen after {openT:0.0} s (wall clock {Time.time - t0:0.0} s), clearance {EscapeTimeline.Clearance(openT):0.00} m - expected ~{EscapeTimeline.StandAt:0.0} s");
            Shot(dir, "esc_3_open_seeker_view");
            CamShot(dir, "esc_4_open_side", GW(new Vector3(-5.5f, 1.9f, 6.5f)), GW(new Vector3(0.5f, 1.2f, 0f)));
            CamShot(dir, "esc_5_boundary_line", GW(new Vector3(2.2f, 2.3f, -1.2f)), GW(new Vector3(-0.3f, 0f, EscapeTimeline.LineZ)));

            // The seeker runs at the line.
            me.TeleportLocal(GW(new Vector3(0f, 0.05f, -2.5f)), GateYaw(Vector3.back));
            yield return new WaitForSeconds(0.3f);
            me.Motor.CommandProvider = () => new MotorCommand { Move = new Vector2(0f, 1f), Run = true };
            float minZ = 99f;
            for (float t = 0f; t < 3f; t += Time.deltaTime)
            {
                minZ = Mathf.Min(minZ, GL(me.transform.position).z);
                yield return null;
            }
            me.Motor.CommandProvider = null;
            ELog($"seeker run at the line: closest local z {minZ:0.00} (line {EscapeTimeline.LineZ}, wall {EscapeTimeline.SeekerWallZ}) -> {(minZ > EscapeTimeline.LineZ ? "BLOCKED OK" : "PASSED - FAIL")}, escaped={me.Escaped.Value}");
            CamShot(dir, "esc_6_seeker_at_wall", GW(new Vector3(-2.5f, 2.2f, -1f)), me.transform.position + Vector3.up);
            me.TeleportLocal(GW(new Vector3(-4.5f, 0.05f, 2.6f)), GateYaw(Vector3.back));
            yield return new WaitForSeconds(0.5f);
            LookAt(me, GW(new Vector3(0f, 1f, -5f)));

            // Hider 1 walks out (its own copy, -escapewatch).
            var h1 = hiders[0];
            var h2 = hiders[1];
            float w0 = Time.time;
            while (!h1.Escaped.Value && Time.time - w0 < 25f)
            {
                if (GL(h1.transform.position).z < -2.5f && k < 99)
                {
                    k = 99;
                    yield return new WaitForSeconds(0.4f);
                    CamShot(dir, "esc_7_hider_running_out", GW(new Vector3(-3f, 2.0f, 3.5f)), h1.transform.position + Vector3.up);
                }
                yield return null;
            }
            ELog($"hider 1 {h1.Nick}: escaped={h1.Escaped.Value} life={h1.Life.Value} aliveHider={h1.IsAliveHider} phase={mm.Phase.Value} winner so far={mm.Winner.Value}");
            yield return new WaitForSeconds(1f);
            Shot(dir, "esc_8_after_first_escape");

            if (kill)
            {
                yield return new WaitForSeconds(2f);
                ELog($"killing hider 2 {h2.Nick} (life {h2.Life.Value})");
                mm.TestKill(h2);
            }
            float w1 = Time.time;
            while (mm.Phase.Value == MatchPhase.Seeking && Time.time - w1 < 40f) yield return null;
            ELog($"round over: phase={mm.Phase.Value} winner={mm.Winner.Value} (expected Hiders) h2 escaped={h2.Escaped.Value} life={h2.Life.Value} anyEscaped={mm.AnyEscaped()}");
            yield return new WaitForSeconds(0.1f);
            Shot(dir, "esc_9_round_end");
            while (OutroDirector.Current == null && mm.Phase.Value == MatchPhase.Ended) yield return null;
            ELog($"outro up: {(OutroDirector.Current != null ? "yes" : "no")} row={OutroDirector.Row.Count}");
            while (OutroDirector.Current != null && OutroDirector.Current.T < 6.5f) yield return null;
            Shot(dir, "esc_10_outro");
            while (mm.Phase.Value != MatchPhase.Lobby) yield return null;
            yield return new WaitForSeconds(3f);
            int still = 0;
            foreach (var p in NetPlayer.All) if (p != null && p.Escaped.Value) still++;
            ELog($"back in the lobby: escaped flags left={still} (expected 0), stage={mm.Escape.Value} gateAt={mm.EscapeGateAt.Value:0.0}");
            ELog("done");
        }

        private IEnumerator EscapeWatch(string dir)
        {
            MatchManager mm = null;
            while ((mm = MatchManager.Instance) == null || NetPlayer.Local == null || mm.Phase.Value != MatchPhase.Seeking) yield return null;
            var me = NetPlayer.Local;
            if (!me.IsHider) { ELog($"{me.Nick} is not a hider - watch only"); yield break; }
            while (mm.Phase.Value == MatchPhase.Seeking && (mm.EscapeGateAt.Value <= 0d || mm.Escape.Value < EscapeStage.GateOpen)) yield return null;
            if (mm.Phase.Value != MatchPhase.Seeking) yield break;
            int idx = HidersByClient().IndexOf(me);
            double start = mm.EscapeGateAt.Value + EscapeTimeline.StandAt + (idx == 0 ? 7.0 : 20.0);
            ELog($"{me.Nick}: hider #{idx}, gate open, walking out at +{start - mm.Now:0.0} s");
            while (mm.Now < start && mm.Phase.Value == MatchPhase.Seeking) yield return null;
            if (!me.IsAliveHider || mm.Phase.Value != MatchPhase.Seeking) { ELog($"{me.Nick}: not walking (life {me.Life.Value}, phase {mm.Phase.Value})"); yield return EscapeWatchEnd(mm, dir); yield break; }
            me.TeleportLocal(GW(new Vector3(idx == 0 ? -1f : 1f, 0.05f, 3f)), GateYaw(Vector3.back));
            yield return new WaitForSeconds(0.3f);
            me.Motor.CommandProvider = () => new MotorCommand { Move = new Vector2(0f, 1f), Run = true };
            float t0 = Time.time;
            while (!me.Escaped.Value && Time.time - t0 < 8f) yield return null;
            me.Motor.CommandProvider = null;
            ELog($"{me.Nick}: escaped={me.Escaped.Value} after {Time.time - t0:0.0} s, local z {GL(me.transform.position).z:0.0}");
            yield return new WaitForSeconds(2.5f);
            var spec = UnityEngine.Object.FindFirstObjectByType<SpectatorCamera>();
            ELog($"{me.Nick}: spectator={(spec != null ? "on" : "off")} watching={(spec != null && spec.Target != null ? spec.Target.Nick : "-")} pos={me.transform.position} input={me.InputReader.InputEnabled}");
            Shot(dir, $"{me.Nick}_esc_spectator");
            yield return EscapeWatchEnd(mm, dir);
        }

        private IEnumerator EscapeWatchEnd(MatchManager mm, string dir)
        {
            var me = NetPlayer.Local;
            while (mm.Phase.Value == MatchPhase.Seeking) yield return null;
            yield return new WaitForSeconds(0.1f);
            Shot(dir, $"{me.Nick}_esc_round_end");
            ELog($"{me.Nick}: round over, winner={mm.Winner.Value}");
            while (OutroDirector.Current == null && mm.Phase.Value == MatchPhase.Ended) yield return null;
            while (OutroDirector.Current != null && OutroDirector.Current.T < 6.5f) yield return null;
            Shot(dir, $"{me.Nick}_esc_outro");
            while (mm.Phase.Value != MatchPhase.Lobby) yield return null;
            ELog($"{me.Nick}: lobby, escaped={me.Escaped.Value}");
            ELog("watch done");
        }
    }
}
