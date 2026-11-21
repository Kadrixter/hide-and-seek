using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Kill sequence = intro (struggle) + core (killing blows, <c>Kills.cs</c> / <c>Gore.cs</c> / <c>GlockKill.cs</c>) +
    /// outro (aftermath), see <see cref="KillMoves"/>. Both sides are pure functions of the action time; the parts are
    /// cross-faded. The seeker's hands track the victim's real bones, the victim's hands track the seeker's real bones
    /// (<see cref="PlayKillReaction(KillMove, BlockyCharacter)"/>); every contact has exactly one leader, so there is no
    /// feedback loop between the two characters.
    /// </summary>
    public partial class BlockyCharacterActions
    {
        KillMove killMove;
        float killSeekerDuration, killVictimDuration;
        /// <summary>Victim side: the seeker killing us (null = scripted nominal seeker).</summary>
        BlockyCharacter killer;
        /// <summary>The core follows an intro (cores skip their own wind-up where it would repeat the intro's end).</summary>
        bool coreAfterIntro;

        const float PhaseBlend = 0.35f;

        /// <summary>Seeker: fired at every event of the current kill move (index into <see cref="KillMoves.Events"/>).</summary>
        public event System.Action<int> KillHit;

        /// <summary>Seeker side of a kill variant. Returns its duration.</summary>
        public float PlayKill(KillMove move, BlockyCharacter victim)
        {
            EnsureInit();
            sawKill = KillMoves.IsSaw(move);
            glockKill = move == KillMove.GlockFinisher;
            knifeVictim = victim;
            if (move == KillMove.SawFinisher) PrepareSawSlots(victim);
            killMove = move;
            coreAfterIntro = KillMoves.IntroOf(move) != KillIntro.None;
            killSeekerDuration = KillMoves.SeekerDuration(move);
            StartAction(ActionKind.Kill);
            return killSeekerDuration;
        }

        /// <summary>Victim side of a kill variant. Returns the time until the reaction is over (lying on the floor).</summary>
        public float PlayKillReaction(KillMove move) => PlayKillReaction(move, null);

        /// <summary>Victim side; <paramref name="seeker"/> lets the hands grab the seeker's real arms / boots.</summary>
        public float PlayKillReaction(KillMove move, BlockyCharacter seeker)
        {
            EnsureInit();
            reviving = false;
            reviveW = 0f;
            killMove = move;
            killer = seeker;
            coreAfterIntro = KillMoves.IntroOf(move) != KillIntro.None;
            killVictimDuration = KillMoves.VictimDuration(move);
            StartAction(ActionKind.KillReaction);
            return killVictimDuration;
        }

        public KillMove CurrentKillMove => killMove;

        void AdvanceKill(float prev)
        {
            if (action == ActionKind.Kill)
            {
                var ev = KillMoves.Events(killMove);
                while (firedEvents < ev.Length && actionTime >= ev[firedEvents].Time)
                {
                    int i = firedEvents++;
                    try { KillHit?.Invoke(i); }
                    catch (System.Exception e) { Debug.LogException(e); }
                }
                if (actionTime >= killSeekerDuration) EndAction();
            }
            else if (action == ActionKind.KillReaction)
            {
                if (KillMoves.EndsDead(killMove))
                {
                    // Lies dead (holding the pose) until the Dead state arrives; safety timeout.
                    if (lifeState == LifeState.Dead && actionTime >= killVictimDuration) EndFinisherReaction();
                    else if (actionTime >= killVictimDuration + 6f) EndFinisherReaction();
                }
                else if (actionTime >= killVictimDuration) EndStabbedReaction();
            }
        }

        void EndFinisherReaction()
        {
            action = ActionKind.None;
            pendingDowned = false;
            pathActive = false;
            killer = null;
            if (lifeState != LifeState.Dead) StartLifePath(LifeState.Dead, lifeState);
        }

        // ================================================================== composition

        CharacterPose KillPose(in CharacterPose current, float t)
        {
            var intro = KillMoves.IntroOf(killMove);
            var outro = KillMoves.OutroOf(killMove);
            float c0 = KillMoves.CoreStart(killMove), o0 = KillMoves.OutroStart(killMove);
            if (intro != KillIntro.None && t < c0 + PhaseBlend)
            {
                var ip = IntroSeekerPose(intro, current, Mathf.Min(t, c0));
                if (t >= c0) ip = LerpKeepProp(ip, CoreSeekerPose(current, t - c0), Seg(t, c0, c0 + PhaseBlend));
                AvoidHeads(ref ip, VictimHead(new Vector3(0f, 4f * Px, 0f)));
                return ip;
            }
            if (outro == KillOutro.None || t < o0)
            {
                var cp = CoreSeekerPose(current, t - c0);
                if (outro != KillOutro.None || t < KillMoves.FallTime(killMove)) AvoidHeads(ref cp, VictimHead(new Vector3(0f, 4f * Px, 0f)));
                return cp;
            }
            var end = CoreSeekerPose(current, o0 - c0);
            var op = OutroSeekerPose(outro, end, t - o0);
            return LerpKeepProp(end, op, Seg(t, o0, o0 + PhaseBlend));
        }

        CharacterPose KillReactionPose(float t, Quaternion propR)
        {
            var intro = KillMoves.IntroOf(killMove);
            var outro = KillMoves.OutroOf(killMove);
            float c0 = KillMoves.CoreStart(killMove), o0 = KillMoves.OutroStart(killMove);
            CharacterPose p;
            if (intro != KillIntro.None && t < c0 + PhaseBlend)
            {
                var ip = IntroVictimPose(intro, Mathf.Min(t, c0));
                p = t < c0 ? ip : CharacterPose.Lerp(ip, CoreVictimPose(t - c0, propR), Seg(t, c0, c0 + PhaseBlend));
            }
            else if (outro == KillOutro.None || t < o0) p = CoreVictimPose(t - c0, propR);
            else
            {
                var end = CoreVictimPose(o0 - c0, propR);
                p = CharacterPose.Lerp(end, OutroVictimPose(outro, end, t - o0), Seg(t, o0, o0 + 0.25f));
            }
            if (t < KillMoves.FallTime(killMove))
                AvoidHeads(ref p, KillerBone(3, new Vector3(0f, 4f * Px, 0f), new Vector3(0f, 1.57f, 0.02f)));
            p.PropR = propR;
            return p;
        }

        /// <summary>
        /// Close clinch: when the two heads come nearer than ~0.5 m, lean the upper body and the head to the own left
        /// (both characters do it, so the heads pass cheek by cheek instead of through each other). The hands are
        /// solved back onto the same points (elbow direction and weapon orientation kept), so grips stay in place.
        /// </summary>
        void AvoidHeads(ref CharacterPose p, Vector3 otherHead)
        {
            Vector3 neck = p.TorsoPoint(BlockyCharacter.NeckRest);
            Vector3 head = neck + p.TorsoRoot * (p.Head * new Vector3(0f, 4f * Px, 0f));
            Vector3 d = head - otherHead;
            d.y *= 0.6f;
            const float Want = 0.5f;
            float dist = d.magnitude;
            if (dist >= Want) return;
            float k = Mathf.Clamp01((Want - dist) / 0.28f);
            Vector3 h0 = p.HandPos(0), h1 = p.HandPos(1);
            Vector3 pole0 = p.ElbowPos(0) - (p.ShoulderPos(0) + h0) * 0.5f;
            Vector3 pole1 = p.ElbowPos(1) - (p.ShoulderPos(1) + h1) * 0.5f;
            Quaternion prop = p.ForearmRoot(0) * p.PropR;
            p.Torso = p.Torso * Quaternion.Euler(0f, -6f * k, 10f * k);
            p.Head = p.Head * Quaternion.Euler(0f, -24f * k, 16f * k);
            p.SolveArm(0, h0, pole0);
            p.SolveArm(1, h1, pole1);
            p.PropR = Quaternion.Inverse(p.ForearmRoot(0)) * prop;
        }

        /// <summary>Lerp that blends the prop too (grip changes between the parts).</summary>
        static CharacterPose LerpKeepProp(in CharacterPose a, in CharacterPose b, float w) => CharacterPose.Lerp(a, b, w);

        CharacterPose IntroSeekerPose(KillIntro i, in CharacterPose current, float t) => i switch
        {
            KillIntro.WristLock => SeekerWristLock(current, t),
            KillIntro.Flee => SeekerFlee(current, t),
            KillIntro.Shove => SeekerShove(current, t),
            KillIntro.SawGrab => SeekerSawGrab(current, t),
            KillIntro.SawTrip => SeekerSawTrip(current, t),
            KillIntro.GlockStandoff => SeekerGlockStandoff(current, t),
            _ => current,
        };

        CharacterPose IntroVictimPose(KillIntro i, float t) => i switch
        {
            KillIntro.WristLock => VictimWristLock(t),
            KillIntro.Flee => VictimFlee(t),
            KillIntro.Shove => VictimShove(t),
            KillIntro.SawGrab => VictimSawGrab(t),
            KillIntro.SawTrip => VictimSawTrip(t),
            KillIntro.GlockStandoff => VictimGlockStandoff(t),
            _ => VictimStand(t, 0f, 0f, 0f, 0f, 0f, 0f, 0f, HandsRestR, HandsRestL),
        };

        CharacterPose OutroSeekerPose(KillOutro o, in CharacterPose from, float t) => o switch
        {
            KillOutro.StompHand => SeekerStompHand(from, t),
            KillOutro.Throes => SeekerThroes(from, t),
            _ => from,
        };

        CharacterPose OutroVictimPose(KillOutro o, in CharacterPose from, float t) => o switch
        {
            KillOutro.StompHand => VictimStompHand(t),
            KillOutro.Throes => VictimThroes(t),
            _ => from,
        };

        // ================================================================== shared helpers

        /// <summary>Distance between the two roots during the current kill.</summary>
        float KillDistance => killMove == KillMove.GlockFinisher ? 2.3f : KnifeAttackDistance;

        /// <summary>A point given in seeker-root space, expressed in the victim's root space (nominal layout).</summary>
        Vector3 SeekerToVictim(Vector3 s) => new Vector3(-s.x, s.y, KillDistance - s.z);

        /// <summary>Victim side: a point on one of the killer's bones in our root space (nominal seeker when unknown).</summary>
        Vector3 KillerBone(int bone, Vector3 local, Vector3 nominalSeekerSpace)
        {
            if (killer != null && killer.IsBuilt)
            {
                Transform b = bone switch
                {
                    0 => killer.ElbowRight,
                    1 => killer.ElbowLeft,
                    2 => killer.Torso,
                    3 => killer.Head,
                    4 => killer.KneeRight,
                    5 => killer.KneeLeft,
                    6 => killer.ShoulderRight,
                    _ => killer.ShoulderLeft,
                };
                if (b != null) return transform.InverseTransformPoint(b.TransformPoint(local));
            }
            return SeekerToVictim(nominalSeekerSpace);
        }

        /// <summary>Killer's fist (0 = right / weapon hand, 1 = left) in our root space.</summary>
        Vector3 KillerHand(int arm) => KillerBone(arm, CharacterPose.HandLocal, new Vector3(arm == 0 ? 0.24f : -0.24f, 0.95f, 0.32f));

        /// <summary>Point along the killer's forearm (0 = elbow, 1 = fist).</summary>
        Vector3 KillerForearm(int arm, float frac) =>
            KillerBone(arm, CharacterPose.HandLocal * frac, new Vector3(arm == 0 ? 0.26f : -0.26f, 0.98f, 0.18f + 0.14f * frac));

        /// <summary>Killer's foot (sole) in our root space.</summary>
        Vector3 KillerFoot(int leg) => KillerBone(leg == 0 ? 4 : 5, new Vector3(0f, -6f * Px, 0.02f), new Vector3(leg == 0 ? 0.15f : -0.13f, 0.03f, leg == 0 ? -0.15f : 0.17f));

        /// <summary>Seeker side: the victim's fist (0 right, 1 left) in our root space.</summary>
        Vector3 VictimHand(int arm)
        {
            if (knifeVictim != null && knifeVictim.IsBuilt)
                return transform.InverseTransformPoint((arm == 0 ? knifeVictim.ElbowRight : knifeVictim.ElbowLeft).TransformPoint(CharacterPose.HandLocal));
            return new Vector3(arm == 0 ? -0.24f : 0.24f, 0.95f, KnifeAttackDistance - 0.3f);
        }

        /// <summary>Whole pose turned around the vertical axis through the root and moved (body yaw while running / spinning).</summary>
        static CharacterPose Turned(in CharacterPose p, float yaw, Vector3 offset)
        {
            var q = Quaternion.Euler(0f, yaw, 0f);
            var r = p;
            r.PelvisPos = q * p.PelvisPos + offset;
            r.Pelvis = q * p.Pelvis;
            return r;
        }

        /// <summary>Foot moving from <paramref name="a"/> to <paramref name="b"/> between t0 and t1 with a lift arc.</summary>
        static Vector3 Step(Vector3 a, Vector3 b, float t, float t0, float t1, float lift = 0.09f)
        {
            float x = ActionTimeline.Remap01(t, t0, t1);
            var p = Vector3.Lerp(a, b, ActionTimeline.SmoothStep01(x));
            p.y += lift * Mathf.Sin(Mathf.PI * x);
            return p;
        }

        /// <summary>Running stride facing +Z (phase 0..1 per two steps), used for escape attempts.</summary>
        static CharacterPose StridePose(float ph, float lean, float flail)
        {
            float a = ph * Mathf.PI * 2f;
            float s = Mathf.Sin(a), c = Mathf.Cos(a);
            var p = CharacterPose.Identity;
            p.PelvisPos = new Vector3(0.015f * s, 0.6f + 0.025f * Mathf.Abs(c), 0f);
            p.Pelvis = Quaternion.Euler(4f, 8f * s, 0f);
            p.Torso = Quaternion.Euler(lean, -10f * s, 0f);
            p.Head = Quaternion.Euler(-lean * 0.6f, 6f * s, 0f);
            p.SolveLeg(0, new Vector3(0.12f, 0.13f * Mathf.Max(0f, -c), 0.3f * s), new Vector3(0.1f, 0f, 1f));
            p.SolveLeg(1, new Vector3(-0.12f, 0.13f * Mathf.Max(0f, c), -0.3f * s), new Vector3(-0.1f, 0f, 1f));
            Vector3 r = new Vector3(0.24f, 0.36f + 0.1f * flail, -0.24f * s + 0.08f);
            Vector3 l = new Vector3(-0.24f, 0.36f + 0.1f * flail, 0.24f * s + 0.08f);
            p.SolveArm(0, p.TorsoPoint(r), TorsoDir(p, 1f, -0.4f, -1f));
            p.SolveArm(1, p.TorsoPoint(l), TorsoDir(p, -1f, -0.4f, -1f));
            return p;
        }

        /// <summary>Down on the right knee facing +Z, left foot planted, left hand on the floor (tripped while running).</summary>
        static CharacterPose OneKneePose(float push)
        {
            var p = CharacterPose.Identity;
            p.PelvisPos = new Vector3(0.02f, 0.43f + 0.08f * push, -0.04f);
            p.Torso = Quaternion.Euler(34f - 14f * push, 0f, -4f);
            p.Head = Quaternion.Euler(-24f, 0f, 0f);
            p.SolveLeg(0, new Vector3(0.13f, 0.07f, -0.44f), new Vector3(0.1f, -0.4f, 1f));
            p.SolveLeg(1, new Vector3(-0.15f, 0f, 0.3f), new Vector3(-0.1f, 0.3f, 1f));
            p.SolveArm(1, new Vector3(-0.32f, 0.08f, 0.36f), TorsoDir(p, -1f, 0f, -1f));
            p.SolveArm(0, p.TorsoPoint(new Vector3(0.16f, 0.12f, 0.3f)), TorsoDir(p, 1f, -0.6f, -0.5f));
            return p;
        }

        /// <summary>Victim: solve one arm to a point in our root space.</summary>
        static void ArmTo(ref CharacterPose p, int arm, Vector3 rootTarget, float w)
        {
            if (w <= 0.001f) return;
            var a = p;
            a.SolveArm(arm, rootTarget, TorsoDir(p, arm == 0 ? 1f : -1f, -0.8f, -0.5f));
            CharacterPose.LerpArm(ref p, a, arm, w);
        }

        /// <summary>Small high-frequency tremble (effort).</summary>
        static Vector3 Tremble(float t, float amount) =>
            new Vector3(Mathf.Sin(t * 47f), Mathf.Sin(t * 39f + 1.7f), Mathf.Sin(t * 31f + 0.6f)) * amount;
    }
}
