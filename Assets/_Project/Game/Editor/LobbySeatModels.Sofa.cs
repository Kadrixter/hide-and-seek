using System.Collections.Generic;
using HideAndSeek.Characters;
using UnityEngine;
using M = HideAndSeek.Game.Editor.LobbySeatMeshes;

namespace HideAndSeek.Game.Editor
{
    /// <summary>
    /// Lobby sofa (local space of the old layout: 2.4 m wide along X, 0.9 m deep, the backrest at +Z, the seats at
    /// x = -+0.55, z = -0.3 facing -Z): a low velvet lounge sofa with rolled arms, a rolled back, two puffy seat
    /// cushions with piping, two tufted back cushions leaning back, walnut legs. The seat is shallow (0.38 m from the
    /// front edge to the back cushion at hip height) and low (0.315 m): the blocky rig's thighs are short and its shins
    /// thick, so a sitter leaning on the back cushion has the knees over the front edge and the feet on the floor.
    /// </summary>
    public static partial class MultiplayerBuilder
    {
        private static void BuildSofaModel(Transform s)
        {
            var velvet = SeatMat("SofaVelvet", new Color(0.42f, 0.085f, 0.075f), VelvetTex(), 0.3f, new Vector2(4f, 2f));
            var deep = SeatMat("SofaVelvetDeep", new Color(0.3f, 0.055f, 0.05f), VelvetTex(), 0.28f, new Vector2(4f, 2f));
            var pipe = SeatMat("SofaPiping", new Color(0.2f, 0.035f, 0.03f), VelvetTex(), 0.25f, new Vector2(4f, 1f));
            var wood = SeatMat("SeatWalnut", new Color(0.32f, 0.2f, 0.12f), Tex("lobby_wood", TexKind.Wood), 0.55f, new Vector2(0.3f, 0.3f));
            if (LobbyLayout.Garage) // greasy, torn upholstery (LobbyGarageArt.cs); seed per sofa so the two differ
            {
                int seed = Mathf.Abs(Mathf.RoundToInt(s.position.x * 10f + s.position.z * 7f)) % 5 + 1;
                velvet = GrimyFabric("GarageSofa" + seed, new Color(0.44f, 0.1f, 0.08f), 7, seed, Vector2.one);
                pipe = GrimyFabric("GarageSofaPiping", new Color(0.17f, 0.035f, 0.03f), 0, 9, Vector2.one);
            }

            const float legH = SeatShape.SofaLegH, baseTop = SeatShape.SofaBaseTopY, seatTop = SeatShape.SofaSeatTopY;
            const float armX = SeatShape.SofaArmX, armTop = SeatShape.SofaArmTopY, armR = SeatShape.SofaArmRoll;
            const float backZ = SeatShape.SofaBackFrameZ, backTop = SeatShape.SofaBackTopY, backR = SeatShape.SofaBackRoll;
            const float front = -0.45f, rear = 0.45f;

            // Frame: base under the cushions, arm panels, back panel.
            var frame = new List<Mesh>
            {
                M.Moved(M.SoftBox(new Vector3(2.4f, baseTop - legH, 0.9f), 0.03f, 0.006f), new Vector3(0f, (legH + baseTop) * 0.5f, 0f), Quaternion.identity, Vector3.one),
                M.Moved(M.SoftBox(new Vector3(2.4f, backTop - backR - legH + 0.02f, rear - backZ), 0.035f, 0.006f),
                    new Vector3(0f, (legH + backTop - backR + 0.02f) * 0.5f, (backZ + rear) * 0.5f), Quaternion.identity, Vector3.one),
            };
            foreach (float side in new[] { -1f, 1f })
                frame.Add(M.Moved(M.SoftBox(new Vector3(0.17f, armTop - armR - legH + 0.03f, 0.9f), 0.03f, 0.008f),
                    new Vector3(side * armX, (legH + armTop - armR + 0.03f) * 0.5f, 0f), Quaternion.identity, Vector3.one));
            Piece(s, "Frame", GarageUv(M.Combine(frame)), velvet, "Sofa_Frame");

            // Rolls: arm tops (along Z) and the back top (along X), with scroll caps at the front.
            var rolls = new List<Mesh>();
            foreach (float side in new[] { -1f, 1f })
            {
                float x = side * (armX + 0.012f);
                rolls.Add(M.Tube(t => new Vector3(x, armTop - armR, Mathf.Lerp(front + 0.005f, rear - 0.03f, t)), _ => armR, 24, 24, false));
                rolls.Add(Disc(new Vector3(x, armTop - armR, front + 0.004f), Vector3.back, armR * 0.995f, -0.01f));
            }
            rolls.Add(M.Tube(t => new Vector3(Mathf.Lerp(-1.2f + 0.02f, 1.2f - 0.02f, t), backTop - backR, backZ + 0.07f), _ => backR, 48, 24, false));
            foreach (float side in new[] { -1f, 1f })
                rolls.Add(Disc(new Vector3(side * (1.2f - 0.02f), backTop - backR, backZ + 0.07f), Vector3.right * side, backR, -0.006f));
            Piece(s, "Rolls", GarageUv(M.Combine(rolls)), velvet, "Sofa_Rolls");

            // Seat cushions: puffy, piping round the top.
            var cushions = new List<Mesh>();
            var piping = new List<Mesh>();
            float cushionBack = SeatShape.SofaCushionRearZ, cd = cushionBack - front, ch = seatTop - 0.016f - baseTop + 0.01f;
            float cy = baseTop - 0.01f + ch * 0.5f, cz = (front + cushionBack) * 0.5f;
            foreach (float side in new[] { -1f, 1f })
            {
                float cx = side * 0.505f;
                cushions.Add(M.Moved(M.SoftBox(new Vector3(1.0f, ch, cd), 0.04f, 0.016f), new Vector3(cx, cy, cz), Quaternion.identity, Vector3.one));
                piping.Add(M.Tube(t => M.RoundRect(t, 0.5f - 0.004f, cd * 0.5f - 0.004f, 0.04f, out _) + new Vector3(cx, cy + ch * 0.5f - 0.014f, cz), _ => 0.0075f, 96, 8, true));
            }
            // Back cushions: leaning back, two rows of three tufts each.
            var tilt = Quaternion.Euler(SeatShape.SofaBackTilt, 0f, 0f);
            const float bt = SeatShape.SofaBackCushionT, bh = 0.42f;
            var buttons = new List<Mesh>();
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 c = new Vector3(side * 0.505f, SeatShape.SofaBackCushionY, SeatShape.SofaBackCushionZ);
                var tufts = new List<Vector2>();
                foreach (float bx in new[] { -0.28f, 0f, 0.28f })
                foreach (float by in new[] { -0.06f, 0.09f })
                    tufts.Add(new Vector2(bx, by));
                var mesh = M.SoftBox(new Vector3(1.0f, bh, bt), 0.05f, 0.03f, q =>
                {
                    if (q.z > 0f) return Vector3.zero; // only the front (-Z) face is tufted
                    float d = 0f;
                    foreach (var t in tufts) d += M.Dimple(new Vector2(q.x - t.x, q.y - t.y).magnitude, 0.022f, 0.04f);
                    return Vector3.forward * Mathf.Min(d, 0.025f);
                }, 20);
                cushions.Add(M.Moved(mesh, c, tilt, Vector3.one));
                foreach (var t in tufts)
                {
                    // On the tufted face: the puff at that spot minus the dimple.
                    float fx = t.x / 0.5f, fy = t.y / (bh * 0.5f);
                    float z = -bt * 0.5f - 0.03f * (1f - fx * fx) * (1f - fy * fy) + 0.022f - 0.002f;
                    buttons.Add(M.Moved(M.Ellipsoid(0.014f, 0.006f), c + tilt * new Vector3(t.x, t.y, z), tilt * Quaternion.Euler(-90f, 0f, 0f), Vector3.one));
                }
            }
            Piece(s, "Cushions", GarageUv(M.Combine(cushions)), velvet, "Sofa_Cushions");
            piping.AddRange(buttons);
            Piece(s, "Piping", GarageUv(M.Combine(piping)), pipe, "Sofa_Piping");

            // Legs: tapered walnut.
            var legs = new List<Mesh>();
            foreach (float lx in new[] { -1.1f, 1.1f })
            foreach (float lz in new[] { -0.36f, 0.36f })
                legs.Add(M.Tube(t => Vector3.Lerp(new Vector3(lx, legH + 0.01f, lz), new Vector3(lx * 1.01f, 0f, lz * 1.03f), t), t => Mathf.Lerp(0.026f, 0.017f, t), 4, 12, false));
            Piece(s, "Legs", M.Combine(legs), wood, "Sofa_Legs");

            // Colliders (the player walks round it; the seated poses use SeatShape, not these).
            BoxCol(s, new Vector3(0f, seatTop * 0.5f, 0f), new Vector3(2.4f, seatTop, 0.9f));
            BoxCol(s, new Vector3(0f, backTop * 0.5f, (backZ + rear) * 0.5f), new Vector3(2.4f, backTop, rear - backZ));
            foreach (float side in new[] { -1f, 1f })
                BoxCol(s, new Vector3(side * armX, armTop * 0.5f, 0f), new Vector3(0.2f, armTop, 0.9f));
            _ = deep;
        }

        /// <summary>Garage look: one stained texture over the whole sofa (box-projected, 1 repeat = 2.6 m); classic: the part UVs.</summary>
        private static Mesh GarageUv(Mesh m) => LobbyLayout.Garage ? GarageBoxUv(m, 2.6f) : m;

        /// <summary>Flat round cap facing <paramref name="normal"/>, slightly dished (scroll end of a roll).</summary>
        private static Mesh Disc(Vector3 c, Vector3 normal, float r, float dish)
        {
            Quaternion q = Quaternion.FromToRotation(Vector3.up, normal);
            return M.Param(24, 6, (u, v) =>
            {
                float a = u * Mathf.PI * 2f, rr = (1f - v) * r;
                return c + q * new Vector3(Mathf.Cos(a) * rr, dish * (1f - (rr / r) * (rr / r)), Mathf.Sin(a) * rr);
            });
        }
    }
}
