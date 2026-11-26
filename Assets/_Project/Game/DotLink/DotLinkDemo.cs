using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace HideAndSeek.Game.DotLink
{
    /// <summary>
    /// Attract mode of the KABELKI cabinet (local, every peer on its own): a puzzle made off the main thread, then its
    /// solution drawn line by line, a cell every <see cref="Step"/> s, two flashes when it is full, a pause, the next one.
    /// <see cref="Version"/> changes whenever the picture does (the cabinet repaints only then).
    /// </summary>
    public sealed class DotLinkDemo
    {
        const float Step = 0.11f, Hold = 2.6f;

        public DotLinkPuzzle Puzzle { get; private set; }
        public DotLinkBoard Board { get; private set; }
        public bool Lit { get; private set; }
        public int Version { get; private set; }

        Task<DotLinkPuzzle> _task;
        int _color, _len;
        float _t, _done = -1f;
        readonly List<int> _cells = new();

        public void Tick(float dt)
        {
            if (Puzzle == null)
            {
                if (_task == null)
                {
                    int seed = Random.Range(1, int.MaxValue);
                    _task = Task.Run(() => DotLinkGenerator.Generate(seed, 10, 10, 10, 12, out _));
                }
                if (!_task.IsCompleted) return;
                var t = _task;
                _task = null;
                if (t.IsFaulted || t.Result == null || t.Result.Solution == null) return;
                Puzzle = t.Result;
                Board = new DotLinkBoard(Puzzle);
                _color = 0;
                _len = 0;
                _t = 0f;
                _done = -1f;
                Version++;
                return;
            }
            if (_done >= 0f)
            {
                _done += dt;
                bool lit = _done < 0.35f || (_done > 0.55f && _done < 1.05f);
                if (lit != Lit) { Lit = lit; Version++; }
                if (_done > Hold)
                {
                    Puzzle = null; // the next one
                    Lit = false;
                    Version++;
                }
                return;
            }
            _t += dt;
            while (_t >= Step && _done < 0f)
            {
                _t -= Step;
                var path = Puzzle.Solution[_color];
                _len++;
                _cells.Clear();
                for (int i = 0; i < _len && i < path.Length; i++) _cells.Add(path[i]);
                Board.SetPath(_color, _cells);
                if (_len >= path.Length)
                {
                    _color++;
                    _len = 0;
                    if (_color >= Puzzle.Colors) _done = 0f;
                }
                Version++;
            }
        }
    }
}
