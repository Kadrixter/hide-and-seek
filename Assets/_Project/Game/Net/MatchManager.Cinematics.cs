using System.Collections.Generic;
using HideAndSeek.Audio;
using HideAndSeek.Characters;
using HideAndSeek.Game.Cinematics;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Round intro and outro (Game/Cinematics). Server: the intro start time and the killer are chosen when the map
    /// is loaded (the hiding time starts after the intro: <see cref="PhaseEnd"/> is pushed back), the round
    /// statistics are sampled (<see cref="RoundStats"/>) and on the end of the round the row of winners and the awards
    /// are sent once (<see cref="OutroRpc"/>); the Ended phase lasts as long as the outro. Every peer plays the
    /// sequences itself from the server time. Host switches: -nointro, -nooutro.
    /// </summary>
    public partial class MatchManager
    {
        /// <summary>Server time of the intro start; -1 = an intro will play once the map is loaded, 0 = none.</summary>
        public readonly NetworkVariable<double> IntroAt = new();
        public readonly NetworkVariable<ulong> IntroKiller = new(ulong.MaxValue);
        public readonly NetworkVariable<int> IntroSeed = new();

        static readonly bool NoIntro = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-nointro") >= 0;
        static readonly bool NoOutro = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-nooutro") >= 0;

        bool _hidingBannerShown;
        bool _introStarted;

        /// <summary>The intro is on now (server time inside it): nobody moves, the hiding time has not started.</summary>
        public bool IntroRunning => IntroAt.Value > 0d && Now < IntroAt.Value + IntroTimeline.Length;

        /// <summary>The intro is still ahead or running: the "CHOWASZ SIĘ" banner waits for its end.</summary>
        public bool IntroHoldsStart => !RaceMode && (IntroAt.Value < 0d || IntroRunning);

        // ================================================================ server

        /// <summary>StartMatch: an intro will play after the load.</summary>
        void PrepareIntro()
        {
            IntroAt.Value = NoIntro || RaceMode ? 0d : -1d;
            IntroKiller.Value = ulong.MaxValue;
            RoundStats.Clear();
        }

        /// <summary>The map is loaded and everybody placed: intro timing, the hiding clock starts after it.</summary>
        void BeginIntro()
        {
            RoundStats.BeginRound(Now);
            if (IntroAt.Value >= 0d) return;
            var seekers = new List<NetPlayer>();
            foreach (var p in NetPlayer.All) if (p != null && p.IsSeeker) seekers.Add(p);
            IntroKiller.Value = seekers.Count > 0 ? seekers[Random.Range(0, seekers.Count)].NetworkObjectId : ulong.MaxValue;
            IntroSeed.Value = Random.Range(0, 1 << 20);
            IntroAt.Value = Now + IntroTimeline.Lead;
            PhaseEnd.Value += IntroTimeline.Lead + IntroTimeline.Length;
            Debug.Log($"[Cine] server: intro at {IntroAt.Value:0.00}, killer {IntroKiller.Value}, hiding ends {PhaseEnd.Value:0.00}");
        }

        /// <summary>EndMatch: the outro data for everybody; returns how long the Ended phase lasts.</summary>
        float BeginOutro(MatchWinner w)
        {
            RoundStats.EndRound(Now);
            if (NoOutro || RaceMode || w == MatchWinner.Racer) return Tuning.EndScreenTime;
            bool seekersWon = w == MatchWinner.Seeker;
            var row = new List<NetPlayer>();
            int hiders = 0, alive = 0;
            foreach (var p in NetPlayer.All)
            {
                if (p == null || !p.IsSpawned) continue;
                if (p.IsHider) { hiders++; if (p.Life.Value != HideAndSeek.Core.LifeState.Dead) alive++; }
                if (p.IsSeeker == seekersWon) row.Add(p);
            }
            // Award holders in the middle of the row, the rest around them.
            var stats = RoundStats.Current;
            var awards = stats != null ? stats.ComputeAwards(seekersWon) : new List<AwardPack>();
            row.Sort((a, b) => Score(b, awards).CompareTo(Score(a, awards)));
            var ordered = new List<ulong>();
            for (int i = 0; i < row.Count; i++)
            {
                if (i % 2 == 0) ordered.Add(row[i].NetworkObjectId);
                else ordered.Insert(0, row[i].NetworkObjectId);
            }
            var pack = new OutroPack
            {
                Winner = (byte)w,
                StartAt = Now + 0.15,
                Row = ordered.ToArray(),
                Awards = awards.ToArray(),
                Survivors = (byte)alive,
                Hiders = (byte)hiders,
            };
            OutroRpc(pack);
            Debug.Log($"[Cine] server: outro {(seekersWon ? "seekers" : "hiders")} row={ordered.Count} awards={awards.Count}");
            return OutroTimeline.Phase + 0.15f;
        }

        static int Score(NetPlayer p, List<AwardPack> awards)
        {
            int s = 0;
            foreach (var a in awards) if (a.Holder == p.NetworkObjectId) s++;
            return s;
        }

        [Rpc(SendTo.Everyone)]
        void OutroRpc(OutroPack pack)
        {
            if (SceneManager.GetActiveScene().name != NetSession.GameScene) return;
            OutroDirector.Play(pack, () => Now);
        }

        /// <summary>Tests: ends the round now with this winner (same path as a real end).</summary>
        public void TestEndMatch(MatchWinner w) => EndMatch(w);

        // ================================================================ every peer, every frame

        void UpdateCinematics()
        {
            if (IsServer) RoundStats.Tick(this, Time.deltaTime);
            var ph = Phase.Value;
            if (ph == MatchPhase.Loading || ph == MatchPhase.Lobby)
            {
                _hidingBannerShown = false;
                _introStarted = false;
                if (OutroDirector.Current == null) OutroDirector.Row.Clear();
                return;
            }
            bool inGame = SceneManager.GetActiveScene().name == NetSession.GameScene && !RaceMode;
            if (!inGame) return;
            if (ph == MatchPhase.Hiding || ph == MatchPhase.Seeking || ph == MatchPhase.Ended) ExitGate.Ensure();
            if (ph != MatchPhase.Hiding) return;

            if (IntroAt.Value > 0d && !_introStarted && IntroDirector.Current == null)
            {
                double t = Now - IntroAt.Value;
                if (t > -IntroTimeline.Lead - 0.5 && t < IntroTimeline.Length - 0.6)
                {
                    _introStarted = true;
                    var local = NetPlayer.Local;
                    IntroDirector.Play(IntroAt.Value, () => Now, NetPlayer.Find(IntroKiller.Value), IntroSeed.Value, local != null && local.IsSeeker);
                }
            }
            if (!_hidingBannerShown && !IntroHoldsStart)
            {
                _hidingBannerShown = true;
                if (IntroAt.Value != 0d) HidingStartedLocal(); // without an intro OnPhaseChanged showed it already
            }
        }
    }
}
