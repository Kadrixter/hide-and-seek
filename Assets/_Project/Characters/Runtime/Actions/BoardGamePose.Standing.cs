using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Standing at a wall board (tic-tac-toe chalkboard): relaxed stance with breathing and a slow weight shift, the
    /// left fist at the chin while thinking. Drawing: the body steps towards the cell (feet lift in turn on the way,
    /// the input's <see cref="Input.Shift"/>), leans in, the right fist follows the chalk on the board (two-bone IK).
    /// </summary>
    public static partial class BoardGamePose
    {
        static CharacterPose Standing(in Input inp, float time)
        {
            var p = CharacterPose.Identity;
            float s = inp.Seed;
            float w = Mathf.SmoothStep(0f, 1f, inp.ReachW);
            float breathe = Mathf.Sin(time * 1.55f + s * 3f);
            float sway = Noise(time * 0.12f, s + 4f);
            Vector3 shift = new Vector3(inp.Shift.x, 0f, inp.Shift.z) * w;
            p.PelvisPos = BlockyCharacter.PelvisRest + shift + new Vector3(sway * 0.025f * (1f - w), -0.01f + breathe * 0.004f, 0f);
            p.Pelvis = Quaternion.Euler(0f, 0f, -sway * 2f * (1f - w));

            // Torso: upright, leaning into the board while drawing, turned a little towards the hand.
            float twist = 0f;
            if (w > 0.001f)
            {
                Vector3 sh = p.PelvisPos + BlockyCharacter.ShoulderRightRest;
                twist = Mathf.Clamp(Mathf.Atan2(inp.Reach.x - sh.x * 0.5f, Mathf.Max(0.05f, inp.Reach.z - p.PelvisPos.z)) * Mathf.Rad2Deg * 0.4f, -25f, 25f) * w;
            }
            float lean = 2f + 3f * inp.Think + inp.Lean + 9f * w + breathe * 0.8f;
            p.Torso = Quaternion.Euler(lean, twist, sway * 1.5f * (1f - w));

            // Feet under the hips, carried along with the shift; the lighter foot lifts on the way.
            float stepping = Mathf.Sin(Mathf.PI * Mathf.Clamp01(inp.ReachW)) * Mathf.Clamp01(inp.Shift.magnitude * 3f);
            int lead = inp.Shift.x >= 0f ? 0 : 1;
            for (int leg = 0; leg < 2; leg++)
            {
                float side = leg == 0 ? 1f : -1f;
                float lift = leg == lead ? stepping * 0.07f : stepping * 0.015f;
                Vector3 sole = new Vector3(side * 0.13f, lift, 0.02f) + shift + new Vector3(sway * 0.01f, 0f, 0f);
                p.SolveLeg(leg, sole, new Vector3(side * 0.15f, 0.2f, 1f));
            }

            // Arms: hanging relaxed with a slight bend; the left fist goes to the chin while thinking.
            Vector3 hangR = p.TorsoPoint(new Vector3(0.36f, 0.12f, 0.08f)) + Vector3.up * breathe * 0.004f;
            Vector3 hangL = p.TorsoPoint(new Vector3(-0.36f, 0.12f, 0.08f)) + Vector3.up * breathe * 0.004f;
            Vector3 chin = p.TorsoPoint(new Vector3(0f, 0.69f, 0.2f));
            Vector3 left = Vector3.Lerp(hangL, chin + new Vector3(0.015f, -0.085f, 0.02f), Mathf.SmoothStep(0f, 1f, inp.Think * (1f - w)));
            p.SolveArm(1, left, Vector3.Lerp(new Vector3(-0.6f, -0.6f, -0.5f), new Vector3(-0.2f, -1f, 0.3f), inp.Think));
            Vector3 right = Vector3.Lerp(hangR, inp.Reach, w);
            p.SolveArm(0, right, Vector3.Lerp(new Vector3(0.6f, -0.6f, -0.5f), new Vector3(0.8f, -0.5f, -0.3f), w));

            // Head towards what matters.
            Vector3 neck = p.TorsoPoint(BlockyCharacter.NeckRest + new Vector3(0f, 0.15f, 0f));
            Vector3 dir = Quaternion.Inverse(p.TorsoRoot) * (inp.Look - neck);
            float hy = Mathf.Clamp(Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, -65f, 65f) + Noise(time * 0.3f, s + 2f) * 3f;
            float hp = Mathf.Clamp(-Mathf.Atan2(dir.y, new Vector2(dir.x, dir.z).magnitude) * Mathf.Rad2Deg, -40f, 50f) + Noise(time * 0.25f, s + 3f) * 2f;
            p.Head = Quaternion.Euler(hp, hy, inp.Think * 6f * Mathf.Sin(time * 0.4f + s));
            return p;
        }
    }
}
