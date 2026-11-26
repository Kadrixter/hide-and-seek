using UnityEngine;
using T = HideAndSeek.Game.KartTuning;

namespace HideAndSeek.Game
{
    /// <summary>
    /// The kart's drift model and countersteer assist, run every physics step on the driver's client just before
    /// <see cref="ArcadeCar"/> works out the tyre forces (pure maths, numbers in KartTuning.Drift.cs):
    ///  - a drift latch (0..1) set by the handbrake, a throttle slide past a few degrees or a quick steering flick, held
    ///    by the throttle and let go when the kart straightens, brakes or slows down;
    ///  - the rear grip as a smooth function of the slip angle and the latch (normal tyre -> slippery drift tyre with the
    ///    throttle spinning it up -> grip coming back past the max drift angle so the kart catches instead of spinning);
    ///  - automatic countersteer: the front wheels turn toward the front axle's velocity (idea from the "countersteer
    ///    assist" of Godot Easy Vehicle Physics by DAShoe1, MIT; reimplemented, no code copied) plus a PID hold of a
    ///    sweet-spot drift angle that the player's steering moves (into the turn = tighter, out = straighter, lifting
    ///    off = smaller); outside a drift the same assist catches small slides, fading out while the player steers hard
    ///    (as there);
    ///  - a spin guard (yaw acceleration against a slip angle growing past the max) like that asset's stability assist,
    ///    a yaw-rate damper, front scrub past the steering lock, gentler brakes when sideways, and a push along the
    ///    velocity under throttle so a long drift keeps the speed it started with.
    /// The result is the front wheel angle (also what the steering wheel, the network pack and the other players see),
    /// the axles' grip multipliers, engine / brake scale, a yaw and a drive acceleration. Deterministic: the same inputs give
    /// the same outputs (only the owner runs it; the others see the synced pose).
    /// Sign convention as <see cref="KartPhysics"/>: + slip = the velocity points right of the nose, + steer / yaw = right.
    /// </summary>
    public sealed class KartDrift
    {
        public struct Input
        {
            public float Dt;
            /// <summary>Velocity in the kart's frame along the nose / to the right (m/s).</summary>
            public float VFwd, VLat;
            /// <summary>Yaw rate (rad/s, + = turning right).</summary>
            public float YawRate;
            /// <summary>Front axle ahead of the centre of mass (m).</summary>
            public float FrontArm;
            public float Throttle, Steer;
            public bool Handbrake, Grounded;
            /// <summary>The player's own steering angle (ArcadeCar's rate-limited, speed-limited steering, deg).</summary>
            public float PlayerSteerDeg;
        }

        // ---- outputs
        /// <summary>Front wheel angle to use (deg, + = right).</summary>
        public float FrontSteerDeg { get; private set; }
        public float FrontGrip { get; private set; } = 1f;
        public float RearGrip { get; private set; } = 1f;
        /// <summary>Yaw acceleration to add (rad/s^2, + = right): spin guard and flick kick.</summary>
        public float YawAccel { get; private set; }
        /// <summary>Acceleration along the velocity (m/s^2): the throttle keeping a drift's speed.</summary>
        public float DriveAccel { get; private set; }
        /// <summary>Engine force multiplier (less in a drift: <see cref="DriveAccel"/> feeds the slide instead).</summary>
        public float EngineScale { get; private set; } = 1f;
        /// <summary>Brake force multiplier: brakes ease off while the kart is well sideways (no spin when braking mid-drift).</summary>
        public float BrakeScale { get; private set; } = 1f;
        public float Latch { get; private set; }
        /// <summary>Slip angle (deg, -180..180).</summary>
        public float SlipDeg { get; private set; }
        /// <summary>Slip angle rate (deg/s, filtered).</summary>
        public float SlipRate { get; private set; }
        /// <summary>The drift angle the assist is holding now (deg, signed; 0 = not drifting).</summary>
        public float TargetDeg { get; private set; }
        /// <summary>How much of the front wheel angle came from the assist (deg).</summary>
        public float AssistDeg { get; private set; }
        /// <summary>Spin-outs so far (slip past 110 deg at speed).</summary>
        public int SpinOuts { get; private set; }

        private float _flickSide, _flickT, _kickT, _kickSign, _hbHold, _flickHold, _holdI, _entrySpeed, _held;
        private bool _spun, _primed;

        public void Reset()
        {
            FrontSteerDeg = 0f;
            FrontGrip = RearGrip = 1f;
            YawAccel = DriveAccel = Latch = SlipDeg = SlipRate = TargetDeg = AssistDeg = 0f;
            _flickSide = _flickT = _kickT = _hbHold = _flickHold = _holdI = _entrySpeed = _held = 0f;
            EngineScale = BrakeScale = 1f;
            _spun = _primed = false;
        }

        public void Step(in Input k)
        {
            float dt = Mathf.Max(1e-4f, k.Dt);
            float speed = Mathf.Sqrt(k.VFwd * k.VFwd + k.VLat * k.VLat);
            float thr = Mathf.Clamp(k.Throttle, -1f, 1f), gas = Mathf.Max(0f, thr);
            float steer = Mathf.Clamp(k.Steer, -1f, 1f);

            // Slip angle (full circle, so a spin shows) and its rate.
            float slip = speed > 1f ? Mathf.Atan2(k.VLat, k.VFwd) * Mathf.Rad2Deg : 0f;
            float rate = _primed ? Mathf.DeltaAngle(SlipDeg, slip) / dt : 0f;
            SlipRate = Mathf.Lerp(SlipRate, rate, 1f - Mathf.Exp(-dt / 0.05f));
            SlipDeg = slip;
            _primed = true;
            float abs = Mathf.Abs(slip), side = slip >= 0f ? 1f : -1f;
            bool forward = k.VFwd > 0.5f && abs < 100f;
            float speedK = Mathf.Clamp01((speed - 3f) / 6f);

            // Spin-out counter (tests): past 110 deg at speed, re-armed once the kart is straight again.
            if (!_spun && abs > 110f && speed > 3f) { _spun = true; SpinOuts++; }
            else if (_spun && (abs < 30f || speed < 1f)) _spun = false;

            // ---- the drift latch
            bool canDrift = k.Grounded && forward && speed > T.DriftMinSpeed;
            // Flick: full steer one way, then the other within FlickWindow, at speed with the throttle down.
            _flickT += dt;
            if (Mathf.Abs(steer) > 0.6f)
            {
                float s = Mathf.Sign(steer);
                if (_flickSide != 0f && s != _flickSide && _flickT < T.FlickWindow && canDrift && gas > 0.5f && speed > T.FlickMinSpeed)
                {
                    Latch = Mathf.Max(Latch, T.FlickLatch);
                    _kickT = T.FlickKickTime;
                    _flickHold = T.FlickHold;
                    _kickSign = s;
                }
                _flickSide = s;
                _flickT = 0f;
            }
            _flickHold -= dt;

            // The drift angle to hold (size; the side is the slip's): the sweet spot, steering into the turn (against
            // the slip side) tightens it, steering out straightens; lifting off lets it shrink (the throttle holds the
            // drift, the handbrake too). It moves at TargetRate, so letting go of a big drift does not whip the kart
            // through straight into a slide the other way.
            float into = -steer * side;
            float heldWant = T.DriftTargetAngle + (into >= 0f ? into * (T.DriftMaxTarget - T.DriftTargetAngle) : into * (T.DriftTargetAngle - T.DriftMinTarget));
            heldWant *= Mathf.Lerp(T.LiftTargetK, 1f, Mathf.Max(Smooth(0.1f, 0.5f, gas), _hbHold));
            _held = Latch > 0f ? Mathf.MoveTowards(_held, heldWant, T.TargetRate * dt) : heldWant;
            bool exiting = _held < T.DriftExitHeld && abs < T.DriftEnterSlip + 3f;

            if (!canDrift) Latch = Mathf.MoveTowards(Latch, 0f, dt / (speed < T.DriftMinSpeed * 0.7f || !forward ? 0.25f : T.DriftExitTime));
            else if (k.Handbrake) Latch = Mathf.MoveTowards(Latch, 1f, dt / T.DriftLatchTime);
            else if (exiting) Latch = Mathf.MoveTowards(Latch, 0f, dt / T.DriftExitTime);
            else if (gas > 0.3f && abs > T.DriftEnterSlip) Latch = Mathf.MoveTowards(Latch, 1f, dt / T.DriftSlideLatchTime);
            else if (_flickHold > 0f) { } // just flicked: give the rear time to step out
            else if (thr < -0.3f) Latch = Mathf.MoveTowards(Latch, 0f, dt / T.DriftBrakeTime);
            else if (abs < T.DriftExitSlip) Latch = Mathf.MoveTowards(Latch, 0f, dt / T.DriftExitTime);
            else if (gas < 0.5f) Latch = Mathf.MoveTowards(Latch, 0f, dt / T.DriftLiftTime);
            // The speed the drift started with (the throttle keeps it, see DriveAccel).
            if (Latch <= 0f) _entrySpeed = 0f;
            else if (_entrySpeed <= 0f || k.Handbrake) _entrySpeed = Mathf.Min(speed, T.DriftMaxSpeed);
            float l = Latch * Latch * (3f - 2f * Latch); // smoothstep: the grip changes gently at both ends

            // ---- grip
            float normal = 1f - T.NormalSlipLoss * Smooth(T.NormalSlipFrom, T.NormalSlipTo, abs);
            float drift = T.DriftRearGrip * (1f - T.ThrottleGripLoss * gas) + T.LiftGrip * (1f - gas);
            _hbHold = k.Handbrake && canDrift ? 1f : Mathf.MoveTowards(_hbHold, 0f, dt / T.HandbrakeRelease);
            drift = Mathf.Lerp(drift, T.HandbrakeRearGrip, _hbHold);
            drift += T.CatchGrip * Smooth(T.MaxDriftAngle - 5f, T.MaxDriftAngle - 5f + T.CatchSpan, abs);
            RearGrip = Mathf.Lerp(normal, drift, l);
            // The front axle's direction of travel relative to the nose: past the steering lock the front tyres scrub
            // (less grip), so they cannot drag the nose on into a spin.
            float frontDeg = forward && speed > 1.5f ? Mathf.Atan2(k.VLat + k.YawRate * k.FrontArm, k.VFwd) * Mathf.Rad2Deg : 0f;
            float scrub = T.FrontScrub * Smooth(T.MaxSteerLock - 4f, T.MaxSteerLock + 12f, Mathf.Abs(frontDeg));
            FrontGrip = Mathf.Lerp(1f, T.DriftFrontGrip, l) * (1f - scrub);

            // ---- steering: the player's angle, the countersteer assist and the drift hold
            float player = k.PlayerSteerDeg;
            float target = 0f, want = player, assist = 0f;
            if (forward && speed > 1.5f)
            {
                // Wheels along the front axle's direction of travel = no front side force.
                float front = Mathf.Clamp(frontDeg, -T.MaxSteerLock, T.MaxSteerLock);
                // Outside a drift: catch small slides, less while the player steers hard (Godot EVP's (1 - |steer|)).
                float w = Smooth(T.AssistFromSlip, T.AssistFullSlip, abs) * speedK * T.AssistStrength;
                float catchSlide = w * T.AssistGain * front * (1f - Mathf.Abs(steer) * 0.7f);
                // In a drift: hold the angle (above).
                target = side * _held;
                // Integral (leaky, clamped) so the held angle really is the sweet spot, not a few degrees short.
                if (l > 0.5f) _holdI = Mathf.Clamp(_holdI + T.AssistHoldI * (slip - target) * dt, -T.AssistHoldIMax, T.AssistHoldIMax);
                else _holdI = Mathf.MoveTowards(_holdI, 0f, 30f * dt);
                float hold = T.AssistGain * front + T.AssistHoldP * (slip - target) + T.AssistHoldD * SlipRate + _holdI;
                float driftWant = hold * T.AssistStrength * speedK + player * T.DriftPlayerSteer;
                want = Mathf.Lerp(player + catchSlide, driftWant, l);
                assist = want - player;
            }
            else _holdI = 0f;
            want = Mathf.Clamp(want, -T.MaxSteerLock, T.MaxSteerLock);
            FrontSteerDeg = Mathf.MoveTowards(FrontSteerDeg, want, T.AssistSteerRate * dt);
            AssistDeg = assist;
            TargetDeg = l > 0.01f ? target : 0f;

            // ---- yaw: spin guard against a slip growing past the max, flick kick
            float yaw = 0f;
            if (forward && speed > 2f)
            {
                float over = Mathf.Max(0f, abs - T.MaxDriftAngle);
                float growing = Mathf.Max(0f, SlipRate * side) * Smooth(T.DriftTargetAngle, T.MaxDriftAngle + 10f, abs);
                yaw = side * Mathf.Min(T.SpinGuardMax, T.SpinGuardP * over + T.SpinGuardD * growing) * speedK;
                // Yaw-rate damper: while the slip grows on past the held angle, slow the rotation down.
                float past = Smooth(0.6f, 1f, abs / Mathf.Max(1f, Mathf.Abs(target)));
                if (SlipRate * side > 0f) yaw += T.YawDamper * SlipRate * Mathf.Deg2Rad * past * l * speedK;
            }
            if (_kickT > 0f)
            {
                _kickT -= dt;
                // Rear out = nose toward the new steer side.
                yaw += _kickSign * T.FlickYaw;
            }
            YawAccel = yaw;

            // ---- throttle keeps a drift's speed
            EngineScale = Mathf.Lerp(1f, T.DriftEngine, l);
            BrakeScale = 1f - T.SlideBrakeRelief * Smooth(10f, 40f, abs);
            DriveAccel = forward ? T.DriftPush * gas * l * Smooth(5f, 25f, abs) * Mathf.Clamp01((_entrySpeed - speed) / 3f) : 0f;
        }

        /// <summary>0 below <paramref name="a"/>, 1 above <paramref name="b"/>, smooth between.</summary>
        private static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }
    }
}
