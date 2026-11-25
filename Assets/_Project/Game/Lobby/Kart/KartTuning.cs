using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Go-kart settings for the third-party <see cref="ArcadeCar"/> controller (ThirdParty/ArcadeCarPhysics): 160 kg
    /// (kart + driver), centre of mass low behind the seat, raycast suspension sized for the small wheels, rear-wheel
    /// drive, a speed curve that reaches ~100 km/h in about 5.5 s and tops out at ~108 km/h, steering lock falling from
    /// 30 deg at a crawl to 7 deg flat out, and slippery tyres after the handbrake (the rear more than the front) for
    /// drifts that the throttle and counter-steer can hold. Values are the demo car's, scaled to the kart's mass.
    /// </summary>
    public static class KartTuning
    {
        public const float Mass = 160f;
        public const float TopSpeedKmh = 108f;

        public static void Apply(ArcadeCar car, Rigidbody rb)
        {
            rb.mass = Mass;
            rb.linearDamping = 0f;
            rb.angularDamping = 0.3f;
            car.centerOfMass = new Vector3(0f, 0.13f, -0.06f);
            car.debugDraw = false;
            car.showDebugGui = false;
            car.raycastMask = KartCollide.LevelMask;
            car.accelerationCurve = Rise(TopSpeedKmh, 2.2f, 12f);
            car.accelerationCurveReverse = Rise(26f, 1.4f, 6f);
            car.steerAngleLimit = Lines((0f, 30f), (40f, 17f), (110f, 7f));
            car.steeringResetSpeed = Lines((0f, 90f), (110f, 60f));
            car.steeringSpeed = Lines((0f, 3.2f), (40f, 1.7f), (110f, 0.75f));
            car.handBrakeSlipperyTime = 1.4f;
            car.downForceCurve = Lines((0f, 0f), (200f, 100f));
            car.downForce = 5f;
            car.flightStabilizationForce = 6f;
            car.flightStabilizationDamping = 0.7f;
            car.axles = new[]
            {
                Axle("Front", KartModel.FrontTrack * 2f, KartPhysics.FrontAxle, 0.28f, KartPhysics.FrontRadius, 2400f, 0.3f, 12f, false),
                Axle("Rear", KartModel.RearTrack * 2f, -KartPhysics.RearAxle, 0.287f, KartPhysics.RearRadius, 1900f, 0.15f, 4f, true),
            };
        }

        private static ArcadeCar.Axle Axle(string name, float width, float z, float y, float radius, float stiffness, float handBrakeK, float brake, bool powered) => new()
        {
            debugName = name,
            width = width,
            offset = new Vector2(z, y),
            radius = radius,
            laterialFriction = 0.6f,
            rollingFriction = 0.02f,
            brakeForceMag = brake,
            stiffness = stiffness,
            damping = 400f,
            lengthRelaxed = 0.18f,
            antiRollForce = 1100f,
            isPowered = powered,
            afterFlightSlipperyK = 0.02f,
            brakeSlipperyK = 0.5f,
            handBrakeSlipperyK = handBrakeK,
        };

        /// <summary>Speed (km/h) over time (s): top * (1 - e^(-t / tau)), sampled with matching tangents (monotonic).</summary>
        private static AnimationCurve Rise(float top, float tau, float until)
        {
            int n = Mathf.CeilToInt(until / 0.5f) + 1;
            var keys = new Keyframe[n];
            for (int i = 0; i < n; i++)
            {
                float t = i * 0.5f, e = Mathf.Exp(-t / tau);
                float slope = top / tau * e;
                keys[i] = new Keyframe(t, top * (1f - e), slope, slope);
            }
            return new AnimationCurve(keys);
        }

        /// <summary>Straight segments through the points (linear tangents).</summary>
        private static AnimationCurve Lines(params (float x, float y)[] p)
        {
            var keys = new Keyframe[p.Length];
            for (int i = 0; i < p.Length; i++)
            {
                float sIn = i > 0 ? (p[i].y - p[i - 1].y) / (p[i].x - p[i - 1].x) : 0f;
                float sOut = i + 1 < p.Length ? (p[i + 1].y - p[i].y) / (p[i + 1].x - p[i].x) : 0f;
                keys[i] = new Keyframe(p[i].x, p[i].y, i > 0 ? sIn : sOut, i + 1 < p.Length ? sOut : sIn);
            }
            return new AnimationCurve(keys);
        }
    }
}
