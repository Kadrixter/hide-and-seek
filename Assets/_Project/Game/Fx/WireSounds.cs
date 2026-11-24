using HideAndSeek.Audio;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Synthesised sounds of the fence wire (made once at runtime): the mesh rattling when grabbed, the strand creaking
    /// and grinding on every bend, the sharp snap with a ping when it breaks, the antenna popping out of the
    /// transmitter, the thrown antenna clinking on the floor, the wire seating in the socket and being twisted.
    /// Played in 3D through <see cref="GameAudio.PlayClipAt"/>.
    /// </summary>
    public static class WireSounds
    {
        private const int Rate = 44100;
        private static AudioClip _rattle, _snap, _pop, _clink, _seat, _twist;
        private static AudioClip[] _creak;

        /// <summary>Played on this peer (tests).</summary>
        public static int Played;

        public static void Rattle(Vector3 pos, float volume = 1f) => Play(_rattle ??= RattleClip(), pos, 0.55f * volume, 18f);
        public static void Creak(Vector3 pos, float volume = 1f)
        {
            _creak ??= new[] { CreakClip(1, 1f), CreakClip(2, 1.12f), CreakClip(3, 0.9f) };
            Play(_creak[Random.Range(0, _creak.Length)], pos, 0.5f * volume, 14f);
        }
        public static void Snap(Vector3 pos) => Play(_snap ??= SnapClip(), pos, 0.85f, 22f);
        public static void Pop(Vector3 pos) => Play(_pop ??= PopClip(), pos, 0.7f, 12f);
        public static void Clink(Vector3 pos) => Play(_clink ??= ClinkClip(), pos, 0.6f, 16f);
        public static void Seat(Vector3 pos) => Play(_seat ??= SeatClip(), pos, 0.75f, 12f);
        public static void Twist(Vector3 pos) => Play(_twist ??= TwistClip(), pos, 0.5f, 10f);

        private static void Play(AudioClip c, Vector3 pos, float vol, float range)
        {
            Played++;
            GameAudio.PlayClipAt(c, pos, vol, range);
        }

        /// <summary>All clips (tests write them as WAV).</summary>
        public static AudioClip[] AllClips() => new[]
        {
            _rattle ??= RattleClip(), CreakClip(1, 1f), _snap ??= SnapClip(), _pop ??= PopClip(), _clink ??= ClinkClip(), _seat ??= SeatClip(), _twist ??= TwistClip(),
        };

        // ---------------------------------------------------------------- synthesis

        private static AudioClip Make(string name, float[] d)
        {
            float peak = 0.001f;
            foreach (float v in d) peak = Mathf.Max(peak, Mathf.Abs(v));
            for (int i = 0; i < d.Length; i++) d[i] = d[i] / peak * 0.9f;
            var clip = AudioClip.Create(name, d.Length, 1, Rate, false);
            clip.SetData(d, 0);
            return clip;
        }

        /// <summary>Inharmonic ring of thin steel (wire / mesh), sum of decaying partials.</summary>
        private static float Ring(float t, float f0, float tau, float phase = 0f)
        {
            float[] ratio = { 1f, 2.76f, 5.4f, 8.93f };
            float[] amp = { 0.5f, 0.3f, 0.18f, 0.1f };
            float s = 0f;
            for (int k = 0; k < ratio.Length; k++) s += amp[k] * Mathf.Sin(2f * Mathf.PI * f0 * ratio[k] * t + phase + k) * Mathf.Exp(-t / (tau / (1f + k * 0.6f)));
            return s;
        }

        /// <summary>A grabbed wire-mesh panel: many small metal ticks and a low shimmer of the whole panel.</summary>
        private static AudioClip RattleClip()
        {
            var rnd = new System.Random(4401);
            int n = (int)(0.6f * Rate);
            var d = new float[n];
            for (int k = 0; k < 26; k++)
            {
                int at = (int)(Mathf.Pow((float)rnd.NextDouble(), 1.6f) * 0.45f * Rate);
                float f = 1800f + (float)rnd.NextDouble() * 3600f, a = 0.25f + (float)rnd.NextDouble() * 0.5f;
                for (int i = at; i < Mathf.Min(n, at + (int)(0.06f * Rate)); i++)
                {
                    float t = (i - at) / (float)Rate;
                    d[i] += a * Ring(t, f, 0.012f);
                }
            }
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                d[i] += 0.25f * Mathf.Sin(2f * Mathf.PI * 210f * t) * Mathf.Sin(2f * Mathf.PI * 7f * t) * Mathf.Exp(-t / 0.25f);
            }
            return Make("WireRattle", d);
        }

        /// <summary>One bend of the strand: a slow grinding creak (stick-slip squeaks) under a faint ring.</summary>
        private static AudioClip CreakClip(int seed, float pitch)
        {
            var rnd = new System.Random(seed * 131);
            int n = (int)(0.36f * Rate);
            var d = new float[n];
            float phase = 0f, lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.34f));
                // stick-slip: a buzzy tone whose pitch wanders, chopped into grains
                float f = (520f + 260f * Mathf.Sin(t * 9f + seed)) * pitch;
                phase += 2f * Mathf.PI * f / Rate;
                float saw = (phase / Mathf.PI % 2f) - 1f;
                float grain = 0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * (38f + seed * 3f) * t);
                float w = (float)(rnd.NextDouble() * 2.0 - 1.0);
                lp += (w - lp) * 0.2f;
                d[i] = env * (0.45f * saw * grain + 0.35f * lp) + 0.15f * env * Ring(t % 0.09f, 2400f * pitch, 0.03f);
            }
            return Make("WireCreak" + seed, d);
        }

        /// <summary>The strand breaks: a hard click, a bright ping of the freed end, the mesh shivering.</summary>
        private static AudioClip SnapClip()
        {
            var rnd = new System.Random(77);
            int n = (int)(0.7f * Rate);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float w = (float)(rnd.NextDouble() * 2.0 - 1.0);
                d[i] = w * Mathf.Exp(-t / 0.0025f) * 1.2f + 0.8f * Ring(t, 3100f, 0.09f) + 0.4f * Ring(t, 690f, 0.22f) * Mathf.Sin(2f * Mathf.PI * 11f * t);
            }
            return Make("WireSnap", d);
        }

        /// <summary>Chrome telescope pulled out of a plastic socket: a squeak and a hollow pop.</summary>
        private static AudioClip PopClip()
        {
            int n = (int)(0.25f * Rate);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float squeak = t < 0.08f ? Mathf.Sin(2f * Mathf.PI * (900f + 4000f * t) * t) * 0.3f * Mathf.Sin(Mathf.PI * t / 0.08f) : 0f;
                float tp = t - 0.085f;
                float pop = tp > 0f ? Mathf.Sin(2f * Mathf.PI * (420f - 900f * tp) * tp) * Mathf.Exp(-tp / 0.025f) : 0f;
                d[i] = squeak + pop + (tp > 0f ? 0.4f * Ring(tp, 2600f, 0.05f) : 0f);
            }
            return Make("RcAntennaPop", d);
        }

        /// <summary>The thrown antenna lands: two light chrome clinks.</summary>
        private static AudioClip ClinkClip()
        {
            int n = (int)(0.5f * Rate);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate, t2 = t - 0.11f;
                d[i] = Ring(t, 2900f, 0.08f) + (t2 > 0f ? 0.6f * Ring(t2, 3300f, 0.06f, 1f) : 0f);
            }
            return Make("RcAntennaClink", d);
        }

        /// <summary>The wire pushed home into the socket: a scrape and a click.</summary>
        private static AudioClip SeatClip()
        {
            var rnd = new System.Random(5);
            int n = (int)(0.3f * Rate);
            var d = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float w = (float)(rnd.NextDouble() * 2.0 - 1.0);
                lp += (w - lp) * 0.5f;
                float scrape = t < 0.18f ? lp * 0.35f * Mathf.Sin(Mathf.PI * t / 0.18f) : 0f;
                float tc = t - 0.19f;
                float click = tc > 0f ? (w * Mathf.Exp(-tc / 0.002f) + 0.6f * Ring(tc, 1900f, 0.03f)) : 0f;
                d[i] = scrape + click;
            }
            return Make("RcWireSeat", d);
        }

        /// <summary>Twisting the wire round the socket: a short ratcheting grind.</summary>
        private static AudioClip TwistClip()
        {
            var rnd = new System.Random(9);
            int n = (int)(0.32f * Rate);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float w = (float)(rnd.NextDouble() * 2.0 - 1.0);
                float tick = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(2f * Mathf.PI * 26f * t)), 18f);
                d[i] = (w * 0.5f + Ring(t % (1f / 26f), 3600f, 0.01f)) * tick * Mathf.Sin(Mathf.PI * t / 0.32f);
            }
            return Make("RcWireTwist", d);
        }
    }
}
