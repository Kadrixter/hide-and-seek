using System.Collections;
using System.Collections.Generic;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>-testcheckers: plays this seat's moves of the scripted game through virtual clicks on the board.</summary>
    public sealed partial class MiniGameProbe
    {
        Ray SquareRay(ChessTable t, int seat, int sq) =>
            RayTo(t, seat, t.Model.BoardPivot.TransformPoint(ChessTableModel.SquareLocal(sq)));

        IEnumerator PlayCheckers(ChessTable t, int seat, CheckersScript.Result script)
        {
            var me = NetPlayer.Local;
            int colour = t.State.ColourOfSeat(seat);
            for (int i = 0; i < script.Moves.Count; i++)
            {
                int mover = i % 2 == 0 ? 1 : -1;
                if (mover != colour)
                {
                    for (float w = 0f; w < 30f && t.State.Ply <= i; w += Time.deltaTime) yield return null;
                    if (t.State.Ply <= i) { Verdict($"ply {i + 1}", false, "the opponent did not move"); yield break; }
                    continue;
                }
                for (float w = 0f; w < 30f && !(t.CkTarget.Side == colour && !t.Animating && t.State.Ply == i); w += Time.deltaTime) yield return null;
                if (t.State.Ply != i || t.Animating) { Verdict($"ply {i + 1}", false, $"out of step (ply {t.State.Ply})"); yield break; }
                var path = CheckersMove.ParsePath(script.Moves[i]);
                t.CkShown.FindLegal(path, out var move);
                yield return new WaitForSeconds(0.3f);
                if (i == script.MandatoryAt) yield return MandatoryCheck(t, seat, i);
                bool multi = move != null && move.Captured.Count >= 2;
                bool special = multi || i == script.PromoteAt || i == script.KingLongAt || i == script.KingCaptureAt;
                for (int k = 0; k < path.Count; k++)
                {
                    bool sent = t.LocalPointer(me, SquareRay(t, seat, path[k]), true);
                    yield return new WaitForSeconds(0.35f);
                    if (multi && k < path.Count - 1)
                    {
                        Verdict($"multi-capture step {k + 1} kept (path so far {t.CkPathLength})", t.CkPathLength == k + 1);
                        Shot($"{Prefix()}ck_multi_{i:00}_step{k}");
                    }
                    if (k == path.Count - 1 && !sent) Verdict($"ply {i + 1} {script.Moves[i]} sent", false);
                }
                for (float w = 0f; w < 5f && t.State.Ply == i; w += Time.deltaTime) yield return null;
                Log($"ply {i + 1}: {script.Moves[i]}{(move != null && move.IsCapture ? $" x{move.Captured.Count}" : "")}{(move != null && move.Promotes ? " =K" : "")} -> server ply {t.State.Ply}");
                if (special)
                {
                    Vector3 side = seat == 0 ? new Vector3(1.6f, 1.35f, -0.35f) : new Vector3(-1.6f, 1.35f, 0.35f);
                    yield return new WaitForSeconds(0.55f);
                    RenderTable(t, $"ck_{i:00}_{script.Moves[i]}_a", side, 48f);
                    Shot($"{Prefix()}ck_{i:00}_{script.Moves[i]}_move");
                    yield return new WaitForSeconds(0.6f);
                    RenderTable(t, $"ck_{i:00}_{script.Moves[i]}_b", side, 48f);
                }
                if (i == script.PromoteAt || i == script.KingLongAt || i == script.KingCaptureAt || multi)
                {
                    for (float w = 0f; w < 8f && t.Animating; w += Time.deltaTime) yield return null;
                    int to = path[path.Count - 1];
                    int p = t.CkShown.Sq[to];
                    if (i == script.PromoteAt) Verdict($"promotion to king ({script.Moves[i]})", p == mover * CheckersBoard.King, $"piece={p}");
                    if (i == script.KingLongAt) Verdict($"king's long move ({script.Moves[i]})", p == mover * CheckersBoard.King);
                    if (i == script.KingCaptureAt) Verdict($"king's flying capture ({script.Moves[i]})", p == mover * CheckersBoard.King && move != null && move.IsCapture);
                    if (multi) Verdict($"multi-capture x{move.Captured.Count} ({script.Moves[i]})", move.Captured.TrueForAll(c => t.CkShown.Sq[c] == 0) && p != 0);
                    yield return new WaitForSeconds(0.3f);
                    Shot($"{Prefix()}ck_{i:00}_{script.Moves[i]}_after");
                }
            }
        }

        /// <summary>Capturing is mandatory: clicking a disc that could only make a quiet move is refused, and so is a forced quiet-move RPC.</summary>
        IEnumerator MandatoryCheck(ChessTable t, int seat, int ply)
        {
            var me = NetPlayer.Local;
            var legal = t.CkShown.LegalMoves();
            var quiet = t.CkShown.QuietMoves(new List<CheckersMove>());
            CheckersMove wrong = null;
            foreach (var q in quiet)
            {
                bool capturer = false;
                foreach (var m in legal) if (m.From == q.From) { capturer = true; break; }
                if (!capturer) { wrong = q; break; }
            }
            if (wrong == null) { Verdict("mandatory capture check", false, "no quiet-only disc"); yield break; }
            float before = t.CkDenied;
            Shot($"{Prefix()}ck_mandatory_{ply:00}_glow");
            t.LocalPointer(me, SquareRay(t, seat, wrong.From), true);
            yield return new WaitForSeconds(0.3f);
            Verdict($"mandatory capture: click on {ChessBoard.SqName(wrong.From)} refused", t.CkSelected < 0 && t.CkDenied > before);
            Shot($"{Prefix()}ck_mandatory_{ply:00}_refused");
            me.CheckersMoveRpc(new Unity.Collections.FixedString64Bytes(wrong.ToString()));
            yield return new WaitForSeconds(1.2f);
            Verdict($"mandatory capture: server refuses {wrong}", t.State.Ply == ply, $"ply={t.State.Ply}");
            yield return new WaitForSeconds(1.2f);
        }
    }
}
