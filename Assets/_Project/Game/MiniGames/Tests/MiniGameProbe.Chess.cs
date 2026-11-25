using System.Collections;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames
{
    public sealed partial class MiniGameProbe
    {
        static readonly string[] Scholar = { "e2e4", "e7e5", "d1h5", "b8c6", "f1c4", "g8f6", "h5f7" };
        /// <summary>En passant (3.), castling both sides (6.), promotion to a knight with capture (8.), queen takes it.</summary>
        static readonly string[] Special =
        {
            "e2e4", "h7h6", "e4e5", "d7d5", "e5d6", "g7g6", "g1f3", "f8g7", "f1e2", "g8f6", "e1g1", "e8g8", "d6d7", "a7a5", "d7c8n", "d8c8",
        };

        IEnumerator ChessRun(MatchManager mm)
        {
            var chess = MiniGameTables.Chess;
            var me = NetPlayer.Local;
            int seat = _host ? 0 : 1;
            if (!_host) yield return new WaitForSeconds(2.5f); // host first
            RenderTable(chess, "00_empty", new Vector3(1.5f, 1.5f, -1.3f));
            yield return SitDown(chess, seat);
            if (_host) me.BoardKindRpc(ChessState.KindChess); // the table asks which game first
            for (float w = 0f; w < 20f && chess.State.Phase != ChessState.Playing; w += Time.deltaTime) yield return null;
            Log($"game 1 phase={chess.State.Phase} colour={(chess.State.ColourOfSeat(seat) > 0 ? "white" : "black")} clock={ChessState.ClockName(chess.State.ClockMin, chess.State.ClockInc)}");
            yield return new WaitForSeconds(1f);
            Shot(Prefix() + "01_seated");
            if (_host) RenderTable(chess, "01_both_seated", new Vector3(1.5f, 1.45f, -1.25f));
            yield return Play(chess, seat, Scholar, "g1");
            for (float w = 0f; w < 8f && (chess.Animating || chess.State.Phase != ChessState.Over); w += Time.deltaTime) yield return null;
            yield return new WaitForSeconds(0.6f);
            Log($"game 1 over: end={chess.State.End} winner={chess.State.Winner} text='{ChessReferee.Describe(chess.State.End, chess.State.Winner, chess.SeatNick(0), chess.SeatNick(1))}' => {(chess.State.End == GameEnd.Checkmate && chess.State.Winner == 1 ? "OK" : "FAIL")}");
            Shot(Prefix() + "09_mate");
            yield return new WaitForSeconds(2.5f);
            _recording = false; // -mgvideo: the first game only
            if (_host) RenderTable(chess, "09_mate", new Vector3(1.2f, 1.35f, 0.9f));

            // Rematch: both press R, colours swap (seat B gets white).
            yield return new WaitForSeconds(_host ? 0.5f : 1.5f);
            me.MiniRematchRpc();
            for (float w = 0f; w < 10f && chess.State.Phase != ChessState.Playing; w += Time.deltaTime) yield return null;
            yield return new WaitForSeconds(1.2f);
            Log($"game 2 phase={chess.State.Phase} whiteIsA={chess.State.WhiteIsA} my colour={(chess.State.ColourOfSeat(seat) > 0 ? "white" : "black")} => {(chess.State.Phase == ChessState.Playing && !chess.State.WhiteIsA ? "OK" : "FAIL")}");
            Shot(Prefix() + "10_game2");
            yield return Play(chess, seat, Special, "g2");
            for (float w = 0f; w < 10f && (chess.Animating || chess.State.Ply < Special.Length); w += Time.deltaTime) yield return null;
            var b = chess.Shown;
            bool ep = b.Sq[ChessBoard.ParseSq("d5")] == 0;
            bool castled = b.Sq[ChessBoard.ParseSq("g1")] == ChessBoard.King && b.Sq[ChessBoard.ParseSq("f1")] == ChessBoard.Rook &&
                           b.Sq[ChessBoard.ParseSq("g8")] == -ChessBoard.King && b.Sq[ChessBoard.ParseSq("f8")] == -ChessBoard.Rook;
            bool promo = b.Sq[ChessBoard.ParseSq("c8")] == -ChessBoard.Queen;
            Log($"game 2 board {b.ToFen()}: en passant {(ep ? "OK" : "FAIL")}, castling {(castled ? "OK" : "FAIL")}, promotion+recapture {(promo ? "OK" : "FAIL")}");
            if (_host) RenderTable(chess, "19_game2", new Vector3(1.3f, 1.4f, -1.1f));
            Shot(Prefix() + "19_game2");

            // White (seat B = the client) resigns: the real Esc action (MiniLeaveRpc after the question).
            if (!_host)
            {
                yield return new WaitForSeconds(1f);
                yield return PressEscape();
                bool asked = HideAndSeek.Game.UI.ConfirmDialog.IsOpen;
                Log($"Esc opens the resign question: {(asked ? "OK" : "no (window without focus?) - leaving through the RPC")}");
                Shot(Prefix() + "19b_esc_question");
                yield return new WaitForSeconds(0.5f);
                if (!asked || !ClickYes()) me.MiniLeaveRpc();
            }
            for (float w = 0f; w < 6f && chess.State.Phase != ChessState.Over; w += Time.deltaTime) yield return null;
            yield return new WaitForSeconds(1f);
            Log($"resign: end={chess.State.End} winner={chess.State.Winner} seatB={chess.State.B} => {(chess.State.End == GameEnd.Resign && chess.State.Winner == -1 ? "OK" : "FAIL")}");
            Shot(Prefix() + "20_resigned");
            if (_host)
            {
                yield return new WaitForSeconds(1.5f);
                me.MiniLeaveRpc();
                yield return new WaitForSeconds(1.5f);
                Log($"host left: seats A={chess.State.A} B={chess.State.B} phase={chess.State.Phase}");
                Shot("21_stood_up");
            }
            else yield return new WaitForSeconds(3f);
        }

        string Prefix() => _prefixOverride ?? (_host ? "a_" : "b_");

        /// <summary>A real Esc key press through the Input System (as if typed).</summary>
        static IEnumerator PressEscape()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) yield break;
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb, new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.Escape));
            yield return null;
            yield return null;
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb, new UnityEngine.InputSystem.LowLevel.KeyboardState());
            yield return null;
            yield return null;
        }

        /// <summary>Clicks "yes" in the open confirm dialog.</summary>
        static bool ClickYes()
        {
            var d = FindFirstObjectByType<HideAndSeek.Game.UI.ConfirmDialog>();
            if (d == null) return false;
            foreach (var b in d.GetComponentsInChildren<UnityEngine.UI.Button>())
                if (b.name == "Yes") { b.onClick.Invoke(); return true; }
            return false;
        }

        /// <summary>Plays this seat's moves of the script through virtual clicks when it is our turn.</summary>
        IEnumerator Play(ChessTable chess, int seat, string[] script, string tag)
        {
            var me = NetPlayer.Local;
            for (int i = 0; i < script.Length; i++)
            {
                int colour = i % 2 == 0 ? 1 : -1;
                if (chess.State.ColourOfSeat(seat) != colour)
                {
                    // The opponent's move: just wait until it has been played.
                    for (float w = 0f; w < 25f && chess.State.Ply <= i; w += Time.deltaTime) yield return null;
                    if (chess.State.Ply <= i) { Log($"{tag} move {i + 1} {script[i]}: opponent did not move FAIL"); yield break; }
                    continue;
                }
                for (float w = 0f; w < 25f && !(chess.Target.Side == colour && !chess.Animating && chess.State.Ply == i); w += Time.deltaTime) yield return null;
                if (chess.State.Ply != i) { Log($"{tag} move {i + 1} {script[i]}: out of step (ply {chess.State.Ply}) FAIL"); yield break; }
                string mv = script[i];
                int from = ChessBoard.ParseSq(mv, 0), to = ChessBoard.ParseSq(mv, 2);
                yield return new WaitForSeconds(0.35f);
                chess.LocalPointer(me, RayTo(chess, seat, chess.Model.BoardPivot.TransformPoint(ChessTableModel.SquareLocal(from))), true);
                yield return new WaitForSeconds(0.45f);
                if (i < 4 || mv.Length > 4) Shot($"{Prefix()}{tag}_{i:00}_select_{mv}");
                chess.LocalPointer(me, RayTo(chess, seat, chess.Model.BoardPivot.TransformPoint(ChessTableModel.SquareLocal(to))), true);
                if (mv.Length > 4)
                {
                    yield return new WaitForSeconds(0.5f);
                    Log($"promotion box open: {(chess.PendingPromotion.from == from ? "OK" : "FAIL")}");
                    Shot($"{Prefix()}{tag}_{i:00}_promotion_box");
                    chess.ChoosePromotion(me, " pnbrqk".IndexOf(mv[4]));
                }
                Log($"{tag} move {i + 1}: {mv}");
                // Frames of the hand carrying the piece, seen from the side.
                if (_dir != null && (i < 3 || mv.Length > 4 || mv == "e1g1" || mv == "e5d6"))
                {
                    yield return new WaitForSeconds(0.3f);
                    Vector3 side = seat == 0 ? new Vector3(1.75f, 1.3f, -0.25f) : new Vector3(-1.75f, 1.3f, 0.25f);
                    RenderTable(chess, $"{tag}_{i:00}_reach_{mv}", side, 46f);
                    yield return new WaitForSeconds(0.45f);
                    RenderTable(chess, $"{tag}_{i:00}_carry_{mv}", side, 46f);
                    Shot($"{Prefix()}{tag}_{i:00}_carry_{mv}");
                    yield return new WaitForSeconds(0.45f);
                    RenderTable(chess, $"{tag}_{i:00}_land_{mv}", side, 46f);
                }
            }
        }
    }
}
