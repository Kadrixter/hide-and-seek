using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>Kill variants. Values are sent over the network - append only.</summary>
    public enum KillMove : byte
    {
        /// <summary>Classic: 4 stabs into the belly (first-person view for the seeker).</summary>
        Stabs = 0,
        /// <summary>Grab the shoulder, slash across the throat, one stab into the chest.</summary>
        ThroatSlit = 1,
        /// <summary>Grab the collar, two overhead (ice-pick) stabs down into the collarbone; victim sinks to the knees.</summary>
        Overhead = 2,
        /// <summary>Knee to the stomach, victim doubles over, two stabs down into the back.</summary>
        GutKnee = 3,
        /// <summary>Last hider: 3 fast stabs, victim on the knees, head pulled back, throat slit, then a knife flip and a taunt.</summary>
        Finisher = 4,
        /// <summary>Chainsaw: rammed into the belly, grinding upwards, ripped out, chainsaw raised overhead. Always kills.</summary>
        Chainsaw = 5,
        /// <summary>Chainsaw: overhead diagonal chop from the shoulder down to the hip.</summary>
        SawChop = 6,
        /// <summary>Chainsaw: two wide horizontal sweeps through the waist.</summary>
        SawSweep = 7,
        /// <summary>Chainsaw: crouch, drive it up from the groin through the chest, lift and throw the body.</summary>
        SawUpper = 8,
        /// <summary>Chainsaw: grab by the collar, saw through the neck - the head flies off.</summary>
        SawNeck = 9,
        /// <summary>Knife: driven into the chest, twisted, pulled upwards, then the victim is shoved off the blade.</summary>
        Impale = 10,
        /// <summary>Last hider, chainsaw: both legs, then both arms are cut off, then the seeker raises the saw.</summary>
        SawFinisher = 11,
        /// <summary>Last hider, Glock: leg shot (knees, begging), shoulder, head shot, the seeker blows the smoke off.</summary>
        GlockFinisher = 12,
    }

    /// <summary>Bit mask of severed limbs (<c>NetPlayer.Severed</c>, <see cref="BlockyCharacter.SetSevered"/>).</summary>
    public static class Limb
    {
        public const int LegR = 1, LegL = 2, ArmR = 4, ArmL = 8, Head = 16;
        public const int Legs = LegR | LegL;
        public const int Arms = ArmR | ArmL;
        public const int All = Legs | Arms | Head;
    }

    /// <summary>
    /// Shared timing table of the kill variants (seconds from the start). Third person
    /// (<see cref="BlockyCharacterActions.PlayKill"/>) and the game's sound / blood events read the same values.
    /// </summary>
    public static class KillMoves
    {
        /// <summary>Normal knife kills (picked at random, never the same one twice in a row).</summary>
        public static readonly KillMove[] KnifePool = { KillMove.Stabs, KillMove.ThroatSlit, KillMove.Overhead, KillMove.GutKnee, KillMove.Impale };
        /// <summary>Normal chainsaw kills.</summary>
        public static readonly KillMove[] SawPool = { KillMove.Chainsaw, KillMove.SawChop, KillMove.SawSweep, KillMove.SawUpper, KillMove.SawNeck };

        static readonly float[] ThroatHits = { 0.82f, 1.65f };
        static readonly float[] OverheadHits = { 0.88f, 1.55f };
        static readonly float[] GutHits = { 0.68f, 1.45f, 2.05f };
        static readonly float[] FinisherHits = { 0.6f, 0.95f, 1.3f, 2.75f };
        static readonly float[] ChainsawHits = { 0.5f, 1.1f, 1.7f, 2.3f };
        static readonly float[] ImpaleHits = { 0.8f, 1.4f };
        static readonly float[] ChopHits = { 0.85f, 1.2f, 1.55f };
        static readonly float[] SweepHits = { 0.85f, 1.8f };
        static readonly float[] UpperHits = { 0.7f, 1.0f, 1.3f, 1.6f };
        static readonly float[] NeckHits = { 0.85f, 1.2f };
        static readonly float[] SawFinisherHits = { 1.0f, 2.1f, 3.2f, 4.3f };
        static readonly float[] GlockFinisherHits = { 0.75f, 1.9f, 3.6f };

        /// <summary>Glock finisher: victim dead / gun lowered / smoke blown off the muzzle.</summary>
        public const float GlockFinisherVictimDead = 4.5f;
        public const float GlockFinisherLowerTime = 4.3f;
        public const float GlockFinisherTauntTime = 4.9f;

        /// <summary>Chainsaw: grinding (blood spraying continuously) between these times.</summary>
        public const float ChainsawGrindStart = 0.5f, ChainsawGrindEnd = 2.35f;
        public const float ChainsawVictimDead = 3.7f;

        /// <summary>Finisher: victim lies dead from this moment, the seeker's flourish starts at <see cref="FinisherTauntTime"/>.</summary>
        public const float FinisherVictimDead = 4.7f;
        public const float FinisherFlipTime = 4.3f;
        public const float FinisherTauntTime = 5.1f;

        /// <summary>Saw finisher: victim dead / saw raised.</summary>
        public const float SawFinisherVictimDead = 5.9f;
        public const float SawFinisherTauntTime = 5.5f;

        public static bool IsSaw(KillMove m) => m == KillMove.Chainsaw || m == KillMove.SawChop || m == KillMove.SawSweep ||
                                               m == KillMove.SawUpper || m == KillMove.SawNeck || m == KillMove.SawFinisher;

        /// <summary>Played for the last hider (shown to everybody with the cinematic camera).</summary>
        public static bool IsFinisher(KillMove m) => m == KillMove.Finisher || m == KillMove.SawFinisher || m == KillMove.GlockFinisher;

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

        /// <summary>Seeker animation length.</summary>
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

        /// <summary>Victim reaction length (normal knife kills end in the downed pose, the rest in the dead pose).</summary>
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

        /// <summary>Whole sequence on the server (both animations done).</summary>
        public static float SequenceDuration(KillMove m) => Mathf.Max(SeekerDuration(m), VictimDuration(m)) + 0.1f;

        /// <summary>The victim ends dead (not downed) - finishers and every chainsaw kill.</summary>
        public static bool EndsDead(KillMove m) => m == KillMove.Finisher || m == KillMove.GlockFinisher || IsSaw(m);

        /// <summary>Blunt hit (knee) - no blade sound / blood.</summary>
        public static bool IsBlunt(KillMove m, int hit) => m == KillMove.GutKnee && hit == 0;

        /// <summary>Slash across the throat (bigger blood spray).</summary>
        public static bool IsSlash(KillMove m, int hit) => (m == KillMove.ThroatSlit && hit == 0) || (m == KillMove.Finisher && hit == 3);

        /// <summary>Limbs removed by hit <paramref name="hit"/> of this move (0 = none). Finisher slots are resolved by the caller.</summary>
        public static int SeveredAtHit(KillMove m, int hit) => m switch
        {
            KillMove.SawNeck => hit == 0 ? Limb.Head : 0,
            KillMove.Chainsaw => hit == 3 ? Limb.ArmR : 0,
            KillMove.SawChop => hit == 2 ? Limb.ArmL : 0,
            KillMove.SawSweep => hit == 1 ? Limb.ArmR : 0,
            KillMove.SawUpper => hit == 3 ? Limb.ArmL : 0,
            _ => 0,
        };

        /// <summary>Everything a kill variant cuts off (the server stores it once the victim is dead).</summary>
        public static int SeveredTotal(KillMove m)
        {
            if (m == KillMove.SawFinisher) return Limb.Legs | Limb.Arms;
            int mask = 0;
            for (int i = 0; i < HitTimes(m).Length; i++) mask |= SeveredAtHit(m, i);
            return mask;
        }

        /// <summary>When the victim hits the floor (body fall sound).</summary>
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

        public static string DisplayName(KillMove m) => m switch
        {
            KillMove.ThroatSlit => "Podcięcie gardła",
            KillMove.Overhead => "Cios z góry",
            KillMove.GutKnee => "Kolano i plecy",
            KillMove.Finisher => "WYKOŃCZENIE",
            KillMove.GlockFinisher => "WYKOŃCZENIE: glock",
            KillMove.Chainsaw => "Piła: mielenie",
            KillMove.Impale => "Nadziewanie",
            KillMove.SawChop => "Piła: rąbnięcie z góry",
            KillMove.SawSweep => "Piła: zamach w pasie",
            KillMove.SawUpper => "Piła: cięcie od dołu",
            KillMove.SawNeck => "Piła: ścięcie głowy",
            KillMove.SawFinisher => "WYKOŃCZENIE",
            _ => "Cztery ciosy",
        };
    }
}
