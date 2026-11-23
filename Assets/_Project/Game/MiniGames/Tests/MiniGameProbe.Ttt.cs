using System.Collections;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames
{
    public sealed partial class MiniGameProbe
    {
        /// <summary>Round 1 (X starts): X wins on the 2-4-6 diagonal. Round 2 (O starts): a full board, draw.</summary>
        static readonly int[] Round1 = { 4, 1, 0, 8, 6, 3, 2 };
        static readonly int[] Round2 = { 4, 0, 2, 6, 3, 5, 7, 1, 8 };

        IEnumerator TttRun(MatchManager mm)
        {
            var ttt = MiniGameTables.Ttt;
            var me = NetPlayer.Local;
            int seat = _host ? 0 : 1;
            if (!_host) yield return new WaitForSeconds(2.5f);
            yield return SitDown(ttt, seat);
            for (float w = 0f; w < 20f && ttt.State.Phase != TttState.Playing; w += Time.deltaTime) yield return null;
            Log($"round 1: phase={ttt.State.Phase} turn={ttt.State.Turn} I am {TttState.Mark(seat)}");
            yield return new WaitForSeconds(0.8f);
            Shot(Prefix() + "t01_seated");
            yield return Marks(ttt, seat, Round1, "r1");
            for (float w = 0f; w < 10f && (ttt.Animating || ttt.State.Phase != TttState.Over); w += Time.deltaTime) yield return null;
            yield return new WaitForSeconds(0.5f);
            Log($"round 1 over: end={ttt.State.End} winner={ttt.State.Winner} line={ttt.State.Line} score {ttt.State.ScoreA}:{ttt.State.ScoreB} => {(ttt.State.End == GameEnd.Line && ttt.State.Winner == 0 && ttt.State.Line == 7 ? "OK" : "FAIL")}");
            Shot(Prefix() + "t09_win");
            if (_host) RenderTable(ttt, "t09_win", new Vector3(1.5f, 0.15f, 2.6f));

            // One R (the client) starts round 2, O begins.
            if (!_host) { yield return new WaitForSeconds(1f); me.MiniRematchRpc(); }
            for (float w = 0f; w < 10f && !(ttt.State.Phase == TttState.Playing && ttt.State.Round >= 2); w += Time.deltaTime) yield return null;
            Log($"round 2: starter={TttState.Mark(ttt.State.Starter)} => {(ttt.State.Starter == 1 ? "OK" : "FAIL")}");
            yield return new WaitForSeconds(0.6f);
            if (_host) RenderTable(ttt, "t10_wipe", new Vector3(1.5f, 0.15f, 2.6f));
            yield return Marks(ttt, seat, Round2, "r2");
            for (float w = 0f; w < 12f && (ttt.Animating || ttt.State.Phase != TttState.Over); w += Time.deltaTime) yield return null;
            yield return new WaitForSeconds(0.5f);
            Log($"round 2 over: end={ttt.State.End} cells={ttt.State.Cells} score {ttt.State.ScoreA}:{ttt.State.ScoreB} draws={ttt.State.Draws} => {(ttt.State.End == GameEnd.Full && ttt.State.Draws == 1 ? "OK" : "FAIL")}");
            Shot(Prefix() + "t19_draw");
            if (_host) RenderTable(ttt, "t19_draw", new Vector3(1.5f, 0.15f, 2.6f));
            if (_host)
            {
                yield return new WaitForSeconds(1f);
                me.MiniLeaveRpc();
                yield return new WaitForSeconds(1.5f);
                Log($"host left: A={ttt.State.A} B={ttt.State.B} phase={ttt.State.Phase}");
            }
            else yield return new WaitForSeconds(3f);
        }

        IEnumerator Marks(TttTable ttt, int seat, int[] cells, string tag)
        {
            var me = NetPlayer.Local;
            int starter = ttt.State.Starter;
            for (int i = 0; i < cells.Length; i++)
            {
                int who = (starter + i) % 2;
                if (who != seat)
                {
                    for (float w = 0f; w < 25f && Count(ttt.State.Cells) <= i; w += Time.deltaTime) yield return null;
                    if (Count(ttt.State.Cells) <= i) { Log($"{tag} mark {i}: opponent did not play FAIL"); yield break; }
                    continue;
                }
                for (float w = 0f; w < 25f && !(ttt.State.Turn == who && !ttt.Animating && Count(ttt.State.Cells) == i); w += Time.deltaTime) yield return null;
                if (Count(ttt.State.Cells) != i) { Log($"{tag} mark {i}: out of step FAIL"); yield break; }
                yield return new WaitForSeconds(0.3f);
                Vector3 c = ttt.transform.TransformPoint(TttTableModel.CellLocal(cells[i]));
                ttt.LocalPointer(me, RayTo(ttt, seat, c), false);
                yield return new WaitForSeconds(0.25f);
                if (i < 2) Shot($"{Prefix()}{tag}_{i}_hover");
                ttt.LocalPointer(me, RayTo(ttt, seat, c), true);
                Log($"{tag} {TttState.Mark(seat)} -> {cells[i]}");
                if (_dir != null && i < 3)
                {
                    yield return new WaitForSeconds(0.6f);
                    Vector3 side = seat == 0 ? new Vector3(1.6f, 0.1f, 2.3f) : new Vector3(-1.6f, 0.1f, 2.3f);
                    RenderTable(ttt, $"{tag}_{i}_pick", side, 46f);
                    yield return new WaitForSeconds(0.7f);
                    RenderTable(ttt, $"{tag}_{i}_draw", side, 46f);
                    Shot($"{Prefix()}{tag}_{i}_draw");
                }
            }
        }

        static int Count(string cells)
        {
            int n = 0;
            foreach (char c in cells) if (c != '.') n++;
            return n;
        }
    }
}
