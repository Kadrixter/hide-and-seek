using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Rally car cockpit, seen by the RC car camera from <see cref="DriverEye"/>: an inner shell (the body's inside,
    /// glass left open so the windows are see-through from inside), floor, firewall, dashboard with dials and a trip
    /// computer, roll cage, two blue buckets and the co-driver in a red suit with his pace notes. The steering wheel
    /// (with gloves on it) is its own mesh so it can turn with the steering (<see cref="SteeringWheel"/>).
    /// </summary>
    public static partial class RallyModel
    {
        /// <summary>Steering wheel centre in car space and its tilt (top leaning towards the dash).</summary>
        public static readonly Vector3 SteeringPos = new Vector3(-0.027f, 0.1185f, 0.022f);
        public static readonly Quaternion SteeringTilt = Quaternion.Euler(24f, 0f, 0f);

        static Mesh _steering;

        /// <summary>Steering wheel mesh: origin = its centre, rim in the local XY plane, facing -Z (the driver).</summary>
        public static Mesh SteeringWheel
        {
            get
            {
                if (_steering != null) return _steering;
                var m = new YarisMesh(Slots);
                const float R = 0.0135f, t = 0.0032f;
                const int seg = 16;
                for (int k = 0; k < seg; k++)
                {
                    float a0 = k * Mathf.PI * 2f / seg, a1 = (k + 1) * Mathf.PI * 2f / seg;
                    Tube(m, new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f) * R, new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f) * R, t, MTrim);
                }
                // three spokes (left, right, down) and the hub with a red top marker
                Tube(m, Vector3.zero, new Vector3(-R, 0f, 0f), 0.0022f, MChrome);
                Tube(m, Vector3.zero, new Vector3(R, 0f, 0f), 0.0022f, MChrome);
                Tube(m, Vector3.zero, new Vector3(0f, -R, 0f), 0.0022f, MChrome);
                m.Box(new Vector3(0f, 0f, 0.001f), new Vector3(0.0065f, 0.0065f, 0.003f), Quaternion.identity, MTrim, 0.4f);
                m.Box(new Vector3(0f, R, -0.0002f), new Vector3(0.0028f, 0.0036f, 0.0036f), Quaternion.identity, MRed, 0.2f);
                // gloves at ten to two
                foreach (float a in new[] { 150f, 30f })
                {
                    float r = a * Mathf.Deg2Rad;
                    var p = new Vector3(Mathf.Cos(r), Mathf.Sin(r), 0f) * R;
                    m.Ellipsoid(p + new Vector3(0f, 0f, -0.0015f), new Vector3(0.0042f, 0.0052f, 0.0042f), Quaternion.Euler(0f, 0f, a), MTrim, 10, 6);
                    m.Box(p * 1.15f + new Vector3(0f, -0.003f, -0.006f), new Vector3(0.0042f, 0.0042f, 0.01f), Quaternion.Euler(20f, 0f, 0f), MSuit, 0.4f);
                }
                _steering = m.ToMesh("Rally_SteeringWheel");
                return _steering;
            }
        }

        static void AddCabin(YarisMesh m, YarisLoft loft)
        {
            var I = Quaternion.identity;
            InnerShell(m, loft);

            // ---- floor, firewall, rear bulkhead
            m.Box(new Vector3(0f, 0.047f, -0.065f), new Vector3(0.19f, 0.004f, 0.27f), I, MCabin, 0.1f);
            m.Box(new Vector3(0f, 0.08f, ZCowl + 0.002f), new Vector3(0.18f, 0.066f, 0.004f), I, MCabin, 0.1f);

            // ---- dashboard: body, top slab, driver's binnacle with two dials, trip computer in the middle
            m.Box(new Vector3(0f, 0.104f, 0.063f), new Vector3(0.172f, 0.022f, 0.026f), I, MCabin, 0.3f);
            m.Box(new Vector3(0f, 0.1155f, 0.066f), new Vector3(0.172f, 0.004f, 0.022f), Quaternion.Euler(-12f, 0f, 0f), MTrim, 0.2f);
            m.Box(new Vector3(SteeringPos.x, 0.1175f, 0.056f), new Vector3(0.034f, 0.01f, 0.012f), I, MTrim, 0.4f);
            foreach (float dx in new[] { -0.0075f, 0.0075f })
                m.Ellipsoid(new Vector3(SteeringPos.x + dx, 0.118f, 0.0498f), new Vector3(0.0048f, 0.0048f, 0.0008f), I, MGauge, 12, 4);
            m.Box(new Vector3(0.016f, 0.1205f, 0.056f), new Vector3(0.018f, 0.011f, 0.004f), Quaternion.Euler(-15f, 0f, 0f), MTrim, 0.3f);
            m.Box(new Vector3(0.016f, 0.1205f, 0.0538f), new Vector3(0.0145f, 0.0075f, 0.0008f), Quaternion.Euler(-15f, 0f, 0f), MGauge, 0.1f);
            // steering column into the dash
            Tube(m, SteeringPos + SteeringTilt * new Vector3(0f, 0f, 0.003f), new Vector3(SteeringPos.x, 0.11f, 0.058f), 0.004f, MTrim);
            // handbrake (hydraulic, tall) and gear lever between the seats
            Tube(m, new Vector3(0.0f, 0.05f, -0.02f), new Vector3(0.0f, 0.1f, -0.01f), 0.003f, MChrome);
            m.Box(new Vector3(0f, 0.101f, -0.0095f), new Vector3(0.005f, 0.008f, 0.005f), I, MRed, 0.3f);
            Tube(m, new Vector3(0.0f, 0.05f, 0.012f), new Vector3(0.0f, 0.088f, 0.02f), 0.0026f, MChrome);
            m.Ellipsoid(new Vector3(0f, 0.09f, 0.0205f), new Vector3(0.004f, 0.004f, 0.004f), I, MTrim, 8, 5);

            // ---- roll cage (yellow)
            const float t = 0.0028f, zB = -0.07f;
            for (int s = -1; s <= 1; s += 2)
            {
                var a0 = new Vector3(s * 0.078f, 0.05f, 0.072f);
                var a1 = new Vector3(s * 0.08f, 0.113f, 0.07f);
                var aTop = new Vector3(s * 0.064f, 0.1655f, -0.003f);
                var b0 = new Vector3(s * 0.083f, 0.05f, zB);
                var b1 = new Vector3(s * 0.083f, 0.117f, zB);
                var bTop = new Vector3(s * 0.064f, 0.1665f, zB);
                Tube(m, a0, a1, t, MCage);
                Tube(m, a1, aTop, t, MCage);      // along the A-pillar
                Tube(m, aTop, bTop, t, MCage);    // roof rail
                Tube(m, b0, b1, t, MCage);
                Tube(m, b1, bTop, t, MCage);      // main hoop
                Tube(m, bTop, new Vector3(s * 0.07f, 0.075f, -0.175f), t, MCage);          // rear stay
                Tube(m, new Vector3(s * 0.082f, 0.058f, 0.06f), new Vector3(s * 0.084f, 0.104f, zB + 0.008f), t, MCage); // door bars (X)
                Tube(m, new Vector3(s * 0.082f, 0.104f, 0.06f), new Vector3(s * 0.084f, 0.058f, zB + 0.008f), t, MCage);
            }
            Tube(m, new Vector3(-0.064f, 0.1665f, zB), new Vector3(0.064f, 0.1665f, zB), t, MCage);
            Tube(m, new Vector3(-0.064f, 0.1655f, -0.003f), new Vector3(0.064f, 0.1655f, -0.003f), t, MCage);
            Tube(m, new Vector3(0.08f, 0.058f, zB), new Vector3(-0.064f, 0.1665f, zB), t, MCage); // hoop diagonal

            // ---- buckets
            foreach (float x in new[] { -0.028f, 0.03f })
            {
                m.Box(new Vector3(x, 0.058f, -0.052f), new Vector3(0.042f, 0.012f, 0.042f), I, MSeat, 0.4f);
                m.Box(new Vector3(x, 0.098f, -0.077f), new Vector3(0.044f, 0.078f, 0.009f), Quaternion.Euler(-10f, 0f, 0f), MSeat, 0.4f);
                foreach (int s in new[] { -1, 1 })
                    m.Box(new Vector3(x + s * 0.02f, 0.085f, -0.07f), new Vector3(0.006f, 0.05f, 0.026f), Quaternion.Euler(-10f, 0f, 0f), MSeat, 0.4f);
                m.Box(new Vector3(x, 0.066f, -0.034f), new Vector3(0.04f, 0.01f, 0.01f), I, MSeat, 0.4f); // thigh bolster
                // harness
                m.Box(new Vector3(x - 0.008f, 0.104f, -0.0705f), new Vector3(0.004f, 0.06f, 0.001f), Quaternion.Euler(-10f, 0f, 0f), MRed, 0.1f);
                m.Box(new Vector3(x + 0.008f, 0.104f, -0.0705f), new Vector3(0.004f, 0.06f, 0.001f), Quaternion.Euler(-10f, 0f, 0f), MRed, 0.1f);
            }

            // ---- the co-driver: red suit, white helmet, dark visor, pace notes in his hands
            const float cx = 0.03f;
            m.Box(new Vector3(cx, 0.097f, -0.062f), new Vector3(0.034f, 0.05f, 0.022f), Quaternion.Euler(-8f, 0f, 0f), MSuit, 0.5f);
            m.Box(new Vector3(cx, 0.067f, -0.022f), new Vector3(0.032f, 0.013f, 0.06f), I, MSuit, 0.5f);       // thighs
            m.Box(new Vector3(cx, 0.06f, 0.016f), new Vector3(0.028f, 0.03f, 0.012f), I, MSuit, 0.5f);         // shins
            m.Box(new Vector3(cx, 0.124f, -0.066f), new Vector3(0.011f, 0.008f, 0.01f), I, MSuit, 0.4f);       // neck
            m.Ellipsoid(new Vector3(cx, 0.1415f, -0.064f), new Vector3(0.0128f, 0.0142f, 0.0138f), I, MHelmet, 14, 8);
            m.Ellipsoid(new Vector3(cx, 0.1405f, -0.0518f), new Vector3(0.0095f, 0.006f, 0.0028f), I, MVisor, 12, 6);
            m.Box(new Vector3(cx, 0.1535f, -0.064f), new Vector3(0.004f, 0.003f, 0.024f), I, MBlue, 0.2f);   // helmet stripe
            foreach (int s in new[] { -1, 1 })
                Tube(m, new Vector3(cx + s * 0.018f, 0.113f, -0.06f), new Vector3(cx + s * 0.009f, 0.093f, -0.03f), 0.0075f, MSuit);
            m.Box(new Vector3(cx, 0.093f, -0.028f), new Vector3(0.024f, 0.0026f, 0.017f), Quaternion.Euler(-38f, 0f, 0f), MDisc, 0.2f);
            m.Box(new Vector3(cx, 0.0938f, -0.0275f), new Vector3(0.018f, 0.0006f, 0.012f), Quaternion.Euler(-38f, 0f, 0f), MInk, 0.05f);
        }

        /// <summary>
        /// The body seen from inside: the loft's quads over the cabin (sides from the flank up, roof, hatch) once more,
        /// facing in, in the cabin colour; glass left out (see-through from the cockpit).
        /// </summary>
        static void InnerShell(YarisMesh m, YarisLoft loft)
        {
            for (int j = 0; j < loft.Stations - 1; j++)
            {
                float z = (loft.Z[j] + loft.Z[j + 1]) * 0.5f;
                if (z > ZCowl + 0.003f || z < ZHatchB - 0.03f) continue;
                for (int i = RLow; i < RC; i++)
                {
                    int slot = BodySlot(z, i);
                    if (slot == MGlass) continue;
                    Vector3 a = loft.P(j, i), b = loft.P(j, i + 1), c = loft.P(j + 1, i + 1), d = loft.P(j + 1, i);
                    Vector3 n = Vector3.Cross(b - a + c - d, d - a + c - b);
                    if (n.sqrMagnitude < 1e-14f) continue;
                    n = -n.normalized; // the outside quad is (a, b, c, d); inside = reversed
                    // 1.5 mm in from the outer skin: never the same depth as the paint (no fighting, no self-shadow).
                    a += n * 0.0015f; b += n * 0.0015f; c += n * 0.0015f; d += n * 0.0015f;
                    m.Quad(a, d, c, b, n, n, n, n, MCabin);
                    Vector3 X(Vector3 v) => new Vector3(-v.x, v.y, v.z);
                    m.Quad(X(a), X(b), X(c), X(d), X(n), X(n), X(n), X(n), MCabin);
                }
            }
        }
    }
}
