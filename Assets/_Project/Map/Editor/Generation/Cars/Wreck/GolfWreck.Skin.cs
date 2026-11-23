using System;
using UnityEngine;
using static HideAndSeek.Map.Editor.CarShapes;

namespace HideAndSeek.Map.Editor
{
    /// <summary>Body and greenhouse skin of the wreck, emitted side by side (the damage is not symmetric).</summary>
    internal sealed partial class GolfWreck
    {
        /// <summary>
        /// Emits the cells of one side of a loft. <paramref name="mat"/>(ring segment, station) picks the material
        /// (null = hole); <paramref name="inner"/> emits the back face, pushed <paramref name="inset"/> inwards.
        /// Normals are smoothed like <see cref="CarLoft"/> (sharp rings kept).
        /// </summary>
        private static void EmitSide(CarLoft l, MeshBuilder mb, float side, Func<int, int, Material> mat, bool inner = false, float inset = 0f)
        {
            int ns = l.Stations, nr = l.Ring;
            var fn = new Vector3[ns - 1, nr - 1];
            for (int j = 0; j < ns - 1; j++)
            for (int i = 0; i < nr - 1; i++)
            {
                Vector3 ds = l.P(j, i + 1) - l.P(j, i) + l.P(j + 1, i + 1) - l.P(j + 1, i);
                Vector3 dz = l.P(j + 1, i) - l.P(j, i) + l.P(j + 1, i + 1) - l.P(j, i + 1);
                fn[j, i] = Vector3.Cross(ds, dz);
            }
            Vector3 Corner(int fj, int fi, int vj, int vi)
            {
                Vector3 sum = Vector3.zero;
                for (int gj = vj - 1; gj <= vj; gj++)
                for (int gi = vi - 1; gi <= vi; gi++)
                {
                    if (gj < 0 || gi < 0 || gj >= ns - 1 || gi >= nr - 1) continue;
                    if (gi != fi && l.SharpRing.Contains(vi)) continue;
                    if (gj != fj && l.SharpStation.Contains(vj)) continue;
                    sum += fn[gj, gi];
                }
                if (l.S[vj][vi].x < 1e-4f) sum.x = 0f;
                if (sum.sqrMagnitude < 1e-14f) sum = fn[fj, fi];
                return sum.normalized;
            }
            Vector3 X(Vector3 v) => side > 0 ? v : new Vector3(-v.x, v.y, v.z);
            for (int j = 0; j < ns - 1; j++)
            for (int i = 0; i < nr - 1; i++)
            {
                var m = mat(i, j);
                if (m == null) continue;
                Vector3 na = Corner(j, i, j, i), nb = Corner(j, i, j, i + 1), nc = Corner(j, i, j + 1, i + 1), nd = Corner(j, i, j + 1, i);
                Vector3 a = l.P(j, i), b = l.P(j, i + 1), c = l.P(j + 1, i + 1), d = l.P(j + 1, i);
                if (inner)
                {
                    a -= na * inset; b -= nb * inset; c -= nc * inset; d -= nd * inset;
                    na = -na; nb = -nb; nc = -nc; nd = -nd;
                }
                mb.Quad(X(a), X(b), X(c), X(d), X(na), X(nb), X(nc), X(nd), m);
            }
        }

        private static Vector3 CellMid(CarLoft l, int i, int j) => (l.P(j, i) + l.P(j, i + 1) + l.P(j + 1, i) + l.P(j + 1, i + 1)) * 0.25f;

        // ------------------------------------------------------------------ body cells

        /// <summary>Front edge of the remaining skin at height y: the bumper corner below the wing is gone, the
        /// right wing is torn back in a ragged line.</summary>
        private static bool Missing(float s, Vector3 c)
        {
            if (c.z > 1.955f) return true;
            if (c.z > 1.58f && c.y < 0.5f) return true;                   // bumper wrap-round
            if (s > 0f)
            {
                float rag = 1.58f + 0.18f * Mathf.Sin(c.y * 23f) * 0.5f + 0.1f * WreckKit.Noise(new Vector3(0f, c.y * 9f, 3f));
                if (c.z > rag - 0.25f * WreckKit.Smooth(0.75f, 0.45f, c.y) && c.y < 0.72f && c.z > 1.3f) return true;
            }
            return false;
        }

        /// <summary>Cells of the driver's door (left, front): outer skin above the sill, door top and inner card.</summary>
        private bool DoorCell(int i, Vector3 c) => c.z > DB && c.z < DF && i >= BLower && i < BDeck2;

        private Material BodyCell(float s, int i, int j)
        {
            var c = CellMid(Body, i, j);
            if (Missing(s, c)) return null;
            if (i <= BWellTop) return c.z > HoodZ && i == BUnder ? null : Well;
            if (c.z > HoodZ && i >= BRound) return null;                // the bonnet is gone
            if (s < 0f && DoorCell(i, c)) return null;                  // the open door is built separately
            if (i >= BRound && TubAt(c.z) > 0.5f)
                return i == BRound ? Paint : i == BDeck1 ? Lib["Car_Interior"] : Carpet;
            if (c.z > 0.86f && c.z < HoodZ && i >= BRound) return Lib["Wreck_Plastic"];  // scuttle under the screen
            return Paint;
        }

        private void Skin()
        {
            foreach (float s in new[] { -1f, 1f })
            {
                float side = s;
                EmitSide(Body, Mb, side, (i, j) => BodyCell(side, i, j));
                // the open front shows the inside of the wings and the floor of the bay: primer back faces
                EmitSide(Body, Mb, side, (i, j) =>
                {
                    var c = CellMid(Body, i, j);
                    if (c.z < 0.86f || Missing(side, c)) return null;
                    if (i == BUnder) return c.z > HoodZ ? null : Primer;
                    return i >= BLip && i < BRound ? Primer : null;
                }, true, 0.003f);
                EmitSide(Cabin, Mb, side, (i, j) => CabinCell(side, i, j));
                EmitSide(Cabin, Mb, side, (i, j) =>
                {
                    var m = CabinCell(side, i, j);
                    return m == null || m == Glass || m == Lib["Wreck_Windscreen"] ? null : Lib["Car_Headliner"];
                }, true, 0.004f);
            }
            // engine bay floor (undertray) seen from above, a little above the skin
            Mb.Box(new Vector3(0f, 0.2f, 1.42f), new Vector3(1.2f, 0.012f, 0.95f), Primer, null, MeshBuilder.Faces.PosY);
            DoorAperture();
        }

        // ------------------------------------------------------------------ greenhouse cells

        private Material CabinCell(float s, int i, int j)
        {
            float z = Mid(Cabin, j);
            bool fdoor = z > FG0 && z < DF + 0.02f, bpil = z >= BP0 && z <= FG0, rdoor = z > RG0 && z < RG1;
            bool div = z >= DV0 && z <= RG0, quarter = z < DV0 && z > QE;
            if (s < 0f && fdoor && i <= CFrame - 1) return null;  // goes with the open door
            switch (i)
            {
                case 0: return fdoor || rdoor || div || bpil || (quarter && z > QA) ? Trim : Paint;
                case 1:
                case 2: return fdoor || rdoor || quarter ? null : bpil || div ? Trim : Paint;
                case 3: return fdoor || rdoor || bpil || div || (quarter && z > QA) ? Trim : Paint;
                case 4: return Paint;
                default:
                    if (z > 0.08f) return z > 0.88f || z < 0.12f ? Trim : Lib["Wreck_Windscreen"];
                    if (z < -1.84f) return z < -1.93f || z > -1.86f ? Trim : Glass;
                    return Paint;
            }
        }

        // ------------------------------------------------------------------ door opening (left front)

        /// <summary>Sill top, hinge (A) and lock (B) pillar faces left by the open driver's door.</summary>
        private void DoorAperture()
        {
            float x0 = -0.86f, x1 = -TubWall;
            // sill: top face and its inner wall down to the floor
            Mb.Box(new Vector3((x0 + x1) * 0.5f, SillY - 0.005f, (DB + DF) * 0.5f), new Vector3(x1 - x0, 0.01f, DF - DB), Paint, null, MeshBuilder.Faces.PosY);
            Mb.Box(new Vector3(x1 + 0.004f, (SillY + TubFloor) * 0.5f, (DB + DF) * 0.5f), new Vector3(0.008f, SillY - TubFloor, DF - DB), Carpet, null, MeshBuilder.Faces.PosX);
            // scuff plate on the sill
            Mb.Box(new Vector3((x0 + x1) * 0.5f + 0.01f, SillY + 0.002f, (DB + DF) * 0.5f), new Vector3(0.1f, 0.004f, DF - DB - 0.12f), Lib["Wreck_Alu"]);
            // B pillar between the doors (painted shut face) and the A pillar with the hinges
            Mb.Box(new Vector3(-0.78f, 0.67f, DB - 0.03f), new Vector3(0.15f, 0.64f, 0.06f), Paint, null, MeshBuilder.Faces.PosZ | MeshBuilder.Faces.NegX);
            Mb.Box(new Vector3(-0.78f, 0.62f, DF + 0.03f), new Vector3(0.15f, 0.56f, 0.06f), Paint, null, MeshBuilder.Faces.NegZ | MeshBuilder.Faces.NegX);
            foreach (float y in new[] { 0.48f, 0.82f })
                Mb.Box(new Vector3(-0.83f, y, DF - 0.01f), new Vector3(0.05f, 0.06f, 0.05f), Lib["Wreck_Primer"]);
            // door check strap and the lock striker
            Mb.Box(new Vector3(-0.82f, 0.68f, DF - 0.03f), new Vector3(0.12f, 0.02f, 0.012f), Lib["Wreck_Steel"]);
            Mb.Box(new Vector3(-0.83f, 0.8f, DB - 0.005f), new Vector3(0.025f, 0.03f, 0.012f), Lib["Car_ChromeBright"]);
        }
    }
}
