using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Pose "Prosto" on an office chair (<see cref="SeatShape.Office"/>, security rooms): the hips sunk into the seat,
    /// the thighs lying on it, the back against the backrest, the feet down on the floor, the forearms on the armrests.
    /// The blocky legs are short, so the thighs slope down to the knees until the soles reach the floor; the solver
    /// tries a few thigh / shin / back angles and keeps the one with the feet closest to the floor and nothing in the
    /// chair.
    /// </summary>
    public static partial class SeatPoses
    {
        static CharacterPose Office(SeatShape s, LookLimits lim)
        {
            CharacterPose best = CharacterPose.Identity;
            float bestScore = float.MaxValue;
            foreach (float recline in new[] { 6f, 9f, 3f })
            foreach (float drop in new[] { 14f, 22f, 30f, 36f })
            foreach (float shin in new[] { -6f, 2f, 10f })
            {
                var p = CharacterPose.Identity;
                p.Pelvis = Quaternion.Euler(-recline * 0.5f, 0f, 0f);
                p.Torso = Quaternion.Euler(-recline * 0.5f, 0f, 0f);
                p.PelvisPos = new Vector3(0f, s.TopY + 0.13f, s.EdgeZ - 0.1f);
                Leg(ref p, 0, 0f, drop, shin, 1f);
                Leg(ref p, 1, 0f, drop, shin, 1f);
                if (Trace != null) Trace.AppendLine($"   before settle pelvis {p.PelvisPos.y:0.000}: {Why(p, s)}");
                Settle(ref p, s, MThighs);
                if (Trace != null) Trace.AppendLine($"   after settle pelvis {p.PelvisPos.y:0.000}: {Why(p, s)}");
                if (Hits(p, s, MTorso) || HeadHitsAnywhere(p, s, lim))
                {
                    Trace?.AppendLine($"   office recline {recline} drop {drop} shin {shin}: torso {Hits(p, s, MTorso)} head {HeadHitsAnywhere(p, s, lim)} at start, pelvis {p.PelvisPos}");
                    continue;
                }
                // Back until the back meets the backrest (or the head would, looking around).
                for (int i = 0; i < 40; i++)
                {
                    var q = p;
                    q.PelvisPos.z -= 0.01f;
                    Settle(ref q, s, MThighs);
                    if (Hits(q, s, MTorso | MShins) || HeadHitsAnywhere(q, s, lim)) break;
                    p = q;
                }
                if (Hits(p, s, MShins))
                {
                    Trace?.AppendLine($"   office recline {recline} drop {drop} shin {shin}: shins hit, sole {Mathf.Min(LowestY(p, (int)BodyPart.ShinR), LowestY(p, (int)BodyPart.ShinL)):0.000}");
                    continue;   // shins in the seat front / feet through the floor
                }
                float sole = Mathf.Min(LowestY(p, (int)BodyPart.ShinR), LowestY(p, (int)BodyPart.ShinL));
                float backGap = p.PelvisPos.z - (s.BackZ + 0.16f);
                float score = Mathf.Abs(sole - 0.02f) * 10f + Mathf.Max(0f, backGap) * 4f + Mathf.Abs(drop - 14f) * 0.004f;
                var armed = p;
                float bad = 0f;
                for (int arm = 0; arm < 2; arm++)
                {
                    float side = arm == 0 ? 1f : -1f;
                    if (s.ArmTopY > 0f)
                    {
                        // Forearm along the armrest, the fist near its front end.
                        Vector3 hand = new Vector3(side * (s.ArmInnerX + 0.045f), s.ArmTopY + 0.065f, armed.ShoulderPos(arm).z + 0.24f);
                        float b = PlaceHand(ref armed, s, arm, hand, new Vector3(side * 0.5f, -0.6f, -0.5f), Vector3.right * side, Vector3.forward, Vector3.up, MLegs | MTorso);
                        if (b > 0f) b = HandOnKnee(ref armed, s, arm, arm, 0.05f, MLegs | MTorso) + 0.01f;
                        bad += b;
                    }
                    else bad += HandOnKnee(ref armed, s, arm, arm, 0.05f, MLegs | MTorso);
                }
                score += bad * 3f;
                Trace?.AppendLine($"   office recline {recline} drop {drop} shin {shin}: sole {sole:0.000} back gap {backGap:0.000} arms {bad:0.000} -> {score:0.000}");
                if (score < bestScore)
                {
                    bestScore = score;
                    best = armed;
                }
            }
            return best;
        }

        /// <summary>Debug: which body parts touch which solid (index, -1 = floor).</summary>
        static string Why(in CharacterPose p, SeatShape s)
        {
            BodyBoxes.Get(p, Boxes);
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < BodyBoxes.Count; i++)
            {
                var b = Boxes[i];
                for (int c = 0; c < 8; c++)
                    if (b.Point((c & 1) == 0 ? -1f : 1f, (c & 2) == 0 ? -1f : 1f, (c & 4) == 0 ? -1f : 1f).y < Margin) { sb.Append($"{(BodyPart)i}:floor "); break; }
                for (int n = 0; n < s.Solids.Length; n++)
                    if (BodyBoxes.Penetration(b, s.Solids[n].AsBox(Margin)) > 0f) sb.Append($"{(BodyPart)i}:{n} ");
            }
            return sb.ToString();
        }

        static float LowestY(in CharacterPose p, int part)
        {
            BodyBoxes.Get(p, Boxes);
            var b = Boxes[part];
            float y = float.MaxValue;
            for (int c = 0; c < 8; c++)
                y = Mathf.Min(y, b.Point((c & 1) == 0 ? -1f : 1f, (c & 2) == 0 ? -1f : 1f, (c & 4) == 0 ? -1f : 1f).y);
            return y;
        }
    }
}
