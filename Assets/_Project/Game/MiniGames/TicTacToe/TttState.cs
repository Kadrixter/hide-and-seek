using System.Globalization;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// Tic-tac-toe table state as one text line (MatchManager.TttNet). Seat A always plays X, seat B plays O; who
    /// starts alternates every round (<see cref="Starter"/>). Cells: '.' empty, 'X', 'O' (index = row * 3 + column,
    /// row 0 = the far side as seen from seat A).
    /// </summary>
    public sealed class TttState
    {
        public const byte Waiting = 0, Playing = 1, Over = 2;
        public const ulong Nobody = ulong.MaxValue;

        public int Seq;
        public ulong A = Nobody, B = Nobody;
        public byte Phase = Waiting;
        public string Cells = ".........";
        /// <summary>Seat to move (0 = A / X, 1 = B / O).</summary>
        public int Turn;
        /// <summary>Seat that started this round.</summary>
        public int Starter;
        public GameEnd End;
        /// <summary>Winning seat (0 / 1), -1 = draw or nothing yet.</summary>
        public int Winner = -1;
        /// <summary>Winning line index into <see cref="TttRules.Lines"/>, -1 = none.</summary>
        public int Line = -1;
        public int ScoreA, ScoreB, Draws;
        /// <summary>Bit 1: seat A wants another round, bit 2: seat B.</summary>
        public int Rematch;
        /// <summary>Cell of the last mark (for the chalk animation), -1 = none.</summary>
        public int LastCell = -1;
        public int Round;

        public ulong Seat(int i) => i == 0 ? A : B;
        public int SeatOf(ulong id) => id == Nobody ? -1 : id == A ? 0 : id == B ? 1 : -1;
        public static char Mark(int seat) => seat == 0 ? 'X' : 'O';

        public string Encode()
        {
            var c = CultureInfo.InvariantCulture;
            return string.Join(";", Seq.ToString(c), A.ToString(c), B.ToString(c), Phase.ToString(c), Cells, Turn.ToString(c), Starter.ToString(c),
                ((int)End).ToString(c), Winner.ToString(c), Line.ToString(c), ScoreA.ToString(c), ScoreB.ToString(c), Draws.ToString(c),
                Rematch.ToString(c), LastCell.ToString(c), Round.ToString(c));
        }

        public static bool TryDecode(string s, TttState into)
        {
            if (string.IsNullOrEmpty(s)) return false;
            var p = s.Split(';');
            if (p.Length < 16 || p[4].Length != 9) return false;
            var c = CultureInfo.InvariantCulture;
            try
            {
                into.Seq = int.Parse(p[0], c);
                into.A = ulong.Parse(p[1], c);
                into.B = ulong.Parse(p[2], c);
                into.Phase = byte.Parse(p[3], c);
                into.Cells = p[4];
                into.Turn = int.Parse(p[5], c);
                into.Starter = int.Parse(p[6], c);
                into.End = (GameEnd)int.Parse(p[7], c);
                into.Winner = int.Parse(p[8], c);
                into.Line = int.Parse(p[9], c);
                into.ScoreA = int.Parse(p[10], c);
                into.ScoreB = int.Parse(p[11], c);
                into.Draws = int.Parse(p[12], c);
                into.Rematch = int.Parse(p[13], c);
                into.LastCell = int.Parse(p[14], c);
                into.Round = int.Parse(p[15], c);
                return true;
            }
            catch (System.FormatException) { return false; }
            catch (System.OverflowException) { return false; }
        }
    }

    /// <summary>Tic-tac-toe rules: the 8 lines, winner and full-board checks.</summary>
    public static class TttRules
    {
        public static readonly int[][] Lines =
        {
            new[] { 0, 1, 2 }, new[] { 3, 4, 5 }, new[] { 6, 7, 8 }, // rows
            new[] { 0, 3, 6 }, new[] { 1, 4, 7 }, new[] { 2, 5, 8 }, // columns
            new[] { 0, 4, 8 }, new[] { 2, 4, 6 },                    // diagonals
        };

        /// <summary>Index of the completed line, -1 = none.</summary>
        public static int WinLine(string cells)
        {
            for (int i = 0; i < Lines.Length; i++)
            {
                char a = cells[Lines[i][0]];
                if (a != '.' && a == cells[Lines[i][1]] && a == cells[Lines[i][2]]) return i;
            }
            return -1;
        }

        public static bool Full(string cells) => cells.IndexOf('.') < 0;
    }
}
