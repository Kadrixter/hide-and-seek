using HideAndSeek.Game.MiniGames.Pool;
using HideAndSeek.Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// Pool texts of the board-game HUD: whose shot, the own group (solids / stripes / open table), balls left of
    /// each group, the note of the last shot, ball in hand, the series score, rematch; flashes for the break, a
    /// foul giving ball in hand and the end of a game. The result of a shot shows only once the balls stopped.
    /// </summary>
    public sealed partial class MiniGameHud
    {
        const string PoolAimHint = "Mysz - celuj (Shift / Q E - precyzyjnie)   LPM przytrzymaj i odciągnij - siła, puść - strzał   PPM / WASD - podkręcenie   Spacja - widok z góry   Esc - odejdź";
        int _poolRound = -1, _poolEndFlashed = -1, _poolShotSeen = -1;

        static int Left(PoolState s, int from, int to)
        {
            int n = 0;
            for (int b = from; b <= to; b++) if (!s.IsDown(b)) n++;
            return n;
        }

        static string GroupName(int g) => g == 1 ? "pełne (1-7)" : g == 2 ? "połówki (9-15)" : "stół otwarty";

        void TickPool(PoolTable t, int seat)
        {
            var s = t.State;
            // Pool texts can be long (notes, endings): they shrink to one line instead of wrapping over each other.
            FitOneLine(_title, 14);
            FitOneLine(_status, 20);
            FitOneLine(_side, 13);
            string opp = t.SeatNick(1 - seat);
            if (string.IsNullOrEmpty(opp)) opp = "-";
            int my = seat == 0 ? s.WinsA : s.WinsB, their = seat == 0 ? s.WinsB : s.WinsA;
            bool rolling = t.Rolling;
            int g = s.GroupOf(seat);
            string mine = g == 0 ? "stół otwarty" : $"grasz: {GroupName(g)}, zostało {(g == 1 ? Left(s, 1, 7) : Left(s, 9, 15))}";
            _title.text = $"BILARD - ÓSEMKA   |   {mine}   |   seria {my} : {their}";
            bool myTurn = s.Phase == PoolState.Playing && s.Turn == seat && !rolling;
            switch (s.Phase)
            {
                case PoolState.Waiting:
                    _status.text = "Czekamy na przeciwnika...";
                    _status.color = UiKit.Text;
                    break;
                case PoolState.Playing:
                    if (rolling) _status.text = t.ReplaySeat == seat ? "Kule w ruchu..." : $"Strzela {opp}...";
                    else if (myTurn) _status.text = s.Break ? "Rozbijasz!" : s.BallInHand ? "Twój strzał - biała w ręce" : "Twój strzał";
                    else _status.text = $"Strzela {opp}";
                    _status.color = myTurn ? Good : UiKit.Text;
                    break;
                default:
                    _status.text = rolling ? "Kule w ruchu..." : PoolEnd(s, seat, opp);
                    _status.color = Gold;
                    break;
            }
            // One short line under the status (a long note would wrap into the status above it): the last shot's
            // note once the balls stopped, else how many balls each group has left.
            if (s.Phase == PoolState.Waiting) _side.text = "";
            else _side.text = !rolling && !string.IsNullOrEmpty(s.Note) ? s.Note : $"pełne: {Left(s, 1, 7)}      połówki: {Left(s, 9, 15)}";
            // Flashes (only for states seen after the HUD came up, and only once the balls stopped).
            if (_seenVersion == -1)
            {
                _poolRound = s.Round;
                _poolEndFlashed = s.Phase == PoolState.Over ? s.Round : -1;
                _poolShotSeen = s.ShotNo;
            }
            if (!rolling)
            {
                if (s.Phase == PoolState.Playing && s.Round != _poolRound)
                    Flash(s.Turn == seat ? "Rozbijasz!" : $"Rozbija {opp}", 2f, Good);
                else if (s.Phase == PoolState.Over && _poolEndFlashed != s.Round)
                {
                    _poolEndFlashed = s.Round;
                    Flash(PoolEnd(s, seat, opp), 4.5f, Gold);
                }
                else if (s.Phase == PoolState.Playing && s.ShotNo != _poolShotSeen && s.BallInHand && !s.Break)
                    Flash(s.Turn == seat ? "Faul przeciwnika - biała w ręce" : "Faul - przeciwnik ma białą w ręce", 2.2f, s.Turn == seat ? Good : Bad);
                _poolRound = s.Round;
                _poolShotSeen = s.ShotNo;
            }
            _seenVersion = t.Version;
            // Hints.
            if (s.Phase == PoolState.Playing)
                _hints.text = myTurn ? (t.LocalHolding ? "Biała w ręce - LPM połóż ją na stole (zielony krąg = wolno)"
                        : (s.BallInHand ? "B - weź białą znowu do ręki      " : "") + PoolAimHint)
                    : "Czekaj na swoją kolej      Esc - odejdź (oddajesz partię)";
            else if (s.Phase == PoolState.Over)
            {
                int other = 1 << (1 - seat), mineBit = 1 << seat;
                string rematch = t.SeatNick(1 - seat) == "" ? ""
                    : (s.Rematch & mineBit) != 0 ? "czekamy na zgodę przeciwnika...      "
                    : (s.Rematch & other) != 0 ? "R - przeciwnik chce rewanżu, zgódź się      " : "R - rewanż (rozbija przegrany)      ";
                _hints.text = rematch + "Esc - odejdź od stołu";
            }
            else _hints.text = "Esc - odejdź od stołu";
            if (_promo.activeSelf) _promo.SetActive(false);
        }

        static void FitOneLine(Text label, int min)
        {
            if (label == null || label.resizeTextForBestFit) return;
            label.resizeTextMaxSize = label.fontSize;
            label.resizeTextMinSize = min;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.resizeTextForBestFit = true;
        }

        static string PoolEnd(PoolState s, int seat, string opp)
        {
            bool won = s.Winner == seat;
            return s.EndReason switch
            {
                PoolState.EndEight => won ? "Wygrywasz - ósemka w łuzie!" : $"Wygrywa {opp} - ósemka w łuzie",
                PoolState.EndEightEarly => won ? "Przedwczesna ósemka przeciwnika - wygrywasz!" : "Ósemka wbita za wcześnie - przegrywasz",
                PoolState.EndEightScratch => won ? "Ósemka przeciwnika z białą w łuzie - wygrywasz!" : "Ósemka i biała w łuzie - przegrywasz",
                PoolState.EndLeft => won ? "Przeciwnik odszedł - wygrywasz" : "Partia przerwana",
                _ => s.Winner > 1 ? "Koniec partii" : won ? "Wygrywasz!" : $"Wygrywa {opp}",
            };
        }

        /// <summary>One line for a spectator next to the pool table.</summary>
        static string PoolWatchLine(PoolTable t)
        {
            var s = t.State;
            string a = Nick(s.A), b = Nick(s.B);
            string what = s.Phase == PoolState.Over
                ? (s.Winner <= 1 ? $"wygrywa {(s.Winner == 0 ? a : b)}" : "koniec partii")
                : t.Rolling ? "kule w ruchu" : $"strzela {(s.Turn == 0 ? a : b)}";
            return $"Bilard: {a} {s.WinsA} : {s.WinsB} {b}   |   {what}";
        }
    }
}
