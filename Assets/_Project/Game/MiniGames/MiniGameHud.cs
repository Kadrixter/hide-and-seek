using HideAndSeek.Game.Net;
using HideAndSeek.Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// Screen texts of the board games. Seated: a panel at the top (who plays what, whose move, clocks / score), a big
    /// flash in the middle ("Szach!", "Mat - wygrywa X", "Remis ..."), the promotion choice, key hints at the bottom.
    /// Standing next to a table (spectator): one line about the game at that table.
    /// </summary>
    public sealed partial class MiniGameHud : MonoBehaviour
    {
        Canvas _canvas;
        Text _title, _status, _side, _flash, _hints, _watch;
        Image _panel;
        GameObject _promo;
        float _flashUntil;
        string _flashText;
        int _seenVersion = -1;
        MiniTable _seenTable;

        public static MiniGameHud Create(Transform parent)
        {
            var canvas = UiKit.CreateCanvas("MiniGameHud", 40, parent);
            var h = canvas.gameObject.AddComponent<MiniGameHud>();
            h._canvas = canvas;
            h.Build();
            return h;
        }

        void Build()
        {
            var root = _canvas.transform;
            _panel = UiStyle.Panel("Panel", root, new Color(0.05f, 0.055f, 0.06f, 0.82f));
            _panel.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(760f, 118f));
            // No green accent strip on this panel: it ran right through the team names / score line.
            var accent = _panel.transform.Find("Accent");
            if (accent != null) Destroy(accent.gameObject);
            _title = UiKit.Label("Title", _panel.transform, "", 22, TextAnchor.UpperCenter, UiKit.TextDim);
            _title.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -10), new Vector2(-30, 30));
            _status = UiKit.Label("Status", _panel.transform, "", 34, TextAnchor.MiddleCenter, UiKit.Text, FontStyle.Bold);
            _status.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -4), new Vector2(-30, 44));
            _side = UiKit.Label("Side", _panel.transform, "", 22, TextAnchor.LowerCenter, UiKit.TextDim);
            _side.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 10), new Vector2(-30, 30));

            _flash = UiKit.Label("Flash", root, "", 64, TextAnchor.MiddleCenter, UiKit.Text, FontStyle.Bold);
            _flash.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 170), new Vector2(1400, 90));
            _flash.AddOutline(3f, 0.9f);

            _hints = UiKit.Label("Hints", root, "", 22, TextAnchor.LowerCenter, new Color(0.85f, 0.86f, 0.84f, 0.92f));
            _hints.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 22), new Vector2(1500, 34));
            _hints.AddOutline(2f, 0.8f);

            _watch = UiKit.Label("Watch", root, "", 24, TextAnchor.UpperCenter, new Color(0.92f, 0.9f, 0.82f, 1f));
            _watch.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -70), new Vector2(1200, 34));
            _watch.AddOutline(2f, 0.85f);

            BuildPromotion(root);
            BuildChoice(root);
            SetSeated(false);
            _watch.text = "";
        }

        void SetSeated(bool on)
        {
            if (_panel.gameObject.activeSelf != on) _panel.gameObject.SetActive(on);
            if (_hints.gameObject.activeSelf != on) _hints.gameObject.SetActive(on);
            if (!on && _promo.activeSelf) _promo.SetActive(false);
            if (!on) ShowChoice(false);
        }

        void Flash(string text, float seconds, Color color)
        {
            _flashText = text;
            _flash.color = color;
            _flashUntil = Time.unscaledTime + seconds;
        }

        public void Tick(NetPlayer me, MiniTable table, int seat)
        {
            bool seated = table != null && seat >= 0;
            SetSeated(seated);
            _watch.text = seated ? "" : WatchLine(me);
            if (!(table is PoolTable) || !seated) HidePoolLobby();
            if (!seated)
            {
                if (Time.unscaledTime > _flashUntil) _flash.text = "";
                _seenTable = null;
                return;
            }
            if (table != _seenTable) { _seenTable = table; _seenVersion = -1; _flashUntil = 0f; }
            if (table is ChessTable chess) TickChess(me, chess, seat);
            else if (table is TttTable ttt) TickTtt(ttt, seat);
            else if (table is PoolTable pool) TickPool(pool, seat);
            _hints.text = UI.PadHints.Board(_hints.text); // pad: button names (no-op with keyboard / mouse)
            float left = _flashUntil - Time.unscaledTime;
            _flash.text = left > 0f ? _flashText : "";
            var c = _flash.color;
            c.a = Mathf.Clamp01(left * 2.5f);
            _flash.color = c;
        }

        /// <summary>One line for a spectator standing near a table.</summary>
        static string WatchLine(NetPlayer me)
        {
            if (me == null) return "";
            Vector3 p = me.transform.position;
            var chess = MiniGameTables.Chess;
            if (chess != null && Vector3.Distance(p, chess.Centre) < 3.2f && chess.State.Phase != ChessState.Waiting && chess.State.IsCheckers)
                return CheckersWatchLine(chess);
            if (chess != null && Vector3.Distance(p, chess.Centre) < 3.2f && chess.State.Phase != ChessState.Waiting)
            {
                string w = Nick(chess.State.White), b = Nick(chess.State.Black);
                string what = chess.State.Phase == ChessState.Over
                    ? ChessReferee.Describe(chess.State.End, chess.State.Winner, w, b)
                    : (chess.Shown.Side > 0 ? "ruch białych" : "ruch czarnych") + (chess.Shown.InCheck(chess.Shown.Side) ? " - szach!" : "");
                return $"Szachy: {w} (białe) - {b} (czarne)   |   {what}";
            }
            var ttt = MiniGameTables.Ttt;
            if (ttt != null && Vector3.Distance(p, ttt.Centre) < 3f && ttt.State.Phase != TttState.Waiting)
                return $"Kółko i krzyżyk: {Nick(ttt.State.A)} (X) {ttt.State.ScoreA} : {ttt.State.ScoreB} {Nick(ttt.State.B)} (O)";
            var pool = MiniGameTables.Pool;
            if (pool != null && pool.DistanceToEdge(p) < 2.2f && pool.State.Phase != HideAndSeek.Game.MiniGames.Pool.PoolState.Waiting) return PoolWatchLine(pool);
            return "";
        }

        static string Nick(ulong id)
        {
            var p = id == ulong.MaxValue ? null : NetPlayer.Find(id);
            return p != null ? p.Nick : "?";
        }
    }
}
