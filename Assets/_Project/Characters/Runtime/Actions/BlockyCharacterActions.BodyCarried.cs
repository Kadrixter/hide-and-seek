using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Being carried (see BlockyCharacterActions.BodyCarry.cs) - the carried player's side. While carried the whole
    /// body comes from the pose the carrier's layer computed this frame, mapped from the carrier's root into the own
    /// root (the own root may lag behind on the network, the body never does). The animator is ticked right after the
    /// carrier's (<see cref="BodyCarryTicker"/>). After the carry the body stays where it was put down (in the end
    /// frame, world space) until the own root got there, then lies down / rolls onto the belly / gets up from there.
    /// An unexpected end in mid-air (the carrier left the game) falls to the floor first.
    /// </summary>
    public partial class BlockyCharacterActions
    {
        BodyCarryState bodyCarriedBy;
        BlockyCharacterAnimator bodyAnimRestore;
        bool bodyRelease, bodyRelFall, bodyRelStanding;
        float bodyRelT;
        CharacterPose bodyRelPose, bodyRelRest;
        Vector3 bodyRelPos;
        Quaternion bodyRelRot = Quaternion.identity;

        /// <summary>This character lies in somebody's arms (also while being lifted / put down).</summary>
        public bool IsCarriedBody => bodyCarriedBy != null;

        /// <summary>After the carry: the body still waits at the spot it was put down (the root is on its way).</summary>
        public bool IsSettlingAfterCarry => bodyRelease;

        internal void AttachBodyCarried(BodyCarryState st)
        {
            EnsureInit();
            // A dead body lying as a ragdoll: the animation takes over from exactly where it lies.
            if (IsRagdolled) StopRagdoll();
            bodyCarriedBy = st;
            bodyRelease = false;
            // Start snapshot: the pose exactly as it is now, in the world.
            st.StartPose = hasLast ? lastOutput : CharacterPose.Capture(character);
            st.StartRootPos = transform.position;
            st.StartRootRot = transform.rotation;
            st.HasStart = true;
            reviving = false;
            if (animator == null) animator = GetComponent<BlockyCharacterAnimator>();
            if (animator != null && animator.autoUpdate)
            {
                animator.autoUpdate = false;
                bodyAnimRestore = animator;
            }
            var ticker = GetComponent<BodyCarryTicker>();
            if (ticker == null) ticker = gameObject.AddComponent<BodyCarryTicker>();
            ticker.enabled = true;
            if (st.Phase == BodyCarryPhase.Hold) BeginCrossfade(); // joined mid-carry: no lift to blend from
        }

        internal void DetachBodyCarried(BodyCarryState st)
        {
            if (bodyCarriedBy != st) return;
            bodyCarriedBy = null;
            if (bodyAnimRestore != null) { bodyAnimRestore.autoUpdate = true; bodyAnimRestore = null; }
            if (!hasLast || IsRagdolled) return;

            bool finished = st.HasEnd && (st.Phase == BodyCarryPhase.PutDown || st.Phase == BodyCarryPhase.Drop) && st.Time >= st.Duration - 0.05f;
            bodyRelStanding = st.Phase == BodyCarryPhase.PutDown && st.EndStanding;
            if (finished)
            {
                // Put down / dropped: the last pose already lies in the end frame.
                bodyRelPos = st.EndPos;
                bodyRelRot = st.EndRot;
                bodyRelFall = false;
            }
            else
            {
                // Cut short (carrier gone, round over...): fall from where the body is to the floor below it.
                if (lifeState == LifeState.Alive && st.Phase != BodyCarryPhase.Drop) { BeginCrossfade(); return; }
                Vector3 pelvis = transform.TransformPoint(lastOutput.PelvisPos);
                Vector3 head = transform.rotation * (lastOutput.TorsoRoot * Vector3.up);
                head.y = 0f;
                if (head.sqrMagnitude < 1e-4f) head = transform.forward;
                head.Normalize();
                bodyRelRot = Quaternion.LookRotation(head, Vector3.up);
                bodyRelPos = new Vector3(pelvis.x, transform.position.y, pelvis.z) + head * BodyCarryTimeline.LyingPelvisBack;
                bodyRelFall = true;
                bodyRelStanding = false;
            }
            bodyRelPose = BodyCarryPoses.Rebase(lastOutput, transform.position, transform.rotation, bodyRelPos, bodyRelRot);
            bodyRelRest = bodyRelStanding ? BodyCarryPoses.StandRest() : BodyCarryPoses.SupineRest();
            bodyRelT = 0f;
            bodyRelease = true;
        }

        /// <summary>Diagnostics for the tests.</summary>
        public string BodyCarryDebug
        {
            get
            {
                var st = bodyCarriedBy ?? bodyCarrying;
                var tick = GetComponent<BodyCarryTicker>();
                Vector3 pel = character != null && character.Pelvis != null ? character.Pelvis.position : Vector3.zero;
                return $"carried={bodyCarriedBy != null} carrying={bodyCarrying != null} frame={Time.frameCount} carriedFrame={(st != null ? st.CarriedFrame : -9)} " +
                       $"tickFrame={(st != null ? st.TickFrame : -9)} auto={(animator != null ? animator.autoUpdate.ToString() : "-")} ticker={(tick != null && tick.enabled)} " +
                       $"pelvis={pel:F2} root={transform.position:F2} settling={bodyRelease}";
            }
        }

        /// <summary>The carrier vanished without ending the carry (left the game): let go of the body here.</summary>
        public void ForceEndCarried()
        {
            if (bodyCarriedBy != null) DetachBodyCarried(bodyCarriedBy);
        }

        /// <summary>Where the carried body ends up after the carry (world root frame), valid while settling.</summary>
        public bool TryGetCarryEnd(out Vector3 pos, out Quaternion rot)
        {
            pos = bodyRelPos;
            rot = bodyRelRot;
            return bodyRelease;
        }

        void ApplyBodyCarried(ref CharacterPose p, float dt)
        {
            var st = bodyCarriedBy;
            if (st != null)
            {
                // Life paths (downed / back up) are replaced by the carry; whatever runs now must not replay afterwards.
                pathActive = false;
                pendingDowned = false;
                var carrier = st.Carrier;
                if (carrier == null || st.CarriedFrame < 0) return;
                var c = BodyCarryPoses.Rebase(st.CarriedPose, carrier.transform.position, carrier.transform.rotation, transform.position, transform.rotation);
                c.PropR = p.PropR;
                p = c;
                st.TickFrame = Time.frameCount;
                return;
            }
            if (!bodyRelease) return;

            bodyRelT += dt;
            const float fallTime = 0.6f;
            var q = bodyRelPose;
            if (bodyRelFall)
            {
                float x = ActionTimeline.Remap01(bodyRelT, 0f, fallTime);
                q = CharacterPose.Lerp(bodyRelPose, bodyRelRest, ActionTimeline.EaseIn(x));
                if (x >= 1f) bodyRelPose = bodyRelRest;
            }
            var c2 = BodyCarryPoses.Rebase(q, bodyRelPos, bodyRelRot, transform.position, transform.rotation);
            c2.PropR = p.PropR;
            p = c2;

            bool fell = !bodyRelFall || bodyRelT >= fallTime;
            Vector3 d = transform.position - bodyRelPos;
            bool arrived = d.sqrMagnitude < 0.04f * 0.04f && Quaternion.Angle(transform.rotation, bodyRelRot) < 3f;
            if (!fell || (!arrived && bodyRelT < 1.5f)) return;

            // Settled: hand the body back to the normal layers from exactly this pose.
            bodyRelease = false;
            lastOutput = p;
            hasLast = true;
            fadeT = 1f;
            if (lifeState == LifeState.Dead) return; // the ragdoll takes it from here
            if (bodyRelStanding) { BeginCrossfade(); return; }
            pathFrom = p;
            keyCount = 0;
            Key(PoseId.Snapshot, 0f, Ease.Smooth);
            if (lifeState == LifeState.Downed)
            {
                // Rolls from the back onto the belly (the downed crawl pose).
                Key(PoseId.Side, 0.8f, Ease.Smooth);
                Key(PoseId.Downed, 1.6f, Ease.Smooth);
            }
            else
            {
                // Dropped but conscious: onto the side, all fours, knees, up.
                Key(PoseId.Side, 0.6f, Ease.Smooth);
                Key(PoseId.AllFours, 1.2f, Ease.Smooth);
                Key(PoseId.Kneel, 1.7f, Ease.Smooth);
                Key(PoseId.Base, 2.3f, Ease.Smooth);
            }
            pathTime = 0f;
            pathActive = true;
        }
    }

    /// <summary>
    /// Ticks a carried character's animator after every other animator (so after the carrier's, which computes the
    /// pose this one shows). Added while carried; gives the animator back its own update when the carry ends.
    /// </summary>
    [DefaultExecutionOrder(105)]
    [DisallowMultipleComponent]
    public class BodyCarryTicker : MonoBehaviour
    {
        BlockyCharacterAnimator anim;
        BlockyCharacterActions act;

        void LateUpdate()
        {
            if (act == null) act = GetComponent<BlockyCharacterActions>();
            if (anim == null) anim = GetComponent<BlockyCharacterAnimator>();
            if (act == null || anim == null || !act.IsCarriedBody)
            {
                enabled = false;
                return;
            }
            anim.Tick(Time.deltaTime);
        }
    }
}
