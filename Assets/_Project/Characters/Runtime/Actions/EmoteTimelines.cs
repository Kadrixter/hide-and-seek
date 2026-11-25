namespace HideAndSeek.Characters
{
    /// <summary>Kind of a contact moment in an emote (the game plays a sound for it).</summary>
    public enum EmoteBeat : byte
    {
        Clap,
        Slap,     // high five
        Bump,     // fist / elbow / hip bump
        Pat,      // pat on the back
        Grip,     // hands grab (handshake, dance)
        Snap,     // short click
        Stomp,    // foot down
        Sit,      // bottom on the floor
    }

    /// <summary>
    /// Shared timing of the emotes (seconds from the start). Body poses (<see cref="BlockyCharacterActions"/>) and the
    /// game (sounds, server end of pair emotes, tests) read the same table.
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
        public const float PointEnd = 2.8f;

        public const float DiscoPeriod = 2f;
        public const float RoofPeriod = 2f;

        /// <summary>Sitting down on the floor (then the loop), and getting up after the stop.</summary>
        public const float SitEnter = 1.5f, SitExit = 1.35f, SitTouch = 0.95f;

        /// <summary>Floss: one swing of the arms (hips the other way); a loop is two swings.</summary>
        public const float FlossSwing = 0.4f, FlossPeriod = 2f * FlossSwing, FlossThumb = 0.22f;

        /// <summary>Breakdance: toprock and the drop (way in), then a loop: back spin, one-hand freeze, back to the crouch.</summary>
        public const float BreakIn = 1.6f, BreakPeriod = 4.6f, BreakExit = 1.2f, BreakThumb = BreakIn + 3.6f;
        /// <summary>Within the loop (seconds after <see cref="BreakIn"/>): crouch, onto the back, spin, sit up to the crouch, kick up into the freeze, crouch.</summary>
        public const float BreakSpinStart = 0.5f, BreakSpinEnd = 2.3f, BreakFreezeIn = 3.2f, BreakFreezeOut = 4.1f;

        /// <summary>Push-ups: down into the plank, 3 push-ups, 3 one-arm push-ups, up, biceps flex.</summary>
        public const float PushPlank = 1.3f, PushRep = 0.8f, PushOneArm = 4.2f, PushOneArmEnd = 6.6f, PushUp = 8.1f,
            PushEnd = 10.4f, PushExit = 1.0f, PushThumb = PushOneArm + PushRep + 0.42f;

        // ------------------------------------------------------------------ pair
        public const float HugEnd = 4.4f, HugDistance = 0.62f;
        public static readonly float[] HugPats = { 2.0f, 2.32f };

        /// <summary>High five: bounce and wind-up, a jump into the hit, a stinging hand and a double fist pump.</summary>
        public const float FiveEnd = 4.3f, FiveHit = 1.45f, FiveDistance = 0.98f;

        /// <summary>"Uścisk prezesa": straighten up, grip, long firm shake with pats, photo pose to the cameras, release.</summary>
        public const float ShakeEnd = 7.1f, ShakeGrip = 1.2f, ShakeRelease = 5.75f, ShakeDistance = 0.92f, ShakeThumb = 3.9f;
        public const float ShakePhotoIn = 3.0f, ShakePhotoOut = 4.8f;
        public static readonly float[] ShakePats = { 1.95f, 2.3f, 2.65f };

        /// <summary>Dance loop (8 s): step-touch, underarm turn, open break, both round the middle.</summary>
        public const float PairDancePeriod = 8f, PairDanceDistance = 0.86f, PairDanceThumb = 2.95f;
        public const float SpinStart = 2.25f, SpinEnd = 3.55f, CircleStart = 6.0f, CircleEnd = 7.85f;

        /// <summary>Bear hug: hug, lift, three turns (speeding up and slowing down), set down, dizzy wobble.</summary>
        public const float BearEnd = 12.4f, BearDistance = 0.62f, BearThumb = 6.3f;
        public const float BearDip = 2.6f, BearLift = 3.1f, BearUp = 3.7f, BearSpinEnd = 8.6f, BearDown = 9.35f, BearLet = 9.9f;
        public const float BearTurns = 3f;

        /// <summary>Secret handshake: glance, high five, low five, elbows, spin, hips, fist and explosion, the same end pose.</summary>
        public const float SecretEnd = 6.8f, SecretDistance = 1.0f, SecretThumb = 1.12f;
        public const float SecretHigh = 1.1f, SecretLow = 1.55f, SecretElbow = 2.0f, SecretSpinEnd = 2.85f, SecretHip = 3.45f,
            SecretFist = 4.05f, SecretPose = 5.2f;

        /// <summary>Piggyback: the carrier turns and crouches, the rider jumps on, then circles in place (loop).</summary>
        public const float PiggyPeriod = 4f, PiggyDistance = 0.6f, PiggyExit = 1.55f, PiggyThumb = 4.6f;
        public const float PiggyTurned = 1.1f, PiggyJump = 1.6f, PiggyLand = 2.1f, PiggyWalk = 2.8f;

        /// <summary>Patty-cake loop (contacts on the beats): left hands, own clap, right hands, own clap (the first one at 0 only leads in).</summary>
        public const float PattyBeat = 0.42f, PattyPeriod = 4f * PattyBeat, PattyDistance = 0.95f, PattyThumb = 2f * PattyBeat + 0.01f;

        /// <summary>Contact moments of an emote (one-shot: absolute; loop: within one period).</summary>
        public static float[] Beats(EmoteId id, out EmoteBeat kind)
        {
            switch (id)
            {
                case EmoteId.Clap: kind = EmoteBeat.Clap; return ClapTimes;
                case EmoteId.SitDown: kind = EmoteBeat.Sit; return SitBeats;
                case EmoteId.Hug: kind = EmoteBeat.Pat; return HugPats;
                case EmoteId.HighFive: kind = EmoteBeat.Slap; return FiveBeats;
                case EmoteId.Handshake: kind = EmoteBeat.Grip; return ShakeBeats;
                case EmoteId.PairDance: kind = EmoteBeat.Grip; return DanceBeats;
                case EmoteId.BearHug: kind = EmoteBeat.Pat; return BearBeats;
                case EmoteId.SecretShake: kind = EmoteBeat.Slap; return SecretBeats;
                case EmoteId.Piggyback: kind = EmoteBeat.Stomp; return PiggyBeats;
                case EmoteId.PattyCake: kind = EmoteBeat.Clap; return PattyBeats;
                default: kind = EmoteBeat.Clap; return None;
            }
        }

        /// <summary>Kind of beat <paramref name="index"/> (emotes mixing several contacts), else <paramref name="fallback"/>.</summary>
        public static EmoteBeat BeatKind(EmoteId id, int index, EmoteBeat fallback)
        {
            EmoteBeat[] k = id switch
            {
                EmoteId.Handshake => ShakeKinds,
                EmoteId.BearHug => BearKinds,
                EmoteId.SecretShake => SecretKinds,
                EmoteId.PattyCake => PattyKinds,
                _ => null,
            };
            return k != null && index >= 0 && index < k.Length ? k[index] : fallback;
        }

        static readonly float[] None = { };
        static readonly float[] SitBeats = { SitTouch };
        static readonly float[] FiveBeats = { FiveHit };
        static readonly float[] ShakeBeats = { ShakeGrip, ShakePats[0], ShakePats[1], ShakePats[2] };
        static readonly EmoteBeat[] ShakeKinds = { EmoteBeat.Grip, EmoteBeat.Pat, EmoteBeat.Pat, EmoteBeat.Pat };
        static readonly float[] DanceBeats = { SpinEnd + 0.25f };
        static readonly float[] BearBeats = { 1.95f, 2.3f, BearDown - 0.05f };
        static readonly EmoteBeat[] BearKinds = { EmoteBeat.Pat, EmoteBeat.Pat, EmoteBeat.Stomp };
        static readonly float[] SecretBeats = { SecretHigh, SecretLow, SecretElbow, SecretHip, SecretFist };
        static readonly EmoteBeat[] SecretKinds = { EmoteBeat.Slap, EmoteBeat.Slap, EmoteBeat.Bump, EmoteBeat.Bump, EmoteBeat.Bump };
        static readonly float[] PiggyBeats = { PiggyLand };
        static readonly float[] PattyBeats = { 0f, PattyBeat, 2f * PattyBeat, 3f * PattyBeat };
        static readonly EmoteBeat[] PattyKinds = { EmoteBeat.Slap, EmoteBeat.Clap, EmoteBeat.Slap, EmoteBeat.Clap };
    }
}
