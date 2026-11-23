using HideAndSeek.Game.Net;
using HideAndSeek.Game.UI;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>Per-game texts of the board-game HUD and the promotion choice.</summary>
    public sealed partial class MiniGameHud
    {
        static readonly Color Good = new(0.45f, 0.95f, 0.5f), Bad = new(1f, 0.32f, 0.25f), Gold = new(1f, 0.85f, 0.35f);
        int _seenPly = -1, _flashPly = -1, _flashedGame = -1;
        byte _seenPhase = 255;
        ChessTable _promoTable;

        void BuildPromotion(Transform root)
        {
            var panel = UiStyle.Panel("Promotion", root, new Color(0.05f, 0.055f, 0.06f, 0.95f));
            panel.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(620, 250));
            _promo = panel.gameObject;
            var title = UiKit.Label("Q", panel.transform, "Promocja - wybierz figurę", 28, TextAnchor.MiddleCenter, UiKit.Text, FontStyle.Bold);
            title.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(-20, 40));
            (string name, int type)[] opts = { ("Hetman", ChessBoard.Queen), ("Wieża", ChessBoard.Rook), ("Goniec", ChessBoard.Bishop), ("Skoczek", ChessBoard.Knight) };
            for (int i = 0; i < opts.Length; i++)
            {
                int type = opts[i].type;
                var b = UiKit.Button("Opt" + i, panel.transform, opts[i].name, () => Choose(type), 24);
                ((RectTransform)b.transform).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(-218f + i * 145f, 4f), new Vector2(135f, 64f));
            }
            var cancel = UiKit.Button("Cancel", panel.transform, "Anuluj", () => Choose(0), 20, new Color(0.25f, 0.12f, 0.1f));
            ((RectTransform)cancel.transform).Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(180f, 46f));
            _promo.SetActive(false);
        }

        void Choose(int type)
        {
            if (_promoTable != null && NetPlayer.Local != null) _promoTable.ChoosePromotion(NetPlayer.Local, type);
            _promo.SetActive(false);
        }

        static string Clock(ChessTable t, int colour)
        {
            if (!t.State.HasClock) return "";
            var mm = MatchManager.Instance;
            int ms = t.State.MsLeft(colour, t.Target.Side, mm != null ? mm.Now : 0d);
            return ChessClockFace.Format(ms);
        }

        void TickChess(NetPlayer me, ChessTable t, int seat)
        {
            var s = t.State;
            int colour = s.ColourOfSeat(seat);
            string opp = t.SeatNick(1 - seat);
            if (string.IsNullOrEmpty(opp)) opp = "-";
            string clockName = ChessState.ClockName(s.ClockMin, s.ClockInc);
            _title.text = $"SZACHY   |   grasz {(colour > 0 ? "białymi" : "czarnymi")}   |   zegar: {clockName}";
            bool myTurn = s.Phase == ChessState.Playing && t.Target.Side == colour;
            switch (s.Phase)
            {
                case ChessState.Waiting:
                    _status.text = "Czekamy na przeciwnika...";
                    _status.color = UiKit.Text;
                    break;
                case ChessState.Playing:
                    bool check = !t.Animating && t.Shown.InCheck(t.Shown.Side);
                    _status.text = myTurn ? (check ? "Szach! Twój ruch" : "Twój ruch") : $"Ruch przeciwnika ({opp})";
                    _status.color = myTurn ? (check ? Bad : Good) : UiKit.Text;
                    break;
                default:
                    _status.text = t.Animating ? "..." : ChessReferee.Describe(s.End, s.Winner, t.SeatNick(s.WhiteIsA ? 0 : 1), t.SeatNick(s.WhiteIsA ? 1 : 0));
                    _status.color = Gold;
                    break;
            }
            string mine = Clock(t, colour), theirs = Clock(t, -colour);
            _side.text = s.HasClock ? $"Ty  {mine}      |      {opp}  {theirs}" : $"Ty vs {opp}   |   ruch {s.Ply / 2 + 1}";
            // Flashes: game start, check (when the piece has landed), game end.
            if (s.Phase != _seenPhase)
            {
                if (s.Phase == ChessState.Playing && _seenPhase != 255) Flash(colour > 0 ? "Start! Grasz białymi" : "Start! Grasz czarnymi", 2.2f, Good);
                _seenPhase = s.Phase;
            }
            if (_seenVersion == -1) { _flashedGame = s.Phase == ChessState.Over ? s.Game : -1; _flashPly = s.Ply; }
            if (!t.Animating)
            {
                string end = ChessReferee.Describe(s.End, s.Winner, t.SeatNick(s.WhiteIsA ? 0 : 1), t.SeatNick(s.WhiteIsA ? 1 : 0));
                if (s.Phase == ChessState.Over && _flashedGame != s.Game) { _flashedGame = s.Game; Flash(end, 4.5f, Gold); }
                else if (s.Phase == ChessState.Playing && s.Ply != _flashPly)
                {
                    _flashPly = s.Ply;
                    if (t.Shown.InCheck(t.Shown.Side)) Flash("Szach!", 1.6f, Bad);
                }
            }
            _seenVersion = t.Version;
            // Promotion choice.
            bool promo = t.PendingPromotion.from >= 0;
            _promoTable = t;
            if (_promo.activeSelf != promo) _promo.SetActive(promo);
            // Hints.
            if (s.Phase == ChessState.Playing)
                _hints.text = "LPM - wybierz figurę, potem pole      kółko myszy - przybliż      Esc - poddaj i wstań";
            else
            {
                int other = 1 << (1 - seat), mineBit = 1 << seat;
                string rematch = s.Phase != ChessState.Over || string.IsNullOrEmpty(t.SeatNick(1 - seat)) ? ""
                    : (s.Rematch & mineBit) != 0 ? "czekamy na zgodę przeciwnika...      "
                    : (s.Rematch & other) != 0 ? "R - przeciwnik chce rewanżu, zgódź się      " : "R - rewanż (zamiana kolorów)      ";
                _hints.text = $"{rematch}C - zegar: {clockName}      Esc - wstań";
            }
            _ = me;
        }

        void TickTtt(TttTable t, int seat)
        {
            var s = t.State;
            string opp = t.SeatNick(1 - seat);
            if (string.IsNullOrEmpty(opp)) opp = "-";
            char mark = TttState.Mark(seat);
            int my = seat == 0 ? s.ScoreA : s.ScoreB, their = seat == 0 ? s.ScoreB : s.ScoreA;
            _title.text = $"KÓŁKO I KRZYŻYK   |   grasz: {mark}   |   runda {Mathf.Max(1, s.Round)}";
            _side.text = s.Phase == TttState.Waiting ? "" : $"Ty {my} : {their} {opp}      remisy: {s.Draws}";
            bool myTurn = s.Phase == TttState.Playing && s.Turn == seat;
            switch (s.Phase)
            {
                case TttState.Waiting:
                    _status.text = "Czekamy na przeciwnika...";
                    _status.color = UiKit.Text;
                    break;
                case TttState.Playing:
                    _status.text = myTurn ? $"Twój ruch ({mark})" : $"Ruch przeciwnika ({opp})";
                    _status.color = myTurn ? Good : UiKit.Text;
                    break;
                default:
                    _status.text = t.Animating ? "..." : TttEnd(s, seat, opp);
                    _status.color = Gold;
                    break;
            }
            if (s.Phase != _seenPhase || t.Version != _seenVersion && s.Phase == TttState.Over && !t.Animating && _seenPly != s.Round)
            {
                if (s.Phase == TttState.Playing && _seenPhase != 255)
                    Flash(s.Turn == seat ? "Zaczynasz!" : $"Zaczyna {opp}", 1.8f, Good);
                if (s.Phase == TttState.Over && !t.Animating) { Flash(TttEnd(s, seat, opp), 3f, Gold); _seenPly = s.Round; }
                if (s.Phase != TttState.Over || !t.Animating) _seenPhase = s.Phase;
                _seenVersion = t.Version;
            }
            _hints.text = s.Phase == TttState.Playing ? "LPM - postaw znak      Esc - odejdź od tablicy"
                : s.Phase == TttState.Over ? "R - następna runda (zaczyna druga osoba)      Esc - odejdź od tablicy" : "Esc - odejdź od tablicy";
            if (_promo.activeSelf) _promo.SetActive(false);
        }

        static string TttEnd(TttState s, int seat, string opp) => s.End switch
        {
            GameEnd.Full => "Remis!",
            GameEnd.Left => "Przeciwnik wyszedł",
            _ => s.Winner == seat ? "Wygrywasz!" : $"Wygrywa {opp}",
        };
    }
}
