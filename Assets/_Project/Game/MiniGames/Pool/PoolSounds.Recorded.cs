using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// Recorded pool sounds (ElevenLabs sound effects, Resources/Pool/&lt;name&gt;_&lt;n&gt;.wav, several takes per
    /// sound picked at random so repeated hits never sound the same). Anything missing falls back to the synthesised
    /// clip of <see cref="PoolSounds"/>. Names: click (ball on ball), strike (cue tip), break (the rack bursting),
    /// cushion, pocket, clink (ball landing on the return's metal rods), tray (knocking into the balls in the tray),
    /// roll (loop, balls on cloth), railroll (loop, a ball running down the metal rods).
    /// </summary>
    public static partial class PoolSounds
    {
        const int MaxTakes = 8;
        static readonly Dictionary<string, AudioClip[]> Takes = new();
        static int _lastPick = -1;

        /// <summary>All recorded takes of <paramref name="name"/> (empty when none is in the build).</summary>
        public static AudioClip[] Recorded(string name)
        {
            if (Takes.TryGetValue(name, out var set)) return set;
            var list = new List<AudioClip>();
            for (int i = 0; i < MaxTakes; i++)
            {
                var c = Resources.Load<AudioClip>($"Pool/{name}_{i}");
                if (c == null) break;
                list.Add(c);
            }
            Debug.Log($"[PoolSounds] {name}: {list.Count} recorded take(s){(list.Count == 0 ? " - synthesised fallback" : "")}");
            return Takes[name] = list.ToArray();
        }

        /// <summary>A random recorded take of <paramref name="name"/> (not the one just played), else <paramref name="fallback"/>.</summary>
        public static AudioClip Pick(string name, AudioClip fallback)
        {
            var set = Recorded(name);
            if (set.Length == 0) return fallback;
            if (set.Length == 1) return set[0];
            int i = Random.Range(0, set.Length);
            if (i == _lastPick) i = (i + 1) % set.Length;
            _lastPick = i;
            return set[i];
        }

        /// <summary>The first recorded take of a loop, else <paramref name="fallback"/>.</summary>
        public static AudioClip Loop(string name, AudioClip fallback)
        {
            var set = Recorded(name);
            return set.Length > 0 ? set[0] : fallback;
        }

        public static bool HasRecorded(string name) => Recorded(name).Length > 0;
    }
}
