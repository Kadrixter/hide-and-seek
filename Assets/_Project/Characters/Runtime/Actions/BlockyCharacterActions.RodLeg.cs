using System;
using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Peg leg made of a metal rod: walking on it (stiff leg swung out round the side, hip hiked during the swing,
    /// a slight lean over the rod while it carries the weight, arms a little out) and the state shared with the
    /// attach animation (<c>BlockyCharacterActions.RodAttach.cs</c>). The rod meshes are placed by <see cref="RodLegRig"/>.
    /// </summary>
    public partial class BlockyCharacterActions
    {
        int rodLegsConfirmed, rodLegsLocal;
        float rodMoveW, rodPrevQ;
        RodLegRig rodRig;

        /// <summary>Rod touches the floor (world tip position, true = scrape while swinging instead of a step).</summary>
        public event Action<Vector3, bool> RodStep;

        /// <summary>Legs (Limb bits) wearing a rod: confirmed by the game plus the one just attached locally.</summary>
        public int RodLegsShown => (rodLegsConfirmed | rodLegsLocal) & Limb.Legs;

        /// <summary>Legs with a rod (Limb bits, from the network). 0 also clears the locally predicted one.</summary>
        public void SetRodLegs(int legs)
        {
            EnsureInit();
            legs &= Limb.Legs;
            if (legs == rodLegsConfirmed) return;
            rodLegsConfirmed = legs;
            if (legs == 0) rodLegsLocal = 0;
            else rodLegsLocal &= ~legs;
            EnsureRodRig();
        }

        void EnsureRodRig()
        {
            if (rodRig != null || character == null) return;
            rodRig = GetComponent<RodLegRig>();
            if (rodRig == null) rodRig = gameObject.AddComponent<RodLegRig>();
        }

        /// <summary>Hip rotation (pelvis space) that points the leg's -Y along <paramref name="axisRoot"/> (root space).</summary>
        static void SetHipAxis(ref CharacterPose p, int leg, Vector3 axisRoot)
        {
            Quaternion inv = Quaternion.Inverse(p.Pelvis);
            Vector3 dl = (inv * axisRoot).normalized;
            Vector3 f = inv * Vector3.forward;
            f -= dl * Vector3.Dot(f, dl);
            if (f.sqrMagnitude < 1e-5f) f = Vector3.Cross(Vector3.right, dl);
            Quaternion hip = Quaternion.LookRotation(f.normalized, -dl);
            if (leg == 0) { p.HipR = hip; p.KneeR = Quaternion.identity; }
            else { p.HipL = hip; p.KneeL = Quaternion.identity; }
        }

        /// <summary>Sole of a leg in root space from the pose (end of the shin).</summary>
        static Vector3 SolePos(in CharacterPose p, int leg)
        {
            Quaternion hip = p.Pelvis * (leg == 0 ? p.HipR : p.HipL);
            Quaternion knee = hip * (leg == 0 ? p.KneeR : p.KneeL);
            return p.HipPos(leg) + hip * BlockyCharacter.KneeRest + knee * BlockyCharacter.KneeRest;
        }

        /// <summary>Axis from the hip that keeps the rod tip on (or above) the floor, heading for <paramref name="tip"/>.</summary>
        static Vector3 RodAxisTo(Vector3 hip, Vector3 tip, float minTipY)
        {
            Vector3 d = tip - hip;
            if (d.sqrMagnitude < 1e-6f) d = Vector3.down;
            d.Normalize();
            float tipY = hip.y + d.y * RodModel.Length;
            if (tipY >= minTipY) return d;
            float vy = Mathf.Clamp((minTipY - hip.y) / RodModel.Length, -1f, 0f);
            Vector3 h = new Vector3(d.x, 0f, d.z);
            if (h.sqrMagnitude < 1e-6f) h = Vector3.forward;
            return h.normalized * Mathf.Sqrt(1f - vy * vy) + Vector3.up * vy;
        }

        // ================================================================== layer (called from ApplyLayer)

        void ApplyRodLayers(ref CharacterPose p, float dt, bool standing)
        {
            if (rodAttachT >= 0f) { ApplyRodAttach(ref p, dt); return; }
            int legs = RodLegsShown;
            if (legs == 0) return;
            EnsureRodRig();
            if (!standing || action != ActionKind.None || VisualLifeState != LifeState.Alive) return;

            var src = animator != null ? animator.Source : null;
            float speed = src != null ? Mathf.Max(0f, src.HorizontalSpeed) : 0f;
            bool grounded = src == null || src.IsGrounded;
            rodMoveW = Damp(rodMoveW, grounded ? ActionTimeline.SmoothStep01(Mathf.InverseLerp(0.05f, 0.8f, speed)) : 0f, 9f, dt);
            float runW = Mathf.InverseLerp(4.2f, 5.6f, speed);
            float duty = Mathf.Lerp(0.6f, 0.36f, runW);
            float gait = animator != null ? animator.GaitPhase : 0f;

            bool both = (legs & Limb.Legs) == Limb.Legs;
            for (int li = 0; li < 2; li++)
            {
                int bit = li == 0 ? Limb.LegR : Limb.LegL;
                if ((legs & bit) == 0) continue;
                float s = li == 0 ? 1f : -1f;
                int gi = 1 - li;
                float q = gait + (li == 0 ? 0.5f : 0f);
                q -= Mathf.Floor(q);
                bool swing = q >= duty;
                float st = swing ? (q - duty) / (1f - duty) : q / duty;
                float swingS = swing ? Mathf.Sin(Mathf.PI * st) * rodMoveW : 0f;
                float stanceS = swing ? 0f : Mathf.Sin(Mathf.PI * st) * rodMoveW;

                Vector3 sole = SolePos(p, li);
                Vector3 goodSole = SolePos(p, gi);
                Quaternion torsoRoot = p.TorsoRoot;

                if (!both)
                {
                    // Hip hike on the rod side while it swings, weight shifted onto the good leg when standing.
                    float roll = s * (4.5f * swingS - 1.2f * stanceS) - s * 1.5f * (1f - rodMoveW);
                    p.Pelvis = Quaternion.Euler(0f, 0f, roll) * p.Pelvis;
                    p.PelvisPos += new Vector3(-s * 0.012f * (1f - rodMoveW), 0.01f * swingS, 0f);
                    // Torso: leans over the rod while it carries the weight, away from it during the swing.
                    float lean = s * (5f * swingS - 3.5f * stanceS + 2f);
                    p.Torso = Quaternion.Inverse(p.Pelvis) * Quaternion.Euler(2.5f * rodMoveW, 0f, lean) * torsoRoot;
                    p.SolveLeg(gi, goodSole, new Vector3(-s * 0.06f, 0f, 1f));
                    // Arms a touch out for balance.
                    float armOut = 5f + 4f * rodMoveW;
                    p.ShoulderR = Quaternion.Euler(0f, 0f, armOut) * p.ShoulderR;
                    p.ShoulderL = Quaternion.Euler(0f, 0f, -armOut) * p.ShoulderL;
                }

                // Stiff rod: swung out round the side (circumduction), tip skims the floor.
                Vector3 tip = sole + new Vector3(s * 0.075f * swingS, 0f, 0f);
                Vector3 hip = p.HipPos(li);
                Vector3 axis = RodAxisTo(hip, tip, 0.012f * swingS);
                SetHipAxis(ref p, li, axis);

                // Sound events (one rod is enough when both legs are rods): plant and a scrape early in the swing.
                bool soundLeg = !both || li == 0;
                if (soundLeg && rodMoveW > 0.3f && grounded)
                {
                    float prev = rodPrevQ;
                    bool planted = prev > 0.75f && q < 0.25f;
                    float scrapeAt = duty + (1f - duty) * 0.22f;
                    bool scraped = prev < scrapeAt && q >= scrapeAt && q - prev < 0.3f;
                    Vector3 tipW = transform.TransformPoint(hip + axis * RodModel.Length);
                    if (planted) RodStep?.Invoke(tipW, false);
                    else if (scraped && Rand01() < 0.6f) RodStep?.Invoke(tipW, true);
                }
                if (soundLeg) rodPrevQ = q;
            }
        }
    }
}
