using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Legs against a wall behind the body (crawl, downed, the fall to the floor, kneeling on all fours): when a wall is
    /// closer than a leg reaches, the shin is raised from the knee, and lying flat the knee itself comes in (the thigh
    /// lifts), so no boot shows on the other side of the wall. Runs on every peer for every character (remote players
    /// included): one sphere sweep per leg from the root to the boot, only while a leg reaches far behind the root. The
    /// owner's motor already keeps the lying body out of walls (PlayerMotor.BodyFootprint.cs); this is the visual safety
    /// net for what it cannot see (splayed crawl kicks, network lag, a turn, a fall, a cupboard).
    /// </summary>
    public partial class BlockyCharacterActions
    {
        /// <summary>Half thickness of a limb box (2 px plus the clothing layer) with a hair of margin.</summary>
        const float LegBoxHalf = 0.13f;

        /// <summary>Gap kept between the folded legs and the wall.</summary>
        const float LegWallGap = 0.01f;

        /// <summary>A leg reaching less than this behind the root never needs a check (walking strides stay inside).</summary>
        const float LegCheckReach = 0.5f;

        /// <summary>
        /// Sweep height above the feet, sphere radius and how far the sweep aims outside the boot: the sphere covers the
        /// whole leg box, also kicked out to the side in the crawl, and stays off the floor.
        /// </summary>
        const float LegSweepY = 0.2f, LegSweepR = 0.14f, LegSweepOut = 0.1f;

        /// <summary>Unfolding speed when the wall is gone (metres of allowed reach per second).</summary>
        const float LegUnfoldSpeed = 1.5f;

        const float NoWall = -9f;

        readonly PartBox[] legBoxes = new PartBox[BodyBoxes.Count];
        readonly float[] legWallZ = { NoWall, NoWall };
        Transform legSelf;

        /// <summary>The own player (the object with the character controller) whose colliders are never a wall.</summary>
        Transform LegSelf
        {
            get
            {
                if (legSelf == null)
                {
                    var cc = GetComponentInParent<CharacterController>(true);
                    legSelf = cc != null ? cc.transform : transform;
                }
                return legSelf;
            }
        }

        /// <summary>Free room behind the root for the legs (metres; 99 = no wall), for tests.</summary>
        public float LegRoomBehind => Mathf.Max(legWallZ[0], legWallZ[1]) <= NoWall + 0.01f ? 99f : -Mathf.Max(legWallZ[0], legWallZ[1]);

        /// <summary>1 while a leg is folded against a wall, for tests.</summary>
        public float LegFoldWeight { get; private set; }

        /// <summary>Applied to the final pose right before it is written to the rig.</summary>
        void ApplyLegRoom(ref CharacterPose p, float dt)
        {
            bool on = BodyRoom.Enabled && !IsCarriedBody && !IsSettlingAfterCarry;
            if (on) BodyBoxes.Get(p, legBoxes);
            bool any = false;
            for (int leg = 0; leg < 2; leg++)
            {
                float reach = on ? LegBack(leg) : 0f;
                float target = reach > LegCheckReach ? LegWallAlong(p, leg) : NoWall;
                // A wall shows up: fold at once (a boot through it for a few frames is what must not be seen); gone: unfold smoothly.
                legWallZ[leg] = target > legWallZ[leg] ? target : Mathf.MoveTowards(legWallZ[leg], target, dt * LegUnfoldSpeed);
                any |= on && -reach < legWallZ[leg];
            }
            LegFoldWeight = any ? 1f : 0f;
            if (!any) return;

            var f = p;
            float wr = FoldLeg(ref f, 0, legWallZ[0]), wl = FoldLeg(ref f, 1, legWallZ[1]);
            p.HipR = Quaternion.Slerp(p.HipR, f.HipR, wr);
            p.KneeR = Quaternion.Slerp(p.KneeR, f.KneeR, wr);
            p.HipL = Quaternion.Slerp(p.HipL, f.HipL, wl);
            p.KneeL = Quaternion.Slerp(p.KneeL, f.KneeL, wl);
        }

        /// <summary>How far a leg's boxes (from the last <see cref="BodyBoxes.Get"/>) reach behind the root (positive = behind).</summary>
        float LegBack(int leg)
        {
            int thigh = leg == 0 ? (int)BodyPart.ThighR : (int)BodyPart.ThighL;
            return Mathf.Max(BoxBack(legBoxes[thigh]), BoxBack(legBoxes[thigh + 1]));
        }

        static float BoxBack(in PartBox b) => -(b.Center.z - BodyBoxes.Radius(b, Vector3.forward));

        /// <summary>
        /// Sweeps a sphere from the root (inside the own capsule) towards the boot of <paramref name="leg"/>; returns
        /// the root-space z in front of which the leg is safe, or <see cref="NoWall"/> when the leg's way is free. An
        /// oblique wall counts where it is nearest across the leg's own width (its boxes from the last
        /// <see cref="BodyBoxes.Get"/>).
        /// </summary>
        float LegWallAlong(in CharacterPose p, int leg)
        {
            Vector3 sole = BodyBoxes.SolePos(p, leg);
            Vector3 a = new Vector3(0f, LegSweepY, 0f);
            Vector3 dir = new Vector3(sole.x + Mathf.Sign(sole.x) * LegSweepOut, 0f, sole.z);
            float len = dir.magnitude;
            if (len < 0.3f) return NoWall;
            dir /= len;
            if (!BodyRoom.SweepWall(transform.TransformPoint(a), transform.TransformPoint(a + dir * (len + 0.04f)), LegSweepR, LegSelf, out var hit))
                return NoWall;
            Vector3 c = transform.InverseTransformPoint(hit.point);
            Vector3 n = transform.InverseTransformDirection(hit.normal);
            float z;
            if (Mathf.Abs(n.z) > 0.05f)
            {
                int thigh = leg == 0 ? (int)BodyPart.ThighR : (int)BodyPart.ThighL;
                float x0 = float.MaxValue, x1 = float.MinValue;
                for (int i = thigh; i <= thigh + 1; i++)
                {
                    float r = BodyBoxes.Radius(legBoxes[i], Vector3.right);
                    x0 = Mathf.Min(x0, legBoxes[i].Center.x - r);
                    x1 = Mathf.Max(x1, legBoxes[i].Center.x + r);
                }
                float slope = n.x / n.z; // wall line: z = c.z - slope * (x - c.x)
                z = Mathf.Max(c.z - slope * (x0 - c.x), c.z - slope * (x1 - c.x));
            }
            else z = 0f; // a wall along the body crossing the leg line: fold all the way
            return Mathf.Min(z, -0.05f) + LegWallGap;
        }

        /// <summary>
        /// Re-solves one leg so that it stays in front of the plane z = <paramref name="wallZ"/> (root space). A knee
        /// that fits stays where it is and only the shin rises from it (kneeling, all fours, a long lying leg); lying
        /// flat a knee that does not fit comes in too (thigh flat when there is room, lifted when not). Returns how much
        /// of the fold to use: 0 when the leg fits, and also when nothing a leg can do helps (the hips themselves at
        /// the wall: raised legs would only stick out higher; the motor prevents that).
        /// </summary>
        float FoldLeg(ref CharacterPose p, int leg, float wallZ)
        {
            if (-LegBack(leg) >= wallZ) return 0f; // this leg fits

            const float T = RigIK.Thigh, S = RigIK.Shin;
            float side = leg == 0 ? 1f : -1f;
            Vector3 hip = p.HipPos(leg);
            Vector3 knee = BodyBoxes.KneePos(p, leg);
            float use = 1f;
            if (knee.z - LegBoxHalf < wallZ)
            {
                if ((p.Pelvis * Vector3.up).y > 0.6f) return 0f; // upright hips with the knee at the wall
                float limit = hip.z - (wallZ + LegBoxHalf); // how far behind the hip the knee may be
                use = Mathf.InverseLerp(-0.05f, 0.12f, limit);
                if (use <= 0f) return 0f;
                float kb = Mathf.Clamp(limit, 0.12f, T * 0.985f);
                float up = Mathf.Sqrt(Mathf.Max(0f, T * T - kb * kb - 0.0009f));
                knee = hip + new Vector3(side * 0.03f, up, -kb);
            }
            float sin = Mathf.Clamp((knee.z - LegBoxHalf - wallZ) / S, -0.3f, 1f);
            float cos = Mathf.Sqrt(Mathf.Max(0f, 1f - sin * sin));
            Vector3 sole = knee + new Vector3(0f, S * cos, -S * sin);
            p.SolveLeg(leg, sole, knee - (hip + sole) * 0.5f);
            return use;
        }
    }
}
