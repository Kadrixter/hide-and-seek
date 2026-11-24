using HideAndSeek.Characters;
using UnityEngine;

namespace HideAndSeek.Game.Cinematics
{
    /// <summary>
    /// Hand-made full-body poses of the intro killer, blended over the idle pose of the puppet: the right hand
    /// reaching for the small lever, gripping and yanking it down (IK on the knob, which moves with the lever), the
    /// harder yank that tears the handle off (recoil, he looks at it in his fist), and the laugh (chuckle, head thrown
    /// back, shoulders shaking in time with the "ha"s, the torn handle raised and shaken in the right hand).
    /// All positions in character-root space (+Z forward, +X right, feet at 0).
    /// </summary>
    public static class IntroPoses
    {
        static float S(float t, float a, float b) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, t));

        /// <summary>
        /// Lever pull with the right hand and the handle torn off. <paramref name="t"/> = intro time,
        /// <paramref name="knob"/> = knob centre (root space, live, moves with the lever until the snap),
        /// <paramref name="snapKnob"/> = where the knob was when it tore off (root space).
        /// </summary>
        public static CharacterPose Lever(in CharacterPose cur, float t, Vector3 knob, Vector3 snapKnob)
        {
            var p = cur;
            float reach = S(t, IntroTimeline.Reach, IntroTimeline.Grip);
            float pull = S(t, IntroTimeline.PullStart, IntroTimeline.PullEnd);
            float force = S(t, IntroTimeline.PullEnd + 0.04f, IntroTimeline.Snap);
            bool torn = t >= IntroTimeline.Snap;
            float jolt = torn ? Mathf.Exp(-(t - IntroTimeline.Snap) * 9f) : 0f;
            // After the snap the fist comes up in front of the right shoulder: he looks at what is in it.
            float show = S(t, IntroTimeline.Snap + 0.04f, IntroTimeline.Snap + 0.38f);
            float down = (pull + 0.6f * force) * (1f - show);
            float shake = (Mathf.Sin(t * 47f) * 0.6f + Mathf.Sin(t * 31f) * 0.4f) * force * (1f - show);

            // Lean in towards the box, a little twist to the right, the right shoulder drops on the pull; the body
            // jerks back on the snap.
            p.Torso = cur.Torso * Quaternion.Euler(8f * reach + 6f * down + 1.2f * shake - 12f * jolt, -10f * reach * (1f - show), 5f * down);
            p.Pelvis = cur.Pelvis * Quaternion.Euler(2f * reach, -4f * reach * (1f - show), 0f);
            p.PelvisPos = cur.PelvisPos + new Vector3(0.02f * reach, -0.025f * down - 0.012f * jolt, 0.02f * reach - 0.05f * jolt);

            // Fist on the knob (slightly behind it, the knob sits in the curled fingers).
            Vector3 rest = cur.HandPos(0);
            Vector3 hand;
            if (!torn)
            {
                Vector3 grip = knob + new Vector3(0.012f, 0.012f, -0.035f);
                hand = Vector3.Lerp(rest, grip, reach);
                // Wind-up before the grip: the hand comes from a little higher (a deliberate, theatrical reach).
                hand += new Vector3(0f, 0.06f, -0.02f) * Mathf.Sin(reach * Mathf.PI) * (1f - pull);
                hand += new Vector3(0f, 0.006f, 0f) * shake;
            }
            else
            {
                Vector3 from = snapKnob + new Vector3(0.012f, 0.012f, -0.035f);
                Vector3 held = p.TorsoPoint(new Vector3(0.3f, 0.7f, 0.28f)) + new Vector3(0f, 0.012f * Mathf.Sin(t * 6.5f), 0.008f * Mathf.Sin(t * 4.1f)) * show;
                // Recoil (the arm flies back and out when the rod gives), then up in front of the face.
                hand = Vector3.Lerp(from, held, show) + new Vector3(0.12f, 0.06f, -0.14f) * Mathf.Sin(Mathf.Clamp01((t - IntroTimeline.Snap) / 0.3f) * Mathf.PI);
            }
            p.SolveArm(0, hand, new Vector3(0.12f, -1f, -0.15f)); // elbow straight down (pulls downwards), the arm in profile

            // Head looks at the knob, then at the torn handle in the fist.
            Vector3 neck = p.TorsoPoint(BlockyCharacter.NeckRest) + Vector3.up * 0.2f;
            Vector3 target = torn ? Vector3.Lerp(knob, hand, show) : knob;
            Vector3 look = Vector3.Lerp(neck + Vector3.forward, target, reach) - neck;
            float pitch = -Mathf.Atan2(look.y, Mathf.Max(0.05f, new Vector2(look.x, look.z).magnitude)) * Mathf.Rad2Deg;
            float side = Mathf.Atan2(look.x, Mathf.Max(0.05f, look.z)) * Mathf.Rad2Deg;
            p.Head = Quaternion.Euler(Mathf.Clamp(pitch * reach - 10f * jolt, -40f, 50f), Mathf.Clamp(side * reach, -60f, 60f), 6f * show);
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

            // Left hand on the belly (rising with each "ha"), the right hand with the torn handle lifted out to the
            // side like a trophy and shaken on the "ha"s.
            Vector3 belly = p.TorsoPoint(new Vector3(-0.02f, 0.2f, 0.17f)) + Vector3.up * 0.02f * bounce;
            p.SolveArm(1, Vector3.Lerp(cur.HandPos(1), belly, S(t, IntroTimeline.LaughStart, IntroTimeline.LaughStart + 0.4f) * body),
                new Vector3(-1f, -0.4f, 0.2f));
            Vector3 trophy = p.TorsoPoint(new Vector3(0.42f, 0.56f + 0.1f * big, 0.3f)) + Vector3.up * 0.06f * bounce
                + new Vector3(0.035f * Mathf.Sin(t * 19f), 0f, 0.02f * Mathf.Sin(t * 13f)) * big;
            Vector3 rhand = Vector3.Lerp(cur.HandPos(0), trophy, body * Mathf.Lerp(0.7f, 1f, big));
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
