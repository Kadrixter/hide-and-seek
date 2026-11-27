using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Go-kart settings for the third-party <see cref="ArcadeCar"/> controller (ThirdParty/ArcadeCarPhysics): 160 kg
    /// (kart + driver), centre of mass low behind the seat, raycast suspension sized for the small wheels, rear-wheel
    /// drive through an automatic 4-speed gearbox (27.11, 120 km/h flat out, the low gears pull harder), steering lock
    /// falling from 27 deg at a crawl to ~5 deg flat out, a handbrake that brakes (no longer spins the kart). Values are the demo car's, scaled to the kart's mass.
    /// </summary>
    public static class KartTuning
    {
        public const float Mass = 160f;
        /// <summary>2026-11-27: 4 gears, 120 km/h flat out (was 170 km/h without gears).</summary>
        public const float TopSpeedKmh = 120f;

        // ---- gearbox (automatic): top speed and speed-rise time constant per gear. Acceleration at speed v in gear g is
        // (Top[g] - v) / Tau[g] km/h per s - the lower the gear, the harder it pulls (and the sooner it runs out).
        public static readonly float[] GearTop = { 42f, 70f, 97f, TopSpeedKmh + 1.5f };
        public static readonly float[] GearTau = { 1.05f, 1.9f, 2.9f, 4.2f };
        /// <summary>Shift up at this share of the gear's top speed; down when below this share of the lower gear's top.</summary>
        public const float ShiftUpAt = 0.95f, ShiftDownAt = 0.82f;
        /// <summary>No drive while the clutch is out (s).</summary>
        public const float ShiftTime = 0.22f;

        /// <summary>The speed-over-time curve of gear <paramref name="g"/> (0..3).</summary>
        public static AnimationCurve GearCurve(int g)
        {
            g = Mathf.Clamp(g, 0, GearTop.Length - 1);
            return Rise(GearTop[g], GearTau[g], GearTau[g] * 6f);
        }

        public static void Apply(ArcadeCar car, Rigidbody rb)
        {
            rb.mass = Mass;
            rb.linearDamping = 0f;
            rb.angularDamping = 0.3f;
            car.centerOfMass = new Vector3(0f, 0.13f, -0.06f);
            car.debugDraw = false;
            car.showDebugGui = false;
            car.raycastMask = KartCollide.LevelMask;
            car.accelerationCurve = GearCurve(0);
            car.accelerationCurveReverse = Rise(26f, 1.4f, 6f);
            // 2026-11-27 "skręcanie strasznie czułe": less lock at speed, the wheel turns about half as fast (deg per
            // physics step at 50 Hz: 2.0 = 100 deg/s at a crawl, 0.45 = 22 deg/s at 100 km/h) and comes back softer.
            car.steerAngleLimit = Lines((0f, 27f), (30f, 16f), (60f, 9.5f), (100f, 6f), (125f, 4.8f));
            car.steeringResetSpeed = Lines((0f, 80f), (60f, 55f), (125f, 40f));
            car.steeringSpeed = Lines((0f, 2.0f), (30f, 1.05f), (60f, 0.7f), (100f, 0.45f), (125f, 0.38f));
            // the handbrake brakes, it no longer makes the tyres slide (it spun the kart round): grip stays ~85 %
            car.handBrakeSlipperyTime = 0.3f;
            car.downForceCurve = Lines((0f, 0f), (200f, 100f), (300f, 160f));
            // Downforce (along the kart's down axis, only while a wheel touches): downForce * curve% m/s^2.
            // 2026-11-26: 5 -> 8 (100 km/h: 0.25 g -> 0.41 g extra, 170 km/h: 0.43 g -> 0.69 g) so the kart stays
            // planted over crests and banked corners. Grip in ArcadeCar does not depend on the wheel load, so this does
            // not change the cornering balance; it only keeps the wheels on the road (and compresses the springs).
            car.downForce = OldRide ? 5f : 8f;
            // A short hop (crest, step) no longer gives the full second of slippery tyres: that much airtime does.
            car.afterFlightFullAirTime = OldRide ? 0f : 0.35f;
            car.wideWheelContact = !OldRide;
            car.flightStabilizationForce = 6f;
            car.flightStabilizationDamping = 0.7f;
            car.axles = new[]
            {
                Axle("Front", KartModel.FrontTrack * 2f, KartPhysics.FrontAxle, 0.28f, KartPhysics.FrontRadius, FrontSpring, 0.9f, 12f, false),
                Axle("Rear", KartModel.RearTrack * 2f, -KartPhysics.RearAxle, 0.287f, KartPhysics.RearRadius, RearSpring, 0.85f, 4f, true),
            };
            if (OldRide)
            {
                // Before 2026-11-26 (comparison runs only): stiff springs, the strong damper, no blow-off.
                foreach (var a in car.axles)
                {
                    a.lengthRelaxed = 0.18f;
                    a.damping = 400f;
                    a.antiRollForce = 1100f;
                    a.bumpKneeSpeed = 0f;
                }
                car.axles[0].stiffness = 2400f;
                car.axles[1].stiffness = 1900f;
            }
        }

        /// <summary>
        /// Command line -kartoldride: the suspension / downforce / wheel contact from before 2026-11-26, for before / after
        /// comparisons of the ride (e.g. the circuit test's "ride:" line). Not for play.
        /// </summary>
        public static readonly bool OldRide = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-kartoldride") >= 0;

        // ---- suspension (per wheel, physical units; ArcadeCar wants them per unit of compression, i.e. times the length)
        // 2026-11-26 (it shook on bumps): the old damper (400 per unit / 0.18 m = 2220 N s/m per wheel) was too strong
        // for the 20 ms physics step - with the kart's small roll inertia (~20-25 kg m^2) the explicit damper overshot
        // every step (roll mode unstable, pitch / heave flip-flopping), so any unevenness set the chassis chattering,
        // and a 1 cm step became a ~1 m/s kick. Now: slightly softer springs (heave ~2.2 Hz, was ~2.75 Hz), damping
        // just under critical (no bounce) and stable at 50 Hz, blow-off for sharp steps, same static ride height.
        /// <summary>Relaxed length (m); was 0.18 - a little longer so the softer springs keep the old ride height.</summary>
        public const float SuspLength = 0.19f;
        /// <summary>Spring rate per wheel (N/m): front 13 300 -> 7 500, rear 10 600 -> 8 000 (static sag ~2.4 / 2.6 cm).</summary>
        public const float FrontSpring = 7500f, RearSpring = 8000f;
        /// <summary>Damper per wheel (N s/m): 2 220 -> 1 100 (heave damping ~0.95 of critical, roll ~0.8).</summary>
        public const float Damper = 1100f;
        /// <summary>Bump (compression) speed above which only <see cref="DamperBlowOffK"/> of the damping acts (m/s).</summary>
        public const float DamperKnee = 0.35f, DamperBlowOffK = 0.35f;
        /// <summary>Anti-roll bar per axle (N per m of left / right travel difference), as before (1100 / 0.18 m).</summary>
        public const float AntiRoll = 6100f;

        // ---- visual body (own kart): follows the chassis' height / tilt with a short lag (LobbyKart.BodyLag)
        /// <summary>Follow rate (1/s; 20 = ~50 ms lag), the most it may lag in height (m) and tilt (degrees).</summary>
        public const float BodyFollow = 20f, BodyLagMaxY = 0.03f, BodyLagMaxDeg = 2.5f;

        private static ArcadeCar.Axle Axle(string name, float width, float z, float y, float radius, float spring, float handBrakeK, float brake, bool powered) => new()
        {
            debugName = name,
            width = width,
            offset = new Vector2(z, y),
            radius = radius,
            laterialFriction = 0.6f,
            rollingFriction = 0.02f,
            brakeForceMag = brake,
            stiffness = spring * SuspLength,
            damping = Damper * SuspLength,
            lengthRelaxed = SuspLength,
            antiRollForce = AntiRoll * SuspLength,
            bumpKneeSpeed = DamperKnee,
            bumpHighSpeedK = DamperBlowOffK,
            isPowered = powered,
            afterFlightSlipperyK = 0.02f,
            brakeSlipperyK = 0.5f,
            handBrakeSlipperyK = handBrakeK,
        };

        /// <summary>Speed (km/h) over time (s): top * (1 - e^(-t / tau)), sampled with matching tangents (monotonic).</summary>
        internal static AnimationCurve Rise(float top, float tau, float until)
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
