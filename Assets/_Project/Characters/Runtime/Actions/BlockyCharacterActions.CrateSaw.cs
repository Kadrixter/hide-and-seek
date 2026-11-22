using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Seeker cutting through the crate in front of a security room with the chainsaw: raises it, plunges the bar into
    /// the top of the crate, drags it down through the planks against the resistance (stalls, shaking), shoves through
    /// when the crate gives way (<see cref="CrateSawBreakTime"/>) and pulls the saw back.
    /// </summary>
    public partial class BlockyCharacterActions
    {
        public const float CrateSawTime = 3.4f;
        /// <summary>Moment the crate falls apart.</summary>
        public const float CrateSawBreakTime = 2.6f;
        /// <summary>Bar in the wood (chips fly) between these times.</summary>
        public const float CrateSawBiteStart = 0.55f, CrateSawBiteEnd = 2.65f;

        float crateSawT = 99f;

        public void PlayCrateSaw()
        {
            EnsureInit();
            crateSawT = 0f;
        }

        public bool CrateSawing => crateSawT < CrateSawTime;

        /// <summary>Cut depth 0..1 along the diagonal (stalls where the bar hits a batten).</summary>
        public static float CrateCutProgress(float t)
        {
            float k = Mathf.Clamp01((t - CrateSawBiteStart) / (CrateSawBreakTime - 0.15f - CrateSawBiteStart));
            return Mathf.Clamp01(k - 0.06f * Mathf.Sin(k * Mathf.PI * 4f));
        }

        void ApplyCrateSawLayer(ref CharacterPose p, float dt)
        {
            if (crateSawT >= CrateSawTime) return;
            crateSawT += dt;
            float t = crateSawT;
            float w = Seg(t, 0f, 0.2f) * (1f - Seg(t, CrateSawTime - 0.3f, CrateSawTime));
            if (w <= 0.001f) return;

            float raise = Seg(t, 0f, 0.35f);
            float plunge = Seg(t, 0.35f, CrateSawBiteStart);
            float cut = CrateCutProgress(t);
            float through = Seg(t, CrateSawBreakTime - 0.12f, CrateSawBreakTime + 0.05f) * (1f - Seg(t, CrateSawBreakTime + 0.15f, CrateSawBreakTime + 0.6f));
            float back = Seg(t, CrateSawBreakTime + 0.15f, CrateSawBreakTime + 0.6f);
            float biting = Seg(t, CrateSawBiteStart - 0.05f, CrateSawBiteStart + 0.05f) * (1f - Seg(t, CrateSawBreakTime - 0.05f, CrateSawBreakTime + 0.1f));
            Vector3 buzz = new Vector3(Mathf.Sin(t * 91f), Mathf.Sin(t * 73f), Mathf.Sin(t * 57f) * 0.5f) * (0.006f + 0.016f * biting);
            float strain = biting * (0.6f + 0.4f * Mathf.Abs(Mathf.Sin(t * 5.5f)));

            var q = p;
            // Body: lean into the cut, knees bent, weight forward on the shove through, upright again at the end.
            float lean = raise * (0.4f + 0.6f * plunge) * (1f - back * 0.8f);
            q.PelvisPos = new Vector3(0f, 0.62f - 0.06f * lean - 0.04f * cut * biting, -0.06f * lean + 0.08f * through);
            q.Pelvis = Quaternion.Euler(6f * lean, -8f * lean, 0f);
            q.Torso = Quaternion.Euler(18f * lean + 10f * cut * biting + 8f * through + strain * Mathf.Sin(t * 29f), 6f * lean, strain * Mathf.Sin(t * 19f));
            q.SolveLeg(0, new Vector3(0.16f, 0f, -0.2f * lean), new Vector3(0.1f, 0f, 1f));
            q.SolveLeg(1, new Vector3(-0.15f, 0f, 0.18f * lean), new Vector3(-0.1f, 0f, 1f));

            // Saw: held high, plunged into the top of the crate, dragged diagonally down, thrust through, pulled back.
            Vector3 hold = new Vector3(0.17f, 0.95f, 0.3f);
            Vector3 high = new Vector3(0.2f, 1.22f, 0.32f);
            Vector3 inTop = new Vector3(0.26f, 1.2f, 0.5f);
            Vector3 inLow = new Vector3(-0.08f, 0.62f, 0.52f);
            Vector3 thrust = new Vector3(-0.05f, 0.65f, 0.66f);
            Vector3 fist = Vector3.Lerp(hold, high, raise);
            fist = Vector3.Lerp(fist, inTop, plunge);
            fist = Vector3.Lerp(fist, inLow, cut);
            fist = Vector3.Lerp(fist, thrust, through);
            fist = Vector3.Lerp(fist, hold, back);
            fist += buzz;
            float pitch = Mathf.Lerp(-10f, 28f, plunge) + 10f * cut - 22f * back;
            Quaternion rot = Quaternion.Euler(pitch + Mathf.Sin(t * 83f) * 1.5f * biting, -8f + 10f * cut, -20f * cut * (1f - back));
            SolveSawHands(ref q, fist, rot);
            // Head: eyes on the cut (a bit ahead of the fists, where the bar bites), never above the horizon.
            q.Head = CrateSawHead(q, fist + new Vector3(-0.02f, -0.08f, 0.3f), lean);

            var keepPropBefore = p.PropR;
            p = CharacterPose.Lerp(p, q, w);
            if (w >= 0.999f) p.PropR = q.PropR;
            else p.PropR = Quaternion.Slerp(keepPropBefore, q.PropR, w);
        }

        /// <summary>Local head rotation that looks at <paramref name="target"/> (root space), pitched down 15..55 degrees.</summary>
        static Quaternion CrateSawHead(in CharacterPose q, Vector3 target, float lean)
        {
            Vector3 eye = q.TorsoPoint(BlockyCharacter.NeckRest + new Vector3(0f, 0.2f, 0.08f));
            Vector3 d = target - eye;
            float flat = new Vector2(d.x, d.z).magnitude;
            float down = Mathf.Clamp(Mathf.Atan2(-d.y, Mathf.Max(0.05f, flat)) * Mathf.Rad2Deg, 15f, 55f);
            float yaw = Mathf.Clamp(Mathf.Atan2(d.x, Mathf.Max(0.05f, d.z)) * Mathf.Rad2Deg, -25f, 25f);
            Quaternion world = Quaternion.Euler(0f, yaw * lean, 0f) * Quaternion.Euler(Mathf.Lerp(8f, down, lean), 0f, 0f);
            return Quaternion.Inverse(q.TorsoRoot) * world;
        }
    }
}
