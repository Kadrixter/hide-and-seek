using HideAndSeek.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// First-person side of the public-address microphone (same clock as <see cref="PaMicTimeline"/>): the right hand
    /// reaches out to the hook (on the view ray, at arm's length - the view-model arms are short and drawn with their
    /// own FOV), takes the mic and holds it low in the right of the view with the grille towards the mouth; the talk
    /// bar goes in and the mic comes a bit closer while talking. Put back = the reverse. The mic drawn here is a
    /// view-model copy; the game uses <see cref="PaMicCordScreenWorld"/> to attach the coiled cord to it.
    /// </summary>
    public partial class FirstPersonViewModel
    {
        bool vmPaHold, vmPaTalk, vmPaPut, vmPaInHand;
        float vmPaT = 99f, vmPaTalkW;
        Vector3 vmPaHookPos;
        Quaternion vmPaHookRot = Quaternion.identity;
        Vector3 vmPaFrom, vmPaLast;
        bool vmPaHasLast;
        PaMicModel.Parts vmPaMic;

        const float VmMicScale = 1.15f;
        static readonly Vector3 VmPaRest = new Vector3(0.34f, -0.82f, 0.42f);

        /// <summary>Every frame from the game (local player): see <see cref="BlockyCharacterActions.SetPaMic"/>.</summary>
        public void SetPaMic(bool hold, Vector3 hookPos, Quaternion hookRot, bool talking, bool canPose)
        {
            vmPaHookPos = hookPos;
            vmPaHookRot = hookRot;
            vmPaTalk = hold && talking;
            if (!canPose)
            {
                vmPaInHand = false;
                vmPaPut = false;
                vmPaHasLast = false;
                if (hold) vmPaT = 0f;
                vmPaHold = hold;
                return;
            }
            if (hold == vmPaHold) return;
            vmPaHold = hold;
            vmPaT = 0f;
            vmPaFrom = vmPaHasLast ? vmPaLast : VmPaRest;
            if (hold) return;
            vmPaPut = vmPaInHand && cam != null && new Vector2(cam.position.x - hookPos.x, cam.position.z - hookPos.z).magnitude <= PaMicTimeline.ReachAnimated;
            if (!vmPaPut) vmPaInHand = false;
        }

        /// <summary>True while the view-model mic is drawn in the hand.</summary>
        public bool PaMicVisible => vmPaInHand && vmPaMic != null && vmPaMic.Root.gameObject.activeInHierarchy;

        /// <summary>
        /// World point that the gameplay camera shows at the same spot of the screen as the strain relief of the
        /// view-model mic (the view model is drawn with its own FOV): the game ends the world cord there.
        /// </summary>
        public Vector3 PaMicCordScreenWorld
        {
            get
            {
                if (vmPaMic == null || cam == null) return Vector3.zero;
                Vector3 w = vmPaMic.Root.TransformPoint(PaMicModel.CordLocal);
                Vector3 d = cam.InverseTransformPoint(w);
                var gc = cam.GetComponent<Camera>();
                float fov = gc != null ? gc.fieldOfView : ViewModelFov;
                float k = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) / Mathf.Tan(ViewModelFov * 0.5f * Mathf.Deg2Rad);
                return cam.TransformPoint(new Vector3(d.x * k, d.y * k, d.z));
            }
        }

        /// <summary>Rig-space point at <paramref name="dist"/> on the view ray to <paramref name="world"/> (view-model FOV corrected).</summary>
        Vector3 RigOnScreen(Vector3 world, float dist)
        {
            if (cam == null || rig == null) return VmPaRest;
            Vector3 d = cam.InverseTransformPoint(world);
            if (d.z < 0.05f) d.z = 0.05f;
            var gc = cam.GetComponent<Camera>();
            float fov = gc != null ? gc.fieldOfView : ViewModelFov;
            float k = Mathf.Tan(ViewModelFov * 0.5f * Mathf.Deg2Rad) / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
            Vector3 s = new Vector3(d.x / d.z * k, d.y / d.z * k, 1f).normalized * dist;
            return rig.InverseTransformPoint(cam.TransformPoint(s));
        }

        /// <summary>Mic centre / rotation (rig space) held in front of the mouth.</summary>
        void VmPaHoldPose(out Vector3 centre, out Quaternion rot)
        {
            float talk = vmPaTalkW;
            centre = Vector3.Lerp(new Vector3(0.16f, -0.17f, 0.5f), new Vector3(0.13f, -0.14f, 0.46f), talk);
            rot = Quaternion.LookRotation(new Vector3(-0.3f, 0.22f, -1f).normalized, new Vector3(0.08f, 1f, 0.32f)) * Quaternion.Euler(0f, 0f, -5f * talk);
        }

        bool PaMicHands(ref HeldItem show, out Vector3 rightT, out Quaternion propRot, ref Vector3 leftT, ref float wantLeft)
        {
            rightT = default;
            propRot = Quaternion.identity;
            float dt = Time.deltaTime;
            vmPaT += dt;
            vmPaTalkW = Mathf.Lerp(vmPaTalkW, vmPaTalk ? 1f : 0f, 1f - Mathf.Exp(-10f * dt));
            bool active = vmPaHold || (vmPaPut && vmPaT < PaMicTimeline.Put);
            if (!active || rig == null)
            {
                vmPaInHand = false;
                vmPaHasLast = false;
                return false;
            }

            VmPaHoldPose(out Vector3 holdC, out Quaternion holdR);
            Vector3 holdHand = holdC + holdR * (PaMicModel.GripLocal * VmMicScale);
            Vector3 hook = RigOnScreen(vmPaHookPos + vmPaHookRot * new Vector3(0f, -0.04f, 0.08f), 0.62f);
            Quaternion hookR = Quaternion.Inverse(rig.rotation) * vmPaHookRot;
            float t = vmPaT;
            float S(float a, float b) => PaMicTimeline.S(t, a, b);
            Quaternion micR;
            if (vmPaHold)
            {
                if (t < PaMicTimeline.Grab)
                {
                    rightT = Vector3.Lerp(vmPaFrom, hook, S(0.04f, PaMicTimeline.Grab));
                    micR = hookR;
                }
                else
                {
                    vmPaInHand = true;
                    float k = S(PaMicTimeline.Grab + 0.04f, PaMicTimeline.Take);
                    rightT = Vector3.Lerp(hook, holdHand, k);
                    micR = Quaternion.Slerp(hookR, holdR, k);
                }
            }
            else
            {
                if (t < PaMicTimeline.Release)
                {
                    float k = S(0f, PaMicTimeline.Release - 0.04f);
                    rightT = Vector3.Lerp(vmPaFrom, hook, k);
                    micR = Quaternion.Slerp(holdR, hookR, k);
                }
                else
                {
                    vmPaInHand = false;
                    rightT = Vector3.Lerp(hook, VmPaRest, S(PaMicTimeline.Release + 0.05f, PaMicTimeline.Put));
                    micR = hookR;
                }
            }
            show = HeldItem.None;
            wantLeft = 0f;
            propRot = micR;
            SolveArm(shR, elR, ShoulderR, rightT, new Vector3(1f, -0.9f, -0.4f));
            vmPaLast = rightT;
            vmPaHasLast = true;
            return true;
        }

        /// <summary>After the arms are solved: the view-model mic in the fist (talk bar pressed while talking).</summary>
        void UpdateVmPaMic()
        {
            bool on = vmPaInHand && visible;
            if (on && vmPaMic == null && socketR != null)
            {
                vmPaMic = PaMicModel.Create(socketR, Layer, ShadowCastingMode.Off);
                foreach (var r in vmPaMic.Renderers) r.lightProbeUsage = LightProbeUsage.BlendProbes;
            }
            if (vmPaMic == null) return;
            if (vmPaMic.Root.gameObject.activeSelf != on) vmPaMic.Root.gameObject.SetActive(on);
            if (!on) return;
            vmPaMic.Root.localRotation = Quaternion.identity;
            vmPaMic.Root.localPosition = -PaMicModel.GripLocal * VmMicScale;
            vmPaMic.Root.localScale = Vector3.one * VmMicScale;
            PaMicModel.SetPressed(vmPaMic, vmPaTalkW);
        }
    }
}
