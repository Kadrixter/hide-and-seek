using System.Collections;
using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Fight tests in the built game:
    ///  -testslow (host -forceseeker + a hider client): knife hit, logs the 13 s cooldown and the seeker's slowdown
    ///      (speed scale over time), a rejected attack during the cooldown, then a kill and the slowdown after it.
    ///  -testaim (host -forceseeker, -aimshots dir): Glock from the hip vs aimed (RMB): spread statistics, first-person
    ///      screenshots (hip, aimed, camera kick right after a shot), third-person side shots of the own body.
    ///  -aimwatch (any client, -aimshots dir): side camera on the seeker while the Glock is out (aim pose, recoil).
    ///  -sawfpp (with -testcratesaw, -crateshots dir): first-person screenshots and view pitch while cutting the crate.
    /// Also starts the seat / brick area tests (<see cref="StartSeatTests"/>).
    /// </summary>
    public partial class NetSession
    {
        private void StartCombatTests(System.Func<string, bool> has)
        {
            if (has("-testslow")) StartCoroutine(SlowTest());
            if (has("-testaim")) StartCoroutine(AimTest());
            if (has("-aimwatch")) StartCoroutine(AimWatch());
            if (has("-sawfpp")) StartCoroutine(SawFppShots());
            StartSeatTests(has);
            StartLegTests(has); // NetSession.LegTests.cs
        }

        private static NetPlayer FirstOther(NetPlayer me, bool hider)
        {
            foreach (var p in NetPlayer.All)
                if (p != null && p != me && (hider ? p.IsHider : p.IsSeeker)) return p;
            return null;
        }

        private IEnumerator SlowTest()
        {
            yield return WaitSeeking();
            var me = NetPlayer.Local;
            var mm = MatchManager.Instance;
            var victim = FirstOther(me, true);
            if (!me.IsSeeker || victim == null) { Debug.Log("[SlowTest] need host seeker + a hider"); yield break; }
            Debug.Log($"[SlowTest] Tuning.AttackCooldown={Tuning.AttackCooldown} slow={Tuning.AttackSlowScale} hold={Tuning.AttackSlowHold} fade={Tuning.AttackSlowFade}");

            void Bring() => victim.TeleportRpc(me.transform.position + me.transform.forward * 1.1f, me.transform.eulerAngles.y + 180f);
            Bring();
            yield return new WaitForSeconds(1.2f);
            for (float w = 0f; w < 3f && Vector3.Distance(me.transform.position, victim.transform.position) > 1.5f; w += Time.deltaTime) yield return null;
            Debug.Log($"[SlowTest] distance to the victim {Vector3.Distance(me.transform.position, victim.transform.position):0.00} m");
            me.AttackReadyAt.Value = 0;
            Debug.Log($"[SlowTest] before: speedScale={me.Motor.SpeedScale:0.00} attackSlow={me.AttackSlowScale:0.00}");
            mm.ServerKnife(me, victim);
            double t0 = mm.Now;
            Debug.Log($"[SlowTest] knife hit: victim limping={victim.Limping.Value}, cooldown={me.AttackReadyAt.Value - mm.Now:0.00} s");
            for (int i = 0; i < 18; i++)
            {
                yield return new WaitForSeconds(0.25f);
                Debug.Log($"[SlowTest] t={mm.Now - t0:0.00} attackSlow={me.AttackSlowScale:0.00} motorSpeedScale={me.Motor.SpeedScale:0.00}");
            }
            while (mm.Now - t0 < 8.5d) yield return null;
            Bring();
            yield return new WaitForSeconds(0.2f);
            mm.ServerKnife(me, victim);
            yield return null;
            Debug.Log($"[SlowTest] attack at t={mm.Now - t0:0.0} s (cooldown left {me.AttackReadyAt.Value - mm.Now:0.0}): busy={me.Busy.Value} (expected False = rejected)");
            while (mm.Now < me.AttackReadyAt.Value) yield return null;
            Debug.Log($"[SlowTest] cooldown over at t={mm.Now - t0:0.00} s");
            // Chainsaw: the first hit only takes a leg (the victim lives) - the seeker is slowed the same way.
            victim.Severed.Value = 0;
            victim.Wounded.Value = false;
            victim.Limping.Value = false;
            victim.Busy.Value = false;
            yield return new WaitForSeconds(0.5f);
            me.AttackReadyAt.Value = 0;
            me.HasChainsaw.Value = true;
            me.Held.Value = HeldItem.Chainsaw;
            Bring();
            yield return new WaitForSeconds(0.6f);
            mm.ServerChainsaw(me, victim);
            double t2 = mm.Now;
            Debug.Log($"[SlowTest] saw hit: cooldown={me.AttackReadyAt.Value - mm.Now:0.00} s");
            for (int i = 0; i < 18; i++)
            {
                yield return new WaitForSeconds(0.25f);
                Debug.Log($"[SlowTest] saw t={mm.Now - t2:0.00} attackSlow={me.AttackSlowScale:0.00} motorSpeedScale={me.Motor.SpeedScale:0.00} victimSevered={victim.Severed.Value} alive={victim.Life.Value}");
            }
            while (mm.Now < me.AttackReadyAt.Value) yield return null;
            me.Held.Value = HeldItem.Knife;
            Bring();
            yield return new WaitForSeconds(0.3f);
            mm.ServerKnife(me, victim);
            yield return null;
            Debug.Log($"[SlowTest] kill started: busy={me.Busy.Value}");
            while (me.Busy.Value) yield return null;
            double t1 = mm.Now;
            Debug.Log($"[SlowTest] kill over: victim life={victim.Life.Value}, next attack in {me.AttackReadyAt.Value - mm.Now:0.00} s");
            for (int i = 0; i < 18; i++)
            {
                Debug.Log($"[SlowTest] after kill t={mm.Now - t1:0.00} attackSlow={me.AttackSlowScale:0.00} motorSpeedScale={me.Motor.SpeedScale:0.00}");
                yield return new WaitForSeconds(0.25f);
            }

            Debug.Log("[SlowTest] done");
        }

        private static GameObject TestLamp(Vector3 at, float intensity = 1.6f)
        {
            var l = new GameObject("CombatTestLight").AddComponent<Light>();
            l.type = LightType.Point;
            l.range = 9f;
            l.intensity = intensity;
            l.transform.position = at;
            return l.gameObject;
        }

        private IEnumerator AimTest()
        {
            yield return WaitSeeking();
            var me = NetPlayer.Local;
            var mm = MatchManager.Instance;
            string dir = ArgOr("-aimshots", "");
            if (!me.IsSeeker) { Debug.Log("[AimTest] need host seeker"); yield break; }
            mm.TestGiveGlock();
            me.Held.Value = HeldItem.Glock;
            var lamp = TestLamp(me.transform.position + Vector3.up * 2.4f + me.transform.forward * 1.5f);
            if (me.Flashlight != null) me.Flashlight.SetOn(true, false);
            yield return new WaitForSeconds(2f);
            Debug.Log($"[AimTest] glock={me.HasGlock.Value} held={me.Held.Value} ammo={me.Ammo.Value}");

            // Third person of the own body: relaxed hip hold, aimed, aimed shot (recoil).
            Vector3 side = me.transform.position + me.transform.right * 2.3f + me.transform.forward * 0.6f + Vector3.up * 1.4f;
            Vector3 front = me.transform.position + me.transform.forward * 2.6f + me.transform.right * 1.0f + Vector3.up * 1.5f;
            Vector3 at = me.transform.position + Vector3.up * 1.15f + me.transform.forward * 0.3f;
            var sideLamp = TestLamp(side + Vector3.up * 0.6f, 1.2f);
            me.SetCinematicBody(true);
            yield return new WaitForSeconds(0.3f);
            CamShot(dir, "tpp_hip_side", side, at);
            CamShot(dir, "tpp_hip_front", front, at);
            NetPlayer.TestAim = true;
            yield return new WaitForSeconds(0.6f);
            CamShot(dir, "tpp_aim_side", side, at);
            CamShot(dir, "tpp_aim_front", front, at);
            LocalPlayerController.TestShoot = true;
            yield return new WaitForSeconds(0.06f);
            CamShot(dir, "tpp_aim_recoil_side", side, at);
            yield return new WaitForSeconds(0.06f);
            CamShot(dir, "tpp_aim_recoil_side2", side, at);
            NetPlayer.TestAim = false;
            me.SetCinematicBody(false);
            Destroy(sideLamp);
            yield return new WaitForSeconds(2f);

            // First person: spread statistics (directions only, no bullets), hip and aimed views.
            var cam = me.Camera.transform;
            float hipMax = 0f, hipSum = 0f, aimMax = 0f, aimSum = 0f;
            for (int i = 0; i < 20; i++) { me.GlockShotDirection(cam); hipSum += me.LastShotSpread; hipMax = Mathf.Max(hipMax, me.LastShotSpread); }
            Shot(dir, "fpp_hip");
            NetPlayer.TestAim = true;
            yield return new WaitForSeconds(0.7f);
            for (int i = 0; i < 20; i++) { me.GlockShotDirection(cam); aimSum += me.LastShotSpread; aimMax = Mathf.Max(aimMax, me.LastShotSpread); }
            Debug.Log($"[AimTest] spread hip avg={hipSum / 20f:0.00} max={hipMax:0.00} deg | aimed avg={aimSum / 20f:0.00} max={aimMax:0.00} deg | aimW={me.GlockAimWeight:0.00} synced={me.GlockAiming.Value} fov={me.Camera.fieldOfView:0.0} speedScale={me.Motor.SpeedScale:0.00}");
            Shot(dir, "fpp_aim");
            yield return new WaitForSeconds(0.3f);

            // Aimed shot: camera kick and its settle.
            float pitch0 = me.Camera.transform.eulerAngles.x;
            LocalPlayerController.TestShoot = true;
            for (int i = 0; i < 6; i++)
            {
                yield return new WaitForSeconds(0.05f);
                Debug.Log($"[AimTest] aimed shot +{(i + 1) * 0.05f:0.00}s cam pitch delta={Mathf.DeltaAngle(pitch0, me.Camera.transform.eulerAngles.x):0.00} deg ammo={me.Ammo.Value}");
                if (i == 1) Shot(dir, "fpp_aim_kick");
            }
            yield return new WaitForSeconds(0.6f);
            Debug.Log($"[AimTest] settled cam pitch delta={Mathf.DeltaAngle(pitch0, me.Camera.transform.eulerAngles.x):0.00} deg");
            Shot(dir, "fpp_aim_settled");
            NetPlayer.TestAim = false;
            yield return new WaitForSeconds(1.2f);
            pitch0 = me.Camera.transform.eulerAngles.x;
            LocalPlayerController.TestShoot = true;
            yield return new WaitForSeconds(0.1f);
            Debug.Log($"[AimTest] hip shot +0.10s cam pitch delta={Mathf.DeltaAngle(pitch0, me.Camera.transform.eulerAngles.x):0.00} deg");
            Shot(dir, "fpp_hip_kick");
            yield return new WaitForSeconds(1.5f);
            // Aiming again for the watchers.
            NetPlayer.TestAim = true;
            yield return new WaitForSeconds(2f);
            LocalPlayerController.TestShoot = true;
            yield return new WaitForSeconds(2f);
            NetPlayer.TestAim = false;
            Destroy(lamp);
            Debug.Log("[AimTest] done");
        }

        private IEnumerator AimWatch()
        {
            yield return WaitSeeking();
            string dir = ArgOr("-aimshots", "");
            var me = NetPlayer.Local;
            GameObject lamp = null;
            int n = 0, lastAmmo = -1;
            bool lastAim = false;
            for (int i = 0; i < 600 && n < 40; i++)
            {
                yield return new WaitForSeconds(0.1f);
                var s = FirstOther(me, false);
                if (s == null || s.Held.Value != HeldItem.Glock) continue;
                if (lamp == null) lamp = TestLamp(s.transform.position + Vector3.up * 2.4f + s.transform.right * 1.5f);
                bool aim = s.GlockAiming.Value;
                bool shot = lastAmmo >= 0 && s.Ammo.Value < lastAmmo;
                lastAmmo = s.Ammo.Value;
                if (aim == lastAim && !shot && i % 10 != 0) continue;
                lastAim = aim;
                Vector3 at = s.transform.position + Vector3.up * 1.15f + s.transform.forward * 0.3f;
                Vector3 side = s.transform.position + s.transform.right * 2.3f + s.transform.forward * 0.6f + Vector3.up * 1.4f;
                if (shot) yield return new WaitForSeconds(0.04f);
                CamShot(dir, $"watch_{n++:00}_{(aim ? "aim" : "hip")}{(shot ? "_shot" : "")}", side, at);
                Debug.Log($"[AimWatch] frame {n} aim={aim} aimW={s.GlockAimWeight:0.00} shot={shot}");
            }
        }

        private IEnumerator SawFppShots()
        {
            yield return WaitSeeking();
            string dir = ArgOr("-crateshots", "");
            var me = NetPlayer.Local;
            int n = 0;
            for (int i = 0; i < 1200 && n < 9; i++)
            {
                yield return new WaitForSeconds(0.1f);
                if (!me.Busy.Value || me.Held.Value != HeldItem.Chainsaw) continue;
                Shot(dir, $"fpp_saw_{n++:00}");
                Debug.Log($"[SawFpp] frame {n}: view pitch={me.Motor.LookPitch:0.0}");
                yield return new WaitForSeconds(0.3f);
            }
        }
    }
}
