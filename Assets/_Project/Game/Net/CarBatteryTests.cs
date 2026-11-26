using System;
using System.Collections;
using System.IO;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using HideAndSeek.Game.UI;
using HideAndSeek.Map;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Test of the car batteries / camera power (own component, started from the command line):
    ///  -testbattery &lt;dir&gt; (host -forcehider -autostart -hidetime 3; a seeker client is optional - without one the
    ///      test holds the hiding phase open): the round's bonnets (active, working, charge), the dark security rooms,
    ///      a piece of wire, the E prompt at a bonnet, the lockpick (one lost try = one wire less, then a won one), the
    ///      bonnet lifted (timed against the dust), the battery taken, carried to the security room of its level,
    ///      connected, the switch on, the charge draining over time, the switch off, a dead battery connected, the
    ///      battery dropped and picked up again with its charge. Logs [BatteryTest]; frames rendered from Camera.main
    ///      (+ the hands camera) into a RenderTexture (works with -batchmode). Quits at the end.
    /// </summary>
    public class CarBatteryTests : MonoBehaviour
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
            if (!Has("-testbattery")) return;
            _dir = ArgOr("-testbattery", "");
            var go = new GameObject("[CarBatteryTests]");
            DontDestroyOnLoad(go);
            var t = go.AddComponent<CarBatteryTests>();
            t.StartCoroutine(t.Run());
        }

        private static void Log(string s) => Debug.Log("[BatteryTest] " + s);

        private static string Pf(bool ok)
        {
            if (!ok) _fails++;
            return ok ? "PASS" : "FAIL";
        }

        private static string PromptNow()
        {
            var lpc = LocalPlayerController.Instance;
            return lpc != null ? $"{lpc.Prompt} '{(lpc.Prompt != PromptKind.None ? lpc.PromptText : "")}'" : "-"; // the text is not cleared with the prompt
        }

        /// <summary>Why the expected prompt may be missing: input, locks, role, distance / angle to the point, what a ray hits.</summary>
        private static void Why(NetPlayer me, Vector3 point, string what)
        {
            var lpc = LocalPlayerController.Instance;
            var mm = MatchManager.Instance;
            Vector3 eye = me.EyePosition;
            Vector3 fwd = me.Camera != null ? me.Camera.transform.forward : me.transform.forward;
            string hit = "nothing";
            int mask = ~((1 << PlayerAvatarBinder.LocalBodyLayer) | (1 << 9) | GameLayers.RagdollMask); // as LocalPlayerController.InView: not the own body
            if (Physics.Raycast(eye, (point - eye).normalized, out var h, (point - eye).magnitude, mask, QueryTriggerInteraction.Ignore))
                hit = $"{h.collider.name} at {h.distance:0.00} m (parent {(h.collider.transform.parent != null ? h.collider.transform.parent.name : "-")})";
            Log($"  diag {what}: prompt {PromptNow()} kb={(Keyboard.current != null)} mouse={(Mouse.current != null)} lpc={(lpc != null && lpc.isActiveAndEnabled)} " +
                $"menu={LocalPlayerController.MenuOpen} phase={mm.Phase.Value} intro={mm.IntroHoldsStart} hider={me.IsAliveHider} seeker={me.IsSeeker} busy={me.Busy.Value} " +
                $"seat={me.Seat.Value} prone={me.Motor.IsProne} work={me.HoodWork.Value.Mode} wire={me.WireCount.Value} battery={me.CarBattery.Value} " +
                $"feet={me.transform.position:F2} eye={eye:F2} point={point:F2} dist={Vector3.Distance(eye, point):0.00} angle={Vector3.Angle(fwd, point - eye):0.0} ray hits {hit}");
        }

        private static string Power(MatchManager mm)
        {
            var s = mm.CamPower.Value;
            var sb = new System.Text.StringBuilder();
            for (int r = 0; r < 3; r++)
                sb.Append($" room{SecurityRooms.LevelName(r)}[bat={(s.HasBattery(r) ? 1 : 0)} on={(s.IsOn(r) ? 1 : 0)} {s.Percent(r)}% powered={(s.Powered(r) ? 1 : 0)}]");
            return sb.ToString();
        }

        private static Light _light;

        private static void LightAt(Vector3 pos, float range = 9f, float intensity = 2f)
        {
            if (_light == null)
            {
                _light = new GameObject("BatteryTestLight").AddComponent<Light>();
                _light.type = LightType.Point;
                _light.shadows = LightShadows.Soft;
            }
            _light.range = range;
            _light.intensity = intensity;
            _light.transform.position = pos;
        }

        /// <summary>Renders the player's view (Camera.main + the first-person hands camera) into a PNG (no ScreenCapture: -batchmode).</summary>
        private static IEnumerator Shot(string name)
        {
            if (string.IsNullOrEmpty(_dir)) yield break;
            yield return new WaitForEndOfFrame();
            var me0 = NetPlayer.Local;
            var cam = me0 != null && me0.Camera != null ? me0.Camera : Camera.main; // the player's own camera (Camera.main can be null in -batchmode)
            if (cam == null) { Log($"shot {name}: no player camera"); yield break; }
            const int w = 1280, h = 720;
            Directory.CreateDirectory(_dir);
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 2 };
            var prevActive = RenderTexture.active;
            var old = cam.targetTexture;
            try
            {
                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = old;
                var me = NetPlayer.Local;
                var hands = me != null ? (me.ViewModel as FirstPersonViewModel)?.ViewModelCamera : null;
                if (hands != null && hands.gameObject.activeInHierarchy)
                {
                    var oldH = hands.targetTexture;
                    hands.targetTexture = rt;
                    hands.Render();
                    hands.targetTexture = oldH;
                }
                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                File.WriteAllBytes(Path.Combine(_dir, name + ".png"), tex.EncodeToPNG());
                Destroy(tex);
            }
            finally
            {
                cam.targetTexture = old;
                RenderTexture.active = prevActive;
                rt.Release();
                Destroy(rt);
            }
        }

        /// <summary>Renders a free camera looking at <paramref name="focus"/> from <paramref name="from"/>.</summary>
        private static IEnumerator Side(string name, Vector3 from, Vector3 focus, float fov = 50f)
        {
            if (string.IsNullOrEmpty(_dir)) yield break;
            yield return new WaitForEndOfFrame();
            const int w = 1280, h = 720;
            Directory.CreateDirectory(_dir);
            var go = new GameObject("BatteryTestSideCam");
            var cam = go.AddComponent<Camera>();
            cam.enabled = false;
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.03f;
            cam.cullingMask = ~(1 << 9);
            go.transform.position = from;
            go.transform.LookAt(focus);
            var rt = new RenderTexture(w, h, 24) { antiAliasing = 2 };
            var prev = RenderTexture.active;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(_dir, name + ".png"), tex.EncodeToPNG());
            RenderTexture.active = prev;
            Destroy(tex);
            rt.Release();
            Destroy(rt);
            Destroy(go);
        }

        /// <summary>Teleports to <paramref name="feet"/> and looks at <paramref name="target"/>.</summary>
        private static void StandLooking(NetPlayer me, Vector3 feet, Vector3 target)
        {
            me.Motor.Teleport(feet, 0f, 0f);
            Vector3 eye = feet + Vector3.up * (me.EyePosition.y - me.transform.position.y);
            Vector3 d = target - eye;
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            float pitch = Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg; // PlayerMotor: + = look up
            me.Motor.Teleport(feet, yaw, pitch);
        }

        private static IEnumerator Until(Func<bool> cond, float timeout)
        {
            float t0 = Time.time;
            while (!cond() && Time.time - t0 < timeout) yield return null;
        }

        private IEnumerator Run()
        {
            // ---- wait for the round, the map's bonnets and the security rooms
            while (MatchManager.Instance == null || NetPlayer.Local == null) yield return null;
            var mm = MatchManager.Instance;
            while (mm.Phase.Value != MatchPhase.Hiding && mm.Phase.Value != MatchPhase.Seeking) yield return null;
            var me = NetPlayer.Local;
            // -batchmode has no input devices: LocalPlayerController skips its prompts while Keyboard.current / Mouse.current
            // are null. Virtual devices (never pressed) let the real prompt / E path run.
            if (Keyboard.current == null) InputSystem.AddDevice<Keyboard>();
            if (Mouse.current == null) InputSystem.AddDevice<Mouse>();
            Log($"input devices: keyboard {(Keyboard.current != null)} mouse {(Mouse.current != null)} batch {Application.isBatchMode}");
            if (!mm.IsServer || !me.IsHider) { Log("need the host as a hider (-forcehider)"); Quit(); yield break; }
            bool hold = true;
            StartCoroutine(HoldRound(mm, () => hold));
            yield return Until(() => mm.CarHoods.Value.Active != 0 && SecurityRooms.Instance != null && SecurityRooms.Instance.Ready, 20f);
            // the round intro holds the hiders still (no prompts, LocalPlayerController locked) - wait for the round proper
            yield return Until(() => mm.Phase.Value == MatchPhase.Seeking || !mm.IntroHoldsStart, 60f);
            yield return Until(() => LocalPlayerController.Instance != null && me.Camera != null && me.Camera.isActiveAndEnabled, 10f);
            yield return new WaitForSeconds(1.5f);
            Log($"ready: phase {mm.Phase.Value} intro holds {mm.IntroHoldsStart} camera {(me.Camera != null ? me.Camera.name : "none")} menu {LocalPlayerController.MenuOpen}");
            var all = CarHood.All;
            var bits = mm.CarHoods.Value;
            Log($"map hoods: {all.Count}, active mask {bits.Active:X}, phase {mm.Phase.Value}");
            int[] active = new int[3], working = new int[3];
            CarHood pick = null;
            foreach (var h in all)
            {
                bool act = HoodBits.Has(bits.Active, h.Index);
                int hc = mm.ServerHoodCharge(h.Index);
                if (act) { active[h.Level]++; if (hc > 0) working[h.Level]++; }
                Log($"  hood #{h.Index} L{h.Level} {h.name} dust {h.Dust:0.0} active={act} charge={hc}% hoodChild={(h.Hood != null)} battery={(h.Battery != null)} pins={BatteryTuning.PinsFor(h.Dust)} lift~{MatchManager.HoodTime(h.Dust):0.0}s");
                if (act && hc > 0 && (pick == null || (pick.Level != 0 && h.Level == 0) || (pick.Level == h.Level && h.Dust > pick.Dust))) pick = h;
            }
            for (int lv = 0; lv < 3; lv++) Log($"level {lv}: active {active[lv]} (3..5) working {working[lv]} (>=3 or all) -> {Pf(active[lv] >= Mathf.Min(3, CountLevel(all, lv)) && active[lv] <= 5 && working[lv] >= Mathf.Min(3, active[lv]))}");
            Log($"start power:{Power(mm)} -> {Pf(!mm.CamPower.Value.AnyPowered)}");

            // ---- the dark security room
            var room0 = SecurityRooms.RoomFloorPoint(0);
            LightAt(new Vector3(72f, 2.4f, 56.5f), 10f, 1.2f);
            StandLooking(me, new Vector3(72.3f, 0f, 56.8f), new Vector3(72.3f, 1.7f, 59.4f));
            yield return new WaitForSeconds(1f);
            yield return Shot("01_room_no_power");
            if (pick == null) { Log("FAIL: no active working hood"); _fails++; Finish(); yield break; }

            // ---- at the bonnet without wire: prompt, then with wire
            Vector3 fwd = pick.transform.forward;
            fwd.y = 0f;
            fwd.Normalize();
            Vector3 stand = pick.FrontWorld + fwd * 1.05f;
            stand.y = pick.transform.position.y;
            LightAt(pick.FrontWorld + fwd * 1.8f + Vector3.up * 1.8f, 9f, 2.2f);
            me.WireCount.Value = 0;
            StandLooking(me, stand, pick.FrontWorld);
            yield return new WaitForSeconds(1f);
            Log($"at {pick.name} (#{pick.Index}, L{pick.Level}, dust {pick.Dust:0.0}) without wire: prompt {PromptNow()} -> {Pf(LocalPlayerController.Instance.Prompt == PromptKind.CarHood && LocalPlayerController.Instance.PromptText.Contains("drutu"))}");
            me.WireCount.Value = 2;
            yield return new WaitForSeconds(0.5f);
            Log($"with 2 wires: prompt {PromptNow()}");
            if (LocalPlayerController.Instance.Prompt != PromptKind.CarHood) Why(me, pick.FrontWorld, "bonnet");
            yield return Shot("02_hood_prompt");
            yield return Side("02_hood_closed_side", pick.FrontWorld + fwd * 2.2f + pick.transform.right * 1.6f + Vector3.up * 1.2f, pick.FrontWorld - fwd * 0.4f);

            // ---- lockpick: one lost try, then a won one
            LocalPlayerController.TestPressE = true;
            yield return Until(() => LockpickUi.Active, 3f);
            Log($"lockpick open: {LockpickUi.Active} work={me.HoodWork.Value.Mode} menu={LocalPlayerController.MenuOpen} -> {Pf(LockpickUi.Active && me.HoodWork.Value.Mode == HoodWorkMode.Lockpick)}");
            double start0 = me.HoodWork.Value.Start;
            int losses0 = LockpickUi.Losses;
            LockpickUi.TestFailNext = true;
            yield return Until(() => LockpickUi.Losses > losses0 && me.HoodWork.Value.Start != start0, 4f);
            yield return new WaitForSeconds(0.3f);
            Log($"lost try: wire {me.WireCount.Value} (expected 1), still picking={me.HoodWork.Value.Mode == HoodWorkMode.Lockpick}, ui={LockpickUi.Active} -> {Pf(me.WireCount.Value == 1 && LockpickUi.Active)}");
            float pickT0 = Time.time;
            LockpickUi.TestAuto = true;
            yield return Until(() => me.HoodWork.Value.Mode == HoodWorkMode.Opening, 15f);
            LockpickUi.TestAuto = false;
            var work = me.HoodWork.Value;
            Log($"lockpick won after {Time.time - pickT0:0.0} s: mode={work.Mode} lift {work.Duration:0.0} s (dust {pick.Dust:0.0}, base {MatchManager.HoodTime(pick.Dust):0.0}) wire {me.WireCount.Value} menu={LocalPlayerController.MenuOpen} -> {Pf(work.Mode == HoodWorkMode.Opening && work.Duration >= 3f && work.Duration <= 7f && !LocalPlayerController.MenuOpen)}");

            // ---- the bonnet creaks up
            float liftT0 = Time.time;
            for (int k = 1; k <= 3; k++)
            {
                yield return Until(() => Time.time - liftT0 >= work.Duration * k / 4f, 10f);
                Log($"lift {k}/4: angle {pick.Angle:0.0} deg");
                yield return Side($"03_lift_{k}", pick.FrontWorld + fwd * 2.2f + pick.transform.right * 1.6f + Vector3.up * 1.2f, pick.FrontWorld - fwd * 0.4f);
            }
            yield return Until(() => HoodBits.Has(mm.CarHoods.Value.Open, pick.Index), 10f);
            float took = Time.time - liftT0;
            yield return new WaitForSeconds(0.8f);
            Log($"bonnet open after {took:0.0} s (planned {work.Duration:0.0}): angle {pick.Angle:0.0}/{pick.OpenAngle:0} -> {Pf(HoodBits.Has(mm.CarHoods.Value.Open, pick.Index) && Mathf.Abs(took - work.Duration) < 0.8f && pick.Angle > pick.OpenAngle - 1f)}");
            yield return Side("04_hood_open_side", pick.FrontWorld + fwd * 2.2f + pick.transform.right * 1.6f + Vector3.up * 1.2f, pick.FrontWorld - fwd * 0.4f);
            yield return Side("04_hood_open_top", pick.FrontWorld + fwd * 1.3f + Vector3.up * 1.9f, pick.BatteryWorld);

            // ---- the battery
            StandLooking(me, stand, pick.BatteryWorld);
            yield return new WaitForSeconds(0.8f);
            Log($"battery prompt {PromptNow()} -> {Pf(LocalPlayerController.Instance.Prompt == PromptKind.CarBatteryTake)}");
            if (LocalPlayerController.Instance.Prompt != PromptKind.CarBatteryTake) Why(me, pick.BatteryWorld, "battery");
            yield return Shot("05_battery_prompt");
            int expect = mm.ServerHoodCharge(pick.Index);
            LocalPlayerController.TestPressE = true;
            yield return Until(() => me.HasCarBattery, 3f);
            yield return new WaitForSeconds(0.6f);
            Log($"took the battery: carried={me.CarBattery.Value} (charge {me.CarBatteryCharge}%, expected {expect}%) slot={Inventory.SlotOf(me, HotbarItem.CarBattery) + 1} speed x{me.BatterySpeedScale:0.00} battery shown in bay={pick.Battery.gameObject.activeSelf} -> {Pf(me.HasCarBattery && me.CarBatteryCharge == expect && !pick.Battery.gameObject.activeSelf)}");
            Hotbar.TestSelect = Inventory.SlotOf(me, HotbarItem.CarBattery);
            yield return new WaitForSeconds(1f);
            Log($"in hand: hint '{Hotbar.Hint(me, HotbarItem.CarBattery)}'");
            yield return Shot("06_battery_in_hand");

            // ---- drop it and pick it up again (the charge goes with it)
            int charge = me.CarBatteryCharge;
            mm.ServerDropSlot(me, (byte)Mathf.Max(0, Inventory.SlotOf(me, HotbarItem.CarBattery)));
            yield return new WaitForSeconds(1.5f);
            int dropId = -1;
            byte dropExtra = 0;
            for (int i = 0; i < mm.Drops.Count; i++)
                if (mm.Drops[i].Kind == HotbarItem.CarBattery) { dropId = mm.Drops[i].Id; dropExtra = mm.Drops[i].Extra; }
            Log($"dropped: drop id {dropId} extra {dropExtra} -> {Pf(dropId >= 0 && dropExtra == charge && !me.HasCarBattery)}");
            if (dropId >= 0) me.RequestPickupDropRpc(dropId);
            yield return Until(() => me.HasCarBattery, 3f);
            Log($"picked up again: charge {me.CarBatteryCharge}% -> {Pf(me.CarBatteryCharge == charge)}");

            // ---- the security room of level 0 (tray + switch)
            int room = 0;
            float y0 = room0.y;
            Vector3 slot = SecurityRooms.PowerSlotPoint(room), sw = SecurityRooms.PowerSwitchPoint(room);
            LightAt(new Vector3(76.4f, y0 + 2.3f, 57.9f), 7f, 1.6f);
            Vector3 roomStand = new Vector3(76.95f, y0, 58.0f);
            StandLooking(me, roomStand, slot);
            yield return new WaitForSeconds(1f);
            Log($"tray prompt {PromptNow()} -> {Pf(LocalPlayerController.Instance.Prompt == PromptKind.CamBattery)}");
            if (LocalPlayerController.Instance.Prompt != PromptKind.CamBattery) Why(me, slot, "tray");
            yield return Shot("07_tray_empty");
            LocalPlayerController.TestPressE = true;
            yield return Until(() => mm.CamPower.Value.HasBattery(room), 3f);
            yield return new WaitForSeconds(0.6f);
            Log($"connected:{Power(mm)} carried={me.CarBattery.Value} -> {Pf(mm.CamPower.Value.HasBattery(room) && !me.HasCarBattery && mm.CamPower.Value.Percent(room) == charge && !mm.CamPower.Value.Powered(room))}");
            yield return Shot("08_tray_connected");
            StandLooking(me, roomStand, sw);
            yield return new WaitForSeconds(0.8f);
            Log($"switch prompt {PromptNow()} -> {Pf(LocalPlayerController.Instance.Prompt == PromptKind.CamSwitch)}");
            if (LocalPlayerController.Instance.Prompt != PromptKind.CamSwitch) Why(me, sw, "switch");
            LocalPlayerController.TestPressE = true;
            yield return Until(() => mm.CamPower.Value.IsOn(room), 3f);
            yield return new WaitForSeconds(0.8f);
            Log($"switched on:{Power(mm)} watched={mm.SecurityWatched.Value} -> {Pf(mm.CamPower.Value.Powered(room))}");
            yield return Shot("09_switch_on");
            StandLooking(me, new Vector3(72.3f, y0, 56.8f), new Vector3(72.3f, y0 + 1.7f, 59.4f));
            yield return new WaitForSeconds(1.5f);
            yield return Shot("10_monitors_on");
            Log($"monitors on: watched={mm.SecurityWatched.Value} cams '{SecurityRooms.Instance.MonitorCameras()}' -> {Pf(mm.SecurityWatched.Value)}");

            // ---- drain while on
            float c0 = mm.CamPower.Value.Percent(room);
            for (int k = 1; k <= 4; k++)
            {
                yield return new WaitForSeconds(5f);
                Log($"drain t={k * 5}s:{Power(mm)}");
            }
            float c1 = mm.CamPower.Value.Percent(room);
            float rate = (c0 - c1) / 20f;
            Log($"drain {c0}% -> {c1}% in 20 s = {rate:0.00} %/s (expected {100f / BatteryTuning.FullLasts:0.00}, full battery lasts {BatteryTuning.FullLasts / 60f:0.0} min) -> {Pf(rate > 0.15f && rate < 0.5f)}");

            // ---- switch off: no drain, screens dark
            StandLooking(me, roomStand, sw);
            yield return new WaitForSeconds(0.6f);
            LocalPlayerController.TestPressE = true;
            yield return Until(() => !mm.CamPower.Value.IsOn(room), 3f);
            float off0 = mm.CamPower.Value.Percent(room);
            yield return new WaitForSeconds(6f);
            Log($"switched off:{Power(mm)} -> {Pf(!mm.CamPower.Value.Powered(room) && mm.CamPower.Value.Percent(room) == off0)}");
            StandLooking(me, new Vector3(72.3f, y0, 56.8f), new Vector3(72.3f, y0 + 1.7f, 59.4f));
            yield return new WaitForSeconds(1f);
            yield return Shot("11_monitors_off");

            // ---- swap: take the battery out, connect a dead one, switch on -> dark
            StandLooking(me, roomStand, slot);
            yield return new WaitForSeconds(0.8f);
            LocalPlayerController.TestPressE = true;
            yield return Until(() => me.HasCarBattery, 3f);
            int keep = me.CarBatteryCharge;
            Log($"taken out:{Power(mm)} carried {keep}% -> {Pf(me.HasCarBattery && !mm.CamPower.Value.HasBattery(room))}");
            me.CarBattery.Value = 1; // a dead battery (as from a dead car)
            yield return new WaitForSeconds(0.3f);
            LocalPlayerController.TestPressE = true;
            yield return Until(() => mm.CamPower.Value.HasBattery(room), 3f);
            StandLooking(me, roomStand, sw);
            yield return new WaitForSeconds(0.6f);
            LocalPlayerController.TestPressE = true;
            yield return Until(() => mm.CamPower.Value.IsOn(room), 3f);
            yield return new WaitForSeconds(0.6f);
            Log($"dead battery connected + on:{Power(mm)} -> {Pf(mm.CamPower.Value.HasBattery(room) && mm.CamPower.Value.IsOn(room) && !mm.CamPower.Value.Powered(room))}");
            StandLooking(me, new Vector3(72.3f, y0, 56.8f), new Vector3(72.3f, y0 + 1.7f, 59.4f));
            yield return new WaitForSeconds(1f);
            yield return Shot("12_monitors_dead_battery");

            // ---- an empty battery runs out while on: put the good one back with 1 % left
            StandLooking(me, roomStand, slot);
            yield return new WaitForSeconds(0.6f);
            LocalPlayerController.TestPressE = true;
            yield return Until(() => me.HasCarBattery, 3f);
            me.CarBattery.Value = 2; // 1 % left
            yield return new WaitForSeconds(0.3f);
            LocalPlayerController.TestPressE = true;
            yield return Until(() => mm.CamPower.Value.HasBattery(room), 3f);
            Log($"1% battery, switch on:{Power(mm)}");
            yield return Until(() => mm.CamPower.Value.Percent(room) == 0, 8f);
            Log($"ran out:{Power(mm)} -> {Pf(!mm.CamPower.Value.Powered(room) && mm.CamPower.Value.HasBattery(room))}");
            Log($"done, {_fails} failed check(s), keep {keep}%");
            hold = false;
            Finish();
        }

        private static int CountLevel(System.Collections.Generic.IReadOnlyList<CarHood> all, int lv)
        {
            int n = 0;
            foreach (var h in all) if (h.Level == lv) n++;
            return n;
        }

        /// <summary>Without a seeker the round would end at once: keep the hiding phase open while the test runs.</summary>
        private static IEnumerator HoldRound(MatchManager mm, Func<bool> hold)
        {
            while (hold())
            {
                bool seeker = false;
                foreach (var p in NetPlayer.All) if (p != null && p.IsSeeker) seeker = true;
                if (!seeker && mm.Phase.Value == MatchPhase.Hiding && mm.PhaseEnd.Value - mm.Now < 30d) mm.PhaseEnd.Value = mm.Now + 120d;
                yield return new WaitForSeconds(1f);
            }
        }

        private static void Finish()
        {
            Log(_fails == 0 ? "RESULT: all checks passed" : $"RESULT: {_fails} check(s) failed");
            Quit();
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
