using UnityEngine;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// Crash damage as a deformation field over the finished front (folds, dents, the right front corner pushed back,
    /// in and down), the three remaining GTI wheels and the right front corner resting on its torn-off hub.
    /// </summary>
    internal sealed partial class GolfWreck
    {
        /// <summary>Crush of the front: 0 behind the windscreen base, strongest at the right front corner.</summary>
        private static float CrushWeight(Vector3 p)
        {
            float right = WreckKit.Smooth(-0.45f, 0.65f, p.x);
            float front = Mathf.Clamp01((p.z - 0.92f) / 1.05f);
            return front * front * (0.22f + 0.78f * right);
        }

        private static Vector3 Crush(Vector3 p)
        {
            float k = CrushWeight(p);
            if (k <= 0f) return p;
            float right = WreckKit.Smooth(-0.3f, 0.6f, p.x);
            Vector3 q = p * 5.5f;
            float n1 = WreckKit.Noise2(q), n2 = WreckKit.Noise2(q + new Vector3(17.2f, 3.1f, 8.4f)), n3 = WreckKit.Noise2(q + new Vector3(4.4f, 21.7f, 1.3f));
            float fine = WreckKit.Noise2(p * 17f + new Vector3(2.2f, 9.1f, 5.5f));
            // accordion folds across the wing, crumples everywhere
            float fold = Mathf.Sin(p.z * 27f + p.y * 9f + n1 * 3f) * 0.02f;
            var d = new Vector3(
                Mathf.Sign(p.x) * fold * Mathf.Clamp01(Mathf.Abs(p.x) / 0.6f) + n1 * 0.06f + fine * 0.015f,
                n2 * 0.045f + fine * 0.012f - 0.075f * right * k,
                -0.21f * (0.45f + 0.55f * right) + n3 * 0.05f);
            d *= k;
            // the right front corner folds inwards, the very nose more
            d.x -= 0.11f * k * right * WreckKit.Smooth(1.45f, 1.95f, p.z) * WreckKit.Smooth(0.3f, 0.75f, p.x);
            // the right wing top sags towards the torn edge
            d.y -= 0.05f * right * WreckKit.Smooth(1.2f, 1.9f, p.z) * WreckKit.Smooth(0.5f, 0.8f, p.x);
            return p + d;
        }

        /// <summary>Scrapes on the right front, finger writing on the right rear door, crack web on the screen.</summary>
        private void SetMaskPlanes()
        {
            Vector3 R(Vector3 v) => _tilt * v;
            // scrapes: z 0 .. 2.05 along the right flank, y 0.12 .. 0.92
            Lib.SetWreckPlane("Wreck_Paint", "_Scrape", Pose(new Vector3(0.86f, 0.12f, 0.0f)), R(Vector3.forward / 2.05f), R(Vector3.up / 0.8f));
            // writing: right rear door and wing, 1.3 x 0.46 m
            Lib.SetWreckPlane("Wreck_Paint", "_Write", Pose(new Vector3(0.86f, 0.55f, -1.35f)), R(Vector3.forward / 1.3f), R(Vector3.up / 0.46f));
            // windscreen: across 1.5 m, up the slope from the base (z 0.95, y 0.95) to the roof (z 0.05, y 1.425)
            Vector3 slope = new Vector3(0f, 1.425f - 0.95f, 0.05f - 0.95f);
            Lib.SetWreckPlane("Wreck_Windscreen", "_Crack", Pose(new Vector3(-0.75f, 0.95f, 0.95f)), R(Vector3.right / 1.5f), R(slope.normalized / slope.magnitude));
        }

        // ------------------------------------------------------------------ wheels and the hub

        private void Wheels()
        {
            foreach (var (c, side) in _wheels)
            {
                if (side > 0f && c.z > 0f) continue;                 // right front: ripped off (lies next to the car)
                var p = Pose(c);
                p.y = TireRadius;
                CarWheels.Wheel(Mb, p, side, TireRadius, TireWidth, RimRadius, RimStyle.TenSpoke, Lib["Wreck_Rim"]);
            }
            HubOnGround();
        }

        /// <summary>Right front corner: brake disc and hub on the floor, broken strut, lower arm and driveshaft to it.</summary>
        private void HubOnGround()
        {
            var rust = Rust;
            Vector3 ax = new Vector3(0.93f, -0.16f, 0.33f).normalized;
            Vector3 hc = new Vector3(0.74f, 0.146f, 1.34f);
            // vented disc (two faces, rusty), hub flange with studs, calliper, knuckle
            float t = 0.024f;
            CarWheels.Lathe(Mb, hc, ax, new[] { new Vector2(0.06f, -t), new Vector2(0.144f, -t), new Vector2(0.144f, t), new Vector2(0.06f, t) }, 32, Lib["Wreck_Disc"], 40f);
            CarWheels.Lathe(Mb, hc + ax * t, ax, new[] { new Vector2(0.075f, 0f), new Vector2(0.075f, 0.012f), new Vector2(0.03f, 0.016f), new Vector2(0f, 0.018f) }, 24, rust);
            Vector3 e1 = Vector3.Cross(ax, Vector3.up).normalized, e2 = Vector3.Cross(ax, e1);
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 2f / 5f;
                Vector3 d = Mathf.Cos(a) * e1 + Mathf.Sin(a) * e2;
                Mb.Cylinder(hc + ax * (t + 0.01f) + d * 0.05f, hc + ax * (t + 0.045f) + d * 0.05f, 0.0065f, 6, Lib["Wreck_Steel"]);
            }
            Vector3 up = Vector3.Cross(e1, ax).normalized;
            if (up.y < 0f) up = -up;
            var krot = Quaternion.LookRotation(ax, up);
            CarBlob.Blob(Mb, hc - ax * 0.05f + up * 0.03f, krot, new Vector3(0.06f, 0.13f, 0.045f), 0.45f, rust, 0.6f);
            Mb.Box(hc - ax * 0.005f + up * 0.11f - e1 * 0.06f, new Vector3(0.07f, 0.06f, 0.11f), Lib["Wreck_Plastic"], krot * Quaternion.Euler(0, 0, 25f));
            // CV joint stub pointing out of the hub (side photo)
            CarWheels.Lathe(Mb, hc - ax * 0.08f, -ax, new[] { new Vector2(0.045f, 0f), new Vector2(0.045f, 0.05f), new Vector2(0.03f, 0.07f), new Vector2(0f, 0.07f) }, 16, rust);

            // the parts still hanging from the car (positions on the crushed, leaning body)
            Vector3 tower = Pose(Crush(new Vector3(0.56f, 0.76f, 1.205f)));
            Vector3 subframe = Pose(Crush(new Vector3(0.32f, 0.2f, 1.18f)));
            Vector3 diff = Pose(new Vector3(-0.02f, 0.33f, 1.28f));
            Vector3 knuckleTop = hc - ax * 0.05f + up * 0.16f;
            // broken strut: lower half on the knuckle, upper half with the spring hanging from the tower
            Vector3 lowEnd = knuckleTop + (tower - knuckleTop).normalized * 0.2f;
            Mb.Cylinder(knuckleTop, lowEnd, 0.026f, 12, Lib["Wreck_Engine"]);
            Vector3 hang = tower + (new Vector3(0.12f, -0.38f, 0.07f));
            Mb.Cylinder(tower, hang, 0.02f, 10, Lib["Wreck_Steel"]);
            Coil(tower + (hang - tower) * 0.1f, hang - (hang - tower) * 0.25f, 0.07f, 5.5f, 0.009f);
            // lower control arm bent down to the knuckle, driveshaft with its boots
            WreckKit.Tube(Mb, WreckKit.Spline(new[] { subframe, Vector3.Lerp(subframe, hc, 0.5f) + Vector3.down * 0.03f, hc - ax * 0.04f - up * 0.09f }), 0.018f, Metal, 8);
            Vector3 inner = hc - ax * 0.12f;
            WreckKit.Tube(Mb, WreckKit.Spline(new[] { diff, Vector3.Lerp(diff, inner, 0.5f) + Vector3.down * 0.04f, inner }), 0.013f, Lib["Wreck_Steel"], 8);
            WreckKit.Bellows(Mb, inner + (diff - inner).normalized * 0.01f, inner + (diff - inner).normalized * 0.13f, 0.045f, 0.022f, 5, Rubber);
            WreckKit.Bellows(Mb, diff + (inner - diff).normalized * 0.02f, diff + (inner - diff).normalized * 0.12f, 0.05f, 0.022f, 4, Rubber);
            // brake hose and ABS wire looping down to the hub
            WreckKit.Tube(Mb, WreckKit.Spline(new[] { tower + new Vector3(-0.02f, -0.05f, -0.04f), tower + new Vector3(0.06f, -0.3f, -0.1f), knuckleTop + new Vector3(0.05f, 0.08f, -0.08f), hc - ax * 0.02f + up * 0.12f }), 0.005f, Lib["Wreck_Plastic"], 6);
            WreckKit.Tube(Mb, WreckKit.Spline(new[] { tower + new Vector3(-0.05f, -0.02f, 0.02f), tower + new Vector3(0.1f, -0.25f, 0.12f), hc + new Vector3(0.03f, 0.05f, 0.1f), hc - ax * 0.03f }), 0.003f, Lib["Wreck_Plastic"], 5);
            // wooden blocks under the right front crossmember (front photo)
            var wood = Lib["Wood"];
            Mb.Box(new Vector3(0.48f, 0.04f, 1.75f), new Vector3(0.12f, 0.08f, 0.5f), wood, Quaternion.Euler(0, 14f, 0));
            Mb.Box(new Vector3(0.46f, 0.12f, 1.78f), new Vector3(0.45f, 0.08f, 0.11f), wood, Quaternion.Euler(0, -8f, 2f));
        }

        /// <summary>Coil spring from a to b.</summary>
        private void Coil(Vector3 a, Vector3 b, float r, float turns, float wire)
        {
            Vector3 axis = (b - a).normalized;
            Vector3 e1 = Vector3.Cross(axis, Vector3.right).normalized, e2 = Vector3.Cross(axis, e1);
            var path = new System.Collections.Generic.List<Vector3>();
            int n = Mathf.CeilToInt(turns * 16);
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n, ang = t * turns * Mathf.PI * 2f;
                path.Add(Vector3.Lerp(a, b, t) + (e1 * Mathf.Cos(ang) + e2 * Mathf.Sin(ang)) * r);
            }
            WreckKit.Tube(Mb, path, wire, Lib["Wreck_Rust"], 6);
        }
    }
}
