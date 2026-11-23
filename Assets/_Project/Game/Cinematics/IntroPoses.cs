using HideAndSeek.Characters;
using UnityEngine;

namespace HideAndSeek.Game.Cinematics
{
    /// <summary>
    /// Hand-made full-body poses of the intro killer, blended over the idle + knife-hold pose of the puppet:
    /// the left hand reaching for the small lever, gripping and yanking it down (IK on the knob, which moves with the
    /// lever), and the laugh (chuckle, head thrown back, shoulders shaking in time with the "ha"s, knife hand up).
    /// All positions in character-root space (+Z forward, +X right, feet at 0).
    /// </summary>
    public static class IntroPoses
    {
        static float S(float t, float a, float b) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, t));

        /// <summary>
        /// Lever pull. <paramref name="t"/> = intro time, <paramref name="knob"/> = knob centre (root space, live),
        /// <paramref name="rest"/> = where the left hand was before the reach.
        /// </summary>
        public static CharacterPose Lever(in CharacterPose cur, float t, Vector3 knob)
        {
            var p = cur;
            float reach = S(t, IntroTimeline.Reach, IntroTimeline.Grip);
            float release = S(t, IntroTimeline.Release, IntroTimeline.Release + 0.35f);
            float pull = S(t, IntroTimeline.PullStart, IntroTimeline.PullEnd);
            float jolt = t > IntroTimeline.PullEnd ? Mathf.Exp(-(t - IntroTimeline.PullEnd) * 9f) : 0f;
            float engage = reach * (1f - release);

            // Lean in towards the box, a little twist to the left, the left shoulder drops on the pull.
            p.Torso = cur.Torso * Quaternion.Euler(8f * engage + 6f * pull * (1f - release) + 3f * jolt, 10f * engage, -5f * pull * (1f - release));
            p.Pelvis = cur.Pelvis * Quaternion.Euler(2f * engage, 4f * engage, 0f);
            p.PelvisPos = cur.PelvisPos + new Vector3(-0.02f, -0.025f * pull * (1f - release) - 0.012f * jolt, 0.02f) * Mathf.Max(engage, 0.0001f);

            // Fist on the knob (slightly behind it, the knob sits in the curled fingers).
            Vector3 rest = cur.HandPos(1);
            Vector3 grip = knob + new Vector3(-0.012f, 0.012f, -0.035f);
            Vector3 hand = Vector3.Lerp(rest, grip, reach);
            // Wind-up before the grip: the hand comes from a little higher (a deliberate, theatrical reach).
            hand += new Vector3(0f, 0.06f, -0.02f) * Mathf.Sin(reach * Mathf.PI) * (1f - pull);
            hand = Vector3.Lerp(hand, rest, release);
            p.SolveArm(1, hand, new Vector3(-0.9f, -0.5f, -0.35f));

            // Head looks at the knob, then up at the gate on the jolt.
            Vector3 neck = p.TorsoPoint(BlockyCharacter.NeckRest) + Vector3.up * 0.2f;
            Vector3 look = Vector3.Lerp(neck + Vector3.forward, knob, engage) - neck;
            float down = -Mathf.Atan2(look.y, Mathf.Max(0.05f, new Vector2(look.x, look.z).magnitude)) * Mathf.Rad2Deg;
            float side = Mathf.Atan2(look.x, Mathf.Max(0.05f, look.z)) * Mathf.Rad2Deg;
            p.Head = Quaternion.Euler(Mathf.Clamp(down * engage - 8f * jolt, -40f, 50f), Mathf.Clamp(side * engage, -60f, 60f), 0f);
            return p;
        }

        /// <summary>Laugh strength 0..1 at <paramref name="t"/> (intro time) and the "ha" bounce (pulses ~6.5 Hz).</summary>
        public static void LaughCurve(float t, out float body, out float bounce, out float big)
        {
            float a = t - IntroTimeline.LaughStart;
            body = S(a, 0f, 0.35f) * (1f - S(a, IntroTimeline.LaughLength - 0.45f, IntroTimeline.LaughLength));
            big = S(a, 0.55f, 0.95f) * (1f - S(a, IntroTimeline.LaughLength - 0.8f, IntroTimeline.LaughLength - 0.2f));
            // Pulses: quick rise, slower fall (shoulders jerk up on each "ha"), slowing down at the end.
            float rate = Mathf.Lerp(6.8f, 5.2f, Mathf.Clamp01(a / IntroTimeline.LaughLength));
            float ph = a * rate;
            float frac = ph - Mathf.Floor(ph);
            bounce = (frac < 0.25f ? frac / 0.25f : 1f - (frac - 0.25f) / 0.75f) * body;
        }

        public static CharacterPose Laugh(in CharacterPose cur, float t)
        {
            LaughCurve(t, out float body, out float bounce, out float big);
            var p = cur;
            if (body <= 0f) return p;
            // Chuckle: hunched, head low, looking up from under the brow. Big laugh: chest out, head thrown back.
            float back = -14f * big + 6f * (1f - big) * body;
            p.Pelvis = cur.Pelvis * Quaternion.Euler(-3f * big, 0f, 0f);
            p.Torso = cur.Torso * Quaternion.Euler(back - 4f * bounce, 0f, 1.5f * Mathf.Sin(t * 13f) * body);
            p.PelvisPos = cur.PelvisPos + new Vector3(0f, 0.012f * bounce - 0.01f * big, -0.03f * big);
            float headPitch = Mathf.Lerp(14f, -32f, big) * body - 7f * bounce;
            p.Head = Quaternion.Euler(headPitch, 6f * Mathf.Sin(t * 2.1f) * body, 4f * Mathf.Sin(t * 3.3f) * big);

            // Left hand on the belly (rising with each "ha"), right (knife) hand lifted out to the side.
            Vector3 belly = p.TorsoPoint(new Vector3(-0.02f, 0.2f, 0.17f)) + Vector3.up * 0.02f * bounce;
            p.SolveArm(1, Vector3.Lerp(cur.HandPos(1), belly, S(t, IntroTimeline.LaughStart, IntroTimeline.LaughStart + 0.4f) * body),
                new Vector3(-1f, -0.4f, 0.2f));
            Vector3 knifeUp = p.TorsoPoint(new Vector3(0.34f, 0.62f + 0.12f * big, 0.18f)) + Vector3.up * 0.03f * bounce;
            Vector3 rhand = Vector3.Lerp(cur.HandPos(0), knifeUp, body * Mathf.Lerp(0.55f, 1f, big));
            p.SolveArm(0, rhand, new Vector3(1f, -0.5f, -0.4f));
            return p;
        }

        /// <summary>
        /// Menacing stance at the start (facing the box, back to the camera): weight back, head low, and a slow look
        /// over the left shoulder at the camera (<paramref name="glance"/> 0..1).
        /// </summary>
        public static CharacterPose Stance(in CharacterPose cur, float t, float glance)
        {
            var p = cur;
            float breathe = Mathf.Sin(t * 2.2f);
            p.Torso = cur.Torso * Quaternion.Euler(5f + breathe * 1.2f, -22f * glance, 0f);
            p.Pelvis = cur.Pelvis * Quaternion.Euler(0f, -6f * glance, 0f);
            p.Head = cur.Head * Quaternion.Euler(Mathf.Lerp(12f, 4f, glance), -62f * glance, -6f * glance);
            return p;
        }
    }
}
