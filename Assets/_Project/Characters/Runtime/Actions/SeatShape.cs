using UnityEngine;

namespace HideAndSeek.Characters
{
    public enum SeatKind : byte { Generic, Sofa, Pouf }

    /// <summary>A solid piece of furniture in seat space: box (optionally puffed like a cushion) or upright cylinder (optionally domed).</summary>
    public struct SeatSolid
    {
        public Vector3 Center;
        public Quaternion Rot;
        /// <summary>Box: half extents. Cylinder (local Y axis): x = radius, y = half height.</summary>
        public Vector3 Half;
        public bool Cylinder;
        /// <summary>Box: every face bulges out by this much at its centre. Cylinder: the top rises by this much at the axis.</summary>
        public float Puff;

        public static SeatSolid Box(Vector3 c, Vector3 size, Quaternion rot, float puff = 0f) =>
            new SeatSolid { Center = c, Half = size * 0.5f, Rot = rot, Puff = puff };

        public static SeatSolid Cyl(Vector3 c, float radius, float height, Quaternion rot, float dome = 0f) =>
            new SeatSolid { Center = c, Half = new Vector3(radius, height * 0.5f, radius), Rot = rot, Cylinder = true, Puff = dome };

        /// <summary>Upright cylinder (tested point by point); everything else is tested as an oriented box.</summary>
        public bool Upright => Cylinder && Vector3.Dot(Rot * Vector3.up, Vector3.up) > 0.99f;

        /// <summary>Bounding oriented box (a box's puff added on every side, a cylinder's square hull).</summary>
        public PartBox AsBox(float margin) => new PartBox
        {
            Center = Center,
            Rot = Rot,
            Half = (Cylinder ? new Vector3(Half.x, Half.y + Puff * 0.5f, Half.z) : Half + Vector3.one * Puff) + Vector3.one * margin
        };

        /// <summary>True when <paramref name="p"/> is inside the solid grown by <paramref name="margin"/>.</summary>
        public bool Contains(Vector3 p, float margin)
        {
            Vector3 l = Quaternion.Inverse(Rot) * (p - Center);
            if (Cylinder)
            {
                float r = new Vector2(l.x, l.z).magnitude;
                if (r > Half.x + margin) return false;
                float f = Mathf.Clamp01(r / Half.x);
                float top = Half.y + Puff * Mathf.Cos(f * Mathf.PI * 0.5f) * (1f - 0.25f * f * f);
                return l.y <= top + margin && l.y >= -Half.y - margin;
            }
            float fx = Mathf.Clamp(l.x / Half.x, -1f, 1f), fy = Mathf.Clamp(l.y / Half.y, -1f, 1f), fz = Mathf.Clamp(l.z / Half.z, -1f, 1f);
            float bx = Puff * (1f - fy * fy) * (1f - fz * fz);
            float by = Puff * (1f - fx * fx) * (1f - fz * fz);
            float bz = Puff * (1f - fx * fx) * (1f - fy * fy);
            return Mathf.Abs(l.x) <= Half.x + bx + margin && Mathf.Abs(l.y) <= Half.y + by + margin && Mathf.Abs(l.z) <= Half.z + bz + margin;
        }
    }

    /// <summary>
    /// What a seat is made of, in seat space: origin on the floor at the seat point (in front of the seat),
    /// +Z = the direction the sitter faces, +X = the sitter's right. The solids follow the lobby builder
    /// (MultiplayerBuilder.Sofa / Pouf + LobbyModels / LobbyDetails) and are slightly conservative (rounded edges
    /// count as square), so a pose that clears them clears the real meshes too; the editor seat check verifies
    /// that against the real scene.
    /// </summary>
    public sealed class SeatShape
    {
        public SeatKind Kind;
        /// <summary>Sofa: +1 = the armrest is on the sitter's right, -1 = on the left. 0 = none.</summary>
        public int ArmSide;
        public SeatSolid[] Solids;
        /// <summary>Front edge of the seat top (z), nominal top height and the backrest front (NaN = no backrest).</summary>
        public float EdgeZ, TopY, BackZ = float.NaN;
        /// <summary>Back end of the sitting area (hands can rest up to here).</summary>
        public float RearZ;
        /// <summary>Armrest: inner face (|x|) and top height (0 = none).</summary>
        public float ArmInnerX, ArmTopY;
        /// <summary>Stable id for pose caching.</summary>
        public int Id;
        /// <summary>
        /// How far a solved pose is lowered into the solids at the end (they are conservative: rounded cushions count as
        /// square, so a body resting on them would hover over the real cushion). Measured by the editor seat check:
        /// the body then touches the real furniture, sinking at most ~1.5 cm into the soft cushion.
        /// </summary>
        public float Sink;
        /// <summary>Sofa: inner face (|x|, on the side away from <see cref="ArmSide"/>) of the far armrest, 0 = none.</summary>
        public float OtherArmInnerX;
        /// <summary>Backrest top (0 = none).</summary>
        public float BackTopY;
        /// <summary>This seat is the mirror image (x -> -x) of that one: its poses are mirrored from it.</summary>
        public SeatShape MirrorSource;
        /// <summary>
        /// Sofa: the same seat without the block that stands for the neighbouring seat's sitter, so a pose may take the
        /// whole sofa (lying along the cushions, see <see cref="SeatPoses.SpansNeighbour"/>). Null = no such room.
        /// </summary>
        public SeatShape Wide;

        /// <summary>Office chair: pose 0 is solved by SeatPoses.Office (back against the backrest, feet on the floor).</summary>
        public bool Office;

        public bool HasBack => !float.IsNaN(BackZ);

        /// <summary>True when <paramref name="p"/> is inside a solid or below the floor (both grown by <paramref name="margin"/>).</summary>
        public bool Contains(Vector3 p, float margin)
        {
            if (p.y < margin) return true; // the floor
            var s = Solids;
            for (int i = 0; i < s.Length; i++)
                if (s[i].Contains(p, margin)) return true;
            return false;
        }

        /// <summary>Height of the highest solid top under (x, z) (probe from above, 2 mm steps), 0 = floor.</summary>
        public float TopAt(float x, float z)
        {
            var s = Solids;
            for (float y = 1.6f; y > 0f; y -= 0.002f)
            {
                var p = new Vector3(x, y, z);
                for (int i = 0; i < s.Length; i++)
                    if (s[i].Contains(p, 0f)) return y;
            }
            return 0f;
        }

        // ------------------------------------------------------------------ presets

        static SeatShape _pouf, _generic;
        static readonly SeatShape[] Sofas = new SeatShape[2];

        /// <summary>Lobby sofa seat. <paramref name="seatX"/> = the seat point's x in sofa space (-0.55 or 0.55).</summary>
        public static SeatShape Sofa(float seatX)
        {
            int idx = seatX < 0f ? 0 : 1;
            if (Sofas[idx] != null) return Sofas[idx];
            float sx = idx == 0 ? -0.55f : 0.55f;
            // Sofa space -> seat space: the seat point is at (sx, 0, -0.3) turned 180 degrees.
            Quaternion flip = Quaternion.Euler(0f, 180f, 0f);
            Vector3 P(float x, float y, float z) => new Vector3(-(x - sx), y, -(z + 0.3f));
            SeatSolid B(float x, float y, float z, float w, float h, float d, Vector3 euler = default, float puff = 0f) =>
                SeatSolid.Box(P(x, y, z), new Vector3(w, h, d), flip * Quaternion.Euler(euler), puff);
            SeatSolid C(float x, float y, float z, float dia, float len, Vector3 euler) =>
                SeatSolid.Cyl(P(x, y, z), dia * 0.5f, len, flip * Quaternion.Euler(euler));
            var s = new SeatShape
            {
                Kind = SeatKind.Sofa,
                Id = 10 + idx,
                ArmSide = idx == 0 ? 1 : -1,
                Solids = new[]
                {
                    B(0f, 0.25f, 0f, 2.4f, 0.4f, 0.9f),                  // seat block
                    B(0f, 0.65f, 0.38f, 2.4f, 0.8f, 0.2f),               // backrest
                    B(-1.15f, 0.5f, 0f, 0.2f, 0.6f, 0.9f),               // arms
                    B(1.15f, 0.5f, 0f, 0.2f, 0.6f, 0.9f),
                    C(-1.15f, 0.8f, 0f, 0.22f, 0.9f, new Vector3(90f, 0f, 0f)),   // arm rolls
                    C(1.15f, 0.8f, 0f, 0.22f, 0.9f, new Vector3(90f, 0f, 0f)),
                    C(0f, 1.05f, 0.38f, 0.22f, 2.4f, new Vector3(0f, 0f, 90f)),   // backrest roll
                    B(-0.53f, 0.48f, -0.04f, 1.02f, 0.1f, 0.78f, default, 0.025f), // seat cushions
                    B(0.53f, 0.48f, -0.04f, 1.02f, 0.1f, 0.78f, default, 0.025f),
                    B(-0.82f, 0.72f, 0.2f, 0.42f, 0.4f, 0.13f, new Vector3(-18f, 12f, 8f), 0.05f),   // throw pillows
                    B(0.82f, 0.72f, 0.2f, 0.42f, 0.4f, 0.13f, new Vector3(-18f, -12f, -8f), 0.05f),
                    B(-0.53f, 0.53f, -0.43f, 1.0f, 0.018f, 0.018f),     // piping on the cushion fronts
                    B(0.53f, 0.53f, -0.43f, 1.0f, 0.018f, 0.018f),
                    // Not furniture: the half of the sofa that belongs to the other seat (its sitter's body).
                    SeatSolid.Box(new Vector3((idx == 0 ? -1f : 1f) * (0.55f + 0.3f), 1f, 0f), new Vector3(0.6f, 2f, 3f), Quaternion.identity),
                },
                EdgeZ = 0.155f,
                TopY = 0.555f,
                BackZ = -0.58f,
                RearZ = -0.56f,
                ArmInnerX = 0.5f,
                ArmTopY = 0.91f,
                BackTopY = 1.16f,
                Sink = 0.024f,
            };
            // Lying along the cushions: the whole sofa, the neighbouring seat must be free (checked by the game).
            s.Wide = new SeatShape
            {
                Kind = s.Kind, Id = s.Id, ArmSide = s.ArmSide, EdgeZ = s.EdgeZ, TopY = s.TopY, BackZ = s.BackZ, RearZ = s.RearZ,
                ArmInnerX = s.ArmInnerX, ArmTopY = s.ArmTopY, BackTopY = s.BackTopY, Sink = s.Sink,
                Solids = WithoutLast(s.Solids), // everything but the neighbour block
                OtherArmInnerX = 2.1f - s.ArmInnerX,
            };
            s.OtherArmInnerX = s.Wide.OtherArmInnerX;
            Sofas[idx] = s;
            // The right-hand seat (armrest on the left) mirrors the left-hand one.
            if (idx == 1) s.MirrorSource = Sofa(-0.55f);
            return s;
        }

        static SeatSolid[] WithoutLast(SeatSolid[] a)
        {
            var r = new SeatSolid[a.Length - 1];
            System.Array.Copy(a, r, r.Length);
            return r;
        }

        /// <summary>Lobby pouf (seat point 0.14 m in front of its centre).</summary>
        public static SeatShape Pouf()
        {
            if (_pouf != null) return _pouf;
            Vector3 c = new Vector3(0f, 0f, -0.14f);
            return _pouf = new SeatShape
            {
                Kind = SeatKind.Pouf,
                Id = 20,
                Solids = new[]
                {
                    // Body with the piping ring round its top edge (0.3175 m, up to 0.427 m) as one cylinder.
                    SeatSolid.Cyl(c + new Vector3(0f, 0.2135f, 0f), 0.3175f, 0.427f, Quaternion.identity),
                    SeatSolid.Cyl(c + new Vector3(0f, 0.42f, 0f), 0.28f, 0f, Quaternion.identity, 0.035f),     // puffy top
                    SeatSolid.Cyl(c + new Vector3(0f, 0.448f, 0f), 0.03f, 0.016f, Quaternion.identity),        // tufting button
                },
                EdgeZ = c.z + 0.3175f,
                TopY = 0.456f,
                RearZ = c.z - 0.24f,
                Sink = 0.008f,
            };
        }

        /// <summary>Unknown seat: a plain 0.45 m block behind the seat point.</summary>
        public static SeatShape Generic()
        {
            if (_generic != null) return _generic;
            return _generic = new SeatShape
            {
                Kind = SeatKind.Generic,
                Id = 1,
                Solids = new[] { SeatSolid.Box(new Vector3(0f, 0.225f, -0.3f), new Vector3(0.6f, 0.45f, 0.5f), Quaternion.identity) },
                EdgeZ = -0.05f,
                TopY = 0.45f,
                RearZ = -0.5f,
            };
        }
    }
}
