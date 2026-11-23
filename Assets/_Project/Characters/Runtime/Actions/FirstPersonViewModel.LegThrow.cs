using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// First-person leg throw (<see cref="LegThrowTimeline"/>): the carried leg (held by the ankle in the lower right)
    /// goes up to the right edge of the view on the wind-up, the shin across the top corner and the cut end behind the
    /// shoulder, the left hand comes up in front to aim; letting go whips it over the top through the middle of the view,
    /// the leg leaves the hand (the flying world item takes over) and the arm follows through down to the left.
    /// </summary>
    public partial class FirstPersonViewModel
    {
        bool vmLegAim;
        float vmLegAimW, vmLegPower, vmLegThrowT = -1f, vmLegFrom, vmLegClock;

        public void SetLegAim(bool aiming, float power)
        {
            vmLegAim = aiming;
            vmLegPower = Mathf.Clamp01(power);
        }

        public void PlayLegThrow(float power)
        {
            vmLegFrom = Mathf.Max(vmLegAimW, 0.35f);
            vmLegThrowT = 0f;
            vmLegPower = Mathf.Clamp01(power);
            vmLegAim = false;
        }

        bool LegThrowHands(ref HeldItem show, out Vector3 rightT, out Quaternion propRot, ref Vector3 leftT, ref float wantLeft)
        {
            rightT = default;
            propRot = Quaternion.identity;
            float dt = Mathf.Clamp(time - vmLegClock, 0f, 0.1f); // the rig's own clock (Tick dt)
            vmLegClock = time;
            if (vmLegThrowT >= 0f)
            {
                vmLegThrowT += dt;
                if (vmLegThrowT >= LegThrowTimeline.End) vmLegThrowT = -1f;
            }
            bool holding = carryShownGrip != null && show == HeldItem.None;
            bool aim = vmLegAim && vmLegThrowT < 0f && holding;
            vmLegAimW = Mathf.MoveTowards(vmLegAimW, aim ? 1f : 0f, dt / (aim ? LegThrowTimeline.WindTime : 0.2f));
            if (rig == null || (vmLegAimW <= 0.001f && vmLegThrowT < 0f)) return false;
            if (vmLegThrowT < 0f && !holding) return false;

            float t = vmLegThrowT;
            float S(float a, float b) => ActionTimeline.SmoothStep01(ActionTimeline.Remap01(t, a, b));
            float cock, swing = 0f, follow = 0f, back = 0f;
            if (t >= 0f)
            {
                cock = vmLegFrom;
                swing = ActionTimeline.EaseIn(ActionTimeline.Remap01(t, 0f, LegThrowTimeline.Release + 0.03f));
                follow = S(LegThrowTimeline.Release, 0.36f);
                back = S(0.4f, LegThrowTimeline.End);
            }
            else cock = ActionTimeline.SmoothStep01(vmLegAimW);

            var g = carryShownGrip;
            Vector3 hold = g != null ? g.FpHand : new Vector3(0.3f, -0.36f, 0.58f);
            Quaternion holdRot = g != null ? Quaternion.Euler(g.FpRotation) : Quaternion.identity;
            float tremble = t < 0f ? 0.006f * vmLegPower * cock : 0f;
            Vector3 cocked = new Vector3(0.36f, -0.06f, 0.5f) + new Vector3(Mathf.Sin(time * 41f), Mathf.Sin(time * 37f + 1f), 0f) * tremble;
            Vector3 release = new Vector3(0.1f, 0.02f, 0.62f);
            Vector3 after = new Vector3(-0.12f, -0.72f, 0.5f);
            Vector3 hidden = new Vector3(0.3f, -0.95f, 0.35f);
            rightT = Vector3.Lerp(Vector3.Lerp(Vector3.Lerp(Vector3.Lerp(hold, cocked, cock), release, swing), after, follow), hidden, back);

            Quaternion cockRot = LegRotFp(new Vector3(0.3f, 0.5f, -0.8f), Vector3.right);
            Quaternion relRot = LegRotFp(new Vector3(-0.05f, 0.75f, 0.66f), Vector3.right);
            propRot = Quaternion.Slerp(Quaternion.Slerp(holdRot, cockRot, cock), relRot, swing);
            SolveArm(shR, elR, ShoulderR, rightT, Vector3.Lerp(new Vector3(1f, -1f, -0.4f), new Vector3(1f, 0.2f, -0.6f), cock * (1f - swing)));
            show = HeldItem.None;
            carryHands = t < LegThrowTimeline.Release; // the leg in the fist until it is let go

            // Left hand: up in front, pointing the way, then out of view with the throw.
            Vector3 aimL = new Vector3(-0.27f, -0.36f, 0.66f);
            leftT = Vector3.Lerp(aimL, new Vector3(-0.34f, -0.9f, 0.3f), Mathf.Max(swing * 0.6f, follow));
            wantLeft = cock * (1f - back);
            return true;
        }

        static Quaternion LegRotFp(Vector3 stump, Vector3 toes)
        {
            stump.Normalize();
            Vector3 f = Vector3.ProjectOnPlane(toes, stump);
            if (f.sqrMagnitude < 1e-4f) f = Vector3.forward;
            return Quaternion.LookRotation(f.normalized, stump);
        }
    }
}
