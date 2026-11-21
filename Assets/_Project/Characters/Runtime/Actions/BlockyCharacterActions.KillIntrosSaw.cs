using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>Chainsaw and Glock kill intros (see <see cref="KillIntro"/>). Same layout as the knife intros.</summary>
    public partial class BlockyCharacterActions
    {
        /// <summary>Lift of one foot while shuffling: <paramref name="steps"/> alternating steps between a and b.</summary>
        static float Shuffle(float t, int leg, float a, float b, int steps, float height = 0.06f)
        {
            if (t <= a || t >= b) return 0f;
            float x = (t - a) / (b - a) * steps;
            int i = Mathf.FloorToInt(x);
            return (i % 2) == leg ? Mathf.Sin(Mathf.PI * (x - i)) * height : 0f;
        }

        /// <summary>Both hands on the running saw: fist at <paramref name="fist"/>, bar pointing at <paramref name="aim"/>.</summary>
        static void SawHold(ref CharacterPose p, in CharacterPose current, float e, Vector3 fist, Vector3 aim, Vector3 jitter)
        {
            fist = Vector3.Lerp(current.HandPos(0), fist, e) + jitter;
            SolveSawHands(ref p, fist, AimRot(fist, aim));
        }

        // ================================================================== saw: the victim grabs the arms, front kick

        CharacterPose SeekerSawGrab(in CharacterPose current, float t)
        {
            float e = Seg(t, 0f, 0.3f);
            float advance = Seg(t, 0.1f, 0.8f);
            float held = Seg(t, 0.85f, 0.98f) * (1f - Seg(t, 1.5f, 1.66f));
            float chamber = Seg(t, 1.5f, 1.66f) * (1f - Seg(t, 1.86f, 2.1f));
            float kick = Seg(t, 1.66f, 1.78f) * (1f - Seg(t, 1.84f, 2.04f));
            float reset = Seg(t, 2.0f, 2.7f);
            float lean = 5f * advance * (1f - chamber) + 7f * held - 12f * chamber - 6f * kick;
            var p = SeekerBody(current, e, lean, 6f * Mathf.Sin(t * 5f) * held, 0.03f * held, 0.04f * e + 0.06f * advance * (1f - reset * 0.7f));
            SeekerFeet(ref p, e, new Vector3(0f, Shuffle(t, 0, 0.15f, 0.75f, 2), 0.08f * advance * (1f - reset)),
                new Vector3(0f, Shuffle(t, 1, 0.15f, 0.75f, 2), 0.16f * advance * (1f - reset * 0.7f)));

            // Right leg: chamber the knee, drive the sole into the victim's belly.
            float k = Mathf.Max(chamber, kick);
            if (k > 0.001f)
            {
                Vector3 hip = p.HipPos(0);
                Vector3 chambered = hip + new Vector3(0.02f, -0.32f, 0.28f);
                Vector3 contact = VictimTorso(BellyLocal) + new Vector3(0f, -0.06f, -0.08f);
                Vector3 sole = Vector3.Lerp(Vector3.Lerp(new Vector3(0.15f, 0f, -0.15f * e), chambered, chamber), contact, kick);
                p.SolveLeg(0, sole, new Vector3(0f, 0.7f, 1f));
            }

            Vector3 chest = VictimTorso(ChestLocal);
            Vector3 holdFist = p.TorsoPoint(new Vector3(0.17f, 0.22f, 0.24f));
            Vector3 upFist = p.TorsoPoint(new Vector3(0.18f, 0.4f, 0.32f));
            Vector3 pushedFist = p.TorsoPoint(new Vector3(0.26f, 0.55f, 0.28f)) + Tremble(t, 0.015f) * held;
            Vector3 hipFist = p.TorsoPoint(new Vector3(0.2f, 0.18f, 0.14f));
            Vector3 fist = Vector3.Lerp(Vector3.Lerp(holdFist, upFist, advance), pushedFist, held);
            fist = Vector3.Lerp(fist, hipFist, Mathf.Max(chamber, kick) * (1f - reset));
            fist = Vector3.Lerp(fist, holdFist, reset);
            Vector3 aim = Vector3.Lerp(chest, fist + new Vector3(-0.35f, 0.75f, 0.6f), held);
            aim = Vector3.Lerp(aim, fist + new Vector3(0f, 0.1f, 1f), Mathf.Max(chamber, reset));
            SawHold(ref p, current, e, fist, aim, SawJitter(t, held > 0.5f ? 0.6f : 0.25f));
            return p;
        }

        CharacterPose VictimSawGrab(float t)
        {
            float backOff = Seg(t, 0.05f, 0.7f);
            float lunge = Seg(t, 0.78f, 0.95f) * (1f - Seg(t, 1.74f, 1.82f));
            float doubled = Seg(t, 1.76f, 1.9f) * (1f - Seg(t, 2.1f, 2.6f));
            float recover = Seg(t, 2.25f, 2.8f);
            float plead = backOff * (1f - lunge);
            float struggle = Seg(t, 0.95f, 1.05f) * (1f - Seg(t, 1.72f, 1.8f));

            float z = Mathf.Lerp(-0.26f * backOff, -0.06f, lunge);
            z = Mathf.Lerp(z, -0.3f, Seg(t, 1.78f, 2.0f) * (1f - recover));
            z = Mathf.Lerp(z, 0f, recover);
            var p = CharacterPose.Identity;
            p.PelvisPos = new Vector3(0.01f * Mathf.Sin(t * 37f) * struggle, 0.655f - 0.05f * struggle - 0.08f * doubled, z);
            p.Pelvis = Quaternion.Euler(-6f * doubled, 0f, 0f);
            p.Torso = Quaternion.Euler(-6f * plead + 5f * lunge + 46f * doubled + 3f * Mathf.Sin(t * 23f) * struggle, 6f * Mathf.Sin(t * 5f) * struggle, 0f);
            p.Head = Quaternion.Euler(-14f * lunge - 20f * doubled, 10f * Mathf.Sin(t * 9f) * plead, 0f);
            Vector3 rf = new Vector3(0.13f, Shuffle(t, 0, 0.05f, 0.7f, 2) + Shuffle(t, 0, 1.8f, 2.8f, 3), z + 0.02f - 0.26f * struggle);
            Vector3 lf = new Vector3(-0.13f, Shuffle(t, 1, 0.05f, 0.7f, 2) + Shuffle(t, 1, 1.8f, 2.8f, 3), z - 0.02f + 0.06f * struggle);
            p.SolveLeg(0, rf, new Vector3(0.1f, 0f, 1f));
            p.SolveLeg(1, lf, new Vector3(-0.1f, 0f, 1f));

            // Hands: up (pleading), on the seeker's forearms, to the belly after the kick, defensive.
            Vector3 upR = p.TorsoPoint(new Vector3(0.22f, 0.74f, 0.3f)), upL = p.TorsoPoint(new Vector3(-0.22f, 0.74f, 0.3f));
            Vector3 defR = p.TorsoPoint(new Vector3(0.14f, 0.62f, 0.32f)), defL = p.TorsoPoint(new Vector3(-0.14f, 0.6f, 0.32f));
            Vector3 r = Vector3.Lerp(p.TorsoPoint(HandsRestR), upR, Seg(t, 0.05f, 0.4f));
            Vector3 l = Vector3.Lerp(p.TorsoPoint(HandsRestL), upL, Seg(t, 0.05f, 0.4f));
            r = Vector3.Lerp(r, KillerForearm(1, 0.85f) + Tremble(t, 0.008f), lunge);
            l = Vector3.Lerp(l, KillerForearm(0, 0.55f) - Tremble(t, 0.008f), lunge);
            r = Vector3.Lerp(r, p.TorsoPoint(BellyHoldR), doubled);
            l = Vector3.Lerp(l, p.TorsoPoint(BellyHoldL), doubled);
            r = Vector3.Lerp(r, defR, recover);
            l = Vector3.Lerp(l, defL, recover);
            p.SolveArm(0, r, TorsoDir(p, 1f, -0.8f, -0.5f));
            p.SolveArm(1, l, TorsoDir(p, -1f, -0.8f, -0.5f));
            return p;
        }

        // ================================================================== saw: runs, kicked behind the knee, dragged up

        CharacterPose SeekerSawTrip(in CharacterPose current, float t)
        {
            float e = Seg(t, 0f, 0.3f);
            float chase = Seg(t, 0.2f, 0.85f);
            float kick = Seg(t, 0.82f, 0.98f) * (1f - Seg(t, 1.02f, 1.25f));
            float reach = Seg(t, 1.55f, 1.85f) * (1f - Seg(t, 2.45f, 2.75f));
            float pull = Seg(t, 1.88f, 2.5f);
            float fwd = 0.36f * chase * (1f - pull) + 0.04f;
            var p = SeekerBody(current, e, 6f * chase + 10f * reach - 8f * pull * (1f - Seg(t, 2.5f, 2.8f)), -8f * reach, 0.04f * reach, fwd * e);
            SeekerFeet(ref p, e, new Vector3(0f, Shuffle(t, 0, 0.2f, 0.8f, 2) + Shuffle(t, 0, 1.9f, 2.5f, 2), 0.2f * chase * (1f - pull)),
                new Vector3(0f, Shuffle(t, 1, 0.2f, 0.8f, 2) + Shuffle(t, 1, 1.9f, 2.5f, 2), 0.36f * chase * (1f - pull)));
            if (kick > 0.001f)
            {
                // Low kick into the back of the victim's right knee.
                Vector3 knee = VictimLimbPoint(Limb.LegR, 0.5f) + new Vector3(0f, -0.02f, -0.06f);
                Vector3 sole = Vector3.Lerp(new Vector3(0.15f, 0f, -0.15f + 0.2f * chase), knee, kick);
                p.SolveLeg(0, sole, new Vector3(0.2f, 0.4f, 1f));
            }

            Vector3 fist = p.TorsoPoint(new Vector3(0.17f, 0.22f + 0.06f * chase, 0.24f));
            Vector3 aim = fist + p.TorsoRoot * new Vector3(-0.05f, 0.12f - 0.25f * reach, 1f);
            SawHold(ref p, current, e, fist, aim, SawJitter(t, 0.3f));
            // Left hand leaves the saw, grabs the back of the collar and drags the victim up, then back on the saw.
            if (reach > 0.001f)
            {
                var lp = p;
                Vector3 back = VictimTorso(BackCollarLocal) + new Vector3(0f, 0.03f, 0f);
                Vector3 front = VictimTorso(CollarFrontLocal) + new Vector3(0f, 0.02f, -0.05f);
                lp.SolveArm(1, Vector3.Lerp(back, front, Seg(t, 2.1f, 2.45f)), TorsoDir(p, -1f, -0.6f, -0.5f));
                CharacterPose.LerpArm(ref p, lp, 1, reach);
            }
            return p;
        }

        CharacterPose VictimSawTrip(float t)
        {
            float turn = Seg(t, 0.06f, 0.4f);
            float run = Seg(t, 0.25f, 0.92f);
            float drop = Seg(t, 0.98f, 1.15f);
            float push = Mathf.Max(0f, Mathf.Sin((t - 1.2f) * 4.2f)) * Seg(t, 1.2f, 1.35f) * (1f - Seg(t, 1.8f, 1.9f));
            float lift = Seg(t, 1.86f, 2.05f);
            float spin = Seg(t, 2.0f, 2.5f);
            float drag = Seg(t, 1.95f, 2.55f);

            Vector3 off = new Vector3(0.06f * turn * (1f - spin), 0f, -0.45f * run * (1f - drag));
            var stride = StridePose(ActionTimeline.Remap01(t, 0.2f, 0.98f) * 1.1f, 20f, 0f);
            var neutral = VictimStand(0f, 0f, 0f, Pulse(t, 0.04f, 1f), 0f, 0f, 0f, 0f, HandsRestR, HandsRestL);
            var running = Turned(CharacterPose.Lerp(neutral, stride, Seg(t, 0.04f, 0.25f)), 155f * turn, off);

            var knee = OneKneePose(push);
            knee.Head = Quaternion.Euler(-10f, -55f * Seg(t, 1.2f, 1.5f), 0f);
            var kneeling = Turned(knee, 155f, off);
            var p = CharacterPose.Lerp(running, kneeling, ActionTimeline.EaseIn(drop));

            // Yanked up by the collar: arched, stumbling, spun round to face the seeker.
            var arched = StridePose(1.3f + 0.6f * ActionTimeline.Remap01(t, 1.9f, 2.5f), -22f, 1f);
            var archedT = Turned(arched, 155f, off);
            p = CharacterPose.Lerp(p, archedT, lift * (1f - spin));
            var facing = VictimStand(t, 0f, 0.05f, Pulse(t, 2.5f, 0.6f), -8f, 0f, 0f, 0.03f,
                new Vector3(0.14f, 0.62f, 0.32f), new Vector3(-0.14f, 0.6f, 0.32f));
            ArmTo(ref facing, 0, KillerForearm(1, 0.9f), Seg(t, 2.1f, 2.3f) * (1f - Seg(t, 2.5f, 2.75f)));
            return CharacterPose.Lerp(p, facing, spin);
        }

        // ================================================================== glock: runs, warning shot, hands up, turns back

        CharacterPose SeekerGlockStandoff(in CharacterPose current, float t)
        {
            float raise = Seg(t, 0f, 0.4f);
            float floorAim = Seg(t, 0.75f, 0.92f) * (1f - Seg(t, 1.15f, 1.5f));
            float r = 0f;
            float x = t - 0.95f;
            if (x >= 0f && x < 0.5f) r = Mathf.Exp(-x * 13f);

            var p = current;
            p.Torso = Quaternion.Euler(6f * raise - 6f * r + 4f * floorAim, 0f, 0f) * p.Torso;
            p.PelvisPos += new Vector3(0f, -0.03f * raise, -0.02f * r);
            Vector3 chest = VictimTorso(new Vector3(0.05f, 0.5f, 0.1f));
            Vector3 floor = transform.InverseTransformPoint(knifeVictim != null ? knifeVictim.transform.position : transform.TransformPoint(0f, 0f, 2.3f))
                            + new Vector3(0.25f, 0.02f, -0.35f);
            Vector3 aim = Vector3.Lerp(chest, floor, floorAim);
            Vector3 gun = p.TorsoPoint(new Vector3(0.04f, 0.55f, 0.46f));
            Vector3 d = aim - gun;
            Quaternion aimRot = Quaternion.LookRotation(d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.forward, Vector3.up);
            gun -= aimRot * new Vector3(0f, -0.02f * r, 0.07f * r);
            aimRot *= Quaternion.Euler(-18f * r, 0f, 0f);
            Vector3 low = p.TorsoPoint(new Vector3(0.12f, 0.18f, 0.3f));
            Quaternion lowRot = p.TorsoRoot * Quaternion.Euler(40f, -6f, 0f);
            SolveGlockHands(ref p, Vector3.Lerp(low, gun, raise), Quaternion.Slerp(lowRot, aimRot, raise), ActionTimeline.SupportGripLocal, 0f, Vector3.zero);
            Vector3 look = aim - p.TorsoPoint(new Vector3(0f, 0.75f, 0f));
            float pitch = -Mathf.Atan2(look.y, new Vector2(look.x, look.z).magnitude) * Mathf.Rad2Deg;
            p.Head = Quaternion.Euler(Mathf.Clamp(pitch, -40f, 40f) * raise, 0f, 0f);
            return p;
        }

        CharacterPose VictimGlockStandoff(float t)
        {
            float turn = Seg(t, 0.08f, 0.42f) * (1f - Seg(t, 1.15f, 1.95f));
            float run = Seg(t, 0.25f, 0.92f);
            float freeze = Seg(t, 0.95f, 1.05f);
            float back = Seg(t, 1.2f, 2.0f);
            float jump = Pulse(t, 0.95f, 1.4f);
            Vector3 off = new Vector3(0.05f * turn, 0.03f * jump, -0.4f * run * (1f - back));

            var stride = StridePose(ActionTimeline.Remap01(t, 0.2f, 0.95f) * 1.05f, 18f, 0f);
            var neutral = VictimStand(0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, HandsRestR, HandsRestL);
            var running = CharacterPose.Lerp(neutral, stride, Seg(t, 0.06f, 0.25f));

            // Hands thrown up, cowering, shuffling back while turning round to face the gun.
            float shake = Mathf.Sin(t * 23f) * 0.012f;
            var up = VictimStand(t, 0f, 0.08f, jump, -6f + 10f * jump, 0f, 0f, 0.04f * freeze * (1f - back * 0.5f),
                new Vector3(0.24f + shake, 0.8f, 0.12f), new Vector3(-0.26f - shake, 0.8f, 0.12f));
            up.SolveLeg(0, new Vector3(0.13f, Shuffle(t, 0, 1.2f, 2.0f, 4), 0.02f), new Vector3(0.1f, 0f, 1f));
            up.SolveLeg(1, new Vector3(-0.13f, Shuffle(t, 1, 1.2f, 2.0f, 4), -0.02f), new Vector3(-0.1f, 0f, 1f));
            var body = CharacterPose.Lerp(running, up, freeze);
            return Turned(body, 155f * turn, off);
        }
    }
}
