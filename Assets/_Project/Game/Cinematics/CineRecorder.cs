using System.Collections.Generic;
using System.IO;
using HideAndSeek.Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HideAndSeek.Game.Cinematics
{
    /// <summary>
    /// Tests: records a cinematic as numbered PNG frames at a fixed rate (Time.captureFramerate, the clock of the
    /// directors follows local frame time) and the 2D sounds it played as a mono WAV, for ffmpeg.
    /// </summary>
    public static class CineRecorder
    {
        static readonly List<(AudioClip clip, float vol, float at)> _sounds = new();
        public static bool Logging;

        public static void LogSound(AudioClip c, float vol, float at)
        {
            if (Logging && c != null) _sounds.Add((c, vol, at));
        }

        public static void Begin()
        {
            _sounds.Clear();
            Logging = true;
        }

        /// <summary>Writes the logged sounds (times relative to the cinematic start, shifted by <paramref name="lead"/>).</summary>
        public static void WriteWav(string path, float length, float lead)
        {
            Logging = false;
            int rate = CineSounds.Rate;
            var mix = new float[(int)(length * rate)];
            foreach (var (clip, vol, at) in _sounds)
            {
                var d = new float[clip.samples * clip.channels];
                if (!clip.GetData(d, 0)) continue;
                int ch = clip.channels;
                double step = clip.frequency / (double)rate;
                int o = (int)((at + lead) * rate);
                for (int i = 0; o + i < mix.Length; i++)
                {
                    int src = (int)(i * step) * ch;
                    if (src >= d.Length) break;
                    if (o + i >= 0) mix[o + i] += d[src] * vol;
                }
            }
            float peak = 0.001f;
            foreach (float v in mix) peak = Mathf.Max(peak, Mathf.Abs(v));
            if (peak > 0.97f) for (int i = 0; i < mix.Length; i++) mix[i] *= 0.97f / peak;
            File.WriteAllBytes(path, CineSounds.Wav(mix));
            Debug.Log($"[CineTest] wav {path} ({_sounds.Count} sounds, peak {peak:0.00})");
        }
    }

    /// <summary>Black that fades away over the gameplay view once a cinematic gave the screen back.</summary>
    public sealed class CineFade : MonoBehaviour
    {
        Image _img;
        float _t, _len;

        public static void FromBlack(float seconds)
        {
            var canvas = UiKit.CreateCanvas("CineFade", 419);
            var f = canvas.gameObject.AddComponent<CineFade>();
            f._img = UiKit.Image("Black", canvas.transform, Color.black);
            f._img.rectTransform.Stretch();
            f._img.raycastTarget = false;
            f._len = Mathf.Max(0.05f, seconds);
        }

        void Update()
        {
            _t += Time.deltaTime;
            _img.color = new Color(0f, 0f, 0f, 1f - Mathf.SmoothStep(0f, 1f, _t / _len));
            if (_t >= _len) Destroy(gameObject);
        }
    }
}
