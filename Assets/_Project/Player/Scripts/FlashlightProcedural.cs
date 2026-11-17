using UnityEngine;

namespace HideAndSeek.Player
{
    /// <summary>Deterministic procedural content for the flashlight: cookie texture, beam mesh, click sounds.</summary>
    public static class FlashlightProcedural
    {
        // ------------------------------------------------------------------ cookie

        /// <summary>
        /// Generates a spot-light cookie (value stored in alpha, also in RGB): hot centre, darker ring,
        /// bright reflector ring, soft halo and faint lens imperfections. Border pixels are black.
        /// </summary>
        public static Texture2D GenerateCookie(int size = 256, int seed = 1337)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, true)
            {
                name = "FlashlightCookie",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 0,
            };
            var px = new Color32[size * size];
            float inv = 1f / (size - 1);

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x * inv * 2f - 1f;
                float v = y * inv * 2f - 1f;
                float value = CookieValue(u, v, seed);
                // keep a black 2px border so clamped sampling never leaks outside the cone
                if (x < 2 || y < 2 || x >= size - 2 || y >= size - 2) value = 0f;
                byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Pow(Mathf.Clamp01(value), 1f) * 255f), 0, 255);
                px[y * size + x] = new Color32(b, b, b, b);
            }

            tex.SetPixels32(px);
            tex.Apply(true, false);
            return tex;
        }

        /// <summary>Cookie intensity at normalised coordinates (-1..1, radius 1 = cone edge).</summary>
        public static float CookieValue(float u, float v, int seed = 1337)
        {
            float r = Mathf.Sqrt(u * u + v * v);
            if (r >= 0.985f) return 0f;
            float ang = Mathf.Atan2(v, u);

            // Hot centre (LED die image), slightly elongated & offset like a real reflector.
            float cu = u - 0.012f, cv = v + 0.008f;
            float rc = Mathf.Sqrt(cu * cu * 1.0f + cv * cv * 1.08f);
            // Target radial profile (linear light): centre 1.0, r=.15 ~.73, r=.3 ~.37, r=.45 ~.17 (slight dip),
            // r=.6 ~.15 (reflector rim), r=.8 ~.03, edge 0.
            float hot = 0.55f * Mathf.Exp(-(rc * rc) / (2f * 0.13f * 0.13f));
            float core = 0.06f * Mathf.Exp(-(rc * rc) / (2f * 0.04f * 0.04f));

            // Main body of the beam.
            float body = 0.3f * (1f - SmoothStep(0.15f, 0.5f, r));

            // Faint darker ring just outside the body, then a soft reflector rim ring.
            float darkRing = 0.03f * Gauss(r, 0.5f, 0.05f);
            float rim = 0.035f * Gauss(r, 0.6f, 0.045f);

            // Soft wide halo (spill) to the edge.
            float halo = 0.15f * Mathf.Pow(1f - SmoothStep(0.4f, 1f, r), 1.6f) + 0.035f * (1f - r);

            float val = hot + core + body + rim + halo - darkRing;

            // Lens / reflector imperfections: angular streaks, faint concentric ripples, a couple of smudges.
            float streaks = 0f;
            float phase = seed * 0.123f;
            streaks += 0.045f * Mathf.Sin(ang * 7f + phase) * Mathf.Sin(ang * 3f + 1.7f + phase);
            streaks += 0.03f * Mathf.Sin(ang * 13f + 0.4f + phase * 2f);
            streaks *= SmoothStep(0.15f, 0.5f, r);
            float ripples = 0.025f * Mathf.Sin(r * 62f + 0.5f) * SmoothStep(0.25f, 0.6f, r) * (1f - SmoothStep(0.7f, 0.95f, r));
            float smudge = 0.06f * Gauss2(u, v, 0.30f, -0.18f, 0.09f) - 0.05f * Gauss2(u, v, -0.22f, 0.27f, 0.11f);
            // Very faint low-frequency blotches (dirty lens), scaled with brightness so dark areas stay clean.
            float blotch = (Mathf.PerlinNoise(u * 3.1f + seed * 0.37f, v * 3.1f + 5.3f) - 0.5f) * 0.08f;

            val *= 1f + streaks + ripples + blotch;
            val += smudge * SmoothStep(0.05f, 0.4f, r) * val * 2.5f;

            // Final edge falloff to exactly zero at the cone edge.
            val *= 1f - SmoothStep(0.88f, 0.985f, r);
            return Mathf.Clamp01(val);
        }

        private static float Gauss(float x, float mu, float sigma)
        {
            float d = (x - mu) / sigma;
            return Mathf.Exp(-0.5f * d * d);
        }

        private static float Gauss2(float u, float v, float cu, float cv, float sigma)
        {
            float du = u - cu, dv = v - cv;
            return Mathf.Exp(-(du * du + dv * dv) / (2f * sigma * sigma));
        }

        private static float SmoothStep(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        // ------------------------------------------------------------------ beam mesh

        /// <summary>
        /// Builds nested open cones along +Z (apex region at the lens). Each shell stores its weight in vertex
        /// colour alpha; uv.y = 0 at the lens, 1 at <paramref name="length"/>; uv.x = around.
        /// </summary>
        public static Mesh BuildBeamMesh(float spotAngle, float length, float lensRadius = 0.03f, int segments = 40, int rings = 16)
        {
            var mesh = new Mesh { name = "FlashlightBeam" };
            float[] shellAngleFrac = { 0.32f, 0.62f, 0.95f };
            float[] shellWeight = { 0.9f, 0.55f, 0.32f };

            int vertsPerShell = (segments + 1) * (rings + 1);
            var verts = new Vector3[vertsPerShell * shellAngleFrac.Length];
            var normals = new Vector3[verts.Length];
            var uvs = new Vector2[verts.Length];
            var colors = new Color[verts.Length];
            var tris = new int[segments * rings * 6 * shellAngleFrac.Length];
            int vi = 0, ti = 0;

            for (int s = 0; s < shellAngleFrac.Length; s++)
            {
                float half = spotAngle * 0.5f * shellAngleFrac[s] * Mathf.Deg2Rad;
                float tanHalf = Mathf.Tan(half);
                float endRadius = lensRadius + length * tanHalf;
                // Cone surface normal tilt (outward & slightly back toward the lens).
                float slope = (endRadius - lensRadius) / length;
                float nz = -slope / Mathf.Sqrt(1f + slope * slope);
                float nr = 1f / Mathf.Sqrt(1f + slope * slope);
                int baseIndex = vi;

                for (int ri = 0; ri <= rings; ri++)
                {
                    // Denser rings near the lens where the cone changes fastest.
                    float t = (float)ri / rings;
                    t = t * t * 0.5f + t * 0.5f;
                    float z = t * length;
                    float radius = lensRadius + z * tanHalf;
                    for (int si = 0; si <= segments; si++)
                    {
                        float a = (float)si / segments * Mathf.PI * 2f;
                        float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                        verts[vi] = new Vector3(ca * radius, sa * radius, z);
                        normals[vi] = new Vector3(ca * nr, sa * nr, nz);
                        uvs[vi] = new Vector2((float)si / segments, t);
                        colors[vi] = new Color(1f, 1f, 1f, shellWeight[s]);
                        vi++;
                    }
                }

                for (int ri = 0; ri < rings; ri++)
                for (int si = 0; si < segments; si++)
                {
                    int a = baseIndex + ri * (segments + 1) + si;
                    int b = a + segments + 1;
                    tris[ti++] = a; tris[ti++] = b; tris[ti++] = a + 1;
                    tris[ti++] = a + 1; tris[ti++] = b; tris[ti++] = b + 1;
                }
            }

            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.triangles = tris;
            float endR = lensRadius + length * Mathf.Tan(spotAngle * 0.5f * Mathf.Deg2Rad);
            mesh.bounds = new Bounds(new Vector3(0f, 0f, length * 0.5f), new Vector3(endR * 2f, endR * 2f, length));
            return mesh;
        }

        // ------------------------------------------------------------------ audio

        /// <summary>Short mechanical click (switch on = brighter "tick", off = lower "tock").</summary>
        public static AudioClip CreateClickClip(bool on, int sampleRate = 44100)
        {
            float duration = 0.075f;
            int n = Mathf.CeilToInt(duration * sampleRate);
            var data = new float[n];
            var rng = new System.Random(on ? 91 : 47);
            float toneA = on ? 3150f : 2350f;
            float toneB = on ? 5400f : 4100f;
            float body = on ? 820f : 610f;
            // Two contacts: the spring snap and the latch, a few ms apart.
            float[] clickTimes = { 0f, on ? 0.0105f : 0.0135f };
            float[] clickGains = { 1f, 0.55f };
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sampleRate;
                float s = 0f;
                for (int c = 0; c < clickTimes.Length; c++)
                {
                    float tc = t - clickTimes[c];
                    if (tc < 0f) continue;
                    float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                    float transient = noise * Mathf.Exp(-tc * 900f);
                    float ring = (Mathf.Sin(2f * Mathf.PI * toneA * tc) * 0.6f + Mathf.Sin(2f * Mathf.PI * toneB * tc) * 0.3f) * Mathf.Exp(-tc * 420f);
                    float thump = Mathf.Sin(2f * Mathf.PI * body * tc) * Mathf.Exp(-tc * 160f) * 0.45f;
                    s += (transient * 0.7f + ring + thump) * clickGains[c];
                }
                // gentle low-pass to remove harshness
                lp += (s - lp) * 0.55f;
                data[i] = Mathf.Clamp(lp * 0.55f, -1f, 1f);
            }
            // short fade-out to avoid a pop at the end
            int fade = Mathf.Min(n, sampleRate / 200);
            for (int i = 0; i < fade; i++) data[n - 1 - i] *= (float)i / fade;

            var clip = AudioClip.Create(on ? "FlashlightClickOn" : "FlashlightClickOff", n, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
