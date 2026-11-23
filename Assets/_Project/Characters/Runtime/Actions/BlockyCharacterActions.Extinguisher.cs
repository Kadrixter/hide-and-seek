using HideAndSeek.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Fire extinguisher in both hands (third person). Taking it: the right hand reaches for the carry handle where the
    /// extinguisher hangs (or lies - the body crouches for a floor pickup), lifts it into the hold: the fist on the
    /// handle in front of the right hip, the tank hanging under it, the left hand holding the nozzle forward. While
    /// spraying the nozzle comes up to the look direction, the lever is squeezed and everything trembles with the jet.
    /// The first use pulls the pin: the left hand goes to the ring, draws the pin out of the valve and flicks it away
    /// (the game throws a loose copy at <see cref="ExtTimeline.PinOut"/>). Driven every frame by the game through
    /// <see cref="SetExtinguisher"/>; when it leaves the hands the game animates its world copy from <see cref="ExtReleasePos"/>.
    /// </summary>
    public partial class BlockyCharacterActions
    {
        bool exHold, exPinIn = true, exSpray, exInHand;
        float exT = 99f, exW, exSprayW, exSqueeze, exPinT = 99f, exCharge = 1f;
        Vector3 exFromPos;                 // world: model root pose to take it from (wall hook / floor)
        Quaternion exFromRot = Quaternion.identity;
        Vector3 exReach;                   // root space: where the right fist was when the take started
        ExtinguisherModel.Parts exProp;
        int exLayer = -1;
        bool exFp;
        Quaternion exModelRootRot = Quaternion.identity;   // root space rotation of the model in the hold
        Quaternion exNozzleRootRot = Quaternion.identity;
        float exAtFrom;

        /// <summary>The extinguisher is drawn in this character's hands (the world copy is hidden).</summary>
        public bool ExtInHand => exInHand;
        /// <summary>The hand copy (null when not shown): nozzle tip for the spray, gauge, etc.</summary>
        public ExtinguisherModel.Parts ExtProp => exInHand ? exProp : null;
        /// <summary>World pose of the hand copy in its last shown frame (the dropped copy starts from it).</summary>
        public Vector3 ExtReleasePos { get; private set; }
        public Quaternion ExtReleaseRot { get; private set; } = Quaternion.identity;
        /// <summary>World position of the pull ring right now (the loose pin starts there).</summary>
        public Vector3 ExtRingWorld => exProp != null ? exProp.Root.TransformPoint(ExtinguisherModel.RingLocal(exProp.Style)) : transform.position;

        /// <summary>
        /// Every frame from the game: <paramref name="hold"/> = this player holds an extinguisher; <paramref name="fromPos"/> /
        /// <paramref name="fromRot"/> = world pose of the model it is taken from (used when the hold starts); pin still in,
        /// charge 0..1 (gauge), spraying now.
        /// </summary>
        public void SetExtinguisher(bool hold, Vector3 fromPos, Quaternion fromRot, bool pinIn, float charge, bool spraying)
        {
            EnsureInit();
            exPinIn = pinIn;
            exCharge = charge;
            exSpray = hold && spraying;
            if (hold == exHold) return;
            exHold = hold;
            exT = 0f;
            exPinT = 99f;
            exFromPos = fromPos;
            exFromRot = fromRot;
            exReach = hasLast ? lastOutput.HandPos(0) : new Vector3(0.38f, 0.75f, 0.05f);
            if (!hold && exInHand) { exInHand = false; ShowExt(false); }
        }

        /// <summary>The first use: pull the safety pin (everybody plays it from the server event).</summary>
        public void PlayExtPinPull() => exPinT = 0f;

        /// <summary>Hard reset (scene change).</summary>
        public void ClearExtinguisher()
        {
            exHold = exInHand = exSpray = false;
            exT = exPinT = 99f;
            exW = 0f;
            ShowExt(false);
        }

        bool ExtHandsBusy => exHold || exW > 0.02f;

        void ApplyExtLayers(ref CharacterPose p, float dt, bool standing, float lookPitch)
        {
            ApplyExtinguisher(ref p, dt, standing, lookPitch);
            ApplyWipeFace(ref p, dt, standing);
        }

        void ApplyExtinguisher(ref CharacterPose p, float dt, bool standing, float lookPitch)
        {
            exT += dt;
            exPinT += dt;
            bool canPose = exHold && VisualLifeState == LifeState.Alive && !pathActive && action == ActionKind.None && sitW < 0.01f;
            exW = Mathf.MoveTowards(exW, canPose ? 1f : 0f, dt / (canPose ? 0.12f : 0.25f));
            exSprayW = Damp(exSprayW, exSpray && canPose ? 1f : 0f, exSpray ? 9f : 4f, dt);
            exSqueeze = Damp(exSqueeze, exSpray ? 1f : 0f, 16f, dt);
            if (!canPose) { if (exInHand) { exInHand = false; ShowExt(false); } }
            if (exW <= 0.001f) return;

            var q = p;
            var s = ExtinguisherModel.Styles[ExtinguisherModel.GameStyle];
            float aim = Mathf.Max(exSprayW, 0.25f);
            float look = Mathf.Clamp(lookPitch, -45f, 50f);
            float tr = exSprayW * (0.006f + 0.004f * Mathf.Sin(time * 7f));
            Vector3 trem = new Vector3(Mathf.Sin(time * 61f), Mathf.Sin(time * 53f + 1f), Mathf.Sin(time * 47f + 2f)) * tr;
            float breath = Mathf.Sin(time * 1.9f) * 0.008f;

            // Hold: right fist on the handle in front of the right hip (raised while spraying), the tank under it.
            Vector3 rHold = q.TorsoPoint(Vector3.Lerp(new Vector3(0.27f, 0.1f + breath, 0.2f), new Vector3(0.22f, 0.2f + breath, 0.3f), aim)) + trem * 0.5f;
            Quaternion mRot = q.TorsoRoot * Quaternion.Euler(4f * aim, -78f, -6f + 3f * aim);
            // Left hand with the nozzle, aimed along the look pitch (low and relaxed when not spraying).
            float pitch = Mathf.Lerp(18f, -look, aim);
            Quaternion nRot = q.TorsoRoot * Quaternion.Euler(pitch, -4f, 0f);
            Vector3 lHold = q.TorsoPoint(new Vector3(-0.05f, 0.33f + 0.1f * aim, 0.4f + 0.14f * aim)) + q.TorsoRoot * (Quaternion.Euler(-look * aim * 0.6f, 0f, 0f) * Vector3.up) * 0.02f + trem;

            float w = ActionTimeline.SmoothStep01(exW);
            Vector3 rHand = rHold, lHand = lHold;
            float lw = 1f;
            if (exT < ExtTimeline.Take && exHold)
            {
                // Take it: reach for the handle (crouch for a low one), then lift it into the hold.
                Vector3 fromGrip = transform.InverseTransformPoint(exFromPos + exFromRot * ExtinguisherModel.GripLocal(s));
                float low = Mathf.Clamp01((0.75f - fromGrip.y) / 0.6f);
                float reach = ExtTimeline.S(exT, 0f, ExtTimeline.Grab);
                float lift = ExtTimeline.S(exT, ExtTimeline.Grab + 0.03f, ExtTimeline.Take);
                rHand = exT < ExtTimeline.Grab ? Vector3.Lerp(exReach, fromGrip, reach) : Vector3.Lerp(fromGrip, rHold, lift);
                exAtFrom = exT < ExtTimeline.Grab ? 1f : 1f - lift;
                float bend = reach * (1f - lift);
                q.PelvisPos += Vector3.down * 0.32f * low * bend;
                q.Torso = q.Torso * Quaternion.Euler((14f + 30f * low) * bend, 0f, 0f);
                p.PelvisPos = q.PelvisPos;
                p.Torso = q.Torso;
                if (low > 0.05f) { q.SolveLeg(0, new Vector3(0.12f, 0f, 0.05f), TorsoDir(q, 0f, 0f, 1f)); q.SolveLeg(1, new Vector3(-0.12f, 0f, -0.08f), TorsoDir(q, 0f, 0f, 1f)); p.HipR = q.HipR; p.KneeR = q.KneeR; p.HipL = q.HipL; p.KneeL = q.KneeL; }
                rHold = q.TorsoPoint(new Vector3(0.27f, 0.1f, 0.2f));
                if (exT >= ExtTimeline.Grab) rHand = Vector3.Lerp(fromGrip, rHold, lift);
                lw = ExtTimeline.S(exT, ExtTimeline.Grab + 0.2f, ExtTimeline.Take);
                exInHand = exT >= ExtTimeline.Grab;
            }
            else
            {
                exAtFrom = 0f;
                exInHand = exHold;
            }

            // Pin pull: left hand to the ring, draws the pin out along the valve, flicks it away, back to the nozzle.
            if (exPinT < ExtTimeline.PinDone && exProp != null)
            {
                Vector3 ring = transform.InverseTransformPoint(ExtRingWorld);
                Vector3 side = mRot * Vector3.right;
                float k1 = ExtTimeline.S(exPinT, 0f, ExtTimeline.PinReach);
                float k2 = ExtTimeline.S(exPinT, ExtTimeline.PinReach, ExtTimeline.PinOut);
                float k3 = ExtTimeline.S(exPinT, ExtTimeline.PinOut, ExtTimeline.PinThrow);
                float k4 = ExtTimeline.S(exPinT, ExtTimeline.PinThrow, ExtTimeline.PinDone);
                Vector3 atRing = ring + side * 0.05f;
                Vector3 pulled = atRing + side * 0.12f + Vector3.up * 0.02f;
                Vector3 flick = pulled + side * 0.12f + Vector3.up * 0.1f - (q.TorsoRoot * Vector3.forward) * 0.05f;
                Vector3 l = Vector3.Lerp(lHold, atRing, k1);
                l = Vector3.Lerp(l, pulled, k2);
                l = Vector3.Lerp(l, flick, k3);
                lHand = Vector3.Lerp(l, lHold, k4);
                q.Head = Quaternion.Slerp(q.Head, Quaternion.Euler(28f, 18f, 0f), (k1 - k4) * 0.8f);
                p.Head = Quaternion.Slerp(p.Head, q.Head, w);
            }

            q.SolveArm(0, rHand, TorsoDir(q, 1f, -1f, -0.3f));
            q.SolveArm(1, lHand, TorsoDir(q, -1f, -1f, -0.2f));
            q.PropR = Quaternion.Inverse(q.ForearmRoot(0)) * mRot;
            CharacterPose.LerpArm(ref p, q, 0, w);
            CharacterPose.LerpArm(ref p, q, 1, w * lw);
            // A little lean back against the weight while spraying.
            p.Torso = Quaternion.Slerp(p.Torso, p.Torso * Quaternion.Euler(-4f * exSprayW, -6f * exSprayW, 0f), w);
            exModelRootRot = mRot;
            exNozzleRootRot = nRot;
        }

        /// <summary>After the pose: the hand copy on the fist (travelling from the take pose), the hose to the left hand.</summary>
        void UpdateExtinguisherProps()
        {
            bool show = exInHand && exW > 0.01f;
            ShowExt(show);
            if (!show || exProp == null) return;
            var s = exProp.Style;
            Quaternion rot = transform.rotation * exModelRootRot;
            Vector3 pos = socketR.position - rot * ExtinguisherModel.GripLocal(s);
            if (exAtFrom > 0.001f)
            {
                pos = Vector3.Lerp(pos, exFromPos, exAtFrom);
                rot = Quaternion.Slerp(rot, exFromRot, exAtFrom);
            }
            exProp.Root.SetPositionAndRotation(pos, rot);
            ExtReleasePos = pos;
            ExtReleaseRot = rot;
            bool pinShown = exPinIn || exPinT < ExtTimeline.PinOut;
            ExtinguisherModel.SetPinIn(exProp, pinShown);
            if (pinShown && exPinT < ExtTimeline.PinOut)
                exProp.Pin.localPosition = exProp.Seal.localPosition = Vector3.right * ExtTimeline.PinTravel * ExtTimeline.S(exPinT, ExtTimeline.PinReach, ExtTimeline.PinOut);
            else exProp.Pin.localPosition = exProp.Seal.localPosition = Vector3.zero;
            ExtinguisherModel.SetCharge(exProp, exCharge);
            ExtinguisherModel.SetSqueeze(exProp, exSqueeze);
            // Nozzle in the left fist (still in its clip while the left hand has not taken it yet).
            float lw = exT < ExtTimeline.Take ? ExtTimeline.S(exT, ExtTimeline.Grab + 0.2f, ExtTimeline.Take) : 1f;
            if (exPinT < ExtTimeline.PinDone) lw = Mathf.Min(lw, 1f - ExtTimeline.Bump(exPinT, 0.02f, ExtTimeline.PinDone - 0.02f, 0.25f));
            if (lw < 0.5f) { ExtinguisherModel.RestHose(exProp); return; }
            Vector3 fist = character.ElbowLeft.TransformPoint(CharacterPose.HandLocal);
            Vector3 dir = transform.rotation * (exNozzleRootRot * Vector3.forward);
            Vector3 coupling = fist - dir * 0.045f;
            ExtinguisherModel.LayHose(exProp, exProp.Root.InverseTransformPoint(coupling), exProp.Root.InverseTransformDirection(dir));
        }

        /// <summary>World tip and direction of the nozzle in the hand (for the spray), false when not shown.</summary>
        public bool ExtNozzle(out Vector3 tip, out Vector3 dir)
        {
            tip = dir = default;
            if (!exInHand || exProp == null || !exProp.Root.gameObject.activeInHierarchy) return false;
            tip = ExtinguisherModel.TipWorld(exProp);
            dir = exProp.Nozzle.forward;
            return true;
        }

        void ShowExt(bool on)
        {
            if (on && exProp == null && socketR != null)
            {
                exProp = ExtinguisherModel.Create(transform, gameObject.layer);
                exLayer = -1;
            }
            if (exProp == null) return;
            if (exProp.Root.gameObject.activeSelf != on) exProp.Root.gameObject.SetActive(on);
            if (!on) return;
            bool fp = character.IsFirstPerson;
            if (gameObject.layer != exLayer || fp != exFp)
            {
                exLayer = gameObject.layer;
                exFp = fp;
                ExtinguisherModel.SetRender(exProp, exLayer, fp ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On);
            }
        }
    }
}
