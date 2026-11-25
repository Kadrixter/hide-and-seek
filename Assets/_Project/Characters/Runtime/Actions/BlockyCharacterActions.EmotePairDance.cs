using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Dance in pairs, an 8 s loop at 120 bpm written once in own space (mirrored for the partner, spacing from the
    /// middle): bar 1 a swaying step-touch with both hands held, bar 2 the follower (role 1) lets go and spins once
    /// (smoother-step turn, stepping feet that never slide) and both hands are caught again, bar 3 an open rock step
    /// on one hand with the free arms flared out, a shoulder shimmy, bar 4 both walk once round the middle holding
    /// both hands, leaning back. Every turn is a continuous eased rotation, the loop ends exactly where it starts.
    /// </summary>
    public partial class BlockyCharacterActions
    {
        const float DanceHoldX = 0.27f, DanceHoldY = 1.0f;

        CharacterPose PairDancePose(in CharacterPose b, float t)
        {
            var fr = Frame();
            float tl = Mathf.Repeat(t, EmoteTimeline.PairDancePeriod);
            bool lead = fr.Lead;
            float half = fr.Half;

            // Holds: A = lead's right + follower's left, B = lead's left + follower's right (both: own side of the arm).
            float freeSpin = Env(tl, 2.05f, 2.2f, 3.6f, 3.8f);
            float wA = 1f - freeSpin;
            float wB = 1f - freeSpin - Env(tl, 3.95f, 4.15f, 4.95f, 5.25f);

            // Sway (pair-space x, the same way in the world for both), beat bounce, rock step, circle.
            float bar1 = 1f - Seg(tl, 1.9f, 2.1f) + Seg(tl, 7.9f, 8f);
            float sway = 0.075f * Mathf.Sin(Mathf.PI * tl) * Mathf.Clamp01(bar1 + 0.5f * Env(tl, 5.2f, 5.4f, 5.8f, 6f));
            float bounce = 0.022f * (0.5f + 0.5f * Mathf.Cos(2f * Mathf.PI * tl / 0.5f));
            float rock = Env(tl, 4.0f, 4.35f, 4.6f, 5.0f);
            float shimmy = Env(tl, 5.3f, 5.45f, 5.8f, 5.95f) * Mathf.Sin(tl * 2f * Mathf.PI * 6f);
            float circle = 360f * Smoother(tl, EmoteTimeline.CircleStart, EmoteTimeline.CircleEnd);
            float circleW = Env(tl, 5.9f, 6.1f, 7.8f, 7.95f);
            float spin = lead ? 0f : 360f * Smoother(tl, EmoteTimeline.SpinStart, EmoteTimeline.SpinEnd);

            float ownX = lead ? sway : -sway;
            Vector3 shift = new Vector3(ownX, -bounce - 0.01f, -0.1f * rock + 0.03f * freeSpin * (lead ? 0f : 1f));
            float lean = 3f - 7f * rock - 7f * circleW;
            var p = OwnStance(b, fr, shift, 0f, lean);
            p.Pelvis = p.Pelvis * Quaternion.Euler(0f, 0f, -4f * (lead ? sway : -sway) / 0.075f * 0.6f);
            p.Torso = p.Torso * Quaternion.Euler(0f, 0f, 4.5f * (lead ? sway : -sway) / 0.075f * 0.6f + 5f * shimmy);
            p.Head = Quaternion.Euler(-4f + 40f * bounce, 0f, -5f * (lead ? sway : -sway) / 0.075f * 0.6f);

            // Whole-body turns: the follower's own spin (about her own centre), the pair circle (about the middle).
            var mid = new Vector3(0f, 0f, half);
            Quaternion qc = Quaternion.Euler(0f, circle, 0f), qs = Quaternion.Euler(0f, spin, 0f);
            Vector3 pelvisOwn = new Vector3(0f, BlockyCharacter.HipHeight - 0.005f, 0f) + shift;
            Vector3 pelvisSpun = mid + qc * (pelvisOwn - mid);
            p.PelvisPos = fr.Own(pelvisSpun);
            p.Pelvis = fr.OwnRot * qc * qs * Quaternion.Euler(0f, 0f, -2.4f * (lead ? sway : -sway) / 0.075f);

            // Feet: planted; stepping with the spin / the circle (each foot moves only while lifted).
            for (int leg = 0; leg < 2; leg++)
            {
                Vector3 local = leg == 0 ? new Vector3(0.13f, 0f, 0.01f) : new Vector3(-0.13f, 0f, -0.01f);
                StepPhase(spin, 60f, leg, out float sAng, out float sLift);
                StepPhase(circle, 40f, leg, out float cAng, out float cLift);
                Vector3 foot = Quaternion.Euler(0f, sAng, 0f) * local + new Vector3(0f, 0.055f * sLift, 0f);
                // Rock step: the right foot steps back and returns (planted while the weight is on it).
                if (leg == 0) foot += new Vector3(0f, 0.05f * Hump(tl, 4.0f, 4.3f) + 0.05f * Hump(tl, 4.6f, 4.95f), -0.16f * Env(tl, 4.0f, 4.3f, 4.6f, 4.95f));
                // Off-beat toe taps in bar 1.
                foot.y += 0.03f * bar1 * Hump(Mathf.Repeat(tl + (leg == 0 ? 0f : 0.5f), 1f), 0.55f, 0.85f) * (1f - freeSpin);
                Vector3 c = mid + Quaternion.Euler(0f, cAng, 0f) * (foot - mid) + new Vector3(0f, 0.05f * cLift, 0f);
                Vector3 fwd = fr.OwnDir(Quaternion.Euler(0f, cAng + sAng, 0f) * Vector3.forward);
                Vector3 sideDir = Vector3.Cross(Vector3.up, fwd);
                p.SolveLeg(leg, fr.Own(c), fwd + sideDir * (leg == 0 ? 0.12f : -0.12f));
            }
            Alive(ref p, t, 0.25f);

            for (int arm = 0; arm < 2; arm++)
            {
                float sx = arm == 0 ? 1f : -1f;
                bool isA = lead ? arm == 0 : arm == 1;
                float w = isA ? wA : wB;
                // Held: own side of the arm, at the middle, swaying with the bodies, rotating with the circle.
                Vector3 held = new Vector3(sx * DanceHoldX + ownX, DanceHoldY + 0.03f * shimmy, half - FistHalf);
                held = mid + qc * (held - mid);
                Vector3 free;
                if (freeSpin > 0.01f)
                    free = lead
                        ? (arm == 0 ? new Vector3(0.52f, 0.86f, 0.06f) : new Vector3(-0.3f, 0.12f, 0.0f))
                        : (arm == 1 ? new Vector3(-0.44f, 0.98f, 0.1f) : new Vector3(0.3f, 0.12f, 0.0f));
                else free = new Vector3(sx * 0.62f, 0.68f, 0.12f); // flared out (rock step)
                Vector3 freeRoot = p.TorsoPoint(free);
                Vector3 target = Vector3.Lerp(freeRoot, fr.Own(held), Mathf.Clamp01(w));
                Vector3 pole = Vector3.Lerp(p.TorsoRoot * new Vector3(sx, -0.6f, -0.4f), p.TorsoRoot * new Vector3(sx, -0.2f, -0.5f), 1f - Mathf.Clamp01(w));
                p.SolveArm(arm, target, pole);
            }
            return p;
        }
    }
}
