namespace HideAndSeek.Characters
{
    /// <summary>
    /// Timings of the CORE part of every kill variant (the actual killing blows), in seconds from the start of the
    /// core. A full kill = intro (struggle) + core + outro (aftermath), see <see cref="KillMoves"/>; the public table
    /// there shifts these values by the intro length. Only the pose functions of the core read this class.
    /// </summary>
    public static class KillCore
    {
        public static readonly float[] ThroatHits = { 0.82f, 1.65f };
        public static readonly float[] OverheadHits = { 0.88f, 1.55f };
        public static readonly float[] GutHits = { 0.68f, 1.45f, 2.05f };
        public static readonly float[] FinisherHits = { 0.6f, 0.95f, 1.3f, 2.75f };
        public static readonly float[] ChainsawHits = { 0.5f, 1.1f, 1.7f, 2.3f };
        public static readonly float[] ImpaleHits = { 0.8f, 1.4f };
        public static readonly float[] ChopHits = { 0.85f, 1.2f, 1.55f };
        public static readonly float[] SweepHits = { 0.85f, 1.8f };
        public static readonly float[] UpperHits = { 0.7f, 1.0f, 1.3f, 1.6f };
        public static readonly float[] NeckHits = { 0.85f, 1.2f };
        public static readonly float[] SawFinisherHits = { 1.0f, 2.1f, 3.2f, 4.3f };
        public static readonly float[] GlockFinisherHits = { 0.75f, 1.9f, 3.6f };

        public const float GlockFinisherVictimDead = 4.5f;
        public const float GlockFinisherLowerTime = 4.3f;
        public const float GlockFinisherTauntTime = 4.9f;

        public const float ChainsawGrindStart = 0.5f, ChainsawGrindEnd = 2.35f;
        public const float ChainsawVictimDead = 3.7f;

        public const float FinisherVictimDead = 4.7f;
        public const float FinisherFlipTime = 4.3f;
        public const float FinisherTauntTime = 5.1f;

        public const float SawFinisherVictimDead = 5.9f;
        public const float SawFinisherTauntTime = 5.5f;

        public static float[] HitTimes(KillMove m) => m switch
        {
            KillMove.ThroatSlit => ThroatHits,
            KillMove.Overhead => OverheadHits,
            KillMove.GutKnee => GutHits,
            KillMove.Finisher => FinisherHits,
            KillMove.Chainsaw => ChainsawHits,
            KillMove.Impale => ImpaleHits,
            KillMove.SawChop => ChopHits,
            KillMove.SawSweep => SweepHits,
            KillMove.SawUpper => UpperHits,
            KillMove.SawNeck => NeckHits,
            KillMove.SawFinisher => SawFinisherHits,
            KillMove.GlockFinisher => GlockFinisherHits,
            _ => ActionTimeline.KnifeStabTimes,
        };

        public static float SeekerDuration(KillMove m) => m switch
        {
            KillMove.ThroatSlit => 2.6f,
            KillMove.Overhead => 2.5f,
            KillMove.GutKnee => 2.8f,
            KillMove.Finisher => 7.6f,
            KillMove.Chainsaw => 4.4f,
            KillMove.Impale => 2.7f,
            KillMove.SawChop => 3.6f,
            KillMove.SawSweep => 3.4f,
            KillMove.SawUpper => 3.6f,
            KillMove.SawNeck => 3.4f,
            KillMove.SawFinisher => 7.6f,
            KillMove.GlockFinisher => 6.9f,
            _ => ActionTimeline.KnifeAttackDuration,
        };

        public static float VictimDuration(KillMove m) => m switch
        {
            KillMove.ThroatSlit => 3.4f,
            KillMove.Overhead => 3.3f,
            KillMove.GutKnee => 3.4f,
            KillMove.Finisher => FinisherVictimDead,
            KillMove.Chainsaw => ChainsawVictimDead,
            KillMove.Impale => 3.5f,
            KillMove.SawChop => 3.3f,
            KillMove.SawSweep => 3.3f,
            KillMove.SawUpper => 3.2f,
            KillMove.SawNeck => 3.4f,
            KillMove.SawFinisher => SawFinisherVictimDead,
            KillMove.GlockFinisher => GlockFinisherVictimDead,
            _ => ActionTimeline.StabbedReactionDuration,
        };

        /// <summary>Length of the core window (both sides done).</summary>
        public static float Length(KillMove m) => System.Math.Max(SeekerDuration(m), VictimDuration(m));

        /// <summary>When the victim hits the floor in the core.</summary>
        public static float FallTime(KillMove m) => m switch
        {
            KillMove.ThroatSlit => 3.05f,
            KillMove.Overhead => 2.75f,
            KillMove.GutKnee => 2.95f,
            KillMove.Finisher => 4.15f,
            KillMove.Chainsaw => 3.2f,
            KillMove.Impale => 3.0f,
            KillMove.SawChop => 2.7f,
            KillMove.SawSweep => 2.8f,
            KillMove.SawUpper => 2.6f,
            KillMove.SawNeck => 2.6f,
            KillMove.SawFinisher => 5.2f,
            KillMove.GlockFinisher => 4.25f,
            _ => ActionTimeline.StabbedReactionDuration - 0.3f,
        };
    }
}
