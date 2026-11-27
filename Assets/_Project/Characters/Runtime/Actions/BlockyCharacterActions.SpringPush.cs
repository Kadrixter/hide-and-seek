using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// 2026-11-27: pushing a sofa spring back into the cushion (it popped out and threw the sitter off). The character
    /// bends over the seat, both hands land on the top of the spring, presses it down in three shoves (the last one the
    /// hardest), pats the cushion flat and straightens up. Played by the game on every peer
    /// (<see cref="PlaySpringPush"/>); <see cref="SpringDepth"/> gives the spring's height for the effect, so the hands
    /// and the coil move together.
    /// </summary>
    public partial class BlockyCharacterActions
    {
        float spT = 99f, spDur = SpringPushDuration;
        Vector3 spLocal;

        public const float SpringPushDuration = 2.6f;

        /// <summary>Hands go to <paramref name="springTopWorld"/> (the top of the popped spring) and push it in.</summary>
        public void PlaySpringPush(Vector3 springTopWorld)
        {
            EnsureInit();
            spT = 0f;
            spDur = SpringPushDuration;
            spLocal = transform.InverseTransformPoint(springTopWorld);
        }

        public bool PushingSpring => spT < spDur;

        /// <summary>0..1 how far out the spring still stands at time <paramref name="t"/> of the push (1 = fully out, 0 = in).</summary>
        public static float SpringDepth(float t)
        {
            float u = Mathf.Clamp01(t / SpringPushDuration);
            if (u < 0.22f) return 1f;
            if (u > 0.8f) return 0f;
            // three shoves: each takes a bit and the coil springs half back, the last one stays in
            float k = (u - 0.22f) / 0.58f * 3f;
            int shove = Mathf.Min(2, Mathf.FloorToInt(k));
            float f = k - shove;
            float before = 1f - shove * 0.33f, after = shove == 2 ? 0f : 1f - (shove + 1) * 0.33f;
            float press = Mathf.Sin(Mathf.Clamp01(f / 0.55f) * Mathf.PI * 0.5f);
            float back = f > 0.55f && shove < 2 ? Mathf.Sin((f - 0.55f) / 0.45f * Mathf.PI) * 0.12f : 0f;
            return Mathf.Clamp01(Mathf.Lerp(before, after - 0.1f, press) + back);
        }

        void ApplySpringPush(ref CharacterPose p, float dt, bool standing)
        {
            if (spT >= spDur) return;
            spT += dt;
            if (!standing) { spT = spDur; return; }
            float u = spT / spDur;
            float w = Smooth(u / 0.14f) * (1f - Smooth((u - 0.86f) / 0.14f));
            if (w <= 0.001f) return;
            var ap = p;
            // bend over the seat: the lower the target, the deeper (never more than 50 degrees)
            float bend = Mathf.Clamp(Mathf.Atan2(1.3f - spLocal.y, Mathf.Max(0.3f, spLocal.z)) * Mathf.Rad2Deg * 0.75f, 20f, 50f);
            float depth = SpringDepth(spT);
            float shove = 1f - depth;
            ap.Torso = p.Torso * Quaternion.Euler(bend + 6f * shove, 0f, 0f);
            ap.PelvisPos += new Vector3(0f, -0.05f * w, -0.06f * w);
            // hands on the spring top (it goes down with them), a little apart; the last part pats the cushion
            Vector3 top = spLocal + Vector3.up * (0.2f * depth); // the coil stands 0.2 m out (SofaSpringFx.Height)
            float pat = u > 0.8f ? Mathf.Abs(Mathf.Sin((u - 0.8f) * 40f)) * 0.03f : 0f;
            Vector3 r = top + new Vector3(0.05f, 0.02f + pat, 0f), l = top + new Vector3(-0.05f, 0.03f + (u > 0.8f ? 0.03f - pat : 0f), -0.01f);
            // never further than the arms reach: pull the hands towards the shoulders
            for (int arm = 0; arm < 2; arm++)
            {
                Vector3 sh = ap.ShoulderPos(arm);
                Vector3 h = arm == 0 ? r : l;
                Vector3 d = h - sh;
                if (d.magnitude > 0.6f) h = sh + d.normalized * 0.6f;
                if (arm == 0) r = h; else l = h;
            }
            ap.SolveArm(0, r, TorsoDir(ap, 1f, -0.5f, -0.4f));
            ap.SolveArm(1, l, TorsoDir(ap, -1f, -0.5f, -0.4f));
            CharacterPose.LerpArm(ref p, ap, 0, w);
            CharacterPose.LerpArm(ref p, ap, 1, w);
            p.Torso = Quaternion.Slerp(p.Torso, ap.Torso, w);
            p.PelvisPos = Vector3.Lerp(p.PelvisPos, ap.PelvisPos, w);
            p.Head = Quaternion.Slerp(p.Head, Quaternion.Euler(28f, 0f, 0f), w * 0.8f);
        }
    }
}
