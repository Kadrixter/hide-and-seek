using HideAndSeek.Game.MiniGames.Pool;
using HideAndSeek.Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// Pool texts of the board-game HUD, kept short: during a game a slim panel with the teams and the series score,
    /// whose shot it is and the groups (the last shot's note for a few seconds after the balls stop); one controls
    /// line that fades out a few seconds into the own turn; flashes for the break, a foul and the end of a game.
    /// Between games the join panel (MiniGameHud.PoolLobby) is shown instead.
    /// </summary>
    public sealed partial class MiniGameHud
    {
        const string PoolAimHint = "LPM przytrzymaj i odciągnij - strzał    PPM - podkręcenie    Spacja - z góry    Esc - odejdź";
        const float HintShow = 7f, NoteShow = 4f;
        int _poolRound = -1, _poolEndFlashed = -1, _poolShotSeen = -1, _poolNoteShot = -1;
        float _poolNoteAt, _poolHintAt;
        string _poolHintKey;

        static int Left(PoolState s, int from, int to)
        {
            int n = 0;
            for (int b = from; b <= to; b++) if (!s.IsDown(b)) n++;
            return n;
        }

        static string GroupShort(PoolState s, int team)
        {
            int g = s.GroupOf(team);
            return g == 1 ? $"pełne ({Left(s, 1, 7)})" : g == 2 ? $"połówki ({Left(s, 9, 15)})" : "-";
        }

        /// <summary>"Host, Ola" - the nicks of a team ("-" when empty).</summary>
        static string TeamNicks(PoolTable t, int team)
        {
            string a = t.SeatNick(team), b = t.SeatNick(team + 2);
            if (a == "") return b == "" ? "-" : b;
            return b == "" ? a : a + ", " + b;
        }

        void TickPool(PoolTable t, int place)
        {
            var s = t.State;
            int team = PoolState.TeamOf(place), other = 1 - team;
            bool playing = s.Phase == PoolState.Playing;
            TickPoolLobby(t, place, !playing);
            if (_panel.gameObject.activeSelf != playing) _panel.gameObject.SetActive(playing);
            FitOneLine(_title, 14);
            FitOneLine(_status, 22);
            FitOneLine(_side, 14);
            bool rolling = t.Rolling;
            int shooter = s.Shooter;
            bool myTurn = playing && shooter == place && !rolling;
            _title.text = $"A: {TeamNicks(t, 0)}    {s.WinsA} : {s.WinsB}    B: {TeamNicks(t, 1)}";
            if (playing)
            {
                if (rolling) _status.text = "Kule w ruchu";
                else if (myTurn) _status.text = s.Break ? "Rozbijasz!" : s.BallInHand ? "Twój strzał - biała w ręce" : "Twój strzał";
                else _status.text = $"Strzela {t.SeatNick(shooter)} ({(PoolState.TeamOf(shooter) == team ? "twoja drużyna" : "rywale")})";
                _status.color = myTurn ? Good : UiKit.Text;
            }
            // Under the status: the note of the last shot for a few seconds, else the groups.
            if (!rolling && s.ShotNo != _poolNoteShot)
            {
                _poolNoteShot = s.ShotNo;
                _poolNoteAt = Time.unscaledTime;
            }
            bool note = !rolling && !string.IsNullOrEmpty(s.Note) && Time.unscaledTime - _poolNoteAt < NoteShow;
            bool solo = s.Members(0) == 0 || s.Members(1) == 0; // solo game: no rivals
            _side.text = note ? s.Note : s.Groups == PoolState.Open ? "stół otwarty"
                : solo ? $"grasz: {GroupShort(s, team)}" : $"twoja drużyna: {GroupShort(s, team)}      rywale: {GroupShort(s, other)}";
            PoolFlashes(t, place, team, rolling);
            _seenVersion = t.Version;
            // One controls line, only while it matters, fading out after a few seconds (ball in hand stays).
            string hint = !myTurn ? "" : t.LocalHolding ? "Biała w ręce - LPM: połóż ją na stole" : (s.BallInHand ? "B - weź białą znowu    " : "") + PoolAimHint;
            if (hint != _poolHintKey)
            {
                _poolHintKey = hint;
                _poolHintAt = Time.unscaledTime;
            }
            float age = Time.unscaledTime - _poolHintAt;
            _hints.text = hint;
            var hc = _hints.color;
            hc.a = t.LocalHolding ? 0.92f : Mathf.Clamp01((HintShow - age) / 1.5f) * 0.92f;
            _hints.color = hc;
            if (_promo.activeSelf) _promo.SetActive(false);
        }

        void PoolFlashes(PoolTable t, int place, int team, bool rolling)
        {
            var s = t.State;
            if (_seenVersion == -1)
            {
                _poolRound = s.Round;
                _poolEndFlashed = s.Phase == PoolState.Over ? s.Round : -1;
                _poolShotSeen = s.ShotNo;
            }
            if (rolling) return;
            bool mineNext = PoolState.TeamOf(s.Shooter) == team;
            if (s.Phase == PoolState.Playing && s.Round != _poolRound)
                Flash(s.Shooter == place ? "Rozbijasz!" : $"Rozbija {t.SeatNick(s.Shooter)}", 2f, Good);
            else if (s.Phase == PoolState.Over && _poolEndFlashed != s.Round)
            {
                _poolEndFlashed = s.Round;
                Flash(PoolEnd(s, team), 4.5f, Gold);
            }
            else if (s.Phase == PoolState.Playing && s.ShotNo != _poolShotSeen && s.BallInHand && !s.Break)
            {
                bool solo = s.Members(0) == 0 || s.Members(1) == 0;
                Flash(solo ? "Faul - biała w ręce" : mineNext ? "Faul rywali - biała w ręce" : "Faul - rywale mają białą w ręce", 2.2f, solo ? Bad : mineNext ? Good : Bad);
            }
            _poolRound = s.Round;
            _poolShotSeen = s.ShotNo;
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

        /// <summary>End of a game from the point of view of <paramref name="team"/>.</summary>
        static string PoolEnd(PoolState s, int team)
        {
            bool won = s.Winner == team;
            return s.EndReason switch
            {
                PoolState.EndEight => won ? "Wygrana - ósemka w łuzie!" : "Przegrana - rywale wbili ósemkę",
                PoolState.EndEightEarly => won ? "Rywale wbili ósemkę za wcześnie - wygrana!" : "Ósemka za wcześnie - przegrana",
                PoolState.EndEightScratch => won ? "Ósemka rywali z faulem - wygrana!" : "Ósemka z faulem - przegrana",
                PoolState.EndLeft => won ? "Rywale odeszli - wygrana" : "Partia przerwana",
                _ => s.Winner > 1 ? "Koniec partii" : won ? "Wygrana!" : "Przegrana",
            };
        }

        /// <summary>One line for a spectator next to the pool table.</summary>
        static string PoolWatchLine(PoolTable t)
        {
            var s = t.State;
            string what = s.Phase == PoolState.Over
                ? (s.Winner <= 1 ? $"wygrywa drużyna {(s.Winner == 0 ? "A" : "B")}" : "koniec partii")
                : t.Rolling ? "kule w ruchu" : $"strzela {t.SeatNick(s.Shooter)}";
            return $"Bilard: {TeamNicks(t, 0)} {s.WinsA} : {s.WinsB} {TeamNicks(t, 1)}   |   {what}";
        }
    }
}
