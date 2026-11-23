using HideAndSeek.Audio;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Pulling a rod out of rubble: synthesised sounds (steel grinding in broken concrete on every yank, gravel
    /// trickling down the heap, the crack of the rip) played in 3D through <see cref="GameAudio.PlayClipAt"/>, and two
    /// shared particle systems in world space: concrete dust puffs and small bits of rubble bouncing off the floor.
    /// </summary>
    public static class RubbleFx
    {
        private const int Rate = 44100;
        private static AudioClip[] _grind, _trickle;
        private static AudioClip _crack;
        private static ParticleSystem _dust, _bits;
        private static Material _mat;

        public static int Played;

        public static void Yank(Vector3 entry, Vector3 axis, float strength)
        {
            Played++;
            _grind ??= new[] { Grind(1, 0.42f), Grind(2, 0.5f), Grind(3, 0.38f) };
            _trickle ??= new[] { Trickle(11, 0.9f), Trickle(12, 1.2f) };
            GameAudio.PlayClipAt(_grind[Random.Range(0, _grind.Length)], entry, 0.55f + 0.25f * strength, 18f);
            GameAudio.PlayClipAt(_trickle[Random.Range(0, _trickle.Length)], entry + Vector3.down * 0.15f, 0.4f, 14f);
            Burst(entry, axis, 0.6f + 0.6f * strength, 6 + (int)(6 * strength));
        }

        public static void Rip(Vector3 entry, Vector3 axis)
        {
            Played++;
            _crack ??= Crack();
            _trickle ??= new[] { Trickle(11, 0.9f), Trickle(12, 1.2f) };
            GameAudio.PlayClipAt(_crack, entry, 0.9f, 24f);
            GameAudio.PlayClipAt(_trickle[1], entry + Vector3.down * 0.15f, 0.55f, 14f);
            RodSounds.Step(entry + axis * 0.3f, 0.8f);
            Burst(entry, axis, 1.6f, 18);
        }

        /// <summary>Little dust while it is held under strain (called a few times per second).</summary>
        public static void Sift(Vector3 entry)
        {
            Ensure();
            Emit(_dust, entry + Random.insideUnitSphere * 0.05f, Vector3.up * 0.05f + Random.insideUnitSphere * 0.08f, 0.12f, 1.6f, 0.22f);
            if (Random.value < 0.5f) Emit(_bits, entry + Random.insideUnitSphere * 0.08f, Random.insideUnitSphere * 0.3f, 0.025f, 1.2f, 1f);
        }

        // ---------------------------------------------------------------- particles

        private static void Burst(Vector3 at, Vector3 axis, float strength, int bits)
        {
            Ensure();
            for (int i = 0; i < 5 + (int)(strength * 6f); i++)
            {
                Vector3 v = (axis * 0.25f + Random.insideUnitSphere * 0.35f + Vector3.up * 0.15f) * strength;
                Emit(_dust, at + Random.insideUnitSphere * 0.08f, v, Random.Range(0.12f, 0.28f) * (0.7f + 0.3f * strength), Random.Range(1.4f, 2.6f), Random.Range(0.25f, 0.45f));
            }
            for (int i = 0; i < bits; i++)
            {
                Vector3 v = (axis * 0.6f + Random.insideUnitSphere * 0.9f + Vector3.up * 0.6f) * (0.6f + 0.5f * strength);
                Emit(_bits, at + Random.insideUnitSphere * 0.06f, v, Random.Range(0.015f, 0.045f), Random.Range(1.2f, 2f), 1f);
            }
        }

        private static void Emit(ParticleSystem ps, Vector3 pos, Vector3 vel, float size, float life, float alpha)
        {
            if (ps == null) return;
            var e = new ParticleSystem.EmitParams
            {
                position = pos,
                velocity = vel,
                startSize = size,
                startLifetime = life,
                startColor = ps == _bits ? new Color(0.42f, 0.41f, 0.39f, alpha) : new Color(0.55f, 0.53f, 0.5f, alpha),
                rotation = Random.Range(0f, 360f),
            };
            ps.Emit(e, 1);
        }

        private static void Ensure()
        {
            if (_dust != null && _bits != null) return;
            if (_mat == null)
            {
                var tex = new Texture2D(32, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
                var px = new Color[32 * 32];
                for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    float dx = (x - 15.5f) / 15.5f, dy = (y - 15.5f) / 15.5f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    px[y * 32 + x] = new Color(1f, 1f, 1f, a * a * (0.8f + 0.2f * Mathf.PerlinNoise(x * 0.3f, y * 0.3f)));
                }
                tex.SetPixels(px);
                tex.Apply();
                var sh = Shader.Find("Sprites/Default");
                _mat = new Material(sh) { mainTexture = tex, name = "RubbleDust", hideFlags = HideFlags.DontSave };
            }
            _dust = Make("RubbleDust", -0.015f, false);
            _bits = Make("RubbleBits", 1f, true);
        }

        private static ParticleSystem Make(string name, float gravity, bool bits)
        {
            var go = new GameObject(name);
            Object.DontDestroyOnLoad(go);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = gravity;
            main.maxParticles = bits ? 300 : 200;
            var em = ps.emission;
            em.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                bits ? new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) }
                     : new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            if (!bits)
            {
                var sz = ps.sizeOverLifetime;
                sz.enabled = true;
                sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 2.2f));
                var vel = ps.limitVelocityOverLifetime;
                vel.enabled = true;
                vel.limit = 0.4f;
                vel.dampen = 0.15f;
            }
            else
            {
                var c = ps.collision;
                c.enabled = true;
                c.type = ParticleSystemCollisionType.World;
                c.mode = ParticleSystemCollisionMode.Collision3D;
                c.quality = ParticleSystemCollisionQuality.Medium;
                c.bounce = 0.25f;
                c.dampen = 0.5f;
                c.lifetimeLoss = 0f;
            }
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = _mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

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

        /// <summary>Steel dragged through broken concrete: gritty low noise, crunches and a short metal squeal.</summary>
        private static AudioClip Grind(int seed, float length)
        {
            var rnd = new System.Random(seed * 131);
            int n = (int)(length * Rate);
            var d = new float[n];
            float lp = 0f, lp2 = 0f, crunch = 0f;
            float squealF = 1400f + seed * 230f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float w = (float)(rnd.NextDouble() * 2.0 - 1.0);
                lp += (w - lp) * 0.18f;
                lp2 += (lp - lp2) * 0.05f;
                if (rnd.NextDouble() < 0.0025) crunch = 0.8f + (float)rnd.NextDouble() * 0.6f;
                crunch *= 0.993f;
                float env = Mathf.Clamp01(t / 0.03f) * Mathf.Exp(-t / (length * 0.45f));
                float squeal = Mathf.Sin(2f * Mathf.PI * squealF * t * (1f + 0.04f * Mathf.Sin(t * 30f))) * Mathf.Exp(-t / 0.09f) * 0.25f;
                d[i] = ((lp - lp2) * 2.2f + lp2 * 1.5f) * env * (0.6f + crunch) + squeal * env;
            }
            return Make("RubbleGrind" + seed, d);
        }

        /// <summary>Gravel trickling down the heap: sparse clicks of pebbles, thinning out.</summary>
        private static AudioClip Trickle(int seed, float length)
        {
            var rnd = new System.Random(seed * 17);
            int n = (int)(length * Rate);
            var d = new float[n];
            for (int k = 0; k < 70; k++)
            {
                float at = Mathf.Pow((float)rnd.NextDouble(), 1.8f) * (length - 0.05f);
                int start = (int)(at * Rate);
                float f = 1800f + (float)rnd.NextDouble() * 3500f, amp = 0.3f + (float)rnd.NextDouble() * 0.7f;
                amp *= 1f - at / length;
                for (int i = 0; i < 900 && start + i < n; i++)
                {
                    float t = i / (float)Rate;
                    d[start + i] += Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t / 0.004f) * amp
                                    + (float)(rnd.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-t / 0.002f) * amp * 0.5f;
                }
            }
            return Make("RubbleTrickle" + seed, d);
        }

        /// <summary>The rip: a dry crack of concrete, a thump and a short scrape of the free rod.</summary>
        private static AudioClip Crack()
        {
            var rnd = new System.Random(4242);
            int n = (int)(0.6f * Rate);
            var d = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float w = (float)(rnd.NextDouble() * 2.0 - 1.0);
                lp += (w - lp) * 0.3f;
                float crack = w * Mathf.Exp(-t / 0.012f) * 1.2f + lp * Mathf.Exp(-t / 0.06f);
                float thump = Mathf.Sin(2f * Mathf.PI * 70f * t) * Mathf.Exp(-t / 0.07f) * 0.9f;
                float scrape = t > 0.05f ? lp * 0.4f * Mathf.Exp(-(t - 0.05f) / 0.15f) : 0f;
                d[i] = crack + thump + scrape;
            }
            return Make("RubbleCrack", d);
        }
    }
}
