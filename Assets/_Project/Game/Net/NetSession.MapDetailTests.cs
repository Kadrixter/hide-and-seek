using System;
using System.Collections;
using System.Collections.Generic;
using HideAndSeek.Characters;
using HideAndSeek.Map;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Tests of the map details of 23.11 (host in a running round, e.g. host -forcehider + one client, -autostart):
    ///  -testdust &lt;folder&gt;: dusty cars on levels 0 / -1 / -2, the four covers, the leaks and the rods in rubble -
    ///      player view with the flashlight and a lit free camera for each; quits when done.
    ///  -testrodpull: the host walks to a rod in rubble and pulls it out (first-person shots), then interrupts a
    ///      second pull after two yanks and finishes it (shorter); logs [RodPullTest].
    ///  -rodpullwatch (other instance): lit third-person frames of every pull it sees (for a video).
    ///  -pullshots &lt;folder&gt;: where the rod pull tests write their PNGs.
    /// </summary>
    public partial class NetSession
    {
        private void StartMapDetailTests(Func<string, bool> has)
        {
            if (ArgOr("-testdust", null) is string d) StartCoroutine(DustTest(d));
            if (has("-testrodpull")) StartCoroutine(RodPullTest());
            if (has("-rodpullwatch")) StartCoroutine(RodPullWatch());
            if (has("-rodpullstand")) StartCoroutine(RodPullStand());
            if (ArgOr("-rodpullfilm", null) is string f) StartCoroutine(RodPullFilm(f));
        }

        private static string PullShots => ArgOr("-pullshots", "");

        private IEnumerator DustTest(string dir)
        {
            yield return WaitForGarageRound();
            var me = NetPlayer.Local;
            if (me.Flashlight != null) me.Flashlight.SetOn(true, false);
            var root = GameObject.Find("ParkingGarage");
            if (root == null) { Debug.Log("[DustTest] no garage"); yield break; }
            // cars per level (dust is decided by the builder; the name keeps the model and the bay)
            for (int lv = 0; lv < 3; lv++)
            {
                var group = root.transform.Find("Cars/Level-" + (lv + 1));
                if (group == null) continue;
                int n = 0, dusty = 0;
                foreach (Transform car in group)
                {
                    var mr = car.GetComponent<MeshRenderer>();
                    if (mr == null) continue;
                    bool d = mr.sharedMaterial != null && mr.sharedMaterial.shader.name == "Hns/CarDust";
                    if (d) dusty++;
                    n++;
                }
                Debug.Log($"[DustTest] level {-lv}: {dusty} of {n} cars dusty");
                int shots = 0;
                foreach (Transform car in group)
                {
                    if (shots >= 3) break;
                    var mr = car.GetComponent<MeshRenderer>();
                    if (mr == null || (lv > 0 && mr.sharedMaterial.shader.name != "Hns/CarDust")) continue;
                    if (!FreeSide(car, out var feet)) continue;
                    yield return PovAndLit(dir, $"dust_L{lv}_{shots}_{car.name}", me, feet, car.position + Vector3.up * 0.7f);
                    shots++;
                }
            }
            // covers
            int c = 0;
            foreach (Transform t in root.GetComponentsInChildren<Transform>())
            {
                if (!t.name.StartsWith("TarpCar_") || c >= 10) continue;
                if (!FreeSide(t, out var feet, 2.6f)) continue;
                yield return PovAndLit(dir, $"cover_{c++}_{t.name}", me, feet, t.position + Vector3.up * 0.7f);
            }
            // leaks (this round)
            var leaks = CarLeaks.Instance;
            if (leaks != null)
            {
                Debug.Log($"[DustTest] leaks seed {leaks.Seed}: {string.Join(", ", leaks.Shown)}");
                int k = 0;
                foreach (Transform l in leaks.transform)
                {
                    Vector3 fwd = l.forward;
                    Vector3 at = l.position + fwd * 0.6f;
                    Vector3 feet = at + fwd * 2.2f + l.right * 0.8f;
                    feet.y = MapInfo.FloorUnder(l.position.y);
                    Pose(me, feet, at);
                    me.Motor.SetLook(me.Motor.LookYaw, -40f);
                    yield return new WaitForSeconds(0.6f);
                    Shot(dir, $"leak_{k}_pov");
                    yield return new WaitForSeconds(0.2f);
                    ItemLitShot(dir, $"leak_{k}_lit", at + fwd * 1.8f + Vector3.up * 1.2f + l.right * 0.6f, at);
                    k++;
                }
            }
            // rods in rubble
            var mm = MatchManager.Instance;
            for (int i = 0; i < mm.Rods.Count; i++)
            {
                var r = mm.Rods[i];
                Debug.Log($"[DustTest] rod {r.Id} embedded={r.Embedded} at {r.Entry} level {MapInfo.LevelOf(r.Stand.y)}");
                if (!r.Embedded) continue;
                Vector3 back = r.Stand + (r.Stand - r.Entry).normalized * 1.2f;
                back.y = r.Stand.y;
                yield return PovAndLit(dir, $"rod_{i}", me, back, r.Exposed);
            }
            Debug.Log("[DustTest] done");
            yield return new WaitForSeconds(1f);
            Application.Quit();
        }

        /// <summary>Player view (flashlight) and a lit free camera of the same thing.</summary>
        private IEnumerator PovAndLit(string dir, string name, NetPlayer me, Vector3 feet, Vector3 at)
        {
            Pose(me, feet, at);
            yield return new WaitForSeconds(0.7f);
            Shot(dir, name + "_pov");
            yield return new WaitForSeconds(0.2f);
            Vector3 eye = feet + Vector3.up * 1.6f;
            ItemLitShot(dir, name + "_lit", Vector3.Lerp(at, eye, 1.3f) + Vector3.up * 0.4f, at);
        }

        /// <summary>A free floor spot 2.2 m (or <paramref name="dist"/>) beside an object.</summary>
        private static bool FreeSide(Transform t, out Vector3 feet, float dist = 2.4f)
        {
            foreach (var d in new[] { t.right, -t.right, t.forward, -t.forward })
            {
                Vector3 p = t.position + d * dist + Vector3.up * 0.05f;
                if (Physics.CheckCapsule(p + Vector3.up * 0.5f, p + Vector3.up * 1.6f, 0.35f)) continue;
                if (!Physics.Raycast(p + Vector3.up, Vector3.down, out var g, 1.5f) || Mathf.Abs(g.point.y - t.position.y) > 0.3f) continue;
                feet = p;
                return true;
            }
            feet = default;
            return false;
        }

        private IEnumerator RodPullTest()
        {
            yield return WaitSeeking();
            var mm = MatchManager.Instance;
            var me = NetPlayer.Local;
            string dir = PullShots;
            int embedded = 0;
            foreach (var r in mm.Rods) if (r.Embedded) embedded++;
            Debug.Log($"[RodPullTest] rods {mm.Rods.Count} ({embedded} in rubble), sockets on the map {RodSpots.Sockets().Count}");
            var drv = me.GetComponent<RodPullDriver>();
            for (int pass = 0; pass < 2; pass++)
            {
                int idx = -1;
                for (int i = 0; i < mm.Rods.Count; i++) if (mm.Rods[i].Embedded && !mm.Rods[i].Taken) { idx = i; break; }
                if (idx < 0) { Debug.Log("[RodPullTest] no rod in rubble left"); break; }
                var rod = mm.Rods[idx];
                Vector3 away = rod.Stand - rod.Entry;
                away.y = 0f;
                Vector3 feet = rod.Stand + away.normalized * 0.6f;
                Pose(me, feet, rod.Exposed);
                if (me.Flashlight != null) me.Flashlight.SetOn(true, false);
                yield return new WaitForSeconds(1.2f);
                var lpc = LocalPlayerController.Instance;
                Debug.Log($"[RodPullTest] pass {pass}: prompt '{lpc.PromptText}' at rod {rod.Id}");
                Shot(dir, $"p{pass}_prompt");
                me.RequestPickupRodRpc(rod.Id);
                float t0 = Time.time;
                if (pass == 1)
                {
                    // interrupt after two yanks
                    while (Time.time - t0 < 2.0f) yield return null;
                    Shot(dir, "p1_before_cancel");
                    me.RequestRodPullCancelRpc();
                    yield return new WaitForSeconds(1.0f);
                    rod = mm.Rods[idx];
                    Debug.Log($"[RodPullTest] interrupted: done={rod.Done} puller={(rod.BeingPulled ? "yes" : "no")} taken={rod.Taken} (expect done 2)");
                    Shot(dir, "p1_after_cancel");
                    Pose(me, feet, rod.Exposed);
                    yield return new WaitForSeconds(0.8f);
                    me.RequestPickupRodRpc(rod.Id);
                    t0 = Time.time;
                }
                float[] at = { 0.3f, 0.75f, 1.1f, 1.8f, 2.95f, 3.6f, 4.15f, 4.35f, 4.7f };
                int s = 0;
                while (Time.time - t0 < 7f && !mm.Rods[idx].Taken)
                {
                    float tl = drv != null ? drv.Timeline : -1f;
                    if (pass == 0 && s < at.Length && tl >= at[s]) { Shot(dir, $"p0_fp_{at[s]:0.00}"); s++; }
                    yield return null;
                }
                Debug.Log($"[RodPullTest] pass {pass}: taken={mm.Rods[idx].Taken} after {Time.time - t0:0.00} s (expected {RodPullTimeline.LengthFrom(pass == 1 ? 2 : 0):0.00}), rods={me.RodCount.Value}, sounds={RubbleFx.Played}");
                yield return new WaitForSeconds(1.5f);
                if (pass == 0)
                {
                    // drop it again so the second pull has room in the inventory
                    me.RodCount.Value = 0;
                    yield return new WaitForSeconds(0.5f);
                }
            }
            Debug.Log("[RodPullTest] done");
        }

        /// <summary>Host: stands still at the first rod in rubble (for -rodpullfilm on the other instance).</summary>
        private IEnumerator RodPullStand()
        {
            yield return WaitSeeking();
            var mm = MatchManager.Instance;
            var me = NetPlayer.Local;
            foreach (var r in mm.Rods)
            {
                if (!r.Embedded) continue;
                Vector3 to = r.Entry - r.Stand;
                me.Motor.Teleport(r.Stand + Vector3.up * 0.02f, YawTo(to), -30f);
                Debug.Log($"[RodPullTest] standing at rod {r.Id} ({r.Stand})");
                break;
            }
        }

        /// <summary>
        /// Other instance: when the hider stands at a rod, plays the pull locally frame by frame (30 fps of timeline per
        /// captured frame, so the video is smooth however slow the capture is) from two sides.
        /// </summary>
        private IEnumerator RodPullFilm(string dir)
        {
            yield return WaitSeeking();
            var mm = MatchManager.Instance;
            NetPlayer h = null;
            RodState rod = default;
            float end = Time.time + 60f;
            while (h == null && Time.time < end)
            {
                yield return new WaitForSeconds(0.25f);
                foreach (var p in NetPlayer.All)
                {
                    if (p == null || p == NetPlayer.Local || !p.IsHider) continue;
                    foreach (var r in mm.Rods)
                        if (r.Embedded && Vector3.Distance(new Vector3(p.transform.position.x, 0f, p.transform.position.z), new Vector3(r.Stand.x, 0f, r.Stand.z)) < 0.35f) { h = p; rod = r; }
                }
            }
            if (h == null) { Debug.Log("[RodPullFilm] no hider at a rod"); yield break; }
            yield return new WaitForSeconds(1.5f);
            RodPullDriver.FilmPlayer = h;
            RodPullDriver.FilmRod = rod.Id;
            for (int take = 0; take < 2; take++)
            {
                int frames = (int)(RodPullTimeline.Duration * 30f) + 12;
                for (int f = 0; f < frames; f++)
                {
                    RodPullDriver.FilmTime = Mathf.Min(f / 30f, RodPullTimeline.Duration - 0.01f);
                    yield return null;
                    yield return new WaitForEndOfFrame();
                    Vector3 at = h.transform.position + Vector3.up * 0.7f + h.transform.forward * 0.3f;
                    Vector3 side = take == 0 ? h.transform.right : -h.transform.right;
                    Vector3 cam = FreeCam(at, side * 2.2f + h.transform.forward * 1.3f + Vector3.up * 0.45f);
                    ItemLitShot(dir, $"film{take}_{f:0000}", cam, at);
                }
                RodPullDriver.FilmTime = -1f;
                for (int k = 0; k < 20; k++) yield return null;
            }
            RodPullDriver.FilmPlayer = null;
            Debug.Log($"[RodPullFilm] done, rod {rod.Id}, sounds {RubbleFx.Played}");
        }

        private IEnumerator RodPullWatch()
        {
            yield return WaitSeeking();
            string dir = PullShots;
            int frame = 0, pulls = 0;
            bool was = false;
            while (frame < 900)
            {
                yield return new WaitForEndOfFrame();
                NetPlayer h = null;
                RodPullDriver drv = null;
                foreach (var p in NetPlayer.All)
                {
                    if (p == null || p == NetPlayer.Local) continue;
                    var d = p.GetComponent<RodPullDriver>();
                    if (d != null && d.Pulling) { h = p; drv = d; }
                }
                if (h == null) { was = false; continue; }
                if (!was) pulls++;
                was = true;
                // three-quarter view from the side of the right (braced) leg
                Vector3 at = h.transform.position + Vector3.up * 0.75f + h.transform.forward * 0.35f;
                Vector3 cam = FreeCam(at, h.transform.right * 2.3f + h.transform.forward * 1.2f + Vector3.up * 0.5f);
                ItemLitShot(dir, $"watch{pulls}_{frame++:0000}_{drv.Timeline:0.00}", cam, at);
            }
            Debug.Log($"[RodPullWatch] done, {frame} frames, {pulls} pulls");
        }
    }
}
