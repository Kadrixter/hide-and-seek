using UnityEngine;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// Synthesised pool sounds (made once, like <see cref="MiniGameSounds"/>): the bright phenolic click of two balls,
    /// the leather tip on the cue ball, the dull rubber thump of a cushion, the harder knock of a pocket jaw, a ball
    /// dropping through a leather pocket into the net, balls knocking together in the basket, and a loopable low
    /// rumble of balls rolling on cloth.
    /// </summary>
    public static partial class PoolSounds
    {
        const int Rate = 44100;
        static AudioClip _click, _strike, _cushion, _jaw, _drop, _bag, _bagClack, _roll;

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
        static float Sine(float t, float hz) => Mathf.Sin(t * 2f * Mathf.PI * hz);
        static float Att(float t) => Mathf.Clamp01(t * 6000f);

        /// <summary>Ball on ball: very short, bright, with the ring of hard resin.</summary>
        public static AudioClip Click => _click != null ? _click : (_click = Clip("PoolClick", 0.12f, (t, r) =>
            Att(t) * (Noise(r) * Mathf.Exp(-t * 2600f) * 0.55f
            + Sine(t, 3150f) * Mathf.Exp(-t * 85f) * 0.5f
            + Sine(t, 5230f) * Mathf.Exp(-t * 140f) * 0.32f
            + Sine(t, 1720f) * Mathf.Exp(-t * 60f) * 0.2f)));

        /// <summary>Cue tip on the cue ball: a softer tick with a low thump from the stick.</summary>
        public static AudioClip Strike => _strike != null ? _strike : (_strike = Clip("PoolStrike", 0.16f, (t, r) =>
            Att(t) * (Noise(r) * Mathf.Exp(-t * 1600f) * 0.5f
            + Sine(t, 2080f) * Mathf.Exp(-t * 170f) * 0.32f
            + Sine(t, 410f) * Mathf.Exp(-t * 55f) * 0.5f
            + Sine(t, 160f) * Mathf.Exp(-t * 40f) * 0.3f)));

        /// <summary>Cushion: dull rubber thump with a short cloth swish.</summary>
        public static AudioClip Cushion
        {
            get
            {
                if (_cushion != null) return _cushion;
                float lp = 0f;
                _cushion = Clip("PoolCushion", 0.22f, (t, r) =>
                {
                    lp += (Noise(r) - lp) * 0.08f;
                    return Att(t) * (Sine(t, 104f) * Mathf.Exp(-t * 26f) * 0.8f + Sine(t, 215f) * Mathf.Exp(-t * 42f) * 0.4f + lp * Mathf.Exp(-t * 120f) * 1.6f);
                });
                return _cushion;
            }
        }

        /// <summary>Pocket jaw (the cushion facing / the metal casting): a harder, higher knock.</summary>
        public static AudioClip Jaw => _jaw != null ? _jaw : (_jaw = Clip("PoolJaw", 0.18f, (t, r) =>
            Att(t) * (Sine(t, 380f) * Mathf.Exp(-t * 48f) * 0.6f + Sine(t, 150f) * Mathf.Exp(-t * 32f) * 0.5f
            + Sine(t, 1240f) * Mathf.Exp(-t * 110f) * 0.25f + Noise(r) * Mathf.Exp(-t * 900f) * 0.4f)));

        /// <summary>Ball falling through the pocket: knock on the leather, a little bounce, the net taking it.</summary>
        public static AudioClip PocketDrop
        {
            get
            {
                if (_drop != null) return _drop;
                float lp = 0f;
                _drop = Clip("PoolPocket", 0.4f, (t, r) =>
                {
                    lp += (Noise(r) - lp) * 0.05f;
                    float knock = Sine(t, 92f) * Mathf.Exp(-t * 20f) * 0.7f + Sine(t, 265f) * Mathf.Exp(-t * 38f) * 0.35f;
                    float t2 = t - 0.085f;
                    float bounce = t2 > 0f ? (Sine(t2, 120f) * Mathf.Exp(-t2 * 30f) * 0.3f + Sine(t2, 330f) * Mathf.Exp(-t2 * 60f) * 0.15f) : 0f;
                    return Att(t) * (knock + bounce + lp * Mathf.Exp(-t * 14f) * 0.9f);
                });
                return _drop;
            }
        }

        /// <summary>Ball landing in an empty basket: soft leather cup thud and the net rustling.</summary>
        public static AudioClip BagThud
        {
            get
            {
                if (_bag != null) return _bag;
                float lp = 0f;
                _bag = Clip("PoolBag", 0.35f, (t, r) =>
                {
                    lp += (Noise(r) - lp) * 0.12f;
                    return Att(t) * (Sine(t, 78f) * Mathf.Exp(-t * 24f) * 0.6f + lp * Mathf.Exp(-t * 11f) * 0.8f * (0.6f + 0.4f * Mathf.Sin(t * 70f)));
                });
                return _bag;
            }
        }

        /// <summary>Ball landing on balls already in the basket: two or three clacks, quieter each time.</summary>
        public static AudioClip BagClack => _bagClack != null ? _bagClack : (_bagClack = Clip("PoolBagClack", 0.32f, (t, r) =>
        {
            float s = 0f;
            float[] at = { 0f, 0.075f, 0.14f };
            float[] amp = { 1f, 0.45f, 0.2f };
            for (int k = 0; k < at.Length; k++)
            {
                float u = t - at[k];
                if (u < 0f) continue;
                s += amp[k] * Att(u) * (Sine(u, 2900f) * Mathf.Exp(-u * 95f) * 0.5f + Sine(u, 1600f) * Mathf.Exp(-u * 70f) * 0.3f + Noise(r) * Mathf.Exp(-u * 2500f) * 0.4f);
            }
            return s + Sine(t, 85f) * Mathf.Exp(-t * 25f) * 0.25f;
        }));

        /// <summary>Balls rolling on cloth: low filtered noise, slowly wavering (loop, 2 s).</summary>
        public static AudioClip RollLoop
        {
            get
            {
                if (_roll != null) return _roll;
                float lp1 = 0f, lp2 = 0f;
                _roll = Clip("PoolRoll", 2f, (t, r) =>
                {
                    lp1 += (Noise(r) - lp1) * 0.02f;
                    lp2 += (lp1 - lp2) * 0.05f;
                    float wob = 0.75f + 0.25f * Mathf.Sin(t * Mathf.PI * 2f * 3f) * Mathf.Sin(t * Mathf.PI * 2f * 0.5f);
                    float edge = Mathf.Clamp01(t * 25f) * Mathf.Clamp01((2f - t) * 25f);
                    return (lp1 * 0.4f + lp2) * wob * edge;
                });
                return _roll;
            }
        }
    }
}
