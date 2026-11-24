using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Shared clock (s) of working on a fuse box socket (third person, first person, the fuse, sounds, server).
    /// Putting one in (one E press): the fuse comes up in the right fist to the socket (<see cref="InReach"/>), is lined
    /// up and pushed home (<see cref="InClick"/> - click, the socket counts as filled), a short press, the hand goes back.
    /// Taking one out (E held): the fist closes on the cap (<see cref="OutGrip"/>), two tugs, it pops out at
    /// <see cref="OutPop"/> (the socket counts as empty) and is pulled back to the chest.
    /// "Out" = how far the fuse sticks out of its seated position along the socket axis (m).
    /// </summary>
    public static class FuseTimeline
    {
        public const float InReach = 0.42f, InClick = 0.74f, InRelease = 0.9f, InEnd = 1.25f;
        public const float OutReach = 0.4f, OutGrip = 0.5f, OutPop = 1.5f, OutEnd = 1.9f;
        public static readonly float[] Tugs = { 0.8f, 1.15f };
        /// <summary>Lined up in front of the socket before the push / how far a popped fuse comes out.</summary>
        public const float Approach = 0.13f, Popped = 0.26f;
        /// <summary>The fist holds the fuse this far in front of its centre (its cap).</summary>
        public const float FistOnCap = 0.1f;

        public static float S(float t, float a, float b) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, t));

        public static float Duration(bool remove) => remove ? OutEnd : InEnd;
        public static bool Running(float t, bool remove) => t >= 0f && t < Duration(remove);

        /// <summary>The fuse sits on the socket axis (true) or rides in the fist (false, the hand is away from the socket).</summary>
        public static bool OnSocketLine(float t, bool remove) => remove ? t < OutPop + 0.12f : t >= InReach;

        /// <summary>Out distance of the fuse while it is on the socket axis.</summary>
        public static float Out(float t, bool remove)
        {
            if (!remove) return Mathf.Lerp(Approach, 0f, S(t, InReach, InClick - 0.02f));
            if (t < OutGrip) return 0f;
            if (t < OutPop)
            {
                // Two tugs that loosen it a little more each time, a fine wiggle in between.
                float o = 0.004f + 0.012f * S(t, OutGrip, OutPop);
                foreach (float k in Tugs) o += 0.012f * Mathf.Exp(-Mathf.Pow((t - k) / 0.07f, 2f));
                return o + 0.0015f * Mathf.Sin(t * 47f);
            }
            return Mathf.Lerp(0.03f, Popped, S(t, OutPop, OutPop + 0.12f));
        }

        /// <summary>How much the right arm is on the job (0 = its own pose, 1 = on the fuse).</summary>
        public static float Reach(float t, bool remove)
        {
            if (!remove) return S(t, 0f, InReach * 0.8f) * (1f - S(t, InRelease, InEnd));
            return S(t, 0f, OutReach) * (1f - S(t, OutPop + 0.1f, OutEnd));
        }

        /// <summary>Left hand flat on the box frame, steadying the body.</summary>
        public static float Brace(float t, bool remove) => remove
            ? S(t, 0.1f, OutReach + 0.1f) * (1f - S(t, OutPop, OutPop + 0.25f))
            : S(t, 0.15f, InReach + 0.1f) * (1f - S(t, InClick + 0.05f, InEnd - 0.1f));

        /// <summary>Body lean / weight over the whole action.</summary>
        public static float Body(float t, bool remove) => S(t, 0f, 0.3f) * (1f - S(t, Duration(remove) - 0.3f, Duration(remove)));

        /// <summary>Little extra push of the fist into the socket right at the click (m).</summary>
        public static float Press(float t) => t > InClick - 0.04f && t < InRelease ? 0.008f * Mathf.Sin((t - InClick + 0.04f) / (InRelease - InClick + 0.04f) * Mathf.PI) : 0f;
    }
}
