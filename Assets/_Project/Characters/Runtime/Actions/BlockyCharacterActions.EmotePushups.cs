using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// "Pompki": squat with the hands to the floor, legs back into the plank, three push-ups, the feet step wide and
    /// the left hand goes behind the back, three slower one-arm push-ups (a smug look aside), back to two hands, feet
    /// jump in, stand up and a double biceps flex with two squeezes and a kiss to the right biceps. The stop while
    /// on the floor gets up quickly (crouch, stand).
    /// </summary>
    public partial class BlockyCharacterActions
    {
        const float PushTop = 27f, PushBottom = 9f, PushBottomOne = 15f;

        /// <summary>0 at the top, 1 at the bottom of the rep that started at <paramref name="start"/>.</summary>
        static float PushDepth(float t, float start, int reps, float down)
        {
            float x = t - start;
            if (x < 0f || x >= reps * EmoteTimeline.PushRep) return 0f;
            float f = Mathf.Repeat(x, EmoteTimeline.PushRep);
            return f < down ? Seg(f, 0f, down) : 1f - Seg(f, down, EmoteTimeline.PushRep);
        }

        static PushBody PushBodyAt(float t)
        {
            const float plank = EmoteTimeline.PushPlank, one = EmoteTimeline.PushOneArm, oneEnd = EmoteTimeline.PushOneArmEnd;
            if (t < 0.9f) return PushBody.Lerp(PushStand(), PushSquat(Seg(t, 0.45f, 0.9f)), Seg(t, 0f, 0.55f));
            if (t < plank)
            {
                // Feet jump back into the plank (lifted on the way).
                var k = PushBody.Lerp(PushSquat(1f), PushPlank(PushTop, 0.12f, 0f, false, 0.3f), Seg(t, 0.9f, plank));
                float hop = 0.13f * Hump(t, 0.92f, plank);
                k.FootR.y += hop;
                k.FootL.y += hop;
                return k;
            }
            if (t < 3.7f)
            {
                float d = PushDepth(t, plank, 3, 0.45f);
                var k = PushPlank(Mathf.Lerp(PushTop, PushBottom, d), 0.12f, 0f, false, 0.3f);
                k.Head = Quaternion.Euler(-48f + 10f * d, 0f, 0f);
                return k;
            }
            if (t < one)
            {
                // Feet step wide one by one, the right hand shifts in under the chest, the left one goes behind the back.
                float fr = Seg(t, 3.72f, 3.88f), fl = Seg(t, 3.86f, 4.02f), hr = Seg(t, 3.7f, 3.82f), hl = Seg(t, 3.95f, 4.18f);
                var a = PushPlank(PushTop, 0.12f, 0f, false, 0.3f);
                var b = PushPlank(PushTop, 0.3f, -12f * hl, true, 0.19f);
                var k = a;
                k.Pelvis = b.Pelvis;
                k.Rot = b.Rot;
                k.FootR = Vector3.Lerp(a.FootR, b.FootR, fr) + Vector3.up * 0.07f * Hump(t, 3.72f, 3.88f);
                k.FootL = Vector3.Lerp(a.FootL, b.FootL, fl) + Vector3.up * 0.07f * Hump(t, 3.86f, 4.02f);
                k.HandR = Vector3.Lerp(a.HandR, b.HandR, hr) + Vector3.up * 0.06f * Hump(t, 3.7f, 3.82f);
                k.HandL = Vector3.Lerp(a.HandL, b.HandL, hl) + (Vector3.up * 0.16f + Vector3.left * 0.22f) * Hump(t, 3.95f, 4.18f);
                k.PoleL = Vector3.Lerp(a.PoleL, b.PoleL, hl);
                return k;
            }
            if (t < oneEnd)
            {
                float d = PushDepth(t, one, 3, 0.5f);
                var k = PushPlank(Mathf.Lerp(PushTop, PushBottomOne, d), 0.3f, -12f - 4f * d, true, 0.19f);
                float smug = Env(t, one + 0.2f, one + 0.5f, oneEnd - 0.4f, oneEnd);
                k.Head = Quaternion.Euler(-48f + 10f * d + 14f * smug, 38f * smug, 0f);
                return k;
            }
            if (t < 7.0f)
            {
                // Left hand back down, feet in.
                float hl = Seg(t, oneEnd, 6.8f), f = Seg(t, 6.75f, 6.98f);
                var a = PushPlank(PushTop, 0.3f, -12f, true, 0.19f);
                var b = PushPlank(PushTop, 0.12f, 0f, false, 0.3f);
                var k = PushBody.Lerp(a, b, hl);
                k.FootR = Vector3.Lerp(a.FootR, b.FootR, f) + Vector3.up * 0.06f * Hump(t, 6.75f, 6.98f);
                k.FootL = Vector3.Lerp(a.FootL, b.FootL, f) + Vector3.up * 0.06f * Hump(t, 6.75f, 6.98f);
                k.HandL += (Vector3.up * 0.16f + Vector3.left * 0.22f) * Hump(t, oneEnd, 6.8f);
                return k;
            }
            if (t < 7.5f)
            {
                var k = PushBody.Lerp(PushPlank(PushTop, 0.12f, 0f, false, 0.3f), PushSquat(1f), Seg(t, 7.0f, 7.5f));
                float hop = 0.22f * Hump(t, 7.02f, 7.45f);
                k.FootR.y += hop;
                k.FootL.y += hop;
                return k;
            }
            if (t < EmoteTimeline.PushUp) return PushBody.Lerp(PushSquat(1f - Seg(t, 7.5f, 7.8f)), PushStand(), Seg(t, 7.55f, EmoteTimeline.PushUp));
            float pump = Hump(t, 8.6f, 8.85f) + Hump(t, 8.95f, 9.2f), kiss = Env(t, 9.25f, 9.45f, 9.7f, 9.9f);
            return PushBody.Lerp(PushStand(), PushFlex(pump, kiss), Env(t, EmoteTimeline.PushUp, 8.5f, 9.95f, 10.35f));
        }

        CharacterPose PushupsPose(in CharacterPose b, float t, float stopT)
        {
            if (stopT < 0f)
            {
                var p = PushSolve(b, PushBodyAt(t));
                if (t > EmoteTimeline.PushUp - 0.3f || t < 0.3f) Alive(ref p, t, 0.4f);
                return p;
            }
            float t0 = t - stopT;
            var from = PushBodyAt(t0);
            if (t0 >= EmoteTimeline.PushUp || t0 < 0.45f) return PushSolve(b, PushBody.Lerp(from, PushStand(), Seg(stopT, 0f, 0.6f)));
            // On the floor: up into the squat (feet hop in), then stand.
            var k = stopT < 0.45f ? PushBody.Lerp(from, PushSquat(1f), Seg(stopT, 0f, 0.45f)) : PushBody.Lerp(PushSquat(1f), PushStand(), Seg(stopT, 0.45f, 0.85f));
            if (stopT < 0.45f)
            {
                float hop = 0.12f * Hump(stopT, 0.02f, 0.43f);
                k.FootR.y += hop;
                k.FootL.y += hop;
            }
            return PushSolve(b, k);
        }
    }
}
