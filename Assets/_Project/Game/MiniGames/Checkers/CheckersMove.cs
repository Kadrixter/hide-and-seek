using System.Collections.Generic;
using System.Text;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// One checkers move: the squares the piece stands on along the way (<see cref="Path"/>: start, every landing square,
    /// end) and the pieces it jumps (<see cref="Captured"/>, in order; they leave the board when the move is complete).
    /// Squares are the chess numbering (a1 = 0, h8 = 63). Text form: the path's square names, e.g. "c3e5c7".
    /// </summary>
    public sealed class CheckersMove
    {
        public readonly List<int> Path = new();
        public readonly List<int> Captured = new();
        /// <summary>A man ends the move on the far row and becomes a king.</summary>
        public bool Promotes;

        public int From => Path[0];
        public int To => Path[Path.Count - 1];
        public bool IsCapture => Captured.Count > 0;

        public CheckersMove Clone()
        {
            var m = new CheckersMove { Promotes = Promotes };
            m.Path.AddRange(Path);
            m.Captured.AddRange(Captured);
            return m;
        }

        public override string ToString()
        {
            var sb = new StringBuilder(Path.Count * 2);
            foreach (int s in Path) sb.Append(ChessBoard.SqName(s));
            return sb.ToString();
        }

        /// <summary>Path squares of a text move ("c3e5c7"); null when malformed.</summary>
        public static List<int> ParsePath(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length % 2 != 0 || text.Length < 4) return null;
            var p = new List<int>(text.Length / 2);
            for (int i = 0; i < text.Length; i += 2)
            {
                int s = ChessBoard.ParseSq(text, i);
                if (s < 0) return null;
                p.Add(s);
            }
            return p;
        }

        /// <summary>Same path as <paramref name="path"/>.</summary>
        public bool SamePath(IList<int> path)
        {
            if (path == null || path.Count != Path.Count) return false;
            for (int i = 0; i < path.Count; i++) if (path[i] != Path[i]) return false;
            return true;
        }

        /// <summary><paramref name="prefix"/> is the start of this move's path (equal counts too).</summary>
        public bool StartsWith(IList<int> prefix)
        {
            if (prefix == null || prefix.Count > Path.Count) return false;
            for (int i = 0; i < prefix.Count; i++) if (prefix[i] != Path[i]) return false;
            return true;
        }
    }
}
