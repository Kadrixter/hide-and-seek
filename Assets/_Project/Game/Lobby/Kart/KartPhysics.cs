using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// The go-kart's driving model as the rest of the kart code sees it. The physics itself is the third-party
    /// <see cref="ArcadeCar"/> (MIT, ThirdParty/ArcadeCarPhysics) on the kart's Rigidbody, tuned in <see cref="KartTuning"/>:
    /// it runs only on the driver's client (the Rigidbody is dynamic there, kinematic everywhere else). This class
    /// passes the controls in (W/S, A/D, Space), puts the kart on a spot, and reads back what the visuals, sounds,
    /// network pack and HUD need: speed, heading, yaw rate, slip angle, front wheel angle, slide strength, engine revs.
    /// Every physics step (before the tyre forces) it runs the drift model and countersteer assist
    /// (<see cref="KartDrift"/>): front wheel angle, axle grips, engine / brake scale, yaw and drive accelerations; plus
    /// a hill hold (no creeping down a slope when stopped) and a little extra gravity in the air (crests).
    /// The front wheel angle it sets is what <see cref="Steer"/> reports, so the wheels, the steering wheel and the
    /// other players (KartPack.Steer) all show the assisted steering.
    /// </summary>
    public sealed class KartPhysics
    {
        // ---- geometry (the model is built from these)
        public const float FrontAxle = 0.5f, RearAxle = 0.55f, Wheelbase = FrontAxle + RearAxle;
        public const float FrontRadius = 0.13f, RearRadius = 0.145f;
        /// <summary>Top speed, m/s (~108 km/h).</summary>
        public const float MaxSpeed = KartTuning.TopSpeedKmh / 3.6f;

        public Rigidbody Body { get; private set; }
        public ArcadeCar Car { get; private set; }
        public readonly KartCollide Collide = new();
        public readonly KartDrift Drift = new();

        // ---- input (set every frame by the driver)
        public float ThrottleIn, SteerIn;
        public bool Handbrake;

        // ---- state / outputs
        public Vector3 Pos => Body != null ? Body.position : Vector3.zero;
        public float Yaw => Body != null ? Body.rotation.eulerAngles.y : 0f;
        public Vector3 Vel => Body != null ? Body.linearVelocity : Vector3.zero;
        public float Speed => new Vector2(Vel.x, Vel.z).magnitude;
        /// <summary>Degrees per second, + = turning right.</summary>
        public float YawRate => Body != null ? Body.angularVelocity.y * Mathf.Rad2Deg : 0f;
        /// <summary>Front wheel angle now (degrees, + = right).</summary>
        public float Steer => Car != null && Car.axles != null && Car.axles.Length > 0 ? Car.axles[0].steerAngle : 0f;
        public float ForwardSpeed { get; private set; }
        /// <summary>Angle between heading and velocity (degrees, + = the velocity points right of the nose).</summary>
        public float SlipAngle { get; private set; }
        public float Rpm01 { get; private set; } = 0.12f;
        public float Slip01 { get; private set; }
        public bool Sliding { get; private set; }
        public bool Braking { get; private set; }
        /// <summary>Wheels touching the ground (last physics step).</summary>
        public int WheelsDown { get; private set; } = 4;

        private float _handbrakeHold, _airTime;

        /// <summary>Adds the controller (off) to the kart; <paramref name="rb"/> is its kinematic Rigidbody.</summary>
        public void Attach(GameObject kart, Rigidbody rb)
        {
            Body = rb;
            Car = kart.AddComponent<ArcadeCar>();
            KartTuning.Apply(Car, rb);
            Car.beforeForces = FixedStep;
            Car.enabled = false;
        }

        /// <summary>Dynamic + simulated on the driver's client; kinematic (moved by the network / parked) otherwise.</summary>
        public void SetDriven(bool on)
        {
            if (Body == null) return;
            Body.isKinematic = !on;
            Body.useGravity = on;
            Body.interpolation = on ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None;
            Body.collisionDetectionMode = on ? CollisionDetectionMode.ContinuousDynamic : CollisionDetectionMode.Discrete;
            Body.centerOfMass = Car.centerOfMass;
            Car.enabled = on;
            if (!on) { Car.inputVertical = 0f; Car.inputHorizontal = 0f; Car.inputHandBrake = false; }
        }

        /// <summary>Puts the kart at <paramref name="pos"/> facing <paramref name="yaw"/>, standing still.</summary>
        public void Reset(Vector3 pos, float yaw)
        {
            if (Body == null) return;
            var rot = Quaternion.Euler(0f, yaw, 0f);
            Body.transform.SetPositionAndRotation(pos, rot);
            Body.position = pos;
            Body.rotation = rot;
            if (!Body.isKinematic)
            {
                Body.linearVelocity = Vector3.zero;
                Body.angularVelocity = Vector3.zero;
            }
            if (Car != null)
            {
                foreach (var a in Car.axles) { a.steerAngle = 0f; a.gripScale = 1f; }
                Car.playerSteerAngle = 0f;
                Car.engineScale = Car.brakeScale = 1f;
            }
            Drift.Reset();
            Sliding = false;
            Slip01 = 0f;
            Rpm01 = 0.12f;
        }

        /// <summary>Driver's client, every frame: controls in, outputs out (the controller itself runs in FixedUpdate).</summary>
        public void Step(float dt)
        {
            if (Body == null || Car == null || dt <= 0f) return;
            float thr = Mathf.Clamp(ThrottleIn, -1f, 1f);
            Car.inputVertical = thr;
            Car.inputHorizontal = Mathf.Clamp(SteerIn, -1f, 1f);
            Car.inputHandBrake = Handbrake;

            Vector3 v = Vel;
            Vector3 fwd = Body.rotation * Vector3.forward;
            fwd.y = 0f;
            fwd.Normalize();
            float speed = new Vector2(v.x, v.z).magnitude;
            ForwardSpeed = Vector3.Dot(v, fwd);
            float side = Vector3.Dot(v, new Vector3(fwd.z, 0f, -fwd.x));
            SlipAngle = speed > 1f ? Mathf.Atan2(side, Mathf.Max(0.01f, Mathf.Abs(ForwardSpeed))) * Mathf.Rad2Deg : 0f;
            Braking = (thr < -0.4f && ForwardSpeed > 0.5f) || (thr > 0.4f && ForwardSpeed < -0.5f);
            _handbrakeHold = Handbrake ? 1f : Mathf.MoveTowards(_handbrakeHold, 0f, dt / 0.6f);
            Sliding = (Mathf.Abs(SlipAngle) > 9f && speed > 3f) || (_handbrakeHold > 0.5f && speed > 3f) || (Drift.Latch > 0.5f && speed > 3f);
            float screech = Mathf.Clamp01((Mathf.Abs(SlipAngle) - 6f) / 20f) * Mathf.Clamp01(speed / 4f);
            if (Handbrake) screech = Mathf.Max(screech, Mathf.Clamp01(speed / 6f));
            if (Braking) screech = Mathf.Max(screech, Mathf.Clamp01((speed - 8f) / 10f) * 0.6f);
            Slip01 = Mathf.MoveTowards(Slip01, screech, dt * 5f);
            float spin = Sliding && thr > 0.05f ? 0.25f * thr : 0f;
            float rpmT = Mathf.Clamp01(Mathf.Abs(ForwardSpeed) / MaxSpeed * 0.85f + 0.12f + (thr > 0.05f ? 0.06f : 0f) + spin);
            Rpm01 = Mathf.MoveTowards(Rpm01, rpmT, dt * (rpmT > Rpm01 ? 2.5f : 1.5f));
        }

        /// <summary>
        /// ArcadeCar.beforeForces, every physics step on the driver's client: the drift model in, its results out to
        /// the controller (front wheel angle, axle grips, engine / brake scale) and to the body (yaw and drive
        /// accelerations), then the hill hold and the extra gravity in the air.
        /// </summary>
        private void FixedStep(float dt)
        {
            if (Body == null || Car == null || Body.isKinematic) return;
            var front = Car.axles[0];
            var rear = Car.axles[1];
            int down = (front.wheelDataL.isOnGround ? 1 : 0) + (front.wheelDataR.isOnGround ? 1 : 0) +
                       (rear.wheelDataL.isOnGround ? 1 : 0) + (rear.wheelDataR.isOnGround ? 1 : 0);
            WheelsDown = down;
            Quaternion inv = Quaternion.Inverse(Body.rotation);
            Vector3 v = inv * Body.linearVelocity;
            Vector3 w = inv * Body.angularVelocity;
            Drift.Step(new KartDrift.Input
            {
                Dt = dt,
                VFwd = v.z,
                VLat = v.x,
                YawRate = w.y,
                FrontArm = FrontAxle - Car.centerOfMass.z,
                Throttle = Car.inputVertical,
                Steer = Car.inputHorizontal,
                Handbrake = Car.inputHandBrake,
                Grounded = down >= 2,
                PlayerSteerDeg = Car.playerSteerAngle,
            });
            front.steerAngle = Drift.FrontSteerDeg;
            front.gripScale = Drift.FrontGrip;
            rear.gripScale = Drift.RearGrip;
            Car.engineScale = Drift.EngineScale;
            Car.brakeScale = Drift.BrakeScale;
            if (down >= 2)
            {
                if (Mathf.Abs(Drift.YawAccel) > 1e-4f) Body.AddRelativeTorque(0f, Drift.YawAccel, 0f, ForceMode.Acceleration);
                Vector3 vel = Body.linearVelocity;
                if (Drift.DriveAccel > 1e-4f && vel.sqrMagnitude > 1f) Body.AddForce(vel.normalized * Drift.DriveAccel, ForceMode.Acceleration);
                // Hill hold: stopped with no throttle, the slope does not roll the kart away.
                if (Mathf.Abs(Car.inputVertical) < 0.1f && vel.magnitude < KartTuning.HoldSpeed)
                {
                    Vector3 n = Body.rotation * Vector3.up;
                    Vector3 slide = Physics.gravity - Vector3.Dot(Physics.gravity, n) * n;
                    Vector3 drift = vel - Vector3.Dot(vel, n) * n;
                    Body.AddForce(-slide - drift * 3f, ForceMode.Acceleration);
                }
                _airTime = 0f;
            }
            else if (down == 0)
            {
                // Briefly airborne (over a crest): a little extra gravity brings the wheels back down sooner.
                _airTime += dt;
                if (_airTime < 1.5f) Body.AddForce(Vector3.down * KartTuning.AirGravity, ForceMode.Acceleration);
            }
        }
    }
}
