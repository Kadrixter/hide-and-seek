using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Pin-up poses for the pose wheel (see SitPoseCatalog), solved like the others against the seat's solids.
    /// Sofa (seat space of the left-hand seat, the armrest on the right; the other seat mirrors it):
    /// 4 reclined on the side against the armrest, facing the room, the lower forearm on the armrest, the upper hand on
    /// the thigh, the top knee forward ("draw me like one of your French girls"); 5 reclined along the sofa with the
    /// back against the armrest, legs long, one knee up, a hand behind the head, the other forearm on the backrest; 6 on the belly, chin on the
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
                case 5: return new LookLimits(65f, 35f, 35f, -70f, 8f);
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
                case 4: return FrenchGirl2(s, lim);
                case 5: return CarefreeBack2(s, lim);
                case 6: return Kitten2(s, lim);
                case 7: return Diva(s, lim);
                case 8: return FirstFit(s, (SeatShape sh, float lean, out float bad) => FemmeFatale2(sh, lean, lim, out bad), -10f, -14f, -6f, -18f, -2f);
                case 9: return FirstFit(s, (SeatShape sh, float lean, out float bad) => PinUp2(sh, lean, lim, out bad), 8f, 4f, 12f, 0f);
                case 10: return FirstFit(s, (SeatShape sh, float lean, out float bad) => Sunbather2(sh, lean, lim, out bad), 30f, 34f, 38f, 26f, 42f, 46f);
                case 11: return FirstFit(s, (SeatShape sh, float lean, out float bad) => Model2(sh, lean, lim, out bad), 2f, 6f, -2f, 10f);
                default: return CharacterPose.Identity;
            }
        }

        // ================================================================== idle

        /// <summary>The editor seat check samples the idle motion over this many seconds.</summary>
        public const float IdlePeriod = 54.6f;

        /// <summary>Seed of the idle motion in the editor check / renders (the game uses one per character).</summary>
        public const int EditorIdleSeed = 7;

        /// <summary>Idle motion on top of a pose (see <see cref="SeatIdle"/>).</summary>
        public static void Idle(int v, SeatShape shape, ref CharacterPose p, float t) => SeatIdle.Apply(v, shape, ref p, t, EditorIdleSeed, false);

        /// <summary>Full pose with the idle motion at <paramref name="time"/> (editor check).</summary>
        public static CharacterPose Evaluate(int variant, SeatShape shape, float yaw, float pitch, float time)
        {
            var p = Evaluate(variant, shape, yaw, pitch);
            Idle(variant, shape, ref p, time);
            return p;
        }

        /// <summary>As in the game while the player leaves the look alone: the head wanders too (editor renders).</summary>
        public static CharacterPose EvaluateIdle(int variant, SeatShape shape, float time, int seed)
        {
            var lim = Limits(variant, shape);
            Vector2 look = SeatIdle.LookAround(variant, time, seed);
            var p = Evaluate(variant, shape, lim.Center + look.x, lim.Pitch + look.y);
            SeatIdle.Apply(variant, shape, ref p, time, seed, false);
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
            (BodyPart.ThighR, BodyPart.ThighL, 0.065f), // the thighs meet at the crotch / lie on each other (clothing only) (BodyPart.ShinR, BodyPart.ShinL, 0.03f), (BodyPart.ShinR, BodyPart.ThighL, 0.03f),
            (BodyPart.ShinL, BodyPart.ThighR, 0.03f), (BodyPart.ShinR, BodyPart.Torso, 0.01f), (BodyPart.ShinL, BodyPart.Torso, 0.01f),
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

        /// <summary>Sideways on the own seat, the back against the armrest, knees up on the cushion, the left forearm along the backrest.</summary>
        static CharacterPose Diva(SeatShape s, LookLimits lim)
        {
            var best = CharacterPose.Identity;
            float bestBad = float.MaxValue;
            foreach (float lean in new[] { 14f, 10f, 18f, 6f, 22f })
            foreach (float z in new[] { 0.0f, 0.03f, -0.03f })
            foreach (float px in new[] { 0.26f, 0.22f, 0.3f, 0.18f })
            {
                var p = CharacterPose.Identity;
                p.Pelvis = Quaternion.Euler(0f, -82f, 0f) * Quaternion.Euler(-8f, 0f, 0f);
                p.Torso = Quaternion.Euler(8f - lean, 0f, 0f);
                p.PelvisPos = new Vector3(px, s.TopY + 0.25f, z);
                NeutralHead(ref p, lim);
                for (int leg = 0; leg < 2; leg++)
                {
                    Vector3 hip = p.HipPos(leg);
                    // Knees up and leaning a little towards the room, feet on the cushion.
                    p.SolveLeg(leg, hip + new Vector3(-0.42f, -0.05f, leg == 0 ? 0.08f : -0.03f), Dir(-0.1f, 1f, leg == 0 ? 0.25f : 0.1f));
                }
                float legBad = LegCross(p);
                Lie(ref p, s, MTorso | MLegs, s.TopY + 0.15f);
                LeanToBack(ref p, s, 15f); // the back on the armrest
                float bad = legBad + (Hits(p, s, MTorso | MLegs) ? 0.05f : 0f);
                if (p.PelvisPos.y > s.TopY + 0.16f) bad += 0.05f; // sitting on the armrest, not the cushion
                bad += Mathf.Max(0f, p.PelvisPos.y - (s.TopY + 0.13f)) * 2f; // the hips down on the cushion

                // Left forearm along the top of the backrest.
                Vector3 shL = p.ShoulderPos(1);
                Vector3 back = new Vector3(shL.x - 0.22f, s.BackTopY + 0.18f, s.BackZ - 0.1f);
                bad += PlaceHand(ref p, s, 1, back, Dir(0.5f, -0.6f, -0.3f), Vector3.back, Vector3.left, Vector3.up, MLegs | MTorso | MHead);
                bad += Reach(p, 1, back) * 0.5f;
                // Right arm draped back over the armrest roll.
                Vector3 roll = new Vector3(p.PelvisPos.x + 0.22f, s.ArmTopY + 0.09f, p.PelvisPos.z + 0.16f);
                bad += PlaceHand(ref p, s, 0, roll, new Vector3(0.3f, -0.2f, 1f), Vector3.right, Vector3.forward, Vector3.up, MLegs | MTorso | MHead);
                bad += Reach(p, 0, roll) * 0.5f;
                Score(p, bad, ref best, ref bestBad, $"diva lean {lean:0} z {z:0.00}");
                if (bad <= 0f) return p;
            }
            return best;
        }

        // ================================================================== pouf

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

    }
}
