using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HideAndSeek.Game.Cinematics;
using HideAndSeek.Game.Escape;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Both street exits (east = gate 0, west = gate 1), logs [GateTest]. Runs next to -testescape (best with -escgate 1):
    ///  -testgates &lt;dir&gt; (host): the closed gates are solid, nothing of the map stands in the way of either gate (a
    ///  player capsule and a car-sized box swept from the garage to the end of the driveway once the gate is up),
    ///  both shutters rise together (measured with rays), the alarm pins come from both gates, pictures of the west gate
    ///  from inside / outside / the lintel / the wall from the street, then the outro is timed (no cut, lobby after).
    ///  -gateshud &lt;dir&gt; (a hider client): while the gate opens, stands nearer the west and then the east gate and logs
    ///  which gate the objective arrow points at (+ screenshots of the HUD).
    /// </summary>
    public class GateTests : MonoBehaviour
    {
        private static string[] Args => Environment.GetCommandLineArgs();
        private static bool Has(string a) => Array.IndexOf(Args, a) >= 0;
        private static string ArgOr(string name, string def)
        {
            int i = Array.IndexOf(Args, name);
            return i >= 0 && i + 1 < Args.Length && !Args[i + 1].StartsWith("-") ? Args[i + 1] : def;
        }

        private static string _dir;
        private static int _fails;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!Has("-testgates") && !Has("-gateshud")) return;
            var go = new GameObject("[GateTests]");
            DontDestroyOnLoad(go);
            var t = go.AddComponent<GateTests>();
            if (Has("-testgates")) { _dir = ArgOr("-testgates", ""); t.StartCoroutine(t.Host()); }
            if (Has("-gateshud")) { _dir = ArgOr("-gateshud", ""); t.StartCoroutine(t.Hud()); }
        }

        private static void Log(string s) => Debug.Log("[GateTest] " + s);
        private static void Fail(string s) { _fails++; Debug.LogWarning("[GateTest] FAIL: " + s); }

        private static void Cam(string name, int gate, Vector3 fromLocal, Vector3 atLocal, float fov = 60f)
        {
            if (string.IsNullOrEmpty(_dir)) return;
            var go = new GameObject("GateTestCam");
            var cam = go.AddComponent<Camera>();
            cam.enabled = false;
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.03f;
            cam.cullingMask = ~(1 << 9);
            go.transform.position = EscapeTimeline.ToWorld(fromLocal, gate);
            go.transform.LookAt(EscapeTimeline.ToWorld(atLocal, gate));
            // No fog and a work light between the camera and the target (the garage is pitch dark).
            bool fog = RenderSettings.fog;
            RenderSettings.fog = false;
            var lg = new GameObject("GateTestLight").AddComponent<Light>();
            lg.type = LightType.Point;
            lg.range = 16f;
            lg.intensity = 1.3f;
            lg.transform.position = Vector3.Lerp(go.transform.position, EscapeTimeline.ToWorld(atLocal, gate), 0.45f) + Vector3.up * 0.8f;
            HideAndSeek.Map.LevelLighting.Instance?.Track(lg);
            var tex = FuseSpotTests.Render(cam, 1280, 720);
            RenderSettings.fog = fog;
            Destroy(lg.gameObject);
            Directory.CreateDirectory(_dir);
            File.WriteAllBytes(Path.Combine(_dir, name + ".png"), tex.EncodeToPNG());
            Destroy(tex);
            Destroy(go);
        }

        /// <summary>Height of the free gap under the shutter (rays across the opening from the floor up).</summary>
        private static float Gap(int gate)
        {
            for (float y = 0.1f; y < 3f; y += 0.05f)
            {
                Vector3 a = EscapeTimeline.ToWorld(new Vector3(0f, y, 1f), gate), b = EscapeTimeline.ToWorld(new Vector3(0f, y, -1f), gate);
                if (Physics.Raycast(a, b - a, 2f, ItemPlacement.Mask, QueryTriggerInteraction.Ignore)) return y - 0.05f;
            }
            return 3f;
        }

        /// <summary>Static things in the way from inside the garage (local z 16) to the end of the driveway.</summary>
        private static void Sweep(int gate, string when)
        {
            Quaternion rot = Quaternion.LookRotation(EscapeTimeline.ToWorld(Vector3.back, gate) - EscapeTimeline.ToWorld(Vector3.zero, gate));
            foreach (float x in new[] { -2.5f, 0f, 2.5f })
            {
                Vector3 a = EscapeTimeline.ToWorld(new Vector3(x, 0.4f, 16f), gate), b = EscapeTimeline.ToWorld(new Vector3(x, 1.5f, 16f), gate);
                Vector3 dir = rot * Vector3.forward;
                var hits = Physics.CapsuleCastAll(a, b, 0.3f, dir, 21f, ItemPlacement.Mask, QueryTriggerInteraction.Ignore);
                var names = new List<string>();
                foreach (var h in hits)
                    if (h.collider.attachedRigidbody == null && h.collider.GetComponentInParent<NetworkObject>() == null)
                        names.Add($"{h.collider.name}@{16f - h.distance:0.0}");
                Log($"gate {gate} {when}: player path at x {x:+0.0;-0.0}: {(names.Count == 0 ? "clear" : string.Join(", ", names))}");
                // The east exit has the security barrier arms in its lanes on purpose (map since 2026-11); the west one must be clear.
                if (names.Count > 0 && gate == 1) Fail($"gate {gate} {when}: player path at x {x} blocked by {string.Join(", ", names)}");
            }
            // A car-sized box (2 m wide, 1.5 m high) along the middle.
            Vector3 c = EscapeTimeline.ToWorld(new Vector3(0f, 0.95f, 18f), gate);
            var carHits = Physics.BoxCastAll(c, new Vector3(1f, 0.75f, 2.2f), rot * Vector3.forward, rot, 20f, ItemPlacement.Mask, QueryTriggerInteraction.Ignore);
            var cn = new List<string>();
            foreach (var h in carHits)
                if (h.collider.attachedRigidbody == null && h.collider.GetComponentInParent<NetworkObject>() == null) cn.Add($"{h.collider.name}@{18f - h.distance:0.0}");
            Log($"gate {gate} {when}: car-sized box: {(cn.Count == 0 ? "clear" : string.Join(", ", cn))}");
        }

        private IEnumerator Host()
        {
            MatchManager mm = null;
            while ((mm = MatchManager.Instance) == null || mm.Phase.Value != MatchPhase.Seeking || NetPlayer.Local == null) yield return null;
            yield return new WaitForSeconds(0.3f);
            for (int g = 0; g < ExitGate.Count; g++)
            {
                var gate = ExitGate.Get(g);
                if (gate == null) { Fail($"gate {g} missing"); continue; }
                Log($"gate {g} at {gate.transform.position} yaw {gate.transform.eulerAngles.y:0} street={gate.StreetVisible} anchor={(GameObject.Find(ExitGate.AnchorName(g)) != null)} closed gap {Gap(g):0.00} m");
                if (Gap(g) > 0.15f) Fail($"gate {g} is not shut before the alarm (gap {Gap(g):0.00} m)");
                if (!gate.StreetVisible) Fail($"gate {g}: no opening in the wall (map not rebuilt?)");
            }
            foreach (int g in new[] { 1, 0 })
            {
                string n = g == 1 ? "w" : "e";
                Cam($"gate{n}_1_inside_closed", g, new Vector3(1.5f, 1.7f, 13f), new Vector3(0f, 1.5f, 0f));
                if (g == 1)
                {
                    Cam("gatew_2_lintel", g, new Vector3(2.2f, 2.1f, 2.4f), new Vector3(0f, 2.9f, 0f));
                    Cam("gatew_3_outside_closed", g, new Vector3(1.2f, 1.8f, -10.5f), new Vector3(0f, 1.6f, 0f));
                    Cam("gatew_4_wall_from_street", g, new Vector3(-3f, 1.6f, -10.8f), new Vector3(-9f, 2.0f, 0f), 75f);
                    Cam("gatew_5_wide_inside", g, new Vector3(-6f, 2.4f, 22f), new Vector3(0f, 1.2f, 0f), 70f);
                }
            }
            var pins = new List<Vector3>();
            void OnPin(Vector3 p) => pins.Add(p);
            MatchManager.AlarmPinged += OnPin;

            while (mm.Escape.Value < EscapeStage.GateOpening || mm.EscapeGateAt.Value <= 0d) { if (mm.Phase.Value != MatchPhase.Seeking) yield break; yield return null; }
            float maxDiff = 0f;
            int shots = 0;
            while (mm.Escape.Value < EscapeStage.GateOpen && mm.Phase.Value == MatchPhase.Seeking)
            {
                yield return new WaitForSeconds(1f);
                double t = mm.Now - mm.EscapeGateAt.Value;
                float e = Gap(0), w = Gap(1);
                maxDiff = Mathf.Max(maxDiff, Mathf.Abs(e - w));
                Log($"opening t={t:0.0}: gap east {e:0.00} m, west {w:0.00} m, expected {EscapeTimeline.Clearance(t):0.00} m");
                if (t > 6.0 && shots == 0) { shots++; Cam("gatew_6_opening_beacons", 1, new Vector3(-4.5f, 2.3f, 9f), new Vector3(0f, 2.0f, 0f)); }
            }
            Log($"both shutters: max gap difference {maxDiff:0.00} m");
            if (maxDiff > 0.12f) Fail($"the shutters do not rise together (diff {maxDiff:0.00} m)");
            yield return new WaitForSeconds(0.5f);
            for (int g = 0; g < ExitGate.Count; g++) Sweep(g, "open");
            Cam("gatew_7_open_inside", 1, new Vector3(1.5f, 1.7f, 13f), new Vector3(0f, 1.3f, -4f));
            Cam("gatew_8_open_from_street", 1, new Vector3(1.0f, 1.7f, -9.5f), new Vector3(0f, 1.2f, 6f));
            int near0 = 0, near1 = 0;
            foreach (var p in pins)
            {
                if ((p - EscapeTimeline.ToWorld(Vector3.zero, 0)).magnitude < 3f) near0++;
                if ((p - EscapeTimeline.ToWorld(Vector3.zero, 1)).magnitude < 3f) near1++;
            }
            Log($"alarm pins: {pins.Count} (east {near0}, west {near1})");
            if (near0 == 0 || near1 == 0) Fail("an alarm pin is missing at one of the gates");
            MatchManager.AlarmPinged -= OnPin;

            // The outro: from the end of the round to the lobby.
            while (mm.Phase.Value == MatchPhase.Seeking) yield return null;
            float endAt = Time.realtimeSinceStartup;
            while (OutroDirector.Current == null && mm.Phase.Value == MatchPhase.Ended) yield return null;
            float outroAt = Time.realtimeSinceStartup, maxT = 0f, maxDt = 0f, worstAt = 0f;
            int frames = 0, shot = 0;
            float[] shotAt = { 10f, 14.6f, 17.5f, 19.2f };
            while (OutroDirector.Current != null && mm.Phase.Value == MatchPhase.Ended)
            {
                float ot = OutroDirector.Current.T;
                maxT = Mathf.Max(maxT, ot);
                if (frames++ > 10 && Time.unscaledDeltaTime > maxDt) { maxDt = Time.unscaledDeltaTime; worstAt = ot; }
                if (shot < shotAt.Length && ot >= shotAt[shot] && !string.IsNullOrEmpty(_dir))
                {
                    ScreenCapture.CaptureScreenshot(Path.Combine(_dir, $"outro_{shotAt[shot]:00.0}s.png".Replace(',', '.')));
                    shot++;
                }
                yield return null;
            }
            float outroGone = Time.realtimeSinceStartup;
            while (mm.Phase.Value != MatchPhase.Lobby) yield return null;
            Log($"outro: up {outroAt - endAt:0.00} s after the round end, ran to T={maxT:0.00} (End {OutroTimeline.End}), gone after {outroGone - outroAt:0.00} s, lobby after {Time.realtimeSinceStartup - outroAt:0.00} s, worst frame {maxDt * 1000f:0} ms at T={worstAt:0.00}");
            if (maxT < OutroTimeline.End - 0.3f) Fail($"outro cut short at T={maxT:0.00}");
            Log($"done, fails={_fails}");
        }

        private IEnumerator Hud()
        {
            MatchManager mm = null;
            while ((mm = MatchManager.Instance) == null || NetPlayer.Local == null || mm.Phase.Value != MatchPhase.Seeking) yield return null;
            var me = NetPlayer.Local;
            if (!me.IsHider) { Log($"{me.Nick} is not a hider - no HUD check"); yield break; }
            while (mm.Escape.Value < EscapeStage.GateOpening && mm.Phase.Value == MatchPhase.Seeking) yield return null;
            yield return new WaitForSeconds(3f);
            var target = typeof(EscapeHud).GetMethod("GateTarget", BindingFlags.NonPublic | BindingFlags.Static);
            foreach (int g in new[] { 1, 0 })
            {
                // 26 m into the garage from gate g, looking at it.
                Vector3 at = EscapeTimeline.ToWorld(new Vector3(0f, 0.05f, 26f), g);
                Vector3 look = EscapeTimeline.ToWorld(Vector3.zero, g) - at;
                me.TeleportLocal(at, Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg);
                yield return new WaitForSeconds(0.8f);
                int near = EscapeTimeline.Nearest(me.transform.position);
                Vector3 tgt = target != null ? (Vector3)target.Invoke(null, new object[] { true }) : Vector3.zero;
                float toW = (tgt - ExitGate.Get(1).SillWorld).magnitude, toE = (tgt - ExitGate.Get(0).SillWorld).magnitude;
                Log($"{me.Nick} near gate {g}: Nearest={near}, HUD target {tgt} ({(toW < 0.5f ? "west" : toE < 0.5f ? "east" : "?")})");
                if (near != g || (g == 1 ? toW : toE) > 0.5f) Fail($"HUD near gate {g} points at the wrong gate");
                if (!string.IsNullOrEmpty(_dir)) { Directory.CreateDirectory(_dir); ScreenCapture.CaptureScreenshot(Path.Combine(_dir, $"{me.Nick}_hud_near_gate{g}.png")); }
                yield return new WaitForSeconds(0.4f);
            }
            Log($"hud done, fails={_fails}");
        }
    }
}
