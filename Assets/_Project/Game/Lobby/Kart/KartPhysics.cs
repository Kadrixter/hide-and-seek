using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// The go-kart's driving model as the rest of the kart code sees it. The physics itself is the third-party
    /// <see cref="ArcadeCar"/> (MIT, ThirdParty/ArcadeCarPhysics) on the kart's Rigidbody, tuned in <see cref="KartTuning"/>:
    /// it runs only on the driver's client (the Rigidbody is dynamic there, kinematic everywhere else). This class
    /// passes the controls in (W/S, A/D, Space), puts the kart on a spot, and reads back what the visuals, sounds,
    /// network pack and HUD need: speed, heading, yaw rate, slip angle, front wheel angle, slide strength, engine revs.
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

        private float _handbrakeHold;

        /// <summary>Adds the controller (off) to the kart; <paramref name="rb"/> is its kinematic Rigidbody.</summary>
        public void Attach(GameObject kart, Rigidbody rb)
        {
            Body = rb;
            Car = kart.AddComponent<ArcadeCar>();
            KartTuning.Apply(Car, rb);
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
            if (Car != null) foreach (var a in Car.axles) a.steerAngle = 0f;
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
            Sliding = (Mathf.Abs(SlipAngle) > 9f && speed > 3f) || (_handbrakeHold > 0.5f && speed > 3f);
            float screech = Mathf.Clamp01((Mathf.Abs(SlipAngle) - 6f) / 20f) * Mathf.Clamp01(speed / 4f);
            if (Handbrake) screech = Mathf.Max(screech, Mathf.Clamp01(speed / 6f));
            if (Braking) screech = Mathf.Max(screech, Mathf.Clamp01((speed - 8f) / 10f) * 0.6f);
            Slip01 = Mathf.MoveTowards(Slip01, screech, dt * 5f);
            float spin = Sliding && thr > 0.05f ? 0.25f * thr : 0f;
            float rpmT = Mathf.Clamp01(Mathf.Abs(ForwardSpeed) / MaxSpeed * 0.85f + 0.12f + (thr > 0.05f ? 0.06f : 0f) + spin);
            Rpm01 = Mathf.MoveTowards(Rpm01, rpmT, dt * (rpmT > Rpm01 ? 2.5f : 1.5f));
        }
    }
}
