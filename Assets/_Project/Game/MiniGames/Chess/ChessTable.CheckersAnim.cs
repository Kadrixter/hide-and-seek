using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// Checkers move animation, the same on every peer: the mover's right hand reaches the disc and slides it along the
    /// board (one square, or the long run of a king); a capture hops over each jumped disc, square by square along the
    /// whole path; when the move is complete the jumped discs are lifted off to the capturer's towers one by one and a
    /// man that ended on the far row gets its second disc (king). Then the hand goes back to the table edge.
    /// </summary>
    public sealed partial class ChessTable
    {
        const float CkReach = 0.42f, CkGrip = 0.52f, CkSet = 0.1f, CkVictimGap = 0.16f, CkBack = 0.36f;

        sealed class CkAnim
        {
            public CheckersMove Move;
            public int Mover, Seat, Step;
            public Transform Piece, Crown;
            public readonly List<Transform> Victims = new();
            public readonly List<int> VictimPieces = new();
            public float[] StepEnd;
            public float T, Land, Dur;
            public bool Landed;
            public int VictimsGone;
            public bool King;
            public float ReturnAt => Land + CkSet + Victims.Count * CkVictimGap * 0.5f;
        }

        readonly List<CkAnim> _ckQueue = new();
        CkAnim _ckCur;

        bool CheckersAnimating => _ckCur != null || _ckQueue.Count > 0;

        void QueueCheckers(CheckersMove move, int mover) =>
            _ckQueue.Add(new CkAnim { Move = move, Mover = mover, Seat = State.ColourOfSeat(0) == mover ? 0 : 1 });

        void CancelCheckersAnims()
        {
            _ckQueue.Clear();
            _ckCur = null;
        }

        void BeginCheckers(CkAnim a)
        {
            var m = a.Move;
            a.Piece = _ck[m.From];
            int p = CkShown.Sq[m.From];
            a.King = p == CheckersBoard.King || p == -CheckersBoard.King;
            foreach (int c in m.Captured)
            {
                a.Victims.Add(_ck[c]);
                a.VictimPieces.Add(CkShown.Sq[c]);
            }
            a.StepEnd = new float[m.Path.Count - 1];
            float t = CkGrip;
            for (int i = 0; i < a.StepEnd.Length; i++)
            {
                int dist = Mathf.Abs(ChessBoard.File(m.Path[i + 1]) - ChessBoard.File(m.Path[i]));
                t += m.IsCapture ? 0.3f + 0.05f * dist : 0.24f + 0.07f * dist;
                a.StepEnd[i] = t;
            }
            a.Land = t;
            a.Dur = a.ReturnAt + CkBack + (a.Move.Promotes ? 0.15f : 0f);
            a.Dur = Mathf.Max(a.Dur, a.Land + CkSet + a.Victims.Count * CkVictimGap + 0.2f);
        }

        void TickCheckersAnim(float dt)
        {
            if (_ckCur == null)
            {
                if (_ckQueue.Count == 0) return;
                _ckCur = _ckQueue[0];
                _ckQueue.RemoveAt(0);
                BeginCheckers(_ckCur);
            }
            var a = _ckCur;
            a.T += dt * (_ckQueue.Count > 0 ? 2.5f : 1f) * AnimSpeed;
            if (a.Piece != null) a.Piece.localPosition = DiscAt(a, a.T);
            // A soft tick each time the disc lands on a square of the path.
            while (a.Step < a.StepEnd.Length && a.T >= a.StepEnd[a.Step])
            {
                a.Step++;
                if (a.Step < a.StepEnd.Length) MiniGameSounds.Knock(this, 0.45f, 1.15f);
            }
            if (!a.Landed && a.T >= a.Land)
            {
                a.Landed = true;
                MiniGameSounds.Knock(this, a.Move.IsCapture ? 1f : 0.8f, Random.Range(0.92f, 1.08f));
                if (a.Move.Promotes && a.Piece != null)
                {
                    a.Crown = AddCrown(a.Piece, a.Mover * CheckersBoard.Man);
                    a.Crown.localScale = Vector3.zero;
                }
            }
            // The jumped discs leave after the move is complete, one after another, onto the capturer's tower.
            while (a.VictimsGone < a.Victims.Count && a.T >= a.Land + CkSet + a.VictimsGone * CkVictimGap)
            {
                var v = a.Victims[a.VictimsGone];
                int piece = a.VictimPieces[a.VictimsGone];
                a.VictimsGone++;
                if (v == null) continue;
                AddDiscCaptured(piece, v);
                int seat = CapturerSeat(piece);
                _hops.Add((v, v.localPosition, CkCapturedLocal(seat, _ckCapturedCount[seat] - 1), 0f));
                MiniGameSounds.Select(this);
            }
            if (a.Crown != null)
            {
                float k = Mathf.Clamp01((a.T - a.Land - CkSet) / 0.3f);
                a.Crown.localScale = Vector3.one * Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, k * 1.6f));
                a.Crown.localPosition = Vector3.up * (CheckersPieceModels.Thickness + 0.035f * (1f - k) * (1f - k));
            }
            if (a.T >= a.Dur) FinishCheckers(a);
        }

        /// <summary>Disc position (board pivot space) at time <paramref name="t"/> of the move.</summary>
        Vector3 DiscAt(CkAnim a, float t)
        {
            var path = a.Move.Path;
            if (t < CkGrip) return Sq(path[0]);
            float start = CkGrip;
            for (int i = 0; i < a.StepEnd.Length; i++)
            {
                if (t < a.StepEnd[i])
                {
                    float k = Mathf.Clamp01((t - start) / (a.StepEnd[i] - start));
                    Vector3 from = Sq(path[i]), to = Sq(path[i + 1]);
                    float e = Mathf.SmoothStep(0f, 1f, k);
                    // Captures hop over the jumped disc; quiet moves slide, barely lifted off the board.
                    float lift = a.Move.IsCapture ? 0.045f * Mathf.Sin(k * Mathf.PI) + 0.004f : 0.003f * Mathf.Sin(k * Mathf.PI);
                    return Vector3.Lerp(from, to, e) + Vector3.up * lift;
                }
                start = a.StepEnd[i];
            }
            return Sq(path[path.Count - 1]);
        }

        void FinishCheckers(CkAnim a)
        {
            CkShown.Make(a.Move);
            _ckShownVersion++;
            _ck[a.Move.From] = null;
            foreach (int c in a.Move.Captured) _ck[c] = null;
            _ck[a.Move.To] = a.Piece;
            if (a.Piece != null) a.Piece.localPosition = Sq(a.Move.To);
            if (a.Crown != null)
            {
                a.Crown.localScale = Vector3.one;
                a.Crown.localPosition = Vector3.up * CheckersPieceModels.Thickness;
            }
            _ckCur = null;
            if (_ckQueue.Count == 0 && _endSoundPending)
            {
                _endSoundPending = false;
                MiniGameSounds.PhaseChange(this, ChessState.Playing, ChessState.Over, State.End);
            }
            else if (State.HasClock) MiniGameSounds.ClockButton(this);
        }

        /// <summary>Seated pose inputs during checkers (same hand rules as chess: reach, carry along the path, back).</summary>
        void CheckersPose(int seat, out Vector3 reach, out float reachW, out float think, out Vector3 look, out float lean)
        {
            reach = HandRest(seat);
            reachW = 0f;
            int colour = State.ColourOfSeat(seat);
            bool myTurn = State.Phase == ChessState.Playing && CkTarget.Side == colour && !Animating;
            think = myTurn ? 1f : State.Phase == ChessState.Waiting ? 0f : 0.15f;
            lean = myTurn ? 2f : 0f;
            look = Centre;
            var a = _ckCur;
            if (a != null && a.Piece != null && a.StepEnd != null)
            {
                float top = CheckersPieceModels.Height(a.King) + FistAbove;
                Vector3 grip = a.Piece.localPosition + Vector3.up * top;
                if (a.Seat == seat)
                {
                    Vector3 hover = Sq(a.Move.From) + Vector3.up * (top + 0.07f);
                    if (a.T < CkReach) { reach = PivotToWorld(hover); reachW = Mathf.SmoothStep(0f, 1f, a.T / CkReach); }
                    else if (a.T < CkGrip) { reach = PivotToWorld(Vector3.Lerp(hover, grip, (a.T - CkReach) / (CkGrip - CkReach))); reachW = 1f; }
                    else if (a.T < a.ReturnAt) { reach = PivotToWorld(grip + Vector3.up * (a.T > a.Land ? 0.03f * Mathf.Clamp01((a.T - a.Land) / CkSet) : 0f)); reachW = 1f; }
                    else
                    {
                        float k = Mathf.Clamp01((a.T - a.ReturnAt) / CkBack);
                        Vector3 last = Sq(a.Move.To) + Vector3.up * (top + 0.03f);
                        reach = PivotToWorld(Vector3.Lerp(last, Model.BoardPivot.InverseTransformPoint(HandRest(seat)), Mathf.SmoothStep(0f, 1f, k)));
                        reachW = 1f - Mathf.SmoothStep(0.3f, 1f, k);
                    }
                    think = 0f;
                    look = PivotToWorld(a.Piece.localPosition);
                }
                else
                {
                    look = PivotToWorld(a.Piece.localPosition);
                    float k = Mathf.Clamp01(a.T / 0.4f) * Mathf.Clamp01((a.Dur - a.T) / 0.4f);
                    lean = -9f * k;
                    think *= 1f - k;
                }
            }
            else if (CkSelected >= 0 && LocalSeat == seat) look = PivotToWorld(Sq(CkSelected));
        }
    }
}
