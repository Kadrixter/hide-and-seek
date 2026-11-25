using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// Checkers at the chess table (every peer): follows the checkers position in the table's state, keeps the wooden
    /// discs on the board pivot in sync (a move is animated by the mover's hand, see ChessTable.CheckersAnim; a jump in
    /// the state, a late join or a new game snaps the board), stacks the captured discs in little towers next to the
    /// board on the capturer's side.
    /// </summary>
    public sealed partial class ChessTable
    {
        /// <summary>Checkers position after all queued animations (what the server has).</summary>
        public readonly CheckersBoard CkTarget = new();
        /// <summary>Checkers position the discs show right now.</summary>
        public readonly CheckersBoard CkShown = new();

        readonly Transform[] _ck = new Transform[64];
        readonly List<Transform> _ckCaptured = new();
        readonly int[] _ckCapturedCount = new int[2];
        /// <summary>Bumped whenever <see cref="CkShown"/> changes (the legal-move cache of the input watches it).</summary>
        int _ckShownVersion;

        /// <summary>Spot of the n-th disc captured by the player of seat 0 / 1: towers of five on the -X side of the board.</summary>
        public static Vector3 CkCapturedLocal(int seat, int n)
        {
            int tower = n / 5, level = n % 5;
            float z = (0.09f + tower * 0.072f) * (seat == 0 ? -1f : 1f);
            return new Vector3(-0.49f, ChessTableModel.TopY + level * CheckersPieceModels.Thickness, z);
        }

        void CheckersOnState(bool sameGame, int prevPly, bool flipped)
        {
            if (sameGame && State.Ply == prevPly + 1 && State.Last.Length >= 4 && TryQueueCheckers(State.Last)) return;
            if (!sameGame || State.Fen != CkTarget.Encode() || flipped)
            {
                CancelAnims();
                CancelCheckersAnims();
                SnapCheckers(State.Fen, !sameGame);
                if (flipped) _pivotYaw = State.WhiteIsA ? 180f : 0f; // turn the board round in view
            }
        }

        /// <summary>Queues the move that led to the new state (found by its path in the target position).</summary>
        bool TryQueueCheckers(string last)
        {
            var path = CheckersMove.ParsePath(last);
            if (path == null || !CkTarget.FindLegal(path, out var move)) return false;
            int mover = CkTarget.Side;
            CkTarget.Make(move);
            if (CkTarget.Encode() != State.Fen)
            {
                CancelCheckersAnims();
                SnapCheckers(State.Fen, false);
                return true;
            }
            QueueCheckers(move, mover);
            return true;
        }

        /// <summary>Rebuilds all discs for a position (captured ones from what is missing); <paramref name="fresh"/> = new game, set up animated.</summary>
        void SnapCheckers(string position, bool fresh)
        {
            ClearPieces(fresh);
            _shownKind = ChessState.KindCheckers;
            if (!CkTarget.Decode(position)) CkTarget.Decode(CheckersBoard.Start);
            CkTarget.CopyTo(CkShown);
            _ckShownVersion++;
            for (int sq = 0; sq < 64; sq++)
                if (CkShown.Sq[sq] != 0) _ck[sq] = NewDisc(CkShown.Sq[sq], sq);
            for (int colour = 1; colour >= -1; colour -= 2)
            {
                int missing = 12 - CkShown.Count(colour);
                for (int k = 0; k < missing; k++) AddDiscCaptured(colour, null);
            }
            if (fresh) AppearCheckers();
        }

        Transform NewDisc(int piece, int sq)
        {
            var k = Model.Kit;
            // A random turn of the grain per disc, so the set does not look stamped.
            var g = k.Part("Disc", Model.BoardPivot, CheckersPieceModels.Disc, piece > 0 ? k.PieceLight : k.PieceDark,
                ChessTableModel.SquareLocal(sq), Quaternion.Euler(0f, (sq * 47) % 360, 0f));
            if (piece == CheckersBoard.King || piece == -CheckersBoard.King) AddCrown(g.transform, piece);
            return g.transform;
        }

        /// <summary>Puts the second disc (with the brass ring and star) on a disc: it is a king now.</summary>
        Transform AddCrown(Transform disc, int piece)
        {
            var k = Model.Kit;
            var top = k.Part("KingDisc", disc, CheckersPieceModels.Disc, piece > 0 ? k.PieceLight : k.PieceDark,
                Vector3.up * CheckersPieceModels.Thickness, Quaternion.Euler(0f, 61f, 0f)).transform;
            float face = CheckersPieceModels.Thickness;
            k.Part("CrownRing", top, CheckersPieceModels.CrownRing, k.Brass, Vector3.up * face, Quaternion.identity, false);
            k.Part("CrownStar", top, CheckersPieceModels.Star, k.Brass, Vector3.up * (face - 0.0003f), Quaternion.Euler(0f, 0f, 90f), false);
            return top;
        }

        /// <summary>Puts a captured disc on the capturer's tower (moved there now, or <paramref name="existing"/> kept where it is for a hop).</summary>
        Transform AddDiscCaptured(int piece, Transform existing)
        {
            int seat = CapturerSeat(piece);
            int n = _ckCapturedCount[seat]++;
            Transform t;
            if (existing != null)
            {
                t = existing;
                // A captured king goes off as a single disc.
                var crown = t.Find("KingDisc");
                if (crown != null) Destroy(crown.gameObject);
            }
            else t = NewDisc(piece > 0 ? CheckersBoard.Man : -CheckersBoard.Man, 0);
            t.SetParent(Model.Root, true);
            if (existing == null)
            {
                t.localPosition = CkCapturedLocal(seat, n);
                t.localRotation = Quaternion.Euler(0f, n * 37f, 0f);
            }
            _ckCaptured.Add(t);
            return t;
        }
    }
}
