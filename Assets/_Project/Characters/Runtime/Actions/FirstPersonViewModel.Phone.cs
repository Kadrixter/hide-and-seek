using HideAndSeek.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// First-person side of the toy phone smash (same clock as <see cref="PhoneTimeline"/>): the right hand brings the
    /// ringing phone up in front of the camera, winds up and throws it down out of view; then a leg (thigh, shin, boot -
    /// a local-only world object, the own body only casts shadows) comes into view from below and stomps on the phone
    /// lying on the floor. The seeker gets only the stomp.
    /// </summary>
    public partial class FirstPersonViewModel
    {
        float vmPhoneT = -1f;
        bool vmPhoneThrow;
        Vector3 vmPhoneLand;
        PhoneModel.Parts vmPhone;
        Transform fpLeg, fpShin, fpShoe;

        /// <summary>Drives the first-person smash; <paramref name="t"/> &lt; 0 ends it.</summary>
        public void SetPhoneSmash(float t, bool throwFirst, Vector3 landWorld)
        {
            vmPhoneT = t < PhoneTimeline.End ? t : -1f;
            vmPhoneThrow = throwFirst;
            vmPhoneLand = landWorld;
        }

        bool PhoneHands(ref HeldItem show, out Vector3 rightT, out Quaternion propRot, ref Vector3 leftT, ref float wantLeft)
        {
            rightT = default;
            propRot = Quaternion.identity;
            UpdateFpLeg();
            if (vmPhoneT < 0f || rig == null)
            {
                ShowVmPhone(false);
                return false;
            }
            float t = vmPhoneT;
            float S(float a, float b) => PhoneTimeline.Seg(t, a, b);
            show = HeldItem.None;
            wantLeft = 0f;
            Vector3 hidden = new Vector3(0.36f, -0.9f, 0.28f);
            if (!vmPhoneThrow || t >= PhoneTimeline.Release + 0.3f)
            {
                rightT = hidden;
                ShowVmPhone(false);
                SolveArm(shR, elR, ShoulderR, rightT, new Vector3(1f, -1f, -0.4f));
                return true;
            }
            float look = S(0.08f, 0.42f), wind = S(0.46f, 0.62f), thr = S(0.62f, 0.78f), back = S(PhoneTimeline.Release, PhoneTimeline.Release + 0.3f);
            Vector3 view = new Vector3(0.16f, -0.36f, 0.72f);
            Vector3 up = new Vector3(0.42f, 0.04f, 0.26f);
            Vector3 down = new Vector3(0.18f, -1.0f, 0.55f);
            rightT = Vector3.Lerp(Vector3.Lerp(Vector3.Lerp(hidden, view, look), up, wind), down, thr);
            rightT += new Vector3(0.004f, 0.003f, 0f) * Mathf.Sin(t * 60f) * look * (1f - wind); // buzzing in the hand
            rightT = Vector3.Lerp(rightT, hidden, back);
            // Screen to the camera, top up; over the shoulder the top points back; thrown nose down.
            Quaternion lookRot = Quaternion.LookRotation(new Vector3(-0.1f, 0.95f, 0.3f), new Vector3(0f, 0.3f, -0.95f));
            Quaternion windRot = Quaternion.LookRotation(new Vector3(0.2f, 0.5f, -0.85f), new Vector3(0f, 0.85f, 0.5f));
            Quaternion throwRot = Quaternion.LookRotation(new Vector3(0f, -0.75f, 0.65f), new Vector3(0f, 0.65f, 0.75f));
            propRot = Quaternion.Slerp(Quaternion.Slerp(Quaternion.Slerp(Quaternion.LookRotation(Vector3.up, Vector3.back), lookRot, look), windRot, wind), throwRot, thr);
            ShowVmPhone(t >= 0.1f && t < PhoneTimeline.Release);
            SolveArm(shR, elR, ShoulderR, rightT, new Vector3(1f, -0.8f, -0.5f));
            return true;
        }

        void ShowVmPhone(bool on)
        {
            if (on && vmPhone == null && socketR != null)
            {
                vmPhone = PhoneModel.Create(socketR, Layer);
                vmPhone.Root.localPosition = new Vector3(0f, -PhoneModel.Thick * 0.5f * PhoneTimeline.HandScale, 0.12f);
                vmPhone.Root.localScale = Vector3.one * PhoneTimeline.HandScale;
                foreach (var r in vmPhone.Renderers)
                {
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    r.receiveShadows = false;
                }
            }
            if (vmPhone == null) return;
            if (vmPhone.Root.gameObject.activeSelf != on) vmPhone.Root.gameObject.SetActive(on);
            if (on) PhoneModel.SetScreen(vmPhone, Mathf.Repeat(Time.time * 3f, 1f) < 0.5f);
        }

        /// <summary>The stomping leg seen from the own eyes (same sole curve as the third-person stomp).</summary>
        void UpdateFpLeg()
        {
            float t = vmPhoneT;
            bool on = t >= PhoneTimeline.StompStart && t < PhoneTimeline.End - 0.15f && cam != null;
            if (!on)
            {
                if (fpLeg != null && fpLeg.gameObject.activeSelf) fpLeg.gameObject.SetActive(false);
                return;
            }
            if (fpLeg == null) BuildFpLeg();
            if (!fpLeg.gameObject.activeSelf) fpLeg.gameObject.SetActive(true);

            Transform body = cam.root;
            Vector3 ph = body.InverseTransformPoint(vmPhoneLand);
            ph = new Vector3(Mathf.Clamp(ph.x, -0.2f, 0.35f), 0f, Mathf.Clamp(ph.z, 0.25f, 0.7f));
            Vector3 sole = PhoneTimeline.StompSole(t, ph, out float pelvisZ, out float drop, out _);
            Vector3 hip = new Vector3(0.1f, BlockyCharacter.HipHeight - 0.02f - drop, pelvisZ);
            RigIK.SolveLeg(hip, Quaternion.identity, sole, new Vector3(0.1f, 0.3f, 1f), out var hipRot, out var kneeRot);
            Quaternion shinRot = hipRot * kneeRot;
            Vector3 knee = hip + hipRot * new Vector3(0f, -RigIK.Thigh, 0f);
            Place(fpShin, body, knee + shinRot * new Vector3(0f, -RigIK.Shin * 0.5f, 0f), shinRot);
            Place(fpShoe, body, knee + shinRot * new Vector3(0f, -RigIK.Shin + 0.055f, 0.035f), shinRot);
        }

        static void Place(Transform part, Transform body, Vector3 local, Quaternion rot) =>
            part.SetPositionAndRotation(body.TransformPoint(local), body.rotation * rot);

        void BuildFpLeg()
        {
            fpLeg = new GameObject("FpStompLeg").transform;
            const float w = 4f * Px;
            fpShin = LegBox("Shin", new Vector3(w - 0.03f, RigIK.Shin, w - 0.03f), new Color(0.28f, 0.36f, 0.58f));
            fpShoe = LegBox("Shoe", new Vector3(w - 0.01f, 0.11f, w + 0.05f), new Color(0.12f, 0.11f, 0.1f));
            var sole = LegBox("Sole", new Vector3(1.02f, 0.22f, 1.02f), new Color(0.62f, 0.6f, 0.56f));
            sole.SetParent(fpShoe, false);
            sole.localPosition = new Vector3(0f, -0.45f, 0f);
        }

        Transform LegBox(string name, Vector3 size, Color c)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name;
            Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(fpLeg, false);
            g.transform.localScale = size;
            var r = g.GetComponent<MeshRenderer>();
            var sh = Shader.Find("Standard");
            r.material = new Material(sh != null ? sh : Shader.Find("Diffuse")) { color = c };
            r.material.SetFloat("_Glossiness", 0.15f);
            r.shadowCastingMode = ShadowCastingMode.Off;
            return g.transform;
        }
    }
}
