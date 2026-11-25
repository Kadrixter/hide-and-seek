using UnityEngine;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// The pendant lamp over the chess table on every peer: dark while the table is free, it comes on when a game is
    /// on the board (<see cref="ChessTable.GameOnBoard"/>, from the replicated table state, so everybody sees the
    /// same) - a short stutter of the filament, then a quick warm-up - and fades out slowly (the filament glows orange
    /// for a moment) when the players leave. One spot light with soft shadows, disabled while dark.
    /// </summary>
    public sealed class ChessLamp : MonoBehaviour
    {
        /// <summary>Full intensity of the spot light (warm pool of light on the table, the room stays as it is).</summary>
        public const float Intensity = 1.65f;
        // Switch-on stutter: (time s, level) keys over the first 0.5 s.
        static readonly Vector2[] Stutter =
        {
            new(0f, 0f), new(0.05f, 0.5f), new(0.09f, 0.06f), new(0.16f, 0.72f), new(0.21f, 0.3f), new(0.3f, 0.85f), new(0.5f, 1f),
        };

        ChessTable _table;
        ChessLampModel _model;
        bool _on, _stutter;
        float _level, _since = 99f, _applied = -1f;

        /// <summary>Current brightness 0..1 (tests).</summary>
        public float Level => _level;
        public ChessLampModel Model => _model;

        public static ChessLamp Create(ChessTable table, Material template, float ceilingY)
        {
            var model = ChessLampModel.Build(table.transform, template, ceilingY);
            var lamp = model.Root.gameObject.AddComponent<ChessLamp>();
            lamp._table = table;
            lamp._model = model;
            lamp.Apply(0f);
            return lamp;
        }

        void Update()
        {
            if (_table == null) return;
            bool want = _table.GameOnBoard;
            float dt = Time.deltaTime;
            if (want != _on)
            {
                _on = want;
                _since = 0f;
                _stutter = want && _level < 0.35f;
            }
            _since += dt;
            if (_on)
            {
                // Coming from dark: the stutter; from a half-faded state: straight back up.
                if (_stutter && _since >= 0.5f) _stutter = false;
                _level = _stutter ? StutterAt(_since) : Mathf.MoveTowards(_level, 1f, dt / 0.25f);
            }
            else _level = Mathf.MoveTowards(_level, 0f, dt / (_level > 0.3f ? 0.9f : 0.6f));
            if (!Mathf.Approximately(_level, _applied)) Apply(_level);
        }

        static float StutterAt(float t)
        {
            for (int i = 1; i < Stutter.Length; i++)
                if (t <= Stutter[i].x)
                {
                    // Steps, not ramps: a filament catching flicks on and off.
                    return t - Stutter[i - 1].x < (Stutter[i].x - Stutter[i - 1].x) * 0.6f ? Stutter[i - 1].y : Stutter[i].y;
                }
            return 1f;
        }

        /// <summary>Light, bulb and shade glow for a brightness 0..1 (the bulb turns orange as it dims).</summary>
        void Apply(float level)
        {
            _applied = level;
            var l = _model.Light;
            l.intensity = Intensity * level;
            l.enabled = level > 0.002f;
            float warm = Mathf.Clamp01(level * 1.4f);
            var tint = Color.Lerp(new Color(1f, 0.38f, 0.1f), new Color(1f, 0.82f, 0.56f), warm);
            l.color = Color.Lerp(new Color(1f, 0.6f, 0.35f), new Color(1f, 0.82f, 0.6f), warm);
            Glow(_model.Bulb, tint * (1.7f * level));
            Glow(_model.Filament, tint * (4f * Mathf.Sqrt(level)));
            Glow(_model.Inner, new Color(1f, 0.84f, 0.62f) * (1.25f * level));
            _model.Bulb.color = Color.Lerp(new Color(0.85f, 0.83f, 0.78f), new Color(1f, 0.95f, 0.85f), level);
        }

        static void Glow(Material m, Color c)
        {
            if (m == null) return;
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            m.SetColor("_EmissionColor", c);
        }
    }
}
