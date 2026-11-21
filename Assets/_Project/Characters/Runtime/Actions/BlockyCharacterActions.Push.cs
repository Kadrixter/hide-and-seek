using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>Pushing a heavy crate: hands onto it, lean in, short heavy steps while it slides, let go.</summary>
    public partial class BlockyCharacterActions
    {
        float pushT = 99f, pushDuration = 1.9f;

        public void PlayPush(float duration)
        {
            EnsureInit();
            pushDuration = Mathf.Max(0.8f, duration);
            pushT = 0f;
        }

        public bool Pushing => pushT < pushDuration;

        CharacterPose PushPose(in CharacterPose current, float t)
        {
            float reach = Seg(t, 0f, 0.3f);
            float lean = Seg(t, 0.2f, 0.45f) * (1f - Seg(t, pushDuration - 0.3f, pushDuration));
            float walking = Seg(t, 0.35f, 0.5f) * (1f - Seg(t, 1.5f, 1.7f));
            float ph = (t - 0.35f) * 5.2f; // heavy, slow steps

            var p = CharacterPose.Identity;
            p.PropR = current.PropR;
            p.PelvisPos = new Vector3(0f, 0.62f - 0.06f * lean + 0.015f * Mathf.Abs(Mathf.Sin(ph * Mathf.PI)) * walking, -0.12f * lean);
            p.Pelvis = Quaternion.Euler(6f * lean, 0f, 2.5f * Mathf.Sin(ph * Mathf.PI) * walking);
            p.Torso = Quaternion.Euler(30f * lean, 0f, -2f * Mathf.Sin(ph * Mathf.PI) * walking);
            p.Head = Quaternion.Euler(-24f * lean, 0f, 0f);

            // Feet: staggered stance, alternate small lifts while walking.
            float sr = Mathf.Sin(ph * Mathf.PI), sl = Mathf.Sin((ph + 1f) * Mathf.PI);
            Vector3 fr = new Vector3(0.13f, 0.06f * Mathf.Max(0f, sr) * walking, 0.08f + 0.12f * sr * walking - 0.18f * lean);
            Vector3 fl = new Vector3(-0.13f, 0.06f * Mathf.Max(0f, sl) * walking, -0.12f + 0.12f * sl * walking - 0.18f * lean);
            p.SolveLeg(0, fr, new Vector3(0.1f, 0f, 1f));
            p.SolveLeg(1, fl, new Vector3(-0.1f, 0f, 1f));

            // Hands flat on the crate side (~0.55 m ahead, chest height), a little shove with every step.
            float shove = 0.02f * Mathf.Sin(ph * Mathf.PI * 2f) * walking;
            Vector3 hr = new Vector3(0.2f, 1.02f, 0.58f + shove), hl = new Vector3(-0.2f, 1.02f, 0.58f + shove);
            p.SolveArm(0, Vector3.Lerp(current.HandPos(0), hr, reach), TorsoDir(p, 1f, -1f, -0.3f));
            p.SolveArm(1, Vector3.Lerp(current.HandPos(1), hl, reach), TorsoDir(p, -1f, -1f, -0.3f));
            return p;
        }

        void ApplyPushLayer(ref CharacterPose p, float dt)
        {
            if (pushT >= pushDuration) return;
            pushT += dt;
            float w = Seg(pushT, 0f, 0.15f) * (1f - Seg(pushT, pushDuration - 0.2f, pushDuration));
            var pp = PushPose(p, pushT);
            var keepProp = p.PropR;
            p = CharacterPose.Lerp(p, pp, w);
            p.PropR = keepProp;
        }
    }
}
