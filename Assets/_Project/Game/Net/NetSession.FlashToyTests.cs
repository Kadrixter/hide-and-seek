using System;
using System.Collections;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Tests for the lobby flashlight box and the toy slime blaster:
    ///  -testflashbox (client in the lobby): no lamp at first, T does nothing, E at the box = take (first-person shots),
    ///      ready + light works, put back = not ready, light off, take again.
    ///  -fbwatch (host): lit side shots of the other player at the box + an overview of the box.
    ///  -testtoygun (host, -forcehider -autostart -hidetime N, + a seeker client with -toywatch): gun on the map, pickup,
    ///      gun out (tune playing), goo shot at the seeker (slow, no knife), other floors, empty gun.
    ///  -toywatch (seeker client): side shots of the hider with the gun, own screen while gooed, tune volume per floor.
    ///  -toyshots &lt;folder&gt;: where all of them write their PNGs.
    /// </summary>
    public partial class NetSession
    {
        private void StartFlashToyTests(Func<string, bool> has)
        {
            if (has("-testflashbox")) StartCoroutine(FlashBoxTest());
            if (has("-fbwatch")) StartCoroutine(FlashBoxWatch());
            if (has("-testtoygun")) StartCoroutine(ToyGunTest());
            if (has("-toywatch")) StartCoroutine(ToyWatch());
        }

        private static string ToyShots => ArgOr("-toyshots", "");

        private static void LookAt(NetPlayer me, Vector3 target)
        {
            Vector3 d = target - me.EyePosition;
            me.Motor.SetLook(Quaternion.LookRotation(new Vector3(d.x, 0f, d.z)).eulerAngles.y, Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg);
        }

        // ================================================================ flashlight box

        private IEnumerator FlashBoxTest()
        {
            while (MatchManager.Instance == null || NetPlayer.Local == null || FlashlightBox.Instance == null) yield return null;
            yield return new WaitForSeconds(3f);
            var me = NetPlayer.Local;
            string dir = ToyShots;
            Debug.Log($"[FlashTest] start: ready={me.Ready.Value} hasLight={me.HasFlashlight} lampWithPlayer={me.LampWithPlayer}");
            me.TeleportLocal(FlashlightBox.StandPos + new Vector3(0.3f, 0f, 1.1f), 180f);
            yield return new WaitForSeconds(0.4f);
            LookAt(me, FlashlightBox.AimPoint);
            yield return new WaitForSeconds(1.6f);
            var lpc = LocalPlayerController.Instance;
            Debug.Log($"[FlashTest] prompt: {lpc.Prompt} '{lpc.PromptText}'");
            Shot(dir, "fb_prompt");
            me.Flashlight.Toggle();
            yield return null;
            yield return null;
            Debug.Log($"[FlashTest] T without a lamp: on={me.Flashlight.IsOn} (expected False)");

            me.RequestFlashBoxRpc(true);
            float t0 = Time.time;
            foreach (float a in new[] { 0.3f, 0.5f, 0.8f, 1.05f, 1.25f, 1.5f, 1.9f })
            {
                while (Time.time - t0 < a) yield return null;
                Shot(dir, $"fb_fp_take_{a:0.00}");
            }
            yield return new WaitForSeconds(0.8f);
            Debug.Log($"[FlashTest] after take: ready={me.Ready.Value} hasLight={me.HasFlashlight} busy={me.Busy.Value}");
            me.Flashlight.SetOn(true, true);
            yield return new WaitForSeconds(0.4f);
            Debug.Log($"[FlashTest] light on: {me.Flashlight.IsOn}");
            LookAt(me, FlashlightBox.AimPoint + new Vector3(0f, 0.4f, 0f));
            yield return new WaitForSeconds(0.3f);
            Shot(dir, "fb_light_on");
            yield return new WaitForSeconds(1.5f);

            me.RequestFlashBoxRpc(false);
            t0 = Time.time;
            foreach (float a in new[] { 0.3f, 0.6f, 0.95f })
            {
                while (Time.time - t0 < a) yield return null;
                Shot(dir, $"fb_fp_put_{a:0.00}");
            }
            yield return new WaitForSeconds(1.2f);
            Debug.Log($"[FlashTest] after put back: ready={me.Ready.Value} hasLight={me.HasFlashlight} lightOn={me.Flashlight.IsOn}");
            yield return new WaitForSeconds(1.5f);
            me.RequestFlashBoxRpc(true);
            yield return new WaitForSeconds(2.6f);
            Debug.Log($"[FlashTest] taken again: ready={me.Ready.Value} everyoneReady={LobbyButton.EveryoneReady(out int r, out int n)} {r}/{n}");
            Debug.Log("[FlashTest] done");
        }

        private IEnumerator FlashBoxWatch()
        {
            while (MatchManager.Instance == null || FlashlightBox.Instance == null) yield return null;
            string dir = ToyShots;
            yield return new WaitForSeconds(2f);
            Vector3 box = FlashlightBox.AimPoint;
            ItemLitShot(dir, "fb_box_overview", box + new Vector3(1.6f, 0.7f, 2.4f), box);
            Vector3 crate = FlashlightBox.CratePos + Vector3.up * 0.08f;
            ItemLitShot(dir, "fb_box_close", box + new Vector3(0.35f, 0.45f, 0.9f), box - new Vector3(0f, 0.1f, 0f));
            ItemLitShot(dir, "fb_box_front", crate + new Vector3(0f, 0.12f, 0.75f), crate);
            ItemLitShot(dir, "fb_box_left", crate + new Vector3(-0.7f, 0.2f, 0.25f), crate);
            ItemLitShot(dir, "fb_box_right", crate + new Vector3(0.7f, 0.15f, 0.2f), crate);
            ItemLitShot(dir, "fb_box_top", crate + new Vector3(0.05f, 0.75f, 0.3f), crate);
            int n = 0;
            float end = Time.time + 40f;
            float lastT = -1f;
            while (Time.time < end && n < 40)
            {
                yield return null;
                foreach (var p in NetPlayer.All)
                {
                    if (p == null || p == NetPlayer.Local || p.FlashAnimTime < 0f) continue;
                    if (p.FlashAnimTime - lastT < 0.22f && lastT >= 0f && p.FlashAnimTime > lastT) continue;
                    lastT = p.FlashAnimTime;
                    Vector3 chest = p.transform.position + Vector3.up * 1.1f;
                    Vector3 cam = chest - p.transform.right * 1.45f + p.transform.forward * 0.3f + Vector3.up * 0.3f;
                    ItemLitShot(dir, $"fb_tp_{(p.FlashAnimTake ? "take" : "put")}_{n++:00}_{p.FlashAnimTime:0.00}", cam, chest);
                }
            }
            int ready = 0;
            foreach (var p in NetPlayer.All) if (p != null && p.Ready.Value) ready++;
            Debug.Log($"[FlashWatch] done, {n} shots, ready players={ready}");
        }

        // ================================================================ toy slime blaster

        private IEnumerator ToyGunTest()
        {
            yield return WaitSeeking();
            var mm = MatchManager.Instance;
            var me = NetPlayer.Local;
            string dir = ToyShots;
            NetPlayer seeker = null;
            foreach (var p in NetPlayer.All) if (p != null && p.IsSeeker) seeker = p;
            if (!me.IsHider || seeker == null) { Debug.Log("[ToyTest] need host hider (-forcehider) + seeker"); yield break; }
            Debug.Log($"[ToyTest] guns on the map: {mm.ToyGuns.Count}");
            for (int i = 0; i < mm.ToyGuns.Count; i++) Debug.Log($"[ToyTest]  gun {mm.ToyGuns[i].Id} at {mm.ToyGuns[i].Pos} level {FloorAcoustics.FloorOf(mm.ToyGuns[i].Pos.y)}");
            if (mm.ToyGuns.Count == 0) mm.SpawnToyGuns(2);
            yield return new WaitForSeconds(0.5f);

            var gun = mm.ToyGuns[0];
            Vector3 fwd = Quaternion.Euler(0f, gun.Yaw, 0f) * Vector3.forward;
            ItemLitShot(dir, "toy_on_map", gun.Pos + new Vector3(0.9f, 0.7f, 0.6f), gun.Pos);
            ItemLitShot(dir, "toy_on_map_close", gun.Pos + new Vector3(0.35f, 0.3f, 0.25f), gun.Pos + Vector3.up * 0.04f);
            Vector3 stand = FreeCam(gun.Pos + Vector3.up * 0.05f, new Vector3(1.2f, 0f, 0.4f));
            stand.y = gun.Pos.y + 0.05f;
            me.TeleportLocal(stand, 0f);
            yield return new WaitForSeconds(0.5f);
            LookAt(me, gun.Pos);
            yield return new WaitForSeconds(0.8f);
            var lpc = LocalPlayerController.Instance;
            Debug.Log($"[ToyTest] prompt: {lpc.Prompt} '{lpc.PromptText}'");
            Shot(dir, "toy_prompt");
            me.RequestPickupToyGunRpc(gun.Id);
            yield return new WaitForSeconds(0.6f);
            Debug.Log($"[ToyTest] picked up: ammo={me.ToyAmmo.Value} taken={mm.ToyGuns[0].Taken}");

            // Out in an open spot (5), first-person view.
            if (OpenSpot(out var open)) { me.TeleportLocal(open, 0f); yield return new WaitForSeconds(0.8f); }
            me.Motor.SetLook(me.Motor.LookYaw, 0f);
            ToyGunControl.TestToggle = true;
            yield return new WaitForSeconds(0.8f);
            Debug.Log($"[ToyTest] out={me.ToyGunOut.Value} shown={me.ToyGunShown} tunes playing={ToyGunAudio.Playing}");
            Shot(dir, "toy_fp_hold");
            yield return new WaitForSeconds(2.5f);

            // Seeker 6 m ahead along the freest flat direction: shoot him.
            Vector3 aimDir = me.transform.forward;
            float freeBest = 0f;
            for (int i = 0; i < 16; i++)
            {
                Vector3 d = Quaternion.Euler(0f, i * 22.5f, 0f) * Vector3.forward;
                float free = Physics.SphereCast(me.transform.position + Vector3.up * 1.0f, 0.4f, d, out var fh, 9f, ~((1 << 9) | (1 << PlayerAvatarBinder.LocalBodyLayer)), QueryTriggerInteraction.Ignore) ? fh.distance : 9f;
                bool flat = Physics.Raycast(me.transform.position + d * 6f + Vector3.up * 1f, Vector3.down, out var gh, 2f) && Mathf.Abs(gh.point.y - me.transform.position.y) < 0.15f;
                if (flat && free > freeBest) { freeBest = free; aimDir = d; }
            }
            me.TeleportLocal(me.transform.position, Quaternion.LookRotation(aimDir).eulerAngles.y);
            Vector3 target = me.transform.position + aimDir * 6f;
            seeker.TeleportRpc(target, Quaternion.LookRotation(-aimDir).eulerAngles.y);
            yield return new WaitForSeconds(1.2f);
            LookAt(me, seeker.transform.position + Vector3.up * 1.15f);
            yield return new WaitForSeconds(0.3f);
            ToyGunControl.TestShoot = true;
            yield return new WaitForSeconds(0.12f);
            Shot(dir, "toy_fp_shot_a");
            yield return new WaitForSeconds(0.15f);
            Shot(dir, "toy_fp_shot_b");
            yield return new WaitForSeconds(0.6f);
            Debug.Log($"[ToyTest] after shot: ammo={me.ToyAmmo.Value} seekerGooed={seeker.Gooed} left={seeker.GooLeft:0.0} speedScale={seeker.GooSpeedScale:0.00}");
            Shot(dir, "toy_fp_seeker_gooed");
            Vector3 sc = seeker.transform.position + Vector3.up * 1.1f;
            ItemLitShot(dir, "toy_seeker_gooed", sc + seeker.transform.forward * 1.8f + seeker.transform.right * 0.8f + Vector3.up * 0.3f, sc);
            seeker.AttackReadyAt.Value = 0;
            seeker.Held.Value = HeldItem.Knife;
            mm.ServerKnife(seeker, me);
            yield return new WaitForSeconds(0.3f);
            Debug.Log($"[ToyTest] knife while gooed: busy={seeker.Busy.Value} limping={me.Limping.Value} (expected False/False)");
            yield return new WaitForSeconds(ToyTuning.GooTime);
            Debug.Log($"[ToyTest] after goo time: gooed={seeker.Gooed} speedScale={seeker.GooSpeedScale:0.00}");

            // Other floors: the seeker listens (-toywatch logs the volume).
            int seekerLevel = FloorAcoustics.FloorOf(seeker.transform.position.y), visited = 0;
            foreach (var sp in SpawnPoints.Hider())
            {
                int lv = FloorAcoustics.FloorOf(sp.position.y);
                if (lv == seekerLevel || (visited & (1 << -lv)) != 0) continue;
                visited |= 1 << -lv;
                me.TeleportLocal(sp.position + Vector3.up * 0.05f, 0f);
                Debug.Log($"[ToyTest] hider now on level {FloorAcoustics.FloorOf(sp.position.y)} (seeker on {FloorAcoustics.FloorOf(seeker.transform.position.y)})");
                yield return new WaitForSeconds(3f);
            }
            if (OpenSpot(out open)) me.TeleportLocal(open, 0f);
            yield return new WaitForSeconds(1f);

            // Empty the gun at the nearest wall (splats), the empty gun is thrown away.
            Vector3 wallDir = me.transform.forward;
            float wd = 99f;
            for (int i = 0; i < 16; i++)
            {
                Vector3 d = Quaternion.Euler(0f, i * 22.5f, 0f) * Vector3.forward;
                if (Physics.Raycast(me.EyePosition, d, out var h, 12f, ~((1 << 9) | (1 << PlayerAvatarBinder.LocalBodyLayer)), QueryTriggerInteraction.Ignore) && h.distance > 2.5f && h.distance < wd)
                { wd = h.distance; wallDir = d; }
            }
            Vector3 wallPt = me.EyePosition + wallDir * Mathf.Min(wd, 12f);
            int shots = 0;
            while (me.ToyAmmo.Value > 0 && shots < 12)
            {
                LookAt(me, wallPt + new Vector3(UnityEngine.Random.Range(-0.6f, 0.6f), UnityEngine.Random.Range(-0.3f, 0.4f), 0f));
                ToyGunControl.TestShoot = true;
                shots++;
                yield return new WaitForSeconds(0.65f);
            }
            yield return new WaitForSeconds(0.8f);
            Debug.Log($"[ToyTest] emptied after {shots} more shots: ammo={me.ToyAmmo.Value} hasGun={me.HasToyGun} out={me.ToyGunOut.Value}");
            ItemLitShot(dir, "toy_wall_splats", me.EyePosition + wallDir * Mathf.Max(1f, wd - 2.5f) + Vector3.up * 0.2f, wallPt);
            Shot(dir, "toy_fp_empty");
            Debug.Log("[ToyTest] done");
        }

        private IEnumerator ToyWatch()
        {
            yield return WaitSeeking();
            string dir = ToyShots;
            int holdShots = 0, gooShots = 0;
            float nextLog = 0f, nextHold = 0f;
            float end = Time.time + 75f;
            while (Time.time < end)
            {
                yield return null;
                var me = NetPlayer.Local;
                NetPlayer h = null;
                foreach (var p in NetPlayer.All) if (p != null && p != me && p.IsHider) h = p;
                if (me == null || h == null) continue;
                if (h.ToyGunShown && holdShots < 6 && Time.time >= nextHold && h.Busy.Value == false)
                {
                    nextHold = Time.time + 0.45f;
                    Vector3 chest = h.transform.position + Vector3.up * 1.15f;
                    Vector3 off = holdShots % 2 == 0 ? h.transform.right * 1.9f + h.transform.forward * 0.9f : h.transform.forward * 2.2f - h.transform.right * 0.7f;
                    ItemLitShot(dir, $"toy_tp_hold_{holdShots++}", FreeCam(chest, off + Vector3.up * 0.25f), chest);
                }
                if (me.Gooed && gooShots < 3 && Time.time >= nextHold)
                {
                    nextHold = Time.time + 1.2f;
                    Shot(dir, $"toy_fp_gooed_{gooShots++}");
                }
                if (Time.time >= nextLog)
                {
                    nextLog = Time.time + 1f;
                    var tune = h.GetComponentInChildren<ToyGunAudio>();
                    if (tune != null && h.ToyGunShown)
                        Debug.Log($"[ToyWatch] tune vol={tune.DebugVolume:0.00} cutoff={tune.DebugCutoff:0} dist={Vector3.Distance(me.transform.position, h.transform.position):0.0} " +
                                  $"levels me={FloorAcoustics.FloorOf(me.transform.position.y)} hider={FloorAcoustics.FloorOf(h.transform.position.y)} gooed={me.Gooed} speed={me.Motor.SpeedScale:0.00}");
                }
            }
            Debug.Log("[ToyWatch] done");
        }
    }
}
