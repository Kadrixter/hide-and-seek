using HideAndSeek.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Timings (s) of taking the public-address microphone off its wall hook and hanging it back; shared by the third
    /// person body, the first-person hands and the game (which hides the hook copy while the mic is in a hand).
    /// </summary>
    public static class PaMicTimeline
    {
        /// <summary>Take: the hand reaches the hook at <see cref="Grab"/>, the mic is at the mouth at <see cref="Take"/>.</summary>
        public const float Grab = 0.36f, Take = 0.85f;
        /// <summary>Put back: the mic is on the hook at <see cref="Release"/>, the arm is down at <see cref="Put"/>.</summary>
        public const float Release = 0.46f, Put = 0.85f;
        /// <summary>Hook farther than this from the body (m, flat): no hang-back animation, the cord pulls it back.</summary>
        public const float ReachAnimated = 1.35f;

        public static float S(float t, float a, float b) => ActionTimeline.SmoothStep01(ActionTimeline.Remap01(t, a, b));

        /// <summary>0..1: how much the mic is still glued to the hook pose (1 at the grab / release moment).</summary>
        public static float AtHook(bool hold, float t) => hold ? 1f - S(t, Grab, Grab + 0.3f) : S(t, Release - 0.3f, Release);

        /// <summary>Mic scale while it travels between the hook (1) and the fist (<see cref="PaMicModel.HandScale"/>).</summary>
        public static float Scale(float atHook, float handScale) => Mathf.Lerp(handScale, 1f, atHook);
    }

    /// <summary>
    /// Public-address microphone in the right hand (third person): reach for the hook, take the mic and hold it in
    /// front of the mouth (a little closer while talking, the thumb presses the talk bar), or bring it back to the hook
    /// and let go. Driven by the game every frame through <see cref="SetPaMic"/>; when the holder cannot pose (downed,
    /// dead, an action takes the body) or the hook is too far away, the mic leaves the hand at once (the game swings it
    /// back to the hook on its cord).
    /// </summary>
    public partial class BlockyCharacterActions
    {
        bool paHold, paTalk, paPut, paInHand;
        float paT = 99f, paTalkW;
        Vector3 paHookPos = Vector3.zero;
        Quaternion paHookRot = Quaternion.identity;
        Vector3 paFrom;               // root-space fist position when the state changed
        Vector3 paLastHand;           // root-space fist position of the last output
        bool paHasLast;
        PaMicModel.Parts paMic;

        /// <summary>True while the microphone is in this character's hand (the hook copy is hidden).</summary>
        public bool PaMicInHand => paInHand;

        /// <summary>World point where the coiled cord leaves the microphone in the hand (valid while <see cref="PaMicInHand"/>).</summary>
        public Vector3 PaMicCordWorld => paMic != null ? paMic.Root.TransformPoint(PaMicModel.CordLocal) : transform.position;

        /// <summary>The mic object in the hand (null when not shown).</summary>
        public Transform PaMicTransform => paInHand && paMic != null ? paMic.Root : null;

        /// <summary>
        /// Every frame from the game: <paramref name="hold"/> = this player has the PA microphone, <paramref name="hookPos"/> /
        /// <paramref name="hookRot"/> = pose of the mic hanging on its hook, <paramref name="talking"/> = talk bar pressed.
        /// </summary>
        public void SetPaMic(bool hold, Vector3 hookPos, Quaternion hookRot, bool talking)
        {
            EnsureInit();
            paHookPos = hookPos;
            paHookRot = hookRot;
            paTalk = hold && talking;
            if (hold == paHold) return;
            paHold = hold;
            paT = 0f;
            paFrom = paHasLast ? paLastHand : new Vector3(0.38f, 0.75f, 0.05f);
            if (hold) return;
            // Hang it back only when the hook is within reach; otherwise it slips out of the hand right away.
            Vector3 d = transform.InverseTransformPoint(hookPos);
            paPut = paInHand && new Vector2(d.x, d.z).magnitude <= PaMicTimeline.ReachAnimated && VisualLifeState == LifeState.Alive && !pathActive;
            if (!paPut) paInHand = false;
        }

        /// <summary>Hard reset (scene change): no mic, no animation.</summary>
        public void ClearPaMic()
        {
            paHold = paPut = paInHand = paTalk = false;
            paT = 99f;
            ShowPaMic(false);
        }

        /// <summary>True while the PA layer owns the right arm (no held item shown).</summary>
        bool PaMicBusy => paHold || (paPut && paT < PaMicTimeline.Put);

        void ApplyPaMic(ref CharacterPose p, float dt, bool standing)
        {
            paT += dt;
            paTalkW = Damp(paTalkW, paTalk ? 1f : 0f, 10f, dt);
            bool canPose = VisualLifeState == LifeState.Alive && !pathActive && action != ActionKind.StabbedReaction && action != ActionKind.KillReaction;
            if (!canPose)
            {
                // Knocked down / killed with the mic: it slips out of the hand (the game swings it back to the hook).
                paInHand = false;
                paPut = false;
                paHasLast = false;
                if (paHold) paT = 0f; // still holding afterwards: take it again from the hook
                return;
            }

            var q = p;
            Vector3 hookL = transform.InverseTransformPoint(paHookPos);
            Quaternion hookRotL = Quaternion.Inverse(transform.rotation) * paHookRot;
            // Fist next to the hanging mic: in front of it and a little below (the fist block must stay out of the wall).
            Vector3 grab = hookL + hookRotL * new Vector3(0f, -0.045f, 0.13f);
            Vector3 hold = PaHoldHand(q, out Quaternion micRot, out float micScale);
            Vector3 rest = paFrom;

            float w;
            Vector3 hand;
            float lean;
            if (paHold)
            {
                if (paT < PaMicTimeline.Grab)
                {
                    hand = Vector3.Lerp(paFrom, grab, PaMicTimeline.S(paT, 0.04f, PaMicTimeline.Grab));
                    w = PaMicTimeline.S(paT, 0f, 0.14f);
                }
                else
                {
                    paInHand = true;
                    hand = Vector3.Lerp(grab, hold, PaMicTimeline.S(paT, PaMicTimeline.Grab + 0.04f, PaMicTimeline.Take));
                    w = 1f;
                }
                lean = PaMicTimeline.S(paT, 0.02f, PaMicTimeline.Grab) * (1f - PaMicTimeline.S(paT, PaMicTimeline.Grab + 0.05f, PaMicTimeline.Take));
            }
            else if (paPut && paT < PaMicTimeline.Put)
            {
                if (paT < PaMicTimeline.Release)
                {
                    hand = Vector3.Lerp(paFrom, grab, PaMicTimeline.S(paT, 0f, PaMicTimeline.Release - 0.04f));
                    w = 1f;
                }
                else
                {
                    paInHand = false;
                    rest = new Vector3(0.38f, 0.75f, 0.05f);
                    hand = Vector3.Lerp(grab, rest, PaMicTimeline.S(paT, PaMicTimeline.Release + 0.05f, PaMicTimeline.Put));
                    w = 1f - PaMicTimeline.S(paT, PaMicTimeline.Release + 0.12f, PaMicTimeline.Put);
                }
                lean = PaMicTimeline.S(paT, 0f, PaMicTimeline.Release) * (1f - PaMicTimeline.S(paT, PaMicTimeline.Release + 0.05f, PaMicTimeline.Put));
            }
            else
            {
                paInHand = false;
                paHasLast = false;
                return;
            }

            // Lean a little towards the hook and look at it while reaching.
            if (lean > 0.001f)
            {
                Vector3 to = hookL - q.TorsoPoint(BlockyCharacter.NeckRest);
                float yaw = Mathf.Clamp(Mathf.Atan2(to.x, Mathf.Max(0.1f, to.z)) * Mathf.Rad2Deg, -35f, 35f);
                q.Torso = q.Torso * Quaternion.Euler(10f * lean, yaw * 0.35f * lean, 0f);
                q.Head = Quaternion.Slerp(q.Head, Quaternion.Euler(Mathf.Clamp(-Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg, -10f, 35f), yaw * 0.6f, 0f), lean);
                p.Torso = Quaternion.Slerp(p.Torso, q.Torso, w);
                p.Head = Quaternion.Slerp(p.Head, q.Head, w);
            }
            q.SolveArm(0, hand, TorsoDir(q, 1f, -1f, -0.15f));
            q.PropR = Quaternion.Inverse(q.ForearmRoot(0)) * micRot;
            CharacterPose.LerpArm(ref p, q, 0, w);
            if (paInHand) p.PropR = q.PropR;
            paLastHand = q.HandPos(0);
            paHasLast = true;
        }

        /// <summary>Fist position (root space) holding the mic in front of the mouth, slightly right of it; the mic faces the lips.</summary>
        Vector3 PaHoldHand(in CharacterPose q, out Quaternion micRot, out float micScale)
        {
            Quaternion head = q.TorsoRoot * q.Head;
            Vector3 neck = q.TorsoPoint(BlockyCharacter.NeckRest);
            float talk = paTalkW;
            // Face plane is 4 px in front of the head pivot; the lips about 2 px above the neck.
            Vector3 grille = neck + head * new Vector3(0.11f - 0.02f * talk, 0.1f + 0.012f * talk, 4f * Px + 0.12f - 0.03f * talk);
            micRot = head * Quaternion.LookRotation(new Vector3(-0.38f, 0.12f, -1f).normalized, new Vector3(0.06f, 1f, 0.18f));
            micRot *= Quaternion.Euler(0f, 0f, -4f * talk);
            micScale = PaMicModel.HandScale;
            Vector3 centre = grille - micRot * (PaMicModel.GrilleLocal * micScale);
            return centre + micRot * (PaMicModel.GripLocal * micScale);
        }

        /// <summary>After the pose is applied: the mic in the fist (travelling from / to the hook pose), the talk bar.</summary>
        void UpdatePaMicProp()
        {
            ShowPaMic(paInHand);
            if (!paInHand || paMic == null) return;
            float atHook = PaMicTimeline.AtHook(paHold, paT);
            if (!paHold && !paPut) atHook = 0f;
            float scale = PaMicTimeline.Scale(atHook, PaMicModel.HandScale);
            Vector3 nominal = socketR.TransformPoint(-PaMicModel.GripLocal * PaMicModel.HandScale);
            Quaternion rot = Quaternion.Slerp(socketR.rotation, paHookRot, atHook);
            Vector3 pos = Vector3.Lerp(nominal, paHookPos, atHook);
            paMic.Root.SetPositionAndRotation(pos, rot);
            paMic.Root.localScale = Vector3.one * scale / Mathf.Max(0.0001f, socketR.lossyScale.x);
            PaMicModel.SetPressed(paMic, paTalkW);
        }

        void ShowPaMic(bool on)
        {
            if (on && paMic == null && socketR != null)
            {
                paMic = PaMicModel.Create(socketR, gameObject.layer);
                lastPaLayer = -1;
            }
            if (paMic == null) return;
            if (paMic.Root.gameObject.activeSelf != on) paMic.Root.gameObject.SetActive(on);
            if (!on) return;
            int layer = gameObject.layer;
            bool fp = character.IsFirstPerson;
            if (layer != lastPaLayer || fp != lastPaFp)
            {
                lastPaLayer = layer;
                lastPaFp = fp;
                PaMicModel.SetRender(paMic, layer, fp ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On);
            }
        }

        int lastPaLayer = -1;
        bool lastPaFp;
    }
}
