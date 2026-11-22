using HideAndSeek.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Parking public address (server side): one wall microphone per level, but one system - only one player at a time
    /// holds a microphone (<see cref="PaHolder"/>, <see cref="PaStation"/> = level of the one he took). Any living player
    /// (hider or seeker) may take a free one in the round; the server puts it back when he lets go (E), walks farther
    /// than the cord reaches, goes down, dies, gets busy (kill animation...), leaves, or the round ends. The talk bar is
    /// the voice channel 4 (<see cref="Voice.VoiceRules.PaLink"/>). Late joiners get the state from the variables.
    /// </summary>
    public partial class MatchManager
    {
        public const ulong NoPaHolder = ulong.MaxValue;

        /// <summary>NetworkObjectId of the player holding a PA microphone (<see cref="NoPaHolder"/> = free).</summary>
        public readonly NetworkVariable<ulong> PaHolder = new(NoPaHolder);
        /// <summary>Level (0, 1, 2) of the station whose microphone is in the hand.</summary>
        public readonly NetworkVariable<byte> PaStation = new();

        /// <summary>The player holding the microphone (null = free).</summary>
        public NetPlayer PaHolderPlayer => PaHolder.Value != NoPaHolder ? NetPlayer.Find(PaHolder.Value) : null;

        /// <summary>PA usable now: a hide-and-seek round in the garage.</summary>
        public bool PaRoundPhase
        {
            get
            {
                var ph = Phase.Value;
                return !RaceMode && (ph == MatchPhase.Hiding || ph == MatchPhase.Seeking) && SceneManager.GetActiveScene().name == NetSession.GameScene;
            }
        }

        /// <summary>May <paramref name="p"/> hold the microphone (alive, standing, not in an animation)?</summary>
        public bool PaCanHold(NetPlayer p) => p != null && PaRoundPhase && p.Life.Value == LifeState.Alive && !p.Busy.Value && p.Seat.Value < 0;

        /// <summary>Server: take (<paramref name="take"/>) the mic of <paramref name="station"/> or hang it back.</summary>
        public void ServerPa(NetPlayer p, bool take, byte station)
        {
            if (!IsServer || p == null) return;
            if (!take)
            {
                if (PaHolder.Value == p.NetworkObjectId) ReleasePa("hung back");
                return;
            }
            if (PaHolder.Value != NoPaHolder || station >= PaLayout.Levels || !PaCanHold(p)) return;
            Vector3 hang = PaLayout.MicHangPos(station);
            Vector3 d = p.transform.position - hang;
            // A little slack over the client's range (latency while walking up to it).
            if (new Vector2(d.x, d.z).magnitude > PaLayout.TakeRange + 0.45f || Mathf.Abs(p.transform.position.y - PaLayout.FloorY(station)) > 1.2f) return;
            PaStation.Value = station;
            PaHolder.Value = p.NetworkObjectId;
            Debug.Log($"[PA] {p.Nick} took the microphone on level {PaLevelName(station)}");
        }

        private void ReleasePa(string why)
        {
            if (PaHolder.Value == NoPaHolder) return;
            var p = PaHolderPlayer;
            Debug.Log($"[PA] microphone back on the hook ({why}){(p != null ? ", was " + p.Nick : "")}");
            PaHolder.Value = NoPaHolder;
        }

        public static string PaLevelName(int lv) => lv == 0 ? "0" : "-" + lv;

        /// <summary>Called from Update on every instance (server: puts the mic back when it may no longer be held).</summary>
        private void UpdatePa()
        {
            PaHud.Ensure();
            if (!IsServer || PaHolder.Value == NoPaHolder) return;
            var p = PaHolderPlayer;
            if (p == null) { ReleasePa("holder left"); return; }
            if (!PaRoundPhase) { ReleasePa("round over"); return; }
            if (p.Life.Value != LifeState.Alive) { ReleasePa(p.Life.Value == LifeState.Dead ? "holder dead" : "holder down"); return; }
            if (p.Busy.Value || p.Seat.Value >= 0) { ReleasePa("holder busy"); return; }
            Vector3 chest = p.transform.position + Vector3.up * 1.2f;
            if (Vector3.Distance(chest, PaLayout.GlandPos(PaStation.Value)) > PaLayout.MaxReach + 0.25f) ReleasePa("cord pulled out of the hand");
        }
    }
}
