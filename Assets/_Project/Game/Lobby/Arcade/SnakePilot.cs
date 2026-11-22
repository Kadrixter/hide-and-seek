using System.Collections.Generic;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>Test autopilot for SNAKE (-testsnake): shortest path to the bonus / food, otherwise the roomiest safe move.</summary>
    public static class SnakePilot
    {
        private static readonly Vector2Int[] Dirs = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

        /// <summary>Direction index 0..3 to press now, or -1 to keep going.</summary>
        public static int Next(List<Vector2Int> body, Vector2Int food, bool hasBonus, Vector2Int bonus)
        {
            if (body.Count < 2) return -1;
            var head = body[0];
            var cur = head - body[1];
            var blocked = new HashSet<Vector2Int>(body);
            blocked.Remove(body[^1]); // the tail moves away this step
            int first = Bfs(head, hasBonus ? bonus : food, blocked);
            if (first < 0 && hasBonus) first = Bfs(head, food, blocked);
            if (first >= 0 && Room(head + Dirs[first], blocked) < body.Count) first = -1; // path into a dead end
            if (first < 0)
            {
                int bestRoom = -1;
                for (int d = 0; d < 4; d++)
                {
                    var n = head + Dirs[d];
                    if (!Free(n, blocked)) continue;
                    int room = Room(n, blocked);
                    if (room > bestRoom) { bestRoom = room; first = d; }
                }
            }
            if (first < 0 || Dirs[first] == cur) return -1;
            return first;
        }

        private static bool Free(Vector2Int c, HashSet<Vector2Int> blocked)
            => c.x >= 0 && c.y >= 0 && c.x < SnakeRules.W && c.y < SnakeRules.H && !blocked.Contains(c);

        private static int Bfs(Vector2Int from, Vector2Int to, HashSet<Vector2Int> blocked)
        {
            var firstDir = new Dictionary<Vector2Int, int> { [from] = -1 };
            var q = new Queue<Vector2Int>();
            q.Enqueue(from);
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                if (c == to) return firstDir[c];
                for (int d = 0; d < 4; d++)
                {
                    var n = c + Dirs[d];
                    if (!Free(n, blocked) || firstDir.ContainsKey(n)) continue;
                    firstDir[n] = c == from ? d : firstDir[c];
                    q.Enqueue(n);
                }
            }
            return -1;
        }

        private static int Room(Vector2Int from, HashSet<Vector2Int> blocked)
        {
            if (!Free(from, blocked)) return 0;
            var seen = new HashSet<Vector2Int> { from };
            var q = new Queue<Vector2Int>();
            q.Enqueue(from);
            while (q.Count > 0 && seen.Count < 400)
            {
                var c = q.Dequeue();
                foreach (var d in Dirs)
                {
                    var n = c + d;
                    if (Free(n, blocked) && seen.Add(n)) q.Enqueue(n);
                }
            }
            return seen.Count;
        }
    }
}
