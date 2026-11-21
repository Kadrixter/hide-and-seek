using System;
using HideAndSeek.Audio;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Scripted tests for the 2026-11-19 update: Glock vs RC car, blood / footprints, chainsaw pull-start,
    /// mirror flashlight bounce, the new settings screen, sound between floors.
    /// </summary>
    public partial class NetSession
    {
        private void StartUpdateTests(Func<string, bool> has)
        {
            if (has("-testrcshot")) StartCoroutine(RcShotHostTest());
            if (has("-testrcshooter")) StartCoroutine(RcShooterTest());
            if (has("-testblood")) StartCoroutine(BloodTest());
            if (has("-testlobbyupd")) StartCoroutine(LobbyUpdateTest());
            if (has("-testsawanim")) StartCoroutine(SawAnimTest());
            if (has("-testfloors")) StartCoroutine(FloorAudioTest());
            if (has("-testsawwatch")) StartCoroutine(SawWatchTest());
            if (has("-testgore")) StartCoroutine(GoreWatchTest());
            if (has("-testrace")) StartCoroutine(RaceTest());
            if (has("-testpush")) StartCoroutine(PushTest());
            if (has("-testglockfin")) StartCoroutine(GlockFinisherTest());
        }

        private static void Shot(string dir, string name)
        {
            if (string.IsNullOrEmpty(dir)) return;
            System.IO.Directory.CreateDirectory(dir);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, name + ".png"));
        }

        /// <summary>Renders a temporary camera at <paramref name="pos"/> looking at <paramref name="at"/> into a PNG.</summary>
        private static void CamShot(string dir, string name, Vector3 pos, Vector3 at)
        {
            if (string.IsNullOrEmpty(dir)) return;
            System.IO.Directory.CreateDirectory(dir);
            var go = new GameObject("TestCamShot");
            var cam = go.AddComponent<Camera>();
            cam.enabled = false;
            cam.fieldOfView = 55f;
            cam.nearClipPlane = 0.03f;
            cam.cullingMask = ~(1 << 9);
            go.transform.position = pos;
            go.transform.LookAt(at);
            var rt = new RenderTexture(1280, 720, 24) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            Destroy(go);
            rt.Release();
            Destroy(tex);
        }

        private static float PitchTo(Vector3 to) => Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg;
        private static float YawTo(Vector3 to) => Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;

        private static int CountNamed(string name)
        {
            int n = 0;
            foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None)) if (t.name == name) n++;
            return n;
        }

        // ================================================================ Glock vs RC car

        /// <summary>-testrcshot (host hider via -forcehider): drive car 0, give the seeker the Glock, wait to be shot.</summary>
        private System.Collections.IEnumerator RcShotHostTest()
        {
            var mm = MatchManager.Instance;
            while (mm == null || mm.Phase.Value != MatchPhase.Seeking || NetPlayer.Local == null) { mm = MatchManager.Instance; yield return null; }
            yield return new WaitForSeconds(1.5f);
            var me = NetPlayer.Local;
            if (!me.IsHider) { Debug.Log("[RcShot] host must hide (-forcehider)"); yield break; }
            string dir = ArgOr("-rcshots", "");
            TestLight(new Vector3(-16f, -1f, 54.5f), 14f, 1.8f);
            var shelfRot = Quaternion.Euler(0f, RcCarShelf.Yaw, 0f);
            me.Motor.Teleport(RcCarShelf.Position + shelfRot * new Vector3(-0.3f, 0f, -1.15f), RcCarShelf.Yaw);
            yield return new WaitForSeconds(0.6f);
            mm.ServerRcPickup(me, 0);
            yield return new WaitForSeconds(0.4f);
            Debug.Log($"[RcShot] picked up: {RcState(mm, 0)}");
            me.Motor.Teleport(new Vector3(-13.6f, -3.4f, 54.6f), 270f);
            yield return new WaitForSeconds(0.8f);
            LocalPlayerController.TestSix = true;
            yield return new WaitForSeconds(2.4f);
            LocalPlayerController.TestRcActive = true;
            LocalPlayerController.TestRcInput = new Vector2(0f, 1f);
            yield return new WaitForSeconds(1.0f);
            LocalPlayerController.TestRcInput = Vector2.zero;
            yield return new WaitForSeconds(0.8f);
            Debug.Log($"[RcShot] driving: {RcState(mm, 0)} cam={me.RcCameraOn}");
            mm.TestGiveGlock();
            if (!string.IsNullOrEmpty(dir))
                StartCoroutine(SideBurst(dir, "rcshot", () => { var c = RcCar.Peek(0); return c != null ? c.transform.position + Vector3.up * 0.1f : me.transform.position; },
                    () => new Vector3(1.1f, 0.7f, -1.3f), 24, 0.3f));
            float t = 0f;
            while (t < 20f && mm.RcSlot(0).Mode != RcMode.Shot) { t += Time.deltaTime; yield return null; }
            Debug.Log($"[RcShot] after {t:0.0} s: {RcState(mm, 0)} cam={me.RcCameraOn}");
            yield return new WaitForSeconds(1.5f);
            var car = RcCar.Peek(0);
            Debug.Log($"[RcShot] wreck view={(car != null ? car.View.ToString() : "-")} cam={me.RcCameraOn} ctrl={me.RcControlling} anim={me.RcAnimating} slotOf={mm.RcSlotOf(me)} smoke={CountNamed("RcSmoke")}");
            LocalPlayerController.TestRcActive = false;
            // A second shot at the wreck must not do anything.
            yield return new WaitForSeconds(4f);
            Debug.Log($"[RcShot] done: {RcState(mm, 0)} move={me.Motor.MovementEnabled}");
        }

        /// <summary>-testrcshooter (seeker client): once the car drives and the Glock is in hand, walk up and shoot it.</summary>
        private System.Collections.IEnumerator RcShooterTest()
        {
            var mm = MatchManager.Instance;
            while (mm == null || mm.Phase.Value != MatchPhase.Seeking || NetPlayer.Local == null) { mm = MatchManager.Instance; yield return null; }
            var me = NetPlayer.Local;
            float wait = 0f;
            while (wait < 40f && !(me.HasGlock.Value && me.Held.Value == HeldItem.Glock && mm.RcSlot(0).Mode == RcMode.Driving))
            {
                wait += Time.deltaTime;
                yield return null;
            }
            Debug.Log($"[RcShooter] ready after {wait:0.0} s: glock={me.HasGlock.Value} held={me.Held.Value} ammo={me.Ammo.Value} {RcState(mm, 0)}");
            yield return new WaitForSeconds(1.2f);
            var car = RcCar.Peek(0);
            if (car == null) { Debug.Log("[RcShooter] no car"); yield break; }
            Vector3 cp = car.transform.position;
            Vector3 stand = cp + new Vector3(3.2f, 0f, -2.2f);
            Vector3 to = cp + Vector3.up * 0.09f - (stand + Vector3.up * 1.62f);
            me.Motor.Teleport(stand, YawTo(to), PitchTo(to));
            me.Motor.SetLook(YawTo(to), PitchTo(to));
            yield return new WaitForSeconds(0.5f);
            // Re-aim at where the car is now (it may still roll a bit).
            cp = car.transform.position;
            to = cp + Vector3.up * 0.09f - me.Camera.transform.position;
            me.Motor.SetLook(YawTo(to), PitchTo(to));
            yield return new WaitForSeconds(0.3f);
            int ammo = me.Ammo.Value;
            LocalPlayerController.TestShoot = true;
            yield return new WaitForSeconds(1.2f);
            Debug.Log($"[RcShooter] shot: ammo {ammo} -> {me.Ammo.Value} {RcState(mm, 0)} view={car.View}");
            Shot(ArgOr("-rcshots", ""), "shooter_view");
            yield return new WaitForSeconds(1.0f);
            LocalPlayerController.TestShoot = true; // at the wreck: nothing more happens
            yield return new WaitForSeconds(1.0f);
            Debug.Log($"[RcShooter] second shot: {RcState(mm, 0)}");
        }

        // ================================================================ blood

        /// <summary>-testblood (host, lobby): sprays, wall splash, a pool, then walk through it and count footprints.</summary>
        private System.Collections.IEnumerator BloodTest()
        {
            MatchManager mm = null;
            while (mm == null || mm.Phase.Value != MatchPhase.Lobby || NetPlayer.Local == null) { mm = MatchManager.Instance; yield return null; }
            yield return new WaitForSeconds(2.5f);
            var me = NetPlayer.Local;
            string dir = ArgOr("-bloodshots", "");

            // Sprays towards the west wall and a big pool on the floor.
            for (int i = 0; i < 4; i++)
            {
                BloodFx.Spray(new Vector3(-6.4f, 1.3f, 1.5f + i * 0.3f), new Vector3(-1f, 0.15f, 0.2f * (i - 1.5f)), 1.8f);
                yield return new WaitForSeconds(0.15f);
            }
            BloodFx.WallSplash(new Vector3(-6.8f, 1.4f, 0.2f), Vector3.left, 3f, 0.8f);
            var pool = BloodFx.Pool(new Vector3(-4f, 0f, -2f), 1.1f, 3f);
            yield return new WaitForSeconds(3.5f);
            Debug.Log($"[BloodTest] splats={CountNamed("BloodSplat")} runs={CountNamed("BloodRun")} drops={CountNamed("BloodDrop")} pools={BloodDecal.Pools.Count} poolR={(pool != null ? pool.Radius : -1f):0.00}");
            CamShot(dir, "blood_wall", new Vector3(-4.2f, 1.6f, 0.6f), new Vector3(-8f, 0.9f, 1.4f));
            CamShot(dir, "blood_pool", new Vector3(-2.6f, 1.5f, -3.2f), new Vector3(-4f, 0f, -2f));

            // Stand in the pool, then walk east 10 m in small steps.
            me.Motor.Teleport(new Vector3(-4f, 0.02f, -2f), 90f);
            yield return new WaitForSeconds(1.2f);
            float soaked = BloodTracks.Instance != null ? BloodTracks.Instance.BloodLeft(me) : -1f;
            Debug.Log($"[BloodTest] in pool: soles={soaked:0.0} m grounded={me.Motor.IsGrounded}");
            Vector3 p = new Vector3(-4f, 0.02f, -2f);
            for (int i = 0; i < 300; i++)
            {
                p += new Vector3(0.035f, 0f, 0f); // ~2.1 m/s at 60 fps
                me.Motor.Teleport(p, 90f);
                yield return null;
            }
            yield return new WaitForSeconds(0.3f);
            int prints = CountNamed("BloodFoot");
            Debug.Log($"[BloodTest] walked {p.x + 4f:0.0} m: footprints={prints} left={(BloodTracks.Instance != null ? BloodTracks.Instance.BloodLeft(me) : -1f):0.0}");
            me.Motor.Teleport(new Vector3(7f, 0.02f, 3f), 0f);
            yield return new WaitForSeconds(0.3f);
            CamShot(dir, "blood_footprints", new Vector3(3.6f, 2.0f, -3.6f), new Vector3(1.2f, 0f, -2f));
            // Stump spurt on a dummy joint above the floor.
            var j = new GameObject("TestJoint").transform;
            j.position = new Vector3(2f, 1.0f, -1.5f);
            BloodFx.Spurt(j, new Vector3(0.4f, 0.3f, 0f), 4f);
            BloodFx.Stump(j, j.position, Quaternion.identity, new Vector3(0.13f, 0.06f, 0.13f));
            yield return new WaitForSeconds(0.5f);
            CamShot(dir, "blood_spurt_a", new Vector3(2.6f, 1.3f, -3.2f), j.position);
            yield return new WaitForSeconds(1.1f);
            CamShot(dir, "blood_spurt_b", new Vector3(2.6f, 1.3f, -3.2f), j.position + Vector3.down * 0.4f);
            yield return new WaitForSeconds(3f);
            Debug.Log($"[BloodTest] done: splats={CountNamed("BloodSplat")} decalsTotal={CountNamed("BloodSplat") + CountNamed("BloodRun") + CountNamed("BloodDrop") + CountNamed("BloodFoot")}");
        }

        // ================================================================ lobby: board + mirror

        /// <summary>-testlobbyupd (host, lobby): new settings screen and the mirror (near / far, flashlight bounce).</summary>
        private System.Collections.IEnumerator LobbyUpdateTest()
        {
            MatchManager mm = null;
            while (mm == null || mm.Phase.Value != MatchPhase.Lobby || NetPlayer.Local == null) { mm = MatchManager.Instance; yield return null; }
            yield return new WaitForSeconds(2.5f);
            var me = NetPlayer.Local;
            string dir = ArgOr("-updshots", "");

            // Settings screen.
            var board = LobbySettingsBoard.Instance;
            Debug.Log($"[LobbyUpd] board={(board != null)} screen={CountNamed("BoardScreen")}");
            if (board != null)
            {
                Vector3 bp = board.transform.position;
                me.Motor.Teleport(new Vector3(bp.x, 0.02f, bp.z - 3.4f), 0f);
                me.Motor.SetLook(0f, 4f);
                yield return new WaitForSeconds(0.8f);
                Shot(dir, "board_front");
                me.Motor.Teleport(new Vector3(bp.x - 2.6f, 0.02f, bp.z - 2.4f), 45f, -5f);
                yield return new WaitForSeconds(0.6f);
                Shot(dir, "board_side");
                var s = mm.Settings.Value;
                s.Mode = (byte)GameMode.RcRace;
                mm.SetSettingsRpc(s);
                yield return new WaitForSeconds(0.8f);
                me.Motor.Teleport(new Vector3(bp.x, 0.02f, bp.z - 3.4f), 0f, -6f);
                yield return new WaitForSeconds(0.6f);
                Shot(dir, "board_rcrace");
                s.Mode = (byte)GameMode.HideAndSeek;
                mm.SetSettingsRpc(s);
            }

            // Mirror on the south wall (0.8, 1.35, -5.97), faces +Z.
            var mirror = FindAnyObjectByType<PlanarMirror>();
            Debug.Log($"[LobbyUpd] mirror={(mirror != null)}");
            if (mirror != null)
            {
                Vector3 mp = mirror.transform.position;
                if (me.Flashlight != null) me.Flashlight.SetOn(true, false);
                foreach (float dist in new[] { 2.5f, 7f, 11.5f })
                {
                    Vector3 stand = new Vector3(mp.x + 0.6f, 0.02f, mp.z + dist);
                    Vector3 to = mp - (stand + Vector3.up * 1.62f);
                    me.Motor.Teleport(stand, YawTo(to), PitchTo(to));
                    me.Motor.SetLook(YawTo(to), PitchTo(to));
                    yield return new WaitForSeconds(0.8f);
                    int lit = 0;
                    foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                        if (l.name == "MirrorBounceLight" && l.enabled) lit++;
                    Debug.Log($"[LobbyUpd] mirror at {dist:0.0} m: bounceLights={lit} flashlight={(me.Flashlight != null && me.Flashlight.IsOn)}");
                    Shot(dir, $"mirror_{dist:0.0}m");
                }
                // Light the floor in front with the reflected beam: aim at the mirror from the side.
                Vector3 side = new Vector3(mp.x - 3f, 0.02f, mp.z + 3.5f);
                Vector3 aim = mp + new Vector3(0f, -0.4f, 0f) - (side + Vector3.up * 1.62f);
                me.Motor.Teleport(side, YawTo(aim), PitchTo(aim));
                me.Motor.SetLook(YawTo(aim), PitchTo(aim));
                yield return new WaitForSeconds(0.8f);
                Shot(dir, "mirror_bounce_side");
            }
            Debug.Log("[LobbyUpd] done");
        }

        // ================================================================ chainsaw pull-start

        /// <summary>-testsawanim (host seeker via -forceseeker + any hider): take the saw, film the pull-start, rev.</summary>
        private System.Collections.IEnumerator SawAnimTest()
        {
            var mm = MatchManager.Instance;
            while (mm == null || mm.Phase.Value != MatchPhase.Seeking || NetPlayer.Local == null) { mm = MatchManager.Instance; yield return null; }
            yield return new WaitForSeconds(1f);
            var me = NetPlayer.Local;
            if (!me.IsSeeker) { Debug.Log("[SawAnim] host must seek (-forceseeker)"); yield break; }
            string dir = ArgOr("-sawshots", "");
            TestLight(ChainsawShelf.Position + new Vector3(0f, 2.2f, -1.5f), 10f, 1.6f);
            me.Motor.Teleport(ChainsawShelf.Position + new Vector3(0f, 0.05f, -1.8f), 0f);
            me.Motor.SetLook(0f, -12f);
            yield return new WaitForSeconds(1.5f);
            mm.ServerPickupChainsaw(me);
            float t0 = Time.time;
            float[] at = { 0.15f, 0.3f, 0.45f, 0.65f, 0.8f, 1.0f, 1.25f, 1.6f };
            foreach (float a in at)
            {
                while (Time.time - t0 < a) yield return null;
                Shot(dir, $"saw_start_{a:0.00}");
            }
            var bca = me.Actions as BlockyCharacterActions;
            Debug.Log($"[SawAnim] held={me.Held.Value} startT={(bca != null ? bca.SawStartTime : -1f):0.00}");
            yield return new WaitForSeconds(0.6f);
            me.SawRev.Value = true;
            yield return new WaitForSeconds(0.5f);
            Shot(dir, "saw_rev_a");
            yield return new WaitForSeconds(0.25f);
            Shot(dir, "saw_rev_b");
            yield return new WaitForSeconds(0.8f);
            me.SawRev.Value = false;
            me.BloodySaw();
            yield return new WaitForSeconds(0.4f);
            Shot(dir, "saw_bloody");
            Debug.Log($"[SawAnim] fpp done exhaust={CountNamed("Exhaust")} teeth={CountNamed("Tooth0")}");

            // Third person: show the own body, put the saw away and take it out again (pull-start from the side).
            mm.ServerEquip(me, HeldItem.Knife);
            yield return new WaitForSeconds(1.2f);
            me.SetCinematicBody(true);
            mm.ServerEquip(me, HeldItem.Chainsaw);
            t0 = Time.time;
            Vector3 eye = me.transform.position + me.transform.right * 1.3f + me.transform.forward * 1.1f + Vector3.up * 1.3f;
            foreach (float a in new[] { 0.2f, 0.35f, 0.5f, 0.75f, 0.9f, 1.4f })
            {
                while (Time.time - t0 < a) yield return null;
                CamShot(dir, $"saw_tpp_{a:0.00}", eye, me.transform.position + Vector3.up * 1.0f + me.transform.forward * 0.3f);
            }
            me.SawRev.Value = true;
            yield return new WaitForSeconds(0.6f);
            CamShot(dir, "saw_tpp_rev", eye, me.transform.position + Vector3.up * 1.0f + me.transform.forward * 0.3f);
            me.SawRev.Value = false;
            me.SetCinematicBody(false);
            Debug.Log("[SawAnim] done");
        }

        /// <summary>-testsawwatch (other peer): films the seeker taking the saw out (pull-start seen from outside).</summary>
        private System.Collections.IEnumerator SawWatchTest()
        {
            var mm = MatchManager.Instance;
            while (mm == null || mm.Phase.Value != MatchPhase.Seeking || NetPlayer.Local == null) { mm = MatchManager.Instance; yield return null; }
            string dir = ArgOr("-sawshots", "");
            TestLight(ChainsawShelf.Position + new Vector3(1.2f, 2.0f, -3.2f), 10f, 1.8f);
            NetPlayer seeker = null;
            foreach (var p in NetPlayer.All) if (p != null && p.IsSeeker) seeker = p;
            if (seeker == null) yield break;
            for (int round = 0; round < 2; round++)
            {
                float wait = 0f;
                while (seeker.Held.Value == HeldItem.Chainsaw && wait < 20f) { wait += Time.deltaTime; yield return null; }
                while (seeker.Held.Value != HeldItem.Chainsaw && wait < 40f) { wait += Time.deltaTime; yield return null; }
                float t0 = Time.time;
                foreach (float a in new[] { 0.2f, 0.32f, 0.45f, 0.7f, 0.8f, 1.0f, 1.5f })
                {
                    while (Time.time - t0 < a) yield return null;
                    Vector3 c = seeker.transform.position + Vector3.up * 1.0f;
                    Vector3 eye = c + seeker.transform.right * 1.6f + seeker.transform.forward * 1.5f + Vector3.up * 0.35f;
                    CamShot(dir, $"watch{round}_{a:0.00}", eye, c + seeker.transform.forward * 0.25f);
                }
            }
            Debug.Log("[SawWatch] done");
        }

        /// <summary>-testgore (victim, with -testsawleg): once dead after the saw, photo of the body, gibs and blood from above.</summary>
        private System.Collections.IEnumerator GoreWatchTest()
        {
            while (NetPlayer.Local == null) yield return null;
            var me = NetPlayer.Local;
            string dir = ArgOr("-goreshots", "");
            bool sawFirst = false;
            float t = 0f;
            while (t < 120f)
            {
                t += Time.deltaTime;
                if (!sawFirst && me.Severed.Value != 0)
                {
                    sawFirst = true;
                    Debug.Log($"[Gore] first cut: gibs={CountNamed("SeveredLimb")} stumps={CountNamed("BloodStump")} spurts={CountNamed("BloodSpurt")}");
                }
                if (me.Life.Value == LifeState.Dead) break;
                yield return null;
            }
            Vector3 c = me.transform.position;
            TestLight(c + Vector3.up * 2.5f, 8f, 1.4f);
            for (int i = 0; i < 4; i++)
            {
                CamShot(dir, $"gore_{i}", c + new Vector3(1.6f, 2.4f, -1.6f), c + Vector3.down * 0.2f);
                CamShot(dir, $"gore_side_{i}", c + new Vector3(-2.2f, 0.9f, 0.4f), c + Vector3.down * 0.3f);
                yield return new WaitForSeconds(1.5f);
            }
            Debug.Log($"[Gore] dead: severed={me.Severed.Value} gibs={CountNamed("SeveredLimb")} stumps={CountNamed("BloodStump")} " +
                      $"spurts={CountNamed("BloodSpurt")} gibBleed={CountNamed("GibBleed")} pools={BloodDecal.Pools.Count} splats={CountNamed("BloodSplat")}");
        }

        // ================================================================ RC race

        /// <summary>-testrace (all peers; host also picks the mode): autopilot laps, standings, photos (-raceshots).</summary>
        private System.Collections.IEnumerator RaceTest()
        {
            MatchManager mm = null;
            while (mm == null || !mm.IsSpawned) { mm = MatchManager.Instance; yield return null; }
            if (NetworkManager.Singleton.IsServer)
            {
                var s = mm.Settings.Value;
                s.Mode = (byte)GameMode.RcRace;
                s.Laps = (byte)(int.TryParse(ArgOr("-racelaps", "1"), out int l) ? l : 1);
                mm.Settings.Value = s;
                if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autostart") < 0)
                {
                    yield return new WaitForSeconds(2.5f);
                    mm.RequestStartRpc();
                }
            }
            string dir = ArgOr("-raceshots", "");
            while (mm.Phase.Value != MatchPhase.Race) yield return null;
            yield return new WaitForSeconds(1f);
            var rs = RaceSystem.Instance;
            Debug.Log($"[RaceTest] race phase, system={(rs != null)} start in {mm.RaceStartAt.Value - mm.Now:0.0} s, me at {NetPlayer.Local.transform.position}");
            if (rs != null && NetworkManager.Singleton.IsServer)
            {
                var o = RaceTrack.Origin;
                CamShot(dir, "race_overview", o + new Vector3(0f, 38f, -46f), o + new Vector3(0f, -2f, -2f));
                CamShot(dir, "race_stand", o + new Vector3(14f, 2.5f, -12f), o + new Vector3(6f, 0.6f, -22f));
                CamShot(dir, "race_grid", o + new Vector3(-3.5f, 1.2f, -14.2f), o + new Vector3(0.5f, 0f, -15.2f));
            }
            Shot(dir, "race_countdown_" + LocalProfile.Nick);
            RaceSystem.TestAutopilot = true;
            int n = 0;
            while (mm.Phase.Value == MatchPhase.Race && n < 200)
            {
                yield return new WaitForSeconds(3f);
                n++;
                var me = NetPlayer.Local;
                Debug.Log($"[RaceTest] t={n * 3} s cps={me.RaceCps.Value} laps={(rs != null ? rs.Laps(me) : -1)} car={(rs != null ? rs.CarPos(me).ToString("F1") : "-")}");
                if (n % 4 == 1) Shot(dir, $"race_{LocalProfile.Nick}_{n:00}");
            }
            RaceSystem.TestAutopilot = false;
            var w = NetPlayer.Find(mm.RaceWinnerId.Value);
            Debug.Log($"[RaceTest] ended: phase={mm.Phase.Value} winner={(w != null ? w.Nick : "-")}");
            yield return new WaitForSeconds(1f);
            Shot(dir, "race_end_" + LocalProfile.Nick);
        }

        // ================================================================ security crate

        /// <summary>-testpush (host, game): push the crate of level 0 aside, walk into the security room (-pushshots).</summary>
        private System.Collections.IEnumerator PushTest()
        {
            var mm = MatchManager.Instance;
            while (mm == null || mm.Phase.Value != MatchPhase.Seeking || NetPlayer.Local == null || SecurityRooms.Instance == null) { mm = MatchManager.Instance; yield return null; }
            yield return new WaitForSeconds(1f);
            var me = NetPlayer.Local;
            string dir = ArgOr("-pushshots", "");
            Vector3 stand = SecurityRooms.PushStand(0);
            TestLight(stand + new Vector3(-1f, 2.4f, -2.5f), 10f, 1.6f);
            me.Motor.Teleport(stand + new Vector3(0.3f, 0f, -1.4f), 300f);
            me.Motor.SetLook(300f, -8f);
            yield return new WaitForSeconds(0.8f);
            var lpc = LocalPlayerController.Instance;
            Debug.Log($"[PushTest] prompt={lpc.Prompt} text={lpc.PromptText} crates={mm.SecurityCrates.Value}");
            Vector3 side = stand + new Vector3(-0.8f, 1.5f, -3.4f);
            Vector3 look = stand + new Vector3(-1.2f, 0.8f, 0.5f);
            CamShot(dir, "push_before", side, look);
            me.RequestPushCrateRpc(0);
            for (int i = 0; i < 5; i++)
            {
                yield return new WaitForSeconds(0.4f);
                CamShot(dir, $"push_{i}", side, look);
            }
            yield return new WaitForSeconds(0.6f);
            Debug.Log($"[PushTest] after: crates={mm.SecurityCrates.Value} busy={me.Busy.Value} me={me.transform.position} crateOpen={SecurityRooms.Instance.CrateOpenNow(0)}");
            // Walk in through the door gap.
            Vector3 door = SecurityRooms.PushStand(0) + new Vector3(-0.85f, 0f, 1.2f);
            for (int i = 0; i < 60; i++)
            {
                me.Motor.Teleport(Vector3.Lerp(door + new Vector3(0f, 0f, -1.4f), door + new Vector3(0f, 0f, 2.2f), i / 59f), 0f);
                yield return null;
            }
            yield return new WaitForSeconds(0.5f);
            Debug.Log($"[PushTest] inside: me={me.transform.position} watched={mm.SecurityWatched.Value}");
            Shot(dir, "push_inside");
            CamShot(dir, "push_sign", stand + new Vector3(-1.2f, 1.6f, -4.5f), stand + new Vector3(-1.2f, 2.0f, 0.6f));
        }

        // ================================================================ Glock finisher

        /// <summary>-testglockfin (host -forceseeker + one hider): wound the hider, shoot again: finisher cutscene (-glockshots).</summary>
        private System.Collections.IEnumerator GlockFinisherTest()
        {
            var mm = MatchManager.Instance;
            while (mm == null || mm.Phase.Value != MatchPhase.Seeking || NetPlayer.Local == null) { mm = MatchManager.Instance; yield return null; }
            yield return new WaitForSeconds(1f);
            var me = NetPlayer.Local;
            NetPlayer h = null;
            foreach (var p in NetPlayer.All) if (p != null && p.IsHider) h = p;
            if (!me.IsSeeker || h == null) { Debug.Log("[GlockFin] need host seeker + hider"); yield break; }
            string dir = ArgOr("-glockshots", "");
            mm.TestGiveGlock();
            yield return new WaitForSeconds(0.6f);
            Vector3 hp = h.transform.position;
            TestLight(hp + Vector3.up * 2.6f, 9f, 1.6f);
            me.Motor.Teleport(hp + h.transform.forward * 3.2f, Quaternion.LookRotation(-h.transform.forward).eulerAngles.y);
            yield return new WaitForSeconds(0.5f);
            mm.ServerShoot(me, me.EyePosition, (h.ChestPosition - me.EyePosition).normalized, h, h.ChestPosition, -1);
            yield return new WaitForSeconds(0.4f);
            Debug.Log($"[GlockFin] after shot 1: limping={h.Limping.Value} life={h.Life.Value}");
            yield return new WaitForSeconds(0.6f);
            mm.ServerShoot(me, me.EyePosition, (h.ChestPosition - me.EyePosition).normalized, h, h.ChestPosition, -1);
            for (int i = 0; i < 15; i++)
            {
                yield return new WaitForSeconds(0.5f);
                Vector3 mid = (me.transform.position + h.transform.position) * 0.5f + Vector3.up * 0.9f;
                Vector3 side = Vector3.Cross(Vector3.up, (h.transform.position - me.transform.position).normalized);
                CamShot(dir, $"glockfin_{i:00}", mid + side * 3.2f + Vector3.up * 0.5f, mid);
            }
            Debug.Log($"[GlockFin] after finisher: life={h.Life.Value} phase={mm.Phase.Value} winner={mm.Winner.Value} busy={me.Busy.Value}");
        }

        // ================================================================ sound between floors

        /// <summary>-testfloors (host, game scene): a sound one floor below must be silent, one on the same floor audible.</summary>
        private System.Collections.IEnumerator FloorAudioTest()
        {
            var mm = MatchManager.Instance;
            while (mm == null || mm.Phase.Value != MatchPhase.Seeking || NetPlayer.Local == null) { mm = MatchManager.Instance; yield return null; }
            yield return new WaitForSeconds(1.5f);
            var me = NetPlayer.Local;
            me.Motor.Teleport(new Vector3(-30f, 0.02f, 20f), 0f);
            yield return new WaitForSeconds(1f);
            Transform At(Vector3 p) { var g = new GameObject("TestSound").transform; g.position = p; return g; }
            var same = GameAudio.StartLoop(GameSfx.CarAlarm, At(new Vector3(-30f, 1f, 26f)), 1f);
            var below = GameAudio.StartLoop(GameSfx.CarAlarm, At(new Vector3(-30f, -3.4f + 1f, 21f)), 1f);
            var deep = GameAudio.StartLoop(GameSfx.CarAlarm, At(new Vector3(-30f, -6.8f + 1f, 21f)), 1f);
            yield return new WaitForSeconds(1.5f);
            Debug.Log($"[Floors] listener y={me.Camera.transform.position.y:0.0}: same floor vol={(same != null ? same.volume : -1f):0.000} " +
                      $"floor -1 vol={(below != null ? below.volume : -1f):0.000} floor -2 vol={(deep != null ? deep.volume : -1f):0.000} " +
                      $"diff(-1)={AudioFloors.DifferentFloor(me.Camera.transform.position, new Vector3(-30f, -2.4f, 21f))}");
            GameAudio.StopLoop(same, 0.1f);
            GameAudio.StopLoop(below, 0.1f);
            GameAudio.StopLoop(deep, 0.1f);
        }
    }
}
