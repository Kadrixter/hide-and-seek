using System.Collections.Generic;
using HideAndSeek.Audio;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// 8-bit SNAKE sounds synthesised once at runtime (pulse waves + noise, 22 kHz) and played from the arcade screen
    /// through <see cref="GameAudio"/> (3D, walls and floors muffle them like any other sound).
    /// </summary>
    public static class SnakeSounds
    {
        public enum Id { Coin, Go, Eat, BonusSpawn, BonusEat, BonusGone, LevelUp, Turn, Die, RecordDing, RecordFanfare }

        private const int Rate = 22050;
        private static readonly Dictionary<Id, AudioClip> Clips = new();
        private static readonly List<(Id id, float at, int cab)> Pending = new();

        private static readonly Dictionary<Id, float> Volume = new()
        {
            { Id.Coin, 0.55f }, { Id.Go, 0.45f }, { Id.Eat, 0.5f }, { Id.BonusSpawn, 0.4f }, { Id.BonusEat, 0.55f }, { Id.BonusGone, 0.3f },
            { Id.LevelUp, 0.45f }, { Id.Turn, 0.12f }, { Id.Die, 0.6f }, { Id.RecordDing, 0.5f }, { Id.RecordFanfare, 0.6f },
        };

        public static void Play(Id id, float delay = 0f, int cab = 0)
        {
            if (delay > 0f) { Pending.Add((id, Time.time + delay, cab)); return; }
            var clip = Get(id);
            if (clip != null) GameAudio.PlayClipAt(clip, ArcadeCabs.Screen(cab), Volume[id], 14f);
        }

        /// <summary>Called every frame by the arcade (delayed sounds such as the record fanfare after GAME OVER).</summary>
        public static void Tick()
        {
            for (int i = Pending.Count - 1; i >= 0; i--)
                if (Time.time >= Pending[i].at)
                {
                    var (id, _, cab) = Pending[i];
                    Pending.RemoveAt(i);
                    Play(id, 0f, cab);
                }
        }

        public static void PlayEvents(byte evt, byte state, bool newRecord, int cab = 0)
        {
            if (evt == 0) return;
            if ((evt & ArcadeState.EvDie) != 0)
            {
                Pending.RemoveAll(p => p.cab == cab);
                Play(Id.Die, 0f, cab);
                if (newRecord) Play(Id.RecordFanfare, 0.95f, cab);
                return;
            }
            if ((evt & ArcadeState.EvStart) != 0) Play(state == ArcadeState.Ready ? Id.Coin : Id.Go, 0f, cab);
            if ((evt & ArcadeState.EvBonusEat) != 0) Play(Id.BonusEat, 0f, cab);
            else if ((evt & ArcadeState.EvEat) != 0) Play(Id.Eat, 0f, cab);
            if ((evt & ArcadeState.EvLevelUp) != 0) Play(Id.LevelUp, 0.12f, cab);
            if ((evt & ArcadeState.EvBonusSpawn) != 0) Play(Id.BonusSpawn, 0.08f, cab);
            if ((evt & ArcadeState.EvBonusGone) != 0) Play(Id.BonusGone, 0f, cab);
            if ((evt & (ArcadeState.EvTurn | ArcadeState.EvEat | ArcadeState.EvBonusEat)) == ArcadeState.EvTurn) Play(Id.Turn, 0f, cab);
        }

        // ================================================================ synthesis

        private static AudioClip Get(Id id)
        {
            if (Clips.TryGetValue(id, out var c) && c != null) return c;
            var data = new List<float>();
            switch (id)
            {
                case Id.Coin: Note(data, 988, 0.07f, 0.5f); Note(data, 1319, 0.28f, 0.5f, decay: 6f); break;
                case Id.Go: Note(data, 523, 0.05f, 0.25f); Note(data, 784, 0.09f, 0.25f, decay: 10f); break;
                case Id.Eat: Sweep(data, 520, 1250, 0.07f, 0.25f); break;
                case Id.BonusSpawn:
                    for (int r = 0; r < 2; r++) foreach (float f in new[] { 1319f, 1568f, 1976f, 2637f }) Note(data, f, 0.035f, 0.125f);
                    break;
                case Id.BonusEat:
                    foreach (float f in new[] { 1047f, 1319f, 1568f, 2093f }) Note(data, f, 0.045f, 0.5f);
                    Note(data, 2637, 0.16f, 0.5f, decay: 8f);
                    break;
                case Id.BonusGone: Sweep(data, 900, 260, 0.16f, 0.5f); break;
                case Id.LevelUp: Note(data, 784, 0.07f, 0.125f); Note(data, 1047, 0.07f, 0.125f); Note(data, 1568, 0.16f, 0.125f, decay: 6f); break;
                case Id.Turn: Note(data, 220, 0.02f, 0.5f, decay: 60f); break;
                case Id.Die:
                    Sweep(data, 760, 90, 0.5f, 0.5f, noise: 0.35f);
                    Note(data, 98, 0.14f, 0.5f); Note(data, 73, 0.3f, 0.5f, decay: 4f);
                    break;
                case Id.RecordDing: Note(data, 1319, 0.06f, 0.25f); Note(data, 1760, 0.06f, 0.25f); Note(data, 2637, 0.18f, 0.25f, decay: 7f); break;
                case Id.RecordFanfare:
                    foreach (float f in new[] { 523f, 659f, 784f, 1047f }) Note(data, f, 0.09f, 0.5f);
                    Note(data, 784, 0.09f, 0.5f);
                    Note(data, 1047, 0.45f, 0.5f, decay: 2.5f, vibrato: 6f);
                    break;
            }
            c = AudioClip.Create("Snake" + id, data.Count, 1, Rate, false);
            c.SetData(data.ToArray(), 0);
            Clips[id] = c;
            return c;
        }

        /// <summary>Pulse wave note with a short attack, exponential decay and 4-bit-ish quantisation.</summary>
        private static void Note(List<float> d, float freq, float dur, float duty, float decay = 12f, float vibrato = 0f)
        {
            int n = (int)(dur * Rate);
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float f = freq * (1f + (vibrato > 0f ? 0.012f * Mathf.Sin(t * vibrato * Mathf.PI * 2f) : 0f));
                phase += f / Rate;
                float wave = (phase % 1.0) < duty ? 1f : -1f;
                float env = Mathf.Min(1f, i / (Rate * 0.003f)) * Mathf.Exp(-t * decay * 0.25f) * Mathf.Min(1f, (n - i) / (Rate * 0.004f));
                d.Add(Quant(wave * env * 0.5f));
            }
        }

        private static void Sweep(List<float> d, float from, float to, float dur, float duty, float noise = 0f)
        {
            int n = (int)(dur * Rate);
            double phase = 0;
            uint lfsr = 0xACE1u;
            float hold = 0f;
            for (int i = 0; i < n; i++)
            {
                float k = (float)i / n;
                phase += Mathf.Lerp(from, to, k) / Rate;
                float wave = (phase % 1.0) < duty ? 1f : -1f;
                if (noise > 0f && i % 6 == 0)
                {
                    lfsr = (lfsr >> 1) ^ (uint)(-(int)(lfsr & 1u) & 0xB400u);
                    hold = (lfsr & 1u) != 0 ? 1f : -1f;
                }
                float env = Mathf.Min(1f, i / (Rate * 0.003f)) * (1f - k * 0.6f) * Mathf.Min(1f, (n - i) / (Rate * 0.004f));
                d.Add(Quant((wave * (1f - noise) + hold * noise) * env * 0.5f));
            }
        }

        private static float Quant(float v) => Mathf.Round(v * 15f) / 15f;
    }
}
