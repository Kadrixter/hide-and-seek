using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Kill variants (<see cref="KillMove"/>): seeker poses track the real victim's bones, victim reactions are pure
    /// functions of time and end exactly in the downed pose (or the dead pose for the finisher).
    /// Layout as in the classic knife attack: seeker at the root facing +Z, victim ~0.85 m ahead facing the seeker.
    /// </summary>
    public partial class BlockyCharacterActions
    {
        // ================================================================== helpers

        static float Seg(float t, float a, float b) => ActionTimeline.SmoothStep01(ActionTimeline.Remap01(t, a, b));

        /// <summary>Sharp flinch pulse peaking ~0.06 s after <paramref name="at"/>.</summary>
        static float Pulse(float t, float at, float scale = 1f)
        {
            float x = t - at;
            if (x < 0f || x > 0.8f) return 0f;
            const float k = 0.06f;
            return (x / k) * Mathf.Exp(1f - x / k) * scale;
        }

        static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float x)
        {
            float u = 1f - x;
            return u * u * a + 2f * u * x * b + x * x * c;
        }

        /// <summary>Stab curve for arbitrary hit times: -1 cocked, 0 ready, 1 blade in.</summary>
        static float QuickStab(float t, float[] hits, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float h = hits[i];
                if (t < h - 0.2f) return 0f;
                if (t < h - 0.07f) return -Seg(t, h - 0.2f, h - 0.07f);
                if (t < h)
                {
                    float x = (t - (h - 0.07f)) / 0.07f;
                    return -1f + 2f * x * x;
                }
                if (t < h + 0.06f) return 1f;
                if (t < h + 0.16f) return 1f - Seg(t, h + 0.06f, h + 0.16f);
            }
            return 0f;
        }

        // Victim body points (torso-local / head-local, metres).
        static readonly Vector3 ThroatLocal = new Vector3(0f, 0.69f, 0.14f);
        static readonly Vector3 ChestLocal = new Vector3(0.04f, 0.45f, 0.13f);
        static readonly Vector3 CollarLeftLocal = new Vector3(-0.17f, 0.66f, 0.02f);
        static readonly Vector3 CollarFrontLocal = new Vector3(0.05f, 0.58f, 0.13f);
        static readonly Vector3 ShoulderLeftGrabLocal = new Vector3(-0.30f, 0.66f, 0.05f);
        static readonly Vector3 BackLocal = new Vector3(0.02f, 0.48f, -0.13f);
        static readonly Vector3 HeadBackLocal = new Vector3(0f, 0.36f, -0.2f);

        /// <summary>Victim torso-local point in this (seeker) root space; scripted standing victim when unknown.</summary>
        Vector3 VictimTorso(Vector3 local)
        {
            if (knifeVictim != null && knifeVictim.IsBuilt)
                return transform.InverseTransformPoint(knifeVictim.Torso.TransformPoint(local));
            return new Vector3(-local.x, BlockyCharacter.HipHeight + local.y, KnifeAttackDistance - local.z);
        }

        Vector3 VictimHead(Vector3 local)
        {
            if (knifeVictim != null && knifeVictim.IsBuilt)
                return transform.InverseTransformPoint(knifeVictim.Head.TransformPoint(local));
            return new Vector3(-local.x, BlockyCharacter.HipHeight + 12f * Px + local.y, KnifeAttackDistance - local.z);
        }

        /// <summary>Seeker body: lean / twist / drop / forward shift, feet in a fighting stance (left foot forward).</summary>
        static CharacterPose SeekerBody(in CharacterPose current, float e, float lean, float twist, float drop, float fwd, float back = 0f)
        {
            var p = CharacterPose.Identity;
            p.PropR = current.PropR;
            p.PelvisPos = new Vector3(0.02f * e, 0.64f - 0.03f * e - drop, -0.02f * e + fwd - back);
            p.Pelvis = Quaternion.Euler(3f * e, -6f * e + twist * 0.35f, 0f);
            p.Torso = Quaternion.Euler(4f * e + lean, twist, 6f * e);
            p.Head = Quaternion.Euler(5f * e - lean * 0.55f, -twist * 0.6f + 5f * e, 12f * e);
            p.SolveLeg(0, new Vector3(0.15f, 0f, -0.15f * e + fwd * 0.4f - back), new Vector3(0.1f, 0f, 1f));
            p.SolveLeg(1, new Vector3(-0.13f, 0f, 0.17f * e + fwd - back), new Vector3(-0.1f, 0f, 1f));
            return p;
        }

        /// <summary>Right fist (knife hand) to <paramref name="hand"/>, blade along <paramref name="bladeDir"/>.</summary>
        static void KnifeHand(ref CharacterPose p, in CharacterPose current, float e, Vector3 hand, Vector3 bladeDir, Vector3 pole, float roll = 0f)
        {
            p.SolveArm(0, Vector3.Lerp(current.HandPos(0), hand, e), pole);
            Vector3 d = bladeDir.sqrMagnitude > 1e-6f ? bladeDir.normalized : Vector3.forward;
            Vector3 up = Mathf.Abs(d.y) > 0.95f ? Vector3.forward : Vector3.up;
            Quaternion blade = Quaternion.LookRotation(d, up) * Quaternion.Euler(0f, 0f, roll);
            p.PropR = Quaternion.Slerp(DefaultPropLocal(HeldItem.Knife), Quaternion.Inverse(p.ForearmRoot(0)) * blade, e);
        }

        static void FreeHand(ref CharacterPose p, in CharacterPose current, float w, Vector3 target, Vector3 pole) =>
            p.SolveArm(1, Vector3.Lerp(current.HandPos(1), target, w), pole);

        const float Blade = 0.22f;

        // ================================================================== seeker poses

        /// <summary>Seeker pose of the core (killing blows), <paramref name="t"/> = seconds from the start of the core.</summary>
        CharacterPose CoreSeekerPose(in CharacterPose current, float t)
        {
            switch (killMove)
            {
                case KillMove.Stabs: return KnifeAttackPose(current, t);
                case KillMove.Finisher: return SeekerFinisher(current, t);
                case KillMove.ThroatSlit: return SeekerThroatSlit(current, t);
                case KillMove.Overhead: return SeekerOverhead(current, t);
                case KillMove.GutKnee: return SeekerGutKnee(current, t);
                case KillMove.Chainsaw: return SeekerChainsaw(current, t);
                case KillMove.Impale: return SeekerImpale(current, t);
                case KillMove.SawChop: return SeekerSawChop(current, t);
                case KillMove.SawSweep: return SeekerSawSweep(current, t);
                case KillMove.SawUpper: return SeekerSawUpper(current, t);
                case KillMove.SawNeck: return SeekerSawNeck(current, t);
                case KillMove.SawFinisher: return SeekerSawFinisher(current, t);
                case KillMove.GlockFinisher: return SeekerGlockFinisher(current, t);
                default: return SeekerFinisher(current, t);
            }
        }

        CharacterPose SeekerThroatSlit(in CharacterPose current, float t)
        {
            float e = Seg(t, 0f, 0.25f);
            float cock = Seg(t, 0.3f, 0.7f);
            float sl = ActionTimeline.Remap01(t, 0.7f, 0.95f);
            float slash = sl * sl * (3f - 2f * sl);
            float stabS = QuickStab(t, ThroatSlitStab, 1);
            float thrust = Mathf.Max(0f, stabS);
            float release = Seg(t, 2.05f, 2.4f);

            float twist = 20f * cock * (1f - slash) - 24f * slash * (1f - Seg(t, 1.1f, 1.45f)) - 8f * thrust;
            var p = SeekerBody(current, e, 6f * slash + 7f * thrust - 6f * release, twist, 0.02f * thrust, 0.05f * e + 0.04f * thrust - 0.06f * release);

            Vector3 throat = VictimTorso(ThroatLocal);
            Vector3 chest = VictimTorso(ChestLocal);
            Vector3 cockPos = throat + new Vector3(0.42f, 0.02f, -0.3f);
            Vector3 endPos = throat + new Vector3(-0.4f, -0.03f, -0.24f);
            Vector3 ready = p.TorsoPoint(new Vector3(0.24f, 0.32f, 0.3f));
            Vector3 hip = p.TorsoPoint(new Vector3(0.36f, 0.12f, -0.12f));

            Vector3 hand;
            Vector3 bladeDir;
            if (t < 0.7f)
            {
                hand = Vector3.Lerp(ready, cockPos, cock);
                bladeDir = Vector3.Slerp(new Vector3(0f, -0.45f, 1f), new Vector3(-0.6f, 0f, 1f), cock);
            }
            else if (t < 1.2f)
            {
                hand = Bezier(cockPos, throat + new Vector3(0f, 0f, -0.16f), endPos, slash);
                bladeDir = new Vector3(-0.7f, 0f, 1f);
            }
            else
            {
                // From the end of the slash to the hip, then a single stab into the chest.
                Vector3 dir = (chest - p.TorsoPoint(new Vector3(0.26f, 0.3f, 0f))).normalized;
                Vector3 hit = chest - dir * Blade;
                Vector3 r2 = Vector3.Lerp(hip, hit, 0.45f) + new Vector3(0f, 0.04f, 0f);
                Vector3 back = Vector3.Lerp(endPos, r2, Seg(t, 1.2f, 1.42f));
                hand = stabS < 0f ? Vector3.Lerp(back, hip, -stabS) : Vector3.LerpUnclamped(back, hit, stabS);
                hand = Vector3.Lerp(hand, ready, release);
                bladeDir = Vector3.Slerp(new Vector3(-0.7f, 0f, 1f), dir, Seg(t, 1.2f, 1.45f));
            }
            KnifeHand(ref p, current, e, hand, bladeDir, TorsoDir(p, 1f, -1f, -0.6f), 0f);

            float g = Seg(t, 0.05f, 0.3f) * (1f - 0.7f * release);
            Vector3 grab = VictimTorso(ShoulderGrabLocal) + new Vector3(0f, 0.05f, 0.02f);
            FreeHand(ref p, current, g, grab, TorsoDir(p, -1f, -0.6f, -0.5f));
            return p;
        }

        static readonly float[] ThroatSlitStab = { 1.65f };

        CharacterPose SeekerOverhead(in CharacterPose current, float t)
        {
            float e = Seg(t, 0f, 0.25f);
            float release = Seg(t, 1.95f, 2.3f);
            // raise -> strike (0.88) -> hold -> half raise -> strike (1.55) -> hold -> pull out
            float up1 = Seg(t, 0.3f, 0.72f);
            float down1 = Mathf.Clamp01((t - 0.75f) / 0.13f);
            float up2 = Seg(t, 1.08f, 1.4f);
            float down2 = Mathf.Clamp01((t - 1.43f) / 0.12f);
            float pull = Seg(t, 1.75f, 2.0f);
            float strike1 = down1 * down1, strike2 = down2 * down2;
            float sink = Seg(t, 0.88f, 1.2f) * 0.04f + Seg(t, 1.55f, 1.9f) * 0.12f;

            float lean = 6f * up1 * (1f - strike1) - 4f * up1 + 18f * strike1 * (1f - up2) + 22f * strike2 * (1f - pull) - 6f * release;
            var p = SeekerBody(current, e, lean, 6f * up1 - 10f * strike1 * (1f - up2) - 10f * strike2, sink + 0.03f * strike2, 0.06f * e);

            Vector3 collar = VictimTorso(CollarLeftLocal);
            Vector3 high = p.TorsoPoint(new Vector3(0.28f, 1.02f, 0.02f));
            Vector3 mid = p.TorsoPoint(new Vector3(0.3f, 0.86f, 0.12f));
            Vector3 hit = collar + new Vector3(0f, Blade, -0.03f);
            Vector3 ready = p.TorsoPoint(new Vector3(0.24f, 0.32f, 0.3f));

            Vector3 hand;
            if (t < 0.75f) hand = Vector3.Lerp(ready, high, up1);
            else if (t < 1.08f) hand = Vector3.Lerp(high, hit, strike1);
            else if (t < 1.43f) hand = Vector3.Lerp(hit, mid, up2);
            else if (t < 1.75f) hand = Vector3.Lerp(mid, hit, strike2);
            else hand = Vector3.Lerp(Vector3.Lerp(hit, hit + new Vector3(0f, 0.25f, -0.1f), pull), ready, release);
            // Ice-pick grip: blade points down (forward-down while raised).
            Vector3 bladeDir = Vector3.Slerp(new Vector3(0f, -0.45f, 1f), new Vector3(0f, -1f, 0.25f), Seg(t, 0.25f, 0.55f) * (1f - release));
            KnifeHand(ref p, current, e, hand, bladeDir, TorsoDir(p, 1f, 0.2f, -0.6f), 0f);

            float g = Seg(t, 0.05f, 0.3f) * (1f - 0.8f * release);
            Vector3 grab = VictimTorso(CollarFrontLocal) + new Vector3(0f, 0f, -0.04f);
            FreeHand(ref p, current, g, grab, TorsoDir(p, -1f, -0.8f, -0.4f));
            return p;
        }

        CharacterPose SeekerGutKnee(in CharacterPose current, float t)
        {
            float e = Seg(t, 0f, 0.25f);
            float release = Seg(t, 2.3f, 2.65f);
            float kneeUp = Seg(t, 0.3f, 0.55f) * (1f - Seg(t, 0.78f, 1.05f));
            float kneeHit = Mathf.Clamp01((t - 0.55f) / 0.13f) * (1f - Seg(t, 0.78f, 1.05f));
            float pullDown = Seg(t, 0.6f, 0.9f) * (1f - release);
            float stab1 = Mathf.Max(0f, QuickStabDown(t, 1.45f, 0.95f));
            float stab2 = Mathf.Max(0f, QuickStabDown(t, 2.05f, 1.7f));

            var p = SeekerBody(current, e, 10f * pullDown + 12f * kneeHit + 8f * (stab1 + stab2) - 8f * release, -6f * kneeHit,
                0.03f * pullDown, 0.06f * e + 0.12f * kneeHit);

            // Right knee into the stomach.
            float k = Mathf.Max(kneeUp, kneeHit);
            if (k > 0.001f)
            {
                Vector3 hip = p.HipPos(0);
                Vector3 sole = Vector3.Lerp(new Vector3(0.15f, 0f, -0.15f * e), hip + new Vector3(0.02f, -0.36f, 0.3f + 0.12f * kneeHit), k);
                p.SolveLeg(0, sole, Vector3.Lerp(new Vector3(0.1f, 0f, 1f), new Vector3(0f, 0.6f, 1f), k));
            }

            // Knife hand: on the victim's left shoulder for the knee, then raised and driven down into the back twice.
            Vector3 shoulderL = VictimTorso(ShoulderLeftGrabLocal) + new Vector3(0f, 0.05f, 0f);
            Vector3 backPt = VictimTorso(BackLocal);
            Vector3 raised = backPt + new Vector3(0.06f, 0.42f, -0.12f);
            Vector3 hit = backPt + new Vector3(0f, Blade, -0.02f);
            Vector3 ready = p.TorsoPoint(new Vector3(0.24f, 0.32f, 0.3f));
            Vector3 hand;
            if (t < 0.95f) hand = shoulderL;
            else
            {
                float s1 = QuickStabDown(t, 1.45f, 0.95f), s2 = QuickStabDown(t, 2.05f, 1.7f);
                float s = t < 1.7f ? s1 : s2;
                Vector3 from = t < 1.2f ? Vector3.Lerp(shoulderL, raised, Seg(t, 0.95f, 1.2f)) : raised;
                hand = s < 0f ? from : Vector3.LerpUnclamped(raised, hit, s);
            }
            hand = Vector3.Lerp(hand, ready, release);
            Vector3 bladeDir = t < 0.95f ? new Vector3(0f, -0.45f, 1f) : Vector3.Slerp(new Vector3(0f, -0.45f, 1f), new Vector3(0f, -1f, 0.2f), Seg(t, 0.95f, 1.2f) * (1f - release));
            KnifeHand(ref p, current, e, hand, bladeDir, TorsoDir(p, 1f, 0.1f, -0.6f), 0f);

            // Left hand: victim's right shoulder, pulling down for the knee, on the neck while stabbing.
            float g = Seg(t, 0.05f, 0.3f) * (1f - 0.8f * release);
            Vector3 grab = VictimTorso(ShoulderGrabLocal) + new Vector3(0f, 0.06f, 0f);
            FreeHand(ref p, current, g, grab, TorsoDir(p, -1f, -0.6f, -0.5f));
            return p;
        }

        /// <summary>Downward stab: -1 raised (windup), 1 blade in. <paramref name="from"/> = start of the windup.</summary>
        static float QuickStabDown(float t, float hit, float from)
        {
            if (t < from) return -1f;
            if (t < hit - 0.1f) return -1f;
            if (t < hit)
            {
                float x = (t - (hit - 0.1f)) / 0.1f;
                return -1f + 2f * x * x;
            }
            if (t < hit + 0.15f) return 1f;
            if (t < hit + 0.32f) return 1f - 2f * Seg(t, hit + 0.15f, hit + 0.32f);
            return -1f;
        }

        static readonly float[] FinisherStabs = { 0.6f, 0.95f, 1.3f };

        CharacterPose SeekerFinisher(in CharacterPose current, float t)
        {
            float e = Seg(t, 0f, 0.25f);
            float stab = QuickStab(t, FinisherStabs, 3);
            float thrust = Mathf.Max(0f, stab), cockS = Mathf.Max(0f, -stab);
            float follow = Seg(t, 1.35f, 2.0f);        // victim sinks to the knees, seeker bends with it
            float headGrab = Seg(t, 2.05f, 2.45f);
            float cock = Seg(t, 2.25f, 2.6f);
            float sl = ActionTimeline.Remap01(t, 2.62f, 2.85f);
            float slash = sl * sl * (3f - 2f * sl);
            float letGo = Seg(t, 3.45f, 3.75f);
            float stepBack = Seg(t, 3.75f, 4.25f);
            float flip = ActionTimeline.Remap01(t, KillCore.FinisherFlipTime, KillCore.FinisherFlipTime + 0.5f);
            float taunt = Seg(t, KillCore.FinisherTauntTime, KillCore.FinisherTauntTime + 0.4f) * (1f - Seg(t, 6.45f, 7.0f));
            float pump = Mathf.Sin(Mathf.PI * ActionTimeline.Remap01(t, 5.55f, 5.85f)) + Mathf.Sin(Mathf.PI * ActionTimeline.Remap01(t, 6.0f, 6.3f));

            float bent = follow * (1f - stepBack);
            float lean = 6f * thrust - 2f * cockS + 16f * bent + 6f * slash * (1f - letGo) - 10f * taunt - 3f * pump;
            float twist = 13f * cockS - 13f * thrust + 22f * cock * (1f - slash) - 26f * slash * (1f - letGo);
            var p = SeekerBody(current, e, lean, twist * (1f - stepBack * 0.8f), 0.1f * bent + 0.02f * thrust, 0.05f * e, 0.28f * stepBack);
            p.Head = p.Head * Quaternion.Euler(-14f * taunt - 6f * pump, 0f, -8f * taunt);

            Vector3 belly = VictimTorso(BellyLocal);
            Vector3 throat = VictimTorso(ThroatLocal);
            Vector3 hip = p.TorsoPoint(new Vector3(0.36f, 0.12f, -0.12f));
            Vector3 ready = p.TorsoPoint(new Vector3(0.24f, 0.32f, 0.3f));

            Vector3 hand, bladeDir;
            float roll = 0f;
            if (t < 1.6f)
            {
                Vector3 from = p.TorsoPoint(new Vector3(0.26f, 0.3f, 0f));
                Vector3 dir = (belly - from).normalized;
                Vector3 hit = belly - dir * Blade;
                Vector3 r = Vector3.Lerp(hip, hit, 0.5f) + new Vector3(0f, 0.04f, 0f);
                hand = stab < 0f ? Vector3.Lerp(r, hip, -stab) : Vector3.LerpUnclamped(r, hit, stab);
                bladeDir = dir;
            }
            else if (t < 2.62f)
            {
                Vector3 cockPos = throat + new Vector3(0.42f, 0.04f, -0.28f);
                hand = Vector3.Lerp(Vector3.Lerp(ready, hip, 0.4f), cockPos, cock);
                bladeDir = Vector3.Slerp(new Vector3(0f, -0.45f, 1f), new Vector3(-0.6f, 0f, 1f), cock);
            }
            else if (t < 3.75f)
            {
                Vector3 cockPos = throat + new Vector3(0.42f, 0.04f, -0.28f);
                Vector3 endPos = throat + new Vector3(-0.42f, -0.03f, -0.22f);
                hand = Bezier(cockPos, throat + new Vector3(0f, 0f, -0.15f), endPos, slash);
                hand = Vector3.Lerp(hand, ready, letGo);
                bladeDir = Vector3.Slerp(new Vector3(-0.7f, 0f, 1f), new Vector3(0f, -0.45f, 1f), letGo);
            }
            else
            {
                // Step back, knife flip (toss up, spin, catch), then the knife raised high.
                Vector3 front = p.TorsoPoint(new Vector3(0.2f, 0.38f, 0.32f));
                float toss = Mathf.Sin(Mathf.PI * flip) * 0.16f;
                Vector3 flipHand = front + new Vector3(0f, toss, 0f);
                Vector3 high = p.TorsoPoint(new Vector3(0.24f, 1.18f + 0.06f * pump, 0.06f));
                hand = Vector3.Lerp(Vector3.Lerp(ready, flipHand, Seg(t, 3.8f, 4.25f)), high, taunt);
                bladeDir = Vector3.Slerp(new Vector3(0f, -0.2f, 1f), Vector3.up + new Vector3(0f, 0f, 0.15f), taunt);
                // Spin around the blade's side axis: emulate by rotating the blade direction twice around X.
                if (flip > 0f && flip < 1f)
                    bladeDir = Quaternion.AngleAxis(720f * ActionTimeline.SmoothStep01(flip), Vector3.right) * new Vector3(0f, -0.2f, 1f);
            }
            KnifeHand(ref p, current, e, hand, bladeDir, TorsoDir(p, 1f, t > 4.9f ? 0.4f : -1f, -0.6f), roll);

            // Left hand: shoulder during the stabs, then the head (pull back), then wide open for the taunt.
            Vector3 shoulder = VictimTorso(ShoulderGrabLocal) + new Vector3(0f, 0.06f, 0f);
            Vector3 head = VictimHead(HeadBackLocal);
            Vector3 l = Vector3.Lerp(shoulder, head + new Vector3(0f, 0.04f, -0.06f * headGrab), headGrab);
            float g = Seg(t, 0.05f, 0.3f) * (1f - letGo);
            Vector3 openArm = p.TorsoPoint(new Vector3(-0.6f, 0.62f + 0.04f * pump, 0.12f));
            Vector3 lt = Vector3.Lerp(Vector3.Lerp(current.HandPos(1), l, g), openArm, taunt);
            p.SolveArm(1, lt, TorsoDir(p, -1f, -0.6f, -0.5f));
            return p;
        }

        // ================================================================== victim reactions

        /// <summary>Victim pose of the core, <paramref name="t"/> = seconds from the start of the core.</summary>
        CharacterPose CoreVictimPose(float t, Quaternion propR)
        {
            CharacterPose p;
            switch (killMove)
            {
                case KillMove.Stabs: return StabbedReactionPose(t, propR);
                case KillMove.ThroatSlit: p = FallToDowned(VictimThroatSlit(Mathf.Min(t, 2.5f)), t, 2.5f, 2.85f, 3.1f, 3.4f); break;
                case KillMove.Overhead: p = VictimOverhead(t); break;
                case KillMove.GutKnee: p = FallToDowned(VictimGutKnee(Mathf.Min(t, 2.3f)), t, 2.3f, 2.65f, 2.95f, 3.4f); break;
                case KillMove.Chainsaw: p = VictimChainsaw(t); break;
                case KillMove.Impale: p = FallToDowned(VictimImpale(Mathf.Min(t, 2.1f)), t, 2.1f, 2.45f, 2.75f, 3.5f); break;
                case KillMove.SawChop: p = VictimSawChop(t); break;
                case KillMove.SawSweep: p = VictimSawSweep(t); break;
                case KillMove.SawUpper: p = VictimSawUpper(t); break;
                case KillMove.SawNeck: p = VictimSawNeck(t); break;
                case KillMove.SawFinisher: p = VictimSawFinisher(t); break;
                case KillMove.GlockFinisher: p = VictimGlockFinisher(t); break;
                default: p = VictimFinisher(t); break;
            }
            p.PropR = propR;
            return p;
        }

        CharacterPose FallToDowned(in CharacterPose stand, float t, float a, float b, float c, float d)
        {
            if (t < a) return stand;
            var kneel = KneelPose(1f, -0.32f);
            if (t < b) return CharacterPose.Lerp(stand, kneel, ActionTimeline.EaseIn(ActionTimeline.Remap01(t, a, b)));
            var fours = AllFoursPose();
            if (t < c) return CharacterPose.Lerp(kneel, fours, ActionTimeline.EaseIn(ActionTimeline.Remap01(t, b, c)));
            return CharacterPose.Lerp(fours, DownedPose(), ActionTimeline.EaseOut(ActionTimeline.Remap01(t, c, d)));
        }

        /// <summary>Standing victim with curl (doubling over), flinch, head tilt, step back and hands at torso-local points.</summary>
        static CharacterPose VictimStand(float t, float grab, float curl, float flinch, float headPitch, float stepBack, float roll, float sink,
            Vector3 handR, Vector3 handL)
        {
            var p = CharacterPose.Identity;
            p.PelvisPos = new Vector3(0.012f * Mathf.Sin(t * 9f) * curl, 0.655f - 0.08f * curl - 0.025f * flinch - sink,
                0.02f * grab - 0.13f * curl - 0.035f * flinch - 0.2f * stepBack);
            p.Pelvis = Quaternion.Euler(-5f * flinch, 0f, 2f * Mathf.Sin(t * 3f) + roll * 0.3f);
            p.Torso = Quaternion.Euler(3f * grab + 13f * curl + 9f * flinch, 3f * Mathf.Sin(t * 5f) * curl, 7f * grab + roll);
            p.Head = Quaternion.Euler(headPitch, 6f * Mathf.Sin(t * 7f) * flinch, 14f * grab - roll * 0.5f);

            float liftR = Mathf.Sin(Mathf.PI * Mathf.Clamp01(stepBack * 2f)) * 0.06f;
            float liftL = Mathf.Sin(Mathf.PI * Mathf.Clamp01(stepBack * 2f - 1f)) * 0.06f;
            float kneeBend = sink * 0.9f;
            p.SolveLeg(0, new Vector3(0.13f, liftR, 0.02f - 0.2f * Mathf.Clamp01(stepBack * 2f) + kneeBend * 0.2f), new Vector3(0.1f, 0f, 1f));
            p.SolveLeg(1, new Vector3(-0.13f, liftL, -0.02f - 0.2f * Mathf.Clamp01(stepBack * 2f - 1f) + kneeBend * 0.2f), new Vector3(-0.1f, 0f, 1f));

            Vector3 jerk = new Vector3(0f, 0.03f * flinch, -0.05f * flinch);
            p.SolveArm(0, p.TorsoPoint(handR + jerk + new Vector3(0.04f * flinch, 0f, 0f)), TorsoDir(p, 1f, -1f, -0.5f));
            p.SolveArm(1, p.TorsoPoint(handL + jerk - new Vector3(0.04f * flinch, 0f, 0f)), TorsoDir(p, -1f, -1f, -0.5f));
            return p;
        }

        static readonly Vector3 HandsRestR = new Vector3(0.1f, 0.45f, 0.3f), HandsRestL = new Vector3(-0.12f, 0.42f, 0.28f);
        static readonly Vector3 ThroatHoldR = new Vector3(0.05f, 0.64f, 0.2f), ThroatHoldL = new Vector3(-0.06f, 0.6f, 0.21f);
        static readonly Vector3 BellyHoldR = new Vector3(0.06f, 0.25f, 0.235f), BellyHoldL = new Vector3(-0.08f, 0.28f, 0.235f);

        static CharacterPose VictimThroatSlit(float t)
        {
            float grab = Seg(t, 0f, 0.25f);
            float fl = Pulse(t, 0.82f, 1.2f) + Pulse(t, 1.65f);
            float throat = Seg(t, 0.88f, 1.12f);
            float curl = 0.1f * grab + 0.15f * Seg(t, 0.82f, 1.1f) + 0.3f * Seg(t, 1.65f, 1.95f);
            float head = -28f * Pulse(t, 0.82f, 1.2f) + 20f * throat + 12f * Seg(t, 1.65f, 2f);
            float step = Seg(t, 1.95f, 2.45f);
            Vector3 r = Vector3.Lerp(HandsRestR, ThroatHoldR, throat);
            Vector3 l = Vector3.Lerp(HandsRestL, ThroatHoldL, throat);
            l = Vector3.Lerp(l, BellyHoldL, Seg(t, 1.7f, 1.95f));
            return VictimStand(t, grab, curl, fl, head, step, 0f, 0f, r, l);
        }

        CharacterPose VictimOverhead(float t)
        {
            float grab = Seg(t, 0f, 0.25f);
            float fl = Pulse(t, 0.88f, 1.3f) + Pulse(t, 1.55f, 1.2f);
            float hold = Seg(t, 0.95f, 1.2f);
            float sink = 0.07f * Seg(t, 0.88f, 1.15f);
            float roll = -14f * Seg(t, 0.88f, 1.1f);
            float lookUp = -22f * Seg(t, 0.35f, 0.7f) * (1f - Seg(t, 0.88f, 1.0f));
            Vector3 r = Vector3.Lerp(HandsRestR, new Vector3(0.12f, 0.72f, 0.3f), Seg(t, 0.3f, 0.7f)); // reaching up to stop the arm
            r = Vector3.Lerp(r, new Vector3(0.08f, 0.5f, 0.22f), Seg(t, 0.9f, 1.1f));
            Vector3 l = Vector3.Lerp(HandsRestL, new Vector3(-0.08f, 0.62f, 0.14f), hold); // hand on the wounded collarbone
            float ts = Mathf.Min(t, 1.62f);
            var stand = VictimStand(ts, grab, 0.12f * grab + 0.1f * hold, fl, lookUp + 14f * hold, 0f, roll, sink, r, l);
            if (t < 1.62f) return stand;

            // Second stab drives the victim down onto the knees, then forward to the floor.
            var kneel = KneelPose(0.35f, -0.08f);
            kneel.Torso = kneel.Torso * Quaternion.Euler(-8f, 0f, -10f);
            if (t < 1.95f) return CharacterPose.Lerp(stand, kneel, ActionTimeline.EaseIn(ActionTimeline.Remap01(t, 1.62f, 1.95f)));
            if (t < 2.4f) return kneel;
            var fours = AllFoursPose();
            if (t < 2.7f) return CharacterPose.Lerp(kneel, fours, ActionTimeline.EaseIn(ActionTimeline.Remap01(t, 2.4f, 2.7f)));
            return CharacterPose.Lerp(fours, DownedPose(), ActionTimeline.EaseOut(ActionTimeline.Remap01(t, 2.7f, 3.3f)));
        }

        static CharacterPose VictimGutKnee(float t)
        {
            float grab = Seg(t, 0f, 0.25f);
            float bend = Seg(t, 0.66f, 0.84f);
            float fl = Pulse(t, 0.68f, 1.3f) + Pulse(t, 1.45f) + Pulse(t, 2.05f);
            Vector3 r = Vector3.Lerp(HandsRestR, BellyHoldR, bend);
            Vector3 l = Vector3.Lerp(HandsRestL, BellyHoldL, bend);
            var p = VictimStand(t, grab, 0.08f * grab, fl, -10f * bend, 0f, 0f, 0.07f * bend, r, l);
            // Doubled over: torso folds forward from the hips, hips pushed back, head still up a little.
            p.PelvisPos += new Vector3(0f, -0.02f * bend, -0.1f * bend);
            p.Torso = Quaternion.Euler(52f * bend + 6f * (Pulse(t, 1.45f) + Pulse(t, 2.05f)), 0f, 0f) * p.Torso;
            p.Head = Quaternion.Euler(-25f * bend, 0f, 0f) * p.Head;
            p.SolveArm(0, p.TorsoPoint(r), TorsoDir(p, 1f, -1f, -0.5f));
            p.SolveArm(1, p.TorsoPoint(l), TorsoDir(p, -1f, -1f, -0.5f));
            p.SolveLeg(0, new Vector3(0.13f, 0f, 0.04f), new Vector3(0.1f, 0f, 1f));
            p.SolveLeg(1, new Vector3(-0.13f, 0f, -0.02f), new Vector3(-0.1f, 0f, 1f));
            return p;
        }

        CharacterPose VictimFinisher(float t)
        {
            // 1. Three stabs standing.
            float ts = Mathf.Min(t, 1.4f);
            float grab = Seg(ts, 0f, 0.25f);
            float fl = Pulse(ts, 0.6f) + Pulse(ts, 0.95f) + Pulse(ts, 1.3f, 1.25f);
            float curl = 0.12f * grab + 0.2f * Seg(ts, 0.6f, 0.85f) + 0.2f * Seg(ts, 0.95f, 1.2f) + 0.25f * Seg(ts, 1.3f, 1.5f);
            float hb = Seg(ts, 0.95f, 1.2f);
            var stand = VictimStand(ts, grab, curl, fl, 28f * curl - 16f * fl, 0f, 0f, 0f,
                Vector3.Lerp(HandsRestR, BellyHoldR, hb), Vector3.Lerp(HandsRestL, BellyHoldL, hb));
            if (t < 1.4f) return stand;

            // 2. Onto the knees (upright), head pulled back, throat slit, hands to the throat.
            var kneel = KneelPose(0.25f, -0.06f);
            float headBack = Seg(t, 2.1f, 2.5f) * (1f - Seg(t, 3.0f, 3.4f));
            float slashFl = Pulse(t, 2.75f, 1.4f);
            float toThroat = Seg(t, 2.82f, 3.05f);
            kneel.Torso = Quaternion.Euler(-12f * headBack + 8f * slashFl + 10f * Seg(t, 3.1f, 3.6f), 0f, 0f) * kneel.Torso;
            kneel.Head = Quaternion.Euler(-38f * headBack - 10f * slashFl + 20f * Seg(t, 3.1f, 3.6f), 4f * Mathf.Sin(t * 6f) * toThroat, 0f);
            Vector3 hr = Vector3.Lerp(BellyHoldR, ThroatHoldR, toThroat), hl = Vector3.Lerp(BellyHoldL, ThroatHoldL, toThroat);
            hr += new Vector3(0f, 0.02f * Mathf.Sin(t * 11f), 0f) * toThroat;
            kneel.SolveArm(0, kneel.TorsoPoint(hr), TorsoDir(kneel, 1f, -1f, -0.5f));
            kneel.SolveArm(1, kneel.TorsoPoint(hl), TorsoDir(kneel, -1f, -1f, -0.5f));
            if (t < 1.85f) return CharacterPose.Lerp(stand, kneel, ActionTimeline.EaseIn(ActionTimeline.Remap01(t, 1.4f, 1.85f)));
            if (t < 3.7f) return kneel;

            // 3. Topples onto the side, rolls onto the back: dead.
            var side = SidePose();
            if (t < 4.1f) return CharacterPose.Lerp(kneel, side, ActionTimeline.EaseIn(ActionTimeline.Remap01(t, 3.7f, 4.1f)));
            return CharacterPose.Lerp(side, DeadPose(), ActionTimeline.EaseOut(ActionTimeline.Remap01(t, 4.1f, KillCore.FinisherVictimDead)));
        }
            // ================================================================== chainsaw

        bool sawKill;

        static void SolveSawHands(ref CharacterPose p, Vector3 pos, Quaternion rot)
        {
            p.SolveArm(0, pos, TorsoDir(p, 1f, -1f, -0.5f));
            p.PropR = Quaternion.Inverse(p.ForearmRoot(0)) * rot;
            p.SolveArm(1, p.HandPos(0) + rot * BlockyProps.ChainsawLeftGripLocal, TorsoDir(p, -1f, -0.6f, -0.4f));
        }

        static Quaternion AimRot(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            return Quaternion.LookRotation(d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.forward, Vector3.up);
        }

        CharacterPose SeekerChainsaw(in CharacterPose current, float t)
        {
            float e = Seg(t, 0f, 0.3f);
            float ram = Seg(t, 0.3f, 0.5f);
            float lift = Seg(t, 0.6f, 2.2f);
            float rip = Seg(t, 2.25f, 2.6f);
            float raise = Seg(t, 2.8f, 3.3f) * (1f - Seg(t, 3.8f, 4.3f));
            bool grinding = t > KillCore.ChainsawGrindStart && t < KillCore.ChainsawGrindEnd;
            float shake = grinding ? 1f : (t > 2.8f && t < 3.8f ? 0.5f : 0.15f);
            Vector3 jit = new Vector3(Mathf.Sin(t * 83f), Mathf.Sin(t * 67f + 1f), Mathf.Sin(t * 59f + 2f)) * 0.012f * shake;

            float lean = 10f * ram + 8f * lift * (1f - rip) - 12f * raise;
            var p = SeekerBody(current, e, lean, -8f * rip + (grinding ? 4f * Mathf.Sin(t * 40f) : 0f),
                0.05f * ram * (1f - rip), 0.1f * ram * (1f - rip), 0.15f * Seg(t, 2.6f, 3.0f));
            p.Head = p.Head * Quaternion.Euler(-16f * raise, 0f, 0f);

            Vector3 belly = VictimTorso(BellyLocal), chest = VictimTorso(ChestLocal);
            Vector3 cut = Vector3.Lerp(belly, chest + new Vector3(0f, 0.08f, 0f), lift);
            Vector3 hold = p.TorsoPoint(new Vector3(0.18f, 0.18f, 0.22f));
            Vector3 holdAim = hold + new Vector3(-0.05f, 0.12f, 1f);
            // Fist placed so that the middle of the bar sits in the cut.
            Vector3 dir = (cut - p.TorsoPoint(new Vector3(0.1f, 0.25f, 0f))).normalized;
            Vector3 inFist = cut - dir * 0.48f;
            Vector3 fist = Vector3.Lerp(hold, inFist, ram);
            Vector3 aim = Vector3.Lerp(holdAim, cut + dir, ram);
            // Rip out: up and to the right.
            fist = Vector3.Lerp(fist, p.TorsoPoint(new Vector3(0.3f, 0.45f, 0.3f)), rip);
            aim = Vector3.Lerp(aim, fist + new Vector3(0.5f, 0.6f, 0.6f), rip);
            // Raise overhead, bar pointing at the ceiling.
            Vector3 up = p.TorsoPoint(new Vector3(0.12f, 0.95f, 0.12f));
            fist = Vector3.Lerp(fist, up, raise);
            aim = Vector3.Lerp(aim, up + new Vector3(0f, 1f, 0.25f), raise);
            fist = Vector3.Lerp(current.HandPos(0), fist, e) + jit;
            SolveSawHands(ref p, fist, AimRot(fist, aim));
            return p;
        }

        CharacterPose VictimChainsaw(float t)
        {
            float grind = t > KillCore.ChainsawGrindStart && t < KillCore.ChainsawGrindEnd ? 1f : 0f;
            float hit = Pulse(t, 0.5f, 1.6f);
            float raiseHands = Seg(t, 0.05f, 0.35f);
            float grab = Seg(t, 0.5f, 0.7f) * (1f - Seg(t, 2.25f, 2.45f));
            float shakeT = grind * (0.6f + 0.4f * Mathf.Sin(t * 7f));
            float s1 = Mathf.Sin(t * 71f) * shakeT, s2 = Mathf.Sin(t * 53f + 1.3f) * shakeT;

            Vector3 defR = new Vector3(0.12f, 0.62f, 0.32f), defL = new Vector3(-0.12f, 0.6f, 0.32f);
            Vector3 r = Vector3.Lerp(Vector3.Lerp(HandsRestR, defR, raiseHands), new Vector3(0.1f, 0.32f, 0.3f), grab);
            Vector3 l = Vector3.Lerp(Vector3.Lerp(HandsRestL, defL, raiseHands), new Vector3(-0.1f, 0.34f, 0.3f), grab);
            float ts = Mathf.Min(t, 2.4f);
            var stand = VictimStand(ts, 1f, 0.15f + 0.2f * grab, hit + 0.4f * Mathf.Abs(s1), -30f * grab + 12f * s2, 0f, 10f * s1, 0.05f * grab, r, l);
            stand.PelvisPos += new Vector3(0.02f * s2, 0.015f * Mathf.Abs(s1), 0f);
            stand.Torso = stand.Torso * Quaternion.Euler(-12f * grab + 6f * s1, 8f * s2, 0f);
            if (t < 2.4f) return stand;

            // Ripped free: knees, topples onto the side and the back.
            var kneel = KneelPose(0.8f, -0.25f);
            if (t < 2.75f) return CharacterPose.Lerp(stand, kneel, ActionTimeline.EaseIn(ActionTimeline.Remap01(t, 2.4f, 2.75f)));
            var side = SidePose();
            if (t < 3.2f) return CharacterPose.Lerp(kneel, side, ActionTimeline.EaseIn(ActionTimeline.Remap01(t, 2.75f, 3.2f)));
            return CharacterPose.Lerp(side, DeadPose(), ActionTimeline.EaseOut(ActionTimeline.Remap01(t, 3.2f, KillCore.ChainsawVictimDead)));
        }
    }
}
