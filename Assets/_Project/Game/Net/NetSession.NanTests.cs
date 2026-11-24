using System.Collections;
using System.Linq;
using HideAndSeek.Game.Gfx;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Black squares / white sparkles hunt (agent blacksq):
    ///  -nanscan &lt;dir&gt;: every few frames the HDR frame is checked for NaN / Inf / negative pixels (see <see cref="NanScan"/>),
    ///      works together with any other test; a summary every 20 s.
    ///  -testnan &lt;dir&gt; (host in a round, -autostart): bloom + MSAA 4x + SMAA forced, walks along every parked car of the
    ///      three levels (flashlight on / off alternately, a full turn at every fifth car), the wreck, then quits.
    ///  -nanevery N: scan every N-th frame (default 3, the tour uses 1).
    ///  -nanculprit (with -nanscan): every frame with a NaN pixel is bisected down to its renderer (camera stack re-rendered).
    ///  -testblacksq &lt;dir&gt; [-blacksqcar Name]: Esc menu and drunk look in front of a car with a NaN pixel (screenshots).
    ///  -testnanguard &lt;dir&gt;: zero-normal specks in front of a car, Esc / drunk / night vision per AA and bloom setting.
    ///  -testnormals &lt;dir&gt;: zero / NaN / opposing normals of every mesh in the lobby and the round (normals.txt).
    /// </summary>
    public partial class NetSession
    {
        private void StartNanTests()
        {
            string tour = ArgOr("-testnan", null), scan = ArgOr("-nanscan", null), sq = ArgOr("-testblacksq", null);
            if (sq != null) { StartCoroutine(BlackSqTest(sq)); return; }
            if (ArgOr("-testnanguard", null) is string gd) { StartCoroutine(NanGuardTest(gd)); return; }
            if (ArgOr("-testnormals", null) is string nd) StartCoroutine(NormalsTest(nd));
            string dir = tour ?? scan;
            if (dir == null) return;
            NanScan.Active = true;
            NanScan.Dir = dir;
            if (int.TryParse(ArgOr("-nanevery", ""), out int every) && every > 0) NanScan.Every = every;
            StartCoroutine(NanSummaries());
            if (tour != null) StartCoroutine(NanTour(tour));
            else if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-nanculprit") >= 0) StartCoroutine(NanWatch());
        }

        /// <summary>-nanscan with -nanculprit: at the end of every frame that had a NaN pixel, bisect for its renderer.</summary>
        private IEnumerator NanWatch()
        {
            NanScan.Every = 1;
            var end = new WaitForEndOfFrame();
            while (_culpritRuns < 120)
            {
                yield return end;
                if (NanScan.LastBadFrame != Time.frameCount) continue;
                _culpritRuns++;
                string r = HideAndSeek.Game.Gfx.NanCulprit.Find();
                Debug.Log($"[NanCulprit] frame {Time.frameCount} {r}");
                int at = r.IndexOf("-> ");
                string key = at >= 0 ? r.Substring(at + 3) : r;
                _culprits.TryGetValue(key, out int n);
                _culprits[key] = n + 1;
                if (_culpritRuns % 10 == 0)
                    foreach (var kv in _culprits.OrderByDescending(k => k.Value)) Debug.Log($"[NanCulprit] {kv.Value}x {kv.Key}");
            }
        }

        private readonly System.Collections.Generic.Dictionary<string, int> _culprits = new();
        private int _culpritRuns;

        /// <summary>Bisects the renderers behind a NaN pixel of the current view (manual renders of the main camera).</summary>
        private IEnumerator NanCulprit()
        {
            if (_culpritRuns >= 120) yield break;
            _culpritRuns++;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                yield return new WaitForEndOfFrame();
                string r = HideAndSeek.Game.Gfx.NanCulprit.Find();
                Debug.Log($"[NanCulprit] [{NanScan.Context}] {r}");
                if (r.StartsWith("no NaN")) continue;
                int at = r.IndexOf("-> ");
                string key = at >= 0 ? r.Substring(at + 3) : r;
                // the car name is noise for counting: keep the part after the last '/'
                _culprits.TryGetValue(key, out int n);
                _culprits[key] = n + 1;
                yield break;
            }
        }

        /// <summary>
        /// -testblacksq &lt;dir&gt; (host in a round): finds a parked car whose view has a NaN pixel (flashlight on), takes the
        /// plain view, opens the Esc menu (live blur of the game behind it) and takes it again, then the same with the
        /// drunk look; NaN pixels in a blurred frame grow into black squares. Logs [BlackSq], quits.
        /// </summary>
        private IEnumerator BlackSqTest(string dir)
        {
            yield return WaitForGarageRound();
            var me = NetPlayer.Local;
            GfxSettings.TestBloom = true;
            GfxSettings.TestAa = AaMode.Msaa4Smaa;
            GfxSettings.Apply();
            NanScan.Active = true;
            NanScan.Every = 1;
            NanScan.MaxSaves = 0;
            if (me.Flashlight != null) me.Flashlight.SetOn(true, false);
            var root = GameObject.Find("ParkingGarage");
            var group = root != null ? root.transform.Find("Cars/Level-1") : null;
            bool found = false;
            string where = "";
            if (group != null)
            {
                // -blacksqcar Name: always this car (the same frame before / after a fix)
                string only = ArgOr("-blacksqcar", null);
                foreach (Transform car in group)
                {
                    if (only != null && car.name != only) continue;
                    if (car.GetComponent<MeshRenderer>() == null || !FreeSide(car, out var feet)) continue;
                    Pose(me, feet, car.position + Vector3.up * 0.35f);
                    int before = NanScan.BadFrames;
                    for (int k = 0; k < 12; k++) yield return null;
                    if (only != null || NanScan.BadFrames - before >= 6) { found = NanScan.BadFrames > before; where = car.name; break; }
                }
            }
            Debug.Log($"[BlackSq] view with NaN: {found} ({where}), last {NanScan.LastBadCount} px at {NanScan.LastBadCentre}");
            Debug.Log("[BlackSq] culprit: " + HideAndSeek.Game.Gfx.NanCulprit.Find());
            yield return new WaitForSeconds(0.3f);
            Shot(dir, "blacksq_1_view");
            yield return new WaitForSeconds(0.4f);
            var hud = typeof(HideAndSeek.Game.GameHud);
            var inst = hud.GetField("_instance", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)?.GetValue(null);
            var show = hud.GetMethod("ShowEscMenu", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var close = hud.GetMethod("CloseMenus", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (inst != null && show != null) show.Invoke(inst, null);
            yield return new WaitForSeconds(0.8f);
            Shot(dir, "blacksq_2_escmenu");
            yield return new WaitForSeconds(0.4f);
            if (inst != null && close != null) close.Invoke(inst, null);
            yield return new WaitForSeconds(0.5f);
            // the drunk look (also the stun wake-up): a five-tap blur and a ghost image on the world camera
            var cam = me.Camera;
            var dv = cam != null ? cam.GetComponent<DrunkVision>() : null;
            if (cam != null && dv == null) dv = cam.gameObject.AddComponent<DrunkVision>();
            if (dv != null)
            {
                dv.enabled = true;
                for (int k = 0; k < 40; k++) { dv.Amount = 1f; dv.Clock = Time.time; yield return null; }
                Shot(dir, "blacksq_3_drunk");
                yield return null;
                dv.Amount = 1f;
                yield return new WaitForSeconds(0.3f);
                dv.Amount = 0f;
            }
            Debug.Log($"[BlackSq] done, bad frames {NanScan.BadFrames}");
            yield return new WaitForSeconds(1f);
            Application.Quit();
        }

        private IEnumerator NormalsTest(string dir)
        {
            yield return new WaitForSeconds(4f);
            string lobby = MeshNormalScan.Run();
            Debug.Log(lobby);
            yield return WaitForGarageRound();
            yield return new WaitForSeconds(2f);
            string round = MeshNormalScan.Run();
            Debug.Log(round);
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "normals.txt"), "LOBBY\n" + lobby + "\nROUND\n" + round);
            Debug.Log("[Normals] done");
            Application.Quit();
        }

        private static IEnumerator NanSummaries()
        {
            while (true)
            {
                yield return new WaitForSecondsRealtime(20f);
                Debug.Log(NanScan.Summary());
            }
        }

        private IEnumerator NanTour(string dir)
        {
            yield return WaitForGarageRound();
            var me = NetPlayer.Local;
            GfxSettings.TestBloom = true;
            GfxSettings.TestAa = AaMode.Msaa4Smaa;
            GfxSettings.Apply();
            NanScan.Every = 1;
            var root = GameObject.Find("ParkingGarage");
            if (root == null) { Debug.Log("[NanTour] no garage"); yield break; }
            int seen = 0;
            for (int lv = 0; lv < 3; lv++)
            {
                var group = root.transform.Find("Cars/Level-" + (lv + 1));
                if (group == null) continue;
                foreach (Transform car in group)
                {
                    if (car.GetComponent<MeshRenderer>() == null) continue;
                    if (!FreeSide(car, out var feet)) continue;
                    bool lamp = (seen & 1) == 0;
                    if (me.Flashlight != null) me.Flashlight.SetOn(lamp, false);
                    var mr = car.GetComponent<MeshRenderer>();
                    NanScan.Context = $"L{-lv} {car.name} {(mr.sharedMaterial != null ? mr.sharedMaterial.shader.name : "-")}{(lamp ? " lamp" : "")}";
                    Pose(me, feet, car.position + Vector3.up * 0.7f);
                    int before = NanScan.BadFrames;
                    for (int k = 0; k < 5; k++) yield return null;
                    if (NanScan.BadFrames > before) yield return NanCulprit();
                    if (seen % 5 == 0)
                    {
                        float yaw = me.Motor.LookYaw;
                        for (int a = 1; a < 8; a++)
                        {
                            me.Motor.SetLook(yaw + a * 45f, -6f);
                            yield return null;
                            yield return null;
                        }
                    }
                    seen++;
                }
                Debug.Log($"[NanTour] level {-lv} done, cars {seen}, bad frames so far {NanScan.BadFrames}");
            }
            var wreck = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude).Select(r => r.transform).FirstOrDefault(t => t.name.StartsWith("Wreck_VwGolf4Gti"));
            if (wreck != null && FreeSide(wreck, out var wf, 2.6f))
            {
                NanScan.Context = "wreck";
                Pose(me, wf, wreck.position + Vector3.up * 0.7f);
                for (int k = 0; k < 30; k++) yield return null;
            }
            NanScan.Context = "";
            Debug.Log(NanScan.Summary());
            foreach (var kv in _culprits.OrderByDescending(k => k.Value)) Debug.Log($"[NanCulprit] {kv.Value}x {kv.Key}");
            if (!string.IsNullOrEmpty(dir)) System.IO.File.WriteAllLines(System.IO.Path.Combine(dir, "nan_culprits.txt"), _culprits.OrderByDescending(k => k.Value).Select(k => $"{k.Value}x {k.Key}"));
            if (!string.IsNullOrEmpty(dir)) System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "nan_summary.txt"), NanScan.Summary());
            Debug.Log("[NanTour] done");
            yield return new WaitForSeconds(1f);
            Application.Quit();
        }
    }
}
