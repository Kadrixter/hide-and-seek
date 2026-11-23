using HideAndSeek.Game.UI;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Network part of <see cref="Consent"/>: the prompt for the asked player, the "waiting" line for the asker, the
    /// answer back to the server. The server API is <see cref="Consent.Ask"/>; the owner answers with
    /// <see cref="AnswerConsent"/> (Y / N in <see cref="ConsentPrompt"/>).
    /// </summary>
    public partial class NetPlayer
    {
        /// <summary>Owner of the asked player: shows the prompt.</summary>
        [Rpc(SendTo.Owner)]
        internal void ConsentAskRpc(uint id, ulong fromId, byte kind, byte arg, FixedString64Bytes text, float seconds)
        {
            var from = Find(fromId);
            ConsentPrompt.ShowAsk(id, from != null ? from.Nick : "Ktoś", text.ToString(), seconds, (ConsentKind)kind, arg);
        }

        /// <summary>Owner of the asked player: the question is over (timeout / cancelled / answered).</summary>
        [Rpc(SendTo.Owner)]
        internal void ConsentCloseRpc(uint id, byte answer) => ConsentPrompt.CloseAsk(id, (ConsentAnswer)answer);

        /// <summary>Owner of the asking player: waiting for the other one.</summary>
        [Rpc(SendTo.Owner)]
        internal void ConsentWaitRpc(uint id, ulong toId, float seconds)
        {
            var to = Find(toId);
            ConsentPrompt.ShowWait(id, to != null ? to.Nick : "?", seconds);
        }

        /// <summary>Owner of the asking player: the answer.</summary>
        [Rpc(SendTo.Owner)]
        internal void ConsentResultRpc(uint id, byte answer, ulong toId)
        {
            var to = Find(toId);
            ConsentPrompt.CloseWait(id, (ConsentAnswer)answer, to != null ? to.Nick : "?");
        }

        /// <summary>Owner: answers question <paramref name="id"/>.</summary>
        public void AnswerConsent(uint id, bool yes)
        {
            if (!IsOwner || !IsSpawned) return;
            ConsentAnswerRpc(id, yes);
        }

        [Rpc(SendTo.Server)]
        private void ConsentAnswerRpc(uint id, bool yes, RpcParams p = default)
        {
            if (p.Receive.SenderClientId != OwnerClientId) return;
            Consent.Answer(this, id, yes);
        }

        /// <summary>Owner of the asker: withdraws the own open question (Esc / moving away handled by the server).</summary>
        public void WithdrawConsent(uint id)
        {
            if (IsOwner && IsSpawned) WithdrawConsentRpc(id);
        }

        [Rpc(SendTo.Server)]
        private void WithdrawConsentRpc(uint id, RpcParams p = default)
        {
            if (p.Receive.SenderClientId != OwnerClientId) return;
            Consent.Withdraw(this, id);
        }
    }
}
