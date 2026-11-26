namespace HideAndSeek.Game
{
    /// <summary>
    /// Drift model and countersteer assist numbers (<see cref="KartDrift"/>). Angles in degrees, speeds in m/s, grips
    /// are multipliers on the tyres' lateral friction (1 = the normal, very grippy arcade tyre). Slip angle = angle
    /// between the nose and the velocity; "drift" = the latch the handbrake / a throttle slide sets (0..1).
    /// </summary>
    public static partial class KartTuning
    {
        // ---- getting into a drift
        /// <summary>No drift below this speed (~22 km/h): the handbrake just brakes.</summary>
        public const float DriftMinSpeed = 6f;
        /// <summary>Slip angle at which a throttle slide (a flick, power over) latches the drift.</summary>
        public const float DriftEnterSlip = 12f;
        /// <summary>Seconds for the handbrake / a throttle slide to fully latch the drift.</summary>
        public const float DriftLatchTime = 0.12f, DriftSlideLatchTime = 0.3f;
        /// <summary>Flick: a full steer to the other side within this time (s) at speed with throttle kicks the latch up.</summary>
        public const float FlickWindow = 0.35f, FlickLatch = 0.75f, FlickMinSpeed = 9f;
        /// <summary>Yaw kick of a flick (rad/s^2, for <see cref="FlickKickTime"/> s): helps the rear step out.</summary>
        public const float FlickYaw = 9f, FlickKickTime = 0.18f;
        /// <summary>After a flick the drift is not let go for this long (s), so the rear has time to step out.</summary>
        public const float FlickHold = 0.5f;

        // ---- rear grip curve
        /// <summary>Outside a drift the rear grip still eases off with slip (no snap): 1 - loss * smoothstep(from, to, |slip|).</summary>
        public const float NormalSlipLoss = 0.1f, NormalSlipFrom = 6f, NormalSlipTo = 20f;
        /// <summary>Rear grip in a drift (full throttle takes <see cref="ThrottleGripLoss"/> of it, lifting adds <see cref="LiftGrip"/>).</summary>
        public const float DriftRearGrip = 0.55f, ThrottleGripLoss = 0.25f, LiftGrip = 0.2f;
        /// <summary>Rear grip while the handbrake is held (locked rear wheels), easing back over <see cref="HandbrakeRelease"/> s.</summary>
        public const float HandbrakeRearGrip = 0.3f, HandbrakeRelease = 0.3f;
        /// <summary>Front grip in a drift (the front keeps its bite).</summary>
        public const float DriftFrontGrip = 1.1f;
        /// <summary>Past <see cref="MaxDriftAngle"/> the rear grip comes back (up to +<see cref="CatchGrip"/> over <see cref="CatchSpan"/> deg): the tyres catch, no spin.</summary>
        public const float CatchGrip = 0.4f, CatchSpan = 20f;

        // ---- drift angle and the countersteer assist
        /// <summary>The "sweet spot" slip angle the assist holds with no steering input.</summary>
        public const float DriftTargetAngle = 30f;
        /// <summary>Steering into the drift raises the held angle up to this; steering out lowers it to <see cref="DriftMinTarget"/>.</summary>
        public const float DriftMaxTarget = 42f, DriftMinTarget = 0f;
        /// <summary>Off the throttle the held angle shrinks to this share (the kart straightens itself, smoothly).</summary>
        public const float LiftTargetK = 0.3f;
        /// <summary>How fast the held angle follows the steering (deg/s); below <see cref="DriftExitHeld"/> deg the drift lets go.</summary>
        public const float TargetRate = 60f, DriftExitHeld = 6f;
        /// <summary>Largest slip angle before the spin guard (catch grip + yaw torque) steps in.</summary>
        public const float MaxDriftAngle = 50f;
        /// <summary>
        /// Countersteer: the front wheels point along the front axle's velocity times <see cref="AssistGain"/> (Godot
        /// Easy Vehicle Physics' countersteer_assist idea), plus a PD hold of the sweet spot (deg of steer per deg of
        /// slip error, per deg/s of slip rate).
        /// </summary>
        public const float AssistGain = 1f, AssistHoldP = 0.4f, AssistHoldD = 0.08f;
        /// <summary>Integral of the hold (deg of steer per deg*s of slip error) and its limit (deg).</summary>
        public const float AssistHoldI = 0.6f, AssistHoldIMax = 12f;
        /// <summary>Outside a drift the assist works from this slip angle, full at <see cref="AssistFullSlip"/>.</summary>
        public const float AssistFromSlip = 3f, AssistFullSlip = 9f;
        /// <summary>Overall assist strength (0 = off, 1 = full).</summary>
        public const float AssistStrength = 1f;
        /// <summary>Share of the player's own steering angle kept on top of the assist in a drift.</summary>
        public const float DriftPlayerSteer = 0.15f;
        /// <summary>Front wheel lock (deg) and how fast the assisted wheels may turn (deg/s).</summary>
        public const float MaxSteerLock = 38f, AssistSteerRate = 200f;
        /// <summary>Front grip lost when the front axle slides past the steering lock (the wheels cannot follow): no spin from the front.</summary>
        public const float FrontScrub = 0.5f;

        // ---- stability and speed
        /// <summary>Spin guard yaw acceleration (rad/s^2) per deg past <see cref="MaxDriftAngle"/> and per deg/s of growing slip; cap.</summary>
        public const float SpinGuardP = 0.8f, SpinGuardD = 0.03f, SpinGuardMax = 14f;
        /// <summary>Yaw-rate damper in a drift: yaw acceleration (rad/s^2) per rad/s of slip rate while the slip runs past the held angle.</summary>
        public const float YawDamper = 5f;
        /// <summary>Throttle in a drift pushes along the velocity (m/s^2) so a long slide keeps the speed it started with (at most <see cref="DriftMaxSpeed"/>).</summary>
        public const float DriftPush = 6f, DriftMaxSpeed = 30f;
        /// <summary>Engine force in a full drift (x this): the slide is fed by <see cref="DriftPush"/> along the velocity instead.</summary>
        public const float DriftEngine = 0.4f;
        /// <summary>Share of the brake force taken off when the kart is 40+ deg sideways (stability: braking mid-drift does not spin it).</summary>
        public const float SlideBrakeRelief = 0.75f;
        /// <summary>The drift unlatches below this slip angle (s to fully let go), slower without throttle, fast when braking / slow.</summary>
        public const float DriftExitSlip = 5f, DriftExitTime = 0.35f, DriftLiftTime = 2.5f, DriftBrakeTime = 0.8f;
        /// <summary>Hill hold: no throttle below this speed holds the kart on a slope.</summary>
        public const float HoldSpeed = 1.5f;
        /// <summary>Extra gravity (m/s^2) while all four wheels are off the ground (crests, bumps): back down sooner.</summary>
        public const float AirGravity = 5f;
    }
}
