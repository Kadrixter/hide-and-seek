using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Boombox on the right shoulder (third person). Lift: the body bends / squats to the radio (deeper the lower it
    /// lies), the right fist takes the handle and the left hand its far end, the radio comes up to the chest and swings
    /// onto the shoulder, the left hand lets go. Carry: the radio rides on the shoulder (with the torso), the right fist
    /// holds the handle above it, the head leans away; with music on, the head nods on the bass and the body sways.
    /// Put down: the reverse, ending with the radio on its spot. Everything follows the server clock passed in by the
    /// game (<see cref="SetShoulderRadio"/>); the game moves the model to <see cref="ShoulderRadioPose"/> after the pose
    /// is applied. Knocked down / killed: the layer lets go at once (the game drops the radio).
    /// </summary>
    public partial class BlockyCharacterActions
    {
        bool srCarry, srPut, srOn;
        float srT = 99f, srBeat, srBeatS, srMusic, srMusicW, srW;
        Vector3 srGroundPos, srPosRoot, srFistRoot, srLeftRoot;
        float srLeftW, srRide;
        Quaternion srGroundRot = Quaternion.identity, srRotRoot = Quaternion.identity;

        /// <summary>
        /// Every frame from the game. <paramref name="carried"/>: this player has the radio (lifting while
        /// <paramref name="sinceMove"/> &lt; <see cref="ShoulderRadioRig.LiftTime"/>); <paramref name="putting"/>: it was
        /// just put down by him (animated while &lt; <see cref="ShoulderRadioRig.PutTime"/>). <paramref name="groundPos"/> /
        /// <paramref name="groundRot"/>: the radio's lying pose (start of the lift / end of the put). <paramref name="beat"/>:
        /// bass envelope of the music (0..1.5), <paramref name="music"/>: it plays.
        /// </summary>
        public void SetShoulderRadio(bool carried, bool putting, float sinceMove, Vector3 groundPos, Quaternion groundRot, float beat, bool music)
        {
            EnsureInit();
            srCarry = carried;
            srPut = !carried && putting;
            srT = sinceMove;
            srGroundPos = groundPos;
            srGroundRot = groundRot;
            srBeat = beat;
            srMusic = music ? 1f : 0f;
        }

        public void ClearShoulderRadio()
        {
            srCarry = srPut = srOn = false;
            srT = 99f;
        }

        /// <summary>True while the radio is in this character's hands / on its shoulder (pose valid).</summary>
        public bool ShoulderRadioOn => srOn;

        /// <summary>World pose of the radio's pivot (bottom centre) from the last applied pose.</summary>
        public bool ShoulderRadioPose(out Vector3 pos, out Quaternion rot)
        {
            pos = transform.TransformPoint(srPosRoot);
            rot = transform.rotation * srRotRoot;
            return srOn;
        }

        /// <summary>
        /// Where the hands hold the radio (world, from the last pose) - for the first-person hands: <paramref name="leftW"/>
        /// = the left hand helps, <paramref name="ride"/> = 1 while it rides on the shoulder.
        /// </summary>
        public bool ShoulderRadioHands(out Vector3 fist, out Vector3 left, out float leftW, out float ride)
        {
            fist = transform.TransformPoint(srFistRoot);
            left = transform.TransformPoint(srLeftRoot);
            leftW = srLeftW;
            ride = srRide;
            return srOn;
        }

        /// <summary>The right hand is on the radio (no held item / inventory item meanwhile).</summary>
        bool RadioBusy => srOn;

        void ApplyShoulderRadio(ref CharacterPose p, float dt, bool standing)
        {
            srOn = false;
            bool canPose = VisualLifeState == LifeState.Alive && !pathActive && action != ActionKind.StabbedReaction && action != ActionKind.KillReaction;
            float t = srT;
            bool lifting = srCarry && t < ShoulderRadioRig.LiftTime;
            bool riding = srCarry && !lifting;
            bool putting = srPut && t < ShoulderRadioRig.PutTime;
            srBeatS = Mathf.Max(srBeat * srMusic, Damp(srBeatS, srBeat * srMusic, 10f, dt));
            srMusicW = Damp(srMusicW, srMusic, 3f, dt);
            if (!canPose || (!lifting && !riding && !putting))
            {
                srW = 0f;
                return;
            }
            srOn = true;
            float S(float a, float b) => ShoulderRadioRig.S(t, a, b);

            var q = p;
            Vector3 gp = transform.InverseTransformPoint(srGroundPos);
            Quaternion gr = Quaternion.Inverse(transform.rotation) * srGroundRot;

            // ---- bend / squat towards the radio (lift and put)
            float bend = lifting ? S(0.04f, 0.42f) * (1f - S(0.6f, 1.02f))
                       : putting ? S(0.3f, 0.72f) * (1f - S(0.92f, 1.28f)) : 0f;
            float drop = Mathf.Clamp(0.78f - gp.y, 0f, 0.72f);         // radio on the floor: deep squat; on a pallet: a bow
            if (bend > 0.001f)
            {
                float k = drop / 0.72f;
                q.PelvisPos = p.PelvisPos + new Vector3(0f, -(0.04f + 0.3f * k) * bend, -(0.05f + 0.1f * k) * bend);
                q.Torso = p.Torso * Quaternion.Euler((16f + 26f * k) * bend, 0f, 0f);
                q.SolveLeg(0, Vector3.Lerp(new Vector3(0.13f, 0f, 0.02f), new Vector3(0.17f, 0f, 0.12f), bend), new Vector3(0.25f, 0f, 1f));
                q.SolveLeg(1, Vector3.Lerp(new Vector3(-0.13f, 0f, -0.02f), new Vector3(-0.15f, 0f, -0.14f), bend), new Vector3(-0.25f, 0f, 1f));
                Vector3 to = gp + Vector3.up * 0.15f - q.TorsoPoint(BlockyCharacter.NeckRest);
                float look = Mathf.Clamp(-Mathf.Atan2(to.y, Mathf.Max(0.1f, new Vector2(to.x, to.z).magnitude)) * Mathf.Rad2Deg - (16f + 26f * k) * bend, -10f, 40f);
                q.Head = Quaternion.Slerp(p.Head, Quaternion.Euler(look, 0f, 0f), bend);
            }

            // ---- where the radio is (root space)
            Quaternion facing = Quaternion.Euler(0f, Vector3.Dot(gr * Vector3.forward, Vector3.forward) >= 0f ? 0f : 180f, 0f);
            Vector3 chestPos = q.TorsoPoint(new Vector3(0.02f, 0.2f, 0.37f));
            Quaternion chestRot = q.TorsoRoot * facing;
            Vector3 ridePos = q.TorsoPoint(ShoulderRadioRig.RideTorso + new Vector3(0f, 0.006f * srBeatS, 0f));
            Quaternion rideRot = q.TorsoRoot * ShoulderRadioRig.RideRot * Quaternion.Euler(1.5f * srBeatS, 0f, 0f);
            Vector3 rPos;
            Quaternion rRot;
            float twoHands;
            if (lifting)
            {
                float up = S(ShoulderRadioRig.LiftGrab, ShoulderRadioRig.LiftChest);
                float swing = S(ShoulderRadioRig.LiftChest - 0.06f, ShoulderRadioRig.LiftTime - 0.08f);
                rPos = Vector3.Lerp(Vector3.Lerp(gp, chestPos, up), ridePos, swing) + Vector3.up * (0.1f * Mathf.Sin(Mathf.PI * swing));
                rRot = Quaternion.Slerp(Quaternion.Slerp(gr, chestRot, up), rideRot, swing);
                twoHands = 1f - S(ShoulderRadioRig.LiftChest + 0.1f, ShoulderRadioRig.LiftTime - 0.05f);
            }
            else if (putting)
            {
                float off = S(0.02f, ShoulderRadioRig.PutChest);
                float down = S(ShoulderRadioRig.PutChest - 0.04f, ShoulderRadioRig.PutRelease - 0.04f);
                rPos = Vector3.Lerp(Vector3.Lerp(ridePos, chestPos, off), gp, down) + Vector3.up * (0.08f * Mathf.Sin(Mathf.PI * off));
                rRot = Quaternion.Slerp(Quaternion.Slerp(rideRot, chestRot, off), gr, down);
                twoHands = S(0.04f, 0.3f);
                if (t >= ShoulderRadioRig.PutRelease) srOn = false; // it is on its spot: the game shows the lying radio
            }
            else
            {
                rPos = ridePos;
                rRot = rideRot;
                twoHands = 0f;
            }
            srPosRoot = rPos;
            srRotRoot = rRot;

            // ---- hands
            Vector3 fist = rPos + rRot * (ShoulderRadioRig.GripLocal + ShoulderRadioRig.FistOnGrip);
            // Left hand on the end of the radio that is on the character's left, from the outside.
            Vector3 endA = rPos + rRot * new Vector3(-ShoulderRadioRig.Width * 0.5f - 0.1f, ShoulderRadioRig.Height * 0.42f, 0f);
            Vector3 endB = rPos + rRot * new Vector3(ShoulderRadioRig.Width * 0.5f + 0.1f, ShoulderRadioRig.Height * 0.42f, 0f);
            Vector3 left = endA.x < endB.x ? endA : endB;

            float reach = lifting ? S(0.02f, ShoulderRadioRig.LiftGrab - 0.04f) : 1f;
            float release = putting ? 1f - S(ShoulderRadioRig.PutRelease + 0.02f, ShoulderRadioRig.PutTime) : 1f;
            srFistRoot = fist;
            srLeftRoot = left;
            srLeftW = Mathf.Min(reach, twoHands) * release;
            Vector3 restR = p.HandPos(0), restL = p.HandPos(1);
            Vector3 handR = Vector3.Lerp(restR, fist, reach * release);
            Vector3 handL = Vector3.Lerp(restL, left, Mathf.Min(reach, twoHands) * release);
            if (putting && t >= ShoulderRadioRig.PutRelease)
            {
                // Let go: the hands lift off the radio first, then drop back.
                float lift = Mathf.Sin(Mathf.PI * S(ShoulderRadioRig.PutRelease, ShoulderRadioRig.PutRelease + 0.25f)) * 0.06f;
                handR += Vector3.up * lift;
                handL += Vector3.up * lift;
            }

            float rideW = riding ? 1f : lifting ? S(ShoulderRadioRig.LiftChest, ShoulderRadioRig.LiftTime) : putting ? 1f - S(0.02f, ShoulderRadioRig.PutChest) : 0f;
            srRide = rideW;
            Vector3 pole = Vector3.Slerp(TorsoDir(q, 1f, -0.9f, -0.3f), q.TorsoRoot * ShoulderRadioRig.RidePole.normalized, rideW);
            q.SolveArm(0, handR, pole);
            q.SolveArm(1, handL, TorsoDir(q, -1f, -0.6f, -0.3f));

            // ---- head away from the radio, nod on the bass; a little sway with the music
            if (rideW > 0.001f)
            {
                float sway = Mathf.Sin(time * Mathf.PI * 2f * 0.92f) * srMusicW;
                float nod = 7f * srBeatS;
                // The block head next to the radio cannot turn (any turn swings a corner into the radio): it keeps the look
                // up / down and the nod, leans away from the radio and only sways a little with the music.
                Vector3 he = q.Head.eulerAngles;
                float hp = Mathf.Clamp(Mathf.DeltaAngle(0f, he.x), -40f, 40f);
                Quaternion held = Quaternion.Euler(hp + nod, Mathf.Clamp(ShoulderRadioRig.HeadYaw + 1.5f * sway, -4f, 2f), ShoulderRadioRig.HeadTilt + 1.5f * sway);
                q.Head = Quaternion.Slerp(q.Head, held, rideW);
                q.Torso = q.Torso * Quaternion.Euler(1.2f * srBeatS * rideW, 3f * sway * rideW, (2.5f + 2.2f * sway) * rideW);
                q.PelvisPos += new Vector3(0.012f * sway * rideW, -0.008f * srBeatS * rideW, 0f);
            }

            float w = lifting ? S(0f, 0.22f) : putting ? 1f - S(ShoulderRadioRig.PutRelease + 0.08f, ShoulderRadioRig.PutTime) : 1f;
            srW = w;
            q.PropR = p.PropR;
            p = CharacterPose.Lerp(p, q, w);
            if (srOn)
            {
                // The hands hold the radio exactly where they were solved (no half-way arm from the blend).
                CharacterPose.LerpArm(ref p, q, 0, Mathf.Max(w, reach * release));
                CharacterPose.LerpArm(ref p, q, 1, Mathf.Max(w, Mathf.Min(reach, twoHands) * release));
                // The radio rides with the blended torso (the shoulder it sits on).
                if (riding || rideW > 0.999f)
                {
                    srPosRoot = p.TorsoPoint(ShoulderRadioRig.RideTorso + new Vector3(0f, 0.006f * srBeatS, 0f));
                    srRotRoot = p.TorsoRoot * ShoulderRadioRig.RideRot * Quaternion.Euler(1.5f * srBeatS, 0f, 0f);
                }
            }
        }
    }
}
