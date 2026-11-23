using HideAndSeek.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// First-person fire extinguisher (same clock as <see cref="ExtTimeline"/>): the right hand reaches for the handle
    /// where it hangs (on the view ray) and lifts it into the lower right of the view - valve head and gauge towards the
    /// eyes, so the needle can be read - the left hand takes the nozzle; spraying raises the nozzle to the centre, squeezes
    /// the lever and shakes the hands with the jet. The first use pulls the pin with the left hand. Also the seeker
    /// wiping the foam off his face (<see cref="WipeHands"/>, FirstPersonViewModel.WipeFace.cs).
    /// </summary>
    public partial class FirstPersonViewModel
    {
        bool vxHold, vxPinIn = true, vxSpray, vxInHand;
        float vxT = 99f, vxPinT = 99f, vxCharge = 1f, vxSprayW, vxSqueeze, vxAtFrom;
        Vector3 vxFromPos, vxFrom;
        Quaternion vxFromRot = Quaternion.identity, vxModelRot = Quaternion.identity, vxNozzleRot = Quaternion.identity;
        ExtinguisherModel.Parts vxProp;

        static readonly Vector3 VxRest = new Vector3(0.3f, -0.85f, 0.42f);

        /// <summary>Every frame from the game (local player): see <see cref="BlockyCharacterActions.SetExtinguisher"/>.</summary>
        public void SetExtinguisher(bool hold, Vector3 fromPos, Quaternion fromRot, bool pinIn, float charge, bool spraying)
        {
            vxPinIn = pinIn;
            vxCharge = charge;
            vxSpray = hold && spraying;
            if (hold == vxHold) return;
            vxHold = hold;
            vxT = 0f;
            vxPinT = 99f;
            vxFromPos = fromPos;
            vxFromRot = fromRot;
            vxFrom = VxRest;
            if (!hold) vxInHand = false;
        }

        public void PlayExtPinPull() => vxPinT = 0f;

        /// <summary>
        /// Nozzle tip as the gameplay camera sees it (the view model has its own FOV) and the spray direction: the spray
        /// leaves the screen where the nozzle is drawn. False when the extinguisher is not in the view.
        /// </summary>
        public bool ExtNozzleScreen(out Vector3 tip, out Vector3 dir)
        {
            tip = dir = default;
            if (!vxInHand || vxProp == null || cam == null || !vxProp.Root.gameObject.activeInHierarchy) return false;
            Vector3 d = cam.InverseTransformPoint(ExtinguisherModel.TipWorld(vxProp));
            var gc = cam.GetComponent<Camera>();
            float fov = gc != null ? gc.fieldOfView : ViewModelFov;
            float k = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) / Mathf.Tan(ViewModelFov * 0.5f * Mathf.Deg2Rad);
            tip = cam.TransformPoint(new Vector3(d.x * k, d.y * k, d.z));
            dir = vxProp.Nozzle.forward;
            return true;
        }

        bool ExtinguisherHands(ref HeldItem show, out Vector3 rightT, out Quaternion propRot, ref Vector3 leftT, ref float wantLeft)
        {
            if (WipeHands(ref show, out rightT, out propRot, ref leftT, ref wantLeft)) { vxInHand = false; return true; }
            float dt = Time.deltaTime;
            vxT += dt;
            vxPinT += dt;
            vxSprayW = Mathf.Lerp(vxSprayW, vxSpray ? 1f : 0f, 1f - Mathf.Exp(-(vxSpray ? 10f : 4f) * dt));
            vxSqueeze = Mathf.Lerp(vxSqueeze, vxSpray ? 1f : 0f, 1f - Mathf.Exp(-16f * dt));
            if (!vxHold || rig == null) { vxInHand = false; return false; }

            var s = ExtinguisherModel.Styles[ExtinguisherModel.GameStyle];
            float aim = vxSprayW;
            float tr = aim * (0.004f + 0.002f * Mathf.Sin(time * 9f));
            Vector3 trem = new Vector3(Mathf.Sin(time * 67f), Mathf.Sin(time * 59f + 1f), Mathf.Sin(time * 43f + 2f)) * tr;
            Vector3 rHold = Vector3.Lerp(new Vector3(0.34f, -0.37f, 0.74f), new Vector3(0.31f, -0.33f, 0.77f), aim) + trem * 0.5f;
            Quaternion mHold = Quaternion.LookRotation(new Vector3(-0.8f, 0.3f, -0.55f).normalized, Vector3.up) * Quaternion.Euler(0f, 0f, 4f * aim);
            Vector3 lHold = Vector3.Lerp(new Vector3(-0.2f, -0.35f, 0.8f), new Vector3(-0.1f, -0.25f, 0.87f), aim) + trem;
            Quaternion nHold = Quaternion.Slerp(Quaternion.LookRotation(new Vector3(0.05f, -0.22f, 1f)), Quaternion.LookRotation(AimPoint - lHold), aim);

            Vector3 r = rHold, l = lHold;
            float lw = 1f;
            Quaternion mRot = mHold;
            vxAtFrom = 0f;
            if (vxT < ExtTimeline.Take)
            {
                Vector3 gripW = vxFromPos + vxFromRot * ExtinguisherModel.GripLocal(s);
                Vector3 at = RigOnScreen(gripW, 0.6f);
                Quaternion fromR = Quaternion.Inverse(rig.rotation) * vxFromRot;
                if (vxT < ExtTimeline.Grab) { r = Vector3.Lerp(vxFrom, at, ExtTimeline.S(vxT, 0.03f, ExtTimeline.Grab)); vxAtFrom = 1f; }
                else
                {
                    float k = ExtTimeline.S(vxT, ExtTimeline.Grab + 0.03f, ExtTimeline.Take);
                    r = Vector3.Lerp(at, rHold, k);
                    mRot = Quaternion.Slerp(fromR, mHold, k);
                }
                lw = ExtTimeline.S(vxT, ExtTimeline.Grab + 0.2f, ExtTimeline.Take);
                vxInHand = vxT >= ExtTimeline.Grab;
            }
            else vxInHand = true;

            if (vxPinT < ExtTimeline.PinDone && vxProp != null)
            {
                Vector3 ring = rig.InverseTransformPoint(vxProp.Root.TransformPoint(ExtinguisherModel.RingLocal(s)));
                Vector3 side = mRot * Vector3.right;
                float k1 = ExtTimeline.S(vxPinT, 0f, ExtTimeline.PinReach), k2 = ExtTimeline.S(vxPinT, ExtTimeline.PinReach, ExtTimeline.PinOut);
                float k3 = ExtTimeline.S(vxPinT, ExtTimeline.PinOut, ExtTimeline.PinThrow), k4 = ExtTimeline.S(vxPinT, ExtTimeline.PinThrow, ExtTimeline.PinDone);
                Vector3 atRing = ring + side * 0.06f + Vector3.back * 0.02f;
                Vector3 pulled = atRing + side * 0.1f + Vector3.up * 0.03f;
                Vector3 flick = new Vector3(-0.4f, -0.2f, 0.55f);
                Vector3 p = Vector3.Lerp(lHold, atRing, k1);
                p = Vector3.Lerp(p, pulled, k2);
                p = Vector3.Lerp(p, flick, k3);
                l = Vector3.Lerp(p, lHold, k4);
                // The right hand turns the head of the extinguisher a little towards the left hand.
                mRot = Quaternion.Slerp(mRot, mRot * Quaternion.Euler(0f, 18f, 0f), k1 * (1f - k4));
            }

            show = HeldItem.None;
            rightT = r;
            propRot = mRot;
            SolveArm(shR, elR, ShoulderR, r, new Vector3(1f, -1f, -0.4f));
            leftT = Vector3.Lerp(HiddenLeft, l, lw);
            wantLeft = 1f;
            vxModelRot = mRot;
            vxNozzleRot = nHold;
            return true;
        }

        /// <summary>After the arms are solved: the view-model extinguisher on the right fist, the nozzle in the left one.</summary>
        void UpdateVmExtinguisher()
        {
            bool on = vxInHand && visible && rig != null;
            if (on && vxProp == null)
            {
                vxProp = ExtinguisherModel.Create(rig, Layer, ShadowCastingMode.Off);
                foreach (var r in vxProp.Renderers) r.receiveShadows = false;
            }
            if (vxProp == null) return;
            if (vxProp.Root.gameObject.activeSelf != on) vxProp.Root.gameObject.SetActive(on);
            if (!on) return;
            var s = vxProp.Style;
            Vector3 pos = HandPos(shR, elR) - vxModelRot * ExtinguisherModel.GripLocal(s);
            Quaternion rot = vxModelRot;
            if (vxAtFrom > 0f && vxT < ExtTimeline.Grab)
            {
                pos = rig.InverseTransformPoint(vxFromPos);
                rot = Quaternion.Inverse(rig.rotation) * vxFromRot;
            }
            vxProp.Root.localPosition = pos;
            vxProp.Root.localRotation = rot;
            bool pinShown = vxPinIn || vxPinT < ExtTimeline.PinOut;
            ExtinguisherModel.SetPinIn(vxProp, pinShown);
            vxProp.Pin.localPosition = vxProp.Seal.localPosition = pinShown && vxPinT < ExtTimeline.PinOut
                ? Vector3.right * ExtTimeline.PinTravel * ExtTimeline.S(vxPinT, ExtTimeline.PinReach, ExtTimeline.PinOut) : Vector3.zero;
            ExtinguisherModel.SetCharge(vxProp, vxCharge);
            ExtinguisherModel.SetSqueeze(vxProp, vxSqueeze);
            bool nozzleInHand = vxT >= ExtTimeline.Grab + 0.35f && !(vxPinT > 0.05f && vxPinT < ExtTimeline.PinDone - 0.08f);
            if (!nozzleInHand) { ExtinguisherModel.RestHose(vxProp); return; }
            Vector3 fist = rig.TransformPoint(HandPos(shL, elL));
            Vector3 dir = rig.rotation * (vxNozzleRot * Vector3.forward);
            ExtinguisherModel.LayHose(vxProp, vxProp.Root.InverseTransformPoint(fist - dir * 0.045f), vxProp.Root.InverseTransformDirection(dir));
        }
    }
}
