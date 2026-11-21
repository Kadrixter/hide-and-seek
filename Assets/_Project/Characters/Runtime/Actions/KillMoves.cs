using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>Kill variants. Values are sent over the network - append only.</summary>
    public enum KillMove : byte
    {
        /// <summary>Classic: 4 stabs into the belly.</summary>
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

    /// <summary>Struggle before the killing blows (both players move: grabs, escape attempts, blows).</summary>
    public enum KillIntro : byte
    {
        None,
        /// <summary>Knife raised, the victim catches the knife wrist with both hands, tug of war, headbutt breaks the grip.</summary>
        WristLock,
        /// <summary>The victim turns and runs, the seeker grabs the collar, yanks them back and spins them round; the victim tries to pry the hand off.</summary>
        Flee,
        /// <summary>The victim shoves the seeker away and throws a punch; the seeker blocks it, twists the arm and pulls them in.</summary>
        Shove,
        /// <summary>Chainsaw: the victim backs off, then grabs the seeker's arms to push the saw away; a front kick ends it.</summary>
        SawGrab,
        /// <summary>Chainsaw: the victim runs, a kick into the back of the knee drops them, the seeker drags them up by the collar.</summary>
        SawTrip,
        /// <summary>Glock: the victim tries to run, a warning shot into the floor, hands up, slowly turns back.</summary>
        GlockStandoff,
    }

    /// <summary>Aftermath once the victim is on the floor.</summary>
    public enum KillOutro : byte
    {
        None,
        /// <summary>Downed victim reaches for the seeker's boot; the seeker shakes it off, stomps on the hand, wipes the blade.</summary>
        StompHand,
        /// <summary>Dying victim lifts a trembling hand, it drops, last spasms; the seeker revs the saw / steps back.</summary>
        Throes,
    }

    /// <summary>What happens at a kill event (sound / blood / camera shake).</summary>
    public enum KillHitKind : byte
    {
        Stab,
        Slash,
        Blunt,
        Saw,
        Shot,
        /// <summary>Glock shot that misses on purpose (into the floor).</summary>
        ShotMiss,
        /// <summary>Rough grab / yank (cloth, grunt), no blood.</summary>
        Grab,
    }

    public struct KillEvent
    {
        public float Time;
        public KillHitKind Kind;
        /// <summary>Limbs cut off at this event (fixed moves).</summary>
        public int Severs;
        /// <summary>Saw finisher: which limb slot (0..3) is cut here, -1 = none (see BlockyCharacterActions.SawFinisherLimb).</summary>
        public int SawSlot;
    }

    /// <summary>
    /// Shared timing table of the kill variants (seconds from the start of the whole sequence). Third person
    /// (<see cref="BlockyCharacterActions.PlayKill"/>), the server (blocking, death) and the game's sound / blood events
    /// read the same values. A sequence = <see cref="IntroOf"/> + core (<see cref="KillCore"/>) + <see cref="OutroOf"/>.
    /// </summary>
    public static class KillMoves
    {
        /// <summary>Normal knife kills (picked at random, never the same one twice in a row).</summary>
        public static readonly KillMove[] KnifePool = { KillMove.Stabs, KillMove.ThroatSlit, KillMove.Overhead, KillMove.GutKnee, KillMove.Impale };
        /// <summary>Normal chainsaw kills.</summary>
        public static readonly KillMove[] SawPool = { KillMove.Chainsaw, KillMove.SawChop, KillMove.SawSweep, KillMove.SawUpper, KillMove.SawNeck };

        public static bool IsSaw(KillMove m) => m == KillMove.Chainsaw || m == KillMove.SawChop || m == KillMove.SawSweep ||
                                               m == KillMove.SawUpper || m == KillMove.SawNeck || m == KillMove.SawFinisher;

        /// <summary>Played for the last hider (shown to everybody with the cinematic camera).</summary>
        public static bool IsFinisher(KillMove m) => m == KillMove.Finisher || m == KillMove.SawFinisher || m == KillMove.GlockFinisher;

        /// <summary>The victim ends dead (not downed) - finishers and every chainsaw kill.</summary>
        public static bool EndsDead(KillMove m) => m == KillMove.Finisher || m == KillMove.GlockFinisher || IsSaw(m);

        // ------------------------------------------------------------------ structure

        public static KillIntro IntroOf(KillMove m) => m switch
        {
            KillMove.Stabs => KillIntro.Flee,
            KillMove.ThroatSlit => KillIntro.WristLock,
            KillMove.Overhead => KillIntro.Shove,
            KillMove.GutKnee => KillIntro.Flee,
            KillMove.Impale => KillIntro.WristLock,
            KillMove.Finisher => KillIntro.Flee,
            KillMove.Chainsaw => KillIntro.SawGrab,
            KillMove.SawChop => KillIntro.SawTrip,
            KillMove.SawSweep => KillIntro.SawTrip,
            KillMove.SawUpper => KillIntro.SawGrab,
            KillMove.SawNeck => KillIntro.SawTrip,
            KillMove.SawFinisher => KillIntro.SawGrab,
            KillMove.GlockFinisher => KillIntro.GlockStandoff,
            _ => KillIntro.None,
        };

        public static KillOutro OutroOf(KillMove m)
        {
            if (IsFinisher(m)) return KillOutro.None; // the core already ends with the taunt
            return EndsDead(m) ? KillOutro.Throes : KillOutro.StompHand;
        }

        public static float IntroLength(KillIntro i) => i switch
        {
            KillIntro.WristLock => 2.9f,
            KillIntro.Flee => 2.9f,
            KillIntro.Shove => 2.8f,
            KillIntro.SawGrab => 2.8f,
            KillIntro.SawTrip => 2.8f,
            KillIntro.GlockStandoff => 2.6f,
            _ => 0f,
        };

        public static float OutroLength(KillOutro o) => o switch
        {
            KillOutro.StompHand => 2.7f,
            KillOutro.Throes => 2.5f,
            _ => 0f,
        };

        /// <summary>Start of the killing blows (= length of the struggle).</summary>
        public static float CoreStart(KillMove m) => IntroLength(IntroOf(m));

        /// <summary>Start of the aftermath.</summary>
        public static float OutroStart(KillMove m) => CoreStart(m) + KillCore.Length(m);

        /// <summary>Whole sequence on one side (seeker and victim have the same length).</summary>
        public static float Duration(KillMove m) => OutroStart(m) + OutroLength(OutroOf(m));

        public static float SeekerDuration(KillMove m) => Duration(m);
        public static float VictimDuration(KillMove m) => Duration(m);

        /// <summary>Whole sequence on the server (both animations done).</summary>
        public static float SequenceDuration(KillMove m) => Duration(m) + 0.1f;

        // ------------------------------------------------------------------ events

        static readonly KillEvent[][] Cache = new KillEvent[16][];

        static void Add(System.Collections.Generic.List<KillEvent> list, float t, KillHitKind k, int severs = 0, int slot = -1) =>
            list.Add(new KillEvent { Time = t, Kind = k, Severs = severs, SawSlot = slot });

        /// <summary>Every event of a kill in time order (struggle blows, killing blows, aftermath).</summary>
        public static KillEvent[] Events(KillMove m)
        {
            int idx = (int)m;
            if (idx < Cache.Length && Cache[idx] != null) return Cache[idx];
            var list = new System.Collections.Generic.List<KillEvent>();
            switch (IntroOf(m))
            {
                case KillIntro.WristLock: Add(list, 2.15f, KillHitKind.Blunt); break;
                case KillIntro.Flee: Add(list, 0.92f, KillHitKind.Grab); break;
                case KillIntro.Shove: Add(list, 0.3f, KillHitKind.Grab); Add(list, 1.42f, KillHitKind.Blunt); break;
                case KillIntro.SawGrab: Add(list, 0.95f, KillHitKind.Grab); Add(list, 1.78f, KillHitKind.Blunt); break;
                case KillIntro.SawTrip: Add(list, 0.98f, KillHitKind.Blunt); Add(list, 1.85f, KillHitKind.Grab); break;
                case KillIntro.GlockStandoff: Add(list, 0.95f, KillHitKind.ShotMiss); break;
            }

            float c = CoreStart(m);
            var hits = KillCore.HitTimes(m);
            for (int i = 0; i < hits.Length; i++)
                Add(list, c + hits[i], CoreKind(m, i), CoreSevers(m, i), m == KillMove.SawFinisher ? i : -1);

            float o = OutroStart(m);
            if (OutroOf(m) == KillOutro.StompHand) Add(list, o + 1.5f, KillHitKind.Blunt);

            var arr = list.ToArray();
            if (idx < Cache.Length) Cache[idx] = arr;
            return arr;
        }

        static KillHitKind CoreKind(KillMove m, int i)
        {
            if (IsSaw(m)) return KillHitKind.Saw;
            if (m == KillMove.GlockFinisher) return KillHitKind.Shot;
            if (m == KillMove.GutKnee && i == 0) return KillHitKind.Blunt;
            if ((m == KillMove.ThroatSlit && i == 0) || (m == KillMove.Finisher && i == 3)) return KillHitKind.Slash;
            return KillHitKind.Stab;
        }

        static int CoreSevers(KillMove m, int i) => m switch
        {
            KillMove.SawNeck => i == 0 ? Limb.Head : 0,
            KillMove.Chainsaw => i == 3 ? Limb.ArmR : 0,
            KillMove.SawChop => i == 2 ? Limb.ArmL : 0,
            KillMove.SawSweep => i == 1 ? Limb.ArmR : 0,
            KillMove.SawUpper => i == 3 ? Limb.ArmL : 0,
            _ => 0,
        };

        /// <summary>Times of all events (index = event index).</summary>
        public static float[] HitTimes(KillMove m)
        {
            var ev = Events(m);
            var t = new float[ev.Length];
            for (int i = 0; i < ev.Length; i++) t[i] = ev[i].Time;
            return t;
        }

        /// <summary>Blunt hit (knee, kick, headbutt, stomp) - no blade sound / blood.</summary>
        public static bool IsBlunt(KillMove m, int hit) => Kind(m, hit) == KillHitKind.Blunt;

        /// <summary>Slash across the throat (bigger blood spray).</summary>
        public static bool IsSlash(KillMove m, int hit) => Kind(m, hit) == KillHitKind.Slash;

        public static KillHitKind Kind(KillMove m, int hit)
        {
            var ev = Events(m);
            return hit >= 0 && hit < ev.Length ? ev[hit].Kind : KillHitKind.Stab;
        }

        /// <summary>Limbs removed by event <paramref name="hit"/> of this move (0 = none). Saw finisher slots: <see cref="SawSlot"/>.</summary>
        public static int SeveredAtHit(KillMove m, int hit)
        {
            var ev = Events(m);
            return hit >= 0 && hit < ev.Length ? ev[hit].Severs : 0;
        }

        /// <summary>Saw finisher: limb slot cut at event <paramref name="hit"/>, -1 = none.</summary>
        public static int SawSlot(KillMove m, int hit)
        {
            var ev = Events(m);
            return hit >= 0 && hit < ev.Length ? ev[hit].SawSlot : -1;
        }

        /// <summary>Everything a kill variant cuts off (the server stores it once the victim is dead).</summary>
        public static int SeveredTotal(KillMove m)
        {
            if (m == KillMove.SawFinisher) return Limb.Legs | Limb.Arms;
            int mask = 0;
            foreach (var e in Events(m)) mask |= e.Severs;
            return mask;
        }

        // ------------------------------------------------------------------ absolute marks

        /// <summary>When the victim hits the floor (body fall sound).</summary>
        public static float FallTime(KillMove m) => CoreStart(m) + KillCore.FallTime(m);

        /// <summary>Finishers: start of the seeker's taunt (hero shot, sting); -1 for the others.</summary>
        public static float TauntTime(KillMove m) => m switch
        {
            KillMove.Finisher => CoreStart(m) + KillCore.FinisherTauntTime,
            KillMove.SawFinisher => CoreStart(m) + KillCore.SawFinisherTauntTime,
            KillMove.GlockFinisher => CoreStart(m) + KillCore.GlockFinisherTauntTime,
            _ => -1f,
        };

        /// <summary>Knife finisher: the knife flip (whoosh).</summary>
        public static float FinisherFlipTime => CoreStart(KillMove.Finisher) + KillCore.FinisherFlipTime;
        public static float FinisherTauntTime => TauntTime(KillMove.Finisher);
        public static float SawFinisherTauntTime => TauntTime(KillMove.SawFinisher);
        public static float GlockFinisherTauntTime => TauntTime(KillMove.GlockFinisher);

        /// <summary>Chainsaw (grind): blood sprays continuously between these absolute times.</summary>
        public static float ChainsawGrindStart => CoreStart(KillMove.Chainsaw) + KillCore.ChainsawGrindStart;
        public static float ChainsawGrindEnd => CoreStart(KillMove.Chainsaw) + KillCore.ChainsawGrindEnd;

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
