using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Solo one-shot emotes: wave, applause, laughing, facepalm, pointing. Each one: anticipation,
    /// the gesture with follow-through, a settle back towards the stance (the layer fade finishes it). Hand targets in
    /// torso space (they ride on the breathing body), contacts with the own head in head space.
    /// </summary>
    public partial class BlockyCharacterActions
    {
        static readonly Vector3 RestR = new Vector3(0.37f, 0.06f, 0.04f), RestL = new Vector3(-0.37f, 0.06f, 0.04f);

        CharacterPose WavePose(in CharacterPose b, float t)
        {
            var p = Stance(b);
            float up = Seg(t, 0.05f, 0.5f) * (1f - Seg(t, 2.05f, 2.65f));
            float sw = Env(t, 0.4f, 0.6f, 1.9f, 2.2f);
            float ph = (t - 0.45f) * 2f * Mathf.PI * 2.3f;
            float s = Mathf.Sin(ph);
            p.PelvisPos += new Vector3(-0.012f * up, -0.006f * (1f - Mathf.Cos(2f * ph)) * sw, 0f);
            p.Torso = Quaternion.Euler(-3f * up, 7f * up, 4f * up + 1.2f * s * sw);
            p.Head = Quaternion.Euler(-5f * up + 2f * Mathf.Abs(s) * sw, -5f * up, -6f * up - 2f * s * sw);
            Feet(ref p);
            Alive(ref p, t);
            Vector3 raised = new Vector3(0.48f + 0.11f * s * sw, 0.93f - 0.03f * Mathf.Abs(s) * sw, 0.15f);
            Vector3 hand = Vector3.Lerp(RestR, raised, up) + new Vector3(0.12f, 0f, 0.1f) * Hump(up, 0f, 1f);
            Arm(ref p, 0, hand, new Vector3(1f, -0.9f, -0.15f));
            Hang(ref p, 1, 0.02f * up);
            return p;
        }

        static float ClapOpen(float t)
        {
            float s = 1f;
            foreach (float c in EmoteTimeline.ClapTimes)
            {
                if (t >= c - 0.13f && t < c) s = Mathf.Min(s, 1f - ActionTimeline.EaseIn((t - (c - 0.13f)) / 0.13f));
                else if (t >= c && t < c + 0.17f) s = Mathf.Min(s, ActionTimeline.EaseOut((t - c) / 0.17f));
            }
            return s;
        }

        CharacterPose ClapPose(in CharacterPose b, float t)
        {
            var p = Stance(b);
            float up = Seg(t, 0f, 0.42f) * (1f - Seg(t, 2.55f, 3.15f));
            float open = Mathf.Lerp(1f, ClapOpen(t), up);
            float hit = 1f - open;
            p.PelvisPos += new Vector3(0f, -0.012f * hit * up, 0f);
            p.Torso = Quaternion.Euler(5f * up + 1.5f * hit, 0f, 0f);
            p.Head = Quaternion.Euler(-4f * up + 3f * hit, 0f, 0f);
            Feet(ref p);
            Alive(ref p, t);
            for (int arm = 0; arm < 2; arm++)
            {
                float sx = arm == 0 ? 1f : -1f;
                var c = new Vector3((0.115f + 0.12f * open) * sx, 0.43f + 0.025f * open, 0.35f - 0.035f * open);
                Arm(ref p, arm, Vector3.Lerp(arm == 0 ? RestR : RestL, c, up), new Vector3(sx, -0.7f, -0.55f));
            }
            return p;
        }

        CharacterPose LaughPose(in CharacterPose b, float t)
        {
            var p = Stance(b);
            float lean = Seg(t, 0.05f, 0.4f);
            float fold = Env(t, 1.0f, 1.45f, 2.3f, 2.85f);
            float end = Seg(t, 2.85f, 3.45f);
            float shake = Env(t, 0.28f, 0.45f, 2.5f, 3.0f);
            float hh = Mathf.Sin(t * 2f * Mathf.PI * 6.3f) * shake;
            float k = 1f - end;
            p.PelvisPos += new Vector3(0f, (-0.045f * fold - 0.006f * Mathf.Abs(hh)) * k, -0.05f * fold * k);
            p.Torso = Quaternion.Euler((-10f * lean * (1f - fold) + 26f * fold + 2.6f * hh) * k, 0f, 2f * hh * k);
            p.Head = Quaternion.Euler((-20f * lean * (1f - fold) + 4f * fold + 5f * hh) * k, 0f, -3f * hh * k);
            Feet(ref p, FootR + new Vector3(0.02f, 0f, 0f), FootL - new Vector3(0.02f, 0f, 0f));
            Alive(ref p, t, 0.6f);
            // Hands on the belly; the right one slaps the knee once while folded, later wipes a tear.
            float belly = Seg(t, 0.05f, 0.4f) * (1f - Seg(t, 2.95f, 3.45f));
            Vector3 bR = Vector3.Lerp(RestR, new Vector3(0.1f, 0.28f, 0.22f), belly);
            Vector3 bL = Vector3.Lerp(RestL, new Vector3(-0.1f, 0.24f, 0.22f), belly);
            Arm(ref p, 1, bL, new Vector3(-1f, -0.6f, -0.4f));
            Vector3 handR = p.TorsoPoint(bR);
            float slap = Hump(t, 1.35f, 1.85f);
            handR = Vector3.Lerp(handR, new Vector3(0.16f, 0.48f, 0.22f), slap * slap);
            float tear = Hump(t, 2.55f, 3.3f);
            handR = Vector3.Lerp(handR, HeadPoint(p, new Vector3(0.12f, 0.25f, 0.31f)), Mathf.Clamp01(tear * 1.4f));
            p.SolveArm(0, handR, p.TorsoRoot * new Vector3(1f, -0.6f, -0.3f));
            return p;
        }

        CharacterPose FacepalmPose(in CharacterPose b, float t)
        {
            var p = Stance(b);
            float look = Hump(t, 0f, 0.5f);
            float up = Seg(t, 0.12f, 0.55f) * (1f - Seg(t, 2.45f, 3.0f));
            float drop = Seg(t, 0.36f, 0.66f) * (1f - Seg(t, 2.5f, 3.05f));
            float shake = Env(t, 0.9f, 1.1f, 2.0f, 2.3f) * Mathf.Sin((t - 0.9f) * 2f * Mathf.PI * 0.9f);
            float sigh = Hump(t, 0.6f, 1.6f);
            p.PelvisPos += new Vector3(0f, -0.01f * drop, -0.012f * drop);
            p.Torso = Quaternion.Euler(9f * drop - 3f * sigh, 0f, 0f);
            p.Head = Quaternion.Euler(-8f * look + 22f * drop, 12f * shake, 0f);
            Feet(ref p);
            Alive(ref p, t, 0.7f);
            Vector3 face = HeadPoint(p, new Vector3(0.03f, 0.27f, 0.33f));
            Vector3 rest = p.TorsoPoint(RestR);
            Vector3 hand = Vector3.Lerp(rest, face, up) + p.TorsoRoot * new Vector3(0.12f, 0f, 0.14f) * Hump(up, 0f, 1f) * (1f - drop);
            p.SolveArm(0, hand, p.TorsoRoot * new Vector3(0.35f, -1f, 0.3f));
            // Other hand on the hip.
            float hip = Seg(t, 0.25f, 0.7f) * (1f - Seg(t, 2.6f, 3.1f));
            Arm(ref p, 1, Vector3.Lerp(RestL, new Vector3(-0.3f, 0.1f, 0.01f), hip), new Vector3(-1f, 0f, -0.45f));
            return p;
        }

        CharacterPose PointPose(in CharacterPose b, float t)
        {
            var p = Stance(b);
            float up = Seg(t, 0.05f, 0.36f) * (1f - Seg(t, 2.15f, 2.7f));
            float jab = Hump(t, 0.52f, 0.72f) + Hump(t, 0.9f, 1.1f);
            float laugh = Env(t, 1.15f, 1.35f, 2.0f, 2.35f);
            float hh = Mathf.Sin(t * 2f * Mathf.PI * 6f) * laugh;
            p.PelvisPos += new Vector3(0f, -0.008f * laugh, -0.01f * laugh);
            p.Torso = Quaternion.Euler(5f * up + 3f * jab + 9f * laugh + 2.5f * hh, -9f * up, 0f);
            p.Head = Quaternion.Euler(-3f * up + 3f * hh, 4f * up, 0f);
            Feet(ref p);
            Alive(ref p, t, 0.7f);
            var aim = new Vector3(0.2f, 0.66f, 0.56f + 0.06f * jab);
            Arm(ref p, 0, Vector3.Lerp(RestR, aim, up), new Vector3(1f, -0.4f, -0.2f));
            float belly = Seg(t, 1.0f, 1.3f) * (1f - Seg(t, 2.15f, 2.6f));
            Arm(ref p, 1, Vector3.Lerp(RestL, new Vector3(-0.08f, 0.26f, 0.22f), belly), new Vector3(-1f, -0.6f, -0.4f));
            return p;
        }
    }
}
