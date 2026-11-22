using System.Collections.Generic;
using HideAndSeek.Audio;
using HideAndSeek.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// RC race mode (game mode <see cref="GameMode.RcRace"/>): everybody gets a car on the race track next to the
    /// garage scene, the drivers sit on the grandstand. Countdown, then the first one to finish the set number of
    /// laps wins (time limit 10 min: the one furthest ahead wins).
    /// </summary>
    public partial class MatchManager
    {
        public readonly NetworkVariable<double> RaceStartAt = new();
        public readonly NetworkVariable<ulong> RaceWinnerId = new(ulong.MaxValue);

        public bool RaceMode => Settings.Value.Mode == (byte)GameMode.RcRace;

        private const float RaceCountdown = 5f, RaceTimeLimit = 600f;

        private void StartRace()
        {
            var players = new List<NetPlayer>(NetPlayer.All);
            players.RemoveAll(x => x == null);
            foreach (var pl in players)
            {
                pl.Role.Value = PlayerRole.None;
                ResetPlayer(pl);
            }
            Winner.Value = MatchWinner.None;
            RaceWinnerId.Value = ulong.MaxValue;
            RaceStartAt.Value = 0;
            GlockPhase.Value = false;
            ChainsawTaken.Value = false;
            SecurityCrates.Value = 0;
            ResetRcCars();
            Phase.Value = MatchPhase.Loading;
            LoadGameSceneOrStay("race start");
        }

        private void OnRaceSceneLoaded()
        {
            RaceSystem.Ensure();
            var order = new List<NetPlayer>(NetPlayer.All);
            order.RemoveAll(x => x == null);
            order.Sort((a, b) => a.OwnerClientId.CompareTo(b.OwnerClientId));
            for (int i = 0; i < order.Count; i++)
                order[i].TeleportRpc(RaceSystem.SeatOf(i), 0f);
            Phase.Value = MatchPhase.Race;
            RaceStartAt.Value = Now + RaceCountdown;
            PhaseEnd.Value = Now + RaceCountdown + RaceTimeLimit;
            Debug.Log($"[Race] {order.Count} drivers, {Settings.Value.Laps} laps, start in {RaceCountdown:0} s");
        }

        private void UpdateRaceServer(double now)
        {
            int laps = Mathf.Max(1, Settings.Value.Laps);
            int need = 1 + laps * RaceTrack.Checkpoints;
            NetPlayer best = null;
            int bestCps = -1;
            int racers = 0;
            foreach (var pl in NetPlayer.All)
            {
                if (pl == null) continue;
                racers++;
                int cps = pl.RaceCps.Value;
                if (cps >= need)
                {
                    FinishRace(pl);
                    return;
                }
                if (cps > bestCps) { bestCps = cps; best = pl; }
            }
            if (racers == 0) { BackToLobby("race without racers"); return; }
            if (now >= PhaseEnd.Value && best != null) FinishRace(best);
        }

        private void FinishRace(NetPlayer winner)
        {
            RaceWinnerId.Value = winner.NetworkObjectId;
            Debug.Log($"[Race] winner {winner.Nick} ({winner.RaceCps.Value} checkpoints)");
            EndMatch(MatchWinner.Racer);
        }

        private void RaceEndedLocal()
        {
            var w = NetPlayer.Find(RaceWinnerId.Value);
            bool iWon = w != null && w == NetPlayer.Local;
            GameAudio.Play2D(iWon ? GameSfx.WinSting : GameSfx.LoseSting);
            GameHud.Banner(w != null ? $"WYGRYWA {w.Nick.ToUpperInvariant()}" : "KONIEC WYŚCIGU",
                iWon ? "Pierwszy na mecie :D" : "Następnym razem :p", Tuning.EndScreenTime - 1f, iWon ? UI.UiKit.Accent : UI.UiKit.Danger);
        }
    }
}
