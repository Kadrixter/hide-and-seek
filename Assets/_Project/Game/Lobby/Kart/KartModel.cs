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
    /// and four wheels with treaded tyres and yellow rims. Sized for the blocky character (1.8 m): 1.7 x 1.25 m.
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
        internal static Material Yellow => Mat(ref _yellow, "KartRim", new Color(0.98f, 0.76f, 0.08f), 0.6f, 0.3f);
        internal static Material Tyre => Mat(ref _tyre, "KartTyre", new Color(0.045f, 0.045f, 0.05f), 0.12f);
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

        /// <summary>Wheel (tyre with tread blocks, rim, hub nut); its spin pivot is the returned transform.</summary>
        private static Transform Wheel(Transform parent, Vector3 at, float side, float radius, float width, string name)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = at;
            // Lathe axis (local Y) along the axle, the outer face to the outside of the kart.
            Quaternion axle = Quaternion.Euler(0f, 0f, side > 0f ? -90f : 90f);
            float w = width * 0.5f, rim = radius * 0.62f;
            var t = new ProcMesh();
            t.At(Vector3.zero, axle).Lathe(new[]
            {
                new Vector2(rim, -w + 0.01f), new Vector2(radius - 0.016f, -w), new Vector2(radius - 0.004f, -w + 0.01f),
                new Vector2(radius, -w + 0.022f), new Vector2(radius, w - 0.022f), new Vector2(radius - 0.004f, w - 0.01f),
                new Vector2(radius - 0.016f, w), new Vector2(rim, w - 0.01f),
            }, 28, 50f);
            // Blocky tread: two staggered rows of lugs.
            for (int i = 0; i < 18; i++)
            for (int row = 0; row < 2; row++)
            {
                float a = (i + row * 0.5f) / 18f * Mathf.PI * 2f;
                var p = new Vector3(Mathf.Cos(a) * (radius + 0.004f), (row == 0 ? -1f : 1f) * w * 0.45f, Mathf.Sin(a) * (radius + 0.004f));
                t.M = Matrix4x4.TRS(Vector3.zero, axle, Vector3.one) * Matrix4x4.TRS(p, Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f), Vector3.one);
                t.Box(Vector3.zero, new Vector3(0.012f, w * 0.8f, 0.022f));
            }
            Piece(pivot, "Tyre", t.ToMesh("KartTyre"), Tyre);

            var r = new ProcMesh();
            r.At(Vector3.zero, axle).Lathe(new[]
            {
                new Vector2(rim + 0.004f, -w + 0.008f), new Vector2(rim + 0.004f, w - 0.012f), new Vector2(rim - 0.012f, w - 0.006f),
                new Vector2(0.035f, w - 0.02f), new Vector2(0.028f, w - 0.006f), new Vector2(0f, w - 0.006f),
            }, 20, 35f);
            r.At(Vector3.zero, axle).Lathe(new[] { new Vector2(0f, -w + 0.014f), new Vector2(rim + 0.004f, -w + 0.014f) }, 20); // inner face (closes the wheel seen from the kart)
            for (int i = 0; i < 5; i++)
            {
                float a = i / 5f * Mathf.PI * 2f;
                r.M = Matrix4x4.TRS(Vector3.zero, axle, Vector3.one) * Matrix4x4.TRS(new Vector3(Mathf.Cos(a) * 0.05f, w - 0.018f, Mathf.Sin(a) * 0.05f), Quaternion.identity, Vector3.one);
                r.Box(Vector3.zero, new Vector3(0.022f, 0.02f, 0.05f)); // spokes / lug bosses
            }
            Piece(pivot, "Rim", r.ToMesh("KartRim"), Yellow);
            var hub = new ProcMesh();
            hub.At(Vector3.zero, axle).Lathe(new[] { new Vector2(0.024f, w - 0.008f), new Vector2(0.024f, w + 0.006f), new Vector2(0.012f, w + 0.014f), new Vector2(0f, w + 0.014f) }, 6, 20f);
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
