using System.Collections.Generic;
using HideAndSeek.Audio;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Small synthesised sounds for the lobby mini interactions (made once at runtime, 22 kHz): switch click, dart
    /// thunk, fish food sprinkle, globe whirr, record crackle and fireplace hiss loops.
    /// </summary>
    public static class LobbyPropSounds
    {
        public enum Id { Click, Thunk, Sprinkle, Whirr, Crackle, FireLoop, Clunk }

        private const int Rate = 22050;
        private static readonly Dictionary<Id, AudioClip> Clips = new();

        public static void Play(Id id, Vector3 at, float volume = 0.6f)
        {
            var c = Get(id);
            if (c != null) GameAudio.PlayClipAt(c, at, volume, 12f);
        }

        /// <summary>Looping 3D source on <paramref name="host"/> (record crackle, fire hiss), starts silent.</summary>
        public static AudioSource Loop(Transform host, Id id, float range = 6f)
        {
            var s = host.gameObject.AddComponent<AudioSource>();
            s.clip = Get(id);
            s.loop = true;
            s.spatialBlend = 1f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = 0.6f;
            s.maxDistance = range;
            s.volume = 0f;
            s.playOnAwake = false;
            s.dopplerLevel = 0f;
            return s;
        }

        public static AudioClip Get(Id id)
        {
            if (Clips.TryGetValue(id, out var c) && c != null) return c;
            var rnd = new System.Random(17 + (int)id);
            float N() => (float)rnd.NextDouble() * 2f - 1f;
            float[] d;
            switch (id)
            {
                case Id.Click:
                {
                    d = new float[(int)(Rate * 0.09f)];
                    for (int i = 0; i < d.Length; i++)
                    {
                        float t = i / (float)Rate;
                        float a = Mathf.Exp(-t * 160f) + 0.5f * Mathf.Exp(-Mathf.Max(0f, t - 0.035f) * 220f) * (t > 0.035f ? 1f : 0f);
                        d[i] = (N() * 0.6f + Mathf.Sin(t * 2f * Mathf.PI * 2400f) * 0.4f) * a * 0.7f;
                    }
                    break;
                }
                case Id.Thunk:
                case Id.Clunk:
                {
                    float f = id == Id.Thunk ? 180f : 95f;
                    d = new float[(int)(Rate * 0.22f)];
                    float lp = 0f;
                    for (int i = 0; i < d.Length; i++)
                    {
                        float t = i / (float)Rate;
                        lp += (N() - lp) * 0.25f;
                        d[i] = (Mathf.Sin(t * 2f * Mathf.PI * f * (1f - t)) * Mathf.Exp(-t * 28f) + lp * Mathf.Exp(-t * 90f) * 1.4f) * 0.8f;
                    }
                    break;
                }
                case Id.Sprinkle:
                {
                    d = new float[(int)(Rate * 1.0f)];
                    for (int k = 0; k < 26; k++)
                    {
                        int at = (int)(rnd.NextDouble() * (d.Length - 900));
                        float amp = 0.15f + (float)rnd.NextDouble() * 0.25f;
                        for (int i = 0; i < 700; i++) d[at + i] += N() * amp * Mathf.Exp(-i / 90f);
                    }
                    break;
                }
                case Id.Whirr:
                {
                    d = new float[(int)(Rate * 1.6f)];
                    float lp = 0f;
                    for (int i = 0; i < d.Length; i++)
                    {
                        float t = i / (float)Rate;
                        lp += (N() - lp) * 0.08f;
                        float wob = 0.6f + 0.4f * Mathf.Sin(t * 2f * Mathf.PI * (14f - t * 6f));
                        d[i] = lp * wob * Mathf.Exp(-t * 1.6f) * 1.6f * Mathf.Clamp01(t * 30f);
                    }
                    break;
                }
                case Id.Crackle:
                {
                    d = new float[Rate * 3];
                    float hum = 0f;
                    for (int i = 0; i < d.Length; i++)
                    {
                        float t = i / (float)Rate;
                        hum = Mathf.Sin(t * 2f * Mathf.PI * 50f) * 0.015f + N() * 0.02f;
                        d[i] = hum;
                        if (rnd.NextDouble() < 0.0012) { float a = 0.3f + (float)rnd.NextDouble() * 0.5f; for (int k = 0; k < 40 && i + k < d.Length; k++) d[i + k] += N() * a * Mathf.Exp(-k / 6f); }
                        // slow rumble of the record turning (once per 1.8 s)
                        d[i] += Mathf.Sin(t * 2f * Mathf.PI * 0.555f) * 0.01f;
                    }
                    break;
                }
                default: // FireLoop
                {
                    d = new float[Rate * 3];
                    float lp = 0f, lp2 = 0f;
                    for (int i = 0; i < d.Length; i++)
                    {
                        lp += (N() - lp) * 0.06f;
                        lp2 += (lp - lp2) * 0.2f;
                        d[i] = lp2 * 0.35f;
                        if (rnd.NextDouble() < 0.0004) { float a = 0.25f; for (int k = 0; k < 60 && i + k < d.Length; k++) d[i + k] += N() * a * Mathf.Exp(-k / 10f); }
                    }
                    // seamless loop
                    int fade = 800;
                    for (int i = 0; i < fade; i++) { float w = i / (float)fade; d[i] = d[i] * w + d[d.Length - fade + i] * (1f - w); }
                    System.Array.Resize(ref d, d.Length - fade);
                    break;
                }
            }
            c = AudioClip.Create("LobbyProp_" + id, d.Length, 1, Rate, false);
            c.SetData(d, 0);
            Clips[id] = c;
            return c;
        }
    }
}
