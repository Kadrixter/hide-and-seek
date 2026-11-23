using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HideAndSeek.Game.Gfx;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Tests of the graphics work (agent mapgfx):
    /// -testgfx &lt;dir&gt; (host in a round): the same garage views with every anti-aliasing mode, bloom on / off, with the
    /// average frame time of each ([GfxTest] lines; vsync off for the measurement).
    /// -testoffice &lt;dir&gt; (host in a round, -autostart -hidetime N): the new security room (lit overview + dark), then sits
    /// on a swivel chair, turns it with the look, gets up ([OfficeTest] lines, frames of every step).
    /// -officewatch &lt;dir&gt; (second client): photographs a chair while somebody else sits and turns on it.
    /// -testzfight: z-fighting scan of the security rooms built at runtime ([ZFightRoom] lines).
    /// </summary>
    public partial class NetSession
    {
        private void StartGfxTests()
        {
            if (ArgOr("-testgfx", null) is string g) StartCoroutine(GfxTest(g));
            if (ArgOr("-testoffice", null) is string o) StartCoroutine(OfficeTest(o));
            if (ArgOr("-officewatch", null) is string w) StartCoroutine(OfficeWatch(w));
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-testzfight") >= 0) StartCoroutine(RoomZFightTest());
        }

        private static IEnumerator MeasureFrames(float seconds, System.Action<float, float> done)
        {
            yield return null;
            int n = 0;
            float t0 = Time.realtimeSinceStartup, worst = 0f, last = t0;
            while (Time.realtimeSinceStartup - t0 < seconds)
            {
                yield return null;
                float now = Time.realtimeSinceStartup;
                worst = Mathf.Max(worst, now - last);
                last = now;
                n++;
            }
            done((Time.realtimeSinceStartup - t0) / Mathf.Max(1, n) * 1000f, worst * 1000f);
        }

        private IEnumerator GfxTest(string dir)
        {
            yield return WaitForGarageRound();
            var me = NetPlayer.Local;
            var views = new List<(string name, Vector3 pos, float yaw, float pitch, bool lamp)>
            {
                ("aisle", new Vector3(-30f, 0.05f, -28.25f), 90f, -4f, true),
                ("cars", new Vector3(-12f, 0.05f, -26.5f), 60f, -12f, true),
            };
            // A view at the nearest map lamp (bloom).
            var lamp = FindObjectsByType<Light>().Where(l => l.type != LightType.Directional && l.transform.position.y > -0.5f && l.transform.position.y < 3.4f && l.GetComponentInParent<NetPlayer>() == null)
                .OrderBy(l => (l.transform.position - new Vector3(-30f, 2f, -28f)).sqrMagnitude).FirstOrDefault();
            if (lamp != null)
            {
                Vector3 lp = lamp.transform.position, from = lp + new Vector3(-6f, 0f, -3f);
                from.y = 0.05f;
                Vector3 to = lp - (from + Vector3.up * 1.6f);
                views.Add(("lamp", from, YawTo(to), PitchTo(to), false));
            }
            var modes = (AaMode[])System.Enum.GetValues(typeof(AaMode));
            var log = new System.Text.StringBuilder();
            foreach (var v in views)
            {
                me.Motor.Teleport(v.pos, v.yaw, v.pitch);
                me.Motor.SetLook(v.yaw, v.pitch);
                if (me.Flashlight != null) me.Flashlight.SetOn(v.lamp, false);
                yield return new WaitForSeconds(1.5f);
                foreach (var m in modes)
                {
                    foreach (bool bloom in m == AaMode.Msaa4Smaa ? new[] { false, true } : new[] { false })
                    {
                        GfxSettings.TestAa = m;
                        GfxSettings.TestBloom = bloom;
                        GfxSettings.Apply();
                        QualitySettings.vSyncCount = 0;
                        Application.targetFrameRate = -1;
                        yield return new WaitForSeconds(0.5f);
                        float avg = 0f, worst = 0f;
                        yield return MeasureFrames(2.5f, (a, w) => { avg = a; worst = w; });
                        string id = $"{v.name}_{m}{(bloom ? "_bloom" : "")}";
                        Shot(dir, "gfx_" + id);
                        string line = $"[GfxTest] {id}: {avg:0.00} ms ({1000f / avg:0} FPS), worst {worst:0.0} ms, {Screen.width}x{Screen.height}";
                        Debug.Log(line);
                        log.AppendLine(line);
                        yield return new WaitForSeconds(0.3f);
                    }
                }
            }
            GfxSettings.TestAa = null;
            GfxSettings.TestBloom = null;
            GfxSettings.Apply();
            if (!string.IsNullOrEmpty(dir)) System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "gfx_times.txt"), log.ToString());
            Debug.Log("[GfxTest] done");
        }

        private IEnumerator OfficeTest(string dir)
        {
            yield return WaitSeekingRoom();
            var me = NetPlayer.Local;
            var sr = SecurityRooms.Instance;
            var mm = MatchManager.Instance;
            Debug.Log($"[OfficeTest] chairs {sr.Chairs.Count}, role {(me.IsSeeker ? "seeker" : "hider")}");

            // Same view as -testroomdark "outside_lamp" (flashlight at the door) for a before / after pair.
            me.Motor.Teleport(new Vector3(76.0f, 0.05f, 46.5f), 5f, 6f);
            me.Motor.SetLook(5f, 6f);
            if (me.Flashlight != null) me.Flashlight.SetOn(true, false);
            yield return new WaitForSeconds(1.5f);
            Shot(dir, "office_outside_lamp");
            yield return new WaitForSeconds(0.4f);
            me.Motor.Teleport(new Vector3(71.5f, 0.05f, 57.6f), 180f, 2f);
            me.Motor.SetLook(180f, 2f);
            yield return new WaitForSeconds(1.2f);
            Shot(dir, "office_south_lamp");
            yield return new WaitForSeconds(0.4f);
            me.Motor.Teleport(new Vector3(70.8f, 0.05f, 56.2f), 270f, 4f);
            me.Motor.SetLook(270f, 4f);
            yield return new WaitForSeconds(1.2f);
            Shot(dir, "office_west_lamp");
            if (me.Flashlight != null) me.Flashlight.SetOn(false, false);

            // Lit overview (work lights in the room and outside), then the room as it is: dark, monitors and the desk lamp.
            var outS = CrateTestLight(new Vector3(71f, 2.7f, 48.5f));
            var outW = CrateTestLight(new Vector3(62.8f, 2.7f, 55f));
            var work = CrateTestLight(new Vector3(72f, 2.6f, 55.5f));
            work.GetComponent<Light>().range = 12f;
            work.GetComponent<Light>().intensity = 1.6f;
            var cams = new (string n, Vector3 pos, Vector3 at)[]
            {
                ("overview_se", new Vector3(75.7f, 2.4f, 53.0f), new Vector3(70.5f, 0.6f, 57.5f)),
                ("overview_sw", new Vector3(66.6f, 2.4f, 52.6f), new Vector3(73f, 0.6f, 58f)),
                ("overview_n", new Vector3(72f, 2.5f, 59.2f), new Vector3(70f, 0.4f, 53.2f)),
                ("coffee", new Vector3(69.6f, 1.6f, 54.6f), new Vector3(67.6f, 0.9f, 52.4f)),
                ("table", new Vector3(69.8f, 1.5f, 55.2f), new Vector3(68.5f, 0.75f, 54.1f)),
                ("shelf", new Vector3(68.6f, 1.4f, 55.5f), new Vector3(66.3f, 1.0f, 55.3f)),
                ("sideboard", new Vector3(75.6f, 1.6f, 56.2f), new Vector3(77.6f, 0.9f, 57.6f)),
                ("jacket", new Vector3(74.2f, 1.5f, 54.0f), new Vector3(75.5f, 1.3f, 52.5f)),
                ("chair", new Vector3(70.6f, 1.2f, 56.6f), new Vector3(69.6f, 0.55f, 58.0f)),
                ("desk_food", new Vector3(73.4f, 1.3f, 57.6f), new Vector3(73.9f, 0.78f, 58.6f)),
                ("outside_s", new Vector3(71f, 1.7f, 47.5f), new Vector3(71f, 1.2f, 52f)),
                ("outside_w", new Vector3(62.5f, 1.7f, 53.5f), new Vector3(66f, 1.2f, 57f)),
            };
            foreach (var c in cams) CamShot(dir, "office_lit_" + c.n, c.pos, c.at);
            Destroy(work);
            Destroy(outS);
            Destroy(outW);
            yield return null;
            foreach (var c in cams) CamShot(dir, "office_dark_" + c.n, c.pos, c.at);

            // Sit on chair 0 of level 0 from where a player would stand, frames of the sit-down (a dim work light for the outside views).
            var seatLight = CrateTestLight(new Vector3(71.2f, 2.6f, 56.3f));
            seatLight.GetComponent<Light>().intensity = 0.8f;
            var chair = sr.Chairs[0];
            Vector3 stand = chair.Anchor.position + new Vector3(0.5f, 0f, -0.9f);
            me.Motor.Teleport(stand, YawTo(chair.SeatCenter - stand), -20f);
            me.Motor.SetLook(YawTo(chair.SeatCenter - stand), -20f);
            if (me.Flashlight != null) me.Flashlight.SetOn(true, false);
            yield return new WaitForSeconds(1f);
            Debug.Log($"[OfficeTest] prompt before sitting: '{LocalPlayerController.Instance?.PromptText}'");
            mm.ChairRequestRpc(0, true, (short)Mathf.RoundToInt(Mathf.Repeat(me.transform.eulerAngles.y, 360f)));
            var act = me.Actions as HideAndSeek.Characters.BlockyCharacterActions;
            for (int i = 0; i < 10; i++)
            {
                Shot(dir, $"office_sit_{i:00}");
                CamShot(dir, $"office_sitext_{i:00}", new Vector3(71.2f, 1.5f, 56.6f), chair.SeatCenter);
                yield return new WaitForSeconds(0.12f);
            }
            Debug.Log($"[OfficeTest] seated: local chair {SecurityChairDriver.LocalChair}, sitter {mm.SecurityChairs.Value.Sitter(0) == me.NetworkObjectId}, sit weight {act?.SitWeight:0.00}, pos {me.transform.position:F2} anchor {chair.Anchor.position:F2}");
            // Swivel: turn the look in steps (the chair follows), towards the monitors and round to the door.
            float y0 = me.transform.eulerAngles.y;
            float[] yaws = { 0f, 20f, 40f, 0f, -30f, -60f, -100f, -140f, -170f };
            for (int k = 0; k < yaws.Length; k++)
            {
                float from = me.transform.eulerAngles.y, to = yaws[k];
                for (float t = 0f; t < 1f; t += Time.deltaTime / 0.35f)
                {
                    me.Motor.SetLook(Mathf.LerpAngle(from, to, Mathf.SmoothStep(0f, 1f, t)), 2f);
                    yield return null;
                }
                me.Motor.SetLook(to, 2f);
                yield return new WaitForSeconds(0.35f);
                Shot(dir, $"office_turn_{k:00}");
                CamShot(dir, $"office_turnext_{k:00}", new Vector3(71.4f, 1.6f, 56.2f), chair.SeatCenter);
                Debug.Log($"[OfficeTest] look yaw {to:0} -> chair shown yaw {chair.ShownYaw:0}");
            }
            me.Motor.SetLook(0f, 8f);
            yield return new WaitForSeconds(1.2f);
            Shot(dir, "office_monitors_seated");
            Debug.Log($"[OfficeTest] getting up (start yaw {y0:0})");
            SecurityChairDriver.LeaveLocal();
            for (int i = 0; i < 6; i++)
            {
                Shot(dir, $"office_stand_{i:00}");
                CamShot(dir, $"office_standext_{i:00}", new Vector3(71.2f, 1.5f, 56.6f), chair.SeatCenter);
                yield return new WaitForSeconds(0.12f);
            }
            yield return new WaitForSeconds(1f);
            Destroy(seatLight);
            Debug.Log($"[OfficeTest] after standing: local chair {SecurityChairDriver.LocalChair}, sitter free {mm.SecurityChairs.Value.Sitter(0) == ChairsState.Free}, cc {me.GetComponent<CharacterController>().enabled}, seated motor {me.Motor.Seated}, pos {me.transform.position:F2}");
            Debug.Log("[OfficeTest] done");
        }

        private IEnumerator OfficeWatch(string dir)
        {
            yield return WaitSeekingRoom();
            var sr = SecurityRooms.Instance;
            var mm = MatchManager.Instance;
            float until = Time.time + 120f;
            while (Time.time < until && mm.SecurityChairs.Value.Sitter(0) == ChairsState.Free) yield return null;
            Debug.Log($"[OfficeWatch] somebody sits on chair 0: {mm.SecurityChairs.Value.Sitter(0)}");
            var light = CrateTestLight(new Vector3(71.5f, 2.6f, 56.5f));
            for (int i = 0; i < 24 && mm.SecurityChairs.Value.Sitter(0) != ChairsState.Free; i++)
            {
                CamShot(dir, $"watch_{i:00}", new Vector3(71.3f, 1.5f, 56.4f), sr.Chairs[0].SeatCenter);
                Debug.Log($"[OfficeWatch] chair yaw {sr.Chairs[0].ShownYaw:0}");
                yield return new WaitForSeconds(0.4f);
            }
            Destroy(light);
            Debug.Log("[OfficeWatch] done");
        }

        private IEnumerator RoomZFightTest()
        {
            yield return WaitSeekingRoom();
            var sr = SecurityRooms.Instance;
            var rends = sr.GetComponentsInChildren<Renderer>().Where(r => r.transform.position.y > -1f && r.transform.position.y < 4f).ToList();
            // Room level 0 against everything of the garage around it (walls, floor, signs).
            foreach (var r in FindObjectsByType<MeshRenderer>())
                if (!r.transform.IsChildOf(sr.transform) && r.bounds.Intersects(new Bounds(new Vector3(72f, 1.5f, 55.5f), new Vector3(16f, 4f, 12f)))) rends.Add(r);
            var pairs = ZFightScan.Find(rends, 0.004f);
            Debug.Log($"[ZFightRoom] {rends.Count} renderers, {pairs.Count} pairs within 4 mm");
            foreach (var p in pairs.Take(60)) Debug.Log("[ZFightRoom]   " + ZFightScan.Describe(p));

            // Far views along painted floors (lit by temporary lamps): flickering decals show as broken / speckled lines.
            string dir = ArgOr("-zfightshots", "");
            if (string.IsNullOrEmpty(dir)) yield break;
            var me = NetPlayer.Local;
            var lamps = new List<GameObject>();
            foreach (var (pos, yaw, n) in new[] { (new Vector3(-70f, 0.05f, -28.25f), 90f, "aisle_far"), (new Vector3(-70f, 0.05f, -11.75f), 90f, "aisle2_far"), (new Vector3(-6f, 0.05f, -50f), 0f, "cross_far"), (new Vector3(20f, -6.75f, -50f), 20f, "flood_far") })
            {
                foreach (var l in lamps) Destroy(l);
                lamps.Clear();
                Vector3 f = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                for (int i = 1; i <= 6; i++)
                {
                    var l = CrateTestLight(pos + f * (i * 9f) + Vector3.up * 2.6f);
                    l.GetComponent<Light>().range = 14f;
                    l.GetComponent<Light>().shadows = LightShadows.None;
                    lamps.Add(l);
                }
                me.Motor.Teleport(pos, yaw, -6f);
                me.Motor.SetLook(yaw, -6f);
                yield return new WaitForSeconds(1.2f);
                for (int k = 0; k < 3; k++)
                {
                    // Tiny sideways steps: z-fighting changes pattern between frames, a real line does not.
                    me.Motor.Teleport(pos + Vector3.Cross(Vector3.up, f) * (k * 0.04f), yaw, -6f);
                    me.Motor.SetLook(yaw, -6f);
                    yield return new WaitForSeconds(0.25f);
                    Shot(dir, $"zf_{n}_{k}");
                    yield return new WaitForSeconds(0.25f);
                }
            }
            foreach (var l in lamps) Destroy(l);
            Debug.Log("[ZFightRoom] shots done");
        }
    }
}
