using System;
using HideAndSeek.Characters;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Easter egg "Toyota Yaris Luna" ("why this dealer"): at the start of every round the server rolls
    /// <see cref="Chance"/>; on a hit ALL RC cars of that round are toy blue Yarises 2010 (shelf, hand, floor) -
    /// <see cref="MatchManager.YarisRound"/> is synced to everybody (late joiners too). On the floor / while driven the
    /// Yaris squashes and stretches to the beat of its own music (<see cref="YarisCarFx"/>, <see cref="YarisMusic"/>).
    /// Test switch: -testyaris = 100 %.
    /// </summary>
    public static class YarisEgg
    {
        /// <summary>Chance per round that the RC cars are Yarises (0..1).</summary>
        public const float Chance = 0.2f;

        /// <summary>Tempo of the squash when no music file is present or its tempo cannot be detected.</summary>
        public const float Bpm = 120f;

        /// <summary>Detect the tempo (and the first beat) from the music file's loudness envelope.</summary>
        public const bool DetectBpm = true;

        /// <summary>Music file: Assets/_Project/Game/Resources/Audio/YarisDealer.(mp3|ogg|wav).</summary>
        public const string MusicResource = "Audio/YarisDealer";

        /// <summary>Music: loudness (0..1 before the game's effects / music sliders) and audible range in metres.</summary>
        public const float MusicVolume = 0.9f, MusicMinDistance = 1.6f, MusicMaxDistance = 24f;

        public static bool ForceTest => Array.IndexOf(Environment.GetCommandLineArgs(), "-testyaris") >= 0;

        /// <summary>This round's RC cars are Yarises (every peer, from the synced round state).</summary>
        public static bool Active
        {
            get
            {
                var mm = MatchManager.Instance;
                return mm != null && mm.IsSpawned && mm.YarisRound.Value && !mm.RaceMode;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            RcCarSkin.YarisWanted = () => Active;
            var go = new GameObject("[YarisEgg]");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<YarisEggRunner>();
        }
    }

    /// <summary>Gives the two shelf cars of the game scene their dance / music component.</summary>
    internal class YarisEggRunner : MonoBehaviour
    {
        private readonly YarisCarFx[] _fx = new YarisCarFx[RcCar.Count];

        private void Update()
        {
            for (int i = 0; i < RcCar.Count; i++)
            {
                var car = RcCar.Peek(i);
                if (car == null) { _fx[i] = null; continue; }
                if (_fx[i] != null && _fx[i].gameObject == car.gameObject) continue;
                var fx = car.GetComponent<YarisCarFx>();
                _fx[i] = fx != null ? fx : car.gameObject.AddComponent<YarisCarFx>();
            }
        }
    }
}
