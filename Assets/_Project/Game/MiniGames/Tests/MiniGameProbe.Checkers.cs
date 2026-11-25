using System.Collections;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// -testcheckers &lt;dir&gt; (host AND client): the chess table's second game and its lamp. Empty board and dark lamp
    /// while the table is free; the host sits down, gets the game choice and picks checkers; the client sits down, the
    /// game starts (discs pop up, the lamp comes on); both play the scripted game of <see cref="CheckersScript"/> through
    /// the real clicks (incl. a refused non-capture while capturing is mandatory, a multi-capture clicked square by
    /// square, a promotion, the king's long move and flying capture) to its end; then G / R switch to chess, a
    /// scholar's mate is played, both get up and the board is cleared, the lamp goes dark. Logs "[CheckersTest]" PASS / FAIL.
    /// </summary>
    public sealed partial class MiniGameProbe
    {
        void Verdict(string what, bool ok, string detail = "") => Log($"{what}: {(ok ? "PASS" : "FAIL")} {detail}");

        /// <summary>Standing view of the table from a player's eye height (free camera) into side_NAME.png.</summary>
        void PlayerView(ChessTable t, string name, Vector3 local)
        {
            Render(name, t.transform.TransformPoint(local), t.Centre + Vector3.up * 0.05f, 60f);
        }

        /// <summary>The local player stands next to the table looking at it (first-person screenshot).</summary>
        IEnumerator StandAndLook(ChessTable t, Vector3 local, string shot)
        {
            var me = NetPlayer.Local;
            Vector3 at = t.transform.TransformPoint(local);
            Vector3 d = t.Centre - at;
            me.TeleportLocal(new Vector3(at.x, t.transform.position.y + 0.05f, at.z), Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg);
            me.Motor.SetLook(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, -24f); // negative pitch = down
            yield return new WaitForSeconds(1.2f);
            Shot(shot);
        }

        /// <summary>
        /// Other furniture inside the chess corner (table + stools + room for the players' legs, up to the lamp) and
        /// next to the lamp's cable: logs every renderer / collider of the room that reaches into it.
        /// </summary>
        void ClearanceCheck(ChessTable t)
        {
            var box = new Bounds(Vector3.zero, Vector3.zero);
            bool any = false;
            int found = 0;
            foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude))
            {
                if (r.transform.IsChildOf(t.transform) || !r.enabled) continue;
                var b = r.bounds;
                if (b.size.x > 5f || b.size.z > 5f) continue; // floor, ceiling, walls
                // Table space footprint: 1.3 m wide, seat to seat incl. the stools' backs (+-1.0 m), floor to the ceiling.
                Vector3 c = t.transform.InverseTransformPoint(b.center);
                Vector3 e = b.extents;
                bool hit = Mathf.Abs(c.x) - e.z < 0.65f && Mathf.Abs(c.z) - e.x < 1.0f && b.max.y > 0.03f;
                if (!hit) continue;
                found++;
                if (!any) { box = b; any = true; } else box.Encapsulate(b);
                Log($"clearance: '{r.name}' ({(r.transform.parent != null ? r.transform.parent.name : "-")}) local {c} size {b.size}");
            }
            Verdict("nothing else in the chess corner", found == 0, $"{found} object(s)");
        }

        IEnumerator CheckersRun(MatchManager mm)
        {
            _tag = "CheckersTest";
            var t = MiniGameTables.Chess;
            var me = NetPlayer.Local;
            int seat = _host ? 0 : 1;
            var script = CheckersScript.Find();
            Log($"script: {(script != null ? script.ToString() : "none")}");
            if (script == null) yield break;
            if (!_host) yield return new WaitForSeconds(2.5f);

            // ---- free table: empty board, dark lamp (from a player's view and from the side)
            Verdict("free table: empty board, lamp off", t.PieceObjects == 0 && t.Lamp.Level < 0.01f && !t.Lamp.Model.Light.enabled,
                $"pieces={t.PieceObjects} lamp={t.Lamp.Level:0.00}");
            if (_host)
            {
                ClearanceCheck(t);
                PlayerView(t, "ck_00_free_player_view", new Vector3(1.55f, 1.62f, -1.35f));
                Render("ck_00_corner_high", t.transform.TransformPoint(new Vector3(2.4f, 3.2f, -2.4f)), t.Centre, 70f);
                RenderTable(t, "ck_00_free_side", new Vector3(1.7f, 1.9f, -1.6f), 55f);
                yield return StandAndLook(t, new Vector3(1.3f, 0f, -1.2f), "ck_00_free_fp");
            }

            // ---- the host sits down and picks checkers in the choice panel
            if (_host)
            {
                yield return SitDown(t, 0);
                yield return new WaitForSeconds(0.6f);
                var panel = GameObject.Find("BoardChoice");
                Verdict("choice panel shown to the seated player", panel != null && panel.activeInHierarchy);
                Shot("a_ck_01_choice");
                yield return new WaitForSeconds(0.5f);
                var btn = panel != null ? panel.transform.Find("Checkers")?.GetComponent<UnityEngine.UI.Button>() : null;
                if (btn != null) btn.onClick.Invoke(); else me.BoardKindRpc(ChessState.KindCheckers);
                for (float w = 0f; w < 5f && t.State.Kind != ChessState.KindCheckers; w += Time.deltaTime) yield return null;
                Verdict("pick synced (kind = checkers)", t.State.Kind == ChessState.KindCheckers);
                yield return new WaitForSeconds(0.4f);
                Shot("a_ck_02_picked_waiting");
                Verdict("still empty and dark while waiting", t.PieceObjects == 0 && t.Lamp.Level < 0.01f, $"pieces={t.PieceObjects}");
            }
            else
            {
                for (float w = 0f; w < 25f && t.State.Kind != ChessState.KindCheckers; w += Time.deltaTime) yield return null;
                Verdict("client sees the host's pick", t.State.Kind == ChessState.KindCheckers && t.State.A != ChessState.Nobody);
                yield return SitDown(t, 1);
            }
            for (float w = 0f; w < 10f && t.State.Phase != ChessState.Playing; w += Time.deltaTime) yield return null;
            Verdict("checkers game started", t.State.Phase == ChessState.Playing && t.State.IsCheckers, $"colour={(t.State.ColourOfSeat(seat) > 0 ? "white" : "black")}");
            yield return new WaitForSeconds(0.75f);
            Shot(Prefix() + "ck_03_setting_up");
            if (_host) RenderTable(t, "ck_03_setting_up", new Vector3(1.5f, 1.45f, -1.25f));
            for (float w = 0f; w < 6f && t.Animating; w += Time.deltaTime) yield return null;
            yield return new WaitForSeconds(0.6f);
            Verdict("discs set up, lamp on", t.PieceObjects == 24 && t.Lamp.Level > 0.95f && t.Lamp.Model.Light.enabled, $"pieces={t.PieceObjects} lamp={t.Lamp.Level:0.00}");
            Shot(Prefix() + "ck_04_start_seated");
            if (_host)
            {
                RenderTable(t, "ck_04_lamp_on_side", new Vector3(1.7f, 1.9f, -1.6f), 55f);
                PlayerView(t, "ck_04_lamp_on_player_view", new Vector3(1.55f, 1.62f, -1.35f));
                PlayerView(t, "ck_04_lamp_on_far", new Vector3(3.2f, 1.62f, -2.6f));
            }

            yield return PlayCheckers(t, seat, script);
            for (float w = 0f; w < 12f && (t.Animating || t.State.Phase != ChessState.Over); w += Time.deltaTime) yield return null;
            yield return new WaitForSeconds(0.8f);
            Verdict("game over as scripted", t.State.Phase == ChessState.Over && t.State.End == script.End && t.State.Winner == script.Winner,
                $"end={t.State.End} winner={t.State.Winner} text='{CheckersReferee.Describe(t.State.End, t.State.Winner, t.SeatNick(t.State.WhiteIsA ? 0 : 1), t.SeatNick(t.State.WhiteIsA ? 1 : 0))}'");
            Shot(Prefix() + "ck_80_end");
            if (_host) RenderTable(t, "ck_80_end", new Vector3(1.3f, 1.4f, -1.1f));

            // ---- G: a rematch in chess (the host proposes, the client agrees with R)
            yield return new WaitForSeconds(2f);
            if (_host) me.BoardKindRpc(ChessState.KindChess);
            else
            {
                for (float w = 0f; w < 8f && (t.State.Rematch & ChessState.RematchSwitch) == 0; w += Time.deltaTime) yield return null;
                Verdict("client sees the proposal to switch to chess", (t.State.Rematch & ChessState.RematchSwitch) != 0);
                yield return new WaitForSeconds(0.6f);
                Shot("b_ck_81_switch_proposal");
                me.MiniRematchRpc();
            }
            for (float w = 0f; w < 10f && !(t.State.Phase == ChessState.Playing && t.State.Kind == ChessState.KindChess); w += Time.deltaTime) yield return null;
            for (float w = 0f; w < 6f && t.Animating; w += Time.deltaTime) yield return null;
            yield return new WaitForSeconds(0.5f);
            Verdict("chess after checkers", t.State.Kind == ChessState.KindChess && t.State.Phase == ChessState.Playing && t.PieceObjects == 32 && !t.State.WhiteIsA,
                $"pieces={t.PieceObjects} whiteIsA={t.State.WhiteIsA}");
            Shot(Prefix() + "ck_82_chess");
            yield return Play(t, seat, Scholar, "ckchess");
            for (float w = 0f; w < 8f && (t.Animating || t.State.Phase != ChessState.Over); w += Time.deltaTime) yield return null;
            Verdict("chess still works (scholar's mate)", t.State.End == GameEnd.Checkmate && t.State.Winner == 1, $"{t.State.End} {t.State.Winner}");
            Shot(Prefix() + "ck_83_chess_mate");

            // ---- both get up (the host first: a standing look at the lit table), board cleared, lamp fades out
            yield return new WaitForSeconds(_host ? 1.5f : 9f);
            me.MiniLeaveRpc();
            if (_host)
            {
                yield return new WaitForSeconds(1.2f);
                yield return StandAndLook(t, new Vector3(1.3f, 0f, -1.2f), "ck_85_lamp_on_standing_fp");
                Render("ck_86_lamp_closeup", t.transform.TransformPoint(new Vector3(0.75f, 1.3f, -0.75f)), t.transform.TransformPoint(new Vector3(0f, ChessLampModel.RimY + 0.08f, 0f)), 40f);
                Render("ck_87_lamp_under", t.transform.TransformPoint(new Vector3(0.35f, 1.05f, -0.4f)), t.transform.TransformPoint(new Vector3(0f, ChessLampModel.RimY + 0.12f, 0f)), 50f);
            }
            for (float w = 0f; w < 8f && t.State.Phase != ChessState.Waiting; w += Time.deltaTime) yield return null;
            if (_host) RenderTable(t, "ck_90_clearing", new Vector3(1.7f, 1.9f, -1.6f), 55f);
            yield return new WaitForSeconds(3f);
            Verdict("table free again: board cleared, lamp off", t.State.Phase == ChessState.Waiting && t.State.Kind == ChessState.KindNone && t.PieceObjects == 0 && t.Lamp.Level < 0.01f,
                $"phase={t.State.Phase} kind={t.State.Kind} pieces={t.PieceObjects} lamp={t.Lamp.Level:0.00}");
            if (_host)
            {
                RenderTable(t, "ck_91_free_again", new Vector3(1.7f, 1.9f, -1.6f), 55f);
                yield return StandAndLook(t, new Vector3(1.3f, 0f, -1.2f), "ck_91_free_again_fp");
            }
            yield return new WaitForSeconds(2f);
        }
    }
}
