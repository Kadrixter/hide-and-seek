using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Shared clock of the vending machine purchase (seconds since the purchase started), used by the third-person
    /// pose, the first-person hands, the world can and the sounds:
    /// press the button (0-1.1), can drops into the tray (1.0-1.55), squat and reach in (1.3-2.7), stand up with the
    /// can (2.7-3.7), pop the tab with the left hand (3.7-5.0), drink with the head back (5.0-6.7), crush it (6.9-7.2),
    /// wind up and toss it over the shoulder (7.2-7.85).
    /// </summary>
    public static class VendingTimeline
    {
        public const float Drop = 1.0f, Land = 1.55f, Grip = 2.7f, Release = 7.85f;
        public const float TabPop = 4.5f, CrushAt = 7.0f;
        /// <summary>Gulp sound marks.</summary>
        public static readonly float[] Gulps = { 5.75f, 6.1f, 6.45f };

        public static float S(float t, float a, float b) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, t));
        public static float Crouch(float t) => S(t, 1.3f, 2.0f) * (1f - S(t, 2.85f, 3.6f));
        public static float Open(float t) => S(t, 4.05f, TabPop) * 0.6f + S(t, TabPop, 4.8f) * 0.4f;
        public static float Crush(float t) => S(t, CrushAt - 0.05f, CrushAt + 0.2f);
        /// <summary>0..1 how far the can is up at the mouth.</summary>
        public static float Drink(float t) => S(t, 5.0f, 5.45f) * (1f - S(t, 6.55f, 6.95f));
        /// <summary>Can tilt while drinking (degrees from upright, top towards the face): more as it empties.</summary>
        public static float Tilt(float t) => 50f * S(t, 5.0f, 5.45f) + 55f * S(t, 5.6f, 6.45f);
        public static bool Holding(float t) => t >= Grip && t < Release;
        /// <summary>Fist -> can direction (character space) while the can is held upright: the can sits left of the fist.</summary>
        public static readonly Vector3 HoldSide = new Vector3(-1f, 0f, 0.3f);
        public static Quaternion HoldRotation => Quaternion.LookRotation(HoldSide.normalized, Vector3.up);
        /// <summary>Can lying in the tray (axis across the body), gripped from above.</summary>
        public static Quaternion GripRotation => Quaternion.LookRotation(Vector3.down, Vector3.right);
    }

    public partial class BlockyCharacterActions
    {
        const float Px1 = BlockyCharacter.PixelSize;
        float vendingTime = -1f, vendW;
        bool vendingOn;
        int vendingBrand;
        Vector3 vendButton, vendTrayCan;

        /// <summary>
        /// Drives the purchase pose. <paramref name="time"/> &lt; 0 ends it (the pose fades out).
        /// Button and the can's resting point in the tray are world positions.
        /// </summary>
        public void SetVending(float time, Vector3 buttonWorld, Vector3 trayCanWorld, int brand)
        {
            EnsureInit();
            vendingOn = time >= 0f && time < VendingTimeline.Release;
            if (!vendingOn) return;
            vendingTime = time;
            vendingBrand = brand;
            vendButton = transform.InverseTransformPoint(buttonWorld);
            vendTrayCan = transform.InverseTransformPoint(trayCanWorld);
        }

        bool VendingShowsCan => vendingOn && VendingTimeline.Holding(vendingTime);

        static CharacterPose SquatPose()
        {
            var q = CharacterPose.Identity;
            q.PelvisPos = new Vector3(0f, 0.5f, -0.14f);
            q.Torso = Quaternion.Euler(34f, 0f, 0f);
            q.Head = Quaternion.Euler(16f, 0f, 0f);
            q.SolveLeg(0, new Vector3(0.12f, 0f, 0.06f), new Vector3(0.15f, 0f, 1f));
            q.SolveLeg(1, new Vector3(-0.12f, 0f, 0.06f), new Vector3(-0.15f, 0f, 1f));
            return q;
        }

        void ApplyVending(ref CharacterPose pose)
        {
            vendW = Mathf.MoveTowards(vendW, vendingOn ? 1f : 0f, Time.deltaTime / (vendingOn ? 0.25f : 0.4f));
            if (vendW <= 0.001f || vendingTime < 0f) return;
            float t = vendingTime;
            float S(float a, float b) => VendingTimeline.S(t, a, b);

            var v = CharacterPose.Lerp(pose, SquatPose(), VendingTimeline.Crouch(t));
            Vector3 r0 = pose.HandPos(0), l0 = pose.HandPos(1);

            // Head: at the buttons, down into the tray, at the can, back to drink, forward for the throw.
            float drink = VendingTimeline.Drink(t);
            float look = 12f * S(0f, 0.3f) * (1f - S(0.9f, 1.3f)) + 14f * S(2.8f, 3.6f) * (1f - S(4.8f, 5.1f));
            float gulp = drink * Mathf.Max(0f, Mathf.Sin((t - 5.6f) * 18f)) * S(5.6f, 5.8f) * 5f;
            float throwT = S(7.2f, 7.6f), fling = S(7.55f, VendingTimeline.Release);
            v.Head = v.Head * Quaternion.Euler(look - 30f * drink + gulp, -6f * throwT, 0f);
            v.Torso = v.Torso * Quaternion.Euler(-6f * drink + 8f * throwT - 14f * fling, 20f * throwT - 10f * fling, 0f);

            // Right hand.
            Vector3 toMe = new Vector3(-vendButton.x, 0f, -vendButton.z).normalized;
            Vector3 pre = vendButton + toMe * 0.14f, pressed = vendButton - toMe * 0.015f;
            Quaternion hold = VendingTimeline.HoldRotation, grip = VendingTimeline.GripRotation;
            Vector3 grab = vendTrayCan - grip * new Vector3(0f, 0f, BlockyProps.CanOffset);
            Vector3 chest = new Vector3(0.1f, 1.08f, 0.36f);
            Vector3 r;
            Quaternion canRot = hold;
            Vector3 push = Vector3.Lerp(pre, pressed, S(0.35f, 0.45f) * (1f - S(0.5f, 0.62f)));
            float pressW = S(0.05f, 0.35f) * (1f - S(0.65f, 1.2f));
            if (t < VendingTimeline.Grip)
            {
                r = Vector3.Lerp(r0, grab + Vector3.up * 0.06f * (1f - S(2.2f, 2.6f)), S(1.6f, 2.45f));
                canRot = grip;
            }
            else if (t < 5.0f)
            {
                float lift = S(2.75f, 3.65f);
                r = Vector3.Lerp(grab, chest, lift);
                canRot = Quaternion.Slerp(grip, hold, lift);
                r += Vector3.up * 0.02f * Mathf.Sin(Mathf.Max(0f, t - TabStart) * 25f) * S(4.4f, 4.55f) * (1f - S(4.6f, 4.8f));
            }
            else
            {
                // Drink: the can top goes to the mouth and tips up more and more.
                float tilt = VendingTimeline.Tilt(t) * Mathf.Deg2Rad;
                Quaternion tipped = Quaternion.LookRotation(Vector3.left, new Vector3(0f, Mathf.Cos(tilt), -Mathf.Sin(tilt)));
                Vector3 mouth = v.TorsoPoint(BlockyCharacter.NeckRest) + v.TorsoRoot * (v.Head * new Vector3(0f, 2.2f * Px1, 4.6f * Px1));
                Vector3 atMouth = mouth - tipped * BlockyProps.CanTopLocal;
                canRot = Quaternion.Slerp(hold, tipped, drink);
                r = Vector3.Lerp(chest, atMouth, drink);
                // Crush, then cock low in front and fling it up and back over the shoulder.
                float crushShake = VendingTimeline.Crush(t) * (1f - S(7.2f, 7.3f));
                r += new Vector3(0.01f, 0.01f, 0f) * Mathf.Sin(t * 70f) * crushShake;
                Vector3 cock = new Vector3(0.3f, 0.92f, 0.42f), over = new Vector3(0.3f, 1.8f, -0.2f);
                r = Vector3.Lerp(r, cock, throwT);
                r = Vector3.Lerp(r, over, fling);
                canRot = Quaternion.Slerp(canRot, Quaternion.LookRotation(Vector3.left, new Vector3(0f, 0.3f, -1f)), fling);
            }
            v.SolveArm(0, r, TorsoDir(v, 1f, -0.5f, -0.5f));
            v.PropR = Quaternion.Inverse(v.ForearmRoot(0)) * canRot;

            // Left hand: on the knee in the squat, over the can top to pop the tab.
            Vector3 l = Vector3.Lerp(l0, new Vector3(-0.2f, 0.58f, 0.3f), VendingTimeline.Crouch(t));
            l = Vector3.Lerp(l, push, pressW); // the keypad is on the left: the left hand presses
            float tabW = S(3.7f, 4.0f) * (1f - S(4.8f, 5.05f));
            if (tabW > 0f)
            {
                Vector3 top = v.HandPos(0) + canRot * BlockyProps.CanTopLocal;
                Vector3 onTab = top + new Vector3(0.02f, 0.1f + 0.035f * VendingTimeline.Open(t), 0.06f);
                l = Vector3.Lerp(l, onTab, tabW);
            }
            v.SolveArm(1, l, TorsoDir(v, -1f, -0.5f, -0.3f));

            pose = CharacterPose.Lerp(pose, v, vendW);
        }

        const float TabStart = 4.05f;

        void UpdateVendingProp(HeldItem show)
        {
            var can = props[(int)HeldItem.Can];
            if (can == null || show != HeldItem.Can) return;
            can.SetCan(vendingBrand, VendingTimeline.Open(vendingTime));
            can.Can.SetCrush(VendingTimeline.Crush(vendingTime));
        }
    }
}
