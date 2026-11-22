using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game.Voice
{
    /// <summary>
    /// Who hears whom. Lobby: everybody. Round: the living hear the living within range; the dead talk on their own
    /// observer channel (heard by the dead only, everywhere, without 3D) and still hear the living.
    /// Used by the server when relaying (dead voices are never even sent to the living) and by every client as a
    /// second check before playing.
    /// </summary>
    public static class VoiceRules
    {
        /// <summary>Lobby or loading: no channels, no range limit.</summary>
        public static bool LobbyLike
        {
            get
            {
                var mm = MatchManager.Instance;
                return mm == null || mm.Phase.Value == MatchPhase.Lobby || mm.Phase.Value == MatchPhase.Loading;
            }
        }

        /// <summary>True when <paramref name="p"/> is dead in a running round (talks / listens on the observer channel).</summary>
        public static bool OnDeadChannel(NetPlayer p) => p != null && p.Life.Value == LifeState.Dead && !LobbyLike;

        /// <summary>Server: does <paramref name="listener"/> get <paramref name="speaker"/>'s voice?</summary>
        public static bool Hears(NetPlayer listener, NetPlayer speaker, bool shout)
        {
            if (LobbyLike) return true;
            bool speakerDead = speaker.Life.Value == LifeState.Dead;
            bool listenerDead = listener.Life.Value == LifeState.Dead;
            if (speakerDead) return listenerDead;
            if (listenerDead) return true;
            float range = (shout ? VoiceChat.ShoutRange : VoiceChat.HearRange) + 5f;
            return (listener.transform.position - speaker.transform.position).sqrMagnitude <= range * range;
        }

        /// <summary>Client: may the local player play this speaker's voice? (the living never hear the dead)</summary>
        public static bool LocalMayHear(NetPlayer speaker) => !OnDeadChannel(speaker) || OnDeadChannel(NetPlayer.Local);
    }
}
