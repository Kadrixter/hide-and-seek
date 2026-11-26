using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// -testkart &lt;dir&gt; (one instance, host): the drift model and countersteer assist (<see cref="KartDrift"/>) on a real
    /// kart. Gets into the red kart, accelerates, kicks a drift with the handbrake + left, then lets go of the steering
    /// with the throttle down (the assist alone has to hold the drift), steers into the drift, steers out (recovery)
    /// and brakes. Logs 10 Hz (slip angle, held angle, latch, grips, wheel angle and how much of it is the assist,
    /// yaw rate, spin-outs) and third-person shots (chase / top / side renders). PASS: a drift held over 2 s, no
    /// spin-out, slip under 75 deg, the wheels countersteering, recovered within 2.5 s, the speed kept.
    /// </summary>
    public partial class NetSession
    {
        private IEnumerator KartDriftTest(string dir)
        {
            yield return WaitKartLobby(1);
            // -batchmode has no input devices: LocalPlayerController reads the (test) kart controls only while
            // Keyboard.current / Mouse.current exist, so add virtual ones (never pressed).
            if (Keyboard.current == null) InputSystem.AddDevice<Keyboard>();
            if (Mouse.current == null) InputSystem.AddDevice<Mouse>();
            var me = NetPlayer.Local;
            var mm = MatchManager.Instance;
            yield return EnterKart(me, 0, dir, null);
            var kart = LobbyKart.Get(0);
            var ph = kart.Phys;
            var dr = ph.Drift;
            LocalPlayerController.TestKartActive = true;
            byte epoch = mm.KartsVar.Value.Get(0).Epoch;
            int spins0 = dr.SpinOuts, hits0 = ph.Collide.Hits;

            const float accel = 2.4f, kick = 0.3f, hold = 3.5f, into = 1.5f, outMax = 2.5f;
            float t = 0f, nextLog = 0f, run = 0f, longest = 0f, maxSlip = 0f, recover = -1f;
            float speedHold0 = -1f, speedHold1 = 0f, outAt = -1f;
            int driftSamples = 0, counterSamples = 0, samples = 0, shots = 0, wheelOk = 0, wheelChecks = 0;
            float nextShot = 0f;
            string phase = "accel";
            while (true)
            {
                float thr = 1f, steer = 0f;
                bool hb = false;
                if (t < accel) phase = "accel";
                else if (t < accel + kick) { phase = "kick"; steer = -1f; hb = true; }
                else if (t < accel + kick + hold) phase = "hold";                     // hands off: the assist holds the drift
                else if (t < accel + kick + hold + into) { phase = "into"; steer = -1f; } // tighter
                else if (recover < 0f && t < accel + kick + hold + into + outMax) { phase = "out"; steer = 1f; thr = 0.4f; }
                else if (t < accel + kick + hold + into + outMax + 2.5f) { phase = "brake"; thr = ph.ForwardSpeed > 0.3f ? -1f : 0f; }
                else break;
                LocalPlayerController.TestKartThrottle = thr;
                LocalPlayerController.TestKartSteer = steer;
                LocalPlayerController.TestKartHandbrake = hb;
                yield return null;
                t += Time.deltaTime;

                float slip = ph.SlipAngle, abs = Mathf.Abs(slip), kmh = ph.Speed * 3.6f;
                bool drifting = abs > 12f && ph.Speed > 3f && dr.Latch > 0.5f;
                if (phase == "hold" || phase == "into")
                {
                    run = drifting ? run + Time.deltaTime : 0f;
                    longest = Mathf.Max(longest, run);
                    maxSlip = Mathf.Max(maxSlip, abs);
                    if (speedHold0 < 0f && t > accel + kick + 0.3f) speedHold0 = ph.Speed;
                    speedHold1 = ph.Speed;
                }
                if (phase == "out")
                {
                    if (outAt < 0f) outAt = t;
                    if (abs < 5f && dr.Latch < 0.05f && recover < 0f) recover = t - outAt;
                }
                if (t >= nextLog)
                {
                    nextLog = t + 0.1f;
                    samples++;
                    // Countersteer: front wheels turned toward the slide (wheel angle on the slip's side).
                    bool counter = drifting && Mathf.Abs(ph.Steer) > 2f && Mathf.Sign(ph.Steer) == Mathf.Sign(slip);
                    if (drifting && (phase == "hold" || phase == "into")) { driftSamples++; if (counter) counterSamples++; }
                    // The model shows the same front wheel angle (and the steering wheel turns with it).
                    if (kart.Parts != null && kart.View == LobbyKart.Mode.Local)
                    {
                        float shown = Mathf.DeltaAngle(0f, kart.Parts.FrontSteer[0].localEulerAngles.y);
                        wheelChecks++;
                        if (Mathf.Abs(shown - ph.Steer) < 1.5f) wheelOk++;
                    }
                    KLog($"drift t={t:0.00} {phase} v={kmh:0} km/h slip={slip:0.0} held={dr.TargetDeg:0} latch={dr.Latch:0.00} " +
                         $"gripF={dr.FrontGrip:0.00} gripR={dr.RearGrip:0.00} wheel={ph.Steer:0.0} assist={dr.AssistDeg:0.0} player={ph.Car.playerSteerAngle:0.0} " +
                         $"yawRate={ph.YawRate:0} yawAcc={dr.YawAccel:0.0} push={dr.DriveAccel:0.0} spins={dr.SpinOuts - spins0} down={ph.WheelsDown} hits={ph.Collide.Hits - hits0}" +
                         $"{(drifting ? " DRIFT" : "")}{(counter ? " COUNTER" : "")}");
                }
                if ((phase == "hold" || phase == "into") && t >= nextShot && shots < 4 && t > accel + kick + 0.8f)
                {
                    nextShot = t + 1f;
                    Vector3 p = kart.transform.position;
                    CamShot(dir, $"kart_drift_{shots}_chase", kart.transform.TransformPoint(new Vector3(0f, 2.3f, -5.8f)), p + Vector3.up * 0.4f);
                    CamShot(dir, $"kart_drift_{shots}_top", p + new Vector3(0.5f, 11f, -0.5f), p);
                    CamShot(dir, $"kart_drift_{shots}_side", p + kart.transform.right * 5f + Vector3.up * 1.4f, p + Vector3.up * 0.3f);
                    shots++;
                }
            }
            LocalPlayerController.TestKartThrottle = 0f;
            LocalPlayerController.TestKartSteer = 0f;
            LocalPlayerController.TestKartHandbrake = false;
            int spins = dr.SpinOuts - spins0;
            bool reset = mm.KartsVar.Value.Get(0).Epoch != epoch;
            float keep = speedHold0 > 0f ? speedHold1 / speedHold0 : 0f;
            KLog($"drift held: longest {longest:0.00} s (need > 2), max |slip| {maxSlip:0.0} deg (< 75), spin-outs {spins} -> {Pf(longest > 2f && maxSlip < 75f && spins == 0)}");
            KLog($"countersteer: wheels toward the slide in {counterSamples}/{driftSamples} drift samples -> {Pf(driftSamples > 0 && counterSamples >= driftSamples * 0.8f)}");
            KLog($"recovery: straight {(recover >= 0f ? recover.ToString("0.00") + " s" : "never")} after steering out -> {Pf(recover >= 0f && recover < outMax)}");
            KLog($"speed kept: {speedHold0 * 3.6f:0} -> {speedHold1 * 3.6f:0} km/h ({keep:0.00}) -> {Pf(keep > 0.6f)}");
            KLog($"wheels shown = physics in {wheelOk}/{wheelChecks} samples -> {Pf(wheelChecks > 0 && wheelOk >= wheelChecks - 1)}");
            KLog($"no reset / wall: epoch {(reset ? "changed (off the pad?)" : "same")}, hits {ph.Collide.Hits - hits0} -> {Pf(!reset)}");
            LocalPlayerController.TestKartActive = false;
            KLog("drift test done");
        }
    }
}
