using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Arcade drift physics of the lobby go-kart (driver's client only), a two-wheel "bicycle" model with saturating
    /// tyres: each axle's sideways grip follows its slip angle up to a peak (~11 deg) and falls off a little past it.
    /// The rear axle is driven, so throttle in a corner eats into its side grip (friction circle) and the tail steps
    /// out; the handbrake (Space) cuts the rear grip to <see cref="HandbrakeGrip"/> and kicks a slide off. Once
    /// sliding with the throttle open the rear keeps only <see cref="SlideGrip"/> of its grip (sliding friction), so a
    /// drift carries on as long as the driver stays on the gas and counter-steers (steering towards the velocity turns
    /// the front tyres' force against the spin); the steering gets full lock and a quicker rack for that. Lifting off
    /// gives the grip back and the kart straightens. A stabiliser damps the spin past ~30 deg of slip (no endless
    /// spin-outs in a small room). Below ~1.5 m/s and in reverse a plain kinematic model takes over (no jitter at a
    /// crawl). Collisions: <see cref="KartCollide"/>. Tuned offline against scripted drivers (drift held 95 % of the
    /// time with counter-steer, ~2.5 m donuts at full lock). Units: metres, seconds, degrees in the API.
    /// </summary>
    public sealed class KartPhysics
    {
        // ---- geometry / mass (kart + driver)
        public const float FrontAxle = 0.5f, RearAxle = 0.55f, Wheelbase = FrontAxle + RearAxle;
        public const float Mass = 160f, Inertia = 58f, CgHeight = 0.28f;
        public const float FrontRadius = 0.13f, RearRadius = 0.145f;

        // ---- tuning
        public const float MaxSpeed = 6.3f;          // m/s flat out (the lobby is 16 x 12 m)
        public const float ReverseMax = 2.4f;
        public const float EngineAccel = 5.2f;       // m/s^2 from standstill
        public const float BrakeDecel = 9.5f;
        public const float HandbrakeDecel = 2.2f;    // rear wheels locked: slows, but the slide carries on
        public const float CoastDecel = 0.8f, AirDrag = 0.025f;
        public const float FrontGrip = 1.05f, RearGrip = 0.95f; // tyre friction (a touch of oversteer)
        public const float HandbrakeGrip = 0.36f;    // rear grip while Space is held
        public const float SlideGrip = 0.85f;        // rear grip while sliding on the throttle
        public const float TyreB = 12f, TyreC = 1.35f; // Pacejka-lite shape: peak at ~11 deg, ~85 % far past it
        public const float SteerLow = 32f, SteerHigh = 22f, SteerSlide = 32f, SteerRate = 210f;
        public const float YawDamping = 1.2f;        // light, always
        public const float Stabiliser = 6f, StabFrom = 30f, StabFull = 65f; // extra yaw damping in a big slide

        // ---- state
        public Vector3 Pos;
        /// <summary>Heading, degrees (Unity yaw: + = clockwise seen from above).</summary>
        public float Yaw;
        public Vector3 Vel;
        /// <summary>Degrees per second, + = turning right.</summary>
        public float YawRate;
        /// <summary>Front wheel angle now (degrees, + = right).</summary>
        public float Steer;

        // ---- input (set every frame by the driver)
        public float ThrottleIn, SteerIn;
        public bool Handbrake;

        // ---- outputs for visuals / sound / tests
        public float Rpm01 { get; private set; }
        public float Slip01 { get; private set; }
        public bool Sliding { get; private set; }
        public bool Braking { get; private set; }
        /// <summary>Angle between heading and velocity (degrees, + = the velocity points right of the nose).</summary>
        public float SlipAngle { get; private set; }
        public float ForwardSpeed { get; private set; }
        public float LongAccel { get; private set; }
        public float LatAccel { get; private set; }
        /// <summary>Rear wheel surface speed (spins up in a power slide, 0 with the handbrake).</summary>
        public float RearWheelSpeed { get; private set; }
        public float Speed => new Vector2(Vel.x, Vel.z).magnitude;

        private float _ax;
        public readonly KartCollide Collide = new();

        public void Reset(Vector3 pos, float yaw)
        {
            Pos = pos;
            Yaw = yaw;
            Vel = Vector3.zero;
            YawRate = 0f;
            Steer = 0f;
            _ax = 0f;
            Sliding = false;
            Slip01 = 0f;
            Rpm01 = 0.12f;
        }

        /// <summary>Advances <paramref name="dt"/> seconds (fixed sub-steps of 1/240 s; collisions every other one).</summary>
        public void Step(float dt, Transform self)
        {
            if (dt <= 0f) return;
            dt = Mathf.Min(dt, 0.1f);
            int n = Mathf.Max(1, Mathf.CeilToInt(dt * 240f));
            float h = dt / n;
            for (int i = 0; i < n; i++)
            {
                SubStep(h);
                if ((i & 1) == 1 || i == n - 1) Collide.Resolve(this, self);
            }
            Collide.Ground(this, dt);
        }

        private static float Tyre(float alpha) => Mathf.Sin(TyreC * Mathf.Atan(TyreB * alpha));

        private void SubStep(float h)
        {
            const float g = 9.81f;
            float psi = Yaw * Mathf.Deg2Rad;
            var f = new Vector3(Mathf.Sin(psi), 0f, Mathf.Cos(psi));
            var r = new Vector3(f.z, 0f, -f.x);
            float vf = Vector3.Dot(Vel, f), vs = Vector3.Dot(Vel, r);
            float speed = new Vector2(vf, vs).magnitude;
            float w = YawRate * Mathf.Deg2Rad;
            float thr = Mathf.Clamp(ThrottleIn, -1f, 1f);

            // Steering: less lock at speed, full lock in a slide; quicker back to centre / across (counter-steer).
            float lim = Mathf.Lerp(SteerLow, SteerHigh, Mathf.InverseLerp(1.5f, MaxSpeed, Mathf.Abs(vf)));
            if (Sliding) lim = Mathf.Max(lim, SteerSlide);
            float target = Mathf.Clamp(SteerIn, -1f, 1f) * lim;
            bool back = target * Steer < 0f || Mathf.Abs(target) < Mathf.Abs(Steer);
            Steer = Mathf.MoveTowards(Steer, target, SteerRate * (back ? 1.8f : 1f) * h);
            float d = Steer * Mathf.Deg2Rad;

            // Longitudinal: engine on the rear axle, brakes, handbrake, rolling / air drag.
            float drive = 0f, brake = 0f;
            if (thr > 0.05f)
            {
                if (vf < -0.4f) brake = BrakeDecel * thr;
                else drive = EngineAccel * thr * Mathf.Max(0f, 1f - (vf / MaxSpeed) * (vf / MaxSpeed));
            }
            else if (thr < -0.05f)
            {
                if (vf > 0.4f) brake = BrakeDecel * -thr;
                else drive = -EngineAccel * 0.6f * -thr * Mathf.Clamp01(1f + vf / ReverseMax);
            }
            Braking = brake > 0f;
            if (Handbrake) brake += HandbrakeDecel;
            brake += CoastDecel + AirDrag * speed * speed;

            // Loads with pitch transfer (braking loads the front: lift-off / brake oversteer).
            const float L = Wheelbase;
            float nf = Mathf.Max(Mass * g * RearAxle / L - Mass * _ax * CgHeight / L, 0.1f * Mass * g);
            float nr = Mathf.Max(Mass * g * FrontAxle / L + Mass * _ax * CgHeight / L, 0.1f * Mass * g);

            // Dynamic (bicycle) model.
            float vfx = Mathf.Max(vf, 0.6f);
            float af = Mathf.Atan2(vs + w * FrontAxle, vfx) - d;
            float ar = Mathf.Atan2(vs - w * RearAxle, vfx);
            float ff = -FrontGrip * nf * Tyre(af);
            float rearCap = RearGrip * nr * (Handbrake ? HandbrakeGrip : 1f) * (Sliding && thr > 0.3f ? SlideGrip : 1f);
            float fx = Mass * Mathf.Max(0f, drive);
            float frMax = Mathf.Sqrt(Mathf.Max(rearCap * rearCap * 0.0625f, rearCap * rearCap - fx * fx));
            float fr = -frMax * Tyre(ar);
            float cosD = Mathf.Cos(d), sinD = Mathf.Sin(d);
            float ay = (ff * cosD + fr) / Mass;
            float beta0 = Mathf.Atan2(vs, Mathf.Max(0.3f, Mathf.Abs(vf))) * Mathf.Rad2Deg;
            float stab = w * beta0 < 0f ? Stabiliser * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(StabFrom, StabFull, Mathf.Abs(beta0))) : 0f;
            float yawAcc = (FrontAxle * ff * cosD - RearAxle * fr) / Inertia - (YawDamping + stab) * w;

            // Kinematic weight: crawling or reversing = no tyre model.
            float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.9f, 2.0f, speed)) * (vf > 0f ? 1f : Mathf.InverseLerp(-0.6f, 0f, vf));

            // Longitudinal acceleration (the front tyres' side force also scrubs speed when steered).
            float sgn = Mathf.Abs(vf) > 0.05f ? Mathf.Sign(vf) : 0f;
            float ax = drive - k * ff * sinD / Mass;
            float free = vf + ax * h, stop = brake * h, vfNew = free;
            if (sgn > 0f) vfNew = Mathf.Max(free - stop, Mathf.Min(0f, free));
            else if (sgn < 0f) vfNew = Mathf.Min(free + stop, Mathf.Max(0f, free));
            else if (Mathf.Abs(drive) < 0.01f) vfNew = 0f;

            float wDyn = w + yawAcc * h;
            float vsDyn = vs + (ay - vf * w) * h;
            float wKin = vf / L * Mathf.Tan(d);
            float vsKin = vs * Mathf.Exp(-12f * h);
            w = Mathf.Lerp(wKin, wDyn, k);
            vs = Mathf.Lerp(vsKin, vsDyn, k);

            _ax = Mathf.Lerp(_ax, (vfNew - vf) / h, 1f - Mathf.Exp(-6f * h));
            LongAccel = _ax;
            LatAccel = Mathf.Lerp(LatAccel, vf * w, 1f - Mathf.Exp(-8f * h));
            vf = vfNew;

            Vel = f * vf + r * vs;
            YawRate = w * Mathf.Rad2Deg;
            Yaw = Mathf.Repeat(Yaw + YawRate * h, 360f);
            Pos += Vel * h;

            // Slide state, screech / smoke strength, engine.
            float arDeg = Mathf.Abs(ar) * Mathf.Rad2Deg * k;
            SlipAngle = speed > 0.3f ? Mathf.Atan2(vs, Mathf.Max(0.01f, Mathf.Abs(vf))) * Mathf.Rad2Deg : 0f;
            ForwardSpeed = vf;
            bool slide = (arDeg > 9f && speed > 1.6f) || (Handbrake && speed > 1.8f);
            Sliding = slide || (Sliding && arDeg > 5f && speed > 1.2f);
            float spin = thr > 0.05f && Sliding && !Handbrake ? 2.8f * thr : 0f;
            RearWheelSpeed = Handbrake ? 0f : vf + spin;
            float screech = Mathf.Clamp01((arDeg - 6f) / 18f) * Mathf.Clamp01(speed / 2.5f);
            if (Handbrake) screech = Mathf.Max(screech, Mathf.Clamp01(speed / 3f));
            if (Braking) screech = Mathf.Max(screech, Mathf.Clamp01((Mathf.Abs(vf) - 3f) / 3f) * 0.5f);
            Slip01 = Mathf.MoveTowards(Slip01, screech, h * 6f);
            float rpmT = Mathf.Clamp01(Mathf.Abs(RearWheelSpeed) / MaxSpeed * 0.85f + 0.12f + (thr > 0.05f ? 0.1f : 0f));
            Rpm01 = Mathf.MoveTowards(Rpm01, rpmT, h * (rpmT > Rpm01 ? 2.2f : 1.5f));
        }
    }
}
