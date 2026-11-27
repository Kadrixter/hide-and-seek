using System.Threading.Tasks;
using HideAndSeek.Core;
using HideAndSeek.Game.DotLink;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Dot link panel at the broken exit-gate lever (server side), escape stage DotLink -> GateOpening. A new 10 x 10
    /// puzzle with one solution is made off the main thread at every round start (<see cref="DotLinkPuzzleNet"/>). One
    /// hider at a time uses the panel (<see cref="DotLinkUser"/>); the drawn lines live here and in
    /// <see cref="DotLinkPathsNet"/>, so everybody near sees them live, late joiners get them, and they stay when the
    /// user leaves (Esc), goes down, dies or gets busy - the next hider carries on. A drag is sent as it goes (each
    /// message replaces the line on the board as it was when the drag began: crossed lines come back when the pointer
    /// goes back, as in Flow Free). Solved = <see cref="ServerEscapeAdvance"/>(GateOpening), the user is let go 1.8 s later.
    /// </summary>
    public partial class MatchManager
    {
        public const ulong NoDotLinkUser = ulong.MaxValue;
        public const int DotLinkSize = 10, DotLinkMinColors = 10, DotLinkMaxColors = 12;
        const float DotLinkSolvedHold = 1.8f;

        public readonly NetworkVariable<FixedString128Bytes> DotLinkPuzzleNet = new();
        public readonly NetworkVariable<FixedString512Bytes> DotLinkPathsNet = new();
        public readonly NetworkVariable<ulong> DotLinkUser = new(NoDotLinkUser);

        DotLinkPuzzle _dlPuzzle;
        DotLinkBoard _dlBoard, _dlDragBase;
        byte _dlDragId;
        Task<(DotLinkPuzzle, DotLinkGenerator.Stats, double)> _dlTask;
        MatchPhase _dlLastPhase = MatchPhase.Lobby;
        double _dlSolvedAt = -1;
        bool _dlUserHurt;

        /// <summary>Server copy of the puzzle with its solution (tests).</summary>
        public DotLinkPuzzle DotLinkServerPuzzle => _dlPuzzle;
        public NetPlayer DotLinkUserPlayer => DotLinkUser.Value != NoDotLinkUser ? NetPlayer.Find(DotLinkUser.Value) : null;

        /// <summary>A hide-and-seek round runs in the garage.</summary>
        public bool DotLinkRoundPhase
        {
            get
            {
                var ph = Phase.Value;
                return !RaceMode && (ph == MatchPhase.Hiding || ph == MatchPhase.Seeking) && SceneManager.GetActiveScene().name == NetSession.GameScene;
            }
        }

        /// <summary>The panel takes a player now (stage DotLink, puzzle ready).</summary>
        public bool DotLinkOpen => Escape.Value == EscapeStage.DotLink && DotLinkRoundPhase && DotLinkPuzzleNet.Value.Length > 0;

        /// <summary>May <paramref name="p"/> work on the panel (a living hider, standing, free hands)?</summary>
        public bool DotLinkCanUse(NetPlayer p) =>
            p != null && p.IsHider && p.Life.Value == LifeState.Alive && !p.Busy.Value && p.Seat.Value < 0 && !p.BodyCarryBusy && !p.HoldsPaMic;

        /// <summary>Every frame on every peer (DotLinkDriver); the server part runs here.</summary>
        internal void UpdateDotLink()
        {
            if (!IsServer) return;
            var ph = Phase.Value;
            if ((ph == MatchPhase.Loading && _dlLastPhase != MatchPhase.Loading) ||
                (DotLinkRoundPhase && _dlTask == null && _dlPuzzle == null)) DotLinkNewRound();
            _dlLastPhase = ph;
            if (_dlTask != null && _dlTask.IsCompleted) DotLinkPublishPuzzle();
            if (DotLinkUser.Value == NoDotLinkUser) return;
            var u = DotLinkUserPlayer;
            string why = u == null ? "user left" : !DotLinkRoundPhase ? "round over" : !DotLinkCanUse(u) ? "user down / dead / busy"
                : DotLinkHurt(u) && !_dlUserHurt ? "user attacked" : null;
            bool solvedHold = _dlSolvedAt >= 0 && Now < _dlSolvedAt + DotLinkSolvedHold;
            if (why == null && Escape.Value != EscapeStage.DotLink && !solvedHold) why = _dlSolvedAt >= 0 ? "solved" : "stage changed";
            if (why == null && DotLinkLayout.TryNearest(u.transform.position, out var lay) && Vector3.Distance(u.transform.position, lay.Stand) > 2.5f) why = "walked away";
            if (why != null) DotLinkRelease(why);
        }

        /// <summary>Slashed / limping / a leg off: a new hit while at the panel throws the user off it.</summary>
        static bool DotLinkHurt(NetPlayer p) => p.Wounded.Value || p.Limping.Value || p.Severed.Value != 0;

        void DotLinkNewRound()
        {
            if (DotLinkUser.Value != NoDotLinkUser) DotLinkRelease("new round");
            _dlPuzzle = null;
            _dlBoard = null;
            _dlDragBase = null;
            _dlSolvedAt = -1;
            DotLinkPuzzleNet.Value = default;
            DotLinkPathsNet.Value = default;
            int seed = Random.Range(1, int.MaxValue);
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-dotlinkseed");
            if (i >= 0 && i + 1 < args.Length && int.TryParse(args[i + 1], out int forced)) seed = forced;
            _dlTask = Task.Run(() =>
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var p = DotLinkGenerator.Generate(seed, DotLinkSize, DotLinkSize, DotLinkMinColors, DotLinkMaxColors, out var st);
                return (p, st, sw.Elapsed.TotalMilliseconds);
            });
        }

        void DotLinkPublishPuzzle()
        {
            var task = _dlTask;
            _dlTask = null;
            if (task.IsFaulted || task.Result.Item1 == null)
            {
                Debug.LogWarning($"[DotLink] generation failed: {task.Exception?.GetBaseException().Message ?? "no puzzle"}");
                return;
            }
            var (p, st, ms) = task.Result;
            _dlPuzzle = p;
            _dlBoard = new DotLinkBoard(p);
            DotLinkPuzzleNet.Value = new FixedString128Bytes(p.Encode());
            DotLinkPublish();
            Debug.Log($"[DotLink] puzzle seed {p.Seed}: {p.W}x{p.H}, {p.Colors} colours, unique={st.Unique}, {st.Attempts} attempts, {st.Nodes} solver nodes, {ms:0} ms");
        }

        void DotLinkPublish()
        {
            _dlBoard.Seq++;
            DotLinkPathsNet.Value = new FixedString512Bytes(_dlBoard.Encode());
        }

        void DotLinkRelease(string why)
        {
            var u = DotLinkUserPlayer;
            Debug.Log($"[DotLink] panel free ({why}){(u != null ? ", was " + u.Nick : "")}; pairs {(_dlBoard != null ? _dlBoard.CompleteCount : 0)}/{(_dlPuzzle != null ? _dlPuzzle.Colors : 0)}");
            DotLinkUser.Value = NoDotLinkUser;
            _dlDragBase = null;
        }

        /// <summary>Server: E at the panel (<paramref name="use"/>) or Esc.</summary>
        public void ServerDotLinkUse(NetPlayer p, bool use)
        {
            if (!IsServer || p == null) return;
            if (!use)
            {
                if (DotLinkUser.Value == p.NetworkObjectId) DotLinkRelease("left with Esc");
                return;
            }
            string why = !DotLinkOpen ? "panel not open" : !DotLinkCanUse(p) ? (p.IsSeeker ? "seeker" : "cannot use now")
                : DotLinkUser.Value != NoDotLinkUser ? "busy" : !DotLinkLayout.TryNearest(p.transform.position, out var lay) ? "no panel"
                : Vector3.Distance(p.transform.position, lay.Stand) > 2.4f ? "too far" : null;
            if (why != null)
            {
                Debug.Log($"[DotLink] {p.Nick} refused at the panel: {why}");
                return;
            }
            DotLinkLayout.TryNearest(p.transform.position, out lay);
            DotLinkUser.Value = p.NetworkObjectId;
            _dlUserHurt = DotLinkHurt(p);
            _dlDragBase = null;
            p.TeleportRpc(lay.Stand, lay.Yaw);
            Debug.Log($"[DotLink] {p.Nick} at the panel of gate {lay.Gate}; pairs {_dlBoard.CompleteCount}/{_dlPuzzle.Colors}, filled {_dlBoard.FilledCount}/{_dlPuzzle.Cells}");
        }

        /// <summary>Server: a line of the user's drag (<paramref name="final"/> = mouse released).</summary>
        public void ServerDotLinkPath(NetPlayer p, byte color, string cells, byte dragId, bool final)
        {
            if (!IsServer || p == null || DotLinkUser.Value != p.NetworkObjectId || _dlBoard == null || _dlBoard.SolvedFlag) return;
            if (Escape.Value != EscapeStage.DotLink || color >= _dlPuzzle.Colors) return;
            var list = DotLinkBoard.DecodeCells(cells ?? "");
            if (list == null) return;
            if (_dlDragBase == null || dragId != _dlDragId)
            {
                _dlDragBase = _dlBoard.Clone();
                _dlDragId = dragId;
            }
            var next = _dlDragBase.Clone();
            if (!next.SetPath(color, list))
            {
                Debug.Log($"[DotLink] illegal line from {p.Nick}: colour {color} '{cells}'");
                return;
            }
            next.Seq = _dlBoard.Seq;
            _dlBoard.CopyFrom(next);
            if (final) _dlDragBase = null;
            if (_dlBoard.Solved) DotLinkSolved(p);
            else DotLinkPublish();
        }

        /// <summary>Server: right mouse button on a colour = its line is wiped.</summary>
        public void ServerDotLinkClear(NetPlayer p, byte color)
        {
            if (!IsServer || p == null || DotLinkUser.Value != p.NetworkObjectId || _dlBoard == null || _dlBoard.SolvedFlag) return;
            if (color >= _dlPuzzle.Colors || _dlBoard.Path(color).Count == 0) return;
            _dlBoard.Clear(color);
            _dlDragBase = null;
            DotLinkPublish();
        }

        void DotLinkSolved(NetPlayer p)
        {
            _dlBoard.SolvedFlag = true;
            _dlDragBase = null;
            _dlSolvedAt = Now;
            DotLinkPublish();
            Debug.Log($"[DotLink] SOLVED by {p.Nick} (seed {_dlPuzzle.Seed}, {_dlPuzzle.Colors} colours)");
            ServerEscapeAdvance(EscapeStage.GateOpening);
        }
    }
}
