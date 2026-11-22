using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>SNAKE tuning shared by the server simulation and the screen.</summary>
    public static class SnakeRules
    {
        /// <summary>Playfield in cells.</summary>
        public const int W = 32, H = 21;
        public const int StartLength = 4;
        /// <summary>Points for the golden bonus food; it appears after every <see cref="BonusEvery"/> normal foods.</summary>
        public const int BonusPoints = 5, BonusEvery = 5;
        public const float BonusTime = 6.5f;
        /// <summary>Turns that can be queued ahead (quick "up, left" in one step both count).</summary>
        public const int TurnBuffer = 3;
        public const int MaxLevel = 10;

        /// <summary>Level 1 at 0 points, +1 every 5 points.</summary>
        public static int Level(int score) => Mathf.Clamp(1 + score / 5, 1, MaxLevel);

        /// <summary>Seconds per step: 0.16 s at level 1 down to ~0.06 s at level 10.</summary>
        public static float Step(int level) => Mathf.Max(0.06f, 0.16f - (level - 1) * 0.011f);
    }
}
