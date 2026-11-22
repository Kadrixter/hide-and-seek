using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// First-person hands for the lobby flashlight box (same clock as <see cref="FlashTakeTimeline"/>, driven by the
    /// game): the left hand reaches into the box, takes a lamp and brings it to the right shoulder (the game tilts the
    /// camera down and to the right meanwhile), or the reverse. The lamp drawn here is a view-model copy.
    /// </summary>
    public partial class FirstPersonViewModel
    {
        float fpFlashT = -1f;
        bool fpFlashTake;
        Vector3 fpFlashGrabW;
        Transform fpLamp;
        bool fpLampOn;

        /// <summary>Drives the box animation; <paramref name="time"/> &lt; 0 ends it.</summary>
        public void SetFlashTake(bool take, float time, Vector3 grabWorld)
        {
            fpFlashTake = take;
            fpFlashT = time < FlashTakeTimeline.Duration(take) ? time : -1f;
            fpFlashGrabW = grabWorld;
        }

        bool FlashTakeHands(ref HeldItem show, out Vector3 rightT, out Quaternion propRot, ref Vector3 leftT, ref float wantLeft)
        {
            rightT = default;
            propRot = Quaternion.identity;
            fpLampOn = false;
            if (fpFlashT < 0f || rig == null) return false;
            float t = fpFlashT, dur = FlashTakeTimeline.Duration(fpFlashTake);
            float S(float a, float b) => FlashTakeTimeline.S(t, a, b);
            bool take = fpFlashTake;

            Quaternion toRig = BodyToRig();
            Vector3 lamp = toRig * FlashTakeTimeline.LampEyeOffset;
            Vector3 front = lamp + toRig * new Vector3(-0.12f, -0.08f, 0.2f);
            Vector3 press = Vector3.Lerp(front, lamp, 0.25f);
            Vector3 strap = lamp + toRig * new Vector3(-0.07f, -0.17f, 0.17f);
            Vector3 grab = rig.InverseTransformPoint(fpFlashGrabW);
            Vector3 above = grab + toRig * new Vector3(0f, 0.14f, -0.04f);
            Vector3 rest = new Vector3(-0.3f, -0.85f, 0.45f);

            Vector3 l;
            if (take)
            {
                if (t < 0.32f) l = Vector3.Lerp(rest, above, S(0.02f, 0.32f));
                else if (t < FlashTakeTimeline.TakeGrab) l = Vector3.Lerp(above, grab, S(0.3f, 0.48f));
                else if (t < 1.0f) l = Vector3.Lerp(grab, front, S(0.55f, 1.0f)) + toRig * new Vector3(0f, 0.08f * Mathf.Sin(Mathf.PI * S(0.55f, 1.0f)), 0f);
                else if (t < 1.3f) l = Vector3.Lerp(front, press, S(1.0f, 1.18f));
                else if (t < 1.55f) l = Vector3.Lerp(press, strap, S(1.3f, 1.42f));
                else l = Vector3.Lerp(strap, rest, S(1.55f, 1.95f));
            }
            else
            {
                if (t < FlashTakeTimeline.PutUnclip) l = Vector3.Lerp(rest, press, S(0.02f, 0.36f));
                else if (t < FlashTakeTimeline.PutRelease) l = Vector3.Lerp(press, grab, S(0.45f, 0.95f)) + toRig * new Vector3(0f, 0.1f * Mathf.Sin(Mathf.PI * S(0.45f, 0.95f)), 0f);
                else l = Vector3.Lerp(grab, rest, S(1.05f, 1.5f));
            }
            leftT = l;
            wantLeft = S(0f, 0.12f) * (1f - S(dur - 0.25f, dur));

            // Right hand drops out of the way.
            rightT = Vector3.Lerp(new Vector3(0.28f, -0.4f, 0.68f), new Vector3(0.34f, -0.82f, 0.42f), S(0f, 0.25f) * (1f - S(dur - 0.35f, dur)));
            show = HeldItem.None;
            SolveArm(shR, elR, ShoulderR, rightT, new Vector3(1f, -1f, -0.4f));

            int state = FlashTakeTimeline.LampState(take, t, out _);
            fpLampOn = state == 1 || (state == 2 && (take ? t < dur - 0.2f : true));
            return true;
        }

        /// <summary>After both arms are solved: the view-model lamp in the left fist / on the shoulder.</summary>
        void UpdateFlashLamp()
        {
            if (fpLampOn && fpLamp == null)
            {
                fpLamp = new GameObject("FpLamp").transform;
                fpLamp.SetParent(rig, false);
                fpLamp.gameObject.layer = Layer;
                LampModel.Build(fpLamp, false, Layer, false);
            }
            if (fpLamp == null) return;
            if (fpLamp.gameObject.activeSelf != fpLampOn) fpLamp.gameObject.SetActive(fpLampOn);
            if (!fpLampOn) return;
            int state = FlashTakeTimeline.LampState(fpFlashTake, fpFlashT, out float toShoulder);
            Quaternion toRig = BodyToRig();
            Vector3 shoulderPos = toRig * FlashTakeTimeline.LampEyeOffset;
            Quaternion shoulderRot = toRig;
            Quaternion arm = shL.localRotation * elL.localRotation;
            Vector3 handPos = shL.localPosition + shL.localRotation * BlockyCharacter.ElbowRest + arm * FlashTakeTimeline.LampInHandLocal;
            Quaternion handRot = arm * FlashTakeTimeline.LampInHandRot;
            float s = state == 2 ? 1f : toShoulder;
            fpLamp.localPosition = Vector3.Lerp(handPos, shoulderPos, s);
            fpLamp.localRotation = Quaternion.Slerp(handRot, shoulderRot, s);
        }
    }
}
