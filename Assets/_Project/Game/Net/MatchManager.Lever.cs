using System.Collections;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Start lever: the room owner pulls it (same rules as before: everyone else ready, enough players, mode ready).
    /// Allowed -> the puller is placed in front of it, everybody plays the pull, the round starts once the blades
    /// are in the jaws. Refused -> the lever rattles for everyone and the puller gets the reason as a toast.
    /// </summary>
    public partial class MatchManager
    {
        /// <summary>Tests: play the pull but do not start the round (-leverstay, -levershots).</summary>
        static readonly bool LeverStay = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-leverstay") >= 0
                                         || System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-levershots") >= 0;

        bool _leverBusy;

        [Rpc(SendTo.Server)]
        public void RequestLeverRpc(RpcParams p = default)
        {
            if (Phase.Value != MatchPhase.Lobby || _leverBusy) return;
            var lever = LobbyButton.Instance;
            ulong sender = p.Receive.SenderClientId;
            var puller = NetPlayer.FindByClient(sender);
            if (lever == null || puller == null || puller.Busy.Value || puller.Life.Value != LifeState.Alive || puller.Seat.Value >= 0) return;
            if (!CanStartFrom(sender))
            {
                LeverJamRpc();
                return;
            }
            _leverBusy = true;
            puller.Busy.Value = true;
            puller.TeleportRpc(lever.StandPoint, lever.StandYaw);
            LeverPullRpc(puller.NetworkObjectId);
            StartCoroutine(LeverThenStart(puller));
        }

        private IEnumerator LeverThenStart(NetPlayer puller)
        {
            yield return new WaitForSeconds(LeverTimeline.StartMatch);
            _leverBusy = false;
            if (puller != null) puller.Busy.Value = false;
            if (Phase.Value != MatchPhase.Lobby) yield break;
            if (LeverStay)
            {
                Debug.Log("[Lever] pull done (test: round not started)");
                yield break;
            }
            // Someone dropped their ready (or left) during the pull: the lever springs back.
            if (puller != null && CanStartFrom(puller.OwnerClientId)) StartMatch();
            else LeverResetRpc();
        }

        [Rpc(SendTo.Everyone)]
        private void LeverPullRpc(ulong playerId)
        {
            Debug.Log("[Lever] pull by " + playerId);
            LobbyButton.Instance?.BeginPull();
            NetPlayer.Find(playerId)?.PlayLeverPull();
        }

        [Rpc(SendTo.Everyone)]
        private void LeverJamRpc()
        {
            Debug.Log("[Lever] locked - rattles");
            LobbyButton.Instance?.Jam();
        }

        [Rpc(SendTo.Everyone)]
        private void LeverResetRpc() => LobbyButton.Instance?.ResetLever();
    }
}
