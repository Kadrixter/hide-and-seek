using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// New kill variants (knife impale, four more chainsaw kills, the chainsaw finisher that cuts off all limbs),
    /// the first-hit chainsaw slash (a leg falls off), one-legged hopping and the dust throw of a hider.
    /// Same conventions as <c>BlockyCharacterActions.Kills.cs</c>: seeker at the root facing +Z, victim ~0.85 m ahead.
    /// </summary>
    public partial class BlockyCharacterActions
    {
        // ================================================================== state

        /// <summary>The four limbs cut by the saw finisher in order (decided when the animation starts).</summary>
        readonly int[] sawSlots = { Limb.LegR, Limb.LegL, Limb.ArmL, Limb.ArmR };

        /// <summary>Limb removed by slot <paramref name="slot"/> (0..3) of the saw finisher.</summary>
        public int SawFinisherLimb(int slot) => sawSlots[Mathf.Clamp(slot, 0, 3)];

        void PrepareSawSlots(BlockyCharacter victim)
        {
            int m = victim != null && victim.IsBuilt ? victim.SeveredMask : 0;
            int a = (m & Limb.LegR) == 0 ? Limb.LegR : Limb.LegL;
            sawSlots[0] = a;
            sawSlots[1] = a == Limb.LegR ? Limb.LegL : Limb.LegR;
            sawSlots[2] = Limb.ArmL;
            sawSlots[3] = Limb.ArmR;
        }

        /// <summary>Hop slowly on one leg because the other one (0 = right, 1 = left) was cut off; -1 = both legs fine.</summary>
        public void SetHop(int lostLeg)
        {
            EnsureInit();
            if (animator != null) animator.hopLeg = lostLeg;
        }

        // ================================================================== victim body points

        /// <summary>Point along a limb of the victim (0 = joint, 1 = end), in this (seeker) root space.</summary>
        Vector3 VictimLimbPoint(int limb, float frac)
        {
            if (knifeVictim != null && knifeVictim.IsBuilt)
            {
                Transform j = limb == Limb.LegR ? knifeVictim.HipRight : limb == Limb.LegL ? knifeVictim.HipLeft
                    : limb == Limb.ArmR ? knifeVictim.ShoulderRight : knifeVictim.ShoulderLeft;
                float len = (limb & Limb.Legs) != 0 ? 0.62f : 0.5f;
                return transform.InverseTransformPoint(j.TransformPoint(new Vector3(0f, -len * frac, 0f)));
            }

            bool right = limb == Limb.LegR || limb == Limb.ArmR;
            float x = (right ? -1f : 1f) * ((limb & Limb.Legs) != 0 ? 0.11f : 0.34f);
            float y = (limb & Limb.Legs) != 0 ? 0.65f - 0.62f * frac : 1.35f - 0.5f * frac;
            return new Vector3(x, y, KnifeAttackDistance);
        }

        /// <summary>Saw tip (the cutting end of the bar) on <paramref name="tip"/>, bar pointing along <paramref name="dir"/>.</summary>
        static void SawTip(ref CharacterPose p, in CharacterPose current, float e, Vector3 tip, Vector3 dir, Vector3 jitter)
        {
            dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.forward;
            Vector3 fist = Vector3.Lerp(current.HandPos(0), tip - dir * 0.46f + jitter, e);
            SolveSawHands(ref p, fist, AimRot(fist, fist + dir));
        }

        static Vector3 SawJitter(float t, float amount) =>
            new Vector3(Mathf.Sin(t * 83f), Mathf.Sin(t * 67f + 1f), Mathf.Sin(t * 59f + 2f)) * 0.012f * amount;

        /// <summary>Saw raised overhead, bar pointing at the ceiling (taunt after the kill).</summary>
        static void SawRaised(ref CharacterPose p, in CharacterPose current, float w, float e)
        {
            Vector3 up = p.TorsoPoint(new Vector3(0.12f, 0.95f, 0.12f));
            var rp = p;
            SolveSawHands(ref rp, up, AimRot(up, up + new Vector3(0f, 1f, 0.25f)));
            CharacterPose.LerpArm(ref p, rp, 0, w);
            CharacterPose.LerpArm(ref p, rp, 1, w);
            p.PropR = Quaternion.Slerp(p.PropR, rp.PropR, w);
        }

        // ================================================================== knife: impale

        static readonly float[] ImpaleStab = { 0.8f };

        CharacterPose SeekerImpale(in CharacterPose current, float t)
        {
            float e = Seg(t, 0f, 0.25f);
            float cock = Seg(t, 0.3f, 0.65f);
            float stab = QuickStab(t, ImpaleStab, 1);
            float thrust = Mathf.Max(0f, stab);
            float twistW = Seg(t, 0.9f, 1.2f) * (1f - Seg(t, 1.45f, 1.6f));
            float rip = Seg(t, 1.15f, 1.4f) * (1f - Seg(t, 1.45f, 1.75f));
            float shove = Seg(t, 1.8f, 1.95f) * (1f - Seg(t, 2.1f, 2.35f));
            float release = Seg(t, 2.2f, 2.55f);

            var p = SeekerBody(current, e, 4f * cock + 10f * thrust + 6f * rip - 14f * shove - 6f * release, -10f * twistW,
                0.03f * thrust, 0.04f * e + 0.07f * thrust - 0.1f * shove);

            Vector3 chest = VictimTorso(ChestLocal);
            Vector3 ready = p.TorsoPoint(new Vector3(0.24f, 0.32f, 0.3f));
            Vector3 hip = p.TorsoPoint(new Vector3(0.34f, 0.1f, -0.1f));
            Vector3 dir = (chest - p.TorsoPoint(new Vector3(0.26f, 0.2f, 0f))).normalized;
            dir = (dir + Vector3.up * 0.35f).normalized;      // driven upwards under the ribs
            Vector3 hit = chest - dir * Blade;
            Vector3 backPos = Vector3.Lerp(hip, hit, 0.4f);
            Vector3 mid = Vector3.Lerp(ready, backPos, cock);

            Vector3 hand;
            if (t < 0.8f) hand = stab < 0f ? Vector3.Lerp(mid, hip, -stab) : Vector3.LerpUnclamped(mid, hit, stab);
            else
            {
                Vector3 inside = hit + new Vector3(0f, 0.18f * rip, 0f);
                hand = Vector3.Lerp(inside, ready, Seg(t, 1.5f, 1.85f));
            }
            Vector3 bladeDir = Vector3.Slerp(new Vector3(0f, -0.45f, 1f), dir, Seg(t, 0.55f, 0.75f) * (1f - Seg(t, 1.5f, 1.85f)));
            KnifeHand(ref p, current, e, hand, bladeDir, TorsoDir(p, 1f, -1f, -0.6f), 90f * twistW);

            float g = Seg(t, 0.05f, 0.3f) * (1f - 0.7f * release);
            Vector3 grab = VictimTorso(CollarFrontLocal) + new Vector3(0f, 0f, -0.04f - 0.2f * shove);
            FreeHand(ref p, current, g, grab, TorsoDir(p, -1f, -0.6f, -0.5f));
            return p;
        }

        static CharacterPose VictimImpale(float t)
        {
            float grab = Seg(t, 0f, 0.25f);
            float fl = Pulse(t, 0.8f, 1.5f) + Pulse(t, 1.4f, 1.0f);
            float clutch = Seg(t, 0.85f, 1.05f) * (1f - Seg(t, 1.75f, 1.95f));
            float curl = 0.1f * grab + 0.25f * Seg(t, 0.8f, 1.1f);
            float step = Seg(t, 1.85f, 2.4f);
            Vector3 r = Vector3.Lerp(HandsRestR, new Vector3(0.12f, 0.4f, 0.26f), clutch);
            Vector3 l = Vector3.Lerp(HandsRestL, new Vector3(-0.1f, 0.45f, 0.26f), clutch);
            return VictimStand(t, grab, curl, fl, -18f * Pulse(t, 0.8f, 1f) + 12f * Seg(t, 0.85f, 1.2f), step, 0f, 0.03f * Seg(t, 0.8f, 1.2f), r, l);
        }

        // ================================================================== saw: chop (shoulder to hip)

        CharacterPose SeekerSawChop(in CharacterPose current, float t)
        {
            float e = Seg(t, 0f, 0.3f);
            float cut = Seg(t, 0.85f, 1.75f);
            float cutting = Seg(t, 0.8f, 0.9f) * (1f - Seg(t, 1.7f, 1.8f));
            float rip = Seg(t, 1.8f, 2.3f);
            float raise = Seg(t, 2.4f, 2.9f) * (1f - Seg(t, 3.2f, 3.6f));
            var p = SeekerBody(current, e, 8f * Seg(t, 0.4f, 0.8f) + 12f * cutting - 10f * raise, -6f * cutting + 8f * Seg(t, 0.2f, 0.6f) * (1f - cutting),
                0.04f * cutting, 0.05f * e + 0.06f * cutting);

            Vector3 start = VictimTorso(CollarLeftLocal) + new Vector3(0f, 0.02f, 0f);
            Vector3 end = VictimTorso(new Vector3(0.16f, 0.04f, 0.12f));
            Vector3 raisedTip = p.TorsoPoint(new Vector3(0.28f, 1.4f, 0.12f));
            Vector3 from = p.TorsoPoint(new Vector3(0.1f, 0.25f, 0f));
            Vector3 tip = Vector3.Lerp(raisedTip, start, Seg(t, 0.4f, 0.85f));
            tip = Vector3.Lerp(tip, end, cut);
            tip = Vector3.Lerp(tip, p.TorsoPoint(new Vector3(0.45f, 0.7f, 0.25f)), rip * (1f - raise));
            Vector3 dir = (tip - from).normalized;
            if (t < 0.85f) dir = Vector3.Slerp(new Vector3(-0.2f, 0.9f, -0.5f).normalized, dir, Seg(t, 0.3f, 0.85f));
            SawTip(ref p, current, e, tip, dir, SawJitter(t, cutting > 0.5f ? 1f : 0.2f));
            SawRaised(ref p, current, raise, e);
            return p;
        }

        CharacterPose VictimSawChop(float t)
        {
            float hands = Seg(t, 0.05f, 0.35f);
            float cutP = Seg(t, 0.85f, 1.75f);
            float hit = Pulse(t, 0.85f, 1.6f);
            float shake = (t > 0.85f && t < 1.75f) ? Mathf.Sin(t * 61f) : 0f;
            Vector3 r = Vector3.Lerp(Vector3.Lerp(HandsRestR, new Vector3(0.12f, 0.62f, 0.32f), hands), new Vector3(-0.2f, 0.62f, 0.14f), Seg(t, 0.9f, 1.1f));
            Vector3 l = Vector3.Lerp(HandsRestL, new Vector3(-0.12f, 0.6f, 0.32f), hands);
            float ts = Mathf.Min(t, 1.8f);
            var stand = VictimStand(ts, 1f, 0.1f + 0.3f * cutP, hit + 0.4f * Mathf.Abs(shake), -20f * cutP + 6f * shake, 0f, 16f * cutP + 4f * shake, 0.05f * cutP, r, l);
            if (t < 1.8f) return stand;
            var kneel = KneelPose(0.8f, -0.2f);
            if (t < 2.15f) return CharacterPose.Lerp(stand, kneel, ActionTimeline.EaseIn(ActionTimeline.Remap01(t, 1.8f, 2.15f)));
            var side = SidePose();
            if (t < 2.55f) return CharacterPose.Lerp(kneel, side, ActionTimeline.EaseIn(ActionTimeline.Remap01(t, 2.15f, 2.55f)));
            return CharacterPose.Lerp(side, DeadPose(), ActionTimeline.EaseOut(ActionTimeline.Remap01(t, 2.55f, KillCore.VictimDuration(KillMove.SawChop))));
        }

        // ================================================================== saw: sweep (through the waist)

        CharacterPose SeekerSawSweep(in CharacterPose current, float t)
        {
            float e = Seg(t, 0f, 0.3f);
            float wind = Seg(t, 0.2f, 0.7f);
            float sw1 = Seg(t, 0.7f, 1.0f);
            float sw2 = Seg(t, 1.5f, 1.95f);
            float raise = Seg(t, 2.7f, 3.1f) * (1f - Seg(t, 3.1f, 3.4f));
            float sweeping = Mathf.Max(Seg(t, 0.65f, 0.75f) * (1f - Seg(t, 1.0f, 1.1f)), Seg(t, 1.45f, 1.55f) * (1f - Seg(t, 1.95f, 2.05f)));
            float twist = 42f * wind * (1f - sw1) - 40f * sw1 * (1f - Seg(t, 1.2f, 1.5f)) + 42f * sw2 * (1f - Seg(t, 2.05f, 2.4f)) - 20f * Seg(t, 1.2f, 1.5f) * (1f - sw2);
            var p = SeekerBody(current, e, 8f * sweeping, twist, 0.04f * sweeping, 0.05f * e + 0.05f * sweeping);

            Vector3 waist = VictimTorso(BellyLocal) + new Vector3(0f, 0.06f, -0.12f);
            Vector3 from = p.TorsoPoint(new Vector3(0.15f, 0.3f, 0.05f));
            Vector3 right = new Vector3(0.78f, 0f, -0.05f), left = new Vector3(-0.78f, 0f, -0.05f);
            Vector3 tip = waist + Vector3.Lerp(right, left, sw1);
            tip = Vector3.Lerp(tip, waist + left, Seg(t, 1.0f, 1.3f));
            if (t >= 1.5f) tip = waist + Vector3.Lerp(left, right, sw2);
            if (t >= 2.05f) tip = Vector3.Lerp(waist + right, p.TorsoPoint(new Vector3(0.4f, 0.65f, 0.3f)), Seg(t, 2.05f, 2.5f));
            if (t < 0.7f) tip = Vector3.Lerp(p.TorsoPoint(new Vector3(0.3f, 0.4f, 0.45f)), waist + right, wind);
            Vector3 dir = (tip - from).normalized;
            SawTip(ref p, current, e, tip, dir, SawJitter(t, sweeping > 0.5f ? 1f : 0.2f));
            SawRaised(ref p, current, raise, e);
            return p;
        }

        CharacterPose VictimSawSweep(float t)
        {
            float hands = Seg(t, 0.05f, 0.35f);
            float bend = Seg(t, 0.85f, 1.15f);
            float fl = Pulse(t, 0.85f, 1.5f) + Pulse(t, 1.8f, 1.2f);
            Vector3 r = Vector3.Lerp(Vector3.Lerp(HandsRestR, new Vector3(0.12f, 0.62f, 0.32f), hands), BellyHoldR, bend);
            Vector3 l = Vector3.Lerp(Vector3.Lerp(HandsRestL, new Vector3(-0.12f, 0.6f, 0.32f), hands), BellyHoldL, bend);
            var p = VictimStand(Mathf.Min(t, 1.95f), 1f, 0.1f * hands + 0.15f * bend, fl, -12f * bend, 0f, 0f, 0.07f * bend, r, l);
            p.PelvisPos += new Vector3(0f, -0.02f * bend, -0.1f * bend);
            p.Torso = Quaternion.Euler(58f * bend + 8f * Pulse(t, 1.8f), 0f, 0f) * p.Torso;
            p.Head = Quaternion.Euler(-28f * bend, 0f, 0f) * p.Head;
            p.SolveArm(0, p.TorsoPoint(r), TorsoDir(p, 1f, -1f, -0.5f));
            p.SolveArm(1, p.TorsoPoint(l), TorsoDir(p, -1f, -1f, -0.5f));
            p.SolveLeg(0, new Vector3(0.13f, 0f, 0.04f), new Vector3(0.1f, 0f, 1f));
            p.SolveLeg(1, new Vector3(-0.13f, 0f, -0.02f), new Vector3(-0.1f, 0f, 1f));
            if (t < 1.95f) return p;
            var fours = AllFoursPose();
            if (t < 2.35f) return CharacterPose.Lerp(p, fours, ActionTimeline.EaseIn(ActionTimeline.Remap01(t, 1.95f, 2.35f)));
            var side = SidePose();
            if (t < 2.75f) return CharacterPose.Lerp(fours, side, ActionTimeline.EaseIn(ActionTimeline.Remap01(t, 2.35f, 2.75f)));
            return CharacterPose.Lerp(side, DeadPose(), ActionTimeline.EaseOut(ActionTimeline.Remap01(t, 2.75f, KillCore.VictimDuration(KillMove.SawSweep))));
        }

        // ================================================================== saw: uppercut (groin to chest, lift, throw)

        CharacterPose SeekerSawUpper(in CharacterPose current, float t)
        {
            float e = Seg(t, 0f, 0.3f);
            float crouch = Seg(t, 0.2f, 0.55f) * (1f - Seg(t, 1.7f, 2.2f));
            float rise = Seg(t, 0.7f, 1.7f);
            float lift = Seg(t, 1.7f, 2.2f);
            float fling = Seg(t, 2.25f, 2.5f);
            float raise = Seg(t, 2.5f, 2.9f) * (1f - Seg(t, 3.1f, 3.6f));
            bool cutting = t > 0.65f && t < 1.75f;
            var p = SeekerBody(current, e, -6f * lift + 10f * crouch - 18f * fling * (1f - raise), 0f, 0.3f * crouch, 0.06f * e + 0.1f * crouch);

            Vector3 groin = VictimTorso(new Vector3(0f, -0.1f, 0.12f));
            Vector3 throat = VictimTorso(ThroatLocal) + new Vector3(0f, 0f, -0.05f);
            Vector3 tip = Vector3.Lerp(groin, throat, rise);
            tip = Vector3.Lerp(p.TorsoPoint(new Vector3(0.3f, 0.2f, 0.4f)), tip, Seg(t, 0.2f, 0.65f));
            Vector3 dir = Vector3.Slerp(new Vector3(0f, 0.2f, 1f), new Vector3(0f, 0.85f, 0.5f), Seg(t, 0.45f, 1.7f)).normalized;
            if (t > 2.25f) dir = Vector3.Slerp(dir, new Vector3(0.1f, 1f, -0.35f).normalized, fling);
            SawTip(ref p, current, e, tip, dir, SawJitter(t, cutting ? 1f : 0.2f));
            SawRaised(ref p, current, raise, e);
            return p;
        }

        CharacterPose VictimSawUpper(float t)
        {
            float hands = Seg(t, 0.05f, 0.35f);
            float rise = Seg(t, 0.7f, 1.7f);
            float lift = Seg(t, 1.7f, 2.1f);
            float hit = Pulse(t, 0.7f, 1.2f) + Pulse(t, 1.3f, 0.8f);
            float shake = (t > 0.7f && t < 2.0f) ? Mathf.Sin(t * 57f) : 0f;
            Vector3 r = Vector3.Lerp(HandsRestR, new Vector3(0.12f, 0.62f, 0.32f), hands);
            Vector3 l = Vector3.Lerp(HandsRestL, new Vector3(-0.12f, 0.6f, 0.32f), hands);
            r = Vector3.Lerp(r, new Vector3(0.1f, 0.3f, 0.3f), Seg(t, 0.7f, 0.9f));
            l = Vector3.Lerp(l, new Vector3(-0.1f, 0.32f, 0.3f), Seg(t, 0.7f, 0.9f));
            var p = VictimStand(Mathf.Min(t, 2.3f), 1f, 0.1f + 0.15f * rise, hit + 0.4f * Mathf.Abs(shake), -25f * rise + 5f * shake, 0f, 6f * shake, 0.04f * rise, r, l);
            // Lifted on the bar: pelvis up, leaning back, legs dangling.
            p.PelvisPos += new Vector3(0f, 0.3f * lift, -0.1f * lift);
            p.Torso = Quaternion.Euler(-28f * lift, 0f, 0f) * p.Torso;
            p.SolveLeg(0, new Vector3(0.13f, 0.3f * lift, 0.0f - 0.1f * lift), new Vector3(0.1f, 0f, 1f));
            p.SolveLeg(1, new Vector3(-0.13f, 0.3f * lift, -0.04f - 0.1f * lift), new Vector3(-0.1f, 0f, 1f));
            if (t < 2.3f) return p;
            // Flung backwards onto the back.
            var dead = DeadPose();
            return CharacterPose.Lerp(p, dead, ActionTimeline.EaseIn(ActionTimeline.Remap01(t, 2.3f, KillCore.VictimDuration(KillMove.SawUpper))));
        }

        // ================================================================== saw: neck (head flies off)

        CharacterPose SeekerSawNeck(in CharacterPose current, float t)
        {
            float e = Seg(t, 0f, 0.3f);
            float wind = Seg(t, 0.3f, 0.6f);
            float sweep = Seg(t, 0.62f, 1.05f);
            float after = Seg(t, 1.1f, 1.5f);
            float raise = Seg(t, 1.5f, 2.1f) * (1f - Seg(t, 2.8f, 3.3f));
            bool cutting = t > 0.6f && t < 1.1f;
            var p = SeekerBody(current, e, 6f * wind + 6f * Seg(t, 0.6f, 0.9f) - 12f * raise, 18f * wind * (1f - sweep) - 22f * sweep * (1f - after), 0.02f * wind, 0.06f * e + 0.04f * wind);

            Vector3 throat = VictimTorso(ThroatLocal) + new Vector3(0f, 0.03f, -0.05f);
            Vector3 from = p.TorsoPoint(new Vector3(0.15f, 0.3f, 0.05f));
            Vector3 tip = throat + Vector3.Lerp(new Vector3(0.55f, 0f, -0.05f), new Vector3(-0.4f, 0f, 0.02f), sweep);
            tip = Vector3.Lerp(p.TorsoPoint(new Vector3(0.3f, 0.45f, 0.4f)), tip, wind);
            tip = Vector3.Lerp(tip, p.TorsoPoint(new Vector3(0.4f, 0.8f, 0.3f)), after * (1f - raise));
            Vector3 dir = (tip - from).normalized;
            SawTip(ref p, current, e, tip, dir, SawJitter(t, cutting ? 1f : 0.2f));
            SawRaised(ref p, current, raise, e);

            // Left hand holds the collar, then lets go.
            float g = Seg(t, 0.05f, 0.3f) * (1f - Seg(t, 1.1f, 1.35f));
            Vector3 grab = VictimTorso(CollarFrontLocal) + new Vector3(0f, 0.02f, -0.05f);
            if (g > 0.001f)
            {
                var lp = p;
                lp.SolveArm(1, grab, TorsoDir(p, -1f, -0.6f, -0.5f));
                CharacterPose.LerpArm(ref p, lp, 1, g);
            }
            return p;
        }

        CharacterPose VictimSawNeck(float t)
        {
            float hands = Seg(t, 0.05f, 0.35f);
            float cutT = Seg(t, 0.85f, 1.0f);
            float jerk = Pulse(t, 0.85f, 1.5f);
            float flail = t > 0.9f ? Mathf.Sin(t * 19f) : 0f;
            Vector3 r = Vector3.Lerp(Vector3.Lerp(HandsRestR, new Vector3(0.12f, 0.62f, 0.32f), hands), new Vector3(0.12f, 0.78f + 0.05f * flail, 0.2f), cutT);
            Vector3 l = Vector3.Lerp(Vector3.Lerp(HandsRestL, new Vector3(-0.12f, 0.6f, 0.32f), hands), new Vector3(-0.1f, 0.78f - 0.05f * flail, 0.2f), cutT);
            float ts = Mathf.Min(t, 1.6f);
            var stand = VictimStand(ts, 1f, 0.1f + 0.1f * cutT, jerk, -15f * Seg(t, 0.4f, 0.8f), 0f, 4f * flail * cutT, 0f, r, l);
            if (t < 1.6f) return stand;
            var kneel = KneelPose(0.2f, -0.1f);
            if (t < 2.0f) return CharacterPose.Lerp(stand, kneel, ActionTimeline.EaseIn(ActionTimeline.Remap01(t, 1.6f, 2.0f)));
            var side = SidePose();
            if (t < 2.5f) return CharacterPose.Lerp(kneel, side, ActionTimeline.EaseIn(ActionTimeline.Remap01(t, 2.0f, 2.5f)));
            return CharacterPose.Lerp(side, DeadPose(), ActionTimeline.EaseOut(ActionTimeline.Remap01(t, 2.5f, KillCore.VictimDuration(KillMove.SawNeck))));
        }

        // ================================================================== saw finisher (all limbs)

        CharacterPose SeekerSawFinisher(in CharacterPose current, float t)
        {
            var hits = KillCore.HitTimes(KillMove.SawFinisher);
            float e = Seg(t, 0f, 0.3f);
            float approach = Seg(t, 0.25f, 0.75f);
            float raise = Seg(t, KillCore.SawFinisherTauntTime, KillCore.SawFinisherTauntTime + 0.6f) * (1f - Seg(t, 7.0f, 7.5f));

            Vector3 hold = new Vector3(0.2f, 0.38f, 0.5f);
            Vector3 tip = VictimLimbPoint(sawSlots[0], 0.45f);
            float cutting = 0f;
            Vector3 axis = Vector3.right;
            for (int s = 0; s < 4; s++)
            {
                float h = hits[s];
                if (s > 0) tip = Vector3.Lerp(tip, VictimLimbPoint(sawSlots[s], s < 2 ? 0.45f : 0.5f), Seg(t, h - 0.65f, h - 0.2f));
                float c = Seg(t, h - 0.12f, h) * (1f - Seg(t, h + 0.3f, h + 0.5f));
                if (c > cutting) { cutting = c; axis = s < 2 ? Vector3.right : Vector3.up; }
            }
            tip += axis * 0.045f * Mathf.Sin(t * 55f) * cutting;

            // Body goes down to the level of the limb being cut.
            var hold0 = CharacterPose.Identity;
            float low = Mathf.Clamp01((0.95f - tip.y) / 0.7f) * approach * (1f - raise);
            var p = SeekerBody(current, e, 8f + 24f * low - 10f * raise, 8f * Mathf.Sin(t * 0.9f) * (1f - raise), 0.32f * low, 0.05f * e + 0.08f * low);
            Vector3 tipWorldHold = p.TorsoPoint(hold);
            Vector3 aimTip = Vector3.Lerp(tipWorldHold, tip, approach);
            aimTip = Vector3.Lerp(aimTip, p.TorsoPoint(new Vector3(0.4f, 0.8f, 0.3f)), Seg(t, 4.8f, 5.3f) * (1f - raise));
            Vector3 from = p.TorsoPoint(new Vector3(0.12f, 0.25f, 0f));
            Vector3 dir = (aimTip - from).normalized;
            SawTip(ref p, current, e, aimTip, dir, SawJitter(t, cutting > 0.5f ? 1f : 0.25f));
            SawRaised(ref p, current, raise, e);
            return p;
        }

        CharacterPose VictimSawFinisher(float t)
        {
            float hands = Seg(t, 0.05f, 0.4f);
            float shake = t < 1.0f ? Mathf.Sin(t * 33f) * Seg(t, 0.4f, 0.9f) : 0f;
            Vector3 r = Vector3.Lerp(HandsRestR, new Vector3(0.12f, 0.64f, 0.32f), hands);
            Vector3 l = Vector3.Lerp(HandsRestL, new Vector3(-0.12f, 0.62f, 0.32f), hands);
            var stand = VictimStand(Mathf.Min(t, 1.0f), 1f, 0.15f * hands, 0.3f * Mathf.Abs(shake), -10f * hands, 0f, 4f * shake, 0.02f * hands, r, l);
            if (t < 1.0f) return stand;

            // Right after the first leg goes: collapses onto the knees (one leg is already gone), drags itself on the hands.
            var kneel = KneelPose(0.7f, -0.2f);
            var fours = AllFoursPose();
            var side = SidePose();
            CharacterPose pose;
            if (t < 1.5f) pose = CharacterPose.Lerp(stand, kneel, ActionTimeline.EaseIn(ActionTimeline.Remap01(t, 1.0f, 1.5f)));
            else if (t < 2.1f) pose = CharacterPose.Lerp(kneel, fours, ActionTimeline.EaseIn(ActionTimeline.Remap01(t, 1.5f, 1.9f)));
            else if (t < 3.0f) pose = CharacterPose.Lerp(fours, side, ActionTimeline.EaseIn(ActionTimeline.Remap01(t, 2.1f, 2.7f)));
            else if (t < 4.8f) pose = side;
            else return CharacterPose.Lerp(side, DeadPose(), ActionTimeline.EaseOut(ActionTimeline.Remap01(t, 4.8f, KillCore.SawFinisherVictimDead)));

            if (t >= 1.0f)
            {
                // Convulsions on every cut.
                float conv = Pulse(t, 1.0f, 1.2f) + Pulse(t, 2.1f, 1.2f) + Pulse(t, 3.2f, 1.2f) + Pulse(t, 4.3f, 1.2f);
                float writhe = Seg(t, 2.7f, 3.0f) * (1f - Seg(t, 4.5f, 4.8f)) * Mathf.Sin(t * 14f);
                pose.Torso = pose.Torso * Quaternion.Euler(10f * conv + 4f * writhe, 6f * writhe, 0f);
                pose.Head = pose.Head * Quaternion.Euler(-16f * conv + 6f * writhe, 0f, 0f);
            }
            return pose;
        }

        // ================================================================== chainsaw first hit: a leg falls off

        float sawSlashT = 99f;
        BlockyCharacter sawSlashVictim;
        int sawSlashLimb = Limb.LegR;

        public const float SawSlashDuration = 0.95f;
        /// <summary>Time from <see cref="PlaySawSlash"/> at which the blade goes through the leg.</summary>
        public const float SawSlashHitTime = 0.38f;

        /// <summary>One low sweep of the running chainsaw through the victim's leg.</summary>
        public void PlaySawSlash(BlockyCharacter victim, int limb)
        {
            EnsureInit();
            sawSlashT = 0f;
            sawSlashVictim = victim;
            sawSlashLimb = limb;
        }

        // ================================================================== hider: throwing dust

        float dustT = 99f;
        public const float DustDuration = 1.05f;
        /// <summary>Time from <see cref="PlayDust"/> at which the dust leaves the hand.</summary>
        public const float DustReleaseTime = 0.66f;

        /// <summary>Hider: crouch, grab a handful of dust from the floor and fling it forward.</summary>
        public void PlayDust()
        {
            EnsureInit();
            dustT = 0f;
        }

        /// <summary>World position of the right hand (for the dust particles).</summary>
        public Vector3 DustHandWorld => character != null && character.IsBuilt ? character.ElbowRight.TransformPoint(CharacterPose.HandLocal) : transform.position + Vector3.up;

        CharacterPose DustPose(in CharacterPose current, float t)
        {
            float down = Seg(t, 0f, 0.28f) * (1f - Seg(t, 0.6f, 0.86f));
            float reach = Seg(t, 0f, 0.3f);
            float scoop = Seg(t, 0.3f, 0.5f);
            float fling = Seg(t, 0.5f, 0.68f);
            float rec = Seg(t, 0.78f, 1.02f);

            var p = CharacterPose.Identity;
            p.PropR = current.PropR;
            p.PelvisPos = new Vector3(0f, 0.64f - 0.27f * down, -0.1f * down + 0.05f * fling);
            p.Pelvis = Quaternion.Euler(4f * down, -8f * fling, 0f);
            p.Torso = Quaternion.Euler(36f * down - 14f * fling * (1f - rec), 10f * (1f - fling) * down - 16f * fling * (1f - rec), 0f);
            p.Head = Quaternion.Euler(-30f * down, 0f, 0f);
            p.SolveLeg(0, new Vector3(0.14f, 0f, 0.04f), new Vector3(0.1f, 0f, 1f));
            p.SolveLeg(1, new Vector3(-0.14f, 0f, -0.1f), new Vector3(-0.1f, 0f, 1f));

            Vector3 rest = p.TorsoPoint(new Vector3(0.18f, 0.4f, 0.15f));
            Vector3 ground = new Vector3(0.3f, 0.05f, 0.42f);
            Vector3 back = new Vector3(0.34f, 0.22f, 0.12f);
            Vector3 fore = new Vector3(0.2f, 0.82f, 0.78f);
            Vector3 hand = Vector3.Lerp(rest, ground, reach);
            hand = Vector3.Lerp(hand, back, scoop);
            hand = Vector3.Lerp(hand, fore, fling);
            hand = Vector3.Lerp(hand, rest, rec);
            p.SolveArm(0, hand, TorsoDir(p, 1f, -0.4f, -0.7f));
            Vector3 lhand = Vector3.Lerp(p.TorsoPoint(new Vector3(-0.16f, 0.4f, 0.12f)), new Vector3(-0.42f, 0.34f + 0.1f * fling, 0.12f), down + 0.5f * fling);
            p.SolveArm(1, lhand, TorsoDir(p, -1f, -0.6f, -0.5f));
            return p;
        }

        // ================================================================== layer hook (called from ApplyLayer)

        void ApplyGoreLayers(ref CharacterPose p, float dt, bool standing)
        {
            // Chainsaw slash: both hands on the saw, a low sweep through the leg.
            if (sawSlashT < SawSlashDuration)
            {
                sawSlashT += dt;
                float t = sawSlashT;
                float w = Seg(t, 0f, 0.14f) * (1f - Seg(t, 0.66f, SawSlashDuration));
                float wind = Seg(t, 0f, 0.2f);
                float thrust = Seg(t, 0.2f, SawSlashHitTime);
                float back = Seg(t, 0.52f, 0.85f);
                Vector3 legPt = VictimLimbPoint(sawSlashLimb, 0.5f);
                if (sawSlashVictim == null) legPt = new Vector3((sawSlashLimb & Limb.LegR) != 0 ? -0.11f : 0.11f, 0.3f, KnifeAttackDistance - 0.1f);
                legPt.y = Mathf.Clamp(legPt.y, 0.2f, 0.7f);
                Vector3 cocked = new Vector3(0.45f, 0.35f, 0.25f);
                var sp = p;
                sp.Torso = p.Torso * Quaternion.Euler(18f * w, -12f * thrust * (1f - back), 0f);
                sp.PelvisPos = p.PelvisPos + new Vector3(0f, -0.08f * w, 0.05f * thrust);
                Vector3 tip = Vector3.Lerp(Vector3.Lerp(p.TorsoPoint(cocked), legPt, thrust), p.TorsoPoint(new Vector3(0.1f, 0.35f, 0.5f)), back);
                Vector3 from = sp.TorsoPoint(new Vector3(0.15f, 0.25f, 0.05f));
                Vector3 dir = (tip - from).normalized;
                Vector3 fist = tip - dir * 0.46f + SawJitter(t, 1f);
                SolveSawHands(ref sp, fist, AimRot(fist, fist + dir));
                p.Torso = Quaternion.Slerp(p.Torso, sp.Torso, w);
                p.PelvisPos = Vector3.Lerp(p.PelvisPos, sp.PelvisPos, w);
                CharacterPose.LerpArm(ref p, sp, 0, w);
                CharacterPose.LerpArm(ref p, sp, 1, w);
                p.PropR = Quaternion.Slerp(p.PropR, sp.PropR, w);
            }

            // Dust throw (whole body: crouch, scoop, fling).
            if (dustT < DustDuration)
            {
                dustT += dt;
                float w = Seg(dustT, 0f, 0.1f) * (1f - Seg(dustT, DustDuration - 0.18f, DustDuration));
                var dp = DustPose(p, dustT);
                var keepProp = p.PropR;
                p = CharacterPose.Lerp(p, dp, w);
                p.PropR = keepProp;
            }
        }
    }
}
