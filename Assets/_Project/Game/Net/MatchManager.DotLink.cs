using System.Threading.Tasks;
using HideAndSeek.Core;
using HideAndSeek.Game.Cinematics;
using HideAndSeek.Game.DotLink;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Dot link panels at the broken exit-gate levers (server side). Since 2026-11-28 every gate (0 = east, 1 = west)
    /// has its OWN panel: its own 10 x 10 puzzle with one solution (made off the main thread at every round start), its
    /// own drawn lines and its own user - working on one does nothing to the other. The panels take players from stage
    /// DotLink on; solving one opens only that gate (<see cref="ServerEscapeGateStart"/>, stage -> GateOpening for the
    /// first one), the other panel stays open until it is solved too. Per gate: one hider at a time
    /// (<see cref="DotLinkUserOf"/>); the lines live here and in <see cref="DotLinkPathsOf"/>, so everybody near sees them
    /// live, late joiners get them, and they stay when the user leaves (Esc), goes down, dies or gets busy. A drag is sent
    /// as it goes (each message replaces the line on the board as it was when the drag began: crossed lines come back
    /// when the pointer goes back, as in Flow Free). The user is let go 1.8 s after solving.
    /// </summary>
    public partial class MatchManager
    {
        public const ulong NoDotLinkUser = ulong.MaxValue;
        public const int DotLinkSize = 10, DotLinkMinColors = 10, DotLinkMaxColors = 12;
        const float DotLinkSolvedHold = 1.8f;

        // Gate 0 (east) keeps the old names (tests read them), gate 1 (west) has its own copies.
        public readonly NetworkVariable<FixedString128Bytes> DotLinkPuzzleNet = new(), DotLinkPuzzleNetW = new();
        public readonly NetworkVariable<FixedString512Bytes> DotLinkPathsNet = new(), DotLinkPathsNetW = new();
        public readonly NetworkVariable<ulong> DotLinkUser = new(NoDotLinkUser), DotLinkUserW = new(NoDotLinkUser);

        /// <summary>Server state of one gate's panel.</summary>
        sealed class DotLinkGateState
        {
            public DotLinkPuzzle Puzzle;
            public DotLinkBoard Board, DragBase;
            public byte DragId;
            public Task<(DotLinkPuzzle, DotLinkGenerator.Stats, double)> Task;
            public double SolvedAt = -1;
            public bool UserHurt;
        }

        readonly DotLinkGateState[] _dl = { new DotLinkGateState(), new DotLinkGateState() };
        MatchPhase _dlLastPhase = MatchPhase.Lobby;

        public NetworkVariable<FixedString128Bytes> DotLinkPuzzleOf(int g) => g == 1 ? DotLinkPuzzleNetW : DotLinkPuzzleNet;
        public NetworkVariable<FixedString512Bytes> DotLinkPathsOf(int g) => g == 1 ? DotLinkPathsNetW : DotLinkPathsNet;
        public NetworkVariable<ulong> DotLinkUserOf(int g) => g == 1 ? DotLinkUserW : DotLinkUser;

        public NetPlayer DotLinkUserPlayerOf(int g)
        {
            ulong id = DotLinkUserOf(g).Value;
            return id != NoDotLinkUser ? NetPlayer.Find(id) : null;
        }

        /// <summary>Server copy of the east gate's puzzle with its solution (tests).</summary>
        public DotLinkPuzzle DotLinkServerPuzzle => _dl[0].Puzzle;
        /// <summary>The east gate's panel user (tests).</summary>
        public NetPlayer DotLinkUserPlayer => DotLinkUserPlayerOf(0);

        /// <summary>The gate whose panel <paramref name="p"/> works on (-1 = none).</summary>
        public int DotLinkGateOf(NetPlayer p)
        {
            if (p == null) return -1;
            for (int g = 0; g < ExitGate.Count; g++)
                if (DotLinkUserOf(g).Value == p.NetworkObjectId) return g;
            return -1;
        }

        /// <summary>A hide-and-seek round runs in the garage.</summary>
        public bool DotLinkRoundPhase
        {
            get
            {
                var ph = Phase.Value;
                return !RaceMode && (ph == MatchPhase.Hiding || ph == MatchPhase.Seeking) && SceneManager.GetActiveScene().name == NetSession.GameScene;
            }
        }

        /// <summary>Gate <paramref name="g"/>'s panel takes a player now (stage DotLink or later, puzzle ready, that gate still shut).</summary>
        public bool DotLinkOpenAt(int g) =>
            Escape.Value >= EscapeStage.DotLink && DotLinkRoundPhase && DotLinkPuzzleOf(g).Value.Length > 0 && EscapeGateAtOf(g).Value <= 0d;

        /// <summary>Any panel takes a player now.</summary>
        public bool DotLinkOpen => DotLinkOpenAt(0) || DotLinkOpenAt(1);

        /// <summary>May <paramref name="p"/> work on a panel (a living hider, standing, free hands)?</summary>
        public bool DotLinkCanUse(NetPlayer p) =>
            p != null && p.IsHider && p.Life.Value == LifeState.Alive && !p.Busy.Value && p.Seat.Value < 0 && !p.BodyCarryBusy && !p.HoldsPaMic;

        /// <summary>Every frame on every peer (DotLinkDriver); the server part runs here.</summary>
        internal void UpdateDotLink()
        {
            if (!IsServer) return;
            var ph = Phase.Value;
            bool newRound = ph == MatchPhase.Loading && _dlLastPhase != MatchPhase.Loading;
            _dlLastPhase = ph;
            for (int g = 0; g < ExitGate.Count; g++)
            {
                var st = _dl[g];
                if (newRound || (DotLinkRoundPhase && st.Task == null && st.Puzzle == null)) DotLinkNewRound(g);
                if (st.Task != null && st.Task.IsCompleted) DotLinkPublishPuzzle(g);
                DotLinkCheckUser(g);
            }
        }

        void DotLinkCheckUser(int g)
        {
            if (DotLinkUserOf(g).Value == NoDotLinkUser) return;
            var st = _dl[g];
            var u = DotLinkUserPlayerOf(g);
            string why = u == null ? "user left" : !DotLinkRoundPhase ? "round over" : !DotLinkCanUse(u) ? "user down / dead / busy"
                : DotLinkHurt(u) && !st.UserHurt ? "user attacked" : null;
            bool solvedHold = st.SolvedAt >= 0 && Now < st.SolvedAt + DotLinkSolvedHold;
            if (why == null && Escape.Value < EscapeStage.DotLink) why = "stage changed";
            if (why == null && EscapeGateAtOf(g).Value > 0d && !solvedHold) why = st.SolvedAt >= 0 ? "solved" : "gate opened";
            if (why == null && DotLinkLayout.TryGet(g, out var lay) && Vector3.Distance(u.transform.position, lay.Stand) > 2.5f) why = "walked away";
            if (why != null) DotLinkRelease(g, why);
        }

        /// <summary>Slashed / limping / a leg off: a new hit while at the panel throws the user off it.</summary>
        static bool DotLinkHurt(NetPlayer p) => p.Wounded.Value || p.Limping.Value || p.Severed.Value != 0;

        void DotLinkNewRound(int g)
        {
            if (DotLinkUserOf(g).Value != NoDotLinkUser) DotLinkRelease(g, "new round");
            var st = _dl[g];
            st.Puzzle = null;
            st.Board = null;
            st.DragBase = null;
            st.SolvedAt = -1;
            DotLinkPuzzleOf(g).Value = default;
            DotLinkPathsOf(g).Value = default;
            int seed = Random.Range(1, int.MaxValue);
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-dotlinkseed");
            if (i >= 0 && i + 1 < args.Length && int.TryParse(args[i + 1], out int forced)) seed = forced + g * 7919; // a different pattern per gate
            st.Task = Task.Run(() =>
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var p = DotLinkGenerator.Generate(seed, DotLinkSize, DotLinkSize, DotLinkMinColors, DotLinkMaxColors, out var gs);
                return (p, gs, sw.Elapsed.TotalMilliseconds);
            });
        }

        void DotLinkPublishPuzzle(int g)
        {
            var st = _dl[g];
            var task = st.Task;
            st.Task = null;
            if (task.IsFaulted || task.Result.Item1 == null)
            {
                Debug.LogWarning($"[DotLink] gate {g}: generation failed: {task.Exception?.GetBaseException().Message ?? "no puzzle"}");
                return;
            }
            var (p, gs, ms) = task.Result;
            st.Puzzle = p;
            st.Board = new DotLinkBoard(p);
            DotLinkPuzzleOf(g).Value = new FixedString128Bytes(p.Encode());
            DotLinkPublish(g);
            Debug.Log($"[DotLink] gate {g} puzzle seed {p.Seed}: {p.W}x{p.H}, {p.Colors} colours, unique={gs.Unique}, {gs.Attempts} attempts, {gs.Nodes} solver nodes, {ms:0} ms");
        }

        void DotLinkPublish(int g)
        {
            var b = _dl[g].Board;
            b.Seq++;
            DotLinkPathsOf(g).Value = new FixedString512Bytes(b.Encode());
        }

        void DotLinkRelease(int g, string why)
        {
            var st = _dl[g];
            var u = DotLinkUserPlayerOf(g);
            Debug.Log($"[DotLink] gate {g} panel free ({why}){(u != null ? ", was " + u.Nick : "")}; pairs {(st.Board != null ? st.Board.CompleteCount : 0)}/{(st.Puzzle != null ? st.Puzzle.Colors : 0)}");
            DotLinkUserOf(g).Value = NoDotLinkUser;
            st.DragBase = null;
        }

        /// <summary>Server: E at a panel (<paramref name="use"/>, the nearer gate's) or Esc.</summary>
        public void ServerDotLinkUse(NetPlayer p, bool use)
        {
            if (!IsServer || p == null) return;
            if (!use)
            {
                int mine = DotLinkGateOf(p);
                if (mine >= 0) DotLinkRelease(mine, "left with Esc");
                return;
            }
            bool found = DotLinkLayout.TryNearest(p.transform.position, out var lay);
            int g = found ? lay.Gate : 0;
            string why = !found ? "no panel" : !DotLinkOpenAt(g) ? "panel not open" : !DotLinkCanUse(p) ? (p.IsSeeker ? "seeker" : "cannot use now")
                : DotLinkUserOf(g).Value != NoDotLinkUser ? "busy" : DotLinkGateOf(p) >= 0 ? "already at a panel"
                : Vector3.Distance(p.transform.position, lay.Stand) > 2.4f ? "too far" : null;
            if (why != null)
            {
                Debug.Log($"[DotLink] {p.Nick} refused at the panel of gate {g}: {why}");
                return;
            }
            var st = _dl[g];
            DotLinkUserOf(g).Value = p.NetworkObjectId;
            st.UserHurt = DotLinkHurt(p);
            st.DragBase = null;
            p.TeleportRpc(lay.Stand, lay.Yaw);
            Debug.Log($"[DotLink] {p.Nick} at the panel of gate {g}; pairs {st.Board.CompleteCount}/{st.Puzzle.Colors}, filled {st.Board.FilledCount}/{st.Puzzle.Cells}");
        }

        /// <summary>The user's gate when he may draw on it now (-1 = not).</summary>
        int DotLinkDrawGate(NetPlayer p)
        {
            if (!IsServer || p == null) return -1;
            int g = DotLinkGateOf(p);
            if (g < 0) return -1;
            var st = _dl[g];
            if (st.Board == null || st.Board.SolvedFlag || Escape.Value < EscapeStage.DotLink || EscapeGateAtOf(g).Value > 0d) return -1;
            return g;
        }

        /// <summary>Server: a line of the user's drag (<paramref name="final"/> = mouse released).</summary>
        public void ServerDotLinkPath(NetPlayer p, byte color, string cells, byte dragId, bool final)
        {
            int g = DotLinkDrawGate(p);
            if (g < 0) return;
            var st = _dl[g];
            if (color >= st.Puzzle.Colors) return;
            var list = DotLinkBoard.DecodeCells(cells ?? "");
            if (list == null) return;
            if (st.DragBase == null || dragId != st.DragId)
            {
                st.DragBase = st.Board.Clone();
                st.DragId = dragId;
            }
            var next = st.DragBase.Clone();
            if (!next.SetPath(color, list))
            {
                Debug.Log($"[DotLink] gate {g}: illegal line from {p.Nick}: colour {color} '{cells}'");
                return;
            }
            next.Seq = st.Board.Seq;
            st.Board.CopyFrom(next);
            if (final) st.DragBase = null;
            if (st.Board.Solved) DotLinkSolved(g, p);
            else DotLinkPublish(g);
        }

        /// <summary>Server: right mouse button on a colour = its line is wiped.</summary>
        public void ServerDotLinkClear(NetPlayer p, byte color)
        {
            int g = DotLinkDrawGate(p);
            if (g < 0) return;
            var st = _dl[g];
            if (color >= st.Puzzle.Colors || st.Board.Path(color).Count == 0) return;
            st.Board.Clear(color);
            st.DragBase = null;
            DotLinkPublish(g);
        }

        void DotLinkSolved(int g, NetPlayer p)
        {
            var st = _dl[g];
            st.Board.SolvedFlag = true;
            st.DragBase = null;
            st.SolvedAt = Now;
            DotLinkPublish(g);
            Debug.Log($"[DotLink] gate {g} SOLVED by {p.Nick} (seed {st.Puzzle.Seed}, {st.Puzzle.Colors} colours)");
            ServerEscapeGateStart(g); // MatchManager.EscapeGate.cs: only this gate opens
        }
    }
}
