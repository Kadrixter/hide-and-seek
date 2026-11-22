using HideAndSeek.Characters;
using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Dead bodies as ragdolls (<see cref="BlockyRagdoll"/>, started by the action layer). The network only carries
    /// what it already did - the Dead state, the kill variant, cut-off limbs and the Glock shot (hit point + direction
    /// from the server) - and every client simulates the fall from that same start. Players who join later see the body
    /// settle from the plain dead pose where it lies. Test switch <c>-noragdoll</c> keeps the old frozen dead pose.
    /// </summary>
    public partial class NetPlayer
    {
        private static readonly bool RagdollDisabled = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-noragdoll") >= 0;

        /// <summary>The ragdoll of this player's body (null with -noragdoll).</summary>
        public BlockyRagdoll Ragdoll { get; private set; }

        /// <summary>Called once the character is built, before the network state is applied.</summary>
        private void SetupRagdoll()
        {
            if (RagdollDisabled || !(Actions is BlockyCharacterActions bca)) return;
            bca.RagdollOnDeath = true;
            Ragdoll = bca.Ragdoll;
            if (Ragdoll != null) Ragdoll.Settled += OnBodySettled;
            // Joined while this player lies dead: no fall animation, the body settles from the dead pose where it is.
            if (Life.Value == LifeState.Dead) bca.SetLifeStateImmediate(LifeState.Dead);
        }

        /// <summary>The body came to rest: blood seeps out under it, where it really ended up.</summary>
        private void OnBodySettled(BlockyRagdoll r)
        {
            if (Life.Value != LifeState.Dead) return;
            bool cut = Severed.Value != 0;
            Vector3 at = r.TorsoCenter;
            BloodFx.Pool(at, Random.Range(0.75f, 1.05f) * (cut ? 1.25f : 1f), Random.Range(16f, 24f));
            Debug.Log($"[Ragdoll] {Nick}: settled after {r.SettleTime:0.00} s, {Vector3.Distance(at, transform.position):0.00} m from the root, lowest {r.LowestPoint - transform.position.y:0.000} m");
        }

        /// <summary>Glock bullet that hit this player (every client, same values from the server).</summary>
        private void OnBulletHit(Vector3 hitPoint, Vector3 dir)
        {
            (Actions as BlockyCharacterActions)?.NotifyBulletHit(hitPoint, dir);
        }
    }
}
