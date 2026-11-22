using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>Timeline of the RC car animations (seconds).</summary>
    public static class RcTimeline
    {
        /// <summary>Kneel down with the car, put it on the floor, sit down, take out the transmitter.</summary>
        public const float Place = 1.75f;
        /// <summary>The car leaves the hand (the world car appears on the floor).</summary>
        public const float Release = 0.85f;
        /// <summary>The car view takes over the camera (owner).</summary>
        public const float CameraIn = 1.05f;
        public const float RemoteShowPlace = 1.2f;
        /// <summary>Sit down again with the car already on the floor (resume driving).</summary>
        public const float Resume = 0.8f;
        public const float RemoteShowResume = 0.35f;
        /// <summary>Put the transmitter away and stand up.</summary>
        public const float StandUp = 0.8f;
        /// <summary>Seeker stomps on a car: knee up, slam down, grind, back.</summary>
        public const float Stomp = 1.0f;
        public const float StompHit = 0.42f;

        /// <summary>Car origin relative to the character root when it is put down (root space).</summary>
        public static readonly Vector3 PlaceLocal = new Vector3(0.08f, 0f, 0.5f);

        public static float Seg(float t, float a, float b) => ActionTimeline.SmoothStep01(ActionTimeline.Remap01(t, a, b));
    }

    public partial class BlockyCharacterActions
    {
        // ---- RC driving (hider)
        bool rcDriving, rcPlacing;
        float rcT = 99f, rcOutT = 99f, rcW;
        Vector2 rcInput;
        Vector3 rcPlaceLocal = RcTimeline.PlaceLocal;
        CharacterPose rcOutFrom;
        bool rcHasOutFrom;

        // ---- stomp (seeker)
        float stompT = -1f;
        Vector3 stompLocal;

        /// <summary>
        /// RC car control: <paramref name="driving"/> while the player sits and steers. <paramref name="t"/> = seconds since
        /// the drive started; <paramref name="placing"/> = it started by putting the car down (else: sitting down again).
        /// </summary>
        public void SetRcDrive(bool driving, bool placing, float t, Vector2 input, Vector3 placeWorld)
        {
            EnsureInit();
            if (rcDriving && !driving)
            {
                rcOutT = 0f;
                rcOutFrom = hasLast ? lastOutput : CharacterPose.Capture(character);
                rcHasOutFrom = true;
            }
            rcDriving = driving;
            rcPlacing = placing;
            rcT = t;
            rcInput = input;
            if (driving) rcPlaceLocal = transform.InverseTransformPoint(placeWorld);
        }

        public bool RcActive => rcDriving || rcOutT < RcTimeline.StandUp;

        /// <summary>Seeker stomps on the RC car at <paramref name="carWorld"/>.</summary>
        public void PlayStomp(Vector3 carWorld)
        {
            EnsureInit();
            stompT = 0f;
            stompLocal = transform.InverseTransformPoint(carWorld);
        }

        void AdvanceRc(float dt)
        {
            if (!rcDriving) rcOutT += dt;
            bool alive = VisualLifeState == LifeState.Alive && action == ActionKind.None && !pathActive;
            float target = (rcDriving || rcOutT < RcTimeline.StandUp) && alive ? 1f : 0f;
            rcW = Mathf.MoveTowards(rcW, target, dt / 0.15f);
            if (stompT >= 0f)
            {
                stompT += dt;
                if (stompT >= RcTimeline.Stomp) stompT = -1f;
            }
        }

        HeldItem RcShownItem()
        {
            if (rcW < 0.5f) return HeldItem.None;
            if (rcDriving)
            {
                if (rcPlacing && rcT < RcTimeline.Release) return HeldItem.RcCar;
                float show = rcPlacing ? RcTimeline.RemoteShowPlace : remoteSelected ? 0f : RcTimeline.RemoteShowResume;
                return rcT >= show ? HeldItem.RcRemote : HeldItem.None;
            }
            return rcOutT < 0.3f || remoteSelected ? HeldItem.RcRemote : HeldItem.None; // "Pilot RC" selected: stays in the hands
        }

        void UpdateRcProps(HeldItem show)
        {
            var remote = props[(int)HeldItem.RcRemote];
            if (remote != null && show == HeldItem.RcRemote) remote.SetRcSticks(rcDriving ? rcInput : Vector2.zero);
        }

        /// <summary>Sitting on the floor (legs forward), transmitter in both hands in front of the belly, looking at it.</summary>
        CharacterPose RcSitPose(Vector2 input, out Quaternion remoteRot)
        {
            var p = SitFloorPose();
            float breath = Mathf.Sin(time * 2.1f);
            p.Torso = Quaternion.Euler(18f + 1.5f * breath, -4f * input.x, 0f);
            p.Head = Quaternion.Euler(30f, 6f * input.x, 3f * input.x);
            // Transmitter: face towards the eyes, antenna up and forward; leans a little into the steering.
            remoteRot = p.TorsoRoot * Quaternion.LookRotation(new Vector3(0f, 0.72f, -0.7f), new Vector3(0f, 0.7f, 0.72f))
                        * Quaternion.Euler(0f, 0f, -9f * input.x);
            Vector3 centre = p.TorsoPoint(new Vector3(0.0f, 0.24f, 0.3f)) + Vector3.up * 0.01f * input.y;
            Vector3 right = centre - remoteRot * (BlockyProps.RcRemoteLeftGrip * 0.5f);
            Vector3 left = right + remoteRot * BlockyProps.RcRemoteLeftGrip;
            p.SolveArm(0, right, TorsoDir(p, 1f, -0.8f, -0.4f));
            p.SolveArm(1, left, TorsoDir(p, -1f, -0.8f, -0.4f));
            p.PropR = Quaternion.Inverse(p.ForearmRoot(0)) * remoteRot;
            return p;
        }

        /// <summary>Kneeling and leaning far forward to reach the floor in front.</summary>
        static CharacterPose RcKneelReach()
        {
            var k = KneelPose(0f, -0.05f);
            k.Torso = Quaternion.Euler(52f, 0f, 0f);
            k.Head = Quaternion.Euler(10f, 0f, 0f);
            return k;
        }

        /// <summary>Full-body RC pose (placing, sitting with the transmitter, standing up).</summary>
        CharacterPose RcPose(in CharacterPose basePose)
        {
            Quaternion remoteRot;
            var sit = RcSitPose(rcInput, out remoteRot);
            if (!rcDriving)
            {
                // Standing up: transmitter away, onto the knees, up.
                float u = rcOutT;
                var from = rcHasOutFrom ? rcOutFrom : sit;
                var kneel = KneelPose(0f, -0.08f);
                if (u < 0.4f) return CharacterPose.Lerp(from, kneel, RcTimeline.Seg(u, 0f, 0.4f));
                return CharacterPose.Lerp(kneel, basePose, RcTimeline.Seg(u, 0.4f, RcTimeline.StandUp));
            }

            float t = rcT;
            if (!rcPlacing)
            {
                // Sitting down again: knees, then sit, transmitter comes out.
                var kneel = KneelPose(0f, -0.08f);
                var r = t < 0.3f ? CharacterPose.Lerp(basePose, kneel, RcTimeline.Seg(t, 0f, 0.3f))
                    : CharacterPose.Lerp(kneel, sit, RcTimeline.Seg(t, 0.3f, RcTimeline.Resume));
                r.PropR = sit.PropR;
                return r;
            }

            // ---- placing the car
            var reach = RcKneelReach();
            Vector3 floor = new Vector3(rcPlaceLocal.x, 0f, rcPlaceLocal.z);
            Vector3 fistAtFloor = floor - BlockyProps.RcCarInHand + new Vector3(0f, 0.004f, 0f);
            if (t < RcTimeline.Release)
            {
                // Car held by the roof: from the hip, lowered onto the floor, set down gently.
                var pose = CharacterPose.Lerp(basePose, reach, RcTimeline.Seg(t, 0f, 0.45f));
                Vector3 hip = pose.TorsoPoint(new Vector3(0.24f, 0.2f, 0.18f));
                Vector3 above = fistAtFloor + new Vector3(0f, 0.12f, -0.05f);
                Vector3 hand = t < 0.55f ? Vector3.Lerp(hip, above, RcTimeline.Seg(t, 0.1f, 0.55f))
                    : Vector3.Lerp(above, fistAtFloor, RcTimeline.Seg(t, 0.55f, RcTimeline.Release - 0.05f));
                pose.SolveArm(0, hand, TorsoDir(pose, 1f, -0.6f, -0.4f));
                pose.SolveArm(1, pose.TorsoPoint(new Vector3(-0.3f, 0.25f, 0.25f)), TorsoDir(pose, -1f, -0.8f, -0.3f)); // other hand on the knee
                pose.PropR = Quaternion.Inverse(pose.ForearmRoot(0)) * Quaternion.Euler(0f, 4f * (1f - RcTimeline.Seg(t, 0.4f, 0.8f)), 0f);
                return pose;
            }
            // Let go, lean back, sit on the floor, take the transmitter out of the pocket.
            var lift = reach;
            lift.SolveArm(0, fistAtFloor + new Vector3(0f, 0.08f, -0.08f), TorsoDir(lift, 1f, -0.6f, -0.4f));
            if (t < 1.25f)
            {
                var r = CharacterPose.Lerp(lift, sit, RcTimeline.Seg(t, RcTimeline.Release, 1.25f));
                r.PropR = sit.PropR;
                return r;
            }
            var pocket = sit;
            pocket.SolveArm(0, sit.TorsoPoint(new Vector3(0.25f, 0.12f, 0.12f)), TorsoDir(sit, 1f, -0.8f, -0.3f));
            pocket.SolveArm(1, sit.TorsoPoint(new Vector3(-0.22f, 0.15f, 0.2f)), TorsoDir(sit, -1f, -0.8f, -0.3f));
            var res = CharacterPose.Lerp(pocket, sit, RcTimeline.Seg(t, RcTimeline.RemoteShowPlace, RcTimeline.Place));
            res.PropR = sit.PropR;
            return res;
        }

        /// <summary>Seeker's stomp on a toy car: right knee up, slam the sole onto the roof, grind, step back.</summary>
        void ApplyStomp(ref CharacterPose p)
        {
            if (stompT < 0f) return;
            float t = stompT;
            float w = RcTimeline.Seg(t, 0f, 0.12f) * (1f - RcTimeline.Seg(t, 0.78f, RcTimeline.Stomp));
            if (w <= 0.001f) return;
            var s = p;
            Vector3 car = new Vector3(Mathf.Clamp(stompLocal.x, -0.2f, 0.35f), 0f, Mathf.Clamp(stompLocal.z, 0.2f, 0.6f));
            Vector3 raised = new Vector3(car.x * 0.6f + 0.05f, 0.48f, car.z * 0.6f);
            Vector3 roof = car + new Vector3(0f, 0.17f, 0f);
            Vector3 flat = car + new Vector3(0f, 0.065f, 0f);
            Vector3 sole;
            if (t < 0.34f) sole = Vector3.Lerp(new Vector3(0.12f, 0f, 0.02f), raised, RcTimeline.Seg(t, 0.05f, 0.34f));
            else if (t < RcTimeline.StompHit) { float k = ActionTimeline.Remap01(t, 0.34f, RcTimeline.StompHit); sole = Vector3.Lerp(raised, roof, k * k); }
            else if (t < 0.52f) sole = Vector3.Lerp(roof, flat, RcTimeline.Seg(t, RcTimeline.StompHit, 0.5f));
            else sole = flat + new Vector3(0.012f * Mathf.Sin((t - 0.52f) * 40f), 0f, 0f) * (1f - RcTimeline.Seg(t, 0.6f, 0.72f));
            float lean = t < RcTimeline.StompHit ? -6f * RcTimeline.Seg(t, 0f, 0.34f) : 14f * RcTimeline.Seg(t, RcTimeline.StompHit, 0.5f);
            float drop = t >= RcTimeline.StompHit ? 0.06f * RcTimeline.Seg(t, RcTimeline.StompHit, 0.5f) : 0f;
            s.PelvisPos = new Vector3(-0.05f, BlockyCharacter.HipHeight - 0.02f - drop, -0.05f);
            s.Pelvis = Quaternion.Euler(0f, -10f, 0f);
            s.Torso = Quaternion.Euler(lean, 8f, 0f);
            s.Head = Quaternion.Euler(24f, 0f, 0f);
            s.SolveLeg(0, sole, new Vector3(0.1f, 0.3f, 1f));
            s.SolveLeg(1, new Vector3(-0.14f, 0f, -0.06f), new Vector3(-0.1f, 0f, 1f));
            // Arms out for balance, the knife hand stays roughly where it is.
            s.SolveArm(1, s.TorsoPoint(new Vector3(-0.42f, 0.42f, 0.12f)), TorsoDir(s, -1f, -0.3f, -0.6f));
            p = CharacterPose.Lerp(p, s, w);
        }
    }
}
