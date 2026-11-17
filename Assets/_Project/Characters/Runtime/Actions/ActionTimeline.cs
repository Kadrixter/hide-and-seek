using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Shared timings of the action animations. Third person (<see cref="BlockyCharacterActions"/>) and first person
    /// (<see cref="FirstPersonViewModel"/>) both read these, so sound / blood / damage can be synced to one table.
    /// All values are seconds from the start of the action.
    /// </summary>
    public static class ActionTimeline
    {
        // ------------------------------------------------------------------ knife
        public const float KnifeAttackDuration = 2.4f;

        /// <summary>Moments the blade enters the victim (seeker StabHit events fire here, victim flinches here).</summary>
        public static readonly float[] KnifeStabTimes = { 0.55f, 0.95f, 1.35f, 1.85f };

        /// <summary>Intended distance between the seeker's and the victim's roots (victim faces the seeker).</summary>
        public const float KnifeAttackDistance = 0.85f;

        /// <summary>Victim: stabs, stagger, knees, fall; on the ground (downed pose) at this time.</summary>
        public const float StabbedReactionDuration = 3.2f;

        // ------------------------------------------------------------------ glock
        public const float ReloadDuration = 1.6f;
        public const float ReloadMagOutTime = 0.25f;
        public const float ReloadMagInTime = 0.88f;
        public const float ReloadSlideRackTime = 1.18f;

        // ------------------------------------------------------------------ curves

        static float WindupLead(int i) => i == 3 ? 0.32f : 0.2f;
        static float ThrustTime(int i) => i == 3 ? 0.09f : 0.075f;
        static float HoldTime(int i) => i == 3 ? 0.22f : 0.05f;
        static float ReturnTime(int i) => i == 3 ? 0.18f : 0.12f;

        /// <summary>
        /// Stab hand curve: -1 = cocked back by the hip, 0 = ready (in front), 1 = blade in the belly.
        /// Anticipation ease, very fast accelerating thrust, short hold, pull out.
        /// </summary>
        public static float StabCurve(float t)
        {
            var times = KnifeStabTimes;
            for (int i = 0; i < times.Length; i++)
            {
                float hit = times[i];
                float w = hit - WindupLead(i);
                float c = hit - ThrustTime(i);
                float h = hit + HoldTime(i);
                float r = h + ReturnTime(i);
                if (t < w) return 0f;
                if (t < c) return -SmoothStep01((t - w) / (c - w));
                if (t < hit)
                {
                    float x = (t - c) / (hit - c);
                    return -1f + 2f * x * x;
                }

                if (t < h) return 1f + 0.06f * Mathf.Sin(Mathf.PI * (t - hit) / (h - hit));
                if (t < r) return 1f - SmoothStep01((t - h) / (r - h));
            }

            return 0f;
        }

        /// <summary>Seconds since the most recent stab hit (large when none yet), and its index.</summary>
        public static float SinceLastStab(float t, out int index)
        {
            index = -1;
            float since = 99f;
            for (int i = 0; i < KnifeStabTimes.Length; i++)
            {
                if (t >= KnifeStabTimes[i])
                {
                    index = i;
                    since = t - KnifeStabTimes[i];
                }
            }

            return since;
        }

        /// <summary>Victim curl 0..1: grows with every stab (doubling over).</summary>
        public static float VictimCurl(float t)
        {
            float c = 0.12f * SmoothStep01(t / 0.3f);
            for (int i = 0; i < KnifeStabTimes.Length; i++)
                c += (i == 3 ? 0.3f : 0.18f) * SmoothStep01((t - KnifeStabTimes[i]) / 0.25f);
            return c;
        }

        /// <summary>Victim flinch impulse: sum of sharp pulses peaking ~0.06 s after each stab.</summary>
        public static float VictimFlinch(float t)
        {
            float f = 0f;
            for (int i = 0; i < KnifeStabTimes.Length; i++)
            {
                float x = t - KnifeStabTimes[i];
                if (x < 0f || x > 0.8f) continue;
                const float k = 0.06f;
                f += (x / k) * Mathf.Exp(1f - x / k) * (i == 3 ? 1.25f : 1f);
            }

            return f;
        }

        public static float SmoothStep01(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        public static float EaseIn(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x;
        }

        public static float EaseOut(float x)
        {
            x = Mathf.Clamp01(x);
            return 1f - (1f - x) * (1f - x);
        }

        public static float Remap01(float t, float a, float b) => Mathf.Clamp01((t - a) / (b - a));

        // ------------------------------------------------------------------ reload

        /// <summary>Where the left hand is during a reload: gun-local point, belt point, or a blend of both.</summary>
        public struct ReloadSample
        {
            /// <summary>0 = gun held in the normal hold frame, 1 = tilted reload frame.</summary>
            public float GunTilt;
            /// <summary>Left hand target in gun-local space.</summary>
            public Vector3 LeftGunLocal;
            /// <summary>0 = use LeftGunLocal, 1 = left hand at the belt (fetching a mag).</summary>
            public float LeftAtBelt;
            /// <summary>0..1 slide pulled back.</summary>
            public float SlideBack;
            public bool MagInGun;
            public bool MagInHand;
            /// <summary>Small upward jolt of the gun when the mag is slapped in / the slide released.</summary>
            public float Jolt;
        }

        /// <summary>Left-hand support grip, gun-local (fist centre). Shared by holds and reloads.</summary>
        public static readonly Vector3 SupportGripLocal = new Vector3(-0.15f, -0.075f, -0.03f);

        /// <summary>Left fist position (gun-local) when the new mag is fully seated.</summary>
        public static readonly Vector3 MagInsertHandLocal = new Vector3(0f, -0.25f, -0.03f);

        static readonly Vector3 MagReadyHandLocal = new Vector3(0f, -0.4f, -0.05f);
        static readonly Vector3 SlideGripLocal = new Vector3(-0.02f, 0.2f, -0.03f);
        static readonly Vector3 SlidePulledLocal = new Vector3(-0.03f, 0.21f, -0.17f);

        public static ReloadSample SampleReload(float t)
        {
            var s = new ReloadSample { MagInGun = true };
            // Gun into the tilted reload frame and back.
            s.GunTilt = SmoothStep01(Remap01(t, 0f, 0.2f)) * (1f - SmoothStep01(Remap01(t, 1.3f, 1.55f)));

            // Magazine: released at MagOut, new one in the hand from 0.5, seated at MagIn.
            s.MagInGun = t < ReloadMagOutTime || t >= ReloadMagInTime;
            s.MagInHand = t >= 0.5f && t < ReloadMagInTime;

            // Left hand path.
            if (t < 0.2f)
            {
                s.LeftGunLocal = SupportGripLocal;
                s.LeftAtBelt = 0f;
            }
            else if (t < 0.5f)
            {
                s.LeftGunLocal = SupportGripLocal;
                s.LeftAtBelt = SmoothStep01(Remap01(t, 0.2f, 0.45f));
            }
            else if (t < 0.78f)
            {
                s.LeftGunLocal = MagReadyHandLocal;
                s.LeftAtBelt = 1f - SmoothStep01(Remap01(t, 0.5f, 0.76f));
            }
            else if (t < ReloadMagInTime)
            {
                float x = Remap01(t, 0.78f, ReloadMagInTime);
                s.LeftGunLocal = Vector3.Lerp(MagReadyHandLocal, MagInsertHandLocal, x * x);
            }
            else if (t < 1.04f)
            {
                s.LeftGunLocal = Vector3.Lerp(MagInsertHandLocal, SlideGripLocal, SmoothStep01(Remap01(t, 0.92f, 1.04f)));
            }
            else if (t < ReloadSlideRackTime)
            {
                float x = Remap01(t, 1.06f, ReloadSlideRackTime);
                s.LeftGunLocal = Vector3.Lerp(SlideGripLocal, SlidePulledLocal, SmoothStep01(x));
                s.SlideBack = SmoothStep01(x);
            }
            else
            {
                float x = Remap01(t, ReloadSlideRackTime, 1.45f);
                Vector3 away = SlidePulledLocal + new Vector3(-0.04f, 0.04f, -0.04f);
                s.LeftGunLocal = x < 0.3f
                    ? Vector3.Lerp(SlidePulledLocal, away, x / 0.3f)
                    : Vector3.Lerp(away, SupportGripLocal, SmoothStep01((x - 0.3f) / 0.7f));
                s.SlideBack = 0f;
            }

            float j1 = t - ReloadMagInTime, j2 = t - ReloadSlideRackTime;
            if (j1 >= 0f && j1 < 0.2f) s.Jolt += Mathf.Sin(j1 / 0.2f * Mathf.PI) * (1f - j1 / 0.2f);
            if (j2 >= 0f && j2 < 0.2f) s.Jolt += 0.7f * Mathf.Sin(j2 / 0.2f * Mathf.PI) * (1f - j2 / 0.2f);
            return s;
        }
    }
}
