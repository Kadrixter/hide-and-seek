using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// First-person "Pilot RC": raised from below in both hands when its slot is selected (face tilted up to the camera,
    /// fists round the handles, left of the centre line the antenna); and the smash on the <see cref="RcRemoteTimeline"/>
    /// clock: brought up and shaken, swung up over the right shoulder (the left hand lets go), slammed down out of view.
    /// The flying transmitter and its wreck are world objects (BlockyCharacterActions / the game).
    /// </summary>
    public partial class FirstPersonViewModel
    {
        bool vmRemoteWanted, vmThrowFromHold;
        float vmRemoteRaise, vmRemoteThrowT = -1f;
        static readonly Vector3 RemoteHoldPos = new Vector3(0.17f, -0.44f, 0.6f);

        /// <summary>The transmitter is selected in the hotbar (shown in both hands).</summary>
        public void SetRcRemoteHeld(bool on) => vmRemoteWanted = on;

        /// <summary>Drives the first-person smash; <paramref name="t"/> &lt; 0 ends it.</summary>
        public void SetRemoteThrow(float t)
        {
            if (vmRemoteThrowT < 0f && t >= 0f) vmThrowFromHold = vmRemoteRaise > 0.5f; // already in the hands: no raise
            vmRemoteThrowT = t >= 0f && t < RcRemoteTimeline.End ? t : -1f;
            if (vmRemoteThrowT < 0f) vmRemoteRaise = 0f; // thrown away: raised again from below if still selected
        }

        static Quaternion RemoteHoldRot => Quaternion.LookRotation(new Vector3(-0.05f, 0.62f, -0.78f), new Vector3(0f, 0.78f, 0.62f));

        /// <summary>Smash (high priority: before the other hold animations).</summary>
        bool RemoteThrowHands(ref HeldItem show, out Vector3 rightT, out Quaternion propRot, ref Vector3 leftT, ref float wantLeft)
        {
            rightT = default;
            propRot = Quaternion.identity;
            if (vmRemoteThrowT < 0f || rig == null) return false;
            float t = vmRemoteThrowT;
            float S(float a, float b) => RcRemoteTimeline.Seg(t, a, b);
            Vector3 hidden = new Vector3(0.36f, -0.9f, 0.28f);
            float rise = vmThrowFromHold ? 1f : S(0f, 0.18f);
            float shakeK = t > 0.08f && t < RcRemoteTimeline.WindStart ? 1f - S(0.22f, RcRemoteTimeline.WindStart) : 0f;
            float wind = S(RcRemoteTimeline.WindStart, RcRemoteTimeline.ThrowStart);
            float thr = S(RcRemoteTimeline.ThrowStart, RcRemoteTimeline.Release + 0.02f);
            float back = S(RcRemoteTimeline.Release + 0.05f, RcRemoteTimeline.Release + 0.4f);

            Vector3 hold = Vector3.Lerp(RemoteHoldPos + new Vector3(0f, -0.4f, 0f), RemoteHoldPos + new Vector3(-0.02f, 0.07f, 0.02f), rise);
            hold += new Vector3(0.008f * Mathf.Sin(t * 46f), 0.006f * Mathf.Sin(t * 39f), 0f) * shakeK;
            Vector3 up = new Vector3(0.46f, 0.08f, 0.22f);     // over the right shoulder, mostly out of view
            Vector3 down = new Vector3(0.2f, -1.05f, 0.62f);   // slammed down below the view
            rightT = Vector3.Lerp(Vector3.Lerp(Vector3.Lerp(hold, up, wind), down, thr), hidden, back);

            Quaternion holdRot = RemoteHoldRot * Quaternion.Euler(0f, 0f, 9f * Mathf.Sin(t * 46f) * shakeK);
            Quaternion windRot = Quaternion.LookRotation(new Vector3(-0.2f, 0.75f, 0.63f), new Vector3(0.1f, 0.62f, -0.78f));
            Quaternion throwRot = Quaternion.LookRotation(new Vector3(0f, 0.5f, 0.87f), new Vector3(0f, -0.87f, 0.5f));
            propRot = Quaternion.Slerp(Quaternion.Slerp(holdRot, windRot, wind), throwRot, thr);
            show = t < RcRemoteTimeline.Release ? HeldItem.RcRemote : HeldItem.None;
            SolveArm(shR, elR, ShoulderR, rightT, new Vector3(1f, -0.8f + 0.9f * wind * (1f - thr), -0.5f));

            // Left hand on the left handle until the wind-up, then it drops away.
            float leftOn = 1f - S(RcRemoteTimeline.WindStart - 0.05f, RcRemoteTimeline.ThrowStart - 0.05f);
            leftT = HandPos(shR, elR) + holdRot * RcRemoteModel.LeftGrip;
            wantLeft = leftOn * rise;
            return true;
        }

        /// <summary>Held in both hands while its slot is selected (raised from below).</summary>
        bool RemoteHoldHands(ref HeldItem show, out Vector3 rightT, out Quaternion propRot, ref Vector3 leftT, ref float wantLeft)
        {
            rightT = default;
            propRot = Quaternion.identity;
            vmRemoteRaise = Mathf.MoveTowards(vmRemoteRaise, vmRemoteWanted && show == HeldItem.None ? 1f : 0f, Time.deltaTime / 0.22f);
            if (vmRemoteRaise <= 0.001f || show != HeldItem.None || rig == null) return false;
            float lower = 1f - ActionTimeline.SmoothStep01(vmRemoteRaise);
            float breath = Mathf.Sin(time * 1.7f);
            rightT = RemoteHoldPos + new Vector3(0f, -0.4f * lower + 0.004f * breath, -0.05f * lower);
            propRot = RemoteHoldRot * Quaternion.Euler(30f * lower, 0f, 0f);
            show = HeldItem.RcRemote;
            SolveArm(shR, elR, ShoulderR, rightT, new Vector3(1f, -1f, -0.5f));
            leftT = HandPos(shR, elR) + propRot * RcRemoteModel.LeftGrip;
            wantLeft = 1f - lower;
            return true;
        }
    }
}
