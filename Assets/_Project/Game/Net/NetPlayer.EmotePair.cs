using HideAndSeek.Characters;
using HideAndSeek.Core;
using HideAndSeek.Game.UI;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Server side of the pair emotes: the owner asks (<see cref="AskPairEmote"/>), the server checks both players and
    /// puts the question through <see cref="Consent"/>; on "yes" (both still free and close) it sets
    /// <see cref="PairVar"/> on both: spots on the line between them at the emote's distance (middle point stays), the
    /// facing, and one start time after the walk. It ends the pair when one-shots are over, someone stops / walks away /
    /// gets busy or hurt, the phase changes or the partner is gone.
    /// </summary>
    public partial class NetPlayer
    {
        private MatchPhase _pairPhase;

        /// <summary>Owner: asks <paramref name="target"/> for pair emote <paramref name="id"/>.</summary>
        public bool AskPairEmote(EmoteId id, NetPlayer target)
        {
            if (!IsOwner || !IsSpawned || !Emotes.IsPair(id) || Emotes.Get(id) == null) return false;
            if (!EmoteRules.Free(this, out var why) || !EmoteRules.PartnerOk(this, target, 0f, out why))
            {
                if (!string.IsNullOrEmpty(why)) GameHud.Toast(why, 2.2f);
                return false;
            }
            StopEmote();
            AskPairEmoteRpc((byte)id, target.NetworkObjectId);
            Debug.Log($"[Emote] {Nick}: asks {target.Nick} for {id}");
            return true;
        }

        [Rpc(SendTo.Server)]
        private void AskPairEmoteRpc(byte idRaw, ulong targetId, RpcParams p = default)
        {
            if (p.Receive.SenderClientId != OwnerClientId) return;
            var id = (EmoteId)idRaw;
            var info = Emotes.Get(id);
            var other = Find(targetId);
            if (info == null || !info.IsPair) return;
            if (!EmoteRules.Free(this, out var why) || !EmoteRules.PartnerOk(this, other, 0.4f, out why) || PairVar.Value.Active || other.PairVar.Value.Active)
            {
                ToastRpc(string.IsNullOrEmpty(why) ? "Teraz nie można." : why, 2.2f);
                return;
            }
            var me = this;
            Consent.Ask(this, other, ConsentKind.Emote, idRaw, info.Ask, answer =>
            {
                if (answer != ConsentAnswer.Yes || me == null || other == null) return;
                me.ServerStartPair(other, id);
            }, () => me != null && other != null && EmoteRules.PartnerOk(me, other, 1f, out _) && EmoteRules.Free(me, out _));
        }

        /// <summary>Server: both said yes - place them and start together.</summary>
        internal void ServerStartPair(NetPlayer other, EmoteId id)
        {
            var info = Emotes.Get(id);
            if (info == null || !EmoteRules.Free(this, out _) || !EmoteRules.PartnerOk(this, other, 1f, out _))
            {
                ToastRpc("Nie wyszło - ktoś się ruszył.", 2f);
                return;
            }
            Vector3 a = transform.position, b = other.transform.position;
            Vector3 axis = b - a;
            axis.y = 0f;
            if (axis.sqrMagnitude < 0.0004f) axis = transform.forward;
            axis.Normalize();
            Vector3 mid = (a + b) * 0.5f;
            float half = info.PairDistance * 0.5f;
            Vector3 spotA = mid - axis * half, spotB = mid + axis * half;
            spotA.y = a.y;
            spotB.y = b.y;
            float walk = Mathf.Max(Vector3.Distance(spotA, a), Vector3.Distance(spotB, b));
            float yaw = Mathf.Atan2(axis.x, axis.z) * Mathf.Rad2Deg;
            double start = Now + Mathf.Clamp(walk / 1.4f + 0.7f, 0.8f, 4f); // up to ~2.5 m each from AskRange 5 m
            PairVar.Value = new PairEmotePack { Id = (byte)id, Role = 0, Partner = other.NetworkObjectId, StartAt = start, Spot = spotA, Yaw = yaw };
            other.PairVar.Value = new PairEmotePack { Id = (byte)id, Role = 1, Partner = NetworkObjectId, StartAt = start, Spot = spotB, Yaw = yaw + 180f };
            _pairPhase = other._pairPhase = MatchManager.Instance.Phase.Value;
            PairArmRpc();
            other.PairArmRpc();
            Debug.Log($"[Emote] pair {id}: {Nick} + {other.Nick}, walk {walk:0.00} m, start in {start - Now:0.00} s");
        }

        [Rpc(SendTo.Owner)]
        private void PairArmRpc() => ArmEmote();

        [Rpc(SendTo.Server)]
        private void StopPairEmoteRpc(RpcParams p = default)
        {
            if (p.Receive.SenderClientId != OwnerClientId) return;
            ServerEndPair("stop");
        }

        /// <summary>Server: ends this player's pair emote and the partner's.</summary>
        internal void ServerEndPair(string why)
        {
            var pv = PairVar.Value;
            if (!pv.Active) return;
            var other = Find(pv.Partner);
            PairVar.Value = default;
            if (other != null && other.PairVar.Value.Active && other.PairVar.Value.Partner == NetworkObjectId) other.PairVar.Value = default;
            Debug.Log($"[Emote] pair end ({why}): {Nick}{(other != null ? " + " + other.Nick : "")}");
        }

        /// <summary>Server, every frame for every player.</summary>
        private void ServerPairTick()
        {
            var pv = PairVar.Value;
            if (!pv.Active) return;
            var other = Find(pv.Partner);
            var mm = MatchManager.Instance;
            var info = Emotes.Get((EmoteId)pv.Id);
            if (other == null || !other.IsSpawned || !other.PairVar.Value.Active || other.PairVar.Value.Partner != NetworkObjectId) { ServerEndPair("partner gone"); return; }
            if (pv.Role != 0) return; // the pair is checked once, from the side that asked
            if (mm == null || mm.Phase.Value != _pairPhase) { ServerEndPair("phase"); return; }
            if (!EmoteRules.Free(this, out _) || !EmoteRules.Free(other, out _)) { ServerEndPair("busy"); return; }
            double t = Now - pv.StartAt;
            Vector3 d = other.transform.position - transform.position;
            // Before the start the two still walk in from up to AskRange (+ the 1 m slack of the ask).
            float far = info != null ? info.PairDistance + (t > 0.8 ? 0.9f : Emotes.AskRange + 1.5f) : Emotes.AskRange + 1.5f;
            if (new Vector2(d.x, d.z).magnitude > far && !Cinematics.OutroDirector.Neighbours(this, other)) { ServerEndPair("apart"); return; }
            if (info == null || (!info.Loops && t >= info.Duration)) ServerEndPair("done");
        }
    }
}
