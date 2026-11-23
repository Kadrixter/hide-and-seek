using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Death -> ragdoll (<see cref="BlockyRagdoll"/>), switched on with <see cref="RagdollOnDeath"/> (the game does, the
    /// editor tools and recorders keep the plain dead pose).
    /// <list type="bullet">
    /// <item>Kill that ends dead (finishers, every chainsaw kill): the moment the victim's animation ends, even before
    /// the Dead state arrives from the server (it always follows).</item>
    /// <item>Any other death (falling down from standing, the last breath of a downed player, a kill that ended downed
    /// and then dead): at the end of the existing death animation, instead of freezing in its last pose.</item>
    /// <item>Shot dead with the Glock: at once, with a push along the shot (<see cref="NotifyBulletHit"/>; the shot and
    /// the Dead state may arrive in either order).</item>
    /// <item>Already dead when we see the player (joined late, <see cref="SetLifeStateImmediate"/>): from the plain dead
    /// pose at rest, so the body settles where it lies.</item>
    /// </list>
    /// Leaving the Dead state (adrenaline, a new round) ends the ragdoll and the get-up starts from where the body lies.
    /// </summary>
    public partial class BlockyCharacterActions
    {
        /// <summary>A shot and the Dead state that far apart (s) still count as "shot dead".</summary>
        const float ShotDeathWindow = 0.75f;

        bool ragdollOnDeath;
        BlockyRagdoll ragdoll;
        float deathTime = -99f, shotTime = -99f;
        Vector3 shotPoint, shotDir;
        bool snappedDead;

        /// <summary>Dead bodies become ragdolls (off by default).</summary>
        public bool RagdollOnDeath
        {
            get => ragdollOnDeath;
            set
            {
                ragdollOnDeath = value;
                if (value) EnsureRagdoll();
                else if (IsRagdolled) StopRagdoll();
            }
        }

        /// <summary>The ragdoll component (null until <see cref="RagdollOnDeath"/> was switched on).</summary>
        public BlockyRagdoll Ragdoll => ragdoll;

        /// <summary>The body currently lies as a ragdoll (moving or frozen).</summary>
        public bool IsRagdolled => ragdoll != null && ragdoll.IsActive;

        /// <summary>Velocity kick (m/s) a Glock bullet gives the body along the shot.</summary>
        public static Vector3 ShotPush(Vector3 dir)
        {
            dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.forward;
            return dir * 3.4f + Vector3.up * 0.5f;
        }

        /// <summary>
        /// A bullet hit this character at <paramref name="point"/> flying along <paramref name="dir"/> (the same values on
        /// every client). When the Dead state comes with it, the ragdoll starts at once with a push along the shot.
        /// </summary>
        public void NotifyBulletHit(Vector3 point, Vector3 dir)
        {
            shotTime = Time.time;
            shotPoint = point;
            shotDir = dir.sqrMagnitude > 1e-6f ? dir.normalized : transform.forward;
            if (IsRagdolled)
            {
                ragdoll.Hit(point, ShotPush(shotDir));
                shotTime = -99f;
            }
        }

        BlockyRagdoll EnsureRagdoll()
        {
            if (ragdoll != null) return ragdoll;
            ragdoll = GetComponent<BlockyRagdoll>();
            if (ragdoll == null) ragdoll = gameObject.AddComponent<BlockyRagdoll>();
            ragdoll.RodLegs = () => RodLegsShown;
            return ragdoll;
        }

        /// <summary>Called at the very end of <see cref="ApplyLayer"/>, once this frame's pose is on the rig.</summary>
        void UpdateRagdollStart()
        {
            if (!ragdollOnDeath || IsRagdolled || shockT >= 0f || gunT >= 0f) return;
            if (bodyCarriedBy != null || bodyRelease) return; // a body in somebody's arms / just put down: physics starts once it lies
            bool shotDead = lifeState == LifeState.Dead && shotTime > -50f && action != ActionKind.KillReaction &&
                            Mathf.Abs(deathTime - shotTime) <= ShotDeathWindow && Time.time - shotTime <= ShotDeathWindow + 0.5f;
            bool killEnded = action == ActionKind.KillReaction && KillMoves.EndsDead(killMove) && actionTime >= killVictimDuration;
            bool lying = lifeState == LifeState.Dead && !pathActive && action == ActionKind.None;
            if (!shotDead && !killEnded && !lying) return;

            var r = EnsureRagdoll();
            if (shotDead)
            {
                pathActive = false; // the fall is physics now
                r.Begin(shotPoint, ShotPush(shotDir), !snappedDead);
            }
            else r.Begin(Vector3.zero, Vector3.zero, !snappedDead);
            shotTime = -99f;
            snappedDead = false;
            if (r.IsActive)
                Debug.Log($"[Ragdoll] {name}: start ({(shotDead ? "shot" : killEnded ? "kill " + killMove : "dead pose")}) {r.LastStartInfo}");
        }

        /// <summary>Ragdoll off; its pose becomes the "last output" every transition blends from.</summary>
        void StopRagdoll()
        {
            if (!IsRagdolled) return;
            ragdoll.End();
            lastOutput = CharacterPose.Capture(character);
            lastOutput.PropR = Quaternion.identity;
            hasLast = true;
            fadeT = 1f;
        }

        /// <summary>The adrenaline needle goes in / something hits the dead body: it jolts (physically when a ragdoll).</summary>
        bool JoltRagdoll()
        {
            if (!IsRagdolled) return false;
            ragdoll.Jolt();
            return true;
        }
    }
}
