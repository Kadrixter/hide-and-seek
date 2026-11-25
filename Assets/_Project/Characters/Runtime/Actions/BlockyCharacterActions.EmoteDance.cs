using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Looping solo emotes: two dances with different rhythm (disco at 120 bpm with hip pops, a step-touch with
    /// "raise the roof" pushes) and sitting down on the floor
    /// tired (sit down, heavy breathing, wiping the forehead, getting up again on the stop).
    /// </summary>
    public partial class BlockyCharacterActions
    {
        CharacterPose DiscoPose(in CharacterPose b, float t)
        {
            var p = Stance(b);
            const float beat = 0.5f;
            float bt = t / beat;
            int i = Mathf.FloorToInt(bt);
            float f = bt - i;
            float a = Seg(f, 0f, 0.4f);
            bool upNow = (i & 1) == 0;
            float s = upNow ? 1f : -1f;
            float xs = Mathf.Lerp(-s, s, a);
            float bounce = 0.5f + 0.5f * Mathf.Cos(2f * Mathf.PI * f);
            p.PelvisPos += new Vector3(0.05f * xs, -0.045f * bounce - 0.01f, 0f);
            p.Pelvis = Quaternion.Euler(0f, 6f * xs, 6f * xs);
            p.Torso = Quaternion.Euler(4f, 9f * xs, -10f * xs);
            p.Head = Quaternion.Euler(-3f + 7f * bounce, -7f * xs, 5f * xs);
            Feet(ref p, new Vector3(0.18f, 0f, 0.03f), new Vector3(-0.18f, 0f, -0.02f));
            Alive(ref p, t, 0.4f);
            Vector3 high = new Vector3(0.5f, 1.12f, 0.22f), low = new Vector3(-0.12f, 0.03f, 0.27f);
            Vector3 hand = upNow ? Vector3.Lerp(low, high, a) : Vector3.Lerp(high, low, a);
            hand += new Vector3(0.16f, 0f, 0.12f) * Hump(a, 0f, 1f);
            Arm(ref p, 0, hand, new Vector3(1f, -0.4f, -0.4f));
            Arm(ref p, 1, new Vector3(-0.3f, 0.1f + 0.015f * bounce, 0.02f), new Vector3(-1f, 0f, -0.45f));
            return p;
        }

        CharacterPose RoofPose(in CharacterPose b, float t)
        {
            var p = Stance(b);
            float ph = Mathf.Repeat(t, EmoteTimeline.RoofPeriod) / EmoteTimeline.RoofPeriod;
            bool right = ph < 0.5f;
            float h = (ph % 0.5f) / 0.5f;
            // Step-touch: one foot steps out, the other closes; the hips travel with the weight.
            float lead = Seg(h, 0f, 0.3f), close = Seg(h, 0.45f, 0.75f);
            float liftLead = 0.075f * Hump(h, 0f, 0.3f), liftClose = 0.06f * Hump(h, 0.45f, 0.75f);
            Vector3 fr, fl;
            if (right)
            {
                fr = new Vector3(Mathf.Lerp(0.12f, 0.34f, lead), liftLead, 0.01f);
                fl = new Vector3(Mathf.Lerp(-0.12f, 0.1f, close), liftClose, -0.01f);
            }
            else
            {
                fl = new Vector3(Mathf.Lerp(0.1f, -0.12f, lead), liftLead, -0.01f);
                fr = new Vector3(Mathf.Lerp(0.34f, 0.12f, close), liftClose, 0.01f);
            }
            float x = 0.11f * (1f - Mathf.Cos(2f * Mathf.PI * ph));
            float beatPh = Mathf.Repeat(t, 0.5f) / 0.5f;
            float bounce = 0.5f + 0.5f * Mathf.Cos(2f * Mathf.PI * beatPh);
            float dir = Mathf.Sin(2f * Mathf.PI * ph);
            p.PelvisPos += new Vector3(x, -0.05f * bounce - 0.012f, 0f);
            p.Pelvis = Quaternion.Euler(0f, 0f, -5f * dir);
            p.Torso = Quaternion.Euler(-4f + 3f * bounce, 6f * dir, 8f * dir);
            p.Head = Quaternion.Euler(-8f + 9f * bounce, -5f * dir, -4f * dir);
            Feet(ref p, fr, fl);
            Alive(ref p, t, 0.3f);
            float push = beatPh < 0.22f ? ActionTimeline.EaseOut(beatPh / 0.22f) : 1f - Seg(beatPh, 0.22f, 1f);
            for (int arm = 0; arm < 2; arm++)
            {
                float sx = arm == 0 ? 1f : -1f;
                var hand = new Vector3(0.3f * sx, 0.9f + 0.19f * push, 0.13f - 0.03f * push);
                Arm(ref p, arm, hand, new Vector3(sx, -0.35f, -0.3f));
            }
            return p;
        }

        // ------------------------------------------------------------------ sitting on the floor

        static CharacterPose FloorSquat()
        {
            var p = CharacterPose.Identity;
            p.PelvisPos = new Vector3(0f, 0.36f, -0.12f);
            p.Torso = Quaternion.Euler(32f, 0f, 0f);
            p.Head = Quaternion.Euler(-14f, 0f, 0f);
            p.SolveLeg(0, new Vector3(0.17f, 0f, 0.08f), new Vector3(0.35f, 0f, 1f));
            p.SolveLeg(1, new Vector3(-0.17f, 0f, 0.06f), new Vector3(-0.35f, 0f, 1f));
            p.SolveArm(0, new Vector3(0.24f, 0.5f, 0.3f), TorsoDir(p, 1f, -0.5f, -0.5f));
            p.SolveArm(1, new Vector3(-0.36f, 0.06f, -0.3f), TorsoDir(p, -0.6f, 0f, -1f));
            return p;
        }

        CharacterPose FloorSit(float t, float tired)
        {
            var p = CharacterPose.Identity;
            float br = Mathf.Sin(t * 2f * Mathf.PI * Mathf.Lerp(0.3f, 0.48f, tired));
            float look = Mathf.PerlinNoise(t * 0.22f, emoteSeed) - 0.5f;
            p.PelvisPos = new Vector3(0f, 0.125f, -0.22f);
            p.Pelvis = Quaternion.Euler(-10f, 0f, 0f);
            p.Torso = Quaternion.Euler(-6f + (2f + 3f * tired) * br, 0f, 0f);
            p.Head = Quaternion.Euler(10f - 6f * tired + 2f * br, 50f * look, 0f);
            p.SolveLeg(0, new Vector3(0.16f, 0.02f, 0.2f), new Vector3(0.2f, 1f, 0.6f));
            p.SolveLeg(1, new Vector3(-0.15f, 0.1f, 0.44f), new Vector3(-0.1f, 1f, 0.3f));
            p.SolveArm(1, new Vector3(-0.33f, 0.06f, -0.56f), TorsoDir(p, -0.4f, 0f, -1f));
            Vector3 knee = p.HipPos(0) + p.Pelvis * (p.HipR * BlockyCharacter.KneeRest);
            Vector3 onKnee = knee + new Vector3(-0.01f, 0.1f, 0.1f);
            // Wiping the forehead once per loop.
            float wipe = Hump(Mathf.Repeat(t, 4.4f), 0.5f, 1.7f);
            float across = Mathf.Repeat(t, 4.4f);
            Vector3 brow = HeadPoint(p, new Vector3(Mathf.Lerp(0.16f, -0.12f, Seg(across, 0.85f, 1.35f)), 0.36f, 0.3f));
            p.SolveArm(0, Vector3.Lerp(onKnee, brow, Mathf.Clamp01(wipe * 1.5f)), TorsoDir(p, 1f, -0.6f, 0f));
            return p;
        }

        static CharacterPose FloorTuck()
        {
            var p = CharacterPose.Identity;
            p.PelvisPos = new Vector3(0f, 0.14f, -0.17f);
            p.Torso = Quaternion.Euler(30f, 0f, 0f);
            p.Head = Quaternion.Euler(-12f, 0f, 0f);
            p.SolveLeg(0, new Vector3(0.16f, 0f, 0.05f), new Vector3(0.3f, 1f, 0.6f));
            p.SolveLeg(1, new Vector3(-0.16f, 0f, 0.03f), new Vector3(-0.3f, 1f, 0.6f));
            p.SolveArm(0, new Vector3(0.36f, 0.06f, -0.02f), TorsoDir(p, 0.5f, 0f, -1f));
            p.SolveArm(1, new Vector3(-0.36f, 0.06f, -0.06f), TorsoDir(p, -0.5f, 0f, -1f));
            return p;
        }

        CharacterPose SitFloorPose(in CharacterPose b, float t, float stopT)
        {
            var stand = Stance(b);
            Feet(ref stand);
            float tired = 1f - Seg(t, 2f, 9f);
            var sit = FloorSit(t, tired);
            CharacterPose p;
            if (t < 0.55f) p = CharacterPose.Lerp(stand, FloorSquat(), Seg(t, 0f, 0.55f));
            else p = CharacterPose.Lerp(FloorSquat(), sit, Seg(t, 0.55f, 1.2f));
            if (stopT < 0f) return p;
            // Getting up: legs in, push off the floor, stand.
            var from = t < 1.2f ? p : sit;
            if (stopT < 0.4f) return CharacterPose.Lerp(from, FloorTuck(), Seg(stopT, 0f, 0.4f));
            if (stopT < 0.85f) return CharacterPose.Lerp(FloorTuck(), FloorSquat(), Seg(stopT, 0.4f, 0.85f));
            return CharacterPose.Lerp(FloorSquat(), stand, Seg(stopT, 0.85f, EmoteTimeline.SitExit));
        }
    }
}
