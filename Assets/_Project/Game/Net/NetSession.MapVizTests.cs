using System.Collections.Generic;
using HideAndSeek.Map;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Tests of the light / visibility update (host in a running round, e.g. host + one client with -autostart):
    /// <c>-testlightleak &lt;folder&gt;</c> - test lights on one level (lamp under a car, alarm-like glow, torch-like
    /// spot with and without shadows, a real map lamp) seen from the neighbouring level, each shot twice: "before"
    /// (level separation off, every light sees every layer) and "after"; <c>-testdarksight &lt;folder&gt;</c> - the
    /// player without a flashlight next to a car and down an aisle on every level, minimal sight off / on;
    /// <c>-testcctv &lt;folder&gt;</c> - CCTV close-ups (lit, dark with the LED watched / idle), the monitors
    /// cycling through 24 cameras and the feed of a new camera. Each test quits the game when done.
    /// </summary>
    public partial class NetSession
    {
        private readonly Dictionary<Light, int> _leakSaved = new();

        private void StartMapVizTests()
        {
            if (ArgOr("-testlightleak", null) is string a) StartCoroutine(LightLeakTest(a));
            if (ArgOr("-testdarksight", null) is string b) StartCoroutine(DarkSightTest(b));
            if (ArgOr("-testcctv", null) is string c) StartCoroutine(CctvTest(c));
        }

        private System.Collections.IEnumerator WaitForGarageRound()
        {
            var mm = MatchManager.Instance;
            while (mm == null || mm.Phase.Value == MatchPhase.Lobby || mm.Phase.Value == MatchPhase.Loading || NetPlayer.Local == null ||
                   LevelLighting.Instance == null || CarAlarms.Instance == null || CarAlarms.Instance.CarCount == 0)
            {
                mm = MatchManager.Instance;
                yield return new WaitForSeconds(0.25f);
            }
            yield return new WaitForSeconds(2f);
            var me = NetPlayer.Local;
            if (me.Flashlight != null) me.Flashlight.SetOn(false, false);
        }

        /// <summary>Old behaviour on (every light sees every level) or back to the level separation.</summary>
        private void SetLeak(bool leak)
        {
            var ll = LevelLighting.Instance;
            if (leak)
            {
                ll.enabled = false;
                foreach (var l in FindObjectsByType<Light>(FindObjectsInactive.Include))
                {
                    if (!_leakSaved.ContainsKey(l)) _leakSaved[l] = l.cullingMask;
                    l.cullingMask |= LevelLighting.AllLevels;
                }
            }
            else
            {
                foreach (var kv in _leakSaved) if (kv.Key != null) kv.Key.cullingMask = kv.Value;
                _leakSaved.Clear();
                ll.enabled = true;
                ll.ScanNow();
                ll.Apply();
            }
        }

        private static void Pose(NetPlayer me, Vector3 feet, Vector3 lookAt)
        {
            Vector3 to = lookAt - (feet + Vector3.up * 1.6f);
            me.Motor.Teleport(feet, YawTo(to), PitchTo(to));
            me.Motor.SetLook(YawTo(to), PitchTo(to));
        }

        private static Light TestLight(string name, LightType type, Vector3 pos, float range, float intensity, Color color, LightShadows shadows)
        {
            var l = new GameObject(name).AddComponent<Light>();
            l.type = type;
            l.transform.position = pos;
            l.range = range;
            l.intensity = intensity;
            l.color = color;
            l.shadows = shadows;
            l.renderMode = LightRenderMode.ForcePixel;
            l.spotAngle = 60f;
            return l;
        }

        /// <summary>Car on a level with a free standing spot beside it (feet, look target).</summary>
        private static bool CarSpot(int level, int skip, out Vector3 feet, out Vector3 target, out Vector3 carFloor)
        {
            var ca = CarAlarms.Instance;
            int found = 0;
            for (int i = 0; i < ca.CarCount; i++)
            {
                var (floor, right, fwd, half) = ca.CarFrame(i);
                if (LevelLighting.LevelAt(floor.y + 0.5f) != level || Mathf.Abs(floor.y + 3.4f * level) > 0.3f) continue;
                foreach (float b in new[] { 1f, -1f })
                {
                    Vector3 p = floor + right * (b * (half.x + 2.2f)) + Vector3.up * 0.05f;
                    if (Physics.CheckCapsule(p + Vector3.up * 0.5f, p + Vector3.up * 1.6f, 0.35f)) continue;
                    if (!Physics.Raycast(p + Vector3.up, Vector3.down, out var g, 1.5f) || Mathf.Abs(g.point.y - floor.y) > 0.3f) continue;
                    if (found++ < skip) break;
                    feet = p; target = floor + Vector3.up * 0.45f; carFloor = floor;
                    return true;
                }
            }
            feet = target = carFloor = Vector3.zero;
            return false;
        }

        private System.Collections.IEnumerator LeakPair(string dir, string name)
        {
            SetLeak(true);
            yield return new WaitForSeconds(0.25f);
            Shot(dir, name + "_before");
            yield return new WaitForSeconds(0.25f);
            SetLeak(false);
            yield return new WaitForSeconds(0.25f);
            Shot(dir, name + "_after");
            yield return new WaitForSeconds(0.25f);
        }

        private System.Collections.IEnumerator LightLeakTest(string dir)
        {
            yield return WaitForGarageRound();
            var me = NetPlayer.Local;
            if (DarkSight.Instance != null) DarkSight.Instance.Scale = 0f;
            // 1. lamp hanging under the slab of level -1, right below a parked car of level 0
            if (CarSpot(0, 3, out var feet, out var target, out var car))
            {
                var lamp = TestLight("LeakLampBelow", LightType.Point, new Vector3(car.x, -0.7f, car.z), 7f, 1.2f, new Color(1f, 0.86f, 0.66f), LightShadows.None);
                Pose(me, feet, target);
                yield return new WaitForSeconds(0.8f);
                yield return LeakPair(dir, "1_lamp_under_car");
                Destroy(lamp.gameObject);
            }
            // 2. alarm-like orange glow on level 0, seen on the floor of level -1 underneath
            Vector3 spot = new Vector3(-20f, 0f, -28.25f);
            var glow = TestLight("LeakGlowAbove", LightType.Point, spot + Vector3.up * 1.8f, 7f, 2.2f, new Color(1f, 0.55f, 0.1f), LightShadows.None);
            Pose(me, spot + new Vector3(-4.5f, -3.4f, 0f), spot + new Vector3(0f, -3.4f, 0f));
            yield return new WaitForSeconds(0.8f);
            yield return LeakPair(dir, "2_glow_above");
            Destroy(glow.gameObject);
            // 3. torch-like spot pointing down on level 0, without and with shadows
            foreach (var sh in new[] { LightShadows.None, LightShadows.Soft })
            {
                var torch = TestLight("LeakTorchAbove", LightType.Spot, spot + Vector3.up * 1.6f, 30f, 3.8f, new Color(1f, 0.95f, 0.85f), sh);
                torch.transform.rotation = Quaternion.LookRotation(Vector3.down + Vector3.right * 0.3f);
                yield return new WaitForSeconds(0.5f);
                yield return LeakPair(dir, "3_torch_above_" + (sh == LightShadows.None ? "noshadow" : "shadow"));
                Destroy(torch.gameObject);
            }
            // 4. a real map lamp of level -1 (steady for the test), seen from level -2 below it
            foreach (var l in FindObjectsByType<Light>(FindObjectsInactive.Exclude))
            {
                if (l.transform.parent == null || !l.transform.parent.name.StartsWith("FlickeringLamp_L2_")) continue;
                var fl = l.GetComponent<FlickeringLight>();
                if (fl != null) fl.enabled = false;
                l.intensity = 0.9f;
                Vector3 lp = l.transform.position;
                Pose(me, new Vector3(lp.x - 4f, -6.8f, lp.z + 0.5f), new Vector3(lp.x, -6.8f, lp.z));
                yield return new WaitForSeconds(0.8f);
                yield return LeakPair(dir, "4_map_lamp_above");
                Pose(me, new Vector3(lp.x - 3f, 0f, lp.z + 0.5f), new Vector3(lp.x, 0.4f, lp.z));
                yield return new WaitForSeconds(0.8f);
                yield return LeakPair(dir, "5_map_lamp_below");
                if (fl != null) fl.enabled = true;
                break;
            }
            Debug.Log("[LeakTest] done");
            yield return new WaitForSeconds(1f);
            Application.Quit();
        }

        private System.Collections.IEnumerator DarkSightTest(string dir)
        {
            yield return WaitForGarageRound();
            var me = NetPlayer.Local;
            var ds = DarkSight.Instance;
            for (int lv = 0; lv < 3; lv++)
            {
                if (!CarSpot(lv, 5, out var feet, out var target, out _)) continue;
                Pose(me, feet, target);
                yield return new WaitForSeconds(0.8f);
                foreach (float s in new[] { 0f, 0.6f, 1f })
                {
                    ds.Scale = s;
                    yield return new WaitForSeconds(0.3f);
                    Shot(dir, $"L{lv}_car_sight{s * 10:00}");
                    yield return new WaitForSeconds(0.3f);
                }
                // down the aisle: only the first metres should show
                Vector3 a = new Vector3(-30f, -3.4f * lv, lv == 1 ? 11.75f : -11.75f);
                Pose(me, a, a + new Vector3(20f, 0.6f, 0f));
                yield return new WaitForSeconds(0.8f);
                foreach (float s in new[] { 0f, 0.6f, 1f })
                {
                    ds.Scale = s;
                    yield return new WaitForSeconds(0.3f);
                    Shot(dir, $"L{lv}_aisle_sight{s * 10:00}");
                    yield return new WaitForSeconds(0.3f);
                }
                Debug.Log($"[DarkSight] level {lv}: on={ds.IsOn} at {feet}");
            }
            ds.Scale = 1f;
            Debug.Log("[DarkSight] done");
            yield return new WaitForSeconds(1f);
            Application.Quit();
        }

        /// <summary>Furthest point up to <paramref name="max"/> m from <paramref name="from"/> along dir with a clear line back to it.</summary>
        private static Vector3 ClearView(Vector3 from, Vector3 dir, float max)
        {
            dir.Normalize();
            float d = max;
            if (Physics.Raycast(from + dir * 0.5f, dir, out var hit, max - 0.5f, ~0, QueryTriggerInteraction.Ignore)) d = Mathf.Max(1f, hit.distance + 0.5f - 0.6f);
            return from + dir * d;
        }

        /// <summary>Close-up under a torch that casts no shadows.</summary>
        private static void FlatLitShot(string dir, string name, Vector3 pos, Vector3 at)
        {
            var lg = TestLight("TestFlatLight", LightType.Spot, pos + Vector3.up * 0.15f, 25f, 2.6f, new Color(1f, 0.95f, 0.85f), LightShadows.None);
            lg.spotAngle = 70f;
            lg.transform.LookAt(at);
            CamShot(dir, name, pos, at);
            Destroy(lg.gameObject);
        }

        private System.Collections.IEnumerator CctvTest(string dir)
        {
            yield return WaitForGarageRound();
            while (SecurityRooms.Instance == null) yield return null;
            var sr = SecurityRooms.Instance;
            var me = NetPlayer.Local;
            Debug.Log($"[CctvTest] cameras per level: {sr.CameraCount(0)}, {sr.CameraCount(1)}, {sr.CameraCount(2)}");
            // close-ups of a few cameras, lit by a torch (front three-quarter, side, from below), then in the dark: the LED
            // of a camera on a monitor while watched (blink on) and the standby blip of one that is not shown
            foreach (var (lv, idx) in new[] { (0, 9), (1, 22), (2, 14) })
            {
                Vector3 cp = sr.CameraPosition(lv, idx);
                var rot = sr.CameraRotation(lv, idx);
                Vector3 f = rot * Vector3.forward, r = rot * Vector3.right;
                Pose(me, new Vector3(cp.x, -3.4f * lv, cp.z) + f * 3f + r * 1.5f, cp);
                yield return new WaitForSeconds(0.5f);
                LitShot(dir, $"cam_L{lv}_{idx}_a_front", cp + f * 0.75f + r * 0.35f - Vector3.up * 0.2f, cp - f * 0.15f);
                LitShot(dir, $"cam_L{lv}_{idx}_b_side", cp + r * 0.85f - f * 0.2f + Vector3.up * 0.05f, cp - f * 0.15f + Vector3.up * 0.15f);
                LitShot(dir, $"cam_L{lv}_{idx}_c_below", cp + f * 1.2f - r * 0.6f - Vector3.up * 1.3f, cp + Vector3.up * 0.25f);
                // same front view with a shadowless torch: what hangs under the camera in the lit shots is its shadow on the ceiling
                FlatLitShot(dir, $"cam_L{lv}_{idx}_e_front_noshadow", cp + f * 0.75f + r * 0.35f - Vector3.up * 0.2f, cp - f * 0.15f);
                while (!sr.IdleBlipOn(lv, idx)) yield return null;
                CamShot(dir, $"cam_L{lv}_{idx}_d_dark_idle_blip", cp + f * 1.6f + r * 0.7f - Vector3.up * 0.6f, cp);
                yield return null;
            }
            SecurityRooms.TestWatched = true;
            for (int slot = 0; slot < 2; slot++)
            {
                var (wl, wi) = sr.SlotCamera(slot);
                if (wl < 0) continue;
                Vector3 cp = sr.CameraPosition(wl, wi);
                var rot = sr.CameraRotation(wl, wi);
                Vector3 f = rot * Vector3.forward, r = rot * Vector3.right;
                Pose(me, new Vector3(cp.x, -3.4f * wl, cp.z) + f * 3f + r * 1.5f, cp);
                yield return new WaitForSeconds(0.6f);
                while (Mathf.Repeat(Time.time * 1.25f, 1f) > 0.2f || Mathf.Repeat(Time.time * 1.25f, 1f) < 0.05f) yield return null;
                CamShot(dir, $"cam_watched_{slot}_on_near", cp + f * 1.6f + r * 0.7f - Vector3.up * 0.6f, cp);
                CamShot(dir, $"cam_watched_{slot}_on_far", ClearView(cp, f + r * 0.2f - Vector3.up * 0.08f, 9f), cp);
                while (Mathf.Repeat(Time.time * 1.25f, 1f) < 0.6f) yield return null;
                CamShot(dir, $"cam_watched_{slot}_off_near", cp + f * 1.6f + r * 0.7f - Vector3.up * 0.6f, cp);
                Debug.Log($"[CctvTest] watched camera L{wl} #{wi} shots");
            }
            SecurityRooms.TestWatched = false;
            // monitors: in the room of level 0, watch them cycle, then a new camera (RAMPA 1) catching motion
            Vector3 p = SecurityRooms.RoomFloorPoint(0);
            me.TeleportRpc(new Vector3(p.x, p.y + 0.1f, p.z + 0.9f), 0f);
            yield return new WaitForSeconds(1f);
            me.RequestSecurityFloorRpc(0);
            yield return new WaitForSeconds(1f);
            Debug.Log($"[CctvTest] monitors t0: {sr.MonitorCameras()}");
            Shot(dir, "monitors_t0");
            for (int i = 1; i <= 4; i++)
            {
                yield return new WaitForSeconds(10f);
                Debug.Log($"[CctvTest] monitors t{i * 10}: {sr.MonitorCameras()}");
            }
            Shot(dir, "monitors_t40");
            sr.ForceMotion(0, 22);
            yield return new WaitForSeconds(1.5f);
            Debug.Log($"[CctvTest] after motion on RAMPA 1: {sr.MonitorCameras()}");
            for (int s = 0; s < 6; s++) sr.DumpFeed(s, System.IO.Path.Combine(dir, $"feed_{s}.png"));
            Debug.Log("[CctvTest] done");
            yield return new WaitForSeconds(1f);
            Application.Quit();
        }
    }
}
