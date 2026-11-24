using System;
using System.Collections;
using System.IO;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Tests of the fence wire / RC antenna upgrade (own component, started from the command line, run on request):
    ///  -testrcwire &lt;dir&gt; (host -forcehider -autostart -hidetime N, + a seeker client): the wire-mesh spots, the E
    ///      prompt at a fence, the 4 s pull (first-person shots), the wire in the inventory and in the hand (no hint, no
    ///      prompt), the same panel refused, a pull let go after 1 s (nothing), the RC car put down and parked, the U
    ///      prompt with the transmitter, the range measured (the car driven slowly outwards until the radio stops it),
    ///      the antenna swap (shots), the range measured again; logs [RcWireTest].
    ///  -testrcwirewatch &lt;dir&gt; (the other instance): films the hider from the side while he tears the wire, holds it,
    ///      swaps the antenna, and the car with its new antenna; logs what it sees.
    /// </summary>
    public class RcWireTests : MonoBehaviour
    {
        private static string[] Args => Environment.GetCommandLineArgs();
        private static bool Has(string a) => Array.IndexOf(Args, a) >= 0;

        private static string ArgOr(string name, string def)
        {
            int i = Array.IndexOf(Args, name);
            return i >= 0 && i + 1 < Args.Length && !Args[i + 1].StartsWith("-") ? Args[i + 1] : def;
        }

        private static string _dir;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!Has("-testrcwire") && !Has("-testrcwirewatch")) return;
            var go = new GameObject("[RcWireTests]");
            DontDestroyOnLoad(go);
            var t = go.AddComponent<RcWireTests>();
            if (Has("-testrcwire")) { _dir = ArgOr("-testrcwire", ""); t.StartCoroutine(t.HostTest()); }
            if (Has("-testrcwirewatch")) { _dir = ArgOr("-testrcwirewatch", ""); t.StartCoroutine(t.Watch()); }
        }

        private static void Log(string s) => Debug.Log("[RcWireTest] " + s);

        private static IEnumerator WaitSeeking()
        {
            while (MatchManager.Instance == null || MatchManager.Instance.Phase.Value != MatchPhase.Seeking || NetPlayer.Local == null) yield return null;
            yield return new WaitForSeconds(1.5f);
        }

        private static void Screen(string name)
        {
            if (string.IsNullOrEmpty(_dir)) return;
            Directory.CreateDirectory(_dir);
            ScreenCapture.CaptureScreenshot(Path.Combine(_dir, name + ".png"));
        }

        private static void Light(Vector3 pos, float range, float intensity)
        {
            var l = new GameObject("RcWireTestLight").AddComponent<Light>();
            l.type = LightType.Point;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.Soft;
            l.transform.position = pos;
        }

        /// <summary>Renders <paramref name="count"/> frames of a side camera (focus + offset recomputed every frame).</summary>
        private IEnumerator Side(string prefix, Func<Vector3> focus, Func<Vector3> offset, int count, float every, float fov = 45f)
        {
            if (string.IsNullOrEmpty(_dir)) yield break;
            Directory.CreateDirectory(_dir);
            var camGo = new GameObject("RcWireSideCam");
            var cam = camGo.AddComponent<Camera>();
            cam.enabled = false;
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.02f;
            cam.cullingMask = ~(1 << 9);
            var rt = new RenderTexture(960, 540, 24) { antiAliasing = 4 };
            cam.targetTexture = rt;
            var tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
            for (int i = 0; i < count; i++)
            {
                yield return new WaitForSeconds(every);
                yield return new WaitForEndOfFrame();
                Vector3 f = focus();
                camGo.transform.position = f + offset();
                camGo.transform.LookAt(f);
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(_dir, $"{prefix}_{i:00}.png"), tex.EncodeToPNG());
            }
            Destroy(camGo);
            rt.Release();
        }

        private static string Inv(NetPlayer me) => $"inv={me.Inv.Value} wire={me.WireCount.Value} wireSlot={Inventory.SlotOf(me, HotbarItem.Wire) + 1}";

        private static string PromptNow()
        {
            var lpc = LocalPlayerController.Instance;
            return lpc != null ? $"{lpc.Prompt} '{lpc.PromptText}'" : "-";
        }

        // ================================================================ host

        private IEnumerator HostTest()
        {
            yield return WaitSeeking();
            var mm = MatchManager.Instance;
            var me = NetPlayer.Local;
            if (!me.IsHider || !mm.IsServer) { Log("need the host as a hider (-forcehider) + a seeker client"); yield break; }
            var spots = WireSpots.All;
            int fences = 0;
            foreach (var s in spots) if (s.Name.StartsWith("TempFence_")) fences++;
            Log($"spots: {spots.Count} ({fences} fence panels, {spots.Count - fences} cage panels)");
            for (int i = 0; i < spots.Count; i++) if (i < 4 || i % 10 == 0) Log($"  spot {i}: {spots[i].Name} at {spots[i].Center:F1}");

            // 1. Pick a fence panel with room to stand in front of it.
            int a = PickSpot(spots, true, -1, out float along, out int side);
            if (a < 0) a = PickSpot(spots, false, -1, out along, out side);
            if (a < 0) { Log("FAIL: no usable spot"); yield break; }
            var sa = spots[a];
            Vector3 stand = sa.Stand(along, side) + sa.Normal * (side * 0.25f);
            float yaw = Quaternion.LookRotation(-sa.Normal * side).eulerAngles.y;
            Light(sa.Center + sa.Normal * (side * 1.6f) + Vector3.up * 1.4f, 9f, 2.2f);
            me.Motor.Teleport(stand, yaw, -12f);
            yield return new WaitForSeconds(1.0f);
            Log($"at {sa.Name} (spot {a}, along {along:0.00}, side {side}): prompt {PromptNow()} | {Inv(me)}");
            Screen("01_fence_prompt");
            yield return new WaitForSeconds(0.3f);

            // 2. Hold E for the whole pull.
            RcWireDriver.TestHoldE = true;
            LocalPlayerController.TestPressE = true;
            float t0 = Time.time;
            var drv = me.GetComponent<RcWireDriver>();
            float[] at = { 0.5f, 1.4f, 2.4f, 3.25f, 3.55f, 3.8f };
            for (int k = 0; k < at.Length; k++)
            {
                while (Time.time - t0 < at[k]) yield return null;
                Screen($"02_pull_fp_{k}");
                Log($"pull t={drv?.PullTime:0.00} prog={LocalPlayerController.Instance.WireProgress:0.00} state={me.WirePull.Value.Spot} strand={(drv != null && drv.Strand != null)}");
            }
            while (Time.time - t0 < 4.6f) yield return null;
            RcWireDriver.TestHoldE = false;
            yield return new WaitForSeconds(0.4f);
            Log($"after pull: {Inv(me)} taken={mm.WireTaken.Value.CountSet()} sounds={WireSounds.Played}");
            if (me.WireCount.Value != 1) Log("FAIL: no wire after the pull");

            // 3. The wire in the hand: no hint above the hotbar, no prompt.
            Hotbar.TestSelect = Inventory.SlotOf(me, HotbarItem.Wire);
            me.Motor.Teleport(me.transform.position - sa.Normal * (side * -0.6f), yaw + 150f, -20f);
            yield return new WaitForSeconds(1.2f);
            Log($"wire in hand: hint='{Hotbar.Hint(me, HotbarItem.Wire)}' prompt {PromptNow()} handItem={me.HandItem}");
            Screen("03_wire_in_hand_fp");
            yield return new WaitForSeconds(0.4f);

            // 4. The same panel again: no prompt any more.
            me.Motor.Teleport(stand, yaw, -12f);
            yield return new WaitForSeconds(0.8f);
            Log($"same panel again: prompt {PromptNow()}");

            // 5. Another panel, E let go after 1 s: nothing.
            int b = PickSpot(spots, false, a, out float alongB, out int sideB);
            if (b >= 0)
            {
                var sb = spots[b];
                me.Motor.Teleport(sb.Stand(alongB, sideB) + sb.Normal * (sideB * 0.25f), Quaternion.LookRotation(-sb.Normal * sideB).eulerAngles.y, -12f);
                yield return new WaitForSeconds(1.0f);
                Log($"at {sb.Name}: prompt {PromptNow()}");
                RcWireDriver.TestHoldE = true;
                LocalPlayerController.TestPressE = true;
                yield return new WaitForSeconds(1.1f);
                RcWireDriver.TestHoldE = false;
                yield return new WaitForSeconds(1.0f);
                Log($"let go after 1 s: pull={me.WirePull.Value.Spot} {Inv(me)} taken={mm.WireTaken.Value.CountSet()}");
            }

            // 6. RC car from the shelf, put down, parked; the transmitter in the hands -> the U prompt.
            var shelfRot = Quaternion.Euler(0f, RcCarShelf.Yaw, 0f);
            me.Motor.Teleport(RcCarShelf.Position + shelfRot * new Vector3(-0.3f, 0f, -1.15f), RcCarShelf.Yaw);
            yield return new WaitForSeconds(0.8f);
            mm.ServerRcPickup(me, 0);
            yield return new WaitForSeconds(0.5f);
            Vector3 body = FindRangeSpot(me, out Vector3 dir0);
            me.Motor.Teleport(body, Quaternion.LookRotation(dir0).eulerAngles.y);
            Light(body + Vector3.up * 2.4f, 10f, 2f);
            yield return new WaitForSeconds(0.8f);
            Hotbar.TestSelect = Inventory.SlotOf(me, HotbarItem.RcCar);
            yield return new WaitForSeconds(0.6f);
            Hotbar.TestClick = true; // put down + steer
            yield return new WaitForSeconds(2.6f);
            LocalPlayerController.TestSix = true; // park
            yield return new WaitForSeconds(1.8f);
            Hotbar.TestSelect = Inventory.SlotOf(me, HotbarItem.RcRemote);
            yield return new WaitForSeconds(1.0f);
            Log($"transmitter in the hands: {RcLine(mm, me)} prompt {PromptNow()} block={mm.AntennaSwapBlock(me) ?? "none"}");
            Screen("04_antenna_prompt_fp");
            yield return new WaitForSeconds(0.3f);

            // 7. Range before.
            float before = -1f;
            yield return MeasureRange(me, mm, Tuning.RcRange, d => before = d);
            Hotbar.TestSelect = Inventory.SlotOf(me, HotbarItem.RcRemote);
            yield return new WaitForSeconds(1.0f);
            Log($"range before: car stopped {before:0.00} m from the driver (limit {MatchManager.RcRangeOf(0):0})");

            // 8. The antenna swap: out on the open lane, 2 m behind the parked car (it is in view).
            Vector3 home = me.transform.position;
            var parked = RcCar.Peek(0);
            if (parked != null)
            {
                Vector3 lane = parked.transform.position - home;
                lane.y = 0f;
                lane.Normalize();
                Vector3 sp = parked.transform.position - lane * 2.3f + Vector3.Cross(Vector3.up, lane) * 0.5f;
                if (Physics.Raycast(sp + Vector3.up * 0.8f, Vector3.down, out var fh, 1.6f)) sp = fh.point;
                me.Motor.Teleport(sp, Quaternion.LookRotation(lane).eulerAngles.y, -14f);
                Light(sp + Vector3.up * 2.6f + lane * 1.2f, 9f, 2.4f);
            }
            yield return new WaitForSeconds(1.0f);
            Screen("04b_antenna_prompt_lane_fp");
            yield return new WaitForSeconds(0.3f);
            Log($"swap: prompt {PromptNow()} block={mm.AntennaSwapBlock(me) ?? "none"} {Inv(me)}");
            LocalPlayerController.TestKeyU = true;
            t0 = Time.time;
            float[] sw = { 0.35f, 0.75f, 1.05f, 1.35f, 1.9f, 2.35f, 2.75f, 3.2f, 3.8f };
            for (int k = 0; k < sw.Length; k++)
            {
                while (Time.time - t0 < sw[k]) yield return null;
                Screen($"05_swap_fp_{k}");
                Log($"swap t={me.AntennaSwapTime:0.00} wired={mm.RcWired.Value} {Inv(me)}");
            }
            yield return new WaitForSeconds(0.8f);
            Log($"after swap: wired={mm.RcWired.Value} range0={MatchManager.RcRangeOf(0):0} range1={MatchManager.RcRangeOf(1):0} {Inv(me)} hint='{Hotbar.Hint(me, HotbarItem.RcRemote)}'");
            Screen("06_new_antenna_fp");
            var car = RcCar.Peek(0);
            if (car != null)
            {
                Light(car.transform.position + Vector3.up * 1.2f, 5f, 1.6f);
                yield return Side("07_car_new_antenna", () => car.transform.position + Vector3.up * 0.15f, () => new Vector3(0.55f, 0.35f, -0.7f), 2, 0.3f, 40f);
            }
            LocalPlayerController.TestKeyU = true; // a second swap is refused
            yield return new WaitForSeconds(0.6f);
            Log($"second U: swapAt={me.AntennaSwapAt.Value:0.0} wired={mm.RcWired.Value} {Inv(me)}");

            // 9. Range after (back where the driver stood before).
            me.Motor.Teleport(home, Quaternion.LookRotation(dir0).eulerAngles.y);
            yield return new WaitForSeconds(1.0f);
            float after = -1f;
            yield return MeasureRange(me, mm, Tuning.RcRange * WireTuning.RangeMul, d => after = d);
            Log($"range after: car stopped {after:0.00} m from the driver (limit {MatchManager.RcRangeOf(0):0})");
            Log($"RESULT range before={before:0.0} m after={after:0.0} m ratio={(before > 0f ? after / before : 0f):0.000}");
            Log("done");
        }

        private static string RcLine(MatchManager mm, NetPlayer me)
        {
            var s = mm.RcSlot(0);
            return $"slot0 {s.Mode} remoteSlot={mm.RcRemoteSlotOf(me)} out={me.RcRemoteOut.Value} {Inv(me)}";
        }

        /// <summary>A spot whose stand point is free (capsule) and on the floor; prefers fences.</summary>
        private static int PickSpot(System.Collections.Generic.IReadOnlyList<WireSpots.Spot> spots, bool fences, int not, out float along, out int side)
        {
            along = 0f;
            side = 1;
            var taken = MatchManager.Instance.WireTaken.Value;
            for (int i = 0; i < spots.Count; i++)
            {
                if (i == not || taken.Get(i)) continue;
                var s = spots[i];
                if (fences != s.Name.StartsWith("TempFence_")) continue;
                foreach (float a in new[] { s.Half * 0.7f, -s.Half * 0.7f, 0f })
                    foreach (int sd in new[] { 1, -1 })
                    {
                        float al = s.ClampAlong(a);
                        Vector3 st = s.Stand(al, sd) + s.Normal * (sd * 0.25f);
                        if (Physics.CheckCapsule(st + Vector3.up * 0.4f, st + Vector3.up * 1.6f, 0.3f, ~((1 << 8) | (1 << 9) | (1 << 2)), QueryTriggerInteraction.Ignore)) continue;
                        if (!Physics.Raycast(st + Vector3.up * 0.3f, Vector3.down, out var h, 0.8f) || Mathf.Abs(h.point.y - s.Floor) > 0.2f) continue;
                        along = al;
                        side = sd;
                        return i;
                    }
            }
            return -1;
        }

        // ================================================================ range

        private static readonly Vector3[] RangeBodies =
        {
            new Vector3(-13.6f, -3.4f, 54.6f), new Vector3(-80f, -3.4f, -50f), new Vector3(80f, -3.4f, -50f), new Vector3(-80f, -3.4f, 50f),
            new Vector3(80f, -3.4f, 50f), new Vector3(-80f, 0f, -50f), new Vector3(80f, 0f, 50f), new Vector3(0f, -3.4f, -50f),
        };

        /// <summary>A body position on level -1 from which a straight free lane reaches 95 m (the direction in <paramref name="dir"/>).</summary>
        private static Vector3 FindRangeSpot(NetPlayer me, out Vector3 dir)
        {
            foreach (var b in RangeBodies)
            {
                if (!Physics.Raycast(b + Vector3.up * 0.5f, Vector3.down, out var h, 1.2f)) continue;
                Vector3 body = h.point;
                for (int deg = 0; deg < 360; deg += 5)
                {
                    Vector3 d = Quaternion.Euler(0f, deg, 0f) * Vector3.forward;
                    if (LaneFree(body, d, 54f, 64f) && LaneFree(body, d, 84f, 94f))
                    {
                        dir = d;
                        Log($"range lane: body {body:F1} dir {deg} deg");
                        return body;
                    }
                }
            }
            dir = Vector3.forward;
            Log("WARN: no free 95 m lane found, using the default spot");
            return RangeBodies[0];
        }

        private static bool LaneFree(Vector3 body, Vector3 dir, float from, float to)
        {
            int mask = ~((1 << 8) | (1 << 9) | (1 << 2) | (1 << 13));
            for (float d = from; d <= to; d += 0.5f)
            {
                Vector3 p = body + dir * d;
                if (!Physics.Raycast(p + Vector3.up * 0.6f, Vector3.down, out var h, 1.2f, mask, QueryTriggerInteraction.Ignore)) return false;
                if (Mathf.Abs(h.point.y - body.y) > 0.12f || h.normal.y < 0.97f) return false;
                if (Physics.CheckBox(h.point + Vector3.up * 0.2f, new Vector3(0.3f, 0.14f, 0.3f), Quaternion.LookRotation(dir), mask, QueryTriggerInteraction.Ignore)) return false;
            }
            return true;
        }

        /// <summary>
        /// Steer the parked car again, put it a few metres inside the expected limit on the lane and drive it slowly
        /// straight out until the radio stops it; reports where it stopped (flat distance from the driver's body).
        /// </summary>
        private IEnumerator MeasureRange(NetPlayer me, MatchManager mm, float expect, Action<float> result)
        {
            LocalPlayerController.TestSix = true; // steer again
            yield return new WaitForSeconds(2.2f);
            var car = RcCar.Peek(0);
            if (car == null || car.View != RcCar.ViewMode.Local) { Log($"FAIL: not driving ({RcLine(mm, me)})"); result(-1f); yield break; }
            Vector3 body = me.transform.position;
            Vector3 dir = Vector3.forward;
            for (int deg = 0; deg < 360; deg += 5)
            {
                Vector3 d = Quaternion.Euler(0f, deg, 0f) * Vector3.forward;
                if (LaneFree(body, d, expect - 6f, expect + 4f)) { dir = d; break; }
            }
            Vector3 start = body + dir * (expect - 4f);
            if (Physics.Raycast(start + Vector3.up * 0.6f, Vector3.down, out var h, 1.2f)) start = h.point;
            car.Respawn(start, Quaternion.LookRotation(dir).eulerAngles.y);
            yield return new WaitForSeconds(0.4f);
            LocalPlayerController.TestRcActive = true;
            LocalPlayerController.TestRcInput = new Vector2(0f, 0.4f);
            float still = 0f, far = 0f, t0 = Time.time;
            while (Time.time - t0 < 16f)
            {
                yield return null;
                Vector3 d = car.transform.position - body;
                d.y = 0f;
                far = Mathf.Max(far, d.magnitude);
                still = Mathf.Abs(car.Speed) < 0.02f && Time.time - t0 > 1f ? still + Time.deltaTime : 0f;
                if (still > 1.0f) break;
            }
            Vector3 fin = car.transform.position - body;
            fin.y = 0f;
            Log($"measure (limit {MatchManager.RcRangeOf(0):0}): started {expect - 4f:0} m out, stopped at {fin.magnitude:0.00} m (max {far:0.00}) after {Time.time - t0:0.0} s, speed {car.Speed:0.00}");
            if (!string.IsNullOrEmpty(_dir)) Screen($"08_range_{Mathf.RoundToInt(expect)}_carview");
            yield return new WaitForSeconds(0.3f);
            LocalPlayerController.TestRcInput = Vector2.zero;
            LocalPlayerController.TestRcActive = false;
            result(fin.magnitude);
            LocalPlayerController.TestSix = true; // park
            yield return new WaitForSeconds(1.8f);
        }

        // ================================================================ watcher

        private IEnumerator Watch()
        {
            yield return WaitSeeking();
            var mm = MatchManager.Instance;
            NetPlayer hider = null;
            bool pullShot = false, holdShot = false, swapShot = false, carShot = false, remoteShot = false;
            float wireSince = -1f;
            for (int k = 0; k < 1200; k++)
            {
                yield return new WaitForSeconds(0.1f);
                if (hider == null) foreach (var p in NetPlayer.All) if (p != null && p.IsHider) hider = p;
                if (hider == null) continue;
                var h = hider;
                if (k % 20 == 0)
                    Log($"watch: hider wire={h.WireCount.Value} pull={h.WirePull.Value.Spot} swap={h.AntennaSwapTime:0.0} hand={h.HandItem} wired={mm.RcWired.Value} taken={mm.WireTaken.Value.CountSet()} spots={WireSpots.Count}");
                if (!pullShot && h.WirePull.Value.Spot >= 0)
                {
                    pullShot = true;
                    Log("watch: the hider tears wire off a fence");
                    Light(h.transform.position + Vector3.up * 2.5f + h.transform.forward * -0.8f, 8f, 2f);
                    Light(h.transform.position + Vector3.up * 2.2f + h.transform.forward * 1.6f, 6f, 1.8f);
                    StartCoroutine(Side("w1_pull_front", () => h.transform.position + Vector3.up * 1.1f + h.transform.forward * 0.35f,
                        () => h.transform.rotation * new Vector3(0.75f, 0.25f, 1.9f), 9, 0.42f));
                    StartCoroutine(Side("w1_pull_side", () => h.transform.position + Vector3.up * 1.0f + h.transform.forward * 0.3f,
                        () => h.transform.rotation * new Vector3(-2.0f, 0.4f, -0.2f), 4, 0.9f));
                }
                if (h.HandItem != HotbarItem.Wire || h.WirePull.Value.Spot >= 0) wireSince = -1f;
                else if (wireSince < 0f) wireSince = Time.time;
                if (!holdShot && wireSince >= 0f && Time.time - wireSince > 0.9f)
                {
                    holdShot = true;
                    Log("watch: the hider holds the wire");
                    Light(h.transform.position + Vector3.up * 2.4f, 8f, 1.8f);
                    StartCoroutine(Side("w2_wire_in_hand", () => h.transform.position + Vector3.up * 0.95f,
                        () => h.transform.rotation * new Vector3(1.1f, 0.3f, 1.5f), 2, 0.3f));
                }
                if (!swapShot && h.AntennaSwapTime >= 0f)
                {
                    swapShot = true;
                    Log("watch: the hider swaps the antenna");
                    Light(h.transform.position + Vector3.up * 2.2f + h.transform.forward * 1.2f, 7f, 2.6f);
                    StartCoroutine(Side("w3_swap_front", () => h.transform.position + Vector3.up * 1.15f + h.transform.forward * 0.2f - h.transform.right * 0.1f,
                        () => h.transform.rotation * new Vector3(-0.75f, 0.3f, 1.55f), 9, 0.36f, 40f));
                }
                if (!remoteShot && mm.RcIsWired(0) && h.AntennaSwapTime < 0f)
                {
                    remoteShot = true;
                    StartCoroutine(Side("w4_remote_new_antenna", () => h.transform.position + Vector3.up * 1.2f + h.transform.forward * 0.25f,
                        () => h.transform.rotation * new Vector3(-0.25f, 0.3f, 0.8f), 1, 0.2f, 40f));
                }
                if (!carShot && mm.RcIsWired(0))
                {
                    var car = RcCar.Peek(0);
                    if (car != null && car.View != RcCar.ViewMode.Hidden)
                    {
                        carShot = true;
                        Log($"watch: car 0 wired, at {car.transform.position:F1}");
                        Light(car.transform.position + Vector3.up * 1.2f, 5f, 1.6f);
                        StartCoroutine(Side("w5_car_new_antenna", () => car.transform.position + Vector3.up * 0.15f, () => new Vector3(-0.6f, 0.35f, 0.65f), 1, 0.3f, 40f));
                    }
                }
            }
            Log("watch done");
        }
    }
}
