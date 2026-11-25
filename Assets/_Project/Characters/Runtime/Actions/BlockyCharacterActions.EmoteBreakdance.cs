using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Breakdance (loop, body on the floor): toprock with a cross step and the drop to a crouch (way in), then a loop:
    /// roll onto the upper back, two fast back-spin turns round the shoulder blades (knees tucked, smoother-step speed),
    /// sit up into the crouch and kick up into a one-hand freeze (right hand on the floor, legs bent in the air, held
    /// with a tiny wobble), back to the crouch. The stop gets up from the moment it was asked (crouch, stand).
    /// </summary>
    public partial class BlockyCharacterActions
    {
        static readonly Vector3 BreakBackAt = new Vector3(0f, 0.06f, -0.18f);

        static CharacterPose BreakCrouch(in CharacterPose b)
        {
            var p = Stance(b);
            p.PelvisPos = new Vector3(0f, 0.33f, -0.1f);
            p.Torso = Quaternion.Euler(42f, 0f, 0f);
            p.Head = Quaternion.Euler(-28f, 0f, 0f);
            FeetAt(ref p, new Vector3(0.18f, 0f, 0.1f), new Vector3(-0.18f, 0f, 0.06f), new Vector3(0f, 0f, 1f));
            p.SolveArm(0, new Vector3(0.24f, 0.11f, 0.42f), p.TorsoRoot * new Vector3(1f, 0f, -0.4f));
            Vector3 knee = BodyBoxes.KneePos(p, 1);
            p.SolveArm(1, knee + new Vector3(-0.04f, 0.14f, 0.08f), p.TorsoRoot * new Vector3(-1f, -0.4f, -0.4f));
            return p;
        }

        /// <summary>On the upper back, turned <paramref name="spin"/> degrees round the shoulder blades, knees tucked.</summary>
        static CharacterPose BreakBack(in CharacterPose b, float spin, float open)
        {
            var p = Stance(b);
            Quaternion rot = Quaternion.Euler(0f, spin, 0f) * Quaternion.Euler(-100f, 0f, 0f);
            p.Pelvis = rot;
            p.PelvisPos = BreakBackAt - rot * new Vector3(0f, 0.46f, -0.13f);
            p.Head = Quaternion.Euler(45f, 0f, 0f);
            // Legs: tucked to the chest, opening into a V with <paramref name="open"/>.
            for (int leg = 0; leg < 2; leg++)
            {
                float sx = leg == 0 ? 1f : -1f;
                Vector3 local = Vector3.Lerp(new Vector3(0.12f * sx, -0.12f, 0.33f), new Vector3(0.42f * sx, -0.25f, 0.5f), open);
                p.SolveLeg(leg, p.PelvisPos + rot * local, rot * new Vector3(0.2f * sx, 0.4f, 1f));
            }
            for (int arm = 0; arm < 2; arm++)
            {
                float sx = arm == 0 ? 1f : -1f;
                Vector3 local = Vector3.Lerp(new Vector3(0.2f * sx, 0.1f, 0.4f), new Vector3(0.55f * sx, 0.55f, 0.1f), open);
                p.SolveArm(arm, p.PelvisPos + rot * local, rot * new Vector3(sx, 0f, 0.3f));
            }
            return p;
        }

        /// <summary>
        /// One-hand freeze ("air chair"): the whole body held up on the right hand planted behind the hip, leaning back
        /// and to the right, knees bent up in front, the left arm thrown up.
        /// </summary>
        static CharacterPose BreakFreeze(in CharacterPose b, float wob)
        {
            var p = Stance(b);
            Quaternion rot = Quaternion.Euler(0f, 18f, 0f) * Quaternion.Euler(-52f + 2f * wob, 0f, 0f) * Quaternion.Euler(0f, 0f, -24f);
            Vector3 hand = new Vector3(0.34f, 0.11f, -0.2f);
            Vector3 shoulder = hand + new Vector3(-0.04f, 0.47f, 0.07f);
            p.Pelvis = rot;
            p.PelvisPos = shoulder - rot * BlockyCharacter.ShoulderRightRest;
            p.Head = Quaternion.Euler(28f, -20f, 10f);
            p.SolveArm(0, hand, rot * new Vector3(0.2f, 0f, -1f));
            p.SolveArm(1, p.PelvisPos + rot * new Vector3(-0.5f, 1.0f, 0.15f), rot * new Vector3(-1f, -0.3f, -0.2f));
            for (int leg = 0; leg < 2; leg++)
            {
                float sx = leg == 0 ? 1f : -1f;
                Vector3 sole = p.PelvisPos + rot * new Vector3(0.16f * sx, -0.18f + 0.03f * wob, 0.5f + 0.06f * sx);
                p.SolveLeg(leg, sole, rot * new Vector3(0.3f * sx, 0.6f, 1f));
            }
            return p;
        }

        CharacterPose BreakToprock(in CharacterPose b, float t)
        {
            var p = Stance(b);
            float bounce = 0.5f + 0.5f * Mathf.Cos(2f * Mathf.PI * t / 0.4f);
            float cross = Env(t, 0.15f, 0.3f, 0.45f, 0.62f);
            float arms = 0.5f + 0.5f * Mathf.Cos(2f * Mathf.PI * t / 0.4f);
            p.PelvisPos += new Vector3(0f, -0.03f * bounce - 0.02f, 0f);
            p.Torso = Quaternion.Euler(6f, 18f * Mathf.Sin(Mathf.PI * t / 0.4f), 0f);
            p.Head = Quaternion.Euler(-6f + 6f * bounce, 0f, 0f);
            Vector3 fr = Vector3.Lerp(FootR, new Vector3(-0.08f, 0f, 0.2f), cross) + Vector3.up * 0.07f * (Hump(t, 0.15f, 0.32f) + Hump(t, 0.45f, 0.62f));
            Feet(ref p, fr, FootL);
            for (int arm = 0; arm < 2; arm++)
            {
                float sx = arm == 0 ? 1f : -1f;
                Arm(ref p, arm, Vector3.Lerp(new Vector3(0.16f * sx, 0.45f, 0.33f), new Vector3(0.48f * sx, 0.55f, 0.22f), arms), new Vector3(sx, -0.6f, -0.4f));
            }
            return p;
        }

        CharacterPose BreakLoop(in CharacterPose b, float t)
        {
            if (t < EmoteTimeline.BreakIn)
            {
                var top = BreakToprock(b, t);
                return t < 0.8f ? top : CharacterPose.Lerp(top, BreakCrouch(b), Seg(t, 0.8f, EmoteTimeline.BreakIn));
            }
            float lt = Mathf.Repeat(t - EmoteTimeline.BreakIn, EmoteTimeline.BreakPeriod);
            const float s0 = EmoteTimeline.BreakSpinStart, s1 = EmoteTimeline.BreakSpinEnd;
            const float f0 = EmoteTimeline.BreakFreezeIn, f1 = EmoteTimeline.BreakFreezeOut;
            if (lt < s0) return CharacterPose.Lerp(BreakCrouch(b), BreakBack(b, 0f, 0f), Seg(lt, 0f, s0));
            if (lt < s1)
            {
                float open = Env(lt, 1.0f, 1.35f, 1.7f, 2.05f);
                return BreakBack(b, 720f * Smoother(lt, s0, s1), open);
            }
            float wob = Mathf.Sin(lt * 7f);
            if (lt < s1 + 0.45f) return CharacterPose.Lerp(BreakBack(b, 0f, 0f), BreakCrouch(b), Seg(lt, s1, s1 + 0.45f));
            if (lt < f0) return CharacterPose.Lerp(BreakCrouch(b), BreakFreeze(b, 0f), Seg(lt, s1 + 0.45f, f0));
            if (lt < f1) return BreakFreeze(b, wob * Seg(lt, f0, f0 + 0.3f));
            return CharacterPose.Lerp(BreakFreeze(b, wob), BreakCrouch(b), Seg(lt, f1, EmoteTimeline.BreakPeriod));
        }

        CharacterPose BreakdancePose(in CharacterPose b, float t, float stopT)
        {
            if (stopT < 0f) return BreakLoop(b, t);
            var from = BreakLoop(b, t - stopT);
            // Stand up over the crouch's feet (no sliding), the layer fade brings them home.
            var stand = Stance(b);
            Feet(ref stand, new Vector3(0.18f, 0f, 0.1f), new Vector3(-0.18f, 0f, 0.06f));
            if (stopT < 0.55f) return CharacterPose.Lerp(from, BreakCrouch(b), Seg(stopT, 0f, 0.55f));
            return CharacterPose.Lerp(BreakCrouch(b), stand, Seg(stopT, 0.55f, 1.0f));
        }
    }
}
