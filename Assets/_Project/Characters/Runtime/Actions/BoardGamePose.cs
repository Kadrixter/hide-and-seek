using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Seated board-game player (chess / tic-tac-toe tables in the lobby): sitting on a stool with the feet on its
    /// ring, forearms resting on the table edge, leaning in. "Thinking" loop: the left fist under the chin, slow
    /// breathing, the head following the game, an idle finger tap. Reaching: the torso leans and turns just as much
    /// as needed for the right hand (two-bone IK) to get to the target, so even the far rank of the board is reached.
    /// Pure function of the input and the clock (the same on every peer).
    /// </summary>
    public static partial class BoardGamePose
    {
        /// <summary>Everything in character-root space (+Z = towards the table).</summary>
        public struct Input
        {
            public Vector3 SeatTop;      // centre of the cushion top
            public Vector3 TableEdge;    // point on the near table edge, at the table top height
            public float FootRestY;      // height of the stool ring the feet rest on
            public Vector3 Reach;        // right fist target
            public float ReachW;         // 0 = resting, 1 = hand at the target
            public float Think;          // 0..1 chin-on-fist
            public Vector3 Look;         // what the head looks at
            public float Seed;
            public float Lean;           // extra lean (degrees) e.g. while it is the player's turn
            public bool Standing;        // at a wall board instead of a table (see BoardGamePose.Standing.cs)
            public Vector3 Shift;        // standing: where the body steps to while reaching (root space, on the floor)
        }

        const float Reach = RigIK.UpperArm + RigIK.Forearm;

        static float Noise(float t, float seed) => Mathf.PerlinNoise(t, seed) * 2f - 1f;

        public static CharacterPose Compute(in Input inp, float time)
        {
            if (inp.Standing) return Standing(inp, time);
            var p = CharacterPose.Identity;
            float s = inp.Seed;
            float breathe = Mathf.Sin(time * 1.55f + s * 3f);
            Vector3 seatPelvis = inp.SeatTop + new Vector3(0f, 0.065f, -0.02f);
            // Half standing, hips just behind the table edge (for the far half of the board).
            Vector3 standPelvis = new Vector3(seatPelvis.x, BlockyCharacter.HipHeight - 0.03f, Mathf.Min(seatPelvis.z + 0.1f, inp.TableEdge.z - 0.16f));
            p.PelvisPos = seatPelvis;
            p.Pelvis = Quaternion.Euler(5f, 0f, 0f);

            // Torso: lean in (more while thinking), breathe. For a reach: the smallest lean (and turn) that brings the
            // shoulder in range; if sitting is not enough the player rises from the stool and leans over the table.
            float baseLean = 9f + 4f * inp.Think + inp.Lean + breathe * 1.1f;
            float yaw = Noise(time * 0.07f, s) * 4f;
            float lean = baseLean, twist = yaw, rise = 0f;
            if (inp.ReachW > 0.001f)
            {
                float best = baseLean, bestYaw = yaw, bestRise = 0f;
                bool found = false;
                for (int ri = 0; ri <= 5 && !found; ri++)
                {
                    float r = ri / 5f;
                    float maxLean = Mathf.Lerp(48f, 80f, r);
                    for (float l = baseLean; l <= maxLean && !found; l += 2f)
                    {
                        var q = p;
                        q.PelvisPos = Vector3.Lerp(seatPelvis, standPelvis, r);
                        Vector3 sh0 = TorsoAt(ref q, l, 0f).ShoulderPos(0);
                        float yw = Mathf.Clamp(Mathf.Atan2(inp.Reach.x - sh0.x * 0.4f, inp.Reach.z - q.PelvisPos.z) * Mathf.Rad2Deg * 0.45f, -28f, 28f);
                        Vector3 sh = TorsoAt(ref q, l, yw).ShoulderPos(0);
                        best = l; bestYaw = yw; bestRise = r;
                        found = Vector3.Distance(sh, inp.Reach) <= Reach * 0.93f;
                    }
                }
                float w = Mathf.SmoothStep(0f, 1f, inp.ReachW);
                lean = Mathf.Lerp(baseLean, best, w);
                twist = Mathf.Lerp(yaw, bestYaw, w);
                rise = bestRise * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(inp.ReachW * 1.4f));
            }
            p.PelvisPos = Vector3.Lerp(seatPelvis, standPelvis, rise);
            p.Pelvis = Quaternion.Euler(5f + 10f * rise, 0f, 0f);
            TorsoAt(ref p, lean - 10f * rise, twist);

            // Legs: feet on the stool ring (one foot taps now and then); rising, the feet go down to the floor.
            float tap = Mathf.Max(0f, Mathf.Sin(time * 7f)) * Mathf.Clamp01(Noise(time * 0.13f, s + 5f) * 3f - 1.2f) * (1f - rise);
            for (int leg = 0; leg < 2; leg++)
            {
                float side = leg == 0 ? 1f : -1f;
                Vector3 ring = new Vector3(seatPelvis.x + side * 0.15f, inp.FootRestY + (leg == 0 ? tap * 0.025f : 0f), seatPelvis.z + 0.17f);
                Vector3 floor = new Vector3(seatPelvis.x + side * 0.17f, 0.02f, p.PelvisPos.z + (leg == 0 ? 0.05f : -0.08f));
                p.SolveLeg(leg, Vector3.Lerp(ring, floor, Mathf.SmoothStep(0f, 1f, rise)), new Vector3(side * 0.25f, 0.35f, 1f));
            }

            // Arms resting on the table edge (forearms on the wood).
            float tableY = inp.TableEdge.y;
            Vector3 restR = new Vector3(0.19f, tableY + 0.05f, inp.TableEdge.z + 0.04f);
            Vector3 restL = new Vector3(-0.19f, tableY + 0.05f, inp.TableEdge.z + 0.04f);
            // Idle: fingers drumming on the table (right fist bobs).
            float drum = Mathf.Max(0f, Mathf.Sin(time * 11f)) * Mathf.Clamp01(Noise(time * 0.11f, s + 9f) * 3f - 1f);
            restR.y += drum * 0.018f;

            // Thinking: left elbow on the table, fist under the chin.
            Vector3 chin = p.TorsoPoint(new Vector3(0f, 0.69f, 0.2f));
            Vector3 thinkL = chin + new Vector3(0.015f, -0.085f, 0.02f) + Vector3.up * breathe * 0.004f;
            Vector3 left = Vector3.Lerp(restL, thinkL, Mathf.SmoothStep(0f, 1f, inp.Think));
            // Leaning over the table: the left fist props the body up on the table, beside the board.
            Vector3 prop = new Vector3(-0.3f, tableY + 0.05f, inp.TableEdge.z + 0.16f);
            left = Vector3.Lerp(left, prop, Mathf.SmoothStep(0f, 1f, rise * 1.5f));
            p.SolveArm(1, left, Vector3.Lerp(new Vector3(-1f, -0.5f, -0.4f), new Vector3(-0.2f, -1f, 0.3f), inp.Think * (1f - rise)));

            Vector3 right = Vector3.Lerp(restR, inp.Reach, Mathf.SmoothStep(0f, 1f, inp.ReachW));
            p.SolveArm(0, right, Vector3.Lerp(new Vector3(1f, -0.5f, -0.4f), new Vector3(0.7f, -0.7f, -0.2f), inp.ReachW));

            // Head: towards the look target, limited; a slight tilt while thinking, slow sway.
            Vector3 neck = p.TorsoPoint(BlockyCharacter.NeckRest + new Vector3(0f, 0.15f, 0f));
            Vector3 dir = Quaternion.Inverse(p.TorsoRoot) * (inp.Look - neck);
            float hy = Mathf.Clamp(Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, -60f, 60f);
            float hp = Mathf.Clamp(-Mathf.Atan2(dir.y, new Vector2(dir.x, dir.z).magnitude) * Mathf.Rad2Deg, -35f, 60f);
            hy += Noise(time * 0.3f, s + 2f) * 3f;
            hp += Noise(time * 0.25f, s + 3f) * 2f;
            p.Head = Quaternion.Euler(hp, hy, inp.Think * 7f * Mathf.Sin(time * 0.4f + s));
            return p;
        }

        static ref CharacterPose TorsoAt(ref CharacterPose p, float lean, float twist)
        {
            p.Torso = Quaternion.Euler(lean - 5f, twist, 0f);
            return ref p;
        }
    }
}
