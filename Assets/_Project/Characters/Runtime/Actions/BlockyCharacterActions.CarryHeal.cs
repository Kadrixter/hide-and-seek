using HideAndSeek.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// A third player treating somebody who lies in another player's arms (medkit, H held): standing at the side of
    /// the carried body, leaning in, the left hand presses a pad on the belly, the right one passes the bandage roll over
    /// and round the belly in slow sweeps (never through the carrier's arms underneath), ties it off with a tug and pulls
    /// back. Hands are placed on the patient's real torso every frame. The patient looks down at it and winces when the
    /// pad goes on (<see cref="BodyCarryState.HealT"/>, BlockyCharacterActions.BodyCarry.cs).
    /// </summary>
    public partial class BlockyCharacterActions
    {
        public const float CarryHealPad = 0.9f, CarryHealWrapEnd = 4.0f, CarryHealTieEnd = 4.55f;

        float chT = -1f, chW;
        Transform chPatient;
        HeldProp chRoll;
        int chRollLayer = -1;
        bool chRollFp;

        /// <summary>Treating a carried player: <paramref name="t"/> = seconds into the hold (negative = off), call every frame.</summary>
        public void SetCarryHealing(float t, Transform patientTorso)
        {
            chT = t;
            chPatient = patientTorso;
        }

        /// <summary>The healer's arms reach the patient (tests / camera).</summary>
        public float CarryHealWeight => chW;

        void ApplyCarryHealer(ref CharacterPose p, float dt)
        {
            bool on = chT >= 0f && chPatient != null && VisualLifeState == LifeState.Alive && !pathActive && action == ActionKind.None;
            chW = Mathf.MoveTowards(chW, on ? 1f : 0f, dt / (on ? 0.6f : 0.7f));
            bool rollShown = false;
            if (chW > 0.001f && chPatient != null)
            {
                float t = Mathf.Max(0f, chT);
                // Patient's belly frame in this character's root space.
                Vector3 c = transform.InverseTransformPoint(chPatient.TransformPoint(new Vector3(0f, 0.2f, 0f)));
                Vector3 up = transform.InverseTransformDirection(chPatient.up);
                Vector3 fwd = transform.InverseTransformDirection(chPatient.forward);
                Vector3 side = transform.InverseTransformDirection(chPatient.right);
                // Use the belly side that faces us (the body may lie either way round).
                Vector3 toMe = new Vector3(0f, 1.2f, 0f) - c;
                if (Vector3.Dot(side, toMe) < 0f) side = -side;
                if (Vector3.Dot(fwd, Vector3.up) < -0.2f) fwd = -fwd;

                var k = p;
                Vector3 flat = new Vector3(c.x, 0f, c.z);
                float dist = flat.magnitude;
                float twist = Mathf.Clamp(Mathf.Atan2(c.x, c.z) * Mathf.Rad2Deg, -35f, 35f);
                float lean = Mathf.Clamp(5f + (dist - 0.5f) * 45f + (1.0f - c.y) * 25f, 4f, 17f);
                float br = Mathf.Sin(breathPhase);
                k.Torso = k.Torso * Quaternion.Euler(lean + 1.2f * br, twist * 0.6f, 0f);
                k.PelvisPos += new Vector3(0f, -0.03f, -0.02f);

                const float R = 0.19f;
                Vector3 pad = c + fwd * R;
                Vector3 restR = k.TorsoPoint(new Vector3(0.18f, 0.28f, 0.22f)), restL = k.TorsoPoint(new Vector3(-0.18f, 0.28f, 0.22f));
                Vector3 hr, hl;
                if (t < CarryHealPad)
                {
                    hl = Vector3.Lerp(restL, pad + side * 0.03f, S(t, 0.1f, CarryHealPad));
                    hr = Vector3.Lerp(restR, pad + side * 0.12f + up * 0.05f, S(t, 0f, CarryHealPad - 0.1f));
                }
                else if (t < CarryHealWrapEnd)
                {
                    // Slow sweeps over the top of the belly, a little further along the waist each time.
                    float u = (t - CarryHealPad) / 0.78f;
                    float swing = Mathf.Sin(u * Mathf.PI * 2f - Mathf.PI * 0.5f) * 0.5f + 0.5f; // 0..1..0
                    float a = Mathf.Lerp(-95f, 95f, swing) * Mathf.Deg2Rad;
                    float along = 0.05f * Mathf.Sin(u * Mathf.PI) - 0.03f;
                    hr = c + (fwd * Mathf.Cos(a) + side * Mathf.Sin(a)) * (R + 0.02f) + up * along;
                    hl = pad + side * 0.03f + fwd * (0.008f * Mathf.Sin(t * 9f));
                }
                else if (t < CarryHealTieEnd)
                {
                    float x = S(t, CarryHealWrapEnd, CarryHealTieEnd);
                    float tug = Mathf.Sin(x * Mathf.PI * 2f) * 0.03f;
                    hr = Vector3.Lerp(c + fwd * (R + 0.02f) + side * 0.1f, pad + side * 0.05f + up * 0.02f, x) + side * tug;
                    hl = pad - side * 0.04f - side * tug;
                }
                else
                {
                    float x = S(t, CarryHealTieEnd, BodyCarryTimeline.HealTime);
                    hr = Vector3.Lerp(pad + side * 0.05f, restR, x);
                    hl = Vector3.Lerp(pad - side * 0.04f, restL, x);
                }
                k.SolveArm(0, hr, k.TorsoRoot * new Vector3(1f, -0.6f, -0.3f));
                k.SolveArm(1, hl, k.TorsoRoot * new Vector3(-1f, -0.6f, -0.3f));
                k.Head = BodyCarryPoses.HeadToward(k, c - k.TorsoPoint(new Vector3(0f, 0.8f, 0f)));
                rollShown = t > 0.5f && t < CarryHealTieEnd - 0.2f && chW > 0.6f;
                k.PropR = Quaternion.Inverse(k.ForearmRoot(0)) * (k.TorsoRoot * Quaternion.Euler(0f, 90f, 0f));
                p = CharacterPose.Lerp(p, k, ActionTimeline.SmoothStep01(chW));
                if (rollShown) p.PropR = k.PropR;
            }
            UpdateCarryHealRoll(rollShown);
        }

        void UpdateCarryHealRoll(bool shown)
        {
            if (!shown)
            {
                if (chRoll != null && chRoll.gameObject.activeSelf) chRoll.gameObject.SetActive(false);
                return;
            }
            if (chRoll == null)
            {
                chRoll = BlockyProps.Create(HeldItem.Bandage, socketR, gameObject.layer);
                chRollLayer = -1;
            }
            bool fp = character.IsFirstPerson;
            if (chRollLayer != gameObject.layer || chRollFp != fp)
            {
                chRollLayer = gameObject.layer;
                chRollFp = fp;
                chRoll.SetLayer(chRollLayer);
                chRoll.SetRenderMode(fp ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On, true);
            }
            if (!chRoll.gameObject.activeSelf) chRoll.gameObject.SetActive(true);
        }
    }
}
