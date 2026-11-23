namespace HideAndSeek.Characters
{
    /// <summary>Kind of a contact moment in an emote (the game plays a sound for it).</summary>
    public enum EmoteBeat : byte
    {
        Clap,
        Slap,     // high five
        Bump,     // fist bump
        Pat,      // pat on the back
        Grip,     // hands grab (handshake, dance)
        Snap,     // robot stop
        Stomp,    // foot down
        Sit,      // bottom on the floor
    }

    /// <summary>
    /// Shared timing of the emotes (seconds from the start). Body poses (<see cref="BlockyCharacterActions"/>) and the
    /// game (sounds, server end of pair emotes) read the same table.
    /// </summary>
    public static class EmoteTimeline
    {
        // ------------------------------------------------------------------ solo
        public const float WaveEnd = 2.8f;

        public const float ClapEnd = 3.3f;
        /// <summary>Moments the palms meet.</summary>
        public static readonly float[] ClapTimes = { 0.6f, 0.9f, 1.2f, 1.5f, 1.8f, 2.1f, 2.4f };

        public const float LaughEnd = 3.6f;
        public const float FacepalmEnd = 3.3f;
        public const float ShrugEnd = 2.3f;
        public const float SaluteEnd = 2.9f;
        public const float PointEnd = 2.8f;

        public const float DiscoPeriod = 2f;
        public const float RobotPeriod = 2.4f;
        public static readonly float[] RobotSnaps = { 0f, 0.3f, 0.6f, 0.9f, 1.2f, 1.5f, 1.8f, 2.1f };
        public const float RoofPeriod = 2f;

        /// <summary>Sitting down on the floor (then the loop), and getting up after the stop.</summary>
        public const float SitEnter = 1.5f, SitExit = 1.35f, SitTouch = 0.95f;

        // ------------------------------------------------------------------ pair
        public const float HugEnd = 4.4f, HugDistance = 0.62f;
        public static readonly float[] HugPats = { 2.0f, 2.32f };

        public const float FiveEnd = 2.3f, FiveHit = 0.78f, FiveDistance = 0.98f;
        public const float BumpEnd = 2.7f, BumpHit = 0.95f, BumpDistance = 0.92f;
        public const float ShakeEnd = 3.1f, ShakeGrip = 0.66f, ShakeRelease = 2.15f, ShakeDistance = 0.84f;

        public const float PairDancePeriod = 4f, PairDanceDistance = 0.74f;
        /// <summary>Within the dance loop: the follower spins under the raised hands.</summary>
        public const float SpinStart = 2f, SpinEnd = 3f;

        /// <summary>Contact moments of an emote (one-shot: absolute; loop: within one period).</summary>
        public static float[] Beats(EmoteId id, out EmoteBeat kind)
        {
            switch (id)
            {
                case EmoteId.Clap: kind = EmoteBeat.Clap; return ClapTimes;
                case EmoteId.DanceRobot: kind = EmoteBeat.Snap; return RobotSnaps;
                case EmoteId.SitDown: kind = EmoteBeat.Sit; return SitBeats;
                case EmoteId.Hug: kind = EmoteBeat.Pat; return HugPats;
                case EmoteId.HighFive: kind = EmoteBeat.Slap; return FiveBeats;
                case EmoteId.FistBump: kind = EmoteBeat.Bump; return BumpBeats;
                case EmoteId.Handshake: kind = EmoteBeat.Grip; return ShakeBeats;
                case EmoteId.PairDance: kind = EmoteBeat.Grip; return DanceBeats;
                default: kind = EmoteBeat.Clap; return None;
            }
        }

        static readonly float[] None = { };
        static readonly float[] SitBeats = { SitTouch };
        static readonly float[] FiveBeats = { FiveHit };
        static readonly float[] BumpBeats = { BumpHit };
        static readonly float[] ShakeBeats = { ShakeGrip };
        static readonly float[] DanceBeats = { SpinEnd + 0.05f };
    }
}
