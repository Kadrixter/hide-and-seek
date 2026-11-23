using HideAndSeek.Audio;
using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Synthesised extinguisher sounds (made once at runtime): the jet hiss loop (broad noise with a pressure rumble
    /// and a little flutter), the last sputters of an empty one, the safety pin popping out (metal tick + scrape) and
    /// landing, the lever click, the tank clanking on concrete, the wet "pff" of the foam in a face, and coughing fits.
    /// One-shots go through <see cref="GameAudio.PlayClipAt"/> (silent on other floors as usual); the hiss is a loop
    /// on its own source (<see cref="HissSource"/>).
    /// </summary>
    public static class ExtinguisherSounds
    {
        private const int Rate = 44100;
        private static AudioClip _hiss, _sputter, _pin, _pinLand, _click, _splat;
        private static AudioClip[] _clank, _cough;

        public static AudioClip Hiss => _hiss ??= HissClip();

        public static void Sputter(Vector3 at) => GameAudio.PlayClipAt(_sputter ??= SputterClip(), at, 0.8f, 26f);
        public static void Pin(Vector3 at) => GameAudio.PlayClipAt(_pin ??= PinClip(), at, 0.75f, 14f);
        public static void PinLand(Vector3 at) => GameAudio.PlayClipAt(_pinLand ??= TinkClip(77, 3100f, 0.6f), at, 0.5f, 10f);
        public static void Click(Vector3 at) => GameAudio.PlayClipAt(_click ??= TinkClip(5, 1900f, 0.25f), at, 0.45f, 8f);
        public static void Splat(Vector3 at) => GameAudio.PlayClipAt(_splat ??= SplatClip(), at, 0.9f, 18f);

        public static void Clank(Vector3 at, float volume = 1f)
        {
            _clank ??= new[] { ClankClip(1, 1f), ClankClip(2, 0.9f), ClankClip(3, 1.1f) };
            GameAudio.PlayClipAt(_clank[Random.Range(0, _clank.Length)], at, 0.85f * volume, 24f);
        }

        /// <summary>A coughing fit (2-4 coughs) following the seeker's head.</summary>
        public static void Cough(Transform head, int variant)
        {
            _cough ??= new[] { CoughClip(31, 3, 1f), CoughClip(32, 2, 0.92f), CoughClip(33, 4, 1.06f) };
            GameAudio.PlayClipAttached(_cough[Mathf.Abs(variant) % _cough.Length], head, 0.95f, 22f, GameSfx.HiderPain, 0.5f);
        }

        /// <summary>A looping 3D source for the jet hiss (volume driven by the caller).</summary>
        public static AudioSource HissSource(Transform parent)
        {
            var go = new GameObject("ExtHiss");
            go.transform.SetParent(parent, false);
            var s = go.AddComponent<AudioSource>();
            s.clip = Hiss;
            s.loop = true;
            s.playOnAwake = false;
            s.spatialBlend = 1f;
            s.rolloffMode = AudioRolloffMode.Logarithmic;
            s.minDistance = 1.5f;
            s.maxDistance = 30f;
            s.dopplerLevel = 0f;
            s.priority = 40;
            s.volume = 0f;
            var rev = go.AddComponent<AudioReverbFilter>();
            rev.reverbPreset = AudioReverbPreset.ParkingLot;
            rev.dryLevel = 0f;
            return s;
        }

        // ---------------------------------------------------------------- synthesis

        private static AudioClip Make(string name, float[] d, float level = 0.92f)
        {
            float peak = 0.001f;
            foreach (float v in d) peak = Mathf.Max(peak, Mathf.Abs(v));
            for (int i = 0; i < d.Length; i++) d[i] = d[i] / peak * level;
            var clip = AudioClip.Create(name, d.Length, 1, Rate, false);
            clip.SetData(d, 0);
            return clip;
        }

        private static float Noise(System.Random r) => (float)(r.NextDouble() * 2.0 - 1.0);

        /// <summary>2 s seamless hiss: band-limited noise, low pressure rumble, slow flutter; ends crossfaded into the start.</summary>
        private static AudioClip HissClip()
        {
            var r = new System.Random(4242);
            int n = 2 * Rate, fade = Rate / 5;
            var d = new float[n + fade];
            float lp = 0f, lp2 = 0f, hp = 0f, rum = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float w = Noise(r);
                lp += (w - lp) * 0.55f;
                hp = lp - (lp2 += (lp - lp2) * 0.04f);
                rum += (w - rum) * 0.012f;
                float flutter = 1f + 0.08f * Mathf.Sin(t * 2f * Mathf.PI * 17f) + 0.05f * Mathf.Sin(t * 2f * Mathf.PI * 5.3f);
                d[i] = (hp * 0.8f + rum * 2.2f) * flutter;
            }
            var o = new float[n];
            for (int i = 0; i < n; i++) o[i] = d[i];
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                o[i] = d[i] * k + d[n + i] * (1f - k);
            }
            return Make("ExtHiss", o, 0.8f);
        }

        /// <summary>Empty: a few weakening coughs of powder with gaps, the last one tiny.</summary>
        private static AudioClip SputterClip()
        {
            var r = new System.Random(99);
            int n = (int)(1.4f * Rate);
            var d = new float[n];
            float[] at = { 0f, 0.16f, 0.27f, 0.46f, 0.62f, 0.9f, 1.15f };
            float[] amp = { 1f, 0.8f, 0.85f, 0.55f, 0.45f, 0.3f, 0.15f };
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate, env = 0f;
                for (int k = 0; k < at.Length; k++)
                    if (t >= at[k]) env += amp[k] * Mathf.Exp(-(t - at[k]) / 0.05f) * Mathf.Clamp01((t - at[k]) / 0.004f);
                lp += (Noise(r) - lp) * 0.4f;
                d[i] = lp * env;
            }
            return Make("ExtSputter", d);
        }

        private static AudioClip PinClip()
        {
            var r = new System.Random(17);
            int n = (int)(0.5f * Rate);
            var d = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                lp += (Noise(r) - lp) * 0.6f;
                float scrape = t < 0.16f ? lp * 0.35f * (0.5f + 0.5f * Mathf.Sin(t * 900f)) : 0f;
                float tick = t > 0.16f ? (Mathf.Sin(2f * Mathf.PI * 2900f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * 4650f * t) * 0.3f) * Mathf.Exp(-(t - 0.16f) / 0.06f) : 0f;
                d[i] = scrape + tick;
            }
            return Make("ExtPin", d);
        }

        private static AudioClip TinkClip(int seed, float f, float len)
        {
            var r = new System.Random(seed);
            int n = (int)(len * Rate);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                d[i] = (Mathf.Sin(2f * Mathf.PI * f * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * f * 1.62f * t) * 0.3f) * Mathf.Exp(-t / (len * 0.18f))
                       + Noise(r) * Mathf.Exp(-t / 0.003f) * 0.5f;
            }
            return Make("ExtTink" + seed, d);
        }

        /// <summary>Steel tank on concrete: dull thump, hollow ring and a smaller bounce.</summary>
        private static AudioClip ClankClip(int seed, float pitch)
        {
            var r = new System.Random(seed * 31);
            int n = (int)(0.9f * Rate);
            var d = new float[n];
            float[] f = { 210f, 470f, 1130f, 2050f };
            float[] a = { 0.6f, 0.4f, 0.22f, 0.12f };
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate, s = 0f;
                foreach (float t0 in new[] { 0f, 0.21f })
                {
                    if (t < t0) continue;
                    float u = t - t0, g = t0 > 0f ? 0.35f : 1f;
                    for (int k = 0; k < f.Length; k++) s += g * a[k] * Mathf.Sin(2f * Mathf.PI * f[k] * pitch * u + k) * Mathf.Exp(-u / (0.16f - k * 0.03f));
                    lp += (Noise(r) - lp) * 0.3f;
                    s += g * lp * Mathf.Exp(-u / 0.01f);
                }
                d[i] = s;
            }
            return Make("ExtClank" + seed, d);
        }

        private static AudioClip SplatClip()
        {
            var r = new System.Random(55);
            int n = (int)(0.45f * Rate);
            var d = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                lp += (Noise(r) - lp) * Mathf.Lerp(0.5f, 0.08f, t / 0.45f);
                d[i] = lp * Mathf.Exp(-t / 0.12f) * Mathf.Clamp01(t / 0.006f);
            }
            return Make("ExtSplat", d);
        }

        /// <summary>
        /// Coughs: a sharp glottal burst (noise through two vocal formants) falling into a rough rasp, each followed by
        /// a short in-breath; the fit gets a little weaker.
        /// </summary>
        private static AudioClip CoughClip(int seed, int count, float pitch)
        {
            var r = new System.Random(seed);
            float len = 0.12f + count * 0.36f;
            int n = (int)(len * Rate);
            var d = new float[n];
            // two resonators (formants) excited by noise + a low voiced buzz
            float f1 = 620f * pitch, f2 = 1450f * pitch, bw = 0.94f;
            float y1a = 0, y1b = 0, y2a = 0, y2b = 0;
            float c1 = 2f * bw * Mathf.Cos(2f * Mathf.PI * f1 / Rate), c2 = 2f * bw * Mathf.Cos(2f * Mathf.PI * f2 / Rate);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate, env = 0f, breath = 0f;
                for (int k = 0; k < count; k++)
                {
                    float t0 = 0.04f + k * 0.36f + (float)(r.NextDouble() * 0.0) ;
                    float u = t - t0;
                    if (u < 0f) continue;
                    float g = 1f - k * 0.15f;
                    env += g * Mathf.Clamp01(u / 0.008f) * (Mathf.Exp(-u / 0.06f) * 0.9f + Mathf.Exp(-u / 0.18f) * 0.25f) * (u < 0.26f ? 1f : 0f);
                    float ib = u - 0.27f;
                    if (ib > 0f && ib < 0.08f) breath += 0.18f * Mathf.Sin(Mathf.PI * ib / 0.08f);
                }
                float w = Noise(r);
                float buzz = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 115f * pitch * t)) * 0.25f;
                float x = (w + buzz) * env;
                float y1 = x + c1 * y1a - bw * bw * y1b; y1b = y1a; y1a = y1;
                float y2 = x + c2 * y2a - bw * bw * y2b; y2b = y2a; y2a = y2;
                d[i] = y1 * 0.6f + y2 * 0.45f + x * 0.15f + w * breath * 0.3f;
            }
            return Make("ExtCough" + seed, d, 0.85f);
        }
    }
}
