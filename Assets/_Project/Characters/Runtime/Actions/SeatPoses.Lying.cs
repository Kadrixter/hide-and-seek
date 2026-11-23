using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Sofa pin-up poses (4-7) placed explicitly along the sofa (seat space of the left-hand seat: the own armrest at
    /// +X, the rest of the sofa towards -X, solved against <see cref="SeatShape.Wide"/>) and then settled onto the
    /// cushions; no sliding into the furniture. The seat is shallow (the rig's thighs are short), so lying bodies are
    /// turned a little towards the back cushion and lean on it.
    /// </summary>
    public static partial class SeatPoses
    {
        /// <summary>
        /// Lowers a lying body onto the cushions from above in 1 cm steps until it touches; when it gets caught on the
        /// back cushion above <paramref name="maxY"/> (pelvis height), it is moved 1 cm towards the room and lowered again.
        /// </summary>
        static void Lie(ref CharacterPose p, SeatShape s, int mask, float maxY, LegsFn legs = null)
        {
            p.PelvisPos.y = s.TopY + 0.75f;
            for (int k = 0; k < 45; k++)
            {
                while (p.PelvisPos.y > s.TopY - 0.1f)
                {
                    var q = p;
                    q.PelvisPos.y -= 0.01f;
                    legs?.Invoke(ref q);
                    if (Hits(q, s, mask)) break;
                    p = q;
                }
                var low = p;
                low.PelvisPos.y -= 0.01f;
                if (p.PelvisPos.y <= maxY || !HitsBehind(low, s, mask) || p.PelvisPos.z > s.EdgeZ - 0.04f) return;
                if (k == 0 || k == 10) Trace?.AppendLine($"    lie: caught at y {p.PelvisPos.y:0.00} z {p.PelvisPos.z:0.00} on{RestOn(p, s)}");
                p.PelvisPos.z += 0.01f;
                legs?.Invoke(ref p);
            }
        }

        static readonly SeatShape BehindShape = new SeatShape { Solids = new SeatSolid[0] };

        /// <summary>The parts touch something behind the seat (back cushion, back frame), not the cushion under them.</summary>
        static bool HitsBehind(in CharacterPose p, SeatShape s, int mask)
        {
            var list = new System.Collections.Generic.List<SeatSolid>();
            foreach (var so in s.Solids)
                if (so.Center.z < s.BackZ + 0.02f) list.Add(so);
            BehindShape.Solids = list.ToArray();
            BodyBoxes.Get(p, Boxes);
            for (int i = 0; i < BodyBoxes.Count; i++)
            {
                if ((mask & (1 << i)) == 0) continue;
                var b = Boxes[i];
                bool floorOnly = true;
                for (int c = 0; c < 8 && floorOnly; c++)
                    if (b.Point((c & 1) == 0 ? -1f : 1f, (c & 2) == 0 ? -1f : 1f, (c & 4) == 0 ? -1f : 1f).y < Margin) floorOnly = false;
                if (floorOnly && BoxHits(b, BehindShape)) return true;
            }
            return false;
        }

        /// <summary>Cushion height under a body part (the seat top minus the soft give).</summary>
        static float CushionY(SeatShape s) => s.TopY - 0.01f;

        /// <summary>
        /// 4: on the right side, facing the room, the head towards the own armrest propped on the right fist (elbow on
        /// the cushion), the bottom leg long, the top knee forward, the top hand on the top thigh.
        /// </summary>
        static CharacterPose FrenchGirl2(SeatShape s, LookLimits lim)
        {
            var best = CharacterPose.Identity;
            float bestBad = float.MaxValue;
            foreach (float rise in new[] { 12f, 16f, 8f, 20f })
            foreach (float px in new[] { -0.62f, -0.56f, -0.68f })
            foreach (float z in new[] { -0.04f, -0.01f, -0.07f })
            {
                float r = rise * Mathf.Deg2Rad;
                var p = CharacterPose.Identity;
                p.Pelvis = Frame(new Vector3(Mathf.Cos(r), Mathf.Sin(r), 0f), new Vector3(0f, 0.15f, 1f));
                p.Torso = Quaternion.Euler(0f, 0f, 4f); // the waist dips towards the cushion
                p.PelvisPos = new Vector3(px, s.TopY + 0.35f, z + 0.04f);
                NeutralHead(ref p, lim);
                Vector3 hr = p.HipPos(0), hl = p.HipPos(1);
                p.SolveLeg(0, hr + new Vector3(-0.62f, 0f, 0.07f), new Vector3(0f, 0.3f, 1f));
                p.SolveLeg(1, hl + new Vector3(-0.36f, -0.04f, 0.32f), new Vector3(0.3f, 0.6f, 1f));
                Lie(ref p, s, MTorso | MLegs, s.TopY + 0.3f);
                float bad = LegCross(p) + (Hits(p, s, MTorso | MLegs) ? 0.05f : 0f);
                // Right fist under the cheek, the elbow on the cushion below the shoulder.
                Quaternion head = p.TorsoRoot * p.Head;
                Vector3 neck = p.TorsoPoint(BlockyCharacter.NeckRest);
                Vector3 cheek = neck + head * new Vector3(0.34f, 0.04f, 0.16f);
                bad += PlaceHand(ref p, s, 0, cheek, new Vector3(-0.3f, -1f, 0.3f), Vector3.down, Vector3.left, Vector3.back, MLegs | MTorso) * 0.5f;
                bad += Reach(p, 0, cheek) * 0.5f;
                // Top hand resting on the top thigh.
                Vector3 thigh = Vector3.Lerp(p.HipPos(1), BodyBoxes.KneePos(p, 1), 0.4f) + Vector3.up * (0.127f + 0.07f);
                bad += PlaceHand(ref p, s, 1, thigh, new Vector3(0.3f, 1f, -0.4f), Vector3.forward, Vector3.left, Vector3.up, MLegs | MTorso | MHead);
                Score(p, bad, ref best, ref bestBad, $"french girl 2 rise {rise:0} x {px:0.00} z {z:0.00}");
                if (bad <= 0f) return p;
            }
            return best;
        }

        /// <summary>
        /// 5: reclined along the sofa, the back against the own armrest (leaning back on it), the legs long along the
        /// cushions over the neighbouring seat, the room-side knee up; one hand behind the head (or on the belly / the
        /// raised thigh, whichever fits), the other forearm along the top of the backrest. No hand touches the seat.
        /// </summary>
        static CharacterPose CarefreeBack2(SeatShape s, LookLimits lim)
        {
            var best = CharacterPose.Identity;
            float bestBad = float.MaxValue;
            foreach (float z in new[] { 0.0f, 0.03f, -0.03f })
            foreach (float px in new[] { 0.3f, 0.26f, 0.22f, 0.34f })
            {
                // Sitting sideways upright first (hips down on the cushion, the back near the armrest), then leaning back
                // until the back rests on the armrest.
                var p = CharacterPose.Identity;
                p.Pelvis = Quaternion.Euler(0f, -82f, 0f) * Quaternion.Euler(-8f, 0f, 0f);
                p.Torso = Quaternion.Euler(4f, 0f, 0f);
                p.PelvisPos = new Vector3(px, s.TopY + 0.13f, z);
                NeutralHead(ref p, lim);
                LegsFn legs = (ref CharacterPose q) =>
                {
                    Vector3 hl = q.HipPos(1), hr = q.HipPos(0);
                    Plant(ref q, 1, hl.x - 0.62f, hl.z - 0.03f, Dir(0f, 1f, 0.1f), CushionY(s));
                    Plant(ref q, 0, hr.x - 0.36f, hr.z + 0.07f, Dir(-0.1f, 1f, 0.25f), CushionY(s));
                };
                // Out of the armrest / back cushion if the upright body starts in them.
                for (int i = 0; i < 12 && Hits(p, s, MTorso); i++) { p.PelvisPos.x -= 0.02f; p.PelvisPos.z += 0.01f; }
                for (int i = 0; i < 4; i++)
                {
                    legs(ref p);
                    Settle(ref p, s, MTorso | MThighs);
                }
                LeanToBack(ref p, s, 30f);
                legs(ref p);
                float legBad = LegCross(p);
                float bad = legBad + (Hits(p, s, MTorso | MLegs) ? 0.05f : 0f);
                if (p.PelvisPos.y > s.TopY + 0.16f) bad += 0.05f; // sitting on the armrest, not the cushion

                // Left forearm along the top of the backrest.
                Vector3 shL = p.ShoulderPos(1);
                Vector3 back = new Vector3(shL.x - 0.22f, s.BackTopY + 0.18f, s.BackZ - 0.1f);
                bad += PlaceHand(ref p, s, 1, back, Dir(0.5f, -0.6f, -0.3f), Vector3.back, Vector3.left, Vector3.up, MLegs | MTorso | MHead);
                bad += Reach(p, 1, back) * 0.5f;

                // Right hand: behind the head, else on the belly, else on the raised thigh.
                Quaternion tr = p.TorsoRoot;
                Vector3 neck = p.TorsoPoint(BlockyCharacter.NeckRest);
                Vector3 chestN = tr * Vector3.forward;
                float br = float.MaxValue;
                var pr = p;
                {
                    var q = p;
                    Vector3 nape = neck + tr * new Vector3(0.12f, -0.17f, -0.3f);
                    float b = PlaceHand(ref q, s, 0, nape, tr * Dir(1f, 0.6f, -0.3f), tr * Vector3.back, tr * Vector3.right, tr * Vector3.down, MLegs | MTorso | MHead)
                              + Reach(q, 0, nape) * 0.5f;
                    if (b < br) { br = b; pr = q; }
                }
                if (br > 0f)
                {
                    var q = p;
                    Vector3 belly = p.TorsoPoint(new Vector3(0.05f, 0.2f, 0f)) + chestN * (0.127f + 0.127f + 0.02f);
                    float b = PlaceHand(ref q, s, 0, belly, chestN + tr * Vector3.right, tr * Vector3.up, tr * Vector3.right, chestN, MLegs | MHead)
                              + Reach(q, 0, belly) * 0.5f;
                    if (b < br) { br = b; pr = q; }
                }
                if (br > 0f)
                {
                    var q = p;
                    float b = HandOnThigh(ref q, s, 0, 0, 0.35f, 0.02f, MLegs | MTorso | MHead);
                    if (b < br) { br = b; pr = q; }
                }
                p = pr;
                bad += br;
                Score(p, bad, ref best, ref bestBad, $"carefree recline z {z:0.00} x {px:0.00}");
                if (bad <= 0f) return p;
            }
            return best;
        }

        /// <summary>6: on the belly, propped on the elbows with the chin on the fists, shins up, the head towards the middle.</summary>
        static CharacterPose Kitten2(SeatShape s, LookLimits lim)
        {
            var best = CharacterPose.Identity;
            float bestBad = float.MaxValue;
            foreach (float rise in new[] { 24f, 28f, 20f, 32f, 16f })
            foreach (float tilt in new[] { 0f, -8f, 8f, -15f, 15f })
            foreach (float px in new[] { -0.2f, -0.26f, -0.14f })
            {
                float a = rise * Mathf.Deg2Rad, t = tilt * Mathf.Deg2Rad;
                Vector3 up = new Vector3(-Mathf.Cos(a), Mathf.Sin(a), 0f);
                Vector3 chest = new Vector3(-Mathf.Sin(a), -Mathf.Cos(a) * Mathf.Cos(t), Mathf.Sin(t));
                var p = CharacterPose.Identity;
                p.Pelvis = Frame(up, chest);
                p.Torso = Quaternion.Euler(-8f, 0f, 0f); // hollow back
                p.PelvisPos = new Vector3(px, s.TopY + 0.35f, -0.02f);
                NeutralHead(ref p, lim);
                for (int leg = 0; leg < 2; leg++)
                {
                    float side = leg == 0 ? 1f : -1f;
                    Vector3 hip = p.HipPos(leg);
                    Vector3 knee = hip + Dir(1f, -0.1f, 0f) * RigIK.Thigh;
                    Vector3 sole = knee + new Vector3(leg == 0 ? 0.2f : -0.06f, 0.3f, side * 0.03f);
                    sole = knee + (sole - knee).normalized * RigIK.Shin * 0.995f;
                    p.SolveLeg(leg, sole, Dir(0.3f, -1f, 0f));
                }
                Lie(ref p, s, MTorso | MLegs, s.TopY + 0.22f);
                float bad = LegCross(p) + (Hits(p, s, MTorso | MLegs) ? 0.05f : 0f);
                if (p.PelvisPos.y > s.TopY + 0.25f) bad += 0.05f;
                Vector3 neck = p.TorsoPoint(BlockyCharacter.NeckRest);
                for (int arm = 0; arm < 2; arm++)
                {
                    float side = arm == 0 ? 1f : -1f;
                    Vector3 target = new Vector3(neck.x - 0.3f, neck.y - 0.2f, neck.z + side * 0.14f);
                    bad += PlaceHand(ref p, s, arm, target, Dir(-0.6f, -1f, side * 0.3f), Vector3.forward * side, Vector3.left, Vector3.down,
                        MLegs | MTorso | MHead | (arm == 1 ? MUpperR | MForeR : 0)) * 0.5f;
                    bad += Reach(p, arm, target) * 0.5f;
                }
                Score(p, bad, ref best, ref bestBad, $"kitten 2 rise {rise:0} tilt {tilt:0} x {px:0.00}");
                if (bad <= 0f) return p;
            }
            return best;
        }
    }
}
