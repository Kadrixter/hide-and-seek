using System.Collections;
using System.Collections.Generic;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using HideAndSeek.Game.UI;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Start lever: the room owner pulls it (same rules as before: everyone else ready, enough players, mode ready).
    /// Allowed -> the puller is placed in front of it, everybody plays the pull, then a 5..0 countdown and the round
    /// starts. If during the countdown someone is no longer ready (dropped it, or a new player joined) or there are
    /// too few players, it is cancelled for everyone with the reason and the lever springs back up.
    /// Refused -> the lever rattles for everyone and the puller gets the reason as a toast.
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
            float t = 0f;
            bool freed = false;
            while (t < LeverTimeline.StartMatch)
            {
                t += Time.deltaTime;
                if (!freed && t >= LeverTimeline.End)
                {
                    freed = true;
                    if (puller != null) puller.Busy.Value = false;
                }
                if (Phase.Value != MatchPhase.Lobby) { _leverBusy = false; yield break; }
                // From the countdown on: anyone not ready (dropped it, or just joined) or leaving cancels it.
                if (t >= LeverTimeline.CountdownAt)
                {
                    string why = LeverAbortReason();
                    if (why != null)
                    {
                        Debug.Log("[Lever] countdown cancelled: " + why);
                        _leverBusy = false;
                        if (puller != null) puller.Busy.Value = false;
                        LeverAbortRpc(why);
                        yield break;
                    }
                }
                yield return null;
            }
            _leverBusy = false;
            if (LeverStay)
            {
                Debug.Log("[Lever] countdown done (test: round not started)");
                yield break;
            }
            Debug.Log("[Lever] countdown done - starting the round");
            StartMatch();
        }

        /// <summary>Server: why the countdown has to stop now, or null when the round can still start.</summary>
        private string LeverAbortReason()
        {
            if (NetPlayer.All.Count < GameSettings.MinPlayers && !RaceMode) return "za mało graczy.";
            if (!GameModes.Get(Settings.Value.Mode).Ready) return "wybrany tryb nie jest gotowy.";
            if (SeekerCountProblem() is string tooMany) return tooMany;
            var notReady = new List<string>();
            foreach (var pl in NetPlayer.All)
                if (pl != null && pl.OwnerClientId != NetworkManager.ServerClientId && !pl.Ready.Value) notReady.Add(pl.Nick);
            if (notReady.Count == 0) return null;
            string list = string.Join(", ", notReady);
            if (list.Length > 60) list = list.Substring(0, 60) + "...";
            return notReady.Count == 1 ? list + " nie jest gotowy." : list + " nie są gotowi.";
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

        /// <summary>Everybody: the countdown was cancelled - message on screen, the lever springs back up.</summary>
        [Rpc(SendTo.Everyone)]
        private void LeverAbortRpc(FixedString128Bytes why)
        {
            string text = why.ToString();
            Debug.Log("[Lever] cancelled: " + text);
            LobbyButton.Instance?.ResetLever(text);
            GameHud.Toast("Odliczanie przerwane: " + text, 4f);
        }
    }
}
