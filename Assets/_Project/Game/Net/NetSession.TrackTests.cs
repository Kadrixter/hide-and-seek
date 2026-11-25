using System.Collections;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>Drift track test, host side (-testtrack; see NetSession.KartTests.cs for the whole scenario).</summary>
    public partial class NetSession
    {
        private IEnumerator TrackHostTest(string dir)
        {
            yield return WaitKartLobby(2);
            var mm = MatchManager.Instance;
            var me = NetPlayer.Local;
            var track = DriftTrack.Instance;
            float az = DriftTrackLayout.ArchZ;
            Vector3 arch = new(-LobbyLayout.HalfW, 1.4f, az);

            // 1. The arch from the lobby; the track is drawn only while the arch is in view.
            Vector3 lobbySpot = new(-3.6f, 0.02f, az - 0.6f);
            Vector3 to = arch - (lobbySpot + Vector3.up * 1.62f);
            me.TeleportLocal(lobbySpot, YawTo(to));
            me.Motor.SetLook(YawTo(to), PitchTo(to));
            yield return new WaitForSeconds(1.2f);
            bool shownArch = track.Shown;
            Shot(dir, "track_00_arch_lobby");
            float msArch = 0f;
            yield return FrameMs(v => msArch = v);
            me.Motor.SetLook(90f, 0f);
            yield return new WaitForSeconds(0.6f);
            bool shownAway = track.Shown;
            float msAway = 0f;
            yield return FrameMs(v => msAway = v);
            KLog($"lobby: track drawn looking at the arch {shownArch}, looking east {shownAway}; frame {msArch:0.0} ms / {msAway:0.0} ms -> {Pf(shownArch && !shownAway)}");
            CamShot(dir, "track_01_arch_close", new Vector3(-5.2f, 1.5f, az - 1.6f), arch);

            // 2. Walk through the arch and the passage onto the track.
            me.Motor.SetLook(270f, 0f);
            yield return WalkTo(me, new Vector3(-6.8f, 0f, az), 6f);
            yield return WalkTo(me, new Vector3(-8.6f, 0f, az), 4f);
            Shot(dir, "track_02_in_arch");
            yield return WalkTo(me, new Vector3(DriftTrackLayout.PassageMid, 0f, az + 0.6f), 4f);
            Shot(dir, "track_03_passage");
            yield return WalkTo(me, new Vector3(DriftTrackLayout.PadEast - 2.5f, 0f, az + 0.6f), 6f);
            Shot(dir, "track_04_on_track");
            KLog($"walked through: at {me.transform.position} -> {Pf(me.transform.position.x < DriftTrackLayout.PadEast - 1f)}");

            // 3. Overview, the karts, the asphalt on the low and the Ultra size.
            DriftTrack.ForceVisible = true;
            CamShot(dir, "track_05_overview", new Vector3(DriftTrackLayout.PadEast - 4f, 12f, az - 10f), DriftTrackLayout.PadCenter + new Vector3(-6f, 0f, 0f));
            CamShot(dir, "track_06_karts", DriftTrackLayout.KartSlot(2) + new Vector3(-7f, 2.6f, -6f), DriftTrackLayout.KartSlot(2) + Vector3.up * 0.3f);
            Vector3 close = new(DriftTrackLayout.PadEast - 9f, 0f, az - 7f);
            foreach (int size in new[] { 256, 2048 })
            {
                AsphaltMaps.TestSize = size;
                AsphaltMaps.Refresh();
                float tw = Time.time;
                while (AsphaltMaps.ShownSize != size && Time.time - tw < 40f) yield return null;
                yield return new WaitForSeconds(0.3f);
                KLog($"asphalt {size}: shown {AsphaltMaps.ShownSize} after {Time.time - tw:0.0} s -> {Pf(AsphaltMaps.ShownSize == size)}");
                CamShot(dir, $"track_07_asphalt_{size}", close + new Vector3(0.9f, 1.1f, -0.9f), close);
                CamShot(dir, $"track_07_asphalt_{size}_wide", close + new Vector3(4f, 1.7f, -5f), close + new Vector3(-6f, 0f, 4f));
            }
            AsphaltMaps.TestSize = 0;
            AsphaltMaps.Refresh();
            DriftTrack.ForceVisible = false;

            // 4. Into the purple kart; wait for the client in the black one.
            yield return EnterKart(me, 1, dir, "track_08_prompt_purple");
            var kart = LobbyKart.Get(1);
            float tc = Time.time;
            while (Time.time - tc < 40f && !mm.KartsVar.Value.Get(3).Driven) yield return null;
            yield return new WaitForSeconds(2.5f);
            KLog($"client in the black kart: {mm.KartsVar.Value.Get(3).Driven} -> {Pf(mm.KartsVar.Value.Get(3).Driven)}");

            // 5. Drift, then stop and wait for the client's bump.
            LocalPlayerController.TestKartActive = true;
            yield return TrackDriftRun(dir, kart);
            int hits0 = kart.Phys.Collide.KartHits;
            float tb = Time.time;
            while (Time.time - tb < 16f && kart.Phys.Collide.KartHits == hits0) yield return null;
            yield return new WaitForSeconds(1f);
            KLog($"bumped by the client: kart hits {kart.Phys.Collide.KartHits - hits0}, own speed now {kart.Phys.Speed:0.00}, y {kart.transform.position.y:0.00} -> {Pf(kart.Phys.Collide.KartHits > hits0 && Mathf.Abs(kart.transform.position.y) < 0.2f)}");
            CamShot(dir, "track_11_after_bump", kart.transform.position + new Vector3(4f, 3f, 4f), kart.transform.position);

            // 6. R: back in the slot; then out.
            int ep = mm.KartsVar.Value.Get(1).Epoch;
            kart.RequestReset("test");
            yield return new WaitForSeconds(1.2f);
            KLog($"reset: epoch {ep} -> {mm.KartsVar.Value.Get(1).Epoch}, kart at {kart.transform.position} -> {Pf(mm.KartsVar.Value.Get(1).Epoch != ep && (kart.transform.position - kart.Spot).magnitude < 0.2f)}");
            LocalPlayerController.TestKartActive = false;
            LocalPlayerController.TestKartE = true;
            yield return new WaitForSeconds(1.5f);
            var cc = me.GetComponent<CharacterController>();
            KLog($"out: kart 1 {(mm.KartsVar.Value.Get(1).Driven ? "taken" : "free")}, me at {me.transform.position}, cc {(cc != null && cc.enabled)} -> {Pf(!mm.KartsVar.Value.Get(1).Driven && cc != null && cc.enabled && KartCamera.Active == null)}");

            // 7. Back to the lobby through the passage.
            yield return WalkTo(me, new Vector3(DriftTrackLayout.PadEast - 1.5f, 0f, az), 8f);
            yield return WalkTo(me, new Vector3(DriftTrackLayout.PassageMid, 0f, az), 5f);
            yield return WalkTo(me, new Vector3(-5f, 0f, az), 6f);
            me.Motor.SetLook(90f, 0f);
            yield return new WaitForSeconds(0.6f);
            Shot(dir, "track_12_back_in_lobby");
            KLog($"back in the lobby: at {me.transform.position}, track drawn {track.Shown} -> {Pf(me.transform.position.x > -LobbyLayout.HalfW + 0.5f && !track.Shown)}");
            yield return new WaitForSeconds(40f);
            KLog("host done");
        }

        /// <summary>Average frame time over 2 s (ms).</summary>
        private static IEnumerator FrameMs(System.Action<float> result)
        {
            float t = 0f;
            int n = 0;
            while (t < 2f) { yield return null; t += Time.unscaledDeltaTime; n++; }
            result(t * 1000f / Mathf.Max(1, n));
        }

        /// <summary>Accelerate west, handbrake kick to the left, hold the drift with counter-steer, straighten, brake; logs 10 Hz.</summary>
        private IEnumerator TrackDriftRun(string dir, LobbyKart kart)
        {
            var ph = kart.Phys;
            float t = 0f, nextLog = 0f, prevCourse = float.NaN, nextShot = 0f;
            int drift = 0, counter = 0, samples = 0, shots = 0;
            float maxBeta = 0f, driftTime = 0f;
            const float target = 25f, turn = -1f, kick = 2.4f;
            while (t < kick + 4.4f)
            {
                float thr = 1f, steer = 0f;
                bool hb = false;
                if (t < kick) steer = 0f;
                else if (t < kick + 0.3f) { steer = turn; hb = true; }
                else if (t < kick + 2.6f) steer = Mathf.Clamp((ph.SlipAngle - (-turn * target)) / 12f - 0.004f * ph.YawRate, -1f, 1f);
                else if (t < kick + 3.2f) { steer = 0f; thr = 0.3f; }
                else thr = -1f;
                if (t > kick + 3.2f && ph.ForwardSpeed < 0.3f) thr = 0f;
                LocalPlayerController.TestKartThrottle = thr;
                LocalPlayerController.TestKartSteer = steer;
                LocalPlayerController.TestKartHandbrake = hb;
                yield return null;
                t += Time.deltaTime;
                Vector3 v = ph.Vel;
                float speed = new Vector2(v.x, v.z).magnitude;
                float course = speed > 0.5f ? Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg : ph.Yaw;
                if (t >= nextLog)
                {
                    float courseRate = float.IsNaN(prevCourse) ? 0f : Mathf.DeltaAngle(prevCourse, course) / 0.1f;
                    prevCourse = course;
                    nextLog = t + 0.1f;
                    bool drifting = Mathf.Abs(ph.SlipAngle) > 12f && speed > 2f;
                    bool cs = drifting && Mathf.Abs(steer) > 0.05f && Mathf.Sign(steer) != Mathf.Sign(ph.YawRate);
                    samples++;
                    if (drifting) { drift++; driftTime += 0.1f; }
                    if (cs) counter++;
                    maxBeta = Mathf.Max(maxBeta, Mathf.Abs(ph.SlipAngle));
                    KLog($"drive t={t:0.00} pos=({ph.Pos.x:0.00},{ph.Pos.z:0.00}) v={speed:0.00} yaw={ph.Yaw:0.0} yawRate={ph.YawRate:0.0} course={course:0.0} courseRate={courseRate:0.0} slip={ph.SlipAngle:0.0} steerIn={steer:0.00} wheel={ph.Steer:0.0} thr={thr:0.0} hb={(hb ? 1 : 0)} slip01={ph.Slip01:0.00} skids={kart.GetComponent<KartFx>().SkidCount}{(drifting ? " DRIFT" : "")}{(cs ? " COUNTER" : "")}");
                }
                if (t > kick + 0.5f && t < kick + 2.6f && t >= nextShot && shots < 3)
                {
                    nextShot = t + 0.6f;
                    Shot(dir, $"track_09_drift_chase_{shots}");
                    CamShot(dir, $"track_10_drift_high_{shots}", ph.Pos + new Vector3(-4f, 4.5f, 5f), ph.Pos);
                    shots++;
                }
            }
            LocalPlayerController.TestKartThrottle = 0f;
            LocalPlayerController.TestKartSteer = 0f;
            yield return new WaitForSeconds(1f);
            KLog($"drift summary: {drift}/{samples} samples drifting ({driftTime:0.0} s), max |slip| {maxBeta:0.0} deg, counter-steer in {counter} samples, skid quads {kart.GetComponent<KartFx>().SkidCount} -> {Pf(driftTime >= 1f && maxBeta > 15f && counter >= 5)}");
        }
    }
}
