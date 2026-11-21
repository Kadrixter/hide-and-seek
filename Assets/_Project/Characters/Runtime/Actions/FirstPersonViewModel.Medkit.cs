using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// First-person hands for the medkit on a wound (<see cref="HoldAnim.HealSelf"/> / <see cref="HoldAnim.HealOther"/>),
    /// same clock and marks as the third-person animation (<see cref="HealTimeline"/> Med*).
    /// </summary>
    public partial class FirstPersonViewModel
    {
        bool MedkitHands(ref HeldItem show, out Vector3 rightT, out Quaternion propRot, ref Vector3 leftT, ref float wantLeft)
        {
            rightT = default;
            propRot = Quaternion.identity;
            if (!HealTimeline.IsMedkit(holdAnim)) return false;
            float t = holdTime;
            bool other = holdAnim == HoldAnim.HealOther;
            Quaternion toRig = BodyToRig();

            // Wound: the own belly below the camera (body frame), or the other hider's belly in front (rig space).
            Vector3 belly = new Vector3(0f, -0.6f, 0.52f);
            if (other)
            {
                belly = new Vector3(0f, -0.25f, 0.6f);
                if (holdTarget.HasValue && rig != null)
                {
                    Vector3 b = rig.InverseTransformPoint(holdTarget.Value + Vector3.down * 0.28f);
                    belly = new Vector3(Mathf.Clamp(b.x, -0.2f, 0.2f), Mathf.Clamp(b.y, -0.6f, 0.05f), Mathf.Clamp(b.z, 0.45f, 0.75f));
                }
            }
            Vector3 W(float a, float r = 1f) => other
                ? belly + new Vector3(0.22f * Mathf.Sin(a) * r, 0.015f * Mathf.Sin(a * 3f), 0.12f * (1f - Mathf.Cos(a)) * r)
                : toRig * new Vector3(0.2f * Mathf.Sin(a) * r, belly.y + 0.015f * Mathf.Sin(a * 3f), belly.z + 0.12f * Mathf.Cos(a) * r);

            Vector3 hiddenR = toRig * new Vector3(0.4f, -0.6f, 0.1f), hiddenL = toRig * new Vector3(-0.4f, -0.6f, 0.1f);
            Vector3 kit = toRig * new Vector3(0.36f, -0.78f, 0.55f);
            Vector3 r, l;
            float wl = 1f;
            if (t < HealTimeline.MedKitDown) { r = Vector3.Lerp(hiddenR, kit, Sg(t, HealTimeline.MedKneel, 0.62f)); l = hiddenL; wl = 0f; }
            else if (t < HealTimeline.MedKitOpen)
            {
                float lid = Mathf.Sin(Mathf.PI * ActionTimeline.Remap01(t, HealTimeline.MedKitDown, HealTimeline.MedKitOpen));
                r = kit;
                l = Vector3.Lerp(hiddenL, kit + toRig * new Vector3(-0.08f, 0.08f + 0.08f * lid, 0f), Sg(t, HealTimeline.MedKitDown, 0.8f));
                wl = Sg(t, HealTimeline.MedKitDown, 0.78f);
            }
            else if (t < HealTimeline.MedPad)
            {
                r = Vector3.Lerp(kit, W(0.7f), Sg(t, HealTimeline.MedKitOpen, HealTimeline.MedRollOut + 0.1f));
                l = Vector3.Lerp(kit + toRig * new Vector3(-0.08f, 0.1f, 0f), W(other ? -0.15f : -0.45f, 0.92f), Sg(t, HealTimeline.MedKitOpen + 0.05f, HealTimeline.MedRollOut + 0.08f));
            }
            else if (t < HealTimeline.MedWrapEnd)
            {
                float cyc = ActionTimeline.Remap01(t, HealTimeline.MedPad, HealTimeline.MedWrapEnd) * 3f;
                float ph = cyc - Mathf.Floor(cyc);
                float reach = other ? 1.5f : 2.0f;
                float a = ph < 0.6f ? Mathf.Lerp(-0.9f, reach, ActionTimeline.SmoothStep01(ph / 0.6f)) : Mathf.Lerp(reach, -0.9f, ActionTimeline.SmoothStep01((ph - 0.6f) / 0.4f));
                r = W(a, ph < 0.6f ? 1f : 1.35f);
                float b = ph < 0.6f ? -0.5f : Mathf.Lerp(-0.5f, -reach, Mathf.Sin(Mathf.PI * (ph - 0.6f) / 0.4f));
                l = W(b);
            }
            else if (t < HealTimeline.MedTieEnd)
            {
                float pull = Mathf.Abs(Mathf.Sin(ActionTimeline.Remap01(t, HealTimeline.MedWrapEnd, HealTimeline.MedTieEnd) * Mathf.PI * 2f));
                Vector3 f = W(0f, other ? 0f : 1.1f) + (other ? Vector3.zero : toRig * new Vector3(0f, 0f, 0.04f));
                Vector3 side = other ? Vector3.right : toRig * Vector3.right;
                r = f + side * (0.03f + 0.15f * pull);
                l = f - side * (0.03f + 0.15f * pull);
            }
            else
            {
                Vector3 f = W(0f, other ? 0f : 1.1f);
                r = Vector3.Lerp(Vector3.Lerp(f, kit, Sg(t, HealTimeline.MedTieEnd, 3.22f)), hiddenR, Sg(t, 3.25f, HealTimeline.MedkitDuration));
                l = Vector3.Lerp(f, hiddenL, Sg(t, HealTimeline.MedTieEnd, 3.35f));
                wl = 1f - Sg(t, 3.15f, 3.4f);
            }
            rightT = r;
            leftT = l;
            wantLeft = wl;
            bool roll = t >= HealTimeline.MedKitOpen && t < HealTimeline.MedWrapEnd + 0.15f;
            show = roll ? HeldItem.Bandage : (t < HealTimeline.MedKitDown - 0.05f || t >= HealTimeline.MedTieEnd + 0.15f) ? HeldItem.Medkit : HeldItem.None;
            propRot = roll ? toRig * Quaternion.Euler(0f, 90f, 0f) : toRig;
            SolveArm(shR, elR, ShoulderR, rightT, new Vector3(1f, -0.4f, -0.5f));
            return true;
        }

        /// <summary>Kit on the floor in front-right while the medkit heal runs (between putting it down and picking it up).</summary>
        bool MedkitOnFloor(out Vector3 localPos, out Quaternion localRot)
        {
            var toRig = BodyToRig();
            localPos = toRig * new Vector3(0.4f, -0.95f, 0.6f);
            localRot = toRig * Quaternion.Euler(0f, -25f, 90f);
            return HealTimeline.IsMedkit(holdAnim) && holdTime >= HealTimeline.MedKitDown - 0.05f && holdTime < HealTimeline.MedTieEnd + 0.15f;
        }
    }
}
