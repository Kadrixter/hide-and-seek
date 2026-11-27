using System.Collections;
using HideAndSeek.Characters;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// 2026-11-27: the sofa spring (decided by the sitter's own client, LocalPlayerController.SofaSpring): a spring
    /// pops out of the seat cushion and throws the sitter off; after <see cref="SpringPushStart"/> s the player bends
    /// over and pushes it back in (BlockyCharacterActions.SpringPush), on every peer.
    /// </summary>
    public partial class NetPlayer
    {
        /// <summary>Seconds from the pop to the start of the push (the throw, the landing and the step back to the sofa).</summary>
        public const float SpringPushStart = 1.5f;

        /// <summary>Cushion surface over a seat (where the spring comes out).</summary>
        public static Vector3 SpringSurface(LobbySeat s) => s.AimPoint + Vector3.down * 0.03f;

        [Rpc(SendTo.Everyone)]
        public void SofaSpringRpc(byte seat)
        {
            var s = LobbySeat.Get(seat);
            if (s == null) return;
            Vector3 surface = SpringSurface(s);
            SofaSpringFx.Play(surface, SpringPushStart);
            (Actions as BlockyCharacterActions)?.PlayHitReaction(); // a jolt as it goes off
            StartCoroutine(SpringPushLater(surface));
            Debug.Log($"[Sofa] {Nick}: a spring popped out of seat {seat} ({s.transform.parent?.name})");
        }

        private IEnumerator SpringPushLater(Vector3 surface)
        {
            yield return new WaitForSeconds(SpringPushStart);
            (Actions as BlockyCharacterActions)?.PlaySpringPush(surface);
        }
    }
}
