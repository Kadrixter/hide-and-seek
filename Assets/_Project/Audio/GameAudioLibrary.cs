using System;
using UnityEngine;

namespace HideAndSeek.Audio
{
    [Serializable]
    public class SfxEntry
    {
        public GameSfx id;
        public AudioClip[] clips = Array.Empty<AudioClip>();
        [Range(0f, 2f)] public float volume = 1f;
        [Tooltip("Random pitch +- fraction.")]
        [Range(0f, 0.3f)] public float pitchJitter = 0.05f;
        [Tooltip("Distance (m) at which the 3D sound fades out.")]
        public float range = 25f;
        [Tooltip("Adds an echo (AudioEchoFilter) for loud sounds in big spaces.")]
        public bool echo;
        [Tooltip("Send into the global reverb (AudioEnvironment). 0 = dry, 1 = full, up to 1.1.")]
        [Range(0f, 1.1f)] public float reverbMix = 1f;
    }

    [Serializable]
    public class AmbientEntry
    {
        public AmbientId id;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 0.5f;
        public float range = 15f;
        [Tooltip("Random pitch +- fraction for 3D emitters (so neighbouring lamps/vents do not phase).")]
        [Range(0f, 0.2f)] public float pitchJitter = 0.04f;
    }

    /// <summary>All gameplay sounds. Lives at Resources/GameAudioLibrary.asset.</summary>
    [CreateAssetMenu(menuName = "HideAndSeek/Audio/Game Audio Library", fileName = "GameAudioLibrary")]
    public class GameAudioLibrary : ScriptableObject
    {
        public const string ResourcePath = "GameAudioLibrary";

        public SfxEntry[] sfx = Array.Empty<SfxEntry>();
        public AmbientEntry[] ambient = Array.Empty<AmbientEntry>();

        public SfxEntry Get(GameSfx id)
        {
            for (int i = 0; i < sfx.Length; i++)
                if (sfx[i] != null && sfx[i].id == id) return sfx[i];
            return null;
        }

        public AmbientEntry Get(AmbientId id)
        {
            for (int i = 0; i < ambient.Length; i++)
                if (ambient[i] != null && ambient[i].id == id) return ambient[i];
            return null;
        }
    }
}
