using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>Attract mode while nobody plays: a demo snake that chases food and avoids walls and itself (local only).</summary>
    public sealed class SnakeAttract
    {
        public readonly SnakeView View = new() { State = ArcadeState.Idle };
        private readonly System.Random _rnd = new(5);
        private Vector2Int _dir = Vector2Int.right;
        private float _clock;

        private static readonly Vector2Int[] Dirs = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

        public SnakeAttract() => Restart();

        private void Restart()
        {
            View.Body.Clear();
            for (int i = 0; i < 5; i++) View.Body.Add(new Vector2Int(6 - i, 4));
            _dir = Vector2Int.right;
            View.Score = 0;
            View.Food = new Vector2Int(20, 4);
        }

        public void Tick(float dt)
        {
            _clock += dt;
            if (_clock < 0.11f) return;
            _clock = 0f;
            var head = View.Body[0];
            Vector2Int best = Vector2Int.zero;
            float bestScore = float.MaxValue;
            foreach (var d in Dirs)
            {
                if (d + _dir == Vector2Int.zero) continue;
                var n = head + d;
                if (!Free(n)) continue;
                float s = Mathf.Abs(View.Food.x - n.x) + Mathf.Abs(View.Food.y - n.y) + (d == _dir ? -0.3f : 0f) + (float)_rnd.NextDouble() * 0.6f;
                // keep some room ahead so the demo does not curl into itself
                if (!Free(n + d)) s += 3f;
                if (s < bestScore) { bestScore = s; best = d; }
            }
            if (best == Vector2Int.zero) { Restart(); return; }
            _dir = best;
            var nh = head + _dir;
            View.Body.Insert(0, nh);
            if (nh == View.Food)
            {
                View.Score++;
                for (int tries = 0; tries < 100; tries++)
                {
                    var f = new Vector2Int(_rnd.Next(1, SnakeRules.W - 1), _rnd.Next(1, SnakeRules.H - 1));
                    if (!View.Body.Contains(f)) { View.Food = f; break; }
                }
                if (View.Body.Count > 28) Restart();
            }
            else View.Body.RemoveAt(View.Body.Count - 1);
            View.Dir = System.Array.IndexOf(Dirs, _dir);
        }

        private bool Free(Vector2Int c)
        {
            if (c.x < 0 || c.y < 0 || c.x >= SnakeRules.W || c.y >= SnakeRules.H) return false;
            for (int i = 0; i < View.Body.Count - 1; i++) if (View.Body[i] == c) return false;
            return true;
        }
    }
}
