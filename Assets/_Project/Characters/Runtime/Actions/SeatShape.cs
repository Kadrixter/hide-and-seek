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

        // ------------------------------------------------------------------ lobby furniture sizes
        // The lobby models (Game/Editor/LobbySeatModels) are built from the same numbers the poses are fitted to.

        /// <summary>Pouf: height of the top at its centre (low enough for the rig's short legs to reach the floor).</summary>
        public const float PoufTopY = 0.27f;
        /// <summary>Pouf: the seat point lies this far in front of the pouf's centre.</summary>
        public const float PoufSeatZ = 0.12f;

        // Sofa (sofa space: X along the sofa, the backrest at +Z, the floor at 0; seats at x -+0.55, z -0.3).
        public const float SofaLegH = 0.07f, SofaBaseTopY = 0.18f, SofaSeatTopY = 0.27f, SofaCushionRearZ = 0.17f;
        public const float SofaArmX = 1.11f, SofaArmTopY = 0.54f, SofaArmRoll = 0.09f;
        public const float SofaBackFrameZ = 0.18f, SofaBackTopY = 0.76f, SofaBackRoll = 0.075f;
        public const float SofaBackTilt = 10f, SofaBackCushionT = 0.24f, SofaBackCushionY = 0.483f, SofaBackCushionZ = 0.088f;

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
                    B(0f, (SofaLegH + SofaBaseTopY) * 0.5f, 0f, 2.4f, SofaBaseTopY - SofaLegH, 0.9f),                       // base
                    B(-0.505f, SofaBaseTopY + 0.032f, (SofaCushionRearZ - 0.45f) * 0.5f, 1.0f, 0.084f, SofaCushionRearZ + 0.45f, default, 0.016f), // seat cushions
                    B(0.505f, SofaBaseTopY + 0.032f, (SofaCushionRearZ - 0.45f) * 0.5f, 1.0f, 0.084f, SofaCushionRearZ + 0.45f, default, 0.016f),
                    B(0f, (SofaLegH + SofaBackTopY - SofaBackRoll) * 0.5f + 0.01f, (SofaBackFrameZ + 0.45f) * 0.5f, 2.4f, SofaBackTopY - SofaBackRoll - SofaLegH + 0.02f, 0.45f - SofaBackFrameZ), // back frame
                    C(0f, SofaBackTopY - SofaBackRoll, SofaBackFrameZ + 0.07f, 2f * SofaBackRoll, 2.4f, new Vector3(0f, 0f, 90f)),      // back roll
                    B(-SofaArmX, (SofaLegH + SofaArmTopY - SofaArmRoll) * 0.5f + 0.015f, 0f, 0.17f, SofaArmTopY - SofaArmRoll - SofaLegH + 0.03f, 0.9f), // arms
                    B(SofaArmX, (SofaLegH + SofaArmTopY - SofaArmRoll) * 0.5f + 0.015f, 0f, 0.17f, SofaArmTopY - SofaArmRoll - SofaLegH + 0.03f, 0.9f),
                    C(-SofaArmX - 0.012f, SofaArmTopY - SofaArmRoll, 0f, 2f * SofaArmRoll, 0.9f, new Vector3(90f, 0f, 0f)),         // arm rolls
                    C(SofaArmX + 0.012f, SofaArmTopY - SofaArmRoll, 0f, 2f * SofaArmRoll, 0.9f, new Vector3(90f, 0f, 0f)),
                    B(-0.505f, SofaBackCushionY, SofaBackCushionZ, 1.0f, 0.42f, SofaBackCushionT, new Vector3(SofaBackTilt, 0f, 0f), 0.03f), // back cushions
                    B(0.505f, SofaBackCushionY, SofaBackCushionZ, 1.0f, 0.42f, SofaBackCushionT, new Vector3(SofaBackTilt, 0f, 0f), 0.03f),
                    // Not furniture: the half of the sofa that belongs to the other seat (its sitter's body).
                    SeatSolid.Box(new Vector3((idx == 0 ? -1f : 1f) * (0.55f + 0.3f), 1f, 0f), new Vector3(0.6f, 2f, 3f), Quaternion.identity),
                },
                EdgeZ = 0.15f,
                TopY = SofaSeatTopY,
                BackZ = -0.22f,
                RearZ = -0.2f,
                ArmInnerX = SofaArmX - 0.085f - 0.55f,
                ArmTopY = SofaArmTopY,
                BackTopY = SofaBackTopY,
                Sink = 0.025f,
            };
            // Lying along the cushions: the whole sofa, the neighbouring seat must be free (checked by the game).
            s.Wide = new SeatShape
            {
                Kind = s.Kind, Id = s.Id, ArmSide = s.ArmSide, EdgeZ = s.EdgeZ, TopY = s.TopY, BackZ = s.BackZ, RearZ = s.RearZ,
                ArmInnerX = s.ArmInnerX, ArmTopY = s.ArmTopY, BackTopY = s.BackTopY, Sink = s.Sink,
                Solids = WithoutLast(s.Solids), // everything but the neighbour block
                OtherArmInnerX = 2f * (SofaArmX - 0.085f) - s.ArmInnerX,
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

        /// <summary>Pouf model used in the lobby: 0 tufted chesterfield, 1 bean bag, 2 channel-tufted drum on legs, 3 wicker cube with a cushion.</summary>
        public const int PoufStyle = 0;

        /// <summary>Lobby pouf (seat point <see cref="PoufSeatZ"/> in front of its centre).</summary>
        public static SeatShape Pouf()
        {
            if (_pouf != null) return _pouf;
            Vector3 c = new Vector3(0f, 0f, -PoufSeatZ);
            SeatSolid[] solids;
            float radius;
            switch (PoufStyle)
            {
                case 1: // bean bag: wide, the back rim higher
                    radius = 0.39f;
                    solids = new[]
                    {
                        SeatSolid.Cyl(c + new Vector3(0f, (PoufTopY - 0.02f) * 0.5f, 0f), radius, PoufTopY - 0.02f, Quaternion.identity),
                        SeatSolid.Cyl(c + new Vector3(0f, PoufTopY - 0.02f, 0f), 0.3f, 0f, Quaternion.identity, 0.02f),
                        SeatSolid.Box(c + new Vector3(0f, PoufTopY + 0.05f, -0.25f), new Vector3(0.4f, 0.1f, 0.12f), Quaternion.identity, 0.03f),
                    };
                    break;
                case 3: // wicker cube with a cushion
                    radius = 0.265f;
                    solids = new[]
                    {
                        SeatSolid.Box(c + new Vector3(0f, (PoufTopY - 0.012f) * 0.5f, 0f), new Vector3(0.53f, PoufTopY - 0.012f, 0.53f), Quaternion.identity, 0.006f),
                    };
                    break;
                default: // round: tufted / drum, the top domed a little
                    radius = PoufStyle == 2 ? 0.3f : 0.318f;
                    solids = new[]
                    {
                        SeatSolid.Cyl(c + new Vector3(0f, (PoufTopY - 0.025f) * 0.5f, 0f), radius, PoufTopY - 0.025f, Quaternion.identity),
                        SeatSolid.Cyl(c + new Vector3(0f, PoufTopY - 0.025f, 0f), radius - 0.03f, 0f, Quaternion.identity, 0.025f),
                    };
                    break;
            }
            return _pouf = new SeatShape
            {
                Kind = SeatKind.Pouf,
                Id = 20,
                Solids = solids,
                EdgeZ = c.z + radius,
                TopY = PoufTopY,
                RearZ = c.z - radius + 0.06f,
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
