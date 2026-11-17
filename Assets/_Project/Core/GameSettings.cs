using System;
using UnityEngine;

namespace HideAndSeek.Core
{
    [Flags]
    public enum HintMode
    {
        None = 0,
        Sound = 1,
        Minimap = 2,
        Both = Sound | Minimap
    }

    /// <summary>Round settings configured in the lobby before the match starts.</summary>
    [Serializable]
    public class GameSettings
    {
        public const int MinPlayers = 2;

        [Tooltip("Seconds hiders have to hide.")]
        public float hideTime = 30f;

        [Tooltip("Seconds the seeker has to find everyone.")]
        public float seekTime = 600f;

        [Tooltip("Seconds into the seek phase when hints start. Negative = half of seekTime.")]
        public float hintStartTime = -1f;

        [Tooltip("Seconds between hints.")]
        public float hintInterval = 60f;

        public HintMode hintMode = HintMode.Both;

        public float EffectiveHintStart => hintStartTime < 0f ? seekTime * 0.5f : hintStartTime;
    }
}
