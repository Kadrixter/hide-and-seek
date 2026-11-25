using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// "Na barana" poses. Carrier (role 0): half turn, crouch with the hands ready behind, catch the rider's knees,
    /// stand up and walk small circles (stepping feet, bobbing, more and more bent and out of breath). Rider (role 1):
    /// crouch, jump in an arc onto the back, legs round the carrier's waist, one hand on the carrier's shoulder, the
    /// other one swinging an imaginary lasso overhead. The stop (from the pose frozen at that moment): the carrier walks
    /// home with the rider still on, the rider hops off backwards onto the own spot, the carrier turns round.
    /// </summary>
    public partial class BlockyCharacterActions
    {
        CharacterPose PiggybackPose(in CharacterPose b, float t, float stopT)
        {
            var fr = Frame();
            float t0 = stopT >= 0f ? t - stopT : t;
            return fr.Lead ? PiggyCarrierPose(b, fr, t0, t, stopT) : PiggyRiderPose(b, fr, t0, t, stopT);
        }

        static void PlacePair(ref CharacterPose p, in PairFrame fr, in BodyState s)
        {
            p.PelvisPos = fr.Point(s.Pos);
            p.Pelvis = fr.Rot * s.Rot;
            p.Torso = s.Torso;
        }

        CharacterPose PiggyCarrierPose(in CharacterPose b, in PairFrame fr, float t, float now, float stopT)
        {
            bool exit = stopT >= 0f;
            var s = exit ? PiggyCarrierExit(t, stopT, out _, out _) : PiggyCarrier(t);
            var r = exit ? PiggyRiderExit(t, stopT, out _) : PiggyRider(t);
            float tired = PiggyTired(t) * (exit ? 1f - Seg(stopT, 0.7f, 1.2f) : 1f);
            var p = Stance(b);
            PlacePair(ref p, fr, s);
            float look = Env(t, 0.75f, 1.0f, 1.9f, 2.2f);
            p.Head = Quaternion.Euler(-6f * look + 14f * tired, 65f * look, 0f);

            for (int leg = 0; leg < 2; leg++)
            {
                float fy;
                Vector3 foot = exit ? PiggyCarrierExitFoot(leg, t, stopT, out fy) : PiggyCarrierFoot(leg, t, out fy);
                Vector3 fwd = fr.Rot * (Quaternion.Euler(0f, fy, 0f) * Vector3.forward);
                p.SolveLeg(leg, fr.Point(foot), fwd + Vector3.Cross(Vector3.up, fwd) * (leg == 0 ? 0.12f : -0.12f));
            }
            Alive(ref p, now, 0.3f);

            float ready = Env(t, 0.6f, 0.95f, 1.75f, 2.0f);
            float catchW = Seg(t, 1.85f, 2.15f) * (exit ? 1f - Seg(stopT, 0.7f, 0.95f) : 1f);
            for (int arm = 0; arm < 2; arm++)
            {
                float sx = arm == 0 ? 1f : -1f;
                Vector3 hand = p.TorsoPoint(arm == 0 ? RestR : RestL);
                Vector3 behind = p.TorsoPoint(new Vector3(0.3f * sx, 0.12f + 0.06f * Hump(Mathf.Repeat(t, 0.5f), 0f, 0.5f) * (arm == 0 ? 1f : 0f), -0.24f));
                hand = Vector3.Lerp(hand, behind, ready);
                Vector3 knee = r.Pos + r.Rot * new Vector3(0.46f * sx, -0.28f, 0.2f);
                hand = Vector3.Lerp(hand, fr.Point(knee), catchW);
                p.SolveArm(arm, hand, p.TorsoRoot * new Vector3(sx, -0.3f, 0.6f));
            }
            return p;
        }

        CharacterPose PiggyRiderPose(in CharacterPose b, in PairFrame fr, float t, float now, float stopT)
        {
            bool exit = stopT >= 0f;
            float hop = 0f;
            var r = exit ? PiggyRiderExit(t, stopT, out hop) : PiggyRider(t);
            var c = exit ? PiggyCarrierExit(t, Mathf.Min(stopT, 0.7f), out _, out _) : PiggyCarrier(t);
            var p = Stance(b);
            float riding = Seg(t, EmoteTimeline.PiggyJump + 0.25f, EmoteTimeline.PiggyLand) * (exit ? 1f - Seg(stopT, 0.7f, 0.9f) : 1f);
            PlacePair(ref p, fr, r);
            float lasso = Seg(t, 2.5f, 2.9f) * (exit ? 1f - Seg(stopT, 0f, 0.25f) : 1f);
            p.Head = Quaternion.Euler(-10f * lasso + 6f * Hump(t, 1.1f, 1.6f), 25f * Mathf.Sin(t * 0.7f) * lasso, 0f);

            float tuck = Hump(t, EmoteTimeline.PiggyJump, EmoteTimeline.PiggyLand) + hop;
            float kick = Hump(Mathf.Repeat(t, 2.4f), 1.7f, 2.05f) * Seg(t, 3f, 3.5f) * (exit ? 0f : 1f);
            for (int leg = 0; leg < 2; leg++)
            {
                float sx = leg == 0 ? 1f : -1f;
                Vector3 hip = r.Pos + r.Rot * (leg == 0 ? BlockyCharacter.HipRightRest : BlockyCharacter.HipLeftRest);
                Vector3 own = new Vector3(0.13f * sx, 0f, 0.01f * sx);
                Vector3 planted = new Vector3(0f, 0f, PiggyHalf) + Quaternion.Euler(0f, 180f, 0f) * own;
                Vector3 tucked = hip + r.Rot * new Vector3(0.05f * sx, -0.42f, 0.12f);
                Vector3 straddle = r.Pos + r.Rot * new Vector3(0.48f * sx, -0.34f, 0.16f - 0.12f * kick);
                Vector3 sole = Vector3.Lerp(planted, tucked, Mathf.Clamp01(tuck * 1.2f));
                sole = Vector3.Lerp(sole, straddle, riding);
                if (exit && stopT > 1.05f) sole = planted;
                Vector3 pole = fr.Rot * (r.Rot * Vector3.Lerp(new Vector3(0.1f * sx, 0f, 1f), new Vector3(1.2f * sx, 0f, 1f), riding));
                p.SolveLeg(leg, fr.Point(sole), pole);
            }
            Alive(ref p, now, 0.3f);

            float prep = Env(t, 1.15f, 1.45f, 1.6f, 1.75f);
            float hold = Seg(t, 1.85f, 2.1f) * (exit ? 1f - Seg(stopT, 0.65f, 0.85f) : 1f);
            for (int arm = 0; arm < 2; arm++)
            {
                float sx = arm == 0 ? 1f : -1f;
                Vector3 hand = p.TorsoPoint(arm == 0 ? RestR : RestL);
                hand = Vector3.Lerp(hand, p.TorsoPoint(new Vector3(0.34f * sx, 0.1f, -0.25f)), prep);
                hand = Vector3.Lerp(hand, p.TorsoPoint(new Vector3(0.3f * sx, 0.75f, 0.32f)), Hump(t, 1.6f, 2.0f));
                Vector3 shoulder = c.Pos + c.Rot * (c.Torso * new Vector3(0.3f * sx, 0.68f, 0.0f)) + Vector3.up * 0.12f;
                hand = Vector3.Lerp(hand, fr.Point(shoulder), hold);
                if (arm == 0)
                {
                    float a = now * 2f * Mathf.PI * 1.1f;
                    Vector3 rope = p.TorsoPoint(new Vector3(0.42f + 0.06f * Mathf.Cos(a), 1.0f, 0.06f + 0.13f * Mathf.Sin(a)));
                    hand = Vector3.Lerp(hand, rope, lasso);
                }
                p.SolveArm(arm, hand, p.TorsoRoot * new Vector3(sx, -0.5f, -0.3f));
            }
            return p;
        }
    }
}
