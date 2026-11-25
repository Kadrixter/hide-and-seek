using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Body placement of the bear hug for both roles, in each role's own space (see the pair frame), as pure functions
    /// of the shared clock: either body can compute the other one exactly (hands on the partner's back during a fast
    /// spin need no one-frame-old transforms). Role 0 lifts: dips, straightens with the partner (role 1) pulled up
    /// against the chest, turns <see cref="EmoteTimeline.BearTurns"/> times round a pivot between the two (smoother
    /// step: speeds up, slows down), sets the partner down.
    /// </summary>
    public partial class BlockyCharacterActions
    {
        /// <summary>Pelvis placement + torso rotation of a body in own space.</summary>
        struct BodyState
        {
            public Vector3 Pos;
            public Quaternion Rot, Torso;

            /// <summary>Torso-space point in own space.</summary>
            public Vector3 Point(Vector3 local) => Pos + Rot * (Torso * local);
        }

        const float BearLiftH = 0.38f, BearPull = 0.32f, BearPivot = 0.17f, BearSideOff = 0.2f;

        /// <summary>0 -> 1 lift (partner up against the chest), back to 0 on the set-down.</summary>
        static float BearLiftW(float t) => Env(t, EmoteTimeline.BearLift, EmoteTimeline.BearUp, EmoteTimeline.BearSpinEnd, EmoteTimeline.BearDown);

        /// <summary>Turn angle round the pivot (degrees, + = clockwise from above).</summary>
        static float BearAngle(float t) => 360f * EmoteTimeline.BearTurns * Smoother(t, EmoteTimeline.BearUp, EmoteTimeline.BearSpinEnd);

        /// <summary>Turn speed 0..1 of the peak (legs fly out with its square).</summary>
        static float BearSpeed(float t)
        {
            float x = Mathf.Clamp01((t - EmoteTimeline.BearUp) / (EmoteTimeline.BearSpinEnd - EmoteTimeline.BearUp));
            return 16f * x * x * (1f - x) * (1f - x);
        }

        /// <summary>Hug closeness 0..1 (bodies in, arms round), out after the set-down.</summary>
        static float BearHold(float t) => Env(t, 0.9f, 1.5f, EmoteTimeline.BearLet, EmoteTimeline.BearLet + 0.4f);

        static BodyState BearState(bool lifter, float t)
        {
            float hold = BearHold(t), lift = BearLiftW(t);
            float dip = Hump(t, EmoteTimeline.BearDip, EmoteTimeline.BearUp - 0.1f)
                        + 0.6f * Hump(t, EmoteTimeline.BearSpinEnd, EmoteTimeline.BearDown + 0.25f);
            float rock = Mathf.Sin((t - 1.5f) * 2f * Mathf.PI * 0.9f) * Env(t, 1.5f, 1.7f, 2.4f, 2.6f);
            float hip = BlockyCharacter.HipHeight - 0.005f;
            var s = new BodyState();
            if (lifter)
            {
                float lean = -3f * Env(t, 0f, 0.4f, 0.8f, 1.0f) + 8f * hold + 8f * Hump(t, EmoteTimeline.BearDip, EmoteTimeline.BearLift + 0.2f) - 19f * lift;
                s.Pos = new Vector3(-BearSideOff * hold, hip - 0.12f * dip, 0.05f * hold + 0.05f * lift);
                s.Rot = Quaternion.Euler(0f, 0f, 3f * rock);
                s.Torso = Quaternion.Euler(lean, 0f, 10f * hold - 2f * rock);
            }
            else
            {
                float lean = -3f * Env(t, 0f, 0.4f, 0.8f, 1.0f) + 8f * hold * (1f - lift) - 4f * lift;
                s.Pos = new Vector3(-BearSideOff * hold, hip + BearLiftH * lift - 0.03f * dip * (1f - lift), 0.05f * hold + (BearPull - 0.05f) * lift);
                s.Rot = Quaternion.Euler(0f, 0f, -3f * rock);
                s.Torso = Quaternion.Euler(lean, 0f, 10f * hold * (1f - lift) + 2f * rock);
            }
            // The spin: both bodies round the same pivot (own space: the lifter's is in front, the partner's behind).
            float ang = BearAngle(t);
            if (ang > 0.001f)
            {
                var q = Quaternion.Euler(0f, ang, 0f);
                Vector3 piv = new Vector3(0f, 0f, lifter ? BearPivot : EmoteTimeline.BearDistance - BearPivot);
                s.Pos = piv + q * (s.Pos - piv);
                s.Rot = q * s.Rot;
            }
            return s;
        }

        /// <summary>Lifter's sole in own space: planted, stepping round the pivot during the turns.</summary>
        static Vector3 BearLifterFoot(int leg, float t)
        {
            Vector3 local = leg == 0 ? new Vector3(0.13f, 0f, 0.04f) : new Vector3(-0.13f, 0f, 0.0f);
            StepPhase(BearAngle(t), 40f, leg, out float a, out float lift);
            var piv = new Vector3(0f, 0f, BearPivot);
            return piv + Quaternion.Euler(0f, a, 0f) * (local - piv) + Vector3.up * 0.06f * lift;
        }
    }
}
