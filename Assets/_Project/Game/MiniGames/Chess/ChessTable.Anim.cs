using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// Move animation, the same on every peer: the mover's right hand reaches the piece, lifts it in an arc to the
    /// target square and sets it down (knock), a captured piece hops off to the side of the table, the rook follows
    /// in castling, a promoted pawn turns into the new piece; then the hand goes back to the table edge.
    /// Several queued moves (fast play, catching up) run faster.
    /// </summary>
    public sealed partial class ChessTable
    {
        // Timeline of one carry (seconds at speed 1).
        const float TReach = 0.5f, TGrip = 0.62f, TLand = 1.18f, TSet = 1.26f, TBack = 1.62f;
        // Castling: after the king the hand moves over to the rook (0.25 s) and carries it (0.5 s).
        const float TRook0 = TSet + 0.05f, TRookGrip = TRook0 + 0.25f, TRook1 = TRookGrip + 0.5f;
        const float FistAbove = 0.05f;

        sealed class MoveAnim
        {
            public ChessMove Move;
            public int Mover, Seat;
            public Transform Piece, Victim, Rook, Promoted;
            public int RookFrom, RookTo, VictimPiece;
            public float T;
            public bool Landed, RookLanded, VictimGone;
            public bool Castle => (Move.Flags & ChessMove.Castle) != 0;
            /// <summary>When the hand starts going back to the table edge.</summary>
            public float ReturnAt => Castle ? TRook1 : TSet;
            public float Dur => ReturnAt + (TBack - TSet);
        }

        readonly List<MoveAnim> _queue = new();
        readonly List<(Transform t, Vector3 from, Vector3 to, float time)> _hops = new();
        MoveAnim _cur;

        /// <summary>True while a move is being shown (the HUD waits with "Szach!" until the piece lands).</summary>
        public bool Animating => _cur != null || _queue.Count > 0;

        void QueueMove(ChessMove move, int mover)
        {
            _queue.Add(new MoveAnim { Move = move, Mover = mover, Seat = State.ColourOfSeat(0) == mover ? 0 : 1 });
        }

        void CancelAnims()
        {
            _queue.Clear();
            _cur = null;
            foreach (var h in _hops) if (h.t != null) h.t.localPosition = h.to;
            _hops.Clear();
        }

        Vector3 Sq(int sq) => ChessTableModel.SquareLocal(sq);
        Vector3 PivotToWorld(Vector3 local) => Model.BoardPivot.TransformPoint(local);

        void TickAnim(float dt)
        {
            for (int i = _hops.Count - 1; i >= 0; i--)
            {
                var h = _hops[i];
                h.time += dt / 0.45f;
                if (h.t == null) { _hops.RemoveAt(i); continue; }
                float k = Mathf.Clamp01(h.time);
                h.t.localPosition = Vector3.Lerp(h.from, h.to, Mathf.SmoothStep(0f, 1f, k)) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.09f;
                if (k >= 1f) { _hops.RemoveAt(i); MiniGameSounds.Knock(this, 0.5f, 0.85f); }
                else _hops[i] = h;
            }
            if (_cur == null)
            {
                if (_queue.Count == 0) return;
                _cur = _queue[0];
                _queue.RemoveAt(0);
                Begin(_cur);
            }
            float speed = _queue.Count > 0 ? 2.5f : 1f;
            var a = _cur;
            a.T += dt * speed;
            Vector3 from = Sq(a.Move.From), to = Sq(a.Move.To);
            if (a.Piece != null)
            {
                if (a.T < TGrip) a.Piece.localPosition = from;
                else if (a.T < TLand)
                {
                    float k = Mathf.SmoothStep(0f, 1f, (a.T - TGrip) / (TLand - TGrip));
                    float lift = Mathf.Sin(Mathf.Clamp01((a.T - TGrip) / (TLand - TGrip)) * Mathf.PI);
                    a.Piece.localPosition = Vector3.Lerp(from, to, k) + Vector3.up * (0.07f * lift + 0.012f);
                }
                else a.Piece.localPosition = to + Vector3.up * 0.012f * Mathf.Clamp01((TSet - a.T) / (TSet - TLand));
            }
            // The captured piece makes room just before the landing: it hops off to the capturer's side of the table.
            if (a.Victim != null && !a.VictimGone && a.T >= TLand - 0.18f)
            {
                a.VictimGone = true;
                var t = a.Victim;
                AddCaptured(a.VictimPiece, true, t);
                int seat = CapturerSeat(a.VictimPiece);
                Vector3 end = ChessTableModel.CapturedLocal(seat, _capturedCount[seat] - 1);
                t.localRotation = Quaternion.Euler(0f, seat == 0 ? 90f : -90f, 0f);
                _hops.Add((t, t.localPosition, end, 0f));
            }
            if (!a.Landed && a.T >= TSet)
            {
                a.Landed = true;
                MiniGameSounds.Knock(this, a.Victim != null ? 1f : 0.8f, Random.Range(0.92f, 1.08f));
                if (a.Move.Promo != 0) Promote(a);
                int ff = ChessBoard.File(a.Move.From), tf = ChessBoard.File(a.Move.To);
                if (a.Piece != null) a.Piece.localRotation = Quaternion.Euler(0f, ChessTableModel.PieceYaw(Shown.Sq[a.Move.From], tf > ff || (tf == ff && tf < 4)), 0f);
            }
            if (a.Promoted != null)
            {
                float k = Mathf.Clamp01((a.T - TSet) / 0.3f);
                if (a.Piece != null) a.Piece.localScale = Vector3.one * (1f - k);
                a.Promoted.localScale = Vector3.one * Mathf.SmoothStep(0f, 1f, k);
            }
            if (a.Rook != null)
            {
                Vector3 rf = Sq(a.RookFrom), rt = Sq(a.RookTo);
                float k = Mathf.Clamp01((a.T - TRookGrip) / (TRook1 - TRookGrip - 0.06f));
                a.Rook.localPosition = Vector3.Lerp(rf, rt, Mathf.SmoothStep(0f, 1f, k)) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.06f;
                if (!a.RookLanded && k >= 1f) { a.RookLanded = true; MiniGameSounds.Knock(this, 0.7f, 1.05f); }
            }
            if (a.T >= a.Dur) Finish(a);
        }

        void Begin(MoveAnim a)
        {
            a.Piece = _at[a.Move.From];
            int victimSq = (a.Move.Flags & ChessMove.EnPassant) != 0 ? a.Move.To - 8 * a.Mover : a.Move.To;
            a.Victim = a.Move.IsCapture ? _at[victimSq] : null;
            a.VictimPiece = Shown.Sq[victimSq];
            if (a.Victim != null) _at[victimSq] = null;
            if ((a.Move.Flags & ChessMove.Castle) != 0)
            {
                ChessBoard.CastleRook(a.Move, out a.RookFrom, out a.RookTo);
                a.Rook = _at[a.RookFrom];
            }
        }

        void Promote(MoveAnim a)
        {
            int piece = a.Move.Promo * a.Mover;
            a.Promoted = NewPiece(piece, a.Move.To);
            a.Promoted.localScale = Vector3.zero;
        }

        void Finish(MoveAnim a)
        {
            Shown.Make(a.Move);
            _at[a.Move.From] = null;
            if (a.Promoted != null)
            {
                if (a.Piece != null) Destroy(a.Piece.gameObject);
                a.Promoted.localScale = Vector3.one;
                _at[a.Move.To] = a.Promoted;
            }
            else _at[a.Move.To] = a.Piece;
            if (a.Piece != null && a.Promoted == null) a.Piece.localPosition = Sq(a.Move.To);
            if (a.Rook != null)
            {
                _at[a.RookFrom] = null;
                _at[a.RookTo] = a.Rook;
                a.Rook.localPosition = Sq(a.RookTo);
            }
            _cur = null;
            if (_queue.Count == 0 && _endSoundPending)
            {
                _endSoundPending = false;
                MiniGameSounds.PhaseChange(this, ChessState.Playing, ChessState.Over, State.End);
            }
            else if (Shown.InCheck(Shown.Side)) MiniGameSounds.Check(this);
            else if (State.HasClock) MiniGameSounds.ClockButton(this);
        }

        // ================================================================ poses

        protected override void PoseInputs(int seat, out Vector3 reach, out float reachW, out float think, out Vector3 look, out float lean)
        {
            reach = HandRest(seat);
            reachW = 0f;
            int colour = State.ColourOfSeat(seat);
            bool myTurn = State.Phase == ChessState.Playing && Target.Side == colour && !Animating;
            think = myTurn ? 1f : 0.15f;
            lean = myTurn ? 2f : 0f;
            look = Centre;
            var a = _cur;
            if (a != null && a.Piece != null)
            {
                float top = ChessPieceModels.Height[Mathf.Abs(Shown.Sq[a.Move.From])];
                Vector3 grip = a.Piece.localPosition + Vector3.up * (top + FistAbove);
                if (a.Seat == seat)
                {
                    Vector3 hover = Sq(a.Move.From) + Vector3.up * (top + FistAbove + 0.07f);
                    if (a.T < TReach) { reach = PivotToWorld(hover); reachW = Mathf.SmoothStep(0f, 1f, a.T / TReach); }
                    else if (a.T < TGrip) { reach = PivotToWorld(Vector3.Lerp(hover, grip, (a.T - TReach) / (TGrip - TReach))); reachW = 1f; }
                    else if (a.T < TSet) { reach = PivotToWorld(grip); reachW = 1f; }
                    else if (a.Castle && a.T < TRook1)
                    {
                        float rt = ChessPieceModels.Height[ChessBoard.Rook];
                        Vector3 rookGrip = (a.Rook != null ? a.Rook.localPosition : Sq(a.RookTo)) + Vector3.up * (rt + FistAbove);
                        Vector3 kingTop = Sq(a.Move.To) + Vector3.up * (top + FistAbove + 0.03f);
                        float k = Mathf.SmoothStep(0f, 1f, (a.T - TSet) / (TRookGrip - TSet));
                        reach = PivotToWorld(Vector3.Lerp(kingTop, rookGrip + Vector3.up * 0.03f * (1f - k), k));
                        reachW = 1f;
                    }
                    else
                    {
                        float k = Mathf.Clamp01((a.T - a.ReturnAt) / (TBack - TSet));
                        Vector3 last = a.Castle ? Sq(a.RookTo) + Vector3.up * (ChessPieceModels.Height[ChessBoard.Rook] + FistAbove + 0.04f)
                                                : Sq(a.Move.To) + Vector3.up * (top + FistAbove + 0.05f);
                        reach = PivotToWorld(Vector3.Lerp(last, Model.BoardPivot.InverseTransformPoint(HandRest(seat)), Mathf.SmoothStep(0f, 1f, k)));
                        reachW = 1f - Mathf.SmoothStep(0.3f, 1f, k);
                    }
                    think = 0f;
                    look = PivotToWorld(a.Piece.localPosition);
                }
                else
                {
                    // The opponent sits up and follows the moving piece (makes room for the reaching player's head).
                    look = PivotToWorld(a.Piece.localPosition);
                    float k = Mathf.Clamp01(a.T / 0.4f) * Mathf.Clamp01((a.Dur - a.T) / 0.4f);
                    lean = -9f * k;
                    think *= 1f - k;
                }
            }
            else if (Selected >= 0 && LocalSeat == seat) look = PivotToWorld(Sq(Selected));
        }
    }
}
