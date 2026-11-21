using UnityEngine;

namespace HideAndSeek.Player.Editor.Audio
{
    /// <summary>
    /// Procedural synthesis of shoe footsteps and movement foley. Every function is deterministic for a given seed.
    /// A step = heel strike (sole transient + band-passed body + low thump) followed by a toe roll-off,
    /// a grit/scuff tail, surface specific layers (metal modes / water splash + droplets), per-variation EQ and
    /// a couple of early reflections for the concrete garage.
    /// </summary>
    internal static class FootstepSynth
    {
        private const float Fs = Dsp.Fs;

        private sealed class Shoe
        {
            public float heel, transient, bodyFreq, bodyQ, bodyTau, transientTau, transientHp;
            public float thump, thumpF0, thumpF1, thumpTau;
            public float toeDelay, toe, toeFreq, toeTau;
            public float scuffStart, scuffDur, scuffFreq, scuff, gritRate, grit;
            public float length, lowpass;

            public Shoe Jittered(Rng r)
            {
                return new Shoe
                {
                    heel = r.Jitter(heel, 0.1f),
                    transient = r.Jitter(transient, 0.25f),
                    bodyFreq = r.Jitter(bodyFreq, 0.16f),
                    bodyQ = r.Jitter(bodyQ, 0.2f),
                    bodyTau = r.Jitter(bodyTau, 0.2f),
                    transientTau = r.Jitter(transientTau, 0.2f),
                    transientHp = r.Jitter(transientHp, 0.15f),
                    thump = r.Jitter(thump, 0.18f),
                    thumpF0 = r.Jitter(thumpF0, 0.1f),
                    thumpF1 = r.Jitter(thumpF1, 0.08f),
                    thumpTau = r.Jitter(thumpTau, 0.2f),
                    toeDelay = r.Jitter(toeDelay, 0.16f),
                    toe = r.Jitter(toe, 0.22f),
                    toeFreq = r.Jitter(toeFreq, 0.18f),
                    toeTau = r.Jitter(toeTau, 0.2f),
                    scuffStart = r.Jitter(scuffStart, 0.2f),
                    scuffDur = r.Jitter(scuffDur, 0.25f),
                    scuffFreq = r.Jitter(scuffFreq, 0.2f),
                    scuff = r.Jitter(scuff, 0.3f),
                    gritRate = r.Jitter(gritRate, 0.3f),
                    grit = r.Jitter(grit, 0.3f),
                    length = length,
                    lowpass = r.Jitter(lowpass, 0.12f),
                };
            }
        }

        private static Shoe ShoeFor(FootstepGait gait)
        {
            switch (gait)
            {
                case FootstepGait.Run:
                    return new Shoe
                    {
                        heel = 1f, transient = 0.62f, bodyFreq = 560f, bodyQ = 0.85f, bodyTau = 0.020f, transientTau = 0.0010f, transientHp = 2600f,
                        thump = 0.75f, thumpF0 = 125f, thumpF1 = 56f, thumpTau = 0.042f,
                        toeDelay = 0.046f, toe = 0.72f, toeFreq = 1250f, toeTau = 0.012f,
                        scuffStart = 0.055f, scuffDur = 0.12f, scuffFreq = 2100f, scuff = 0.13f, gritRate = 380f, grit = 0.16f,
                        length = 0.36f, lowpass = 9000f,
                    };
                case FootstepGait.Crouch:
                    return new Shoe
                    {
                        heel = 0.5f, transient = 0.1f, bodyFreq = 380f, bodyQ = 0.7f, bodyTau = 0.03f, transientTau = 0.0018f, transientHp = 2000f,
                        thump = 0.34f, thumpF0 = 88f, thumpF1 = 56f, thumpTau = 0.03f,
                        toeDelay = 0.16f, toe = 0.34f, toeFreq = 820f, toeTau = 0.02f,
                        scuffStart = 0.025f, scuffDur = 0.27f, scuffFreq = 1650f, scuff = 0.2f, gritRate = 210f, grit = 0.2f,
                        length = 0.48f, lowpass = 5200f,
                    };
                default:
                    return new Shoe
                    {
                        heel = 1f, transient = 0.42f, bodyFreq = 480f, bodyQ = 0.9f, bodyTau = 0.017f, transientTau = 0.0012f, transientHp = 2800f,
                        thump = 0.72f, thumpF0 = 108f, thumpF1 = 60f, thumpTau = 0.034f,
                        toeDelay = 0.085f, toe = 0.46f, toeFreq = 1120f, toeTau = 0.011f,
                        scuffStart = 0.07f, scuffDur = 0.11f, scuffFreq = 2350f, scuff = 0.085f, gritRate = 260f, grit = 0.12f,
                        length = 0.38f, lowpass = 7600f,
                    };
            }
        }

        /// <summary>One footstep variation.</summary>
        public static float[] Step(SurfaceType surface, FootstepGait gait, int variant, ulong seed)
        {
            var r = new Rng(seed ^ ((ulong)surface << 40) ^ ((ulong)gait << 32) ^ (ulong)(variant * 7919 + 1));
            var shoe = ShoeFor(gait).Jittered(r);
            float extra = surface == SurfaceType.Metal ? 0.14f : surface == SurfaceType.Water ? 0.1f : 0f;
            var buf = new float[Dsp.Samples(shoe.length + extra + 0.05f)];

            float shoeScale = surface == SurfaceType.Concrete ? 1f : surface == SurfaceType.Metal ? 0.55f : 0.45f;
            AddShoe(buf, shoe, r, shoeScale, surface);

            if (surface == SurfaceType.Metal) AddMetal(buf, shoe, gait, r);
            else if (surface == SurfaceType.Water) AddWater(buf, shoe, gait, r);

            // Overall tone: sole damping + per-variation colour.
            Dsp.Filter(buf,
                Biquad.LowPass(Fs, surface == SurfaceType.Metal ? shoe.lowpass * 1.4f : shoe.lowpass, 0.6f),
                Biquad.Peak(Fs, r.Range(900f, 3400f), 1.1f, r.Range(-2.5f, 2.5f)),
                Biquad.HighShelf(Fs, 5000f, r.Range(-3f, 1.5f)),
                Biquad.Peak(Fs, r.Range(160f, 260f), 0.9f, r.Range(-1.5f, 2f)));

            // Garage early reflections (the runtime reverb adds the long tail).
            Dsp.EarlyReflections(buf,
                new[] { r.Range(0.011f, 0.016f), r.Range(0.019f, 0.027f), r.Range(0.031f, 0.039f) },
                new[] { 0.15f, 0.09f, 0.05f }, 2600f);

            return Dsp.Finish(buf, 0.89f * r.Range(0.86f, 1f));
        }

        private static void AddShoe(float[] buf, Shoe s, Rng r, float scale, SurfaceType surface)
        {
            // --- heel strike: sole "tick" transient
            Dsp.AddNoise(buf, 0f, s.transientTau * 8f, t => Dsp.AttackDecay(t, 0.00015f, s.transientTau), s.transient * s.heel * 0.6f * scale, r,
                Biquad.HighPass(Fs, s.transientHp, 0.7f), Biquad.LowPass(Fs, 11000f));
            // body: two cascaded band-passes give the woody "tock" of a sole on concrete
            Dsp.AddNoise(buf, 0f, s.bodyTau * 7f, t => Dsp.AttackDecay(t, 0.0007f, s.bodyTau), s.heel * 4.2f * scale, r,
                Biquad.BandPass(Fs, s.bodyFreq, s.bodyQ), Biquad.LowPass(Fs, s.bodyFreq * 2.6f));
            // lower "knock" of the heel block (fills 200-400 Hz)
            Dsp.AddNoise(buf, 0f, s.bodyTau * 8f, t => Dsp.AttackDecay(t, 0.001f, s.bodyTau * 1.25f), s.heel * 4.8f * scale, r,
                Biquad.BandPass(Fs, s.bodyFreq * 0.52f, 1.1f));
            // low thump (heel/body weight into the slab) - kept in the 50-130 Hz range
            float thumpScale = surface == SurfaceType.Metal ? 0.6f : surface == SurfaceType.Water ? 0.7f : 1f;
            Dsp.AddThump(buf, 0.0005f, s.thumpF0, s.thumpF1, 0.012f, 0.0025f, s.thumpTau, s.thump * 0.55f * thumpScale);
            Dsp.AddNoise(buf, 0f, s.thumpTau * 5f, t => Dsp.AttackDecay(t, 0.002f, s.thumpTau * 0.7f), s.thump * 0.9f * thumpScale, r,
                Biquad.LowPass(Fs, 240f, 0.8f), Biquad.LowPass(Fs, 300f));

            // --- toe roll-off: smaller, brighter contact
            float toeT = s.toeDelay;
            Dsp.AddNoise(buf, toeT, s.toeTau * 7f, t => Dsp.AttackDecay(t, 0.0012f, s.toeTau), s.toe * 2.6f * scale, r,
                Biquad.BandPass(Fs, s.toeFreq, 0.9f), Biquad.HighPass(Fs, 300f));
            Dsp.AddNoise(buf, toeT, 0.01f, t => Dsp.AttackDecay(t, 0.0002f, 0.001f), s.toe * s.transient * 0.6f * scale, r,
                Biquad.HighPass(Fs, s.transientHp * 1.2f));
            Dsp.AddThump(buf, toeT, s.thumpF0 * 1.25f, s.thumpF1 * 1.2f, 0.01f, 0.002f, s.thumpTau * 0.5f, s.thump * 0.16f * s.toe * thumpScale);

            // --- scuff / grit tail (sole sliding on dusty concrete)
            float scuffGain = surface == SurfaceType.Water ? 0.4f : 1f;
            Dsp.AddNoise(buf, s.scuffStart, s.scuffDur, t => Dsp.Hump(t, s.scuffDur, 0.65f, 1.6f), s.scuff * 0.95f * scuffGain * scale, r,
                Biquad.HighPass(Fs, 900f), Biquad.BandPass(Fs, s.scuffFreq, 0.65f));
            Dsp.AddGrit(buf, s.scuffStart * 0.6f, s.scuffDur * 1.25f, s.gritRate, t => Dsp.Hump(t, s.scuffDur * 1.25f, 0.5f, 1f),
                s.grit * 0.5f * scuffGain * scale, r, 1600f, 3400f, 0.6f);
            // tiny debris settling after the step
            Dsp.AddGrit(buf, s.toeDelay + 0.02f, 0.1f, s.gritRate * 0.25f, t => Mathf.Exp(-t / 0.04f), s.grit * 0.25f * scuffGain * scale, r, 2500f, 4500f, 0.7f);
        }

        private static readonly float[] PlateRatios = { 1f, 1.59f, 2.14f, 2.30f, 2.65f, 2.92f, 3.50f, 4.15f, 5.40f, 6.21f, 7.63f, 9.08f, 11.3f };

        private static void AddMetal(float[] buf, Shoe s, FootstepGait gait, Rng r)
        {
            float f0 = gait == FootstepGait.Run ? r.Range(400f, 500f) : gait == FootstepGait.Crouch ? r.Range(340f, 440f) : r.Range(380f, 480f);
            float baseTau = gait == FootstepGait.Run ? 0.12f : gait == FootstepGait.Crouch ? 0.06f : 0.095f;
            float gain = gait == FootstepGait.Run ? 0.26f : gait == FootstepGait.Crouch ? 0.11f : 0.19f;
            float hiCut = gait == FootstepGait.Crouch ? 0.6f : 0.12f; // crouch = soft sole, fewer high modes

            void Strike(float t0, float g, float freqMul)
            {
                for (int i = 0; i < PlateRatios.Length; i++)
                {
                    float f = f0 * PlateRatios[i] * freqMul * r.Range(0.985f, 1.015f);
                    float amp = r.Range(0.5f, 1f) / (1f + hiCut * i);
                    float tau = baseTau / (1f + 0.3f * i) * r.Range(0.8f, 1.2f);
                    Dsp.AddMode(buf, t0, f, tau, g * amp, r.Range(0f, 6.28f));
                }
            }

            Strike(0f, gain, 1f);
            Strike(s.toeDelay, gain * 0.55f, r.Range(0.99f, 1.01f));

            // loose grate bars rattling right after the impact
            int rattles = gait == FootstepGait.Crouch ? 1 : r.Range(2, gait == FootstepGait.Run ? 5 : 4);
            float rt = 0.012f;
            for (int k = 0; k < rattles; k++)
            {
                rt += r.Range(0.008f, 0.022f);
                float rf = f0 * r.Range(3.5f, 6f);
                float rg = gain * r.Range(0.25f, 0.5f) * Mathf.Pow(0.7f, k);
                Dsp.AddMode(buf, rt, rf, r.Range(0.012f, 0.025f), rg, r.Range(0f, 6.28f));
                Dsp.AddMode(buf, rt, rf * r.Range(1.45f, 1.7f), r.Range(0.008f, 0.016f), rg * 0.6f, r.Range(0f, 6.28f));
                Dsp.AddNoise(buf, rt, 0.008f, t => Dsp.AttackDecay(t, 0.0002f, 0.0012f), rg * 0.8f, r, Biquad.BandPass(Fs, rf * 1.8f, 1.5f));
            }

            // bright "ping" of steel on steel and the hollow cavity under the grate
            Dsp.AddNoise(buf, 0f, 0.06f, t => Dsp.AttackDecay(t, 0.0003f, 0.012f), gain * 2.4f, r, Biquad.BandPass(Fs, r.Range(2800f, 4200f), 2.5f));
            Dsp.AddThump(buf, 0.001f, 230f, 200f, 0.02f, 0.003f, 0.035f, gain * 0.45f);
        }

        private static void AddWater(float[] buf, Shoe s, FootstepGait gait, Rng r)
        {
            float splashTau = gait == FootstepGait.Run ? 0.11f : gait == FootstepGait.Crouch ? 0.06f : 0.075f;
            float splashGain = gait == FootstepGait.Run ? 0.75f : gait == FootstepGait.Crouch ? 0.22f : 0.5f;
            int drops = gait == FootstepGait.Run ? r.Range(14, 19) : gait == FootstepGait.Crouch ? r.Range(3, 6) : r.Range(8, 12);
            float dropMean = gait == FootstepGait.Run ? 0.07f : 0.055f;

            // flat "slap" of the sole on the water film
            Dsp.AddNoise(buf, 0f, 0.06f, t => Dsp.AttackDecay(t, 0.0008f, 0.009f), splashGain * 0.9f, r,
                Biquad.BandPass(Fs, r.Range(850f, 1150f), 0.8f));
            // body of the water "plop" displaced by the sole
            Dsp.AddNoise(buf, 0.001f, 0.16f, t => Dsp.AttackDecay(t, 0.002f, 0.032f), splashGain * 3.2f, r,
                Biquad.BandPass(Fs, r.Range(300f, 420f), 0.9f));

            // splash spray: band-limited noise with a spattering (granular) amplitude
            int n = Dsp.Samples(splashTau * 6f);
            var grains = Dsp.Crinkle(n, gait == FootstepGait.Crouch ? 140f : 320f, 0.3f, r);
            var hp = Biquad.HighPass(Fs, 650f);
            var bp = Biquad.BandPass(Fs, r.Range(2000f, 2800f), 0.5f);
            int s0 = Dsp.Samples(0.002f);
            for (int i = 0; i < n && s0 + i < buf.Length; i++)
            {
                float t = i / Fs;
                buf[s0 + i] += bp.Tick(hp.Tick(r.Noise())) * Dsp.AttackDecay(t, 0.006f, splashTau) * Mathf.Min(grains[i], 1.4f) * splashGain * 1.6f;
            }

            // droplets / bubbles falling back
            for (int k = 0; k < drops; k++)
            {
                float t = 0.008f + Mathf.Min(r.Exp(dropMean), 0.32f);
                float f = r.Range(650f, 3200f) * Mathf.Lerp(1f, 0.75f, t / 0.32f);
                float g = r.Range(0.08f, 0.3f) * Mathf.Exp(-t / 0.15f) * (gait == FootstepGait.Crouch ? 0.6f : 1f);
                Dsp.AddBubble(buf, t, f, r.Range(0.15f, 0.8f), r.Range(0.004f, 0.014f), g);
            }

            // toe lifting out of the water
            Dsp.AddNoise(buf, s.toeDelay, 0.12f, t => Dsp.AttackDecay(t, 0.004f, 0.025f), splashGain * 0.45f, r,
                Biquad.BandPass(Fs, r.Range(1300f, 1900f), 0.7f));
            for (int k = 0; k < 3; k++)
                Dsp.AddBubble(buf, s.toeDelay + r.Range(0.01f, 0.08f), r.Range(900f, 2600f), r.Range(0.2f, 0.7f), r.Range(0.004f, 0.01f), r.Range(0.06f, 0.14f));

            if (gait == FootstepGait.Crouch)
            {
                // slow slosh
                Dsp.AddNoise(buf, 0.01f, 0.35f, t => Dsp.Hump(t, 0.3f, 0.5f, 1.4f), 0.35f, r,
                    Biquad.LowPass(Fs, 1000f, 0.8f), Biquad.HighPass(Fs, 250f));
            }
        }

        // ------------------------------------------------------------------ foley

        public static float[] Jump(int variant, ulong seed)
        {
            var r = new Rng(seed ^ 0xA11CEUL ^ (ulong)(variant * 104729 + 3));
            var buf = new float[Dsp.Samples(0.5f)];
            // pressure thump of the push-off
            Dsp.AddThump(buf, 0f, r.Range(90f, 105f), 62f, 0.015f, 0.004f, 0.03f, 0.35f);
            Dsp.AddNoise(buf, 0f, 0.1f, t => Dsp.AttackDecay(t, 0.003f, 0.025f), 0.5f, r, Biquad.LowPass(Fs, 260f));
            // forefoot push scrape + grit
            float sd = r.Range(0.1f, 0.14f);
            Dsp.AddNoise(buf, 0.005f, sd, t => Dsp.Hump(t, sd, 0.45f, 1.2f), 0.4f, r,
                Biquad.HighPass(Fs, 500f), Biquad.BandPass(Fs, r.Range(1100f, 1500f), 0.7f));
            Dsp.AddGrit(buf, 0.005f, sd, 420f, t => Dsp.Hump(t, sd, 0.45f, 1f), 0.1f, r, 1600f, 3200f);
            Dsp.AddNoise(buf, 0.012f, 0.01f, t => Dsp.AttackDecay(t, 0.0002f, 0.0012f), 0.22f, r, Biquad.HighPass(Fs, 2600f));
            // clothing whoosh with a rising band
            int n = Dsp.Samples(0.36f);
            var mod = Dsp.Crinkle(n, 70f, 0.4f, r);
            int s0 = Dsp.Samples(0.03f);
            var hp = Biquad.HighPass(Fs, 300f);
            float fStart = r.Range(380f, 460f), fEnd = r.Range(1000f, 1300f);
            var bp = Biquad.BandPass(Fs, fStart, 0.6f);
            for (int i = 0; i < n && s0 + i < buf.Length; i++)
            {
                float t = i / Fs;
                if ((i & 31) == 0) bp.Retune(Biquad.BandPass(Fs, Mathf.Lerp(fStart, fEnd, Mathf.Clamp01(t / 0.22f)), 0.6f));
                buf[s0 + i] += bp.Tick(hp.Tick(r.Noise())) * Dsp.Hump(t, 0.36f, 0.6f, 1.3f) * Mathf.Min(mod[i], 1.5f) * 0.55f;
            }
            Dsp.Filter(buf, Biquad.Peak(Fs, r.Range(1000f, 2500f), 1f, r.Range(-2f, 2f)), Biquad.LowPass(Fs, 5500f, 0.6f));
            Dsp.EarlyReflections(buf, new[] { 0.013f, 0.023f }, new[] { 0.12f, 0.07f }, 2500f);
            return Dsp.Finish(buf, 0.85f);
        }


        public static float[] Landing(bool heavy, int variant, ulong seed)
        {
            var r = new Rng(seed ^ (heavy ? 0xBEEFUL : 0xFEEDUL) ^ (ulong)(variant * 15485863 + 5));
            var buf = new float[Dsp.Samples(heavy ? 0.8f : 0.5f)];
            float second = r.Range(0.012f, 0.024f);
            // both feet slapping down
            foreach (var (t0, g) in new[] { (0f, 1f), (second, r.Range(0.6f, 0.85f)) })
            {
                Dsp.AddNoise(buf, t0, 0.01f, t => Dsp.AttackDecay(t, 0.00015f, 0.0012f), g * (heavy ? 0.6f : 0.5f), r, Biquad.HighPass(Fs, 2500f));
                Dsp.AddNoise(buf, t0, 0.15f, t => Dsp.AttackDecay(t, 0.0007f, 0.022f), g * (heavy ? 4.5f : 3.8f), r,
                    Biquad.BandPass(Fs, r.Range(480f, 650f), 0.85f), Biquad.LowPass(Fs, 1500f));
                Dsp.AddNoise(buf, t0, 0.15f, t => Dsp.AttackDecay(t, 0.001f, 0.026f), g * (heavy ? 3.2f : 2.6f), r,
                    Biquad.BandPass(Fs, r.Range(240f, 300f), 1.1f));
            }
            // body weight into the floor
            if (heavy)
            {
                Dsp.AddThump(buf, 0.002f, r.Range(62f, 70f), 36f, 0.03f, 0.004f, 0.11f, 0.8f);
                Dsp.AddNoise(buf, 0f, 0.5f, t => Dsp.AttackDecay(t, 0.003f, 0.08f), 0.8f, r, Biquad.LowPass(Fs, 200f, 0.9f), Biquad.LowPass(Fs, 260f));
                Dsp.AddNoise(buf, 0f, 0.35f, t => Dsp.AttackDecay(t, 0.002f, 0.06f), 0.7f, r, Biquad.BandPass(Fs, r.Range(160f, 200f), 1.2f));
                // knees / jacket compressing
                float rd = r.Range(0.3f, 0.38f);
                Dsp.AddRustle(buf, 0.035f, rd, r.Range(1200f, 1600f), 0.3f, t => Dsp.Hump(t, rd, 0.45f, 1.2f), r, 110f);
                Dsp.AddGrit(buf, 0.03f, 0.18f, 300f, t => Mathf.Exp(-t / 0.06f), 0.22f, r, 2500f, 5000f);
            }
            else
            {
                Dsp.AddThump(buf, 0.002f, r.Range(72f, 82f), 48f, 0.02f, 0.003f, 0.05f, 0.8f);
                Dsp.AddNoise(buf, 0f, 0.25f, t => Dsp.AttackDecay(t, 0.002f, 0.04f), 0.7f, r, Biquad.LowPass(Fs, 260f, 0.8f));
                float rd = r.Range(0.16f, 0.22f);
                Dsp.AddRustle(buf, 0.01f, rd, r.Range(1100f, 1500f), 0.16f, t => Dsp.Hump(t, rd, 0.4f, 1.3f), r, 90f);
                Dsp.AddGrit(buf, 0.02f, 0.12f, 220f, t => Mathf.Exp(-t / 0.05f), 0.14f, r, 2500f, 5000f);
            }
            Dsp.Filter(buf, Biquad.Peak(Fs, r.Range(1200f, 3000f), 1f, r.Range(-2f, 2f)));
            Dsp.EarlyReflections(buf, new[] { r.Range(0.012f, 0.016f), r.Range(0.021f, 0.027f), 0.036f }, new[] { 0.16f, 0.1f, 0.06f }, 2200f);
            return Dsp.Finish(buf, 0.9f);
        }

        public static float[] CrouchRustle(bool down, int variant, ulong seed)
        {
            // Quiet, soft clothing rustle only: low crinkle rate (no crackle), no thumps or grit, darker filtering.
            // (The old version had a sole thump + dense crinkles that read as a bone crack.)
            var r = new Rng(seed ^ (down ? 0xC0C1UL : 0x5758UL) ^ (ulong)(variant * 7727 + 11));
            var buf = new float[Dsp.Samples(0.75f)];
            float dur = down ? r.Range(0.5f, 0.6f) : r.Range(0.42f, 0.52f);
            float center = down ? r.Range(820f, 980f) : r.Range(950f, 1150f);
            Dsp.AddRustle(buf, 0f, dur, center, 0.45f, t => Dsp.Hump(t, dur, down ? 0.5f : 0.4f, 1.6f), r, 16f);
            // second, softer fabric layer slightly later (trousers vs jacket)
            float dur2 = dur * r.Range(0.55f, 0.75f);
            Dsp.AddRustle(buf, r.Range(0.06f, 0.12f), dur2, center * r.Range(0.7f, 0.8f), 0.22f, t => Dsp.Hump(t, dur2, 0.55f, 1.8f), r, 12f);
            Dsp.Filter(buf, Biquad.LowPass(Fs, 2600f), Biquad.HighPass(Fs, 380f));
            Dsp.EarlyReflections(buf, new[] { 0.014f, 0.025f }, new[] { 0.06f, 0.03f }, 2000f);
            return Dsp.Finish(buf, 0.5f);
        }

        /// <summary>Seamless clothing rustle loop.</summary>
        public static float[] ClothLoop(float seconds, ulong seed)
        {
            var r = new Rng(seed ^ 0xC107UL);
            float xfade = 0.25f;
            var raw = new float[Dsp.Samples(seconds + xfade)];
            // slow swells (arm swing / thigh contact) on top of continuous fabric noise
            var swell = new float[raw.Length];
            float phase = r.Range(0f, 6.28f);
            for (int i = 0; i < raw.Length; i++)
            {
                float t = i / Fs;
                swell[i] = 0.55f + 0.3f * Mathf.Sin(t * 2f * Mathf.PI * 1.1f + phase) + 0.15f * Mathf.Sin(t * 2f * Mathf.PI * 2.7f + phase * 1.7f);
            }
            Dsp.AddRustle(raw, 0f, seconds + xfade, 1350f, 0.5f, t => swell[Mathf.Min(swell.Length - 1, Dsp.Samples(t))], r, 140f);
            Dsp.AddRustle(raw, 0f, seconds + xfade, 2600f, 0.18f, t => swell[Mathf.Min(swell.Length - 1, Dsp.Samples(t))], r, 220f);
            Dsp.Filter(raw, Biquad.HighPass(Fs, 300f));

            // crossfade the tail into the head for a click-free loop
            int n = Dsp.Samples(seconds), xf = Dsp.Samples(xfade);
            var loop = new float[n];
            for (int i = 0; i < n; i++) loop[i] = raw[i];
            for (int i = 0; i < xf; i++)
            {
                float a = (float)i / xf;
                float eqIn = Mathf.Sin(a * Mathf.PI * 0.5f), eqOut = Mathf.Cos(a * Mathf.PI * 0.5f);
                loop[i] = raw[i] * eqIn + raw[n + i] * eqOut;
            }
            float p = Dsp.Peak(loop);
            for (int i = 0; i < n; i++) loop[i] *= 0.6f / Mathf.Max(1e-6f, p);
            return loop;
        }
    }
}
