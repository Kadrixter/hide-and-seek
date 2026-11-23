using HideAndSeek.Audio;
using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Sound of the map radios: fully 3D from the radio (so a seeker can walk towards the music), linear fall-off to
    /// <see cref="MaxDistance"/> m; another floor = <see cref="FloorAcoustics"/> (silent unless both are at a ramp /
    /// stairwell, then muffled); walls in between muffle it a little. The carrier hears it partly 2D (it sits next to
    /// his ear - fully 3D would be in one ear only). A plastic clunk when it is put down / lands.
    /// </summary>
    public partial class MapRadioSystem
    {
        public const float MinDistance = 2.2f, MaxDistance = 30f;
        private static AudioClip _clunk;

        private static void SetupAudio(View v)
        {
            var src = v.Box.Source;
            if (src == null) return;
            src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = MinDistance;
            src.maxDistance = MaxDistance;
            src.spread = 70f;
            src.dopplerLevel = 0f;
            v.LowPass = v.Box.gameObject.AddComponent<AudioLowPassFilter>(); // after BoomboxAudio: filters the music
            v.LowPass.cutoffFrequency = 22000f;
        }

        private void UpdateAudio(MatchManager mm, int n)
        {
            var l = ActiveListener.Find();
            Vector3 lp = l != null ? l.transform.position : Vector3.zero;
            var me = NetPlayer.Local;
            for (int i = 0; i < n; i++)
            {
                var v = _views[i];
                if (v.Box == null || v.Box.Source == null) continue;
                var s = mm.MapRadios[i];
                Vector3 pos = v.HasPose ? v.Pos + Vector3.up * 0.15f : s.Pos;
                bool mine = me != null && s.Mode == MapRadioState.Carried && s.Carrier == me.NetworkObjectId;
                v.Box.Source.spatialBlend = mine ? 0.45f : 1f;
                if (l != null && Time.time >= v.NextAudio)
                {
                    v.NextAudio = Time.time + 0.15f;
                    v.FloorTarget = 1f;
                    v.Cutoff = 22000f;
                    float occ = mine ? 0f : AudioOcclusion.Sample(lp, pos, l.transform.root, v.Box.transform, GameAudio.OcclusionMask);
                    if (FloorAcoustics.DifferentFloor(lp, pos)) v.FloorTarget = FloorAcoustics.Transfer(lp, pos, occ <= 0f, out v.Cutoff);
                    v.Occlusion = occ;
                }
                v.FloorGain = Mathf.MoveTowards(v.FloorGain, v.FloorTarget, Time.deltaTime * 3f);
                v.Box.VolumeScale = v.FloorGain * (1f - 0.35f * Mathf.Clamp01(v.Occlusion));
                float cut = Mathf.Min(v.Occlusion > 0.5f ? 1800f : v.Occlusion > 0.1f ? 5000f : 22000f, v.FloorGain < 0.99f ? v.Cutoff : 22000f);
                if (v.LowPass != null) v.LowPass.cutoffFrequency = Mathf.Lerp(v.LowPass.cutoffFrequency, cut, 0.25f);
            }
        }

        /// <summary>Plastic clunk of the radio landing on concrete (synthesised once: thump + rattle).</summary>
        private static void PlayClunk(Vector3 pos, float volume)
        {
            if (_clunk == null)
            {
                int rate = 44100, len = (int)(rate * 0.32f);
                var d = new float[len];
                var rng = new System.Random(7);
                float lp = 0f;
                for (int i = 0; i < len; i++)
                {
                    float t = i / (float)rate;
                    float thump = Mathf.Sin(2f * Mathf.PI * (105f - 40f * t) * t) * Mathf.Exp(-t * 26f);
                    float noise = (float)(rng.NextDouble() * 2 - 1);
                    lp += (noise - lp) * 0.35f;
                    float click = lp * Mathf.Exp(-t * 60f) * 0.8f;
                    float rattle = (float)(rng.NextDouble() * 2 - 1) * Mathf.Exp(-(t - 0.07f) * (t - 0.07f) * 2500f) * 0.25f;
                    float ring = Mathf.Sin(2f * Mathf.PI * 820f * t) * Mathf.Exp(-t * 40f) * 0.12f;
                    d[i] = Mathf.Clamp((thump * 0.9f + click + rattle + ring) * 0.8f, -1f, 1f);
                }
                _clunk = AudioClip.Create("radio-clunk", len, 1, rate, false);
                _clunk.SetData(d, 0);
            }
            GameAudio.PlayClipAt(_clunk, pos + Vector3.up * 0.1f, volume, 22f);
        }
    }
}
