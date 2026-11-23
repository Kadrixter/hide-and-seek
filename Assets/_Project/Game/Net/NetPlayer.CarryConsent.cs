using HideAndSeek.Core;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Consent for the body carry: the carrier asks (B held on an injured hider), the other player gets
    /// "&lt;nick&gt; chce cię podnieść - [Y] tak / [N] nie" for <see cref="CarryConsentTime"/> s
    /// (<see cref="UI.CarryConsentPrompt"/>), the server starts the lift only on "yes".
    /// <para>
    /// The whole question lives in ONE server method, <see cref="ServerAskCarry"/>: it asks and calls
    /// <see cref="ServerCarryAnswered"/> exactly once. To switch over to the shared consent system replace its body with
    /// <c>Consent.Ask(this, target, ConsentKind.Carry, 0, "chce cię podnieść i nieść na rękach",
    /// a =&gt; ServerCarryAnswered(target, a == ConsentAnswer.Yes), () =&gt; CanCarry(this, target, out _))</c> and delete
    /// the RPCs below it plus <c>UI/CarryConsentPrompt.cs</c>'s question panel (its status line stays).
    /// </para>
    /// Test switch <c>-autoconsent</c>: this player says yes to every request after a moment.
    /// </summary>
    public partial class NetPlayer
    {
        public const float CarryConsentTime = 10f;

        /// <summary>Test: answer "yes" automatically.</summary>
        public static readonly bool AutoConsent = Array.IndexOf(Environment.GetCommandLineArgs(), "-autoconsent") >= 0;

        /// <summary>Owner of the carrier: asks <paramref name="targetId"/> whether he may be carried.</summary>
        [Rpc(SendTo.Server)]
        public void RequestCarryRpc(ulong targetId)
        {
            var target = Find(targetId);
            if (!CanCarry(this, target, out string why))
            {
                if (!string.IsNullOrEmpty(why)) ToastRpc(why, 2.5f);
                return;
            }
            if (target.Life.Value == LifeState.Dead) ServerStartCarry(target); // a body: nobody to ask
            else ServerAskCarry(target);
        }

        /// <summary>Server: both agreed (or not) - the single answer of <see cref="ServerAskCarry"/>.</summary>
        private void ServerCarryAnswered(NetPlayer target, bool yes)
        {
            if (target == null) return;
            if (!yes)
            {
                ToastRpc($"{target.Nick} nie chce, żebyś go niósł.", 3f);
                Debug.Log($"[Carry] {target.Nick} said no to {Nick}");
                return;
            }
            ServerStartCarry(target);
        }

        // ================================================================ own consent (replaceable by the shared one)

        // Server: pending questions, carried player's id -> (carrier's id, valid until).
        private static readonly Dictionary<ulong, (ulong carrier, double until)> _carryAsks = new();

        /// <summary>Server: asks <paramref name="target"/>; calls <see cref="ServerCarryAnswered"/> once (no answer = no).</summary>
        private void ServerAskCarry(NetPlayer target)
        {
            if (target == null) return;
            // Shared consent system (Game/Net/Consent.cs, the same [Y]/[N] prompt as the pair emotes).
            Consent.Ask(this, target, ConsentKind.Carry, 0, "chce cię podnieść i nieść na rękach",
                a => ServerCarryAnswered(target, a == ConsentAnswer.Yes), () => CanCarry(this, target, out _));
            Debug.Log($"[Carry] {Nick} asks {target.Nick} (Consent)");
        }

        // Old carry-only question (kept unused for now; the shared Consent above replaces it).
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
        private void RespondCarryRpc(ulong carrierId, bool yes)
        {
            if (!_carryAsks.TryGetValue(NetworkObjectId, out var a) || a.carrier != carrierId) return;
            _carryAsks.Remove(NetworkObjectId);
            var carrier = Find(carrierId);
            var mm = MatchManager.Instance;
            if (carrier == null || mm == null) return;
            carrier.CarryAnsweredRpc();
            if (mm.Now > a.until) { carrier.ToastRpc($"{Nick} nie odpowiedział.", 2.5f); return; }
            carrier.ServerCarryAnswered(this, yes);
        }

        [Rpc(SendTo.Owner)]
        private void CarryAnsweredRpc() => UI.CarryConsentPrompt.Answered();
    }
}
