using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>How the character moves a heavy crate.</summary>
    public enum PushMode : byte
    {
        Walk = 0,   // hands on the side, shoves and walks along with it
        Brace = 1,  // shoves from where he stands (from inside the doorway), does not walk
        Pull = 2,   // grabs the edge and drags it towards himself (from inside the doorway)
    }

    /// <summary>
    /// Pushing a heavy crate: hands onto it, bracing, three shoves with stops in between (the crate sticks on the
    /// concrete), heavy steps while it slides, then letting go and shaking out the arms. <see cref="CrateProgress"/> is
    /// the shared timing of the crate itself, so the crate, the walk and the scrape sound match the body.
    /// </summary>
    public partial class BlockyCharacterActions
    {
        /// <summary>Whole push, from the hands going on to standing straight again.</summary>
        public const float CratePushTime = 3.8f;

        float pushT = 99f, pushDuration = CratePushTime, pushDistance = 1.65f;
        PushMode pushMode;

        // Crate progress keys (time, 0..1): three shoves, each one a lurch that dies out as the crate sticks again.
        static readonly Vector2[] ShoveKeys =
        {
            new(0f, 0f), new(0.62f, 0f), new(1.02f, 0.17f), new(1.5f, 0.17f), new(1.98f, 0.46f),
            new(2.3f, 0.46f), new(3.1f, 1f), new(CratePushTime, 1f),
        };
        static readonly Vector2[] Shoves = { new(0.62f, 1.02f), new(1.5f, 1.98f), new(2.3f, 3.1f) };

        /// <summary>Crate position along its path (0 = start, 1 = end) at <paramref name="t"/> s into the push.</summary>
        public static float CrateProgress(float t)
        {
            if (t <= 0f) return 0f;
            for (int i = 1; i < ShoveKeys.Length; i++)
            {
                if (t > ShoveKeys[i].x) continue;
                Vector2 a = ShoveKeys[i - 1], b = ShoveKeys[i];
                if (b.y <= a.y) return a.y;
                float k = Mathf.Clamp01((t - a.x) / (b.x - a.x));
                // A lurch: quick start, the friction eats it up (ease out), with a stutter (stick-slip) on the way.
                float e = 1f - (1f - k) * (1f - k) * (1f - k);
                e += Mathf.Sin(k * Mathf.PI * 5f) * 0.035f * Mathf.Sin(k * Mathf.PI);
                return Mathf.Lerp(a.y, b.y, Mathf.Clamp01(e));
            }
            return 1f;
        }

        /// <summary>How hard the crate is sliding right now (0..1), for the scrape sound and the wobble.</summary>
        public static float CrateSlide(float t) => Mathf.Clamp01((CrateProgress(t + 0.02f) - CrateProgress(t - 0.02f)) / 0.04f * 0.45f);

        /// <summary>Body effort 0..1: bracing before a shove, straining during it, catching breath between.</summary>
        static float ShoveEffort(float t)
        {
            float e = 0f;
            foreach (var s in Shoves)
                e = Mathf.Max(e, Seg(t, s.x - 0.32f, s.x + 0.02f) * (1f - Seg(t, s.y - 0.05f, s.y + 0.3f)));
            return e;
        }

        public void PlayPush(float duration, PushMode mode = PushMode.Walk, float distance = 1.65f)
        {
            EnsureInit();
            pushDuration = Mathf.Max(0.8f, duration);
            pushMode = mode;
            pushDistance = distance;
            pushT = 0f;
        }

        public bool Pushing => pushT < pushDuration;

        CharacterPose PushPose(in CharacterPose current, float t)
        {
            float reach = Seg(t, 0f, 0.4f);
            float release = Seg(t, pushDuration - 0.65f, pushDuration - 0.2f);
            float on = reach * (1f - release);
            float effort = ShoveEffort(t) * on;
            float slide = CrateSlide(t) * on;
            float shake = Mathf.Sin(t * 37f) * 0.6f + Mathf.Sin(t * 23f) * 0.4f;   // strain tremble
            bool pull = pushMode == PushMode.Pull;

            var p = CharacterPose.Identity;
            p.PropR = current.PropR;

            // Distance walked so far drives the steps (feet stay planted while the crate sticks).
            float walked = pushMode == PushMode.Walk ? CrateProgress(t) * pushDistance : 0f;
            const float stride = 0.3f, amp = 0.21f;
            float phase = walked / stride;

            if (!pull)
            {
                float lean = on * (0.5f + 0.4f * effort);
                float crouch = on * (0.03f + 0.07f * effort);
                float bob = 0.012f * Mathf.Abs(Mathf.Sin(phase * Mathf.PI));
                p.PelvisPos = new Vector3(0f, 0.62f - crouch + bob, -0.07f * lean - 0.04f * effort);
                p.Pelvis = Quaternion.Euler(8f * lean, 0f, 3f * Mathf.Sin(phase * Mathf.PI));
                p.Torso = Quaternion.Euler(28f * lean + 0.8f * shake * effort, 0f, -2.5f * Mathf.Sin(phase * Mathf.PI) + 1.5f * shake * effort);
                p.Head = Quaternion.Euler(-30f * lean + 6f * effort, 3f * shake * effort, 0f);
            }
            else
            {
                // Pulling: sits back into it, arms long, yanks with every shove.
                float lean = on * (0.6f + 0.4f * effort);
                p.PelvisPos = new Vector3(0f, 0.6f - 0.08f * lean, -0.1f * lean - 0.12f * effort);
                p.Pelvis = Quaternion.Euler(14f * lean, 0f, 0f);
                p.Torso = Quaternion.Euler(10f * lean - 16f * effort + 0.8f * shake * effort, 0f, 1.5f * shake * effort);
                p.Head = Quaternion.Euler(-6f * lean, 3f * shake * effort, 0f);
            }

            // Feet.
            Vector3 fr, fl;
            if (pushMode == PushMode.Walk)
            {
                fr = StepFoot(0.13f, 0.02f, phase, 0f, amp);
                fl = StepFoot(-0.13f, -0.2f, phase, 0.5f, amp);
                float back = 0.16f * effort;
                fr.z -= 0.14f * on; fl.z -= 0.14f * on + back;
            }
            else if (pushMode == PushMode.Brace)
            {
                fr = new Vector3(0.14f, 0f, 0.06f - 0.06f * on);
                fl = new Vector3(-0.14f, 0f, -0.12f - 0.24f * on - 0.06f * effort);
            }
            else
            {
                fr = new Vector3(0.14f, 0f, 0.12f + 0.1f * on);
                fl = new Vector3(-0.14f, 0f, -0.12f - 0.1f * on);
            }
            p.SolveLeg(0, fr, new Vector3(0.1f, 0f, 1f));
            p.SolveLeg(1, fl, new Vector3(-0.1f, 0f, 1f));

            // Hands: flat on the crate side at chest height (a jolt at the start of each shove), or gripping the edge.
            float jolt = 0.035f * effort * Mathf.Clamp01(1f - slide * 1.5f);
            Vector3 hr, hl;
            if (!pull)
            {
                hr = new Vector3(0.21f, 1.02f - 0.05f * effort, 0.56f + jolt);
                hl = new Vector3(-0.21f, 1.05f - 0.05f * effort, 0.56f + jolt);
            }
            else
            {
                hr = new Vector3(0.14f, 0.96f, 0.74f - 0.07f * effort);
                hl = new Vector3(-0.16f, 0.98f, 0.72f - 0.07f * effort);
            }
            // After letting go: shake out the arms once.
            float shakeOut = Seg(t, pushDuration - 0.55f, pushDuration - 0.35f) * (1f - Seg(t, pushDuration - 0.25f, pushDuration));
            Vector3 relaxR = current.HandPos(0) + new Vector3(0f, 0.03f * Mathf.Sin(t * 40f) * shakeOut, 0f);
            Vector3 relaxL = current.HandPos(1) + new Vector3(0f, 0.03f * Mathf.Sin(t * 40f + 1f) * shakeOut, 0f);
            p.SolveArm(0, Vector3.Lerp(relaxR, hr, on), TorsoDir(p, 1f, -1f, -0.3f));
            p.SolveArm(1, Vector3.Lerp(relaxL, hl, on), TorsoDir(p, -1f, -1f, -0.3f));
            return p;
        }

        /// <summary>Planted foot that slides back (in body space) as the body walks, then swings forward.</summary>
        static Vector3 StepFoot(float x, float z0, float phase, float offset, float amp)
        {
            float c = Mathf.Repeat(phase * 0.5f + offset, 1f);
            float z, lift = 0f;
            if (c < 0.7f) z = amp - c / 0.7f * 2f * amp;
            else
            {
                float s = (c - 0.7f) / 0.3f;
                z = -amp + s * 2f * amp;
                lift = Mathf.Sin(s * Mathf.PI) * 0.07f;
            }
            return new Vector3(x, lift, z0 + z);
        }

        void ApplyPushLayer(ref CharacterPose p, float dt)
        {
            ApplyCrateSawLayer(ref p, dt);
            if (pushT >= pushDuration) return;
            pushT += dt;
            float w = Seg(pushT, 0f, 0.2f) * (1f - Seg(pushT, pushDuration - 0.25f, pushDuration));
            var pp = PushPose(p, pushT);
            var keepProp = p.PropR;
            p = CharacterPose.Lerp(p, pp, w);
            p.PropR = keepProp;
        }
    }
}
