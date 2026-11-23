using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Consent for the body carry: the carrier asks (B held on an injured hider), the other player gets
    /// "&lt;nick&gt; chce cię podnieść - [Y] tak / [N] nie" for <see cref="CarryConsentTime"/> s
    /// (<see cref="UI.CarryConsentPrompt"/>), the server starts the lift only on "yes". Kept apart from the carry
    /// itself so it can later be switched over to a shared consent system (same keys Y / N).
    /// Test switch <c>-autoconsent</c>: this player says yes to every request after a moment.
    /// </summary>
    public partial class NetPlayer
    {
        public const float CarryConsentTime = 10f;

        /// <summary>Test: answer "yes" automatically.</summary>
        public static readonly bool AutoConsent = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-autoconsent") >= 0;

        // Server: pending questions, carried player's id -> (carrier's id, valid until).
        private static readonly Dictionary<ulong, (ulong carrier, double until)> _carryAsks = new();

        /// <summary>Owner of the carrier: asks <paramref name="targetId"/> whether he may be carried.</summary>
        [Rpc(SendTo.Server)]
        public void RequestCarryRpc(ulong targetId)
        {
            var target = Find(targetId);
            var mm = MatchManager.Instance;
            if (mm == null) return;
            if (!CanCarry(this, target, out string why))
            {
                if (!string.IsNullOrEmpty(why)) ToastRpc(why, 2.5f);
                return;
            }
            if (_carryAsks.TryGetValue(targetId, out var a) && a.until > mm.Now && a.carrier != NetworkObjectId)
            {
                ToastRpc($"Ktoś inny już pyta {target.Nick}.", 2.5f);
                return;
            }
            _carryAsks[targetId] = (NetworkObjectId, mm.Now + CarryConsentTime + 0.5);
            target.CarryAskRpc(NetworkObjectId, NickVar.Value, RpcTarget.Single(target.OwnerClientId, RpcTargetUse.Temp));
            CarryAskSentRpc(targetId);
            Debug.Log($"[Carry] {Nick} asks {target.Nick}");
        }

        /// <summary>The question on the carried player's screen.</summary>
        [Rpc(SendTo.SpecifiedInParams)]
        private void CarryAskRpc(ulong carrierId, FixedString32Bytes nick, RpcParams rpcParams)
        {
            if (!IsOwner) return;
            UI.CarryConsentPrompt.Ask(nick.ToString(), CarryConsentTime, AutoConsent, yes => RespondCarryRpc(carrierId, yes));
        }

        /// <summary>The carrier's own screen: waiting for the answer.</summary>
        [Rpc(SendTo.Owner)]
        private void CarryAskSentRpc(ulong targetId)
        {
            var t = Find(targetId);
            UI.CarryConsentPrompt.Waiting(t != null ? t.Nick : "?", CarryConsentTime);
        }

        /// <summary>Owner of the asked player: yes / no (or no answer in time = no).</summary>
        [Rpc(SendTo.Server)]
        public void RespondCarryRpc(ulong carrierId, bool yes)
        {
            if (!_carryAsks.TryGetValue(NetworkObjectId, out var a) || a.carrier != carrierId) return;
            _carryAsks.Remove(NetworkObjectId);
            var carrier = Find(carrierId);
            var mm = MatchManager.Instance;
            if (carrier == null || mm == null) return;
            carrier.CarryAnsweredRpc(yes);
            if (mm.Now > a.until) { carrier.ToastRpc($"{Nick} nie odpowiedział.", 2.5f); return; }
            if (!yes)
            {
                carrier.ToastRpc($"{Nick} nie chce, żebyś go niósł.", 3f);
                Debug.Log($"[Carry] {Nick} said no to {carrier.Nick}");
                return;
            }
            carrier.ServerStartCarry(this);
        }

        [Rpc(SendTo.Owner)]
        private void CarryAnsweredRpc(bool yes) => UI.CarryConsentPrompt.Answered();
    }
}
