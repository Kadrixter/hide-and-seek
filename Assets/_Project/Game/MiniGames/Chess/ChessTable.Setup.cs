using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// Setting up and clearing the board: while the table is free the board is empty; when a game starts its pieces
    /// pop up onto their squares one after another (back rank first, a short drop with a small overshoot, soft knocks),
    /// and when the players leave they shrink away. Works for both games (chess pieces and checkers discs).
    /// </summary>
    public sealed partial class ChessTable
    {
        const float AppearTime = 0.26f, VanishTime = 0.3f;

        sealed class SetupFx
        {
            public Transform T;
            public Vector3 Rest, Scale;
            public float Delay, Time;
            public bool Appear, Sound;
        }

        readonly List<SetupFx> _setup = new();
        /// <summary>Game whose pieces are on the board now (<see cref="ChessState.KindNone"/> = empty board).</summary>
        byte _shownKind;
        int _appearing;

        /// <summary>Chess pieces and checkers discs (kings count once) at the table now, incl. ones still shrinking away (tests).</summary>
        public int PieceObjects
        {
            get
            {
                int n = 0;
                foreach (var r in GetComponentsInChildren<MeshRenderer>())
                    if (r.name == "Piece" || r.name == "Disc") n++;
                return n;
            }
        }

        /// <summary>Pieces of a new game are still popping up (input waits for it).</summary>
        bool Appearing => _appearing > 0;

        /// <summary>Takes every piece and disc off the board (animated: they shrink away, else destroyed at once).</summary>
        void ClearPieces(bool animated)
        {
            int n = 0;
            void Take(Transform t)
            {
                if (t == null) return;
                if (!animated) { Destroy(t.gameObject); return; }
                for (int i = _setup.Count - 1; i >= 0; i--) if (_setup[i].T == t) { if (_setup[i].Appear) _appearing--; _setup.RemoveAt(i); }
                _setup.Add(new SetupFx { T = t, Rest = t.localPosition, Scale = t.localScale == Vector3.zero ? Vector3.one : t.localScale, Delay = (n++ % 16) * 0.018f });
            }
            for (int i = 0; i < 64; i++) { Take(_at[i]); _at[i] = null; Take(_ck[i]); _ck[i] = null; }
            foreach (var c in _captured) Take(c);
            foreach (var c in _ckCaptured) Take(c);
            _captured.Clear();
            _ckCaptured.Clear();
            _capturedCount[0] = _capturedCount[1] = 0;
            _ckCapturedCount[0] = _ckCapturedCount[1] = 0;
            if (!animated)
            {
                foreach (var f in _setup) if (f.T != null && !f.Appear) Destroy(f.T.gameObject);
                _setup.Clear();
                _appearing = 0;
            }
        }

        /// <summary>Lets a piece pop up onto its spot after <paramref name="delay"/> seconds.</summary>
        void AppearPiece(Transform t, float delay, bool sound)
        {
            if (t == null) return;
            _setup.Add(new SetupFx { T = t, Rest = t.localPosition, Scale = t.localScale, Delay = delay, Appear = true, Sound = sound });
            t.localScale = Vector3.zero;
            _appearing++;
        }

        /// <summary>Chess set-up order: for each side the back rank from the a-file, then the pawns.</summary>
        void AppearChess()
        {
            for (int sq = 0; sq < 64; sq++)
            {
                if (_at[sq] == null) continue;
                int p = Shown.Sq[sq];
                int fromOwn = p > 0 ? ChessBoard.Rank(sq) : 7 - ChessBoard.Rank(sq);
                float delay = 0.25f + fromOwn * 0.16f + ChessBoard.File(sq) * 0.045f;
                AppearPiece(_at[sq], delay, ChessBoard.File(sq) % 3 == 0);
            }
            float d = 0.3f;
            foreach (var c in _captured) AppearPiece(c, d += 0.05f, false);
        }

        /// <summary>Checkers set-up: both sides' rows from the middle outwards at once, left to right.</summary>
        void AppearCheckers()
        {
            for (int sq = 0; sq < 64; sq++)
            {
                if (_ck[sq] == null) continue;
                int p = CkShown.Sq[sq];
                int fromOwn = p > 0 ? ChessBoard.Rank(sq) : 7 - ChessBoard.Rank(sq);
                float delay = 0.25f + fromOwn * 0.2f + ChessBoard.File(sq) * 0.05f;
                AppearPiece(_ck[sq], delay, ChessBoard.File(sq) < 2);
            }
            float d = 0.3f;
            foreach (var c in _ckCaptured) AppearPiece(c, d += 0.05f, false);
        }

        void TickSetup(float dt)
        {
            for (int i = _setup.Count - 1; i >= 0; i--)
            {
                var f = _setup[i];
                if (f.T == null) { if (f.Appear) _appearing--; _setup.RemoveAt(i); continue; }
                f.Time += dt;
                float k = (f.Time - f.Delay) / (f.Appear ? AppearTime : VanishTime);
                if (k < 0f) continue;
                if (f.Appear)
                {
                    k = Mathf.Clamp01(k);
                    // Drop from 6 cm with a little overshoot of the scale (back-out ease).
                    float s = 1f + 2.2f * Mathf.Pow(k - 1f, 3f) + 1.2f * Mathf.Pow(k - 1f, 2f);
                    f.T.localScale = f.Scale * Mathf.Max(0f, s);
                    f.T.localPosition = f.Rest + Vector3.up * (0.06f * (1f - k) * (1f - k));
                    if (k >= 1f)
                    {
                        f.T.localScale = f.Scale;
                        f.T.localPosition = f.Rest;
                        if (f.Sound) MiniGameSounds.Knock(this, 0.35f, Random.Range(1.05f, 1.2f));
                        _appearing--;
                        _setup.RemoveAt(i);
                    }
                }
                else
                {
                    float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k));
                    f.T.localScale = f.Scale * (1f - e);
                    f.T.localPosition = f.Rest + Vector3.up * (0.04f * e);
                    if (k >= 1f) { Destroy(f.T.gameObject); _setup.RemoveAt(i); }
                }
            }
            if (_appearing < 0) _appearing = 0;
        }
    }
}
