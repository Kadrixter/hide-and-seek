using HideAndSeek.Game.Net;
using HideAndSeek.Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// HUD of the chess table's two games: the game choice ("Szachy" / "Warcaby", mouse) shown to the seated players
    /// while no game runs (both see the pick), the checkers texts (whose move, mandatory capture, clocks, pieces left,
    /// the end) and the rematch hints shared with chess (R = rematch, G = rematch in the other game).
    /// </summary>
    public sealed partial class MiniGameHud
    {
        GameObject _choice;
        Image _choiceChess, _choiceCheckers;
        Text _choiceNote;
        ChessTable _choiceTable;
        int _ckFlashedGame = -1;
        float _ckDenySeen = -10f;
        static readonly Color ChoiceOff = new(1f, 1f, 1f, 0.06f), ChoiceOn = new(0.25f, 0.72f, 0.66f, 0.45f);

        void BuildChoice(Transform root)
        {
            var panel = UiStyle.Panel("BoardChoice", root, new Color(0.05f, 0.055f, 0.06f, 0.9f));
            panel.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(600, 230));
            _choice = panel.gameObject;
            var title = UiKit.Label("Q", panel.transform, "W co gramy?", 30, TextAnchor.MiddleCenter, UiKit.Text, FontStyle.Bold);
            title.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -14), new Vector2(-20, 42));
            var chess = UiKit.BoxButton("Chess", panel.transform, "SZACHY", () => Pick(ChessState.KindChess), 28, ChoiceOff);
            ((RectTransform)chess.transform).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-138f, 2f), new Vector2(250f, 78f));
            var checkers = UiKit.BoxButton("Checkers", panel.transform, "WARCABY", () => Pick(ChessState.KindCheckers), 28, ChoiceOff);
            ((RectTransform)checkers.transform).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(138f, 2f), new Vector2(250f, 78f));
            _choiceChess = chess.GetComponent<Image>();
            _choiceCheckers = checkers.GetComponent<Image>();
            _choiceNote = UiKit.Label("Note", panel.transform, "", 20, TextAnchor.MiddleCenter, UiKit.TextDim);
            _choiceNote.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(-24, 30));
            _choice.SetActive(false);
        }

        void Pick(byte kind)
        {
            if (_choiceTable != null && NetPlayer.Local != null) NetPlayer.Local.BoardKindRpc(kind);
        }

        void ShowChoice(bool on)
        {
            if (_choice != null && _choice.activeSelf != on) _choice.SetActive(on);
        }

        /// <summary>Seated at the chess table before a game: pick chess or checkers (the opponent sees the pick).</summary>
        void TickBoardChoice(ChessTable t, int seat)
        {
            var s = t.State;
            _choiceTable = t;
            ShowChoice(true);
            if (_promo.activeSelf) _promo.SetActive(false);
            string opp = t.SeatNick(1 - seat);
            bool both = !string.IsNullOrEmpty(opp);
            _title.text = $"STOLIK DO GRY   |   zegar: {ChessState.ClockName(s.ClockMin, s.ClockInc)}";
            if (s.Kind == ChessState.KindNone)
            {
                _status.text = both ? "Wybierz grę" : "Wybierz grę i poczekaj na przeciwnika";
                _choiceNote.text = both ? $"Wybór widzicie oboje - może go też zrobić {opp}" : "Gra zacznie się, gdy ktoś się dosiądzie";
            }
            else
            {
                _status.text = $"Wybrano: {(s.IsCheckers ? "warcaby" : "szachy")} - czekamy na przeciwnika...";
                _choiceNote.text = "Możesz jeszcze zmienić grę";
            }
            _status.color = UiKit.Text;
            _side.text = both ? $"Przeciwnik: {opp}" : "Przeciwnik: -";
            _choiceChess.color = s.Kind == ChessState.KindChess ? ChoiceOn : ChoiceOff;
            _choiceCheckers.color = s.IsCheckers ? ChoiceOn : ChoiceOff;
            _hints.text = $"LPM - wybierz grę      C - zegar: {ChessState.ClockName(s.ClockMin, s.ClockInc)}      Esc - wstań";
            _seenPhase = s.Phase;
            _seenVersion = t.Version;
        }

        /// <summary>"1 pionek", "3 pionki", "12 pionków".</summary>
        static string Pcs(int n) => n == 1 ? "1 pionek"
            : n % 10 >= 2 && n % 10 <= 4 && (n % 100 < 10 || n % 100 >= 20) ? $"{n} pionki" : $"{n} pionków";

        static string Clocks(ChessTable t, int colour, string opp)
        {
            var b = t.CkShown;
            int mine = b.Count(colour), theirs = b.Count(-colour);
            return t.State.HasClock
                ? $"Ty  {Clock(t, colour)}  ({Pcs(mine)})      |      {opp}  {Clock(t, -colour)}  ({Pcs(theirs)})"
                : $"Ty: {Pcs(mine)}   |   {opp}: {Pcs(theirs)}   |   ruch {t.State.Ply / 2 + 1}";
        }

        void TickCheckers(ChessTable t, int seat)
        {
            var s = t.State;
            int colour = s.ColourOfSeat(seat);
            string opp = t.SeatNick(1 - seat);
            if (string.IsNullOrEmpty(opp)) opp = "-";
            _title.text = $"WARCABY   |   grasz {(colour > 0 ? "białymi" : "czarnymi")}   |   zegar: {ChessState.ClockName(s.ClockMin, s.ClockInc)}";
            bool myTurn = s.Phase == ChessState.Playing && t.CkTarget.Side == colour;
            string white = t.SeatNick(s.WhiteIsA ? 0 : 1), black = t.SeatNick(s.WhiteIsA ? 1 : 0);
            if (s.Phase == ChessState.Playing)
            {
                int must = t.Animating ? 0 : t.CkMustCapture;
                _status.text = myTurn ? (must > 0 ? $"Twój ruch - bicie obowiązkowe ({Pcs(must)})" : "Twój ruch")
                    : $"Ruch przeciwnika ({opp})";
                _status.color = myTurn ? (must > 0 ? Gold : Good) : UiKit.Text;
            }
            else
            {
                _status.text = t.Animating ? "..." : CheckersReferee.Describe(s.End, s.Winner, white, black);
                _status.color = Gold;
            }
            _side.text = Clocks(t, colour, opp);
            if (s.Phase != _seenPhase)
            {
                if (s.Phase == ChessState.Playing && _seenPhase != 255) Flash(colour > 0 ? "Warcaby! Grasz białymi" : "Warcaby! Grasz czarnymi", 2.2f, Good);
                _seenPhase = s.Phase;
            }
            if (_seenVersion == -1) _ckFlashedGame = s.Phase == ChessState.Over ? s.Game : -1;
            if (!t.Animating && s.Phase == ChessState.Over && _ckFlashedGame != s.Game)
            {
                _ckFlashedGame = s.Game;
                Flash(CheckersReferee.Describe(s.End, s.Winner, white, black), 4.5f, Gold);
            }
            if (t.CkDenied > _ckDenySeen)
            {
                _ckDenySeen = t.CkDenied;
                if (t.CkMustCapture > 0) Flash("Bicie jest obowiązkowe!", 1.4f, Bad);
            }
            _seenVersion = t.Version;
            if (_promo.activeSelf) _promo.SetActive(false);
            _hints.text = s.Phase == ChessState.Playing
                ? "LPM - pionek, potem pole (bicie wielokrotne: pole po polu)      kółko myszy - przybliż      Esc - poddaj i wstań"
                : $"{RematchHint(t, seat)}C - zegar: {ChessState.ClockName(s.ClockMin, s.ClockInc)}      Esc - wstań";
        }

        /// <summary>After a game: R = rematch, G = rematch in the other game; what the opponent asked for.</summary>
        static string RematchHint(ChessTable t, int seat)
        {
            var s = t.State;
            if (s.Phase != ChessState.Over || string.IsNullOrEmpty(t.SeatNick(1 - seat))) return "";
            int mine = 1 << seat, other = 1 << (1 - seat);
            bool sw = (s.Rematch & ChessState.RematchSwitch) != 0;
            string otherGame = ChessState.KindName(s.OtherKind);
            if ((s.Rematch & mine) != 0) return sw ? $"czekamy na zgodę przeciwnika ({otherGame})...      " : "czekamy na zgodę przeciwnika...      ";
            if ((s.Rematch & other) != 0) return sw ? $"R - przeciwnik proponuje {otherGame}, zgódź się      " : "R - przeciwnik chce rewanżu, zgódź się      ";
            return $"R - rewanż (zamiana kolorów)      G - zagrajmy w {otherGame}      ";
        }

        static string CheckersWatchLine(ChessTable t)
        {
            var s = t.State;
            string w = Nick(s.White), b = Nick(s.Black);
            string what = s.Phase == ChessState.Over ? CheckersReferee.Describe(s.End, s.Winner, w, b)
                : t.CkShown.Side > 0 ? "ruch białych" : "ruch czarnych";
            return $"Warcaby: {w} (białe) - {b} (czarne)   |   {what}";
        }
    }
}
