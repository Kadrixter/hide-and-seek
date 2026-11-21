using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Knife kill intros (struggle before the killing blows, see <see cref="KillIntro"/>). Seeker at the root facing +Z,
    /// victim 0.85 m ahead facing the seeker. Every intro ends with the victim standing upright at its root and the
    /// seeker in the fighting stance, so the core starts from the same place it always did.
    /// </summary>
    public partial class BlockyCharacterActions
    {
        static readonly Vector3 KnifeDir = new Vector3(0f, -0.45f, 1f);
        static readonly Vector3 BackCollarLocal = new Vector3(0f, 0.6f, -0.13f);

        /// <summary>Seeker feet placed explicitly (stance with left foot forward), each with its own forward offset and lift.</summary>
        static void SeekerFeet(ref CharacterPose p, float e, Vector3 right, Vector3 left)
        {
            p.SolveLeg(0, new Vector3(0.15f, 0f, -0.15f * e) + right, new Vector3(0.1f, 0f, 1f));
            p.SolveLeg(1, new Vector3(-0.13f, 0f, 0.17f * e) + left, new Vector3(-0.1f, 0f, 1f));
        }

        // ================================================================== wrist lock

        CharacterPose SeekerWristLock(in CharacterPose current, float t)
        {
            float e = Seg(t, 0f, 0.3f);
            float lunge = Seg(t, 0.05f, 0.4f);
            float effort = Seg(t, 0.55f, 0.7f) * (1f - Seg(t, 2.05f, 2.2f));
            float headPrep = Seg(t, 1.9f, 2.06f) * (1f - Seg(t, 2.06f, 2.12f));
            float butt = Seg(t, 2.06f, 2.15f) * (1f - Seg(t, 2.22f, 2.55f));
            float settle = Seg(t, 2.3f, 2.9f);
            float surge = 0.5f + 0.5f * Mathf.Sin(t * 5.5f);
            float aside = Seg(t, 1.5f, 1.9f) * (1f - Seg(t, 2.1f, 2.4f));

            float lean = 4f * lunge + effort * (7f + 4f * surge) - 9f * headPrep + 15f * butt - 3f * settle;
            var p = SeekerBody(current, e, lean, -9f * aside, 0.05f * effort, 0.02f * lunge + 0.02f * effort + 0.04f * butt - 0.03f * settle);
            p.Head = p.Head * Quaternion.Euler(-16f * headPrep + 28f * butt, 0f, 0f);
            SeekerFeet(ref p, e, new Vector3(0f, 0f, 0.03f * lunge - 0.06f * effort), Step(Vector3.zero, new Vector3(0f, 0f, 0.1f), t, 0.05f, 0.35f, 0.06f));

            Vector3 ready = p.TorsoPoint(new Vector3(0.24f, 0.32f, 0.3f));
            Vector3 high = p.TorsoPoint(new Vector3(0.28f, 1.0f, 0.1f));
            Vector3 chest = VictimTorso(ChestLocal);
            // Contact point of the fist while the victim holds the wrist: in, pushed back up, twisted aside.
            Vector3 w = Vector3.Lerp(new Vector3(0.1f, 1.33f, 0.37f), new Vector3(0.07f, 1.22f, 0.48f), Seg(t, 0.65f, 1.1f));
            w = Vector3.Lerp(w, new Vector3(0.12f, 1.37f, 0.38f), Seg(t, 1.1f, 1.45f));
            w = Vector3.Lerp(w, new Vector3(0.27f, 1.28f, 0.41f), aside);
            w += Tremble(t, 0.009f) * effort + new Vector3(0f, 0f, 0.05f * butt);

            Vector3 hand;
            if (t < 0.4f) hand = Vector3.Lerp(ready, high, Seg(t, 0.08f, 0.4f));
            else if (t < 0.6f) hand = Vector3.Lerp(high, w, Seg(t, 0.4f, 0.58f));
            else if (t < 2.15f) hand = w;
            else hand = Vector3.Lerp(w, ready, Seg(t, 2.2f, 2.75f));
            // Ice-pick grip for the downward stab, flipped back to the normal grip for the core (grip change).
            float ice = Seg(t, 0.1f, 0.4f) * (1f - Seg(t, 2.35f, 2.8f));
            Vector3 bladeDir = Vector3.Slerp(KnifeDir, (chest - hand).normalized + Vector3.down * 0.4f, ice);
            KnifeHand(ref p, current, e, hand, bladeDir, TorsoDir(p, 1f, 0.2f, -0.6f));

            float g = Seg(t, 0.1f, 0.35f);
            FreeHand(ref p, current, g, VictimTorso(CollarFrontLocal) + new Vector3(0f, 0.02f, -0.05f) + Tremble(t, 0.005f) * effort,
                TorsoDir(p, -1f, -0.6f, -0.5f));
            return p;
        }

        CharacterPose VictimWristLock(float t)
        {
            float guard = Seg(t, 0.05f, 0.3f) * (1f - Seg(t, 2.55f, 2.9f));
            float hold = Seg(t, 0.42f, 0.58f) * (1f - Seg(t, 2.15f, 2.32f));
            float pushed = Seg(t, 0.65f, 1.1f) * (1f - Seg(t, 1.1f, 1.45f));
            float aside = Seg(t, 1.5f, 1.9f) * (1f - Seg(t, 2.15f, 2.5f));
            float brace = Seg(t, 0.4f, 0.7f) * (1f - Seg(t, 2.35f, 2.85f));
            float butt = Pulse(t, 2.15f, 1.5f);
            float daze = Seg(t, 2.15f, 2.3f) * (1f - Seg(t, 2.55f, 2.9f));
            float startle = Pulse(t, 0.06f, 1f);

            var p = CharacterPose.Identity;
            p.PelvisPos = new Vector3(0.03f * aside, 0.655f - 0.07f * brace - 0.03f * pushed - 0.02f * startle,
                -0.03f * hold - 0.1f * pushed - 0.05f * startle - 0.1f * daze);
            p.Pelvis = Quaternion.Euler(0f, -12f * aside, 2f * Mathf.Sin(t * 3f) * hold);
            p.Torso = Quaternion.Euler(3f * hold - 16f * pushed - 14f * butt + 7f * daze - 4f * startle, -16f * aside, 4f * Mathf.Sin(t * 4f) * hold);
            p.Head = Quaternion.Euler(-12f * hold + 12f * pushed - 34f * butt + 12f * daze, 8f * aside + 14f * Mathf.Sin(t * 2.3f) * daze,
                10f * Mathf.Sin(t * 2.9f) * daze);
            Vector3 rf = new Vector3(0.14f, 0f, Mathf.Lerp(0.02f, -0.27f, brace) - 0.12f * daze);
            Vector3 lf = Step(new Vector3(-0.13f, 0f, -0.02f), new Vector3(-0.14f, 0f, 0.08f), t, 0.35f, 0.6f, 0.05f);
            lf.z -= 0.08f * Seg(t, 2.6f, 2.9f);
            p.SolveLeg(0, rf, new Vector3(0.1f, 0f, 1f));
            p.SolveLeg(1, lf, new Vector3(-0.1f, 0f, 1f));

            Vector3 gr = p.TorsoPoint(Vector3.Lerp(HandsRestR, new Vector3(0.14f, 0.62f, 0.32f), guard));
            Vector3 gl = p.TorsoPoint(Vector3.Lerp(HandsRestL, new Vector3(-0.14f, 0.6f, 0.32f), guard));
            Vector3 tr = Tremble(t, 0.006f) * hold;
            p.SolveArm(0, Vector3.Lerp(gr, KillerForearm(0, 0.6f) + tr, hold), TorsoDir(p, 1f, -0.8f, -0.5f));
            p.SolveArm(1, Vector3.Lerp(gl, KillerForearm(0, 0.95f) - tr, hold), TorsoDir(p, -1f, -0.8f, -0.5f));
            return p;
        }

        // ================================================================== flee (grabbed by the collar)

        CharacterPose SeekerFlee(in CharacterPose current, float t)
        {
            float e = Seg(t, 0f, 0.3f);
            float lunge = Seg(t, 0.28f, 0.85f);
            float yank = Seg(t, 0.92f, 1.08f) * (1f - Seg(t, 1.6f, 2.0f));
            float pullBack = Seg(t, 0.92f, 1.3f);
            float hold = Seg(t, 1.6f, 2.0f);
            float spin = Seg(t, 1.3f, 1.8f) * (1f - hold);
            float fwd = 0.42f * lunge * (1f - pullBack) + 0.03f * hold;
            float lean = 16f * lunge * (1f - pullBack) - 12f * yank + 5f * hold + 2.5f * Mathf.Sin(t * 7f) * hold;
            var p = SeekerBody(current, e, lean, -16f * spin, 0.04f * lunge * (1f - pullBack) + 0.02f * hold, fwd);
            // Two quick steps after the runner, two steps back while pulling.
            Vector3 right = Step(Vector3.zero, new Vector3(0f, 0f, 0.34f), t, 0.5f, 0.82f);
            right = Step(right, new Vector3(0f, 0f, -0.06f), t, 0.95f, 1.2f, 0.07f);
            Vector3 left = Step(Vector3.zero, new Vector3(0f, 0f, 0.42f), t, 0.28f, 0.58f);
            left = Step(left, new Vector3(0f, 0f, 0.0f), t, 1.1f, 1.35f, 0.07f);
            SeekerFeet(ref p, e, right, left);

            // Knife hand low while running, cocked back by the hip while holding (threat), ready for the core.
            Vector3 ready = p.TorsoPoint(new Vector3(0.24f, 0.32f, 0.3f));
            Vector3 low = p.TorsoPoint(new Vector3(0.3f, 0.16f, 0.14f));
            Vector3 cocked = p.TorsoPoint(new Vector3(0.36f, 0.22f, -0.1f));
            Vector3 hand = Vector3.Lerp(Vector3.Lerp(ready, low, Seg(t, 0.3f, 0.6f)), cocked, hold);
            hand = Vector3.Lerp(hand, ready, Seg(t, 2.45f, 2.85f));
            KnifeHand(ref p, current, e, hand, Vector3.Slerp(KnifeDir, new Vector3(0f, 0.3f, 1f), hold * (1f - Seg(t, 2.45f, 2.85f))), TorsoDir(p, 1f, -1f, -0.6f));

            // Left hand: grab the back of the collar, regrip to the front while spinning the victim round.
            float reach = Seg(t, 0.5f, 0.92f);
            Vector3 back = VictimTorso(BackCollarLocal) + new Vector3(0f, 0.03f, 0f);
            Vector3 front = VictimTorso(CollarFrontLocal) + new Vector3(0f, 0.02f, -0.05f);
            Vector3 tgt = Vector3.Lerp(back, front, Seg(t, 1.45f, 1.85f)) + Tremble(t, 0.008f) * hold;
            FreeHand(ref p, current, reach, tgt, TorsoDir(p, -1f, -0.6f, -0.5f));
            return p;
        }

        CharacterPose VictimFlee(float t)
        {
            float startle = Pulse(t, 0.05f, 1f);
            float turn = Seg(t, 0.08f, 0.42f);
            float run = Seg(t, 0.3f, 0.9f);
            float yank = Seg(t, 0.92f, 1.06f);
            float drag = Seg(t, 1.0f, 1.72f);
            float spin = Seg(t, 1.3f, 1.8f);
            float pry = Seg(t, 1.75f, 1.95f) * (1f - Seg(t, 2.5f, 2.85f));

            // Running away (local frame facing away), arched back when the collar is yanked.
            float ph = t < 0.92f ? ActionTimeline.Remap01(t, 0.22f, 0.92f) : 1f + 0.7f * ActionTimeline.Remap01(t, 0.92f, 1.6f);
            var stride = StridePose(ph, 20f * (1f - yank) - 26f * yank, yank);
            stride.Head = stride.Head * Quaternion.Euler(-24f * yank, 0f, 0f);
            var neutral = VictimStand(0f, 0f, 0f, startle, -6f * startle, 0f, 0f, 0f, HandsRestR, HandsRestL);
            var runPose = CharacterPose.Lerp(neutral, stride, Seg(t, 0.06f, 0.3f));
            var away = Turned(runPose, 155f * turn, new Vector3(0.06f * turn, 0f, -0.6f * run * (1f - drag)));

            // Facing the seeker again, held by the collar: both hands try to pry the seeker's fist off, squirming.
            float sq = Mathf.Sin(t * 6.5f);
            var stand = VictimStand(t, 0f, 0.06f, 0f, -10f + 6f * sq * pry, 0f, 4f * sq * pry, 0.03f * pry, HandsRestR, HandsRestL);
            stand.Pelvis = Quaternion.Euler(0f, 9f * sq * pry, 0f) * stand.Pelvis;
            stand.Head = stand.Head * Quaternion.Euler(0f, -18f * pry + 8f * Mathf.Sin(t * 4f) * pry, 0f);
            stand.SolveLeg(0, new Vector3(0.13f, 0.05f * Mathf.Max(0f, sq) * pry, 0.02f), new Vector3(0.1f, 0f, 1f));
            ArmTo(ref stand, 0, KillerForearm(1, 0.92f) + Tremble(t, 0.006f), pry);
            ArmTo(ref stand, 1, KillerForearm(1, 0.55f) - Tremble(t, 0.006f), pry);

            return CharacterPose.Lerp(away, stand, spin);
        }

        // ================================================================== shove, punch, arm twist

        CharacterPose SeekerShove(in CharacterPose current, float t)
        {
            float e = Seg(t, 0f, 0.25f);
            float shoved = Seg(t, 0.26f, 0.42f) * (1f - Seg(t, 0.72f, 1.1f));
            float block = Seg(t, 1.12f, 1.34f) * (1f - Seg(t, 1.5f, 1.66f));
            float twistArm = Seg(t, 1.42f, 1.62f) * (1f - Seg(t, 1.8f, 2.1f));
            float pullIn = Seg(t, 1.75f, 2.1f);
            float lean = -13f * shoved + 4f * block + 12f * twistArm + 5f * pullIn;
            var p = SeekerBody(current, e, lean, 10f * block - 20f * twistArm, 0.05f * twistArm, 0.04f * e + 0.03f * pullIn, 0.24f * shoved);
            p.Head = p.Head * Quaternion.Euler(-10f * shoved - 6f * block, 0f, 0f);
            Vector3 right = Step(Vector3.zero, new Vector3(0f, 0f, -0.2f), t, 0.3f, 0.5f, 0.06f);
            right = Step(right, Vector3.zero, t, 0.8f, 1.05f, 0.06f);
            SeekerFeet(ref p, e, right, new Vector3(0f, 0f, -0.04f * shoved));

            Vector3 ready = p.TorsoPoint(new Vector3(0.24f, 0.32f, 0.3f));
            Vector3 low = p.TorsoPoint(new Vector3(0.32f, 0.16f, 0.1f));
            Vector3 flail = p.TorsoPoint(new Vector3(0.42f, 0.4f, 0.05f));
            Vector3 hand = Vector3.Lerp(Vector3.Lerp(ready, low, Seg(t, 0.1f, 0.3f)), flail, shoved);
            hand = Vector3.Lerp(hand, ready, Seg(t, 2.05f, 2.6f));
            KnifeHand(ref p, current, e, hand, KnifeDir, TorsoDir(p, 1f, -1f, -0.6f));

            // Left: flails, blocks the punch with the forearm, grabs and twists the wrist down, then the collar.
            Vector3 lRest = p.TorsoPoint(new Vector3(-0.24f, 0.36f, 0.26f));
            Vector3 lFlail = p.TorsoPoint(new Vector3(-0.44f, 0.42f, 0.08f));
            Vector3 lBlock = p.TorsoPoint(new Vector3(-0.06f, 0.98f, 0.32f));
            Vector3 lTwist = p.TorsoPoint(new Vector3(0.04f, 0.48f, 0.46f));
            Vector3 collar = VictimTorso(CollarFrontLocal) + new Vector3(0f, 0.02f, -0.05f);
            Vector3 l = Vector3.Lerp(Vector3.Lerp(lRest, lFlail, shoved), lBlock, block);
            l = Vector3.Lerp(l, lTwist, twistArm);
            l = Vector3.Lerp(l, collar, pullIn);
            Vector3 pole = Vector3.Lerp(TorsoDir(p, -1f, -0.6f, -0.5f), TorsoDir(p, -1f, -0.1f, 0.2f), block);
            p.SolveArm(1, Vector3.Lerp(current.HandPos(1), l, e), pole);
            return p;
        }

        CharacterPose VictimShove(float t)
        {
            float push = Seg(t, 0.05f, 0.28f) * (1f - Seg(t, 0.4f, 0.75f));
            float wind = Seg(t, 0.95f, 1.22f) * (1f - Seg(t, 1.24f, 1.42f));
            float punch = Seg(t, 1.22f, 1.42f) * (1f - Seg(t, 1.45f, 1.6f));
            float twisted = Seg(t, 1.42f, 1.62f) * (1f - Seg(t, 1.95f, 2.45f));
            float pulled = Seg(t, 1.75f, 2.1f) * (1f - Seg(t, 2.35f, 2.8f));
            float hurt = Pulse(t, 1.45f, 1.2f);

            var p = CharacterPose.Identity;
            p.PelvisPos = new Vector3(0f, 0.655f - 0.09f * twisted - 0.03f * push, 0.1f * push - 0.04f * wind + 0.08f * punch + 0.07f * pulled);
            p.Pelvis = Quaternion.Euler(0f, 14f * wind - 10f * punch, 0f);
            p.Torso = Quaternion.Euler(14f * push + 6f * punch + 32f * twisted + 6f * pulled, 22f * wind - 18f * punch + 16f * twisted, -10f * twisted);
            p.Head = Quaternion.Euler(-8f * push - 6f * wind + 6f * twisted - 14f * hurt, -12f * twisted, 8f * hurt);
            Vector3 rf = Step(new Vector3(0.13f, 0f, 0.02f), new Vector3(0.13f, 0f, 0.18f), t, 0.05f, 0.25f, 0.06f);
            rf = Step(rf, new Vector3(0.14f, 0f, -0.12f), t, 0.45f, 0.75f, 0.06f);
            rf = Step(rf, new Vector3(0.13f, 0f, 0.06f), t, 1.2f, 1.4f, 0.05f);
            rf = Step(rf, new Vector3(0.13f, 0f, 0.02f), t, 2.3f, 2.7f, 0.05f);
            Vector3 lf = new Vector3(-0.13f, 0f, -0.02f + 0.04f * pulled);
            p.SolveLeg(0, rf, new Vector3(0.1f, 0f, 1f));
            p.SolveLeg(1, lf, new Vector3(-0.1f, 0f, 1f));

            Vector3 restR = p.TorsoPoint(Vector3.Lerp(HandsRestR, new Vector3(0.14f, 0.62f, 0.32f), Seg(t, 2.3f, 2.8f)));
            Vector3 restL = p.TorsoPoint(Vector3.Lerp(HandsRestL, new Vector3(-0.14f, 0.6f, 0.32f), Seg(t, 2.3f, 2.8f)));
            Vector3 chestR = KillerBone(2, new Vector3(-0.12f, 0.45f, 0.14f), new Vector3(-0.12f, 1.1f, 0.12f));
            Vector3 chestL = KillerBone(2, new Vector3(0.12f, 0.45f, 0.14f), new Vector3(0.12f, 1.1f, 0.12f));
            Vector3 ear = p.TorsoPoint(new Vector3(0.24f, 0.72f, -0.02f));
            Vector3 blockPt = KillerForearm(1, 0.7f);
            Vector3 held = KillerHand(1) + new Vector3(0f, -0.03f, 0f);
            Vector3 r = Vector3.Lerp(restR, chestR, push);
            r = Vector3.Lerp(r, ear, wind);
            r = Vector3.Lerp(r, blockPt, punch);
            r = Vector3.Lerp(r, held, Mathf.Max(twisted, pulled * 0.6f));
            Vector3 l = Vector3.Lerp(restL, chestL, push);
            l = Vector3.Lerp(l, p.TorsoPoint(new Vector3(-0.2f, 0.55f, 0.25f)), wind);
            l = Vector3.Lerp(l, KillerForearm(1, 0.4f), twisted);
            l = Vector3.Lerp(l, chestL, pulled * (1f - twisted));
            p.SolveArm(0, r, TorsoDir(p, 1f, -0.6f, -0.5f));
            p.SolveArm(1, l, TorsoDir(p, -1f, -0.8f, -0.5f));
            return p;
        }
    }
}
