using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>The moving parts of a built go-kart (<see cref="KartModel.Build"/>).</summary>
    public sealed class KartParts
    {
        /// <summary>Everything sprung (frame, bodywork, seat, engine): pitches and rolls with the driving.</summary>
        public Transform Body;
        /// <summary>Front wheels: [0] left, [1] right; steer pivot (Y) and the wheel itself (spins round X).</summary>
        public readonly Transform[] FrontSteer = new Transform[2];
        public readonly Transform[] FrontWheel = new Transform[2];
        /// <summary>Rear wheels: [0] left, [1] right (spin round X).</summary>
        public readonly Transform[] RearWheel = new Transform[2];
        /// <summary>Steering wheel (turns round its own local Z, the column axis).</summary>
        public Transform SteeringWheel;
        /// <summary>Seat point the driver's pose hangs on: on the seat pan, +Z = where the driver faces.</summary>
        public Transform Seat;
        /// <summary>Exhaust tip (smoke puffs).</summary>
        public Transform Exhaust;
        /// <summary>Brake light glow (emission follows the brake / handbrake).</summary>
        public Renderer BrakeLight;
    }

    /// <summary>
    /// Procedural go-kart in the game's blocky-but-detailed style (runtime meshes, no assets): chrome tube frame with
    /// a floor tray, red nose cone, front panel with a number plate, side pods, black bucket seat with bolsters, a
    /// single-cylinder engine beside the seat (finned head, pull start, air box, chain to the rear axle, exhaust and
    /// silencer), rear bumper, pedals, a low fuel tank between the legs, the steering column and a three-spoke wheel,
    /// and four wheels with slick tyres and silver rims (27.11). Sized for the blocky character (1.8 m): 1.7 x 1.25 m.
    /// Kart space: origin on the floor between the axles, +Z forward.
    /// </summary>
    public static partial class KartModel
    {
        public const float FrontZ = KartPhysics.FrontAxle, RearZ = -KartPhysics.RearAxle;
        public const float FrontTrack = 0.5f, RearTrack = 0.53f;
        public const float FrontWidth = 0.12f, RearWidth = 0.19f;
        /// <summary>Seat point (kart space) the driver sits on.</summary>
        public static readonly Vector3 SeatPos = new(0f, 0.12f, -0.26f);
        /// <summary>Steering wheel centre and its tilt (the column leans back towards the driver).</summary>
        public static readonly Vector3 WheelPos = new(0f, 0.5f, 0.0f);
        public static readonly Quaternion WheelTilt = Quaternion.Euler(40f, 0f, 0f); // local +Z runs down the column, away from the driver
        public const float WheelRimRadius = 0.15f;

        private static Material _red, _black, _chrome, _yellow, _tyre, _grey, _dark, _white, _brake;

        private static Material Mat(ref Material m, string n, Color c, float gloss, float metal = 0f)
        {
            if (m != null) return m;
            m = new Material(Shader.Find("Standard")) { name = n, color = c };
            m.SetFloat("_Glossiness", gloss);
            m.SetFloat("_Metallic", metal);
            return m;
        }

        internal static Material Red => Mat(ref _red, "KartRed", new Color(0.82f, 0.07f, 0.05f), 0.75f);
        internal static Material Black => Mat(ref _black, "KartBlack", new Color(0.06f, 0.06f, 0.07f), 0.35f);
        internal static Material Chrome => Mat(ref _chrome, "KartChrome", new Color(0.78f, 0.79f, 0.82f), 0.85f, 0.85f);
        /// <summary>Polished silver rims (27.11: were gold / yellow).</summary>
        internal static Material RimSilver => Mat(ref _yellow, "KartRimSilver", new Color(0.8f, 0.81f, 0.84f), 0.82f, 0.9f);
        internal static Material Tyre => Mat(ref _tyre, "KartTyre", new Color(0.05f, 0.05f, 0.055f), 0.32f); // slick: a little sheen
        internal static Material Grey => Mat(ref _grey, "KartEngine", new Color(0.4f, 0.41f, 0.44f), 0.45f, 0.5f);
        internal static Material Dark => Mat(ref _dark, "KartDark", new Color(0.16f, 0.16f, 0.18f), 0.3f, 0.4f);
        internal static Material White => Mat(ref _white, "KartWhite", new Color(0.93f, 0.93f, 0.9f), 0.5f);

        /// <summary>Brake light: emissive red, its own material instance per kart (the glow follows the brakes).</summary>
        internal static Material BrakeMat()
        {
            var m = new Material(Shader.Find("Standard")) { name = "KartBrakeLight", color = new Color(0.5f, 0.02f, 0.02f) };
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", new Color(0.3f, 0f, 0f));
            return m;
        }

        /// <summary>Builds the kart under <paramref name="root"/> (kart space = root space).</summary>
        public static KartParts Build(Transform root, KartLivery livery)
        {
            var parts = new KartParts();
            var body = new GameObject("Body").transform;
            body.SetParent(root, false);
            parts.Body = body;

            BuildFrame(body);
            BuildBodywork(body, parts, livery);
            BuildSeatAndControls(body, parts);
            BuildEngine(body, parts);

            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                var steer = new GameObject(i == 0 ? "SteerL" : "SteerR").transform;
                steer.SetParent(root, false);
                steer.localPosition = new Vector3(side * FrontTrack, KartPhysics.FrontRadius, FrontZ);
                parts.FrontSteer[i] = steer;
                parts.FrontWheel[i] = Wheel(steer, Vector3.zero, side, KartPhysics.FrontRadius, FrontWidth, i == 0 ? "WheelFL" : "WheelFR");
                Spindle(steer, side);
                parts.RearWheel[i] = Wheel(root, new Vector3(side * RearTrack, KartPhysics.RearRadius, RearZ), side, KartPhysics.RearRadius, RearWidth, i == 0 ? "WheelRL" : "WheelRR");
            }
            return parts;
        }

        internal static GameObject Piece(Transform parent, string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            return go;
        }

        /// <summary>Tube frame: two main rails, cross members, the front axle beam and the rear axle bearings; floor tray.</summary>
        private static void BuildFrame(Transform body)
        {
            var m = new ProcMesh();
            m.At(Vector3.zero);
            const float y = 0.07f, r = 0.017f;
            for (int s = -1; s <= 1; s += 2)
            {
                m.Tube(new[]
                {
                    new Vector3(s * 0.2f, y, -0.74f), new Vector3(s * 0.27f, y, -0.6f), new Vector3(s * 0.28f, y, -0.1f),
                    new Vector3(s * 0.22f, y, 0.3f), new Vector3(s * 0.2f, y + 0.02f, 0.66f), new Vector3(s * 0.12f, y + 0.04f, 0.8f),
                }, r, 8);
                // Side nerf bar between the wheels (bumper the pods sit on).
                m.Tube(new[] { new Vector3(s * 0.27f, y, -0.3f), new Vector3(s * 0.47f, y + 0.03f, -0.28f), new Vector3(s * 0.47f, y + 0.03f, 0.28f), new Vector3(s * 0.24f, y, 0.3f) }, 0.014f, 8);
            }
            m.Tube(new[] { new Vector3(-0.27f, y, -0.6f), new Vector3(0.27f, y, -0.6f) }, r, 8);
            m.Tube(new[] { new Vector3(-0.28f, y, -0.1f), new Vector3(0.28f, y, -0.1f) }, r, 8);
            m.Tube(new[] { new Vector3(-0.42f, KartPhysics.FrontRadius - 0.02f, FrontZ), new Vector3(-0.2f, y + 0.02f, FrontZ), new Vector3(0.2f, y + 0.02f, FrontZ), new Vector3(0.42f, KartPhysics.FrontRadius - 0.02f, FrontZ) }, r, 8);
            m.Tube(new[] { new Vector3(-0.12f, y + 0.04f, 0.8f), new Vector3(0.12f, y + 0.04f, 0.8f) }, r, 8);
            // Rear axle (chrome) with two bearing housings.
            m.Tube(new[] { new Vector3(-RearTrack + 0.06f, KartPhysics.RearRadius, RearZ), new Vector3(RearTrack - 0.06f, KartPhysics.RearRadius, RearZ) }, 0.02f, 10);
            Piece(body, "Frame", m.ToMesh("KartFrame"), Chrome);

            var d = new ProcMesh();
            d.At(new Vector3(0f, y - 0.012f, 0.15f)).RoundBox(new Vector3(0.48f, 0.012f, 1.1f), 0.005f, 2);
            for (int s = -1; s <= 1; s += 2)
                d.At(new Vector3(s * 0.27f, KartPhysics.RearRadius, RearZ)).RoundBox(new Vector3(0.05f, 0.08f, 0.08f), 0.012f, 2);
            // Brake disc and caliper on the rear axle.
            d.At(new Vector3(-0.16f, KartPhysics.RearRadius, RearZ), Quaternion.Euler(0f, 0f, 90f)).Lathe(new[] { new Vector2(0.03f, -0.004f), new Vector2(0.09f, -0.004f), new Vector2(0.09f, 0.004f), new Vector2(0.03f, 0.004f) }, 20);
            d.At(new Vector3(-0.16f, KartPhysics.RearRadius + 0.07f, RearZ - 0.03f)).RoundBox(new Vector3(0.04f, 0.05f, 0.07f), 0.01f, 2);
            Piece(body, "Tray", d.ToMesh("KartTray"), Dark);
        }

        /// <summary>
        /// Wheel (27.11): a racing slick (smooth tread, rounded shoulders, a grey line on the outer sidewall), a polished
        /// silver rim (barrel with a rolled lip, dished face, six spokes, centre hub with three lug nuts, valve); its spin
        /// pivot is the returned transform.
        /// </summary>
        private static Transform Wheel(Transform parent, Vector3 at, float side, float radius, float width, string name)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = at;
            // Lathe axis (local Y) along the axle, the outer face to the outside of the kart.
            Quaternion axle = Quaternion.Euler(0f, 0f, side > 0f ? -90f : 90f);
            float w = width * 0.5f, rim = radius * 0.6f;
            var t = new ProcMesh();
            t.At(Vector3.zero, axle).Lathe(new[]
            {
                new Vector2(rim, -w + 0.008f), new Vector2(radius - 0.034f, -w), new Vector2(radius - 0.016f, -w + 0.003f),
                new Vector2(radius - 0.006f, -w + 0.011f), new Vector2(radius - 0.001f, -w + 0.024f), new Vector2(radius, -w + 0.04f),
                new Vector2(radius, w - 0.04f), new Vector2(radius - 0.001f, w - 0.024f), new Vector2(radius - 0.006f, w - 0.011f),
                new Vector2(radius - 0.016f, w - 0.003f), new Vector2(radius - 0.034f, w), new Vector2(rim, w - 0.008f),
            }, 40, 60f);
            Piece(pivot, "Tyre", t.ToMesh("KartSlick"), Tyre);
            var mark = new ProcMesh(); // sidewall line on the outer face
            mark.At(Vector3.zero, axle).Lathe(new[] { new Vector2(radius - 0.026f, w + 0.0012f), new Vector2(radius - 0.02f, w + 0.0012f) }, 40);
            Piece(pivot, "TyreLine", mark.ToMesh("KartSlickLine"), Grey);

            var r = new ProcMesh();
            float lip = rim + 0.008f;
            // open rim: barrel with both lips, six full spokes from the hub to the barrel, a dark back plate behind them
            r.At(Vector3.zero, axle).Lathe(new[]
            {
                new Vector2(rim + 0.002f, -w + 0.004f), new Vector2(lip, -w + 0.002f), new Vector2(lip, -w + 0.01f),          // inner lip
                new Vector2(rim + 0.001f, -w + 0.012f), new Vector2(rim + 0.001f, w - 0.012f),                                  // barrel
                new Vector2(lip, w - 0.008f), new Vector2(lip + 0.002f, w - 0.002f), new Vector2(lip - 0.004f, w + 0.003f),   // rolled outer lip
                new Vector2(rim - 0.008f, w - 0.001f), new Vector2(rim - 0.008f, -w + 0.012f),                                  // lip inner face, back
            }, 36, 40f);
            float sy = w - 0.016f, sLen = rim - 0.02f;
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                r.M = Matrix4x4.TRS(Vector3.zero, axle, Vector3.one) * Matrix4x4.TRS(dir * (0.024f + sLen * 0.5f) + Vector3.up * sy, Quaternion.LookRotation(dir, Vector3.up), Vector3.one);
                r.RoundBox(new Vector3(0.017f, 0.016f, sLen), 0.005f, 2);
            }
            r.At(Vector3.zero, axle).Lathe(new[] { new Vector2(0.04f, w - 0.026f), new Vector2(0.04f, w - 0.008f), new Vector2(0.03f, w - 0.002f), new Vector2(0f, w - 0.002f) }, 18, 35f); // hub
            // valve stem
            r.M = Matrix4x4.TRS(Vector3.zero, axle, Vector3.one) * Matrix4x4.TRS(new Vector3(rim - 0.012f, w - 0.006f, 0.01f), Quaternion.identity, Vector3.one);
            r.Lathe(new[] { new Vector2(0.004f, 0f), new Vector2(0.004f, 0.012f), new Vector2(0f, 0.013f) }, 6);
            var back = new ProcMesh(); // dark plate / brake drum seen between the spokes
            back.At(Vector3.zero, axle).Lathe(new[] { new Vector2(0f, -w + 0.03f), new Vector2(rim - 0.004f, -w + 0.03f) }, 24);
            back.At(Vector3.zero, axle).Lathe(new[] { new Vector2(rim - 0.004f, -w + 0.02f), new Vector2(0f, -w + 0.02f) }, 24);
            Piece(pivot, "RimBack", back.ToMesh("KartRimBack"), Dark);
            Piece(pivot, "Rim", r.ToMesh("KartRim"), RimSilver);
            var hub = new ProcMesh();
            hub.At(Vector3.zero, axle).Lathe(new[] { new Vector2(0.026f, w - 0.006f), new Vector2(0.026f, w + 0.004f), new Vector2(0.014f, w + 0.012f), new Vector2(0f, w + 0.013f) }, 12, 30f);
            for (int i = 0; i < 3; i++)
            {
                float a = i / 3f * Mathf.PI * 2f + 0.5f;
                hub.M = Matrix4x4.TRS(Vector3.zero, axle, Vector3.one) * Matrix4x4.TRS(new Vector3(Mathf.Cos(a) * 0.017f, w + 0.004f, Mathf.Sin(a) * 0.017f), Quaternion.identity, Vector3.one);
                hub.Lathe(new[] { new Vector2(0.0055f, 0f), new Vector2(0.0055f, 0.008f), new Vector2(0f, 0.009f) }, 6, 10f);
            }
            Piece(pivot, "Hub", hub.ToMesh("KartHub"), Chrome);
            return pivot;
        }

        /// <summary>Front stub axle / kingpin block on the steer pivot (turns with the wheel).</summary>
        private static void Spindle(Transform steer, float side)
        {
            var m = new ProcMesh();
            m.At(new Vector3(-side * 0.075f, 0f, 0f)).RoundBox(new Vector3(0.05f, 0.09f, 0.05f), 0.01f, 2);
            m.At(new Vector3(-side * 0.075f, 0.01f, -0.06f)).RoundBox(new Vector3(0.025f, 0.02f, 0.1f), 0.006f, 2); // steering arm
            Piece(steer, "Spindle", m.ToMesh("KartSpindle"), Dark);
        }
    }
}
