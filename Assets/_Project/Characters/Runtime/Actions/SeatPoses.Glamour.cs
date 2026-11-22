using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Pin-up poses for the pose wheel (see SitPoseCatalog), solved like the others against the seat's solids.
    /// Sofa (seat space of the left-hand seat, the armrest on the right; the other seat mirrors it):
    /// 4 reclined on the side against the armrest, facing the room, the lower forearm on the armrest, the upper hand on
    /// the thigh, the top knee forward ("draw me like one of your French girls"); 5 on the back, the head towards
    /// the middle of the sofa, a hand on the belly, one ankle on the raised knee; 6 on the belly, chin on the
    /// fists, shins up; 7 sideways with the back against the armrest, knees up on the cushion, forearm along the
    /// backrest. 4-6 lie along both seats (solved against <see cref="SeatShape.Wide"/>), 7 stays on the own seat.
    /// Pouf: 8 legs crossed high, hand on the top knee, the other on the hip, back arched; 9 kneeling on the
    /// pouf, sitting on the heels, hands on the thighs; 10 leaning back on the hands, legs out, one knee popped up,
    /// chin up; 11 hand behind the head, the other on the hip, hip pushed out, knees together to one side.
    /// </summary>
    public static partial class SeatPoses
    {
        /// <summary>Leg against leg / torso: allowed overlap (both have a 1.4 cm clothing layer; thighs meet at the hips anyway).</summary>
        public const float LegAllow = 0.015f;

        static LookLimits GlamourLimits(int v)
        {
            switch (v)
            {
                case 4: return new LookLimits(55f, 30f, 35f, 0f, -5f, -20f);
                case 5: return new LookLimits(60f, 80f, -10f, 90f, 40f);
                case 6: return new LookLimits(65f, 35f, 30f, -90f, 0f);
                case 7: return new LookLimits(70f, 35f, 45f, -70f, 0f);
                case 8: return new LookLimits(70f, 40f, 50f, 15f, 5f, 6f);
                case 9: return new LookLimits(75f, 35f, 55f, 0f, 8f, 5f);
                case 10: return new LookLimits(70f, 50f, 30f, 0f, 18f);
                case 11: return new LookLimits(70f, 35f, 45f, 10f, 4f, 6f);
                default: return new LookLimits(75f, 40f, 55f);
            }
        }

        static SeatShape CurrentShape;

        static CharacterPose BuildGlamour(int v, SeatShape s, LookLimits lim)
        {
            CurrentShape = s;
            switch (v)
            {
                case 4: return FrenchGirl(s, lim);
                case 5: return CarefreeBack(s, lim);
                case 6: return Kitten(s, lim);
                case 7: return Diva(s, lim);
                case 8: return BestLean(s, (SeatShape sh, float lean, out float bad) => FemmeFatale(sh, lean, lim, out bad), -6f, 16f, -14f);
                case 9: return BestLean(s, (SeatShape sh, float lean, out float bad) => Kneeling(sh, lean, lim, out bad), 0f, 14f, -8f);
                case 10: return Sunbather(s, lim);
                case 11: return BestLean(s, (SeatShape sh, float lean, out float bad) => Model(sh, lean, lim, out bad), 2f, 14f, -6f);
                default: return CharacterPose.Identity;
            }
        }

        // ================================================================== idle

        /// <summary>Breathing (4.2 s) and swaying (2.6 s) both repeat after this many seconds.</summary>
        public const float IdlePeriod = 54.6f;

        /// <summary>
        /// Small life on top of a pin-up pose (any space: only local joints turn): breathing in the chest and a
        /// swinging foot / swaying shoulders. Amplitudes are checked by the editor seat check like the poses.
        /// </summary>
        public static void Idle(int v, SeatShape shape, ref CharacterPose p, float t)
        {
            if (v < RandomCount || v >= Count || !Supports((shape ?? SeatShape.Generic()).MirrorSource ?? shape ?? SeatShape.Generic(), v)) return;
            float breath = Mathf.Sin(t * (2f * Mathf.PI / 4.2f));
            float sway = Mathf.Sin(t * (2f * Mathf.PI / 2.6f) + 0.7f);
            p.Torso *= Quaternion.Euler(0.8f * breath, 0f, 0f);
            switch (v)
            {
                case 4: p.KneeL *= Quaternion.Euler(3f * sway, 0f, 0f); break;
                case 5: p.KneeR *= Quaternion.Euler(4f * sway, 0f, 0f); break;
                case 6:
                    p.KneeR *= Quaternion.Euler(6f * sway, 0f, 0f);
                    p.KneeL *= Quaternion.Euler(-6f * sway, 0f, 0f);
                    break;
                case 7: p.KneeR *= Quaternion.Euler(2.5f * sway, 0f, 0f); break;
                case 8: p.KneeR *= Quaternion.Euler(-3f - 3f * sway, 0f, 0f); break; // the foot only swings out, away from the knee under it
                case 9: p.Torso *= Quaternion.Euler(0f, 0f, 1.2f * sway); break;
                case 10: p.KneeR *= Quaternion.Euler(3f * sway, 0f, 0f); break;
                case 11: p.Torso *= Quaternion.Euler(0f, 0f, 1.5f * sway); break;
            }
        }

        /// <summary>Full pose with the idle motion at <paramref name="time"/> (editor check; in the game the idle goes on top of the head too).</summary>
        public static CharacterPose Evaluate(int variant, SeatShape shape, float yaw, float pitch, float time)
        {
            var p = Evaluate(variant, shape, yaw, pitch);
            Idle(variant, shape, ref p, time);
            return p;
        }

        // ================================================================== helpers

        static Quaternion Frame(Vector3 up, Vector3 chest) => Quaternion.LookRotation(Vector3.ProjectOnPlane(chest, up).normalized, up.normalized);

        static Vector3 Dir(float x, float y, float z) => new Vector3(x, y, z).normalized;

        /// <summary>How far the fist falls short of <paramref name="target"/> (0 = reached).</summary>
        static float Reach(in CharacterPose p, int arm, Vector3 target) => Mathf.Max(0f, (p.HandPos(arm) - target).magnitude - 0.06f);

        /// <summary>
        /// Deepest overlap of a leg with the other leg or a shin with the torso, beyond <see cref="LegAllow"/>
        /// (legs side by side overlap by 3 cm anyway: the rig's limbs are 4 px apart with a clothing layer each).
        /// </summary>
        public static float LegCross(in CharacterPose p, PartBox[] boxes = null) => LegCross(p, boxes, out _);

        static readonly (BodyPart a, BodyPart b, float joint)[] LegPairs =
        {
            (BodyPart.ThighR, BodyPart.ThighL, 0.03f), (BodyPart.ShinR, BodyPart.ShinL, 0.03f), (BodyPart.ShinR, BodyPart.ThighL, 0.03f),
            (BodyPart.ShinL, BodyPart.ThighR, 0.03f), (BodyPart.ShinR, BodyPart.Torso, 0f), (BodyPart.ShinL, BodyPart.Torso, 0f),
        };

        /// <summary><see cref="LegCross(in CharacterPose, PartBox[])"/> naming the worst pair.</summary>
        public static float LegCross(in CharacterPose p, PartBox[] boxes, out string worst)
        {
            boxes ??= Boxes;
            BodyBoxes.Get(p, boxes);
            float w = 0f;
            worst = "-";
            foreach (var (a, b, joint) in LegPairs)
            {
                float d = BodyBoxes.Penetration(boxes[(int)a], boxes[(int)b]) - joint;
                if (d > w) { w = d; worst = $"{a} x {b}"; }
            }
            return Mathf.Max(0f, w - LegAllow);
        }

        /// <summary>The head at the pose's neutral look (hands are placed around it).</summary>
        static void NeutralHead(ref CharacterPose p, in LookLimits lim) =>
            p.Head = Quaternion.Inverse(p.TorsoRoot) * HeadRot(lim, lim.Center, lim.Pitch);

        /// <summary>
        /// Slides the body along <paramref name="dir"/> (1 cm steps, re-settled) until a part in <paramref name="stop"/>
        /// or the head (at its neutral look; the look range is fitted afterwards) would touch, or the body would have to
        /// climb (onto the armrest) instead of resting on the cushion.
        /// </summary>
        static void SlideUntil(ref CharacterPose p, SeatShape s, Vector3 dir, int restMask, int stop, LookLimits lim, int steps = 90, float climb = 0.025f, bool head = true)
        {
            float rest = p.PelvisPos.y;
            int headMask = head && !Hits(p, s, MHead) ? MHead : 0; // the look range is fitted afterwards anyway
            for (int i = 0; i < steps; i++)
            {
                var q = p;
                q.PelvisPos += dir * 0.01f;
                Settle(ref q, s, restMask);
                if (q.PelvisPos.y > rest + climb || Hits(q, s, stop | headMask)) // the head at its neutral look
                {
                    Trace?.AppendLine($"    slid {i} cm, stopped: {(q.PelvisPos.y > rest + climb ? $"climbs {(q.PelvisPos.y - rest) * 100f:0.0} cm" : "touches" + RestOn(q, s))}");
                    return;
                }
                p = q;
            }
        }

        /// <summary>Editor trace: what the body rests on (part and solid index hit when lowered 1 cm).</summary>
        static string RestOn(in CharacterPose p, SeatShape s)
        {
            var q = p;
            q.PelvisPos.y -= 0.01f;
            BodyBoxes.Get(q, Boxes);
            var one = new SeatShape { Solids = new SeatSolid[1] };
            string r = "";
            for (int i = 0; i < BodyBoxes.Count; i++)
            {
                if (i == (int)BodyPart.Head) continue;
                for (int n = 0; n < s.Solids.Length; n++)
                {
                    one.Solids[0] = s.Solids[n];
                    if (BoxHits(Boxes[i], one)) r += $" {(BodyPart)i}/{n}";
                }
            }
            return r.Length > 0 ? r : " nothing";
        }

        static void Score(in CharacterPose p, float bad, ref CharacterPose best, ref float bestBad, string what)
        {
            Trace?.AppendLine($"   {what}: bad {bad * 100f:0.0}, pelvis {p.PelvisPos.x:0.00} {p.PelvisPos.y:0.00} {p.PelvisPos.z:0.00}, rests on{RestOn(p, CurrentShape)}");
            if (bad < bestBad) { bestBad = bad; best = p; }
        }

        // ================================================================== sofa

        /// <summary>
        /// "Draw me like one of your French girls": reclined on the right side against the armrest, facing the room,
        /// the head leaning on the armrest, the left arm over the head on the arm roll, the right hand on the cushion
        /// in front of the chest, the bottom leg long, the top knee bent forward over it.
        /// </summary>
        static CharacterPose FrenchGirl(SeatShape s, LookLimits lim)
        {
            var best = CharacterPose.Identity;
            float bestBad = float.MaxValue;
            foreach (float rise in new[] { 34f, 30f, 38f, 42f, 26f })
            foreach (float z in new[] { -0.12f, -0.07f, -0.16f })
            {
                float a = rise * Mathf.Deg2Rad, r = 28f * Mathf.Deg2Rad;
                Vector3 up = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                Vector3 n = new Vector3(-Mathf.Sin(a), Mathf.Cos(a), 0f);
                var p = CharacterPose.Identity;
                p.Pelvis = Frame(up, Vector3.forward * Mathf.Cos(r) + n * Mathf.Sin(r));
                p.Torso = Quaternion.Euler(-4f, 0f, 0f);
                p.PelvisPos = new Vector3(-0.75f, s.TopY + 0.3f, z);
                NeutralHead(ref p, lim);
                // Legs stacked (the hips are one above the other), knees bent forward a little, the top one a bit more.
                float legBad = float.MaxValue;
                var keep = p;
                foreach (float bend in new[] { 0.35f, 0.25f, 0.45f })
                foreach (float extra in new[] { 0.08f, 0.04f, 0f })
                foreach (float dx in new[] { 0.04f, 0.09f, 0f })
                {
                    var q = p;
                    Vector3 hr = q.HipPos(0), hl = q.HipPos(1);
                    Vector3 sole = new Vector3(-0.6f, -0.08f, 0.05f);
                    Vector3 pole = Vector3.forward + Vector3.up * bend;
                    q.SolveLeg(0, hr + sole, pole);
                    q.SolveLeg(1, hl + sole + new Vector3(dx, 0f, extra), pole);
                    float lb = LegCross(q);
                    if (lb < legBad) { keep = q; legBad = lb; }
                    if (lb <= 0f) break;
                }
                p = keep;
                Settle(ref p, s, MTorso | MLegs);
                SlideUntil(ref p, s, Vector3.right, MTorso | MLegs, MTorso | MLegs, lim);
                float bad = legBad + (Hits(p, s, MTorso | MLegs) ? 0.05f : 0f);

                // Right (lower) forearm on the arm roll in front of the chest, left hand on the top thigh.
                Vector3 sh = p.ShoulderPos(0);
                float best0 = float.MaxValue;
                var p0 = p;
                foreach (float fwd in new[] { 0.28f, 0.2f, 0.36f })
                {
                    var q = p;
                    Vector3 arm = new Vector3(s.ArmInnerX + 0.08f, s.ArmTopY + 0.13f, Mathf.Min(sh.z + fwd, s.EdgeZ - 0.02f));
                    float b0 = PlaceHand(ref q, s, 0, arm, Dir(-0.5f, -0.6f, -0.3f), Vector3.forward, Vector3.right, Vector3.up, MLegs | MTorso | MHead)
                               + Reach(q, 0, arm) * 0.5f;
                    if (b0 < best0) { best0 = b0; p0 = q; }
                    if (b0 <= 0f) break;
                }
                p = p0;
                bad += best0;
                bad += HandOnKnee(ref p, s, 1, 1, 0.15f, MLegs | MTorso | MHead);
                Score(p, bad, ref best, ref bestBad, $"french girl rise {rise:0} z {z:0.00}");
                if (bad <= 0f) return p;
            }
            return best;
        }

        /// <summary>
        /// On the back along the sofa, the head towards the middle (the corners hold the throw pillows), the feet towards
        /// the own armrest: left knee up, right ankle on it, one hand on the belly, the other arm along the body. Looks along the body to the feet and up.
        /// </summary>
        static CharacterPose CarefreeBack(SeatShape s, LookLimits lim)
        {
            var best = CharacterPose.Identity;
            float bestBad = float.MaxValue;
            foreach (float rise in new[] { 18f, 22f, 14f, 26f })
            foreach (float z in new[] { -0.08f, -0.04f, -0.12f })
            {
                float a = rise * Mathf.Deg2Rad;
                Vector3 up = new Vector3(-Mathf.Cos(a), Mathf.Sin(a), 0f);
                Vector3 chest = new Vector3(Mathf.Sin(a), Mathf.Cos(a), 0f);
                var p = CharacterPose.Identity;
                p.Pelvis = Frame(up, chest); // right side towards the backrest
                p.PelvisPos = new Vector3(-0.3f, s.TopY + 0.3f, z);
                NeutralHead(ref p, lim);
                // Left (room side) knee up, the foot flat on the cushion; right ankle on top of the left knee, that knee open.
                Vector3 hl = p.HipPos(1);
                p.SolveLeg(1, hl + new Vector3(0.42f, -0.06f, 0.02f), Vector3.up);
                Vector3 kl = BodyBoxes.KneePos(p, 1);
                float legBad = float.MaxValue;
                var keep = p;
                foreach (float lift in new[] { 0.25f, 0.27f, 0.29f, 0.31f, 0.33f, 0.35f, 0.38f, 0.41f })
                foreach (float dx in new[] { -0.02f, -0.08f, 0.05f, -0.14f })
                foreach (float open in new[] { 0.5f, 0.8f, 1.2f })
                {
                    var q = p;
                    Vector3 hr = q.HipPos(0);
                    Vector3 sole = kl + new Vector3(dx, lift, -0.03f);
                    Vector3 axis = (sole - hr).normalized;
                    q.SolveLeg(0, sole, Vector3.ProjectOnPlane(Dir(-0.1f, 0.8f, -open), axis));
                    float lb = LegCross(q);
                    if (lb < legBad) { legBad = lb; keep = q; }
                    if (lb <= 0f) break;
                }
                p = keep;
                Settle(ref p, s, MTorso | MLegs);
                // Feet up to the armrest / the throw pillow in its corner.
                SlideUntil(ref p, s, Vector3.right, MTorso | MLegs, MTorso | MLegs, lim, 90, 0.06f, false); // over the cushion's puff; the head lies back
                float bad = legBad + (Hits(p, s, MTorso | MLegs) ? 0.05f : 0f);
                if (p.PelvisPos.y > s.TopY + 0.25f) bad += 0.1f;
                if (Hits(p, s, MHead)) bad += 0.03f; // the head lies back on the cushion: clear of it at the neutral look

                // Left (room side) hand on the belly, the right arm along the body, the fist on the cushion by the hip.
                Vector3 belly = p.TorsoPoint(new Vector3(-0.13f, 0.25f, 0.141f + 0.127f + 0.01f));
                bad += PlaceHand(ref p, s, 1, belly, TorsoDir(p, -1f, 0f, 0.4f), TorsoDir(p, -1f, 0f, 0f), TorsoDir(p, 0f, 1f, 0f), TorsoDir(p, 0f, 0f, 1f),
                    MLegs | MTorso | MHead);
                bad += Reach(p, 1, belly) * 0.5f;
                Vector3 byHip = p.TorsoPoint(new Vector3(0.38f, 0.12f, 0f));
                byHip.y = s.TopY + 0.13f;
                bad += PlaceHand(ref p, s, 0, byHip, TorsoDir(p, 1f, 0.3f, 0f), TorsoDir(p, 1f, 0f, 0f), TorsoDir(p, 0f, 1f, 0f), Vector3.up,
                    MLegs | MTorso | MHead | MForeL | MUpperL);
                bad += Reach(p, 0, byHip) * 0.5f;
                Score(p, bad, ref best, ref bestBad, $"carefree rise {rise:0} z {z:0.00}");
                if (bad <= 0f) return p;
            }
            return best;
        }

        /// <summary>On the belly, propped on the elbows with the chin on the fists, shins up, the feet towards each other.</summary>
        static CharacterPose Kitten(SeatShape s, LookLimits lim)
        {
            var best = CharacterPose.Identity;
            float bestBad = float.MaxValue;
            foreach (float rise in new[] { 18f, 14f, 22f, 26f, 10f })
            foreach (float z in new[] { -0.04f, 0f, -0.08f })
            {
                float a = rise * Mathf.Deg2Rad;
                Vector3 up = new Vector3(-Mathf.Cos(a), Mathf.Sin(a), 0f);
                Vector3 chest = new Vector3(-Mathf.Sin(a), -Mathf.Cos(a), 0f);
                var p = CharacterPose.Identity;
                p.Pelvis = Frame(up, chest);
                p.Torso = Quaternion.Euler(-6f, 0f, 0f); // hollow back
                p.PelvisPos = new Vector3(-0.45f, s.TopY + 0.3f, z);
                NeutralHead(ref p, lim);
                float legBad = float.MaxValue;
                var keep = p;
                foreach (float spread in new[] { 0.36f, 0.42f, 0.48f, 0.3f })
                {
                    var q = p;
                    for (int leg = 0; leg < 2; leg++)
                    {
                        float side = leg == 0 ? 1f : -1f; // right leg on the room side (+z)
                        Vector3 hip = q.HipPos(leg);
                        Vector3 knee = hip + Dir(1f, -0.12f, 0f) * RigIK.Thigh;
                        // Shins up, the feet towards each other, the right one leaning back, the left one forward.
                        Vector3 sole = knee + new Vector3(leg == 0 ? spread * 0.7f : -spread * 0.15f, 0.29f, side * 0.04f);
                        sole = knee + (sole - knee).normalized * RigIK.Shin * 0.995f;
                        q.SolveLeg(leg, sole, Dir(0.3f, -1f, 0f));
                    }
                    float lb = LegCross(q);
                    if (lb < legBad) { legBad = lb; keep = q; }
                    if (lb <= 0f) break;
                }
                p = keep;
                Settle(ref p, s, MTorso | MThighs);
                SlideUntil(ref p, s, Vector3.right, MTorso | MThighs, MTorso | MLegs, lim);
                float bad = legBad + (Hits(p, s, MTorso | MLegs) ? 0.05f : 0f);

                // Elbows on the cushion under the shoulders, fists under the chin.
                Vector3 neck = p.TorsoPoint(BlockyCharacter.NeckRest);
                for (int arm = 0; arm < 2; arm++)
                {
                    float side = arm == 0 ? 1f : -1f;
                    Vector3 target = new Vector3(neck.x - 0.34f, neck.y - 0.22f, neck.z + side * 0.15f);
                    bad += PlaceHand(ref p, s, arm, target, Dir(-0.8f, -1f, side * 0.3f), Vector3.forward * side, Vector3.left, Vector3.down,
                        MLegs | MTorso | MHead | (arm == 1 ? MUpperR | MForeR : 0));
                    bad += Reach(p, arm, target) * 0.5f;
                }
                Score(p, bad, ref best, ref bestBad, $"kitten rise {rise:0} z {z:0.00}");
                if (bad <= 0f) return p;
            }
            return best;
        }

        /// <summary>Sideways on the own seat, the back against the armrest, knees up on the cushion, the left forearm along the backrest.</summary>
        static CharacterPose Diva(SeatShape s, LookLimits lim)
        {
            var best = CharacterPose.Identity;
            float bestBad = float.MaxValue;
            foreach (float lean in new[] { 14f, 10f, 18f, 6f, 22f })
            foreach (float z in new[] { 0.04f, 0.07f, 0.01f })
            {
                var p = CharacterPose.Identity;
                p.Pelvis = Quaternion.Euler(0f, -82f, 0f) * Quaternion.Euler(-8f, 0f, 0f);
                p.Torso = Quaternion.Euler(8f - lean, 0f, 0f);
                p.PelvisPos = new Vector3(0.08f, s.TopY + 0.25f, z);
                NeutralHead(ref p, lim);
                for (int leg = 0; leg < 2; leg++)
                {
                    Vector3 hip = p.HipPos(leg);
                    // Knees up and leaning a little towards the room, feet on the cushion.
                    p.SolveLeg(leg, hip + new Vector3(-0.42f, -0.05f, leg == 0 ? 0.08f : -0.03f), Dir(-0.1f, 1f, leg == 0 ? 0.25f : 0.1f));
                }
                float legBad = LegCross(p);
                Settle(ref p, s, MTorso | MLegs);
                SlideUntil(ref p, s, Vector3.right, MTorso | MLegs, MTorso | MLegs, lim);
                float bad = legBad + (Hits(p, s, MTorso | MLegs) ? 0.05f : 0f);
                if (p.PelvisPos.y > s.TopY + 0.25f) bad += 0.05f; // sitting on the pillow / armrest, not the cushion

                // Left forearm along the top of the backrest.
                Vector3 shL = p.ShoulderPos(1);
                Vector3 back = new Vector3(shL.x - 0.22f, s.BackTopY + 0.08f, s.BackZ - 0.08f);
                bad += PlaceHand(ref p, s, 1, back, Dir(0.5f, -0.6f, -0.3f), Vector3.back, Vector3.left, Vector3.up, MLegs | MTorso | MHead);
                bad += Reach(p, 1, back) * 0.5f;
                // Right hand on top of the right knee, else on the cushion in front of the hip.
                Vector3 knee = BodyBoxes.KneePos(p, 0);
                float bestR = float.MaxValue;
                var pr = p;
                foreach (var target in new[]
                         {
                             knee + new Vector3(0.04f, 0.127f + 0.075f, 0.06f),
                             new Vector3(p.PelvisPos.x - 0.12f, s.TopY + 0.13f, Mathf.Min(p.PelvisPos.z + 0.33f, s.EdgeZ - 0.04f)),
                             new Vector3(p.PelvisPos.x + 0.02f, s.TopY + 0.13f, Mathf.Min(p.PelvisPos.z + 0.34f, s.EdgeZ - 0.04f)),
                         })
                {
                    var q = p;
                    float br = PlaceHand(ref q, s, 0, target, Dir(0.3f, -0.5f, 1f), Vector3.forward, Vector3.right, Vector3.up, MLegs | MTorso | MHead)
                               + Reach(q, 0, target) * 0.5f;
                    if (br < bestR) { bestR = br; pr = q; }
                    if (br <= 0f) break;
                }
                p = pr;
                bad += bestR;
                Score(p, bad, ref best, ref bestBad, $"diva lean {lean:0} z {z:0.00}");
                if (bad <= 0f) return p;
            }
            return best;
        }

        // ================================================================== pouf

        /// <summary>Legs crossed high (right over left), a hand on the top knee, the other on the hip, back arched.</summary>
        static CharacterPose FemmeFatale(SeatShape s, float lean, LookLimits lim, out float bad)
        {
            var p = CharacterPose.Identity;
            p.Pelvis = Quaternion.Euler(6f, -6f, 0f);
            p.Torso = Quaternion.Euler(lean - 6f, 10f, -2f);
            p.PelvisPos = new Vector3(0f, s.TopY + 0.13f, s.EdgeZ - 0.27f);
            NeutralHead(ref p, lim);
            Leg(ref p, 0, 4f, 8f, 14f, 2f);
            Leg(ref p, 1, 4f, 8f, 14f, 2f);
            FitSeat(ref p, s, MThighs);
            CrossHigh(ref p, s, out float legBad);
            // One hand on the top (right) knee, the other on the hip, the back arched.
            float best = float.MaxValue;
            var keep = p;
            foreach (int knee in new[] { 1, 0 })
            foreach (float back in new[] { 0.05f, 0.12f, 0.2f, 0.28f })
            {
                var q = p;
                int other = 1 - knee;
                float b = HandOnKnee(ref q, s, knee, 0, back, MLegs | MTorso | MHead);
                b += HandOnHip(ref q, s, other);
                if (b < best) { best = b; keep = q; }
                if (b <= 0f) break;
            }
            p = keep;
            bad = legBad + best;
            Trace?.AppendLine($"   femme fatale lean {lean:0}: legs {legBad * 100f:0.0}, hands {best * 100f:0.0}");
            return p;
        }

        /// <summary>Right thigh over the left one (as low as the thick thighs allow), the shin hanging in front of the left knee.</summary>
        static void CrossHigh(ref CharacterPose p, SeatShape s, out float legBad)
        {
            legBad = float.MaxValue;
            var keep = p;
            for (float rise = 10f; rise <= 58f; rise += 3f)
            {
                foreach (float inward in new[] { 22f, 16f, 28f, 10f })
                foreach (float fwd in new[] { 0.45f, 0.6f, 0.75f, 0.9f, 1.1f, 1.4f, 1.8f })
                {
                    var q = p;
                    Vector3 hip = q.HipPos(0);
                    Vector3 knee = hip + Quaternion.Euler(-rise, -inward, 0f) * Vector3.forward * RigIK.Thigh;
                    Vector3 sole = knee + Dir(-0.18f, -1f, fwd) * RigIK.Shin * 0.995f;
                    q.SolveLeg(0, sole, knee - (hip + sole) * 0.5f);
                    float lb = LegCross(q) + (Hits(q, s, MLegs) ? 0.05f : 0f);
                    if (lb < legBad) { legBad = lb; keep = q; }
                    if (lb <= 0f)
                    {
                        p = keep;
                        legBad = 0f;
                        return;
                    }
                }
            }
            p = keep;
        }

        /// <summary>Kneeling on the pouf, sitting on the heels, hands on the thighs, back arched, chest out.</summary>
        static CharacterPose Kneeling(SeatShape s, float lean, LookLimits lim, out float bad)
        {
            var p = CharacterPose.Identity;
            p.Pelvis = Quaternion.Euler(8f, 0f, 0f);
            p.Torso = Quaternion.Euler(lean - 14f, 0f, 0f);
            p.PelvisPos = new Vector3(0f, s.TopY + 0.6f, s.EdgeZ - 0.42f);
            NeutralHead(ref p, lim);
            for (int leg = 0; leg < 2; leg++)
            {
                float side = leg == 0 ? 1f : -1f;
                Vector3 hip = p.HipPos(leg);
                // Thigh forward and down to the knee at the front of the top, the shin back along the top.
                Vector3 knee = hip + Dir(0f, -0.68f, 0.74f) * RigIK.Thigh;
                Vector3 sole = knee + Dir(side * 0.03f, 0f, -1f) * RigIK.Shin * 0.995f;
                p.SolveLeg(leg, sole, knee - (hip + sole) * 0.5f);
            }
            Settle(ref p, s, MShins | MThighs);
            // The knees must stay on the top: back off while the shins hang over the front edge.
            bad = LegCross(p);
            for (int i = 0; i < 30 && BodyBoxes.KneePos(p, 0).z > s.EdgeZ - 0.16f; i++)
            {
                p.PelvisPos.z -= 0.01f;
                Settle(ref p, s, MShins | MThighs);
            }
            for (int arm = 0; arm < 2; arm++) bad += HandOnKnee(ref p, s, arm, arm, 0.12f, MLegs | MTorso);
            return p;
        }

        /// <summary>Leaning back on the hands on the pouf, legs long, one knee popped up, chin up.</summary>
        static CharacterPose Sunbather(SeatShape s, LookLimits lim)
        {
            var best = CharacterPose.Identity;
            float bestBad = float.MaxValue;
            for (float lean = 18f; lean <= 46f; lean += 2f)
            {
                var p = CharacterPose.Identity;
                p.Pelvis = Quaternion.Euler(-12f, 0f, 0f);
                p.Torso = Quaternion.Euler(12f - lean, 0f, 0f);
                p.PelvisPos = new Vector3(0f, s.TopY + 0.13f, s.EdgeZ - 0.27f);
                NeutralHead(ref p, lim);
                // Legs long and down to the front.
                float drop = 0.36f;
                float legBad = 0f;
                for (int i = 0; i < 16; i++)
                {
                    Vector3 hl = p.HipPos(1);
                    Vector3 soleL = hl + new Vector3(0.03f, -drop, 0.58f);
                    p.SolveLeg(1, soleL, Dir(0f, 1f, 0.2f));
                    soleL = BodyBoxes.SolePos(p, 1);
                    // Right leg beside it, the knee popped up a little (the foot pulled back).
                    legBad = float.MaxValue;
                    var keep = p;
                    foreach (float back in new[] { 0.1f, 0.06f, 0.14f })
                    foreach (float dx in new[] { 0.24f, 0.28f, 0.32f })
                    {
                        var q = p;
                        q.SolveLeg(0, soleL + new Vector3(dx, 0.02f, -back), Dir(0.15f, 1f, 0.3f));
                        float lb = LegCross(q);
                        if (lb < legBad) { legBad = lb; keep = q; }
                        if (lb <= 0f) break;
                    }
                    p = keep;
                    FitSeat(ref p, s, MThighs);
                    if (!Hits(p, s, MShins) && legBad <= 0f) break;
                    drop -= 0.025f;
                }
                float bad = legBad + (Hits(p, s, MLegs) ? 0.05f : 0f);
                for (int arm = 0; arm < 2; arm++)
                {
                    float side = arm == 0 ? 1f : -1f;
                    Vector3 hand = new Vector3(side * 0.2f, s.TopY + 0.12f, s.RearZ + 0.08f);
                    bad += PlaceHand(ref p, s, arm, hand, Dir(side, 0f, -0.7f), Vector3.right * side, Vector3.back, Vector3.up, MLegs | MTorso);
                    bad += Reach(p, arm, hand);
                }
                Score(p, bad, ref best, ref bestBad, $"sunbather lean {lean:0}");
                if (bad <= 0f) return p;
            }
            return best;
        }

        /// <summary>Right hand behind the head (at the nape), left hand on the hip, hip pushed out, knees together and slanted to the left.</summary>
        static CharacterPose Model(SeatShape s, float lean, LookLimits lim, out float bad)
        {
            var p = CharacterPose.Identity;
            p.Pelvis = Quaternion.Euler(2f, 10f, 5f);
            p.Torso = Quaternion.Euler(lean, -8f, -10f);
            p.PelvisPos = new Vector3(0f, s.TopY + 0.13f, s.EdgeZ - 0.26f);
            NeutralHead(ref p, lim);
            Leg(ref p, 0, -6f, 6f, 24f, -2f);
            Leg(ref p, 1, 19f, 6f, 24f, 15f);
            FitSeat(ref p, s, MThighs);
            bad = LegCross(p);
            // Right fist behind the nape, under the back of the head (the head turns freely above it), elbow up and out.
            Vector3 neck = p.TorsoPoint(BlockyCharacter.NeckRest);
            Quaternion tr = p.TorsoRoot;
            Vector3 nape = neck + tr * new Vector3(0.12f, -0.17f, -0.3f);
            bad += PlaceHand(ref p, s, 0, nape, tr * Dir(1f, 0.6f, -0.3f), tr * Vector3.back, tr * Vector3.right, tr * Vector3.down, MLegs | MTorso | MHead);
            bad += Reach(p, 0, nape) * 0.5f;
            bad += HandOnHip(ref p, s, 1);
            return p;
        }
    }
}
