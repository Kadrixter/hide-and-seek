using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Characters
{
    public partial class BlockyCharacterActions
    {
        /// <summary>Pelvis of the lying body (the head ends up near the root, the legs behind it).</summary>
        const float OdLieZ = -0.7f;

        float overdoseT = -1f, overdoseSeed;

        /// <summary>
        /// Collapse after too many cans (<see cref="OverdoseTimeline"/>): <paramref name="t"/> = seconds since it started
        /// (server time, so every client and late joiners show the same moment), &lt; 0 = off. <paramref name="seed"/>
        /// = the player's sway seed (the seizure rhythm is a pure function of time and seed). Overrides every other layer.
        /// </summary>
        public void SetOverdose(float t, float seed)
        {
            EnsureInit();
            bool on = t >= 0f;
            if (on != overdoseT >= 0f && hasLast)
            {
                fadeFrom = lastOutput;
                fadeT = 0f;
            }
            overdoseT = on ? t : -1f;
            overdoseSeed = seed;
        }

        public bool OverdoseActive => overdoseT >= 0f;

        float OdNoise(float f, float k) => Drunkenness.Fbm(time * f, overdoseSeed + k);

        CharacterPose OverdosePose(in CharacterPose current, float t)
        {
            float S(float a, float b) => ActionTimeline.SmoothStep01(ActionTimeline.Remap01(t, a, b));
            float In(float a, float b) => ActionTimeline.EaseIn(ActionTimeline.Remap01(t, a, b));

            if (t >= OverdoseTimeline.SeizeStart && t < OverdoseTimeline.GetUpStart) return SeizePose(t - OverdoseTimeline.SeizeStart);

            CharacterPose p;
            if (t < OverdoseTimeline.FallEnd)
            {
                // ---- staggers, clutches the chest, the knees give, topples onto the side and rolls onto the back
                if (t < OverdoseTimeline.Lurch) p = CharacterPose.Lerp(current, OdLurchPose(), S(0f, OverdoseTimeline.Lurch));
                else if (t < OverdoseTimeline.StepBack) p = CharacterPose.Lerp(OdLurchPose(), OdStepBackPose(), S(OverdoseTimeline.Lurch, OverdoseTimeline.StepBack));
                else if (t < OverdoseTimeline.Clutch) p = CharacterPose.Lerp(OdStepBackPose(), OdClutchPose(), S(OverdoseTimeline.StepBack, OverdoseTimeline.Clutch));
                else if (t < OverdoseTimeline.Knees) p = CharacterPose.Lerp(OdClutchPose(), OdKneelPose(), In(OverdoseTimeline.Clutch, OverdoseTimeline.Knees));
                else if (t < OverdoseTimeline.Topple) p = CharacterPose.Lerp(OdKneelPose(), OdSidePose(false), In(OverdoseTimeline.Knees, OverdoseTimeline.Topple));
                else p = CharacterPose.Lerp(OdSidePose(false), SeizePose(0f), S(OverdoseTimeline.Topple, OverdoseTimeline.FallEnd));
                // Still on the feet: the body keeps wobbling on top of the keys.
                float up = S(0f, 0.35f) * (1f - S(OverdoseTimeline.Clutch, OverdoseTimeline.Knees));
                if (up > 0f)
                {
                    p.Torso = p.Torso * Quaternion.Euler(3f * OdNoise(1.3f, 1f) * up, 4f * OdNoise(0.9f, 2f) * up, 5f * OdNoise(1.1f, 3f) * up);
                    p.Head = p.Head * Quaternion.Euler(6f * OdNoise(1.2f, 4f) * up, 7f * OdNoise(0.8f, 5f) * up, 6f * OdNoise(1f, 6f) * up);
                }
            }
            else
            {
                // ---- gets up slowly: onto the side, hands and knees, one knee (pushing on it), hands on the knees, up
                var limp = SeizePose(OverdoseTimeline.GetUpStart - OverdoseTimeline.SeizeStart);
                if (t < OverdoseTimeline.RollSide) p = CharacterPose.Lerp(limp, OdSidePose(true), S(OverdoseTimeline.GetUpStart, OverdoseTimeline.RollSide));
                else if (t < OverdoseTimeline.AllFours) p = CharacterPose.Lerp(OdSidePose(true), OdAllFoursPose(), S(OverdoseTimeline.RollSide, OverdoseTimeline.AllFours));
                else if (t < OverdoseTimeline.HalfKneel) p = CharacterPose.Lerp(OdAllFoursPose(), OdHalfKneelPose(), S(OverdoseTimeline.AllFours, OverdoseTimeline.HalfKneel));
                else if (t < OverdoseTimeline.HandsOnKnees) p = CharacterPose.Lerp(OdHalfKneelPose(), OdBentPose(), S(OverdoseTimeline.HalfKneel, OverdoseTimeline.HandsOnKnees));
                else p = CharacterPose.Lerp(OdBentPose(), current, S(OverdoseTimeline.HandsOnKnees, OverdoseTimeline.End));
                // Weak and dizzy: the body sways while it works its way up.
                float sway = S(OverdoseTimeline.RollSide - 0.3f, OverdoseTimeline.AllFours) * (1f - S(OverdoseTimeline.HandsOnKnees + 0.4f, OverdoseTimeline.End));
                if (sway > 0f)
                {
                    p.Pelvis = Quaternion.Euler(0f, 0f, 4f * OdNoise(0.7f, 7f) * sway) * p.Pelvis;
                    p.Torso = p.Torso * Quaternion.Euler(4f * OdNoise(0.9f, 8f) * sway, 3f * OdNoise(0.6f, 9f) * sway, 5f * OdNoise(0.8f, 10f) * sway);
                    p.Head = p.Head * Quaternion.Euler(8f * OdNoise(0.7f, 11f) * sway, 9f * OdNoise(0.5f, 12f) * sway, 5f * OdNoise(0.9f, 13f) * sway);
                }
            }
            p.PropR = current.PropR;
            return p;
        }

        // ================================================================== falling

        /// <summary>Lurches to the right: the right foot steps out to catch it, the left arm flung up for balance, head lolling.</summary>
        static CharacterPose OdLurchPose()
        {
            var p = CharacterPose.Identity;
            p.PelvisPos = new Vector3(0.08f, BlockyCharacter.HipHeight - 0.05f, -0.02f);
            p.Pelvis = Quaternion.Euler(-3f, 4f, -7f);
            p.Torso = Quaternion.Euler(-5f, 6f, -9f);
            p.Head = Quaternion.Euler(-12f, -14f, 15f);
            p.SolveLeg(0, new Vector3(0.24f, 0f, 0.04f), new Vector3(0.3f, 0f, 1f));
            p.SolveLeg(1, new Vector3(-0.1f, 0f, -0.04f), new Vector3(0f, 0f, 1f));
            p.SolveArm(0, p.TorsoPoint(new Vector3(0.58f, 0.3f, 0.16f)), TorsoDir(p, 1f, -0.5f, -0.6f));
            p.SolveArm(1, p.TorsoPoint(new Vector3(-0.72f, 0.66f, 0.1f)), TorsoDir(p, -0.3f, -1f, -0.4f));
            return p;
        }

        /// <summary>Topples back the other way: a catch step back with the left foot, the right arm flung out.</summary>
        static CharacterPose OdStepBackPose()
        {
            var p = CharacterPose.Identity;
            p.PelvisPos = new Vector3(-0.05f, BlockyCharacter.HipHeight - 0.08f, -0.2f);
            p.Pelvis = Quaternion.Euler(-6f, -6f, 8f);
            p.Torso = Quaternion.Euler(-9f, -8f, 8f);
            p.Head = Quaternion.Euler(-20f, 12f, -10f);
            p.SolveLeg(0, new Vector3(0.14f, 0f, -0.04f), new Vector3(0.1f, 0f, 1f));
            p.SolveLeg(1, new Vector3(-0.22f, 0f, -0.36f), new Vector3(-0.2f, 0f, 1f));
            p.SolveArm(0, p.TorsoPoint(new Vector3(0.74f, 0.62f, 0.02f)), TorsoDir(p, 0.3f, -1f, -0.4f));
            p.SolveArm(1, p.TorsoPoint(new Vector3(-0.5f, 0.24f, 0.2f)), TorsoDir(p, -1f, -0.5f, -0.6f));
            return p;
        }

        /// <summary>Doubles over with the right fist pressed on the heart, the other arm hanging, the knees going.</summary>
        static CharacterPose OdClutchPose()
        {
            var p = CharacterPose.Identity;
            p.PelvisPos = new Vector3(0.01f, 0.5f, -0.24f);
            p.Pelvis = Quaternion.Euler(10f, 4f, -3f);
            p.Torso = Quaternion.Euler(20f, 8f, -5f);
            p.Head = Quaternion.Euler(30f, 12f, 10f);
            p.SolveLeg(0, new Vector3(0.15f, 0f, -0.06f), new Vector3(0.25f, 0f, 1f));
            p.SolveLeg(1, new Vector3(-0.17f, 0f, -0.3f), new Vector3(-0.25f, 0f, 1f));
            p.SolveArm(0, p.TorsoPoint(new Vector3(-0.06f, 0.46f, 0.18f)), TorsoDir(p, 1f, -0.6f, -0.2f));
            p.SolveArm(1, p.TorsoPoint(new Vector3(-0.38f, 0.1f, 0.22f)), TorsoDir(p, -1f, -0.4f, -0.8f));
            return p;
        }

        /// <summary>Dropped onto the knees, slumped and tipping to the left, the fist still on the chest.</summary>
        static CharacterPose OdKneelPose()
        {
            const float z = -0.25f;
            var p = KneelPose(0.8f, z);
            p.Pelvis = Quaternion.Euler(0f, 0f, 9f);
            p.SolveLeg(0, new Vector3(0.12f, 0.11f, z - 0.34f), new Vector3(0f, -0.3f, 1f));
            p.SolveLeg(1, new Vector3(-0.12f, 0.11f, z - 0.34f), new Vector3(0f, -0.3f, 1f));
            p.Torso = Quaternion.Euler(34f, -6f, 10f);
            p.Head = Quaternion.Euler(34f, 10f, 16f);
            p.SolveArm(0, p.TorsoPoint(new Vector3(-0.05f, 0.44f, 0.18f)), TorsoDir(p, 1f, -0.6f, -0.2f));
            p.SolveArm(1, p.TorsoPoint(new Vector3(-0.44f, 0.05f, 0.12f)), TorsoDir(p, -1f, -0.3f, -0.6f));
            return p;
        }

        /// <summary>
        /// On the left side, facing +X. <paramref name="pushing"/>: getting up, the right hand planted on the floor in
        /// front of the chest; otherwise the arms flop as the body hits the floor.
        /// </summary>
        static CharacterPose OdSidePose(bool pushing)
        {
            var p = SidePose();
            if (pushing)
            {
                p.Torso = Quaternion.Euler(18f, 0f, -8f);
                p.Head = Quaternion.Euler(22f, 0f, -12f);
                p.SolveArm(0, new Vector3(0.24f, 0.07f, -0.08f), new Vector3(0.3f, 1f, -0.3f));
            }
            p.PelvisPos += new Vector3(0f, 0f, OdLieZ + 0.05f - ProneZ);
            return p;
        }

        /// <summary>On hands and knees, the head hanging (all the strength it has).</summary>
        static CharacterPose OdAllFoursPose()
        {
            var p = AllFoursPose();
            p.Head = Quaternion.Euler(4f, 10f, 6f);
            p.PelvisPos += new Vector3(0f, -0.02f, -0.08f);
            return p;
        }

        /// <summary>Left knee on the floor, right foot planted, both hands pushing down on the right knee.</summary>
        static CharacterPose OdHalfKneelPose()
        {
            var p = CharacterPose.Identity;
            p.PelvisPos = new Vector3(0f, 0.48f, -0.25f);
            p.Pelvis = Quaternion.Euler(6f, 0f, 0f);
            p.Torso = Quaternion.Euler(39f, 0f, 3f);
            p.Head = Quaternion.Euler(18f, 6f, 4f);
            p.SolveLeg(0, new Vector3(0.15f, 0f, 0.12f), new Vector3(0.2f, 0.3f, 1f));
            p.SolveLeg(1, new Vector3(-0.12f, 0.11f, -0.6f), new Vector3(0f, -0.3f, 1f));
            p.SolveArm(0, new Vector3(0.15f, 0.5f, 0.08f), TorsoDir(p, 1f, 0.2f, -0.6f));
            p.SolveArm(1, new Vector3(0.02f, 0.52f, 0.1f), TorsoDir(p, -1f, 0.2f, -0.6f));
            return p;
        }

        /// <summary>Nearly up, bent over with both hands on the thighs above the knees.</summary>
        static CharacterPose OdBentPose()
        {
            var p = CharacterPose.Identity;
            p.PelvisPos = new Vector3(0f, 0.5f, -0.12f);
            p.Pelvis = Quaternion.Euler(18f, 0f, 0f);
            p.Torso = Quaternion.Euler(37f, 0f, 0f);
            p.Head = Quaternion.Euler(6f, 0f, 0f);
            p.SolveLeg(0, new Vector3(0.14f, 0f, 0.04f), new Vector3(0.15f, 0f, 1f));
            p.SolveLeg(1, new Vector3(-0.14f, 0f, 0.04f), new Vector3(-0.15f, 0f, 1f));
            p.SolveArm(0, new Vector3(0.16f, 0.4f, 0.14f), TorsoDir(p, 1f, 0f, -0.5f));
            p.SolveArm(1, new Vector3(-0.16f, 0.4f, 0.14f), TorsoDir(p, -1f, 0f, -0.5f));
            return p;
        }

        // ================================================================== seizure

        /// <summary>
        /// On the back, <paramref name="u"/> s into the fit: a rigid start (arched back, head thrown back, arms locked on
        /// the chest, fine tremor), then rhythmic jerks of the whole body (arms and knees flex, the chin snaps forward,
        /// the hips bump the floor) with every limb taking its own share and the head rolling side to side; the jerks
        /// slow down and fade, then the body lies limp, breathing heavily, the head turned aside.
        /// </summary>
        CharacterPose SeizePose(float u)
        {
            float sd = overdoseSeed;
            float jerk = Drunkenness.Seizure(u, sd, out float tonic, out int beat);
            float limp = Drunkenness.Seg(u, OverdoseTimeline.SeizeDuration - 1.8f, OverdoseTimeline.SeizeDuration + 0.4f);
            float fit = 1f - limp;
            float trem = 0.9f * tonic + 0.3f * fit;
            float N(float f, float k) => Drunkenness.Fbm(u * f, sd + k);
            float L(int limb) => jerk * (0.7f + 0.3f * Drunkenness.Hash(beat * 7 + limb, sd + 7.7f));
            float br = Mathf.Sin(breathPhase * 1.3f) * limp;

            var p = CharacterPose.Identity;
            p.PelvisPos = new Vector3(0.012f * N(6.1f, 1f) * trem, ProneY + 0.028f * L(0) + 0.005f * N(8.3f, 2f) * trem, OdLieZ);
            p.Pelvis = Supine * Quaternion.Euler(6f * L(0) * fit,
                (-6f + 7f * N(0.6f, 3f)) * fit + 10f * limp + 3f * N(7.7f, 4f) * trem,
                3f * N(5.3f, 5f) * trem);
            p.Torso = Quaternion.Euler(-10f * tonic + 9f * L(1) + 3f * br + 3f * N(9.1f, 6f) * trem,
                5f * N(0.9f, 7f) * fit,
                6f * N(0.7f, 8f) * fit + 2.5f * N(8.7f, 9f) * trem);
            p.Head = Quaternion.Euler(-20f * tonic + 18f * L(2) - 4f * limp + 2f * br + 5f * N(10.3f, 10f) * trem,
                28f * N(0.45f, 11f) * fit + 38f * limp + 9f * N(6.9f, 12f) * trem,
                8f * N(0.8f, 13f) * fit);

            for (int arm = 0; arm < 2; arm++)
            {
                float side = arm == 0 ? 1f : -1f;
                float j = L(3 + arm);
                var semi = new Vector3(side * 0.42f, 0.26f, 0.22f);
                var bent = new Vector3(side * 0.2f, 0.47f, 0.27f);
                var stiff = new Vector3(side * 0.22f, 0.43f, 0.22f);
                var rest = new Vector3(side * 0.45f, 0.17f, 0.01f);
                var jitter = new Vector3(N(7.3f, 20f + arm), N(8.9f, 22f + arm), N(6.7f, 24f + arm)) * (0.035f * trem + 0.03f * fit);
                Vector3 target = Vector3.Lerp(semi, stiff, tonic) + (bent - semi) * j + jitter;
                target = Vector3.Lerp(target, rest, limp);
                p.SolveArm(arm, p.TorsoPoint(target), TorsoDir(p, side, Mathf.Lerp(-0.2f, -1f, limp), Mathf.Lerp(0.1f, -0.4f, limp)));
            }

            for (int leg = 0; leg < 2; leg++)
            {
                float side = leg == 0 ? 1f : -1f;
                float j = L(5 + leg);
                var straight = new Vector3(side * 0.05f, -0.665f, 0f);
                var flex = new Vector3(side * 0.07f, -0.53f, 0.02f);
                var rest = leg == 0 ? new Vector3(0.1f, -0.64f, 0f) : new Vector3(-0.16f, -0.6f, 0f);
                var jitter = new Vector3(N(6.3f, 30f + leg), N(7.1f, 32f + leg), 0f) * (0.025f * trem + 0.02f * fit);
                Vector3 local = Vector3.Lerp(straight + (flex - straight) * j + jitter, rest, limp);
                Vector3 pole = p.Pelvis * new Vector3(side * Mathf.Lerp(0.15f, 0.7f, leg == 1 ? limp : 0.3f * limp), 0f, 1f);
                p.SolveLeg(leg, p.HipPos(leg) + p.Pelvis * local, pole);
            }
            return p;
        }
    }
}
