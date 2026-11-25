using UnityEngine;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// Synthesised sounds of the board games (made once): a boxwood piece knocking on the board (short noise click,
    /// two damped wood resonances and a low thud), a light lift tick, a dull "no" knock, a soft chime for check and
    /// game end chords, the chess clock plunger, chalk scratching (loop) and an eraser swish.
    /// </summary>
    public static class MiniGameSounds
    {
        const int Rate = 44100;
        static AudioClip _knock, _tick, _deny, _check, _win, _draw, _clockBtn, _chalk, _erase;

        static AudioClip Clip(string name, float seconds, System.Func<float, int, float> f)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            var rnd = new System.Random(name.GetHashCode());
            for (int i = 0; i < n; i++) data[i] = f(i / (float)Rate, rnd.Next());
            float peak = 0f;
            foreach (float v in data) peak = Mathf.Max(peak, Mathf.Abs(v));
            if (peak > 0.0001f) for (int i = 0; i < n; i++) data[i] *= 0.9f / peak;
            var c = AudioClip.Create(name, n, 1, Rate, false);
            c.SetData(data, 0);
            return c;
        }

        static float Noise(int r) => (r & 0xFFFF) / 32768f - 1f;

        static AudioClip KnockClip => _knock != null ? _knock : (_knock = Clip("BoardKnock", 0.16f, (t, r) =>
            Noise(r) * Mathf.Exp(-t * 900f) * 0.7f
            + Mathf.Sin(t * 2f * Mathf.PI * 1180f) * Mathf.Exp(-t * 55f) * 0.55f
            + Mathf.Sin(t * 2f * Mathf.PI * 2480f) * Mathf.Exp(-t * 90f) * 0.3f
            + Mathf.Sin(t * 2f * Mathf.PI * 190f) * Mathf.Exp(-t * 38f) * 0.45f));

        static AudioClip TickClip => _tick != null ? _tick : (_tick = Clip("BoardTick", 0.06f, (t, r) =>
            Noise(r) * Mathf.Exp(-t * 1500f) * 0.5f + Mathf.Sin(t * 2f * Mathf.PI * 3100f) * Mathf.Exp(-t * 160f) * 0.4f));

        static AudioClip DenyClip => _deny != null ? _deny : (_deny = Clip("BoardDeny", 0.14f, (t, r) =>
            Mathf.Sin(t * 2f * Mathf.PI * 140f) * Mathf.Exp(-t * 30f) * 0.8f + Noise(r) * Mathf.Exp(-t * 600f) * 0.3f));

        static float Bell(float t, float f, float decay) =>
            (Mathf.Sin(t * 2f * Mathf.PI * f) + 0.35f * Mathf.Sin(t * 2f * Mathf.PI * f * 2.76f) * Mathf.Exp(-t * 6f)) * Mathf.Exp(-t * decay) * Mathf.Clamp01(t * 400f);

        static AudioClip CheckClip => _check != null ? _check : (_check = Clip("BoardCheck", 0.9f, (t, r) =>
            Bell(t, 988f, 5f) * 0.6f + (t > 0.12f ? Bell(t - 0.12f, 1319f, 5f) * 0.5f : 0f)));

        static AudioClip WinClip => _win != null ? _win : (_win = Clip("BoardWin", 1.6f, (t, r) =>
            Bell(t, 523f, 3f) * 0.5f + (t > 0.14f ? Bell(t - 0.14f, 659f, 3f) * 0.5f : 0f)
            + (t > 0.28f ? Bell(t - 0.28f, 784f, 3f) * 0.5f : 0f) + (t > 0.42f ? Bell(t - 0.42f, 1047f, 2.5f) * 0.6f : 0f)));

        static AudioClip DrawClip => _draw != null ? _draw : (_draw = Clip("BoardDraw", 1.2f, (t, r) =>
            Bell(t, 659f, 3.5f) * 0.5f + (t > 0.18f ? Bell(t - 0.18f, 587f, 3.5f) * 0.5f : 0f)));

        static AudioClip ClockClip => _clockBtn != null ? _clockBtn : (_clockBtn = Clip("ClockButton", 0.12f, (t, r) =>
            Noise(r) * Mathf.Exp(-t * 700f) * 0.6f + Mathf.Sin(t * 2f * Mathf.PI * 1650f) * Mathf.Exp(-t * 120f) * 0.4f
            + (t > 0.03f ? Mathf.Sin((t - 0.03f) * 2f * Mathf.PI * 900f) * Mathf.Exp(-(t - 0.03f) * 140f) * 0.3f : 0f)));

        /// <summary>Chalk on slate: band-limited scratchy noise with a rough stroke rhythm (loopable, 1.2 s).</summary>
        public static AudioClip ChalkLoop
        {
            get
            {
                if (_chalk != null) return _chalk;
                float lp = 0f, hp = 0f, prev = 0f;
                _chalk = Clip("ChalkScratch", 1.2f, (t, r) =>
                {
                    float x = Noise(r);
                    lp += (x - lp) * 0.45f;           // low-pass ~6 kHz
                    float band = lp - prev; prev = lp; // high-pass
                    hp = band;
                    float grain = 0.55f + 0.45f * Mathf.PerlinNoise(t * 60f, 0.3f);
                    float env = Mathf.Clamp01(t * 30f) * Mathf.Clamp01((1.2f - t) * 30f);
                    return hp * grain * env * (0.8f + 0.2f * Mathf.Sin(t * 2f * Mathf.PI * 7f));
                });
                return _chalk;
            }
        }

        static AudioClip EraseClip => _erase != null ? _erase : (_erase = Clip("Eraser", 1.0f, (t, r) =>
        {
            float sweep = Mathf.Abs(Mathf.Sin(t * Mathf.PI * 4f));
            return Noise(r) * 0.35f * sweep * Mathf.Clamp01((1f - t) * 4f);
        }));

        public static void Knock(MiniTable t, float volume, float pitch) => t?.PlaySfx(KnockClip, 0.55f * volume, pitch);
        public static void Select(MiniTable t) => t?.PlaySfx(TickClip, 0.35f, Random.Range(0.95f, 1.05f));
        public static void Deny(MiniTable t) => t?.PlaySfx(DenyClip, 0.4f, 1f);
        public static void Check(MiniTable t) => t?.PlaySfx(CheckClip, 0.45f, 1f);
        public static void ClockButton(MiniTable t) => t?.PlaySfx(ClockClip, 0.5f, 1f);
        public static void Erase(MiniTable t) => t?.PlaySfx(EraseClip, 0.5f, 1f);
        public static void Win(MiniTable t) => t?.PlaySfx(WinClip, 0.5f, 1f);
        public static void Draw(MiniTable t) => t?.PlaySfx(DrawClip, 0.45f, 1f);

        /// <summary>Start (clock click) or end of a game (chord: a win / a draw).</summary>
        public static void PhaseChange(MiniTable t, byte from, byte to, GameEnd end)
        {
            if (to == 1 && from != 1) ClockButton(t);
            else if (to == 2 && from == 1)
            {
                bool draw = end is GameEnd.Stalemate or GameEnd.FiftyMoves or GameEnd.Repetition or GameEnd.Material or GameEnd.Full or GameEnd.KingMoves;
                if (draw) Draw(t); else Win(t);
            }
        }
    }
}
