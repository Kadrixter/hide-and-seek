using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Pure pose math of the body carry, in the carrier's root space and canonical layout (the carried's head on the
    /// carrier's left, his legs over the right forearm). The mirrored layout is the same pose reflected (<see cref="Mirror"/>).
    /// </summary>
    public static class BodyCarryPoses
    {
        const float Px = BlockyCharacter.PixelSize;
        const float Deg = Mathf.Deg2Rad;

        // ================================================================== mirror / frames

        static Quaternion M(Quaternion q) => new Quaternion(q.x, -q.y, -q.z, q.w);
        public static Vector3 M(Vector3 v) => new Vector3(-v.x, v.y, v.z);

        /// <summary>The pose reflected across the character's YZ plane (left and right swapped).</summary>
        public static CharacterPose Mirror(in CharacterPose a)
        {
            var p = a;
            p.PelvisPos = M(a.PelvisPos);
            p.Pelvis = M(a.Pelvis);
            p.Torso = M(a.Torso);
            p.Head = M(a.Head);
            p.ShoulderR = M(a.ShoulderL);
            p.ShoulderL = M(a.ShoulderR);
            p.ElbowR = M(a.ElbowL);
            p.ElbowL = M(a.ElbowR);
            p.HipR = M(a.HipL);
            p.HipL = M(a.HipR);
            p.KneeR = M(a.KneeL);
            p.KneeL = M(a.KneeR);
            return p;
        }

        /// <summary>A root-space pose of a body whose root stands at (pos, rot) in the world, in another root's space.</summary>
        public static CharacterPose Rebase(in CharacterPose a, Vector3 fromPos, Quaternion fromRot, Vector3 toPos, Quaternion toRot)
        {
            var p = a;
            Quaternion inv = Quaternion.Inverse(toRot);
            p.PelvisPos = inv * (fromPos + fromRot * a.PelvisPos - toPos);
            p.Pelvis = inv * fromRot * a.Pelvis;
            return p;
        }

        static Quaternion Frame(Vector3 up, Vector3 belly)
        {
            up.Normalize();
            belly = Vector3.ProjectOnPlane(belly, up);
            if (belly.sqrMagnitude < 1e-6f) belly = Vector3.ProjectOnPlane(Vector3.up, up);
            return Quaternion.LookRotation(belly.normalized, up);
        }

        static Vector3 Dir(float deg) => new Vector3(Mathf.Cos(deg * Deg), Mathf.Sin(deg * Deg), 0f);

        // ================================================================== carried (canonical, head on -X)

        /// <summary>Shape of the body lying across the arms.</summary>
        public struct Cradle
        {
            public Vector3 Pelvis;
            public float Rise;      // spine above the horizontal (deg), towards -X
            public float Roll;      // belly turned from straight up towards the carrier (deg)
            public float Curl;      // spine flexion (deg)
            public float Thigh;     // thighs above the horizontal (deg), towards +X
            public float Bend;      // knee bend (deg)
            public float Swing;     // extra shin swing (deg, + = forward / up)

            public static Cradle Lerp(in Cradle a, in Cradle b, float t) => new Cradle
            {
                Pelvis = Vector3.LerpUnclamped(a.Pelvis, b.Pelvis, t),
                Rise = Mathf.LerpUnclamped(a.Rise, b.Rise, t),
                Roll = Mathf.LerpUnclamped(a.Roll, b.Roll, t),
                Curl = Mathf.LerpUnclamped(a.Curl, b.Curl, t),
                Thigh = Mathf.LerpUnclamped(a.Thigh, b.Thigh, t),
                Bend = Mathf.LerpUnclamped(a.Bend, b.Bend, t),
                Swing = Mathf.LerpUnclamped(a.Swing, b.Swing, t),
            };
        }

        /// <summary>Held high in the arms (the carry loop, before bounce / breathing).</summary>
        public static readonly Cradle Hold = new Cradle
        { Pelvis = new Vector3(0.1f, 0.88f, 0.45f), Rise = 13f, Roll = 16f, Curl = 9f, Thigh = 10f, Bend = 96f };

        /// <summary>Half way up (lift) / down (put down).</summary>
        public static readonly Cradle Mid = new Cradle
        { Pelvis = new Vector3(0.08f, 0.6f, 0.44f), Rise = 12f, Roll = 14f, Curl = 12f, Thigh = 16f, Bend = 90f };

        /// <summary>Just off the floor: back on the left forearm, knees up over the right one.</summary>
        public static readonly Cradle Low = new Cradle
        { Pelvis = new Vector3(0.06f, 0.26f, 0.46f), Rise = 9f, Roll = 10f, Curl = 12f, Thigh = 30f, Bend = 92f };

        /// <summary>Body + legs of a cradle (arms resting on the belly, head neutral).</summary>
        public static CharacterPose CradlePose(in Cradle c, float legSway = 0f)
        {
            var p = CharacterPose.Identity;
            p.PelvisPos = c.Pelvis;
            Vector3 up = new Vector3(-Mathf.Cos(c.Rise * Deg), Mathf.Sin(c.Rise * Deg), 0f);
            Vector3 belly = new Vector3(0f, Mathf.Cos(c.Roll * Deg), -Mathf.Sin(c.Roll * Deg));
            p.Pelvis = Frame(up, belly);
            p.Torso = Quaternion.Euler(c.Curl, 0f, 0f);
            p.Head = Quaternion.Euler(8f, 0f, 0f);
            for (int leg = 0; leg < 2; leg++)
            {
                // The far leg (left, +Z) hangs a little lower and swings out of phase.
                float extra = leg == 1 ? 6f : 0f;
                float sway = legSway * (leg == 0 ? 1f : -0.8f);
                Vector3 hip = p.HipPos(leg);
                Vector3 thigh = Dir(c.Thigh - extra * 0.5f);
                Vector3 knee = hip + thigh * RigIK.Thigh;
                Vector3 shin = Dir(c.Thigh - c.Bend - extra + c.Swing + sway);
                shin.z += leg == 1 ? 0.06f : -0.04f;
                p.SolveLeg(leg, knee + shin.normalized * RigIK.Shin, thigh + Vector3.up * 0.8f);
            }
            RestArms(ref p, 0f);
            return p;
        }

        /// <summary>Both hands on the own belly (the wound), elbows out.</summary>
        public static void RestArms(ref CharacterPose p, float breath)
        {
            p.SolveArm(0, p.TorsoPoint(new Vector3(0.07f, 0.23f + 0.006f * breath, 0.17f)), p.TorsoRoot * new Vector3(0.45f, -1f, -0.2f));
            p.SolveArm(1, p.TorsoPoint(new Vector3(-0.09f, 0.19f, 0.17f)), p.TorsoRoot * new Vector3(-1f, -0.4f, -0.2f));
        }

        /// <summary>On the side facing the carrier, knees drawn up a little (being rolled over during the lift).</summary>
        public static CharacterPose SideFacing(Vector3 pelvis)
        {
            var p = CharacterPose.Identity;
            p.PelvisPos = new Vector3(pelvis.x, 0.25f, pelvis.z);
            p.Pelvis = Frame(new Vector3(-1f, 0.06f, 0f), new Vector3(0f, 0.25f, -1f));
            p.Torso = Quaternion.Euler(18f, 0f, 0f);
            p.Head = Quaternion.Euler(20f, 0f, 0f);
            for (int leg = 0; leg < 2; leg++)
            {
                Vector3 hip = p.HipPos(leg);
                Vector3 knee = hip + new Vector3(0.26f, 0f, -0.2f + (leg == 1 ? 0.04f : 0f));
                p.SolveLeg(leg, new Vector3(knee.x + 0.3f, 0.11f + (leg == 1 ? 0.12f : 0f), knee.z + 0.05f), new Vector3(0.3f, 0f, -1f));
            }
            p.SolveArm(0, new Vector3(pelvis.x - 0.38f, 0.1f, pelvis.z - 0.2f), new Vector3(0f, 1f, 0.3f));
            p.SolveArm(1, p.TorsoPoint(new Vector3(-0.05f, 0.25f, 0.2f)), new Vector3(0f, 1f, 0f));
            return p;
        }

        /// <summary>Tipped back into the left arm, knees giving (lift of a standing player, first beat).</summary>
        public static CharacterPose LeanBack(Vector3 rootLocal, float lean)
        {
            var p = CharacterPose.Identity;
            float k = lean / 28f;
            p.PelvisPos = new Vector3(rootLocal.x - 0.05f * k, 0.6f, rootLocal.z - 0.04f * k);
            Vector3 up = new Vector3(-Mathf.Sin(lean * Deg), Mathf.Cos(lean * Deg), 0f);
            p.Pelvis = Frame(up, new Vector3(1f, 0f, -0.25f * k));
            p.Torso = Quaternion.Euler(10f * k, 0f, 0f);
            p.Head = Quaternion.Euler(10f, -20f * k, 0f);
            for (int leg = 0; leg < 2; leg++)
            {
                Vector3 hip = p.HipPos(leg);
                p.SolveLeg(leg, new Vector3(hip.x + 0.12f * k, 0.05f + 0.07f * k * (leg == 0 ? 1f : 0.3f), hip.z), new Vector3(1f, 0f, 0f));
            }
            p.SolveArm(0, p.TorsoPoint(new Vector3(0.24f, 0.5f, 0.12f)), p.TorsoRoot * new Vector3(0.5f, -1f, -0.5f));
            p.SolveArm(1, p.TorsoPoint(new Vector3(-0.2f, 0.25f, 0.16f)), p.TorsoRoot * new Vector3(-1f, -0.5f, 0f));
            return p;
        }

        /// <summary>Lying on the back, head along the root's +Z, hands on the belly (root space of the end frame).</summary>
        public static CharacterPose SupineRest()
        {
            var p = CharacterPose.Identity;
            p.PelvisPos = new Vector3(0f, 0.13f, -BodyCarryTimeline.LyingPelvisBack);
            p.Pelvis = Quaternion.LookRotation(Vector3.up, Vector3.forward);
            p.Torso = Quaternion.Euler(-4f, 0f, 3f);
            p.Head = Quaternion.Euler(-6f, 28f, 0f);
            p.SolveArm(0, p.TorsoPoint(new Vector3(0.08f, 0.22f, 0.15f)), Vector3.down + p.TorsoRoot * Vector3.right);
            p.SolveArm(1, p.TorsoPoint(new Vector3(-0.1f, 0.17f, 0.15f)), Vector3.down + p.TorsoRoot * Vector3.left);
            Vector3 hr = p.HipPos(0), hl = p.HipPos(1);
            p.SolveLeg(0, new Vector3(hr.x + 0.05f, 0.11f, hr.z - 0.64f), Vector3.up);
            p.SolveLeg(1, new Vector3(hl.x - 0.1f, 0.11f, hl.z - 0.6f), Vector3.up + Vector3.left * 0.6f);
            return p;
        }

        /// <summary>Standing upright, arms down (root space of the end frame); the live locomotion takes over after.</summary>
        public static CharacterPose StandRest()
        {
            var p = CharacterPose.Identity;
            p.ShoulderR = Quaternion.Euler(0f, 0f, 4f);
            p.ShoulderL = Quaternion.Euler(0f, 0f, -4f);
            p.ElbowR = p.ElbowL = Quaternion.Euler(-8f, 0f, 0f);
            return p;
        }

        // ================================================================== carried details (head, arms)

        /// <summary>Head turned towards <paramref name="dir"/> (root space) within the neck's limits.</summary>
        public static Quaternion HeadToward(in CharacterPose p, Vector3 dir, float maxYaw = 75f, float minPitch = -50f, float maxPitch = 55f)
        {
            Vector3 d = Quaternion.Inverse(p.TorsoRoot) * dir;
            if (d.sqrMagnitude < 1e-6f) return Quaternion.identity;
            d.Normalize();
            float yaw = Mathf.Clamp(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, -maxYaw, maxYaw);
            float pitch = Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(d.y, -1f, 1f)) * Mathf.Rad2Deg, minPitch, maxPitch);
            return Quaternion.Euler(pitch, yaw, 0f);
        }

        // ================================================================== carrier (canonical)

        /// <summary>On the right knee, left foot planted forward, bent over the body on the floor.</summary>
        public static CharacterPose Kneel(float lean)
        {
            var p = CharacterPose.Identity;
            p.PelvisPos = new Vector3(0f, 0.4f, -0.08f);
            p.Pelvis = Quaternion.Euler(10f, 0f, 0f);
            p.Torso = Quaternion.Euler(lean, 0f, 0f);
            p.Head = Quaternion.Euler(18f, 0f, 0f);
            p.SolveLeg(1, new Vector3(-0.15f, 0.02f, 0.26f), new Vector3(0f, 0.4f, 1f));
            p.SolveLeg(0, new Vector3(0.13f, 0.08f, -0.42f), new Vector3(0.1f, -0.6f, 1f));
            ReachArms(ref p);
            return p;
        }

        /// <summary>Bent down at the hips and knees, reaching for the floor in front.</summary>
        public static CharacterPose Stoop()
        {
            var p = CharacterPose.Identity;
            p.PelvisPos = new Vector3(0f, 0.56f, -0.1f);
            p.Pelvis = Quaternion.Euler(14f, 0f, 0f);
            p.Torso = Quaternion.Euler(42f, 0f, 0f);
            p.Head = Quaternion.Euler(12f, 0f, 0f);
            p.SolveLeg(1, new Vector3(-0.14f, 0.02f, 0.16f), new Vector3(0f, 0f, 1f));
            p.SolveLeg(0, new Vector3(0.14f, 0.02f, -0.12f), new Vector3(0f, 0f, 1f));
            ReachArms(ref p);
            return p;
        }

        /// <summary>Rising with the weight: knees bent, feet planted, back straightening.</summary>
        public static CharacterPose Rise(float depth)
        {
            var p = CharacterPose.Identity;
            p.PelvisPos = new Vector3(0f, Mathf.Lerp(0.66f, 0.53f, depth), -0.06f);
            p.Pelvis = Quaternion.Euler(8f * depth, 0f, 0f);
            p.Torso = Quaternion.Euler(22f * depth, 0f, 0f);
            p.Head = Quaternion.Euler(6f, 0f, 0f);
            p.SolveLeg(1, new Vector3(-0.14f, 0.02f, 0.1f), new Vector3(0f, 0f, 1f));
            p.SolveLeg(0, new Vector3(0.14f, 0.02f, -0.08f), new Vector3(0f, 0f, 1f));
            ReachArms(ref p);
            return p;
        }

        /// <summary>Knees bent, upright (lifting / setting down someone who stands).</summary>
        public static CharacterPose Dip(float depth)
        {
            var p = CharacterPose.Identity;
            p.PelvisPos = new Vector3(0f, BlockyCharacter.HipHeight - 0.13f * depth, -0.04f * depth);
            p.Pelvis = Quaternion.Euler(6f * depth, 0f, 0f);
            p.Torso = Quaternion.Euler(14f * depth, 0f, 0f);
            p.Head = Quaternion.Euler(8f, 0f, 0f);
            p.SolveLeg(1, new Vector3(-0.13f, 0.02f, 0.08f), new Vector3(0f, 0f, 1f));
            p.SolveLeg(0, new Vector3(0.13f, 0.02f, -0.04f), new Vector3(0f, 0f, 1f));
            ReachArms(ref p);
            return p;
        }

        /// <summary>Startled when hit: arms thrown open, twisted, leaning back.</summary>
        public static CharacterPose Stagger(in CharacterPose basePose)
        {
            var p = basePose;
            p.PelvisPos += new Vector3(0f, -0.04f, -0.05f);
            p.Torso = p.Torso * Quaternion.Euler(-14f, 18f, 8f);
            p.Head = Quaternion.Euler(-18f, -14f, 6f);
            p.SolveArm(0, p.TorsoPoint(new Vector3(0.5f, 0.55f, 0.28f)), p.TorsoRoot * new Vector3(1f, -0.5f, -0.5f));
            p.SolveArm(1, p.TorsoPoint(new Vector3(-0.5f, 0.62f, 0.22f)), p.TorsoRoot * new Vector3(-1f, -0.5f, -0.5f));
            return p;
        }

        static void ReachArms(ref CharacterPose p)
        {
            p.SolveArm(0, p.TorsoPoint(new Vector3(0.22f, 0.2f, 0.42f)), p.TorsoRoot * new Vector3(0.6f, -1f, 0f));
            p.SolveArm(1, p.TorsoPoint(new Vector3(-0.22f, 0.2f, 0.42f)), p.TorsoRoot * new Vector3(-0.6f, -1f, 0f));
        }

        // ================================================================== contacts (carried canonical pose -> carrier hands)

        /// <summary>Under the carried's knees (right fist), on the leg(s) that are still there.</summary>
        public static Vector3 UnderKnees(in CharacterPose c, int severedLegs)
        {
            Vector3 sum = Vector3.zero;
            float w = 0f;
            for (int leg = 0; leg < 2; leg++)
            {
                if ((severedLegs & (1 << leg)) != 0) continue;
                Quaternion thigh = c.Pelvis * (leg == 0 ? c.HipR : c.HipL);
                Vector3 hip = c.HipPos(leg);
                Vector3 knee = hip + thigh * BlockyCharacter.KneeRest;
                Vector3 pt = Vector3.Lerp(hip, knee, 0.7f) + thigh * Vector3.back * (4.3f * Px);
                float k = leg == 1 ? 1.4f : 1f; // fist towards the far leg
                sum += pt * k;
                w += k;
            }
            if (w <= 0f) return c.TorsoPoint(new Vector3(0f, -0.05f, -4.3f * Px)); // no legs: under the hips
            return sum / w;
        }

        /// <summary>Under the carried's upper back (left fist).</summary>
        public static Vector3 UnderBack(in CharacterPose c) => c.TorsoPoint(new Vector3(-0.05f, 0.3f, -4.2f * Px));
    }
}
