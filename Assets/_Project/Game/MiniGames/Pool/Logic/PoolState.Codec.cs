using System.Globalization;
using System.Text;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames.Pool
{
    /// <summary>
    /// Text form of <see cref="PoolState"/> for a FixedString512Bytes network variable. ASCII fields split by '|':
    /// "P2|phase|A|B|A2|B2|turn|up|groups|flags|winner|end|winsA|winsB|round|shot|ready|down|positions|note".
    /// Ids in hex ("-" = nobody), Down in hex, positions = 32 coordinates x 3 chars of base64 (18 bits on the 0.05 mm
    /// grid), the note last (UTF-8, cut to <see cref="NoteMaxChars"/> chars and to the byte budget). At most ~200
    /// bytes before the note, 478 bytes in all.
    /// </summary>
    public sealed partial class PoolState
    {
        const string Tag = "P2";
        const int Fields = 20, MaxBytes = 478, PosChars = 96, QOffset = 131072;
        /// <summary>Longest note kept in the network text (characters).</summary>
        public const int NoteMaxChars = 90;
        const string B64 = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";
        static readonly int[] Rev = BuildRev();

        static int[] BuildRev()
        {
            var r = new int[128];
            for (int i = 0; i < r.Length; i++) r[i] = -1;
            for (int i = 0; i < B64.Length; i++) r[B64[i]] = i;
            return r;
        }

        static string Inv(int x) => x.ToString(CultureInfo.InvariantCulture);
        static string Id(ulong id) => id == Nobody ? "-" : id.ToString("X", CultureInfo.InvariantCulture);

        public string Encode()
        {
            var sb = new StringBuilder(MaxBytes);
            sb.Append(Tag).Append('|').Append(Inv(Phase)).Append('|').Append(Id(A)).Append('|').Append(Id(B)).Append('|')
                .Append(Id(A2)).Append('|').Append(Id(B2)).Append('|')
                .Append(Inv(Turn)).Append('|').Append(Inv(Up)).Append('|').Append(Inv(Groups)).Append('|')
                .Append(Inv((BallInHand ? 1 : 0) | (Kitchen ? 2 : 0) | (Break ? 4 : 0))).Append('|')
                .Append(Inv(Winner)).Append('|').Append(Inv(EndReason)).Append('|').Append(Inv(WinsA)).Append('|')
                .Append(Inv(WinsB)).Append('|').Append(Inv(Round)).Append('|').Append(Inv(ShotNo)).Append('|')
                .Append(Inv(Ready)).Append('|').Append(Down.ToString("X", CultureInfo.InvariantCulture)).Append('|');
            for (int i = 0; i < 16; i++)
            {
                Coord(sb, Pos[i].x);
                Coord(sb, Pos[i].y);
            }
            sb.Append('|');
            sb.Append(CleanNote(Note, MaxBytes - sb.Length));
            return sb.ToString();
        }

        static void Coord(StringBuilder sb, float x)
        {
            int k = Mathf.Clamp(QInt(x) + QOffset, 0, (1 << 18) - 1);
            sb.Append(B64[(k >> 12) & 63]).Append(B64[(k >> 6) & 63]).Append(B64[k & 63]);
        }

        /// <summary>
        /// A note fit for the network text: no '|' or control chars, at most <see cref="NoteMaxChars"/> chars and
        /// <paramref name="maxBytes"/> UTF-8 bytes (a Polish letter = 2 bytes), never cutting a surrogate pair.
        /// </summary>
        public static string CleanNote(string n, int maxBytes)
        {
            if (string.IsNullOrEmpty(n) || maxBytes <= 0) return "";
            var sb = new StringBuilder(Mathf.Min(n.Length, NoteMaxChars * 2));
            int bytes = 0, chars = 0;
            for (int i = 0; i < n.Length && chars < NoteMaxChars; i++)
            {
                char c = n[i];
                if (char.IsHighSurrogate(c))
                {
                    if (i + 1 >= n.Length || !char.IsLowSurrogate(n[i + 1])) continue;
                    if (bytes + 4 > maxBytes) break;
                    sb.Append(c).Append(n[i + 1]);
                    i++;
                    bytes += 4;
                    chars++;
                    continue;
                }
                if (char.IsLowSurrogate(c)) continue;
                if (c == '|') c = '/';
                else if (c < ' ' || c == '\u007f') c = ' ';
                int len = c < 0x80 ? 1 : c < 0x800 ? 2 : 3;
                if (bytes + len > maxBytes) break;
                sb.Append(c);
                bytes += len;
                chars++;
            }
            return sb.ToString();
        }

        /// <summary>Reads <see cref="Encode"/>'s text into <paramref name="into"/>; false (and <paramref name="into"/> untouched) on anything malformed.</summary>
        public static bool TryDecode(string s, PoolState into)
        {
            if (into == null || string.IsNullOrEmpty(s) || s.Length > 1024) return false;
            string[] f = s.Split(new[] { '|' }, Fields);
            if (f.Length != Fields || f[0] != Tag) return false;
            if (!Byte(f[1], 2, out byte phase) || !Ulong(f[2], out ulong a) || !Ulong(f[3], out ulong b)) return false;
            if (!Ulong(f[4], out ulong a2) || !Ulong(f[5], out ulong b2)) return false;
            if (!Byte(f[6], 1, out byte turn) || !Byte(f[7], 3, out byte up) || !Byte(f[8], 2, out byte groups) || !Byte(f[9], 7, out byte flags)) return false;
            if (!Byte(f[10], 255, out byte winner) || (winner > 1 && winner != 255)) return false;
            if (!Byte(f[11], EndLeft, out byte end) || !Byte(f[12], 255, out byte winsA) || !Byte(f[13], 255, out byte winsB)) return false;
            if (!int.TryParse(f[14], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int round)) return false;
            if (!int.TryParse(f[15], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int shot)) return false;
            if (!Byte(f[16], 15, out byte ready)) return false;
            if (f[17].Length < 1 || f[17].Length > 4 ||
                !ushort.TryParse(f[17], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ushort down)) return false;
            string ps = f[18];
            if (ps.Length != PosChars) return false;
            var pos = new Vector2[16];
            for (int i = 0; i < 32; i++)
            {
                int k = 0;
                for (int c = 0; c < 3; c++)
                {
                    char ch = ps[i * 3 + c];
                    int v = ch < 128 ? Rev[ch] : -1;
                    if (v < 0) return false;
                    k = (k << 6) | v;
                }
                float x = (k - QOffset) * QStep;
                if (i % 2 == 0) pos[i / 2].x = x;
                else pos[i / 2].y = x;
            }
            into.Phase = phase;
            into.A = a;
            into.B = b;
            into.A2 = a2;
            into.B2 = b2;
            into.Turn = turn;
            into.Up = up;
            into.Groups = groups;
            into.BallInHand = (flags & 1) != 0;
            into.Kitchen = (flags & 2) != 0;
            into.Break = (flags & 4) != 0;
            into.Winner = winner;
            into.EndReason = end;
            into.WinsA = winsA;
            into.WinsB = winsB;
            into.Round = round;
            into.ShotNo = shot;
            into.Ready = ready;
            into.Down = down;
            System.Array.Copy(pos, into.Pos, 16);
            into.Note = f[19];
            return true;
        }

        static bool Byte(string s, int max, out byte v)
        {
            v = 0;
            if (string.IsNullOrEmpty(s) || s.Length > 3) return false;
            if (!int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out int x) || x < 0 || x > max) return false;
            v = (byte)x;
            return true;
        }

        static bool Ulong(string s, out ulong v)
        {
            v = Nobody;
            if (s == "-") return true;
            return !string.IsNullOrEmpty(s) && s.Length <= 16 &&
                   ulong.TryParse(s, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out v);
        }
    }
}
