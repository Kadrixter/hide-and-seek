using System.Globalization;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>Why a game ended.</summary>
    public enum GameEnd : byte
    {
        None, Checkmate, Stalemate, FiftyMoves, Repetition, Material, Resign, Timeout, Left, Aborted, Line, Full,
    }

    /// <summary>
    /// Everything the chess table shows, as one text line in a network variable (MatchManager.ChessNet), so a late
    /// joiner gets the whole board at once and every change is a single update. Seat A / B are the two chairs of the
    /// table; <see cref="WhiteIsA"/> says who has white (swapped on every rematch). <see cref="Last"/> is the move that
    /// led to <see cref="Fen"/> (clients animate it when <see cref="Seq"/> grew by one).
    /// </summary>
    public sealed class ChessState
    {
        public const byte Waiting = 0, Playing = 1, Over = 2;
        public const ulong Nobody = ulong.MaxValue;

        public int Seq;
        public ulong A = Nobody, B = Nobody;
        public bool WhiteIsA = true;
        public byte Phase = Waiting;
        public string Fen = ChessBoard.StartFen;
        public string Last = "-";
        public GameEnd End;
        /// <summary>+1 white won, -1 black won, 0 draw / nothing yet.</summary>
        public int Winner;
        /// <summary>Clock: base minutes (0 = no clock) and increment seconds.</summary>
        public int ClockMin = 5, ClockInc = 3;
        /// <summary>Milliseconds left for white / black when the current turn started.</summary>
        public int WhiteMs, BlackMs;
        /// <summary>Server time the current turn started (clock running for the side to move).</summary>
        public double TurnStart;
        /// <summary>Bit 1: seat A wants a rematch, bit 2: seat B.</summary>
        public int Rematch;
        /// <summary>Half-moves played in this game.</summary>
        public int Ply;
        /// <summary>Game number at this table (rematches count up).</summary>
        public int Game;

        public bool HasClock => ClockMin > 0;
        public ulong White => WhiteIsA ? A : B;
        public ulong Black => WhiteIsA ? B : A;
        public ulong Seat(int i) => i == 0 ? A : B;
        public int SeatOf(ulong id) => id == Nobody ? -1 : id == A ? 0 : id == B ? 1 : -1;
        /// <summary>Colour played from a seat (+1 white / -1 black).</summary>
        public int ColourOfSeat(int seat) => (seat == 0) == WhiteIsA ? ChessBoard.White : ChessBoard.Black;
        public ulong PlayerOf(int colour) => colour > 0 ? White : Black;

        public string Encode()
        {
            var c = CultureInfo.InvariantCulture;
            return string.Join(";", Seq.ToString(c), A.ToString(c), B.ToString(c), WhiteIsA ? "1" : "0", Phase.ToString(c), Fen, Last,
                ((int)End).ToString(c), Winner.ToString(c), ClockMin.ToString(c), ClockInc.ToString(c), WhiteMs.ToString(c), BlackMs.ToString(c),
                TurnStart.ToString("0.###", c), Rematch.ToString(c), Ply.ToString(c), Game.ToString(c));
        }

        public static bool TryDecode(string s, ChessState into)
        {
            if (string.IsNullOrEmpty(s)) return false;
            var p = s.Split(';');
            if (p.Length < 17) return false;
            var c = CultureInfo.InvariantCulture;
            try
            {
                into.Seq = int.Parse(p[0], c);
                into.A = ulong.Parse(p[1], c);
                into.B = ulong.Parse(p[2], c);
                into.WhiteIsA = p[3] == "1";
                into.Phase = byte.Parse(p[4], c);
                into.Fen = p[5];
                into.Last = p[6];
                into.End = (GameEnd)int.Parse(p[7], c);
                into.Winner = int.Parse(p[8], c);
                into.ClockMin = int.Parse(p[9], c);
                into.ClockInc = int.Parse(p[10], c);
                into.WhiteMs = int.Parse(p[11], c);
                into.BlackMs = int.Parse(p[12], c);
                into.TurnStart = double.Parse(p[13], c);
                into.Rematch = int.Parse(p[14], c);
                into.Ply = int.Parse(p[15], c);
                into.Game = int.Parse(p[16], c);
                return true;
            }
            catch (System.FormatException) { return false; }
            catch (System.OverflowException) { return false; }
        }

        public void CopyTo(ChessState o) => TryDecode(Encode(), o);

        /// <summary>Clock options cycled with a key while waiting (minutes, increment).</summary>
        public static readonly (int min, int inc)[] ClockOptions = { (5, 3), (10, 5), (3, 2), (0, 0) };

        public static string ClockName(int min, int inc) => min <= 0 ? "bez zegara" : $"{min}+{inc}";

        /// <summary>Milliseconds left on a clock right now (<paramref name="now"/> = server time).</summary>
        public int MsLeft(int colour, int sideToMove, double now)
        {
            int ms = colour > 0 ? WhiteMs : BlackMs;
            if (Phase == Playing && colour == sideToMove && HasClock) ms -= (int)((now - TurnStart) * 1000.0);
            return ms < 0 ? 0 : ms;
        }
    }
}
