using System.Collections.Generic;
using HideAndSeek.Characters;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Swivel office chairs in front of the monitors (two per room): five-star base on twin castors, gas lift,
    /// padded seat, mesh backrest in a moulded frame, lumbar pad, T armrests. The upper part (seat, back, arms) turns
    /// round the gas lift; the seat anchor (<see cref="Chair.Anchor"/>, in front of the seat, facing away from it) is
    /// what a seated player's pose hangs on (see SecurityRooms.ChairSeat.cs). <see cref="ChairShape"/> describes the
    /// seat to the seated-pose solver.
    /// </summary>
    public partial class SecurityRooms
    {
        public const int ChairsPerRoom = 2;
        /// <summary>The gas lift is let down this much: the blocky legs are short, at this height the feet reach the floor.</summary>
        public const float SeatDrop = 0.21f;
        public const int ChairCount = Levels * ChairsPerRoom;

        public sealed class Chair
        {
            public int Index, Level;
            public Transform Root, Upper, Anchor;
            public float Yaw, ShownYaw, Dip, DipVel;
            public Vector3 SeatCenter => Upper.position + Vector3.up * 0.5f;   // Upper sits SeatDrop below the root
        }

        private readonly List<Chair> _chairs = new();
        public IReadOnlyList<Chair> Chairs => _chairs;

        private static SeatShape _chairShape;

        /// <summary>The chair in seat space (origin on the floor 0.24 m in front of the seat centre, +Z = where the sitter faces).</summary>
        public static SeatShape ChairShape => _chairShape ??= new SeatShape
        {
            Kind = SeatKind.Generic,
            Office = true,
            Id = 42,
            Solids = new[]
            {
                SeatSolid.Box(new Vector3(0f, 0.47f - SeatDrop, -0.34f), new Vector3(0.52f, 0.095f, 0.32f), Quaternion.identity, 0.01f),
                SeatSolid.Box(new Vector3(0f, 0.88f - SeatDrop, -0.53f), new Vector3(0.46f, 0.56f, 0.05f), Quaternion.Euler(-10f, 0f, 0f)),
                SeatSolid.Box(new Vector3(-0.31f, 0.665f - SeatDrop, -0.24f), new Vector3(0.075f, 0.035f, 0.25f), Quaternion.identity),
                SeatSolid.Box(new Vector3(0.31f, 0.665f - SeatDrop, -0.24f), new Vector3(0.075f, 0.035f, 0.25f), Quaternion.identity),
                SeatSolid.Box(new Vector3(0f, 0.2f - SeatDrop * 0.5f, -0.24f), new Vector3(0.08f, 0.24f, 0.08f), Quaternion.identity),
            },
            EdgeZ = -0.18f,
            TopY = 0.5175f - SeatDrop,
            BackZ = -0.47f,
            RearZ = -0.46f,
            ArmInnerX = 0.2725f,
            ArmTopY = 0.6825f - SeatDrop,
            BackTopY = 1.16f - SeatDrop,
            Sink = 0.022f,   // the cushion gives under the hips
        };

        private void BuildSwivelChair(Transform room, int lv, int idx, Vector3 at, float yaw)
        {
            if (GetComponent<SecurityChairDriver>() == null) gameObject.AddComponent<SecurityChairDriver>();
            var black = new Color(0.05f, 0.05f, 0.055f);
            var plastic = new Color(0.09f, 0.09f, 0.1f);
            var chrome = new Color(0.62f, 0.63f, 0.66f);
            var fabric = new Color(0.13f, 0.15f, 0.19f);
            var meshCol = new Color(0.1f, 0.11f, 0.13f);

            var root = new GameObject($"SwivelChair{idx}").transform;
            root.SetParent(room, false);
            root.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));

            // ---- base (does not turn)
            var hub = CachedMesh("ChairHub", m => m.Lathe(new[] { new Vector2(0f, 0.07f), new Vector2(0.055f, 0.07f), new Vector2(0.062f, 0.095f), new Vector2(0.046f, 0.13f), new Vector2(0f, 0.135f) }, 20));
            MeshObj(root, "Hub", hub, Vector3.zero, Quaternion.identity, black, Surf.Shiny(0.45f), true);
            var leg = CachedMesh("ChairLeg", m => m.At(new Vector3(0f, 0.088f, 0.17f), Quaternion.Euler(4f, 0f, 0f)).RoundBox(new Vector3(0.05f, 0.032f, 0.28f), 0.012f, 4));
            var fork = CachedMesh("ChairFork", m => m.RoundBox(new Vector3(0.034f, 0.035f, 0.046f), 0.01f, 3));
            var wheel = CachedMesh("ChairWheel", m => m.At(Vector3.zero, Quaternion.Euler(0f, 0f, 90f)).Torus(0.017f, 0.012f, 16, 8)
                .At(Vector3.zero, Quaternion.Euler(0f, 0f, 90f)).Lathe(new[] { new Vector2(0f, -0.004f), new Vector2(0.02f, -0.004f), new Vector2(0.02f, 0.004f), new Vector2(0f, 0.004f) }, 12));
            for (int i = 0; i < 5; i++)
            {
                var q = Quaternion.Euler(0f, i * 72f + 36f, 0f);
                MeshObj(root, "Leg", leg, Vector3.zero, q, black, Surf.Shiny(0.5f), true);
                Vector3 tip = q * new Vector3(0f, 0f, 0.305f);
                MeshObj(root, "Fork", fork, tip + Vector3.up * 0.06f, q, plastic, Surf.Shiny(0.4f), true, false);
                // Castors trail behind the stem, turned a little differently each (they swivel freely).
                var cq = q * Quaternion.Euler(0f, (i * 53f) % 70f - 35f, 0f);
                Vector3 axle = tip + cq * new Vector3(0f, 0.03f, -0.016f);
                foreach (float s in new[] { -0.012f, 0.012f })
                    MeshObj(root, "Castor", wheel, axle + cq * new Vector3(s, 0f, 0f), cq, black, Surf.Shiny(0.55f), true, false);
            }
            var sleeve = CachedMesh("ChairSleeveLow2", m => m.Lathe(new[] { new Vector2(0.034f, 0.13f), new Vector2(0.034f, 0.15f), new Vector2(0.028f, 0.16f), new Vector2(0.02f, 0.16f) }, 16));
            MeshObj(root, "GasSleeve", sleeve, Vector3.zero, Quaternion.identity, black, Surf.Shiny(0.5f), true);

            // ---- upper part (turns, dips when somebody sits down)
            var upper = new GameObject("Upper").transform;
            upper.SetParent(root, false);
            upper.localPosition = new Vector3(0f, -SeatDrop, 0f);
            var piston = CachedMesh("ChairPiston", m => m.Lathe(new[] { new Vector2(0.019f, 0.26f), new Vector2(0.019f, 0.38f), new Vector2(0f, 0.385f) }, 14));
            MeshObj(upper, "Piston", piston, Vector3.zero, Quaternion.identity, chrome, Surf.Shiny(0.85f, 1f), true);
            MeshObj(upper, "Mechanism", CachedMesh("ChairMech", m => m.RoundBox(new Vector3(0.22f, 0.045f, 0.25f), 0.012f, 3)), new Vector3(0f, 0.395f, 0f), Quaternion.identity, plastic, Surf.Shiny(0.35f, 0.3f), true);
            MeshObj(upper, "Lever", CachedMesh("ChairLever", m => m.Tube(new[] { new Vector3(0.1f, 0.39f, 0.05f), new Vector3(0.2f, 0.385f, 0.07f), new Vector3(0.235f, 0.38f, 0.12f) }, 0.006f, 6)
                .At(new Vector3(0.235f, 0.38f, 0.12f)).RoundBox(new Vector3(0.022f, 0.022f, 0.04f), 0.008f, 2)), Vector3.zero, Quaternion.identity, black, Surf.Shiny(0.4f), true, false);
            MeshObj(upper, "SeatShell", CachedMesh("ChairShell2", m => m.RoundBox(new Vector3(0.53f, 0.035f, 0.33f), 0.015f, 4)), new Vector3(0f, 0.432f, -0.1f), Quaternion.identity, plastic, Surf.Shiny(0.3f), true);
            MeshObj(upper, "Cushion", CachedMesh("ChairCushion2", m => m.RoundBox(new Vector3(0.52f, 0.075f, 0.32f), 0.034f, 6)), new Vector3(0f, 0.48f, -0.1f), Quaternion.identity, fabric, Surf.T(SecurityTextures.Kind.Fabric, 0.22f, 0.08f), true);

            // Spine from under the seat up behind the backrest.
            MeshObj(upper, "Spine", CachedMesh("ChairSpine", m => m.Tube(new[] { new Vector3(0f, 0.405f, -0.08f), new Vector3(0f, 0.405f, -0.24f), new Vector3(0f, 0.47f, -0.3f), new Vector3(0f, 0.6f, -0.33f), new Vector3(0f, 0.66f, -0.33f) }, 0.022f, 10)),
                Vector3.zero, Quaternion.identity, plastic, Surf.Shiny(0.4f), true);

            // Backrest: moulded frame loop with the mesh stretched inside, lumbar pad, tilted back 10 degrees.
            var backRot = Quaternion.Euler(-10f, 0f, 0f);
            Vector3 backC = new Vector3(0f, 0.88f, -0.29f);
            var frame = CachedMesh("ChairBackFrame", m => m.Tube(RoundedRect(0.46f, 0.56f, 0.09f), 0.017f, 8, false));
            MeshObj(upper, "BackFrame", frame, backC, backRot, plastic, Surf.Shiny(0.35f), true);
            MeshObj(upper, "BackMesh", CachedMesh("ChairBackMesh", m => m.RoundBox(new Vector3(0.44f, 0.54f, 0.008f), 0.003f, 2)), backC, backRot, meshCol, Surf.T(SecurityTextures.Kind.Fabric, 0.06f, 0.12f), true);
            MeshObj(upper, "Lumbar", CachedMesh("ChairLumbar", m => m.RoundBox(new Vector3(0.36f, 0.075f, 0.03f), 0.014f, 3)), backC + backRot * new Vector3(0f, -0.15f, 0.018f), backRot, plastic, Surf.Shiny(0.3f), true);

            // Armrests: curved posts from the seat sides, soft pads.
            foreach (float s in new[] { -1f, 1f })
            {
                MeshObj(upper, "ArmPost", CachedMesh("ChairArmPostW2" + s, m => m.Tube(new[] { new Vector3(0.22f * s, 0.415f, -0.04f), new Vector3(0.28f * s, 0.43f, -0.04f), new Vector3(0.31f * s, 0.5f, -0.035f), new Vector3(0.31f * s, 0.65f, -0.03f) }, 0.016f, 8)),
                    Vector3.zero, Quaternion.identity, black, Surf.Shiny(0.4f), true);
                MeshObj(upper, "ArmPad", CachedMesh("ChairArmPad", m => m.RoundBox(new Vector3(0.075f, 0.03f, 0.25f), 0.013f, 4)), new Vector3(0.31f * s, 0.665f, 0f), Quaternion.identity, black, Surf.Shiny(0.2f), true);
            }

            var anchor = new GameObject("SeatAnchor").transform;
            anchor.SetParent(upper, false);
            anchor.localPosition = new Vector3(0f, SeatDrop, 0.24f);   // on the floor again (Upper is let down)

            var col = root.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.5f, -0.02f);
            col.size = new Vector3(0.6f, 1.0f, 0.62f);

            _chairs.Add(new Chair { Index = _chairs.Count, Level = lv, Root = root, Upper = upper, Anchor = anchor, Yaw = yaw, ShownYaw = yaw });
        }

        /// <summary>Closed loop of points round a rectangle with rounded corners (XY plane).</summary>
        private static List<Vector3> RoundedRect(float w, float h, float r)
        {
            var pts = new List<Vector3>();
            float hx = w * 0.5f - r, hy = h * 0.5f - r;
            Vector2[] c = { new(hx, hy), new(-hx, hy), new(-hx, -hy), new(hx, -hy) };
            for (int k = 0; k < 4; k++)
                for (int i = 0; i <= 4; i++)
                {
                    float a = (k * 90f + i * 22.5f) * Mathf.Deg2Rad;
                    pts.Add(new Vector3(c[k].x + Mathf.Cos(a) * r, c[k].y + Mathf.Sin(a) * r, 0f));
                }
            pts.Add(pts[0]);
            pts.Add(pts[1]);
            return pts;
        }
    }
}
