using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>How the hand meets a lobby object (<see cref="BlockyCharacterActions.PlayReach"/>).</summary>
    public enum ReachStyle : byte
    {
        /// <summary>Flick a switch / button: quick reach, two fingers press, back.</summary>
        Press,
        /// <summary>Spin a globe: hand lands on the side and sweeps across.</summary>
        Sweep,
        /// <summary>Feed the fish: hand above the tank, wrist shakes a few times.</summary>
        Sprinkle,
        /// <summary>Lower the tone arm / turn a knob: slow careful reach, hold, back.</summary>
        Careful,
        /// <summary>Dart throw: wind-up next to the head, snap forward, follow-through.</summary>
        Throw,
    }

    /// <summary>
    /// Lobby mini interactions: the right arm reaches for a point in the world with an in / hold / out timeline,
    /// the torso leans towards low targets, the head looks at the hand. Breathing keeps going underneath.
    /// </summary>
    public partial class BlockyCharacterActions
    {
        float reachT = 99f, reachDur = 1f;
        Vector3 reachLocal;
        ReachStyle reachStyle;

        public static float ReachDuration(ReachStyle s) => s switch
        {
            ReachStyle.Press => 0.85f,
            ReachStyle.Sweep => 1.1f,
            ReachStyle.Sprinkle => 1.9f,
            ReachStyle.Careful => 1.5f,
            _ => 0.95f,
        };

        /// <summary>Right hand goes to <paramref name="world"/> (for <see cref="ReachStyle.Throw"/>: the target it throws at).</summary>
        public void PlayReach(Vector3 world, ReachStyle style)
        {
            EnsureInit();
            reachT = 0f;
            reachStyle = style;
            reachDur = ReachDuration(style);
            reachLocal = transform.InverseTransformPoint(world);
        }

        public bool Reaching => reachT < reachDur;

        /// <summary>Hand target (character space) and arm weight at time t of a reach - shared with the first-person view.</summary>
        public static void ReachCurve(ReachStyle style, float t, Vector3 target, Vector3 shoulder, out Vector3 hand, out float weight)
        {
            float dur = ReachDuration(style);
            float u = Mathf.Clamp01(t / dur);
            Vector3 toT = target - shoulder;
            float len = toT.magnitude;
            // never further than the arm (0.62 m) - the IK would lock the elbow straight
            Vector3 reachable = len > 0.6f ? shoulder + toT / len * 0.6f : target;
            Vector3 rest = shoulder + new Vector3(0.05f, -0.45f, 0.18f);
            switch (style)
            {
                case ReachStyle.Throw:
                {
                    // 0-0.38 wind-up by the ear, 0.38-0.5 snap, 0.5-1 follow-through and back.
                    Vector3 cock = shoulder + new Vector3(0.02f, 0.12f, -0.02f);
                    Vector3 fwd = shoulder + (toT.sqrMagnitude > 0.001f ? toT.normalized : Vector3.forward) * 0.58f;
                    Vector3 low = shoulder + new Vector3(-0.08f, -0.35f, 0.35f);
                    if (u < 0.38f) hand = Vector3.Lerp(rest, cock, Smooth(u / 0.38f));
                    else if (u < 0.5f) hand = Vector3.Lerp(cock, fwd, Smooth((u - 0.38f) / 0.12f));
                    else if (u < 0.72f) hand = Vector3.Lerp(fwd, low, Smooth((u - 0.5f) / 0.22f));
                    else hand = Vector3.Lerp(low, rest, Smooth((u - 0.72f) / 0.28f));
                    weight = u < 0.12f ? Smooth(u / 0.12f) : u > 0.85f ? Smooth((1f - u) / 0.15f) : 1f;
                    return;
                }
                case ReachStyle.Sprinkle:
                {
                    Vector3 above = reachable + new Vector3(0f, 0.08f, -0.03f);
                    float inT = Smooth(u / 0.3f), outT = Smooth((u - 0.78f) / 0.22f);
                    hand = Vector3.Lerp(rest, above, inT);
                    if (u > 0.3f && u < 0.78f) hand += new Vector3(0f, Mathf.Sin((u - 0.3f) * 52f) * 0.018f, 0f);
                    hand = Vector3.Lerp(hand, rest, outT);
                    weight = 1f - outT * outT;
                    weight *= Smooth(u / 0.12f);
                    return;
                }
                case ReachStyle.Sweep:
                {
                    Vector3 side = new Vector3(0.12f, 0f, 0f);
                    float inT = Smooth(u / 0.38f), sweep = Smooth((u - 0.38f) / 0.2f), outT = Smooth((u - 0.62f) / 0.38f);
                    hand = Vector3.Lerp(rest, reachable + side, inT);
                    hand = Vector3.Lerp(hand, reachable - side * 1.6f, sweep);
                    hand = Vector3.Lerp(hand, rest, outT);
                    weight = Smooth(u / 0.12f) * (1f - outT * outT);
                    return;
                }
                default:
                {
                    // Press / Careful: in, small push at the target, hold, out.
                    float inEnd = style == ReachStyle.Careful ? 0.4f : 0.35f, holdEnd = style == ReachStyle.Careful ? 0.68f : 0.55f;
                    float inT = Smooth(u / inEnd), outT = Smooth((u - holdEnd) / (1f - holdEnd));
                    Vector3 push = toT.sqrMagnitude > 0.001f ? toT.normalized * 0.025f : Vector3.zero;
                    float press = u > inEnd && u < holdEnd ? Mathf.Sin((u - inEnd) / (holdEnd - inEnd) * Mathf.PI) : 0f;
                    hand = Vector3.Lerp(Vector3.Lerp(rest, reachable - push, inT) + push * press, rest, outT);
                    weight = Smooth(u / 0.15f) * (1f - outT * outT);
                    return;
                }
            }
        }

        static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        void ApplyReach(ref CharacterPose p, float dt, bool standing)
        {
            if (reachT >= reachDur) return;
            reachT += dt;
            if (!standing) { reachT = reachDur; return; }
            var ap = p;
            // Lean towards low / far targets (record player, fireplace knob), never more than 28 degrees.
            float lean = Mathf.Clamp(Mathf.Atan2(1.25f - reachLocal.y, Mathf.Max(0.35f, reachLocal.z)) * Mathf.Rad2Deg * 0.45f, -8f, 28f);
            if (reachStyle == ReachStyle.Throw) lean = -4f + 10f * Mathf.Clamp01((reachT / reachDur - 0.38f) * 4f);
            Vector3 shoulder = ap.ShoulderPos(0);
            ReachCurve(reachStyle, reachT, reachLocal, shoulder, out var hand, out float w);
            ap.Torso = p.Torso * Quaternion.Euler(lean, reachStyle == ReachStyle.Throw ? -12f + 24f * Mathf.Clamp01((reachT / reachDur - 0.3f) * 3f) : 0f, 0f);
            // re-solve from the leaned shoulder so the hand still lands on the target
            shoulder = ap.ShoulderPos(0);
            ReachCurve(reachStyle, reachT, reachLocal, shoulder, out hand, out w);
            ap.SolveArm(0, hand, TorsoDir(ap, 1f, -0.8f, -0.3f));
            CharacterPose.LerpArm(ref p, ap, 0, w);
            p.Torso = Quaternion.Slerp(p.Torso, ap.Torso, w);
            // look at the hand / target
            Vector3 look = (reachStyle == ReachStyle.Throw ? reachLocal : hand) - new Vector3(0f, 1.6f, 0f);
            float pitch = Mathf.Clamp(-Mathf.Atan2(look.y, Mathf.Max(0.2f, new Vector2(look.x, look.z).magnitude)) * Mathf.Rad2Deg, -30f, 40f);
            float yaw = Mathf.Clamp(Mathf.Atan2(look.x, Mathf.Max(0.2f, look.z)) * Mathf.Rad2Deg, -35f, 35f);
            p.Head = Quaternion.Slerp(p.Head, Quaternion.Euler(pitch * 0.7f, yaw * 0.6f, 0f), w * 0.8f);
        }
    }
}
