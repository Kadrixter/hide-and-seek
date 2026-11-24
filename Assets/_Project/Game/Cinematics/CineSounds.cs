using HideAndSeek.Audio;
using UnityEngine;

namespace HideAndSeek.Game.Cinematics
{
    /// <summary>
    /// Sounds of the round intro / outro. Recorded (ElevenLabs, Audio/Resources/Cinematics): the killer's laugh
    /// (two takes) and the shutter slam with the garage echo. Synthesised once at runtime: the low drone under the
    /// intro, the lever clack with a spark, the handle tearing off (groan, crack, crackle), the rattle of the falling slats, the lamp buzz and the award card ticks;
    /// the outro breaker + tube hum is recorded too (outro_lights_on, synthesised fallback). Cinematic sounds are 2D (everybody watches the same shot).
    /// </summary>
    public static class CineSounds
    {
        public const int Rate = 44100;
        /// <summary>Time of the impact inside intro_gate_slam.wav.</summary>
        public const float SlamHitInClip = 0.30f;
        /// <summary>Time of the crack (the rod giving) inside the synthesised snap.</summary>
        public const float SnapCrackInClip = 0.22f;

        static AudioClip _drone, _clack, _snap, _rattle, _buzz, _breaker, _tick, _whoosh;

        public static AudioClip Laugh(int variant) =>
            Resources.Load<AudioClip>(variant % 2 == 0 ? "Cinematics/intro_laugh_a" : "Cinematics/intro_laugh_b");
        public static AudioClip Slam => Resources.Load<AudioClip>("Cinematics/intro_gate_slam");
        public static AudioClip Drone => _drone ??= Make("CineDrone", DroneData(9f));
        public static AudioClip Clack => _clack ??= Make("CineClack", ClackData());
        /// <summary>The lever handle torn off: metal groaning under the yank, a sharp crack, the stub crackling.</summary>
        public static AudioClip Snap => _snap ??= Make("CineSnap", SnapData());
        public static AudioClip Rattle => _rattle ??= Make("CineRattle", RattleData(1.05f));
        public static AudioClip Buzz => _buzz ??= Make("CineBuzz", BuzzData(0.5f));
        public static AudioClip Breaker => Resources.Load<AudioClip>("Cinematics/outro_lights_on") is AudioClip c ? c : _breaker ??= Make("CineBreaker", BreakerData());
        public static AudioClip Tick => _tick ??= Make("CineTick", TickData());
        public static AudioClip Whoosh => _whoosh ??= Make("CineWhoosh", WhooshData(0.5f));

        public static AudioSource Play(AudioClip c, float volume)
        {
            if (c == null) return null;
            return GameAudio.PlayClip2D(c, volume, GameSfx.RoundStart);
        }

        // ================================================================ synthesis

        static uint _rng = 0x1234567u;
        static float Rnd()
        {
            _rng ^= _rng << 13; _rng ^= _rng >> 17; _rng ^= _rng << 5;
            return (_rng & 0xFFFFFF) / (float)0x1000000 * 2f - 1f;
        }

        static AudioClip Make(string name, float[] d)
        {
            float peak = 0.001f;
            foreach (float v in d) peak = Mathf.Max(peak, Mathf.Abs(v));
            int fade = Mathf.Min(d.Length / 4, Rate / 100);
            for (int i = 0; i < d.Length; i++)
            {
                float k = 0.9f / peak;
                int fromEnd = d.Length - 1 - i;
                if (fromEnd < fade) k *= fromEnd / (float)fade;
                if (i < 64) k *= i / 64f;
                d[i] *= k;
            }
            var clip = AudioClip.Create(name, d.Length, 1, Rate, false);
            clip.SetData(d, 0);
            return clip;
        }

        /// <summary>Low, uneasy drone: two detuned low sines and filtered noise, swelling up to the slam.</summary>
        static float[] DroneData(float len)
        {
            var d = new float[(int)(len * Rate)];
            float lp = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Clamp01(t / 1.5f) * Mathf.Lerp(0.55f, 1f, Mathf.Clamp01((t - 3f) / 3.5f)) * Mathf.Clamp01((len - t) / 1.2f);
                lp += (Rnd() - lp) * 0.012f;
                float s = Mathf.Sin(t * 2f * Mathf.PI * 46f) * 0.5f + Mathf.Sin(t * 2f * Mathf.PI * 46.7f) * 0.45f
                          + Mathf.Sin(t * 2f * Mathf.PI * 69.3f) * 0.18f * (0.5f + 0.5f * Mathf.Sin(t * 0.9f)) + lp * 2.4f;
                d[i] = s * env;
            }
            return d;
        }

        /// <summary>The small lever: a sharp metal clack, a short electric crackle.</summary>
        static float[] ClackData()
        {
            var d = new float[(int)(0.5f * Rate)];
            float bp = 0f, bp2 = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float clack = Mathf.Exp(-t * 70f) * (Mathf.Sin(t * 2f * Mathf.PI * 2350f) * 0.6f + Mathf.Sin(t * 2f * Mathf.PI * 3900f) * 0.35f + Rnd() * 0.5f);
                float knock = Mathf.Exp(-t * 35f) * Mathf.Sin(t * 2f * Mathf.PI * 420f) * 0.7f;
                bp += (Rnd() - bp) * 0.5f;
                bp2 += (bp - bp2) * 0.3f;
                float crackle = (bp - bp2) * (Rnd() > 0.6f ? 1f : 0.15f) * Mathf.Exp(-(t - 0.02f) * 14f) * (t > 0.015f ? 1.6f : 0f);
                d[i] = clack + knock + crackle;
            }
            return d;
        }

        static float[] SnapData()
        {
            const float crack = SnapCrackInClip;
            var d = new float[(int)(0.9f * Rate)];
            // Stick-slip groan of the bending rod (impulses, faster and faster) through two metal resonances.
            var groan = new float[d.Length];
            double next = 0.0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                if (t < crack && t >= next) { groan[i] = 1f; next = t + 1.0 / (45.0 + 300.0 * t + (Rnd() * 0.5 + 0.5) * 15.0); }
            }
            float y1 = 0f, y2 = 0f, z1 = 0f, z2 = 0f;
            float c1 = 2f * 0.996f * Mathf.Cos(2f * Mathf.PI * 880f / Rate), r1 = 0.996f * 0.996f;
            float c2 = 2f * 0.994f * Mathf.Cos(2f * Mathf.PI * 2350f / Rate), r2 = 0.994f * 0.994f;
            float hp = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float x = groan[i];
                float a = x + c1 * y1 - r1 * y2; y2 = y1; y1 = a;
                float b = x + c2 * z1 - r2 * z2; z2 = z1; z1 = b;
                float swell = Mathf.Clamp01(t / crack) * (t < crack ? 1f : Mathf.Exp(-(t - crack) * 60f));
                float g = (a * 0.08f + b * 0.06f) * swell;
                // The crack: broadband burst, a bright inharmonic ring of the snapped rod, a low knock in the box.
                float lt = t - crack;
                float hit = 0f;
                if (lt >= 0f)
                {
                    float n = Rnd();
                    hp += (n - hp) * 0.7f;
                    hit = (n - hp) * 1.6f * Mathf.Exp(-lt * 80f)
                          + Mathf.Exp(-lt * 11f) * (Mathf.Sin(lt * 2f * Mathf.PI * 2930f) * 0.32f + Mathf.Sin(lt * 2f * Mathf.PI * 4170f) * 0.2f
                                                     + Mathf.Sin(lt * 2f * Mathf.PI * 6340f) * 0.1f)
                          + Mathf.Exp(-lt * 30f) * Mathf.Sin(lt * 2f * Mathf.PI * 175f) * 0.7f
                          // Electric crackle from the stub, dying away.
                          + (Rnd() > 0.994f ? Rnd() * 1.1f : 0f) * Mathf.Exp(-lt * 7f) * (lt > 0.03f ? 1f : 0f);
                }
                d[i] = g + hit;
            }
            return d;
        }

        /// <summary>Slats clattering in the guide rails as the shutter falls, faster and faster.</summary>
        static float[] RattleData(float len)
        {
            var d = new float[(int)(len * Rate)];
            double next = 0.0;
            float ring = 0f, ringV = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float k = t / len;
                if (t >= next)
                {
                    ringV += 0.6f + 0.4f * Rnd();
                    next = t + Mathf.Lerp(0.05f, 0.012f, k * k) * (0.8f + 0.4f * Mathf.Abs(Rnd()));
                }
                ring = ring * 0.9985f + ringV;
                ringV *= 0.55f;
                float metal = Mathf.Sin(t * 2f * Mathf.PI * (1150f + 300f * Mathf.Sin(t * 7f))) * ring * 0.05f;
                float hiss = Rnd() * 0.12f * k;
                d[i] = (metal + hiss) * Mathf.Lerp(0.35f, 1f, k);
            }
            return d;
        }

        /// <summary>Fluorescent tube buzz with a few dropouts (lamps flickering after the slam).</summary>
        static float[] BuzzData(float len)
        {
            var d = new float[(int)(len * Rate)];
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float gate = Mathf.Sin(t * 37f) > -0.2f ? 1f : 0.1f;
                d[i] = (Mathf.Sin(t * 2f * Mathf.PI * 100f) * 0.5f + Mathf.Sin(t * 2f * Mathf.PI * 200f) * 0.3f + Rnd() * 0.08f) * gate;
            }
            return d;
        }

        /// <summary>Outro: big breaker thrown, then tubes clicking on.</summary>
        static float[] BreakerData()
        {
            var d = new float[(int)(1.4f * Rate)];
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float thunk = Mathf.Exp(-t * 18f) * (Mathf.Sin(t * 2f * Mathf.PI * 95f) * 0.9f + Rnd() * 0.3f);
                float clang = Mathf.Exp(-t * 9f) * Mathf.Sin(t * 2f * Mathf.PI * 760f) * 0.25f;
                float hum = Mathf.Clamp01((t - 0.2f) * 2f) * (Mathf.Sin(t * 2f * Mathf.PI * 100f) * 0.18f + Mathf.Sin(t * 2f * Mathf.PI * 300f) * 0.05f);
                d[i] = thunk + clang + hum;
            }
            return d;
        }

        static float[] TickData()
        {
            var d = new float[(int)(0.12f * Rate)];
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                d[i] = Mathf.Exp(-t * 55f) * (Mathf.Sin(t * 2f * Mathf.PI * 1760f) * 0.6f + Mathf.Sin(t * 2f * Mathf.PI * 2640f) * 0.25f);
            }
            return d;
        }

        static float[] WhooshData(float len)
        {
            var d = new float[(int)(len * Rate)];
            float lp = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float k = t / len;
                float cut = Mathf.Lerp(0.02f, 0.25f, Mathf.Sin(k * Mathf.PI));
                lp += (Rnd() - lp) * cut;
                d[i] = lp * Mathf.Sin(k * Mathf.PI);
            }
            return d;
        }

        /// <summary>16-bit mono WAV bytes (tests: the sound track of the recorded video).</summary>
        public static byte[] Wav(float[] samples) => LeverSounds.Wav(samples);
    }
}
