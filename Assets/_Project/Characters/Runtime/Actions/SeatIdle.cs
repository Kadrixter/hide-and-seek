using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Life on top of a seated pose, so nobody sits frozen: breathing (chest and shoulders), a slow shift of the weight
    /// in the spine, the head looking round now and then (only while the player leaves the look alone), and per pose a
    /// few small habits: a bouncing heel, a dangling foot swinging, a hand drumming on what it rests on, now and then
    /// a hand going up to the hair. Everything is a pure function of the time and a per-character seed (no state), so
    /// every client shows the same kind of motion, different characters move out of step, and the pose wheel preview /
    /// editor renders get the same motion. Only local joints of the limbs and the spine turn: the pelvis (and with it
    /// the contact with the seat and the floor) stays put. The own first-person body skips everything that would move
    /// the camera (spine, head, hand to the hair).
    /// </summary>
    public static class SeatIdle
    {
        struct Habits
        {
            public float Breath, Sway, LookYaw, LookPitch;
            /// <summary>Leg whose heel bounces (foot on the floor), leg whose dangling foot swings, arm that drums, arm that goes to the hair (-1 = none).</summary>
            public int Bounce, Swing, Drum, Hair;
            public float SwingAmp;
        }

        static Habits For(int variant)
        {
            switch (variant)
            {
                case 0: return new Habits { Breath = 1.1f, Sway = 1.6f, LookYaw = 32f, LookPitch = 8f, Bounce = -1, Swing = -1, Drum = 1, Hair = 0 };
                case 1: return new Habits { Breath = 1.3f, Sway = 1.0f, LookYaw = 34f, LookPitch = 7f, Bounce = -1, Swing = -1, Drum = 0, Hair = 1 };
                case 2: return new Habits { Breath = 0.6f, Sway = 0.6f, LookYaw = 26f, LookPitch = 9f, Bounce = -1, Swing = -1, Drum = -1, Hair = -1 };
                case 3: return new Habits { Breath = 1.1f, Sway = 1.3f, LookYaw = 30f, LookPitch = 8f, Bounce = -1, Swing = -1, Drum = 0, Hair = -1 };
                case 4: return new Habits { Breath = 1.2f, Sway = 0.8f, LookYaw = 24f, LookPitch = 6f, Bounce = -1, Swing = -1, SwingAmp = 5f, Drum = 1, Hair = -1 };
                case 5: return new Habits { Breath = 1.4f, Sway = 0.6f, LookYaw = 28f, LookPitch = 10f, Bounce = -1, Swing = 1, SwingAmp = 4f, Drum = 1, Hair = -1 };
                case 6: return new Habits { Breath = 1.0f, Sway = 0.8f, LookYaw = 28f, LookPitch = 7f, Bounce = -1, Swing = 0, SwingAmp = 9f, Drum = -1, Hair = -1 };
                case 7: return new Habits { Breath = 1.2f, Sway = 1.0f, LookYaw = 30f, LookPitch = 8f, Bounce = -1, Swing = -1, Drum = 0, Hair = 0 };
                case 8: return new Habits { Breath = 1.1f, Sway = 1.4f, LookYaw = 30f, LookPitch = 7f, Bounce = -1, Swing = 0, SwingAmp = 7f, Drum = 1, Hair = 0 };
                case 9: return new Habits { Breath = 1.1f, Sway = 1.6f, LookYaw = 28f, LookPitch = 8f, Bounce = 0, Swing = -1, Drum = 0, Hair = 0 };
                case 10: return new Habits { Breath = 1.3f, Sway = 0.8f, LookYaw = 30f, LookPitch = 8f, Bounce = -1, Swing = 0, SwingAmp = 5f, Drum = -1, Hair = -1 };
                case 11: return new Habits { Breath = 1.1f, Sway = 1.5f, LookYaw = 28f, LookPitch = 7f, Bounce = 0, Swing = -1, Drum = -1, Hair = 1 };
                default: return new Habits { Breath = 1f, Sway = 1f, LookYaw = 25f, LookPitch = 6f, Bounce = -1, Swing = -1, Drum = -1, Hair = -1 };
            }
        }

        // ------------------------------------------------------------------ noise helpers

        static float Hash(int seed, int k)
        {
            unchecked
            {
                uint h = (uint)seed * 374761393u + (uint)k * 668265263u;
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }

        static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

        /// <summary>
        /// 0..1 envelope of an occasional habit: once per slot of <paramref name="slot"/> s (random start), lasting
        /// <paramref name="len"/> s with <paramref name="fade"/> s ease in / out; <paramref name="chance"/> of slots have one.
        /// </summary>
        static float Burst(float t, int seed, int salt, float slot, float len, float fade, float chance, out float local)
        {
            int k = Mathf.FloorToInt(t / slot);
            local = 0f;
            if (Hash(seed, k * 31 + salt) > chance) return 0f;
            float start = k * slot + Hash(seed, k * 31 + salt + 7) * (slot - len);
            local = t - start;
            if (local < 0f || local > len) return 0f;
            return Smooth(local / fade) * Smooth((len - local) / fade);
        }

        /// <summary>Gaze that wanders: holds a direction for a few seconds, turns to the next (sometimes back to the middle).</summary>
        static float Wander(float t, int seed, int salt, float range)
        {
            float slot = 3.4f + Hash(seed, salt) * 2.2f;
            int k = Mathf.FloorToInt(t / slot);
            float a = Target(seed, salt, k - 1, range), b = Target(seed, salt, k, range);
            float turn = 0.7f + Hash(seed, k * 13 + salt) * 0.5f;
            float local = t - k * slot;
            return Mathf.Lerp(a, b, Smooth(local / turn)) + (Mathf.PerlinNoise(t * 0.35f + salt, seed * 0.013f) - 0.5f) * range * 0.12f;
        }

        static float Target(int seed, int salt, int k, float range)
        {
            float h = Hash(seed, k * 17 + salt);
            return h < 0.3f ? 0f : (Hash(seed, k * 17 + salt + 3) * 2f - 1f) * range;
        }

        // ------------------------------------------------------------------ public

        /// <summary>Head look offsets (yaw, pitch up +, degrees) while the player leaves the look alone (weighted by the caller).</summary>
        public static Vector2 LookAround(int variant, float t, int seed)
        {
            var h = For(variant);
            return new Vector2(Wander(t, seed, 11, h.LookYaw), Wander(t, seed, 29, h.LookPitch) - h.LookPitch * 0.2f);
        }

        /// <summary>
        /// Adds the idle motion to a seated pose (any space: local joints only). <paramref name="firstPerson"/> = the own
        /// body seen from its eyes: nothing that would move the camera.
        /// </summary>
        public static void Apply(int variant, SeatShape shape, ref CharacterPose p, float t, int seed, bool firstPerson)
        {
            var h = For(variant);
            if (shape != null && shape.MirrorSource != null)
            {
                // Mirrored seat: the pose's limbs are swapped.
                h.Bounce = Swap(h.Bounce); h.Swing = Swap(h.Swing); h.Drum = Swap(h.Drum); h.Hair = Swap(h.Hair);
            }
            float tt = t + Hash(seed, 1) * 40f;
            if (!firstPerson)
            {
                // Breathing: the chest lifts and opens (the arms ride along, so resting hands stay where they rest).
                float period = 3.9f + Hash(seed, 2) * 0.9f;
                float br = Mathf.Sin(tt * 2f * Mathf.PI / period);
                br = br > 0f ? br : br * 0.8f;
                p.Torso *= Quaternion.Euler(-0.8f * h.Breath * br, 0f, 0f);
                // Weight shift in the spine (two slow waves).
                float w1 = Mathf.Sin(tt * 2f * Mathf.PI / (10.5f + Hash(seed, 3) * 4f));
                float w2 = Mathf.Sin(tt * 2f * Mathf.PI / (16f + Hash(seed, 4) * 5f) + 1.3f);
                // Small: the arms ride along with the spine, resting fists must not sink into what they rest on.
                p.Torso *= Quaternion.Euler(0.25f * h.Sway * w2, 0.5f * h.Sway * w1, 0.55f * h.Sway * (0.7f * w1 + 0.4f * w2));
            }
            if (h.Bounce >= 0)
            {
                // Heel bouncing (nervous leg): the thigh lifts the foot a few centimetres, quickly, in bursts.
                float e = Burst(tt, seed, 41, 9f + Hash(seed, 5) * 4f, 3.2f + Hash(seed, 6) * 1.5f, 0.35f, 0.75f, out float l);
                if (e > 0f)
                {
                    float b = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(l * 2f * Mathf.PI * 2.4f)), 2f) * e;
                    if (h.Bounce == 0) { p.HipR *= Quaternion.Euler(-3.2f * b, 0f, 0f); p.KneeR *= Quaternion.Euler(2f * b, 0f, 0f); }
                    else { p.HipL *= Quaternion.Euler(-3.2f * b, 0f, 0f); p.KneeL *= Quaternion.Euler(2f * b, 0f, 0f); }
                }
            }
            if (h.Swing >= 0)
            {
                // Dangling foot swinging back and forth, now and then.
                float e = 0.25f + 0.75f * Burst(tt, seed, 53, 8f + Hash(seed, 7) * 4f, 5f + Hash(seed, 8) * 2f, 1.2f, 0.85f, out _);
                // Only ever straightening a little from the pose (never folding into the leg it rests on).
                float sw = -(0.5f + 0.5f * Mathf.Sin(tt * 2f * Mathf.PI * (0.75f + Hash(seed, 9) * 0.3f))) * h.SwingAmp * e;
                if (h.Swing == 0) p.KneeR *= Quaternion.Euler(sw, 0f, 0f);
                else p.KneeL *= Quaternion.Euler(sw, 0f, 0f);
            }
            if (h.Drum >= 0)
            {
                // Hand drumming on the thigh / the arm roll: the fist taps quickly a few times.
                float e = Burst(tt, seed, 67, 11f + Hash(seed, 10) * 5f, 1.8f + Hash(seed, 11) * 1.2f, 0.2f, 0.8f, out float l);
                if (e > 0f)
                {
                    float d = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(l * 2f * Mathf.PI * 3.3f)), 4f) * e;
                    if (h.Drum == 0) p.ElbowR *= Quaternion.Euler(-7f * d, 0f, 0f);
                    else p.ElbowL *= Quaternion.Euler(-7f * d, 0f, 0f);
                }
            }
            if (h.Hair >= 0 && !firstPerson)
            {
                // Now and then a hand goes up to the hair behind the ear, strokes it and comes back down.
                const float len = 2.8f;
                float e = Burst(tt, seed, 79, 17f + Hash(seed, 12) * 9f, len, 0.75f, 0.7f, out float l);
                if (e > 0f) HandToHair(ref p, h.Hair, e, l);
            }
        }

        static int Swap(int limb) => limb < 0 ? limb : 1 - limb;

        static void HandToHair(ref CharacterPose p, int arm, float w, float local)
        {
            float side = arm == 0 ? 1f : -1f;
            Quaternion head = p.TorsoRoot * p.Head;
            Vector3 neck = p.TorsoPoint(BlockyCharacter.NeckRest);
            // Beside the head above the ear, a little behind; it strokes back and down while there.
            float stroke = Mathf.Sin(local * 2f * Mathf.PI * 0.8f) * 0.025f;
            Vector3 target = neck + head * new Vector3(side * 0.33f, 0.3f + stroke, -0.05f - stroke);
            var q = p;
            q.SolveArm(arm, target, p.TorsoRoot * new Vector3(side, 0.15f, -0.5f));
            if (arm == 0)
            {
                p.ShoulderR = Quaternion.Slerp(p.ShoulderR, q.ShoulderR, w);
                p.ElbowR = Quaternion.Slerp(p.ElbowR, q.ElbowR, w);
            }
            else
            {
                p.ShoulderL = Quaternion.Slerp(p.ShoulderL, q.ShoulderL, w);
                p.ElbowL = Quaternion.Slerp(p.ElbowL, q.ElbowL, w);
            }
        }
    }
}
