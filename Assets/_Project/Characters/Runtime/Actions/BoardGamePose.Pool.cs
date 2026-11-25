using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Player at the lobby pool table with a cue. The table moves the cue; both fists follow it (two-bone IK).
    /// Bend 0: standing, the cue upright at the right side (<see cref="Input.Reach"/> = right fist on it), the left
    /// arm hanging, steps while the table walks the player around (<see cref="Input.Walk"/>). Bend 1: the shooting
    /// stance - feet apart with the left foot forward, hips back, the torso bent over the cue (just as low as the
    /// bridge hand needs to reach <see cref="Input.Bridge"/>, never so low that the head sinks under the cue), turned so the head is above the cue line, the left fist
    /// on the cloth beside the cue, the right fist on the butt with the elbow up, the head looking along the shot.
    /// </summary>
    public static partial class BoardGamePose
    {
        /// <summary>Half the torso's depth (its front face is the underside when bent over).</summary>
        const float TorsoHalfD = 2f * BlockyCharacter.PixelSize;

        /// <summary>
        /// Largest lean (degrees, at least 20) for which the bent torso's underside and the neck stay above a table edge
        /// <paramref name="edgeZ"/> ahead with its top at <paramref name="edgeY"/>: the chest rests over the rail, never in it.
        /// </summary>
        static float ClearLean(Vector3 pelvis, float torsoLen, float edgeZ, float edgeY)
        {
            float best = 20f;
            for (float l = 20f; l <= 80f; l += 1f)
            {
                float r = l * Mathf.Deg2Rad, sin = Mathf.Sin(r), cos = Mathf.Cos(r);
                // Where the underside (front face) crosses the edge, how high it is there.
                float y = (edgeZ - pelvis.z - TorsoHalfD * cos) / Mathf.Max(0.05f, sin);
                bool torsoOk = y > torsoLen || y < 0f && pelvis.y - TorsoHalfD * sin >= edgeY + 0.02f
                               || pelvis.y + y * cos - TorsoHalfD * sin >= edgeY + 0.02f;
                // The neck (and the big head on it) past the edge: well above the rail.
                float neckZ = pelvis.z + torsoLen * sin, neckY = pelvis.y + torsoLen * cos;
                bool neckOk = neckZ < edgeZ || neckY >= edgeY + 0.14f;
                if (!torsoOk || !neckOk) break;
                best = l;
            }
            return best;
        }

        static CharacterPose PoolStance(in Input inp, float time)
        {
            var p = CharacterPose.Identity;
            float s = inp.Seed;
            float b = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(inp.Bend));
            float walk = Mathf.Clamp01(inp.Walk) * (1f - 0.75f * b);
            float breathe = Mathf.Sin(time * 1.55f + s * 3f);
            float sway = Noise(time * 0.12f, s + 4f) * (1f - b) * (1f - walk);
            float ph = time * 9f + s * 5f;
            float bob = Mathf.Abs(Mathf.Sin(ph)) * walk;

            // Hips: relaxed standing (bobbing while walking) -> back and a little down in the stance.
            Vector3 pelUp = BlockyCharacter.PelvisRest + new Vector3(sway * 0.02f, -0.01f + breathe * 0.004f + bob * 0.022f - walk * 0.012f, 0f);
            Vector3 pelBent = new Vector3(0.02f, BlockyCharacter.HipHeight - 0.03f + breathe * 0.003f, -0.08f);
            p.PelvisPos = Vector3.Lerp(pelUp, pelBent, b);

            // Torso: the smallest lean that brings the left shoulder within reach of the bridge, turned so the chin is
            // above the cue line (the right fist holds the cue, so it is on that line). The hips are about as high as
            // the table, so the lean stops where the neck would sink below a hand over the bridge: the big head and
            // the chest stay above the cue and the rail instead of going into the table (a long reach straightens
            // the arm instead).
            float lean = 2f + breathe * 0.8f + walk * 4f, twist = 0f;
            if (b > 0.001f)
            {
                float neckLen = BlockyCharacter.NeckRest.y;
                float minNeck = Mathf.Max(inp.Bridge.y, 0.7f) + 0.2f;
                float maxL = Mathf.Clamp(Mathf.Acos(Mathf.Clamp((minNeck - pelBent.y) / neckLen, 0f, 1f)) * Mathf.Rad2Deg, 30f, 80f);
                if (inp.ClearZ > 0.01f) maxL = Mathf.Min(maxL, ClearLean(pelBent, neckLen, inp.ClearZ, inp.ClearY));
                float bestL = maxL, bestT = 0f;
                for (float l = 30f; l <= maxL + 0.01f; l += 3f)
                {
                    float r = l * Mathf.Deg2Rad;
                    float fwd = 0.69f * Mathf.Sin(r) + 0.2f * Mathf.Cos(r);
                    float t = Mathf.Asin(Mathf.Clamp((inp.Reach.x - p.PelvisPos.x) / Mathf.Max(0.25f, fwd), -0.6f, 0.6f)) * Mathf.Rad2Deg;
                    var q = p;
                    q.Pelvis = Quaternion.Euler(0f, t * 0.35f, 0f);
                    q.Torso = Quaternion.Euler(l, t * 0.65f, 0f);
                    bestL = l;
                    bestT = t;
                    if (Vector3.Distance(q.ShoulderPos(1), inp.Bridge) <= Reach * 0.94f) break;
                }
                lean = Mathf.Lerp(lean, bestL + breathe * 0.4f, b);
                twist = bestT * b;
            }
            p.Pelvis = Quaternion.Euler(0f, twist * 0.35f, -sway * 2f);
            p.PelvisPos.z = Mathf.Min(p.PelvisPos.z, inp.ClearZ > 0.01f ? inp.ClearZ - TorsoHalfD - 0.04f : p.PelvisPos.z);
            p.Torso = Quaternion.Euler(lean, twist * 0.65f, sway * 1.5f);

            // Feet: under the hips (stepping in turn while walking) -> the stance (right foot back on the line, left forward).
            for (int leg = 0; leg < 2; leg++)
            {
                float side = leg == 0 ? 1f : -1f;
                float step = Mathf.Sin(ph + leg * Mathf.PI);
                Vector3 up = new Vector3(side * 0.13f + sway * 0.01f, Mathf.Max(0f, step) * 0.08f * walk, 0.02f + step * 0.14f * walk);
                Vector3 bent = leg == 0 ? new Vector3(0.22f, 0f, -0.2f) : new Vector3(-0.14f, 0f, 0.13f);
                p.SolveLeg(leg, Vector3.Lerp(up, bent, b), new Vector3(side * 0.2f, 0.2f, 1f));
            }

            // Left arm: hanging (swinging while walking) -> the bridge on the cloth, elbow out to the side.
            float swing = Mathf.Sin(ph) * walk;
            Vector3 hangL = p.TorsoPoint(new Vector3(-0.36f, 0.12f, 0.08f + swing * 0.12f)) + Vector3.up * breathe * 0.004f;
            // (On the way between the two the hand goes up in an arc, over the rail, not through it.)
            Vector3 leftTo = Vector3.Lerp(hangL, inp.Bridge, b) + Vector3.up * (0.2f * Mathf.Sin(Mathf.PI * b));
            p.SolveArm(1, leftTo, Vector3.Lerp(new Vector3(-0.6f, -0.6f, -0.5f), new Vector3(-1f, 0.35f, 0.1f), b));
            // Right arm: the fist on the cue (upright at the side, or the butt behind the body with the elbow up).
            p.SolveArm(0, inp.Reach, Vector3.Lerp(new Vector3(0.6f, -0.6f, -0.5f), new Vector3(0.35f, 1f, -0.45f), b));

            // Head: towards the look target (in the stance that is far up relative to the bent torso).
            Vector3 neck = p.TorsoPoint(BlockyCharacter.NeckRest + new Vector3(0f, 0.15f, 0f));
            Vector3 dir = Quaternion.Inverse(p.TorsoRoot) * (inp.Look - neck);
            float hy = Mathf.Clamp(Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, -60f, 60f) + Noise(time * 0.3f, s + 2f) * 3f * (1f - b);
            float hp = Mathf.Clamp(-Mathf.Atan2(dir.y, new Vector2(dir.x, dir.z).magnitude) * Mathf.Rad2Deg, -85f, 50f) + Noise(time * 0.25f, s + 3f) * 2f * (1f - b);
            p.Head = Quaternion.Euler(hp, hy, 0f);
            return p;
        }
    }
}
