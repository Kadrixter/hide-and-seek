using System.Collections.Generic;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// The chess table on every peer: follows <see cref="MatchManager.ChessNet"/>, keeps the 3D pieces in sync
    /// (moves are animated by the mover's hand, see ChessTable.Anim; a jump in the state, a late join or a new game
    /// snaps the board), turns the board when the colours swap, runs the clock display, poses both players.
    /// The table also plays checkers (<see cref="ChessState.Kind"/>, ChessTable.Checkers*). The board stays empty while
    /// no game has started; the pieces of a new game pop up onto it and fly off when the table is free again
    /// (ChessTable.Setup). The pendant lamp above it (<see cref="ChessLamp"/>) burns while a game is on the board.
    /// </summary>
    public sealed partial class ChessTable : MiniTable
    {
        public override byte Game => MatchManager.GameChess;
        public override string Title => State.IsCheckers ? "warcaby" : State.Kind == ChessState.KindChess ? "szachy" : "szachy / warcaby";
        protected override float StoolZ => ChessTableModel.StoolZ;
        protected override float TopY => ChessTableModel.TopY;
        protected override float TableHalf => ChessTableModel.TableTop * 0.5f;
        public override float SurfaceY => ChessTableModel.BoardTop;
        public override ulong SeatPlayer(int seat) => State.Seat(seat);
        public override string SeatName(int seat) => State.ColourOfSeat(seat) > 0 ? "białe" : "czarne";
        public override bool Playing => State.Phase == ChessState.Playing;
        /// <summary>A game (running or just finished) stands on the board: the pieces are out and the lamp is on.</summary>
        public bool GameOnBoard => _hasState && State.Phase != ChessState.Waiting && State.Kind != ChessState.KindNone;
        /// <summary>Side to move in the game on the board (+1 white, -1 black).</summary>
        public int SideToMove => State.IsCheckers ? CkTarget.Side : Target.Side;

        public ChessTableModel Model { get; private set; }
        public ChessLamp Lamp { get; private set; }
        /// <summary>Latest state from the server.</summary>
        public readonly ChessState State = new();
        /// <summary>Position after all queued animations (what the server has).</summary>
        public readonly ChessBoard Target = new();
        /// <summary>Position the 3D pieces show right now (animations finish into it).</summary>
        public readonly ChessBoard Shown = new();

        readonly Transform[] _at = new Transform[64];
        readonly List<Transform> _captured = new();
        readonly int[] _capturedCount = new int[2];
        string _lastNet;
        bool _hasState;
        float _pivotYaw;
        int _clockSecA = -1, _clockSecB = -1;
        bool _clockRunA, _clockRun, _endSoundPending;
        /// <summary>Bumped on every state change (HUD and sounds watch it).</summary>
        public int Version { get; private set; }

        public static ChessTable Create(Transform parent, Vector3 pos, Quaternion rot, Material template)
        {
            var go = new GameObject("ChessTable") { layer = parent.gameObject.layer };
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot);
            var t = go.AddComponent<ChessTable>();
            t.Model = ChessTableModel.Build(go.transform, template, 0);
            t.InitMarks();
            t.InitCheckersMarks();
            t.Target.SetFen(ChessBoard.StartFen);
            t.Target.CopyTo(t.Shown);
            t.Lamp = ChessLamp.Create(t, template, LobbyLayout.H - pos.y);
            return t;
        }

        void Update()
        {
            var mm = MatchManager.Instance;
            if (mm != null)
            {
                string net = mm.ChessNet.Value.ToString();
                if (net != _lastNet)
                {
                    _lastNet = net;
                    var ns = new ChessState();
                    if (ChessState.TryDecode(net, ns)) OnState(ns);
                }
            }
            float dt = Time.deltaTime;
            TickSetup(dt);
            TickAnim(dt);
            TickCheckersAnim(dt);
            float wantYaw = State.WhiteIsA ? 0f : 180f;
            _pivotYaw = Mathf.MoveTowardsAngle(_pivotYaw, wantYaw, dt * 240f * AnimSpeed);
            Model.BoardPivot.localRotation = Quaternion.Euler(0f, _pivotYaw, 0f);
            UpdateClock(mm);
            DrivePoses();
            UpdateMarks();
        }

        void OnState(ChessState ns)
        {
            bool sameGame = _hasState && ns.Game == State.Game && ns.Kind == _shownKind;
            bool flipped = _hasState && ns.WhiteIsA != State.WhiteIsA;
            int prevPly = State.Ply;
            byte prevPhase = State.Phase;
            ns.CopyTo(State);
            _hasState = true;
            Version++;
            if (!GameOnBoard)
            {
                // Free table / nobody picked a game yet: the board is cleared.
                if (_shownKind != ChessState.KindNone) { CancelAnims(); CancelCheckersAnims(); ClearPieces(true); }
                _shownKind = ChessState.KindNone;
            }
            else if (ns.IsCheckers) CheckersOnState(sameGame, prevPly, flipped);
            else if (sameGame && ns.Ply == prevPly + 1 && ns.Last.Length >= 4 && TryQueue(ns.Last)) { }
            else if (!sameGame || ns.Fen != Target.ToFen() || flipped)
            {
                CancelAnims();
                CancelCheckersAnims();
                Snap(ns.Fen, !sameGame);
                if (flipped) _pivotYaw = ns.WhiteIsA ? 180f : 0f; // turn the board round in view
            }
            if (prevPhase != ns.Phase)
            {
                // The end chord waits until the last move has landed.
                if (Animating && ns.Phase == ChessState.Over) _endSoundPending = true;
                else MiniGameSounds.PhaseChange(this, prevPhase, ns.Phase, ns.End);
            }
        }

        /// <summary>Queues the move that led to the new state (parsed against the target position).</summary>
        bool TryQueue(string last)
        {
            int from = ChessBoard.ParseSq(last, 0), to = ChessBoard.ParseSq(last, 2);
            int promo = last.Length > 4 ? " pnbrqk".IndexOf(last[4]) : 0;
            if (from < 0 || to < 0 || !Target.FindLegal(from, to, promo, out var move)) return false;
            int mover = Target.Side;
            Target.Make(move);
            if (Target.ToFen() != State.Fen)
            {
                // Out of step (should not happen): show the server's board.
                CancelAnims();
                Snap(State.Fen, false);
                return true;
            }
            QueueMove(move, mover);
            return true;
        }

        // ================================================================ snapping

        /// <summary>
        /// Rebuilds all pieces for a position; captured pieces from the material that is missing. <paramref name="fresh"/>
        /// (a new game): the old pieces fly off and the new ones pop up one after another.
        /// </summary>
        void Snap(string fen, bool fresh)
        {
            ClearPieces(fresh);
            _shownKind = ChessState.KindChess;
            Target.SetFen(fen);
            Target.CopyTo(Shown);
            var count = new int[13];
            for (int sq = 0; sq < 64; sq++)
            {
                int p = Shown.Sq[sq];
                if (p == 0) continue;
                count[p + 6]++;
                _at[sq] = NewPiece(p, sq);
            }
            int[] full = { 0, 8, 2, 2, 2, 1, 1 };
            for (int colour = 1; colour >= -1; colour -= 2)
                for (int type = 1; type <= 5; type++)
                {
                    int missing = full[type] - count[type * colour + 6];
                    for (int k = 0; k < missing; k++) AddCaptured(type * colour, false);
                }
            if (fresh) AppearChess();
        }

        Transform NewPiece(int piece, int sq)
        {
            var g = Model.Piece(piece, Model.BoardPivot);
            g.transform.localPosition = ChessTableModel.SquareLocal(sq);
            g.transform.localRotation = Quaternion.Euler(0f, ChessTableModel.PieceYaw(piece, ChessBoard.File(sq) < 4), 0f);
            return g.transform;
        }

        /// <summary>Seat that captured a piece of this colour (the opponent's seat).</summary>
        int CapturerSeat(int piece) => State.ColourOfSeat(0) == (piece > 0 ? -1 : 1) ? 0 : 1;

        /// <summary>Puts a captured piece next to the board on the capturer's side; returns its transform.</summary>
        Transform AddCaptured(int piece, bool keepObject, Transform existing = null)
        {
            int seat = CapturerSeat(piece);
            int n = _capturedCount[seat]++;
            var t = existing != null ? existing : Model.Piece(piece, Model.Root).transform;
            t.SetParent(Model.Root, true);
            if (!keepObject)
            {
                t.localPosition = ChessTableModel.CapturedLocal(seat, n);
                t.localRotation = Quaternion.Euler(0f, seat == 0 ? 90f : -90f, 0f);
            }
            _captured.Add(t);
            return t;
        }

        // ================================================================ clock

        void UpdateClock(MatchManager mm)
        {
            if (Model.ClockTex == null) return;
            double now = mm != null ? mm.Now : 0d;
            int side = SideToMove;
            int msA = State.MsLeft(State.ColourOfSeat(0), side, now), msB = State.MsLeft(State.ColourOfSeat(1), side, now);
            bool running = State.Phase == ChessState.Playing;
            bool runA = running && State.ColourOfSeat(0) == side;
            int keyA = msA < 10000 ? msA / 100 : msA / 1000, keyB = msB < 10000 ? msB / 100 : msB / 1000;
            if (keyA == _clockSecA && keyB == _clockSecB && runA == _clockRunA && running == _clockRun) return;
            _clockSecA = keyA; _clockSecB = keyB; _clockRunA = runA; _clockRun = running;
            ChessClockFace.Draw(Model.ClockTex, msA, msB, runA, running, State.HasClock);
            // The player whose clock does NOT run has his plunger pressed down.
            Model.ButtonA.localPosition = new Vector3(Model.ButtonA.localPosition.x, running && !runA ? 0.0805f : 0.0855f, Model.ButtonA.localPosition.z);
            Model.ButtonB.localPosition = new Vector3(Model.ButtonB.localPosition.x, running && runA ? 0.0805f : 0.0855f, Model.ButtonB.localPosition.z);
        }
    }
}
