using HideAndSeek.Game.MiniGames.Pool;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// The lamp over the pool table burns only while somebody plays (a game running or just over with players still
    /// at the table - from the replicated state, so every peer sees the same), like the chess lamp: a short stutter
    /// of the filaments, a quick warm-up, and a slow fade when the players leave.
    /// </summary>
    public sealed partial class PoolTable
    {
        static readonly Vector2[] LampStutter =
        {
            new(0f, 0f), new(0.05f, 0.5f), new(0.09f, 0.06f), new(0.16f, 0.72f), new(0.21f, 0.3f), new(0.3f, 0.85f), new(0.5f, 1f),
        };

        bool _lampOn, _lampStutter;
        float _lampLevel, _lampSince = 99f, _lampApplied = -1f;

        /// <summary>Somebody is playing at the table (the lamp should burn).</summary>
        public bool GameOnTable => _hasState && State.Seated > 0 && State.Phase != PoolState.Waiting;

        /// <summary>Current lamp brightness 0..1 (tests).</summary>
        public float LampLevel => _lampLevel;

        void TickLamp(float dt)
        {
            if (Lamp == null) return;
            bool want = GameOnTable;
            if (want != _lampOn)
            {
                _lampOn = want;
                _lampSince = 0f;
                _lampStutter = want && _lampLevel < 0.35f;
            }
            _lampSince += dt;
            if (_lampOn)
            {
                if (_lampStutter && _lampSince >= 0.5f) _lampStutter = false;
                _lampLevel = _lampStutter ? LampStutterAt(_lampSince) : Mathf.MoveTowards(_lampLevel, 1f, dt / 0.25f);
            }
            else _lampLevel = Mathf.MoveTowards(_lampLevel, 0f, dt / (_lampLevel > 0.3f ? 0.9f : 0.6f));
            if (!Mathf.Approximately(_lampLevel, _lampApplied))
            {
                _lampApplied = _lampLevel;
                Lamp.SetLevel(_lampLevel);
            }
        }

        static float LampStutterAt(float t)
        {
            for (int i = 1; i < LampStutter.Length; i++)
                if (t <= LampStutter[i].x)
                    return t - LampStutter[i - 1].x < (LampStutter[i].x - LampStutter[i - 1].x) * 0.6f ? LampStutter[i - 1].y : LampStutter[i].y;
            return 1f;
        }
    }
}
