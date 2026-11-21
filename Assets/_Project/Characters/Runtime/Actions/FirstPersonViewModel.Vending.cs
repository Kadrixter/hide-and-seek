using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// First-person hands for the vending machine purchase (same clock as <see cref="VendingTimeline"/>).
    /// Button and tray are reached in world space; holding, opening and drinking are laid out in view space so the
    /// can stays in frame, at a natural size, with its logo towards the camera.
    /// </summary>
    public partial class FirstPersonViewModel
    {
        float vendT = -1f;
        int vendBrand;
        Vector3 vendButtonW, vendTrayCanW;
        Quaternion vendBodyW = Quaternion.identity;

        /// <summary>Starts / drives the purchase hands; <paramref name="time"/> &lt; 0 ends it.</summary>
        public void SetVending(float time, Vector3 buttonWorld, Vector3 trayCanWorld, Quaternion bodyWorld, int brand)
        {
            vendT = time < VendingTimeline.Release ? time : -1f;
            vendButtonW = buttonWorld;
            vendTrayCanW = trayCanWorld;
            vendBodyW = bodyWorld;
            vendBrand = brand;
        }

        bool VendingHands(ref HeldItem show, out Vector3 rightT, out Quaternion propRot, ref Vector3 leftT, ref float wantLeft)
        {
            rightT = default;
            propRot = Quaternion.identity;
            if (vendT < 0f || rig == null) return false;
            float t = vendT;
            float S(float a, float b) => VendingTimeline.S(t, a, b);

            Vector3 rest = new Vector3(0.3f, -0.78f, 0.32f);
            Vector3 button = rig.InverseTransformPoint(vendButtonW);
            button.z = Mathf.Max(button.z, 0.32f);
            Vector3 pre = button + new Vector3(-0.02f, -0.02f, -0.12f), pressed = button + new Vector3(0f, 0f, 0.015f);

            // Can lying in the tray (world), seen from the (pitched) camera; gripped from above.
            Quaternion trayRot = Quaternion.Inverse(rig.rotation) * (vendBodyW * VendingTimeline.GripRotation);
            Vector3 trayCan = rig.InverseTransformPoint(vendTrayCanW);
            Vector3 grab = trayCan - trayRot * new Vector3(0f, 0f, BlockyProps.CanOffset * VendingTimeline.MachineCanScale / BlockyProps.CanScale);

            // In view: can just right of centre, upright on screen, logo to the camera.
            Quaternion viewHold = Quaternion.LookRotation(new Vector3(-1f, 0f, 0.25f).normalized, Vector3.up);
            Vector3 chest = new Vector3(0.3f, -0.24f, 0.62f);

            // The keypad is on the left: the left hand presses the button.
            float pressW = S(0.05f, 0.35f) * (1f - S(0.65f, 1.2f));
            Vector3 push = Vector3.Lerp(pre, pressed, S(0.35f, 0.45f) * (1f - S(0.5f, 0.62f)));
            if (t < VendingTimeline.Grip)
            {
                rightT = Vector3.Lerp(rest, grab + rig.InverseTransformDirection(Vector3.up) * 0.06f * (1f - S(2.2f, 2.6f)), S(1.6f, 2.45f));
                propRot = trayRot;
                show = HeldItem.None;
            }
            else
            {
                show = t < VendingTimeline.Release - 0.12f ? HeldItem.Can : HeldItem.None;
                float lift = S(2.75f, 3.65f);
                rightT = Vector3.Lerp(grab, chest, lift);
                propRot = Quaternion.Slerp(trayRot, viewHold, lift);
                // Tab pop: a small jolt of the can.
                rightT += new Vector3(0f, 0.012f, 0f) * Mathf.Sin(Mathf.Max(0f, t - 4.05f) * 25f) * S(4.4f, 4.55f) * (1f - S(4.6f, 4.8f));

                // Drink: can top at the mouth, the bottom rises in front of the face (tipped past horizontal).
                float drink = VendingTimeline.Drink(t);
                if (drink > 0f)
                {
                    float tilt = (VendingTimeline.Tilt(t) + 15f) * Mathf.Deg2Rad;
                    Quaternion tipped = Quaternion.LookRotation(Vector3.left, new Vector3(0f, Mathf.Cos(tilt), -Mathf.Sin(tilt)));
                    Vector3 atMouth = new Vector3(0f, -0.1f, 0.12f) - tipped * BlockyProps.CanTopLocal;
                    rightT = Vector3.Lerp(rightT, atMouth, drink);
                    propRot = Quaternion.Slerp(propRot, tipped, drink);
                }
                // Crush (shake), cock low and fling up and back past the right ear, out of view.
                float crush = VendingTimeline.Crush(t) * (1f - S(7.2f, 7.3f));
                rightT += new Vector3(0.006f, 0.006f, 0f) * Mathf.Sin(t * 70f) * crush;
                float cock = S(7.2f, 7.6f), fling = S(7.55f, VendingTimeline.Release);
                rightT = Vector3.Lerp(rightT, new Vector3(0.3f, -0.45f, 0.5f), cock);
                rightT = Vector3.Lerp(rightT, new Vector3(0.45f, 0.25f, -0.15f), fling);
                propRot = Quaternion.Slerp(propRot, Quaternion.LookRotation(Vector3.left, new Vector3(0f, 0.3f, -1f)), fling);
            }
            SolveArm(shR, elR, ShoulderR, rightT, new Vector3(1f, -0.8f, -0.4f));

            // Left hand: presses the button, later pops the tab (fingers over the top of the can).
            float tabW = S(3.7f, 4.0f) * (1f - S(4.8f, 5.05f));
            if (pressW > 0f)
            {
                leftT = push;
                wantLeft = pressW;
            }
            else if (tabW > 0f)
            {
                Vector3 top = HandPos(shR, elR) + propRot * BlockyProps.CanTopLocal;
                leftT = top + new Vector3(-0.12f, 0.06f + 0.03f * VendingTimeline.Open(t), 0.1f);
                wantLeft = tabW;
            }
            else wantLeft = 0f;
            return true;
        }

        void UpdateVendingProp(HeldItem show)
        {
            var can = props[(int)HeldItem.Can];
            if (can == null || show != HeldItem.Can) return;
            can.SetCan(vendBrand, VendingTimeline.Open(vendT));
            can.Can.SetCrush(VendingTimeline.Crush(vendT));
            // Picked up at machine size, grows to the hand size while it is lifted (the fist is 22 cm wide).
            float grow = Mathf.Lerp(VendingTimeline.MachineCanScale / BlockyProps.CanScale, 1f, VendingTimeline.S(vendT, 2.75f, 3.4f));
            can.Can.Scale = BlockyProps.CanScale * grow;
            can.Can.transform.localPosition = new Vector3(0f, 0f, BlockyProps.CanOffset * grow);
        }
    }
}
