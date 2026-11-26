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

        /// <summary>
        /// Posture of the laugh over time: below 0 thrown back (head up), above 0 folded forward. Follows the recorded
        /// laugh (<see cref="EmoteTimeline.LaughBursts"/>): thrown back by the opening laugh, folded and wheezing
        /// silently, jerked back by the "HA", bobbing through the chuckles, winding up and folding into the loudest
        /// burst, catching breath, one more "HA", straightening up while calming down, a last little relapse.
        /// </summary>
        static readonly Vector2[] LaughLean =
        {
            new(0f, 0f), new(0.25f, -0.9f), new(0.8f, -0.7f), new(1.2f, -0.2f), new(1.6f, 0.85f), new(2.4f, 1f),
            new(3.3f, 0.9f), new(3.7f, 0.5f), new(3.92f, -0.8f), new(4.25f, -0.3f), new(4.6f, 0.3f), new(5.2f, 0.12f),
            new(5.8f, 0.25f), new(6.25f, -0.4f), new(6.55f, -1f), new(6.85f, 0.9f), new(7.4f, 1f), new(7.85f, 0.5f),
            new(8.02f, -0.6f), new(8.35f, 0.1f), new(9.0f, 0.2f), new(9.5f, 0.35f), new(9.95f, 0f), new(10.25f, 0.22f),
            new(10.5f, 0.05f),
        };

        /// <summary>Loudness of the recorded laugh at <paramref name="t"/> (0..1): drives the "ha" shaking.</summary>
        static float LaughLoud(float t)
        {
            float l = 0f;
            foreach (var b in EmoteTimeline.LaughBursts)
                if (t > b.x - 0.08f && t < b.y + 0.2f) l = Mathf.Max(l, b.z * Env(t, b.x - 0.08f, b.x + 0.04f, b.y - 0.04f, b.y + 0.2f));
            return l;
        }

        CharacterPose LaughPose(in CharacterPose b, float t)
        {
            var p = Stance(b);
            float lean = EmoteKeys.Sample(LaughLean, t);
            float back = Mathf.Max(0f, -lean), fold = Mathf.Max(0f, lean);
            // "Ha-ha" pulses: fast in the big laughs, a little slower as it calms down; a fine shoulder tremble
            // through the silent wheeze.
            float loud = LaughLoud(t);
            float wheeze = 0.3f * Env(t, 1.35f, 1.6f, 3.5f, 3.8f);
            float ph = t * 2f * Mathf.PI * (6.6f - 0.057f * t);
            float hh = Mathf.Sin(ph) * loud + Mathf.Sin(ph * 1.6f) * wheeze;
            // Chuckles: rocking side to side, the head shaking "I can't".
            float rock = Env(t, 4.35f, 4.7f, 5.75f, 6.15f) * Mathf.Sin((t - 4.35f) * 2f * Mathf.PI * 0.85f);
            float k = 1f - Seg(t, 10.3f, 10.5f);
            p.PelvisPos += new Vector3(0.012f * rock, -0.045f * fold - 0.008f * Mathf.Abs(hh) - 0.01f * back, -0.05f * fold + 0.012f * back) * k;
            p.Torso = Quaternion.Euler((-11f * back + 28f * fold + 2.8f * hh * (1f + 0.5f * fold)) * k, 7f * rock * k, (2f * hh + 4f * rock) * k);
            p.Head = Quaternion.Euler((-22f * back + 5f * fold + 5f * hh) * k, 9f * rock * k, (-3f * hh - 3f * rock) * k);
            Feet(ref p, FootR + new Vector3(0.02f, 0f, 0f), FootL - new Vector3(0.02f, 0f, 0f));
            Alive(ref p, t, 0.6f);

            // Hands on the belly (they ride the shaking); braced on the knees through the silent wheeze (the right one
            // patting the thigh); left on the chest catching breath; the right one winds up and slaps the knee at the
            // loudest burst, wipes a tear while calming down.
            float belly = Seg(t, 0.05f, 0.4f) * (1f - Seg(t, 10.1f, 10.5f));
            float bob = 0.012f * hh;
            Vector3 bR = Vector3.Lerp(RestR, new Vector3(0.1f, 0.28f + bob, 0.22f), belly);
            Vector3 bL = Vector3.Lerp(RestL, new Vector3(-0.1f, 0.24f + bob, 0.22f), belly);
            float chest = Env(t, 7.2f, 7.5f, 7.85f, 8.05f);
            bL = Vector3.Lerp(bL, new Vector3(-0.05f, 0.42f, 0.21f), chest);
            float knees = Env(t, 1.42f, 1.8f, 3.4f, 3.75f);
            float pat = Hump(t, 1.6f, 1.82f) + Hump(t, 2.3f, 2.52f) + Hump(t, 2.92f, 3.12f);
            Vector3 handL = Vector3.Lerp(p.TorsoPoint(bL), new Vector3(-0.15f, 0.55f, 0.24f), knees);
            p.SolveArm(1, handL, p.TorsoRoot * new Vector3(-1f, -0.6f, -0.4f));
            Vector3 handR = Vector3.Lerp(p.TorsoPoint(bR), new Vector3(0.15f, 0.55f + 0.07f * pat, 0.24f), knees);
            float windUp = Hump(t, 6.2f, 6.78f);
            handR = Vector3.Lerp(handR, p.TorsoPoint(new Vector3(0.4f, 0.5f, 0.18f)), windUp);
            float slap = Hump(t, 6.62f, 7.05f);
            handR = Vector3.Lerp(handR, new Vector3(0.16f, 0.5f, 0.22f), slap * slap);
            float tear = Hump(t, 8.95f, 9.95f);
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
