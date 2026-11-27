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

        /// <summary>
        /// Round intro (before the hiding clock starts) or the end of the round (outro / end screen): everybody hears
        /// everybody - the living, the dead and the escaped - flat, at any distance and on any level, like in the lobby.
        /// Every peer reads the same networked values (phase, intro time), so the server relay and the clients agree.
        /// </summary>
        public static bool Cinematic
        {
            get
            {
                var mm = MatchManager.Instance;
                if (mm == null || mm.RaceMode) return false;
                var ph = mm.Phase.Value;
                return ph == MatchPhase.Ended || (ph == MatchPhase.Hiding && mm.IntroHoldsStart);
            }
        }

        /// <summary>No channels and no range right now (lobby, loading, intro, outro).</summary>
        public static bool AllHearAll => LobbyLike || Cinematic;

        /// <summary>True when <paramref name="p"/> is out of a running round - dead or escaped (talks / listens on the observer channel).</summary>
        public static bool OnDeadChannel(NetPlayer p) => p != null && p.IsObserver && !AllHearAll;

        /// <summary>Server: does <paramref name="listener"/> get <paramref name="speaker"/>'s voice?</summary>
        public static bool Hears(NetPlayer listener, NetPlayer speaker, bool shout)
        {
            if (AllHearAll) return true;
            bool speakerDead = speaker.IsObserver; // dead or escaped: the observer channel (the dead players' lobby)
            bool listenerDead = listener.IsObserver;
            if (speakerDead) return listenerDead;
            if (listenerDead) return true;
            float range = (shout ? VoiceChat.ShoutRange : VoiceChat.HearRange) + 5f;
            return (listener.transform.position - speaker.transform.position).sqrMagnitude <= range * range;
        }

        /// <summary>
        /// Toy phone link (round only): both are hiders, neither is dead and both carry a phone - then the listener gets
        /// the speaker through the phone, at any distance and on any level (on top of the normal proximity voice).
        /// The dead never talk through a phone (their observer channel stays as it is).
        /// </summary>
        public static bool PhoneLink(NetPlayer listener, NetPlayer speaker)
        {
            var mm = MatchManager.Instance;
            if (LobbyLike || mm == null || listener == null || speaker == null || listener == speaker) return false;
            if (!listener.IsHider || !speaker.IsHider) return false;
            if (listener.Life.Value == LifeState.Dead || speaker.Life.Value == LifeState.Dead) return false;
            return mm.HasPhone(listener) && mm.HasPhone(speaker);
        }

        /// <summary>Client: second check before playing a frame on the phone channel.</summary>
        public static bool LocalPhoneLink(NetPlayer speaker) => PhoneLink(NetPlayer.Local, speaker);

        /// <summary>How many others the local player's phone connects with right now.</summary>
        public static int LocalPhoneLinks()
        {
            int n = 0;
            foreach (var p in NetPlayer.All) if (p != null && !p.IsOwner && PhoneLink(NetPlayer.Local, p)) n++;
            return n;
        }

        /// <summary>
        /// Public address (round only): <paramref name="speaker"/> holds the wall microphone and is alive - his voice with
        /// the talk bar down goes to everybody through the loudspeakers on every level (on top of the normal voice).
        /// </summary>
        public static bool PaLink(NetPlayer speaker)
        {
            var mm = MatchManager.Instance;
            if (LobbyLike || mm == null || speaker == null || speaker.Life.Value != LifeState.Alive) return false;
            return mm.PaHolder.Value == speaker.NetworkObjectId;
        }

        /// <summary>Client: second check before playing a frame on the public-address channel.</summary>
        public static bool LocalPaLink(NetPlayer speaker) => PaLink(speaker);

        /// <summary>Client: may the local player play this speaker's voice? (the living never hear the dead)</summary>
        public static bool LocalMayHear(NetPlayer speaker) => !OnDeadChannel(speaker) || OnDeadChannel(NetPlayer.Local);
    }
}
