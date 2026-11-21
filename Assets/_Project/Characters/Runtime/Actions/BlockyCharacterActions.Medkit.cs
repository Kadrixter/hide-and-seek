using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Medkit on a wounded (limping) hider, third person: <see cref="HoldAnim.HealSelf"/> (kneel, kit on the floor,
    /// pad on the own wound, bandage round the belly) and <see cref="HoldAnim.HealOther"/> (kneel in front of the other
    /// hider and bandage their waist). The treated player gets a "patient" layer (<see cref="SetPatient"/>): arms out to
    /// the sides, head down, a wince when the pad goes on. Timing: <see cref="HealTimeline"/> (Med*).
    /// </summary>
    public partial class BlockyCharacterActions
    {
        float medW, medWrap;
        float patientT, patientW, patientUntil = -1f;
        Vector3 medTargetLocal = new Vector3(0f, 0.95f, 0.6f);
        float medStep;

        /// <summary>Treated by someone else's medkit (call every frame while it runs); <paramref name="t"/> = their hold time.</summary>
        public void SetPatient(float t)
        {
            EnsureInit();
            patientT = t;
            patientUntil = time + 0.25f;
        }

        void AdvanceMedkit(float dt)
        {
            bool alive = VisualLifeState == LifeState.Alive && action == ActionKind.None;
            bool on = HealTimeline.IsMedkit(holdAnim) && alive;
            medW = Mathf.MoveTowards(medW, on ? 1f : 0f, dt / (on ? 0.25f : 0.35f));
            bool patient = time < patientUntil && alive;
            patientW = Mathf.MoveTowards(patientW, patient ? 1f : 0f, dt / 0.3f);
            float own = on && holdAnim == HoldAnim.HealSelf ? HealTimeline.MedBandage(holdTime) : 0f;
            float other = patient ? HealTimeline.MedBandage(patientT) : 0f;
            medWrap = Mathf.Max(own, other);
        }

        HeldItem MedkitShownItem()
        {
            float t = holdTime;
            if (t < HealTimeline.MedKitDown - 0.05f || t >= HealTimeline.MedTieEnd + 0.15f) return HeldItem.Medkit;
            if (t >= HealTimeline.MedKitOpen && t < HealTimeline.MedWrapEnd + 0.15f) return HeldItem.Bandage;
            return HeldItem.None;
        }

        /// <summary>Hook in ApplyLayer (after the injection layer).</summary>
        void ApplyMedkitLayers(ref CharacterPose p, LifeState visual)
        {
            if (visual != LifeState.Alive || pathActive) return;
            if (medW > 0.001f)
            {
                var mp = MedkitPose(p, holdTime, holdAnim == HoldAnim.HealOther);
                p = CharacterPose.Lerp(p, mp, ActionTimeline.SmoothStep01(medW));
                p.PropR = mp.PropR;
            }
            if (patientW > 0.001f) ApplyPatient(ref p, ActionTimeline.SmoothStep01(patientW));
        }

        /// <summary>World chest of the treated player (HealOther): the belly is a little below it.</summary>
        void SetMedTarget(Vector3? world)
        {
            if (!world.HasValue) return;
            Vector3 l = transform.InverseTransformPoint(world.Value) + new Vector3(0f, -0.28f, 0f);
            l.x = Mathf.Clamp(l.x, -0.2f, 0.2f);
            l.y = Mathf.Clamp(l.y, 0.75f, 1.1f);
            // Further away than the arms reach: kneel down closer (up to 0.4 m).
            medStep = Mathf.Clamp(l.z - 0.6f, 0f, 0.4f);
            l.z = Mathf.Clamp(l.z, 0.42f, 0.62f + medStep);
            medTargetLocal = l;
        }

        CharacterPose MedkitPose(in CharacterPose current, float t, bool other)
        {
            float S(float a, float b) => HealTimeline.Seg(t, a, b);
            float k = S(0f, HealTimeline.MedKneel) * (1f - 0.55f * S(HealTimeline.MedTieEnd + 0.15f, HealTimeline.MedkitDuration));
            float wrapping = S(HealTimeline.MedPad - 0.1f, HealTimeline.MedPad) * (1f - S(HealTimeline.MedWrapEnd, HealTimeline.MedWrapEnd + 0.1f));
            float wrapT = Mathf.Clamp01((t - HealTimeline.MedPad) / (HealTimeline.MedWrapEnd - HealTimeline.MedPad));
            float cyc = wrapT * 3f, ph = cyc - Mathf.Floor(cyc);
            float sway = wrapping * Mathf.Sin(ph * Mathf.PI * 2f);

            // Down on the right knee, left foot planted forward (as for the injection).
            var p = CharacterPose.Identity;
            p.PropR = current.PropR;
            float step = other ? medStep * k : 0f;
            p.PelvisPos = Vector3.Lerp(new Vector3(0f, 0.64f, 0f), new Vector3(0.02f, 0.47f, -0.2f), k) + new Vector3(0f, 0f, step);
            p.Pelvis = Quaternion.Euler(0f, 6f * k, 0f);
            float lean = other ? 8f + 4f * Mathf.Abs(sway) : 14f + 5f * Mathf.Abs(sway);
            float reachKit = S(0.35f, 0.55f) * (1f - S(HealTimeline.MedRollOut - 0.15f, HealTimeline.MedRollOut))
                             + S(HealTimeline.MedTieEnd, HealTimeline.MedTieEnd + 0.12f) * (1f - S(3.25f, 3.45f));
            p.Torso = Quaternion.Euler(Mathf.Lerp(2f, lean, k) + 10f * reachKit, (other ? 6f : 8f) * sway - 10f * reachKit, 3f * sway);
            p.Head = Quaternion.Euler(other ? 4f + 10f * reachKit : 30f + 6f * reachKit, -8f * sway, 0f);
            Vector3 fs = new Vector3(0f, 0f, step);
            p.SolveLeg(0, Vector3.Lerp(new Vector3(0.13f, 0f, 0f), new Vector3(0.14f, 0.07f, -0.62f), k) + fs, new Vector3(0.1f, -0.4f, 1f));
            p.SolveLeg(1, Vector3.Lerp(new Vector3(-0.13f, 0f, 0f), new Vector3(-0.15f, 0f, 0.16f), k) + fs, new Vector3(-0.05f, 0.3f, 1f));

            // Wrap circle: own belly (torso space) or the other hider's waist (root space, front surface towards us).
            Vector3 W(float ang, float r = 1f)
            {
                if (!other) return p.TorsoPoint(new Vector3(0.21f * Mathf.Sin(ang) * r, 0.2f + 0.015f * Mathf.Sin(ang * 3f), 0.15f * Mathf.Cos(ang) * r + 0.03f));
                Vector3 c = medTargetLocal + new Vector3(0f, 0f, 0.13f);
                return c + new Vector3(0.22f * Mathf.Sin(ang) * r, 0.015f * Mathf.Sin(ang * 3f), -0.14f * Mathf.Cos(ang) * r - 0.03f);
            }
            Vector3 hip = p.TorsoPoint(new Vector3(0.3f, 0.06f, 0.02f));
            Vector3 kit = new Vector3(0.34f, 0.2f, 0.3f + step);
            Vector3 knee = p.TorsoPoint(new Vector3(-0.16f, 0.12f, 0.3f));
            Vector3 r, l;
            if (t < HealTimeline.MedKneel) { r = Vector3.Lerp(current.HandPos(0), hip, S(0f, 0.3f)); l = Vector3.Lerp(current.HandPos(1), knee, k); }
            else if (t < HealTimeline.MedKitDown) { r = Vector3.Lerp(hip, kit, S(HealTimeline.MedKneel, 0.62f)); l = knee; }
            else if (t < HealTimeline.MedKitOpen)
            {
                float lid = Mathf.Sin(Mathf.PI * ActionTimeline.Remap01(t, HealTimeline.MedKitDown, HealTimeline.MedKitOpen));
                r = kit + new Vector3(0f, 0.02f, 0f);
                l = Vector3.Lerp(knee, kit + new Vector3(-0.06f, 0.1f + 0.1f * lid, 0.04f * lid), S(HealTimeline.MedKitDown, 0.8f));
            }
            else if (t < HealTimeline.MedPad)
            {
                // Roll out of the kit; the left hand presses the pad onto the wound.
                r = Vector3.Lerp(kit, W(0.7f), S(HealTimeline.MedKitOpen, HealTimeline.MedRollOut + 0.1f));
                Vector3 pad = other ? W(-0.15f, 0.92f) : W(-0.45f, 0.92f);
                l = Vector3.Lerp(kit + new Vector3(-0.06f, 0.12f, 0f), pad, S(HealTimeline.MedKitOpen + 0.05f, HealTimeline.MedRollOut + 0.08f));
                l += (other ? new Vector3(0f, 0f, 0.02f) : Vector3.zero) * S(HealTimeline.MedRollOut, HealTimeline.MedPad);
            }
            else if (t < HealTimeline.MedWrapEnd)
            {
                // Right hand carries the roll round (front -> side -> back), the left one takes it over at the other side.
                float reach = other ? 1.75f : 2.0f;
                float a = ph < 0.6f ? Mathf.Lerp(-0.9f, reach, ActionTimeline.SmoothStep01(ph / 0.6f)) : Mathf.Lerp(reach, -0.9f, ActionTimeline.SmoothStep01((ph - 0.6f) / 0.4f));
                r = W(a, ph < 0.6f ? 1f : 1.4f);
                float b = ph < 0.6f ? -0.5f : Mathf.Lerp(-0.5f, -reach, Mathf.Sin(Mathf.PI * (ph - 0.6f) / 0.4f));
                l = W(b);
            }
            else if (t < HealTimeline.MedTieEnd)
            {
                float pull = Mathf.Abs(Mathf.Sin(ActionTimeline.Remap01(t, HealTimeline.MedWrapEnd, HealTimeline.MedTieEnd) * Mathf.PI * 2f));
                Vector3 f = W(0f, 1.15f);
                Vector3 side = other ? Vector3.right : p.TorsoRoot * Vector3.right;
                r = f + side * (0.03f + 0.15f * pull) + Vector3.up * 0.01f;
                l = f - side * (0.03f + 0.15f * pull);
            }
            else
            {
                // Pick the kit up, left hand back on the knee, start to rise.
                r = Vector3.Lerp(W(0f, 1.15f), kit, S(HealTimeline.MedTieEnd, 3.22f));
                r = Vector3.Lerp(r, hip, S(3.25f, HealTimeline.MedkitDuration));
                l = Vector3.Lerp(W(0f, 1.15f), knee, S(HealTimeline.MedTieEnd, 3.3f));
            }
            p.SolveArm(0, r, TorsoDir(p, 1f, -0.3f, -0.6f));
            p.SolveArm(1, l, TorsoDir(p, -1f, -0.3f, -0.6f));
            // Prop: medkit hangs from the fist; the roll is held across the palm.
            var shown = MedkitShownItem();
            p.PropR = shown == HeldItem.Bandage
                ? Quaternion.Inverse(p.ForearmRoot(0)) * (p.TorsoRoot * Quaternion.Euler(0f, 90f, 0f))
                : Quaternion.Inverse(p.ForearmRoot(0)) * Quaternion.identity;
            return p;
        }

        /// <summary>The treated hider: arms out to the sides, looking down at the belly, a wince when the pad goes on.</summary>
        void ApplyPatient(ref CharacterPose p, float w)
        {
            float t = patientT;
            float raise = HealTimeline.Seg(t, 0.85f, 1.15f) * (1f - HealTimeline.Seg(t, HealTimeline.MedTieEnd - 0.1f, HealTimeline.MedTieEnd + 0.25f));
            float wince = Pulse(t, HealTimeline.MedRollOut + 0.05f, 1.2f) + 0.5f * Pulse(t, HealTimeline.MedWrapEnd + 0.15f, 1f);
            float look = HealTimeline.Seg(t, 0.4f, 0.8f) * (1f - HealTimeline.Seg(t, 3.1f, 3.5f));
            var a = p;
            a.Torso = p.Torso * Quaternion.Euler(-4f * raise + 6f * wince, 0f, 0f);
            a.Head = Quaternion.Euler(26f * look + 10f * wince, 0f, 6f * wince);
            float br = Mathf.Sin(time * 5f) * 0.012f;
            a.SolveArm(0, a.TorsoPoint(new Vector3(0.44f, 0.6f + br, 0.08f)), TorsoDir(a, 1f, 0.4f, -0.4f));
            a.SolveArm(1, a.TorsoPoint(new Vector3(-0.44f, 0.6f - br, 0.08f)), TorsoDir(a, -1f, 0.4f, -0.4f));
            CharacterPose.LerpArm(ref p, a, 0, w * raise);
            CharacterPose.LerpArm(ref p, a, 1, w * raise);
            p.Torso = Quaternion.Slerp(p.Torso, a.Torso, w);
            p.Head = Quaternion.Slerp(p.Head, a.Head, w * Mathf.Max(look, wince));
        }
    }
}
