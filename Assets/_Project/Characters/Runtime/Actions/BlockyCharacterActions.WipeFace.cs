using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Seeker hit by the extinguisher (third person): the weapon is put away by the game (held item = none for the
    /// effect), the head snaps back, both hands fly to the face and wipe the foam off in alternating strokes (one hand
    /// sweeps across the eyes and flicks the foam off to the side while the other shields the face), with coughing fits
    /// (doubled over, a fist at the mouth), head shakes and a hunched, unsteady stance. In the last
    /// <see cref="ExtTimeline.WipeOut"/> s the hands come down, the head shakes once more and the weapon is drawn again.
    /// Driven every frame by the game through <see cref="SetFoamed"/>.
    /// </summary>
    public partial class BlockyCharacterActions
    {
        bool wfOn;
        float wfElapsed, wfLeft, wfW, wfDur = 5f;

        /// <summary>Every frame from the game: <paramref name="on"/> = covered in foam, seconds since the hit and seconds left.</summary>
        public void SetFoamed(bool on, float elapsed, float left)
        {
            wfOn = on && left > 0f;
            wfElapsed = elapsed;
            wfLeft = left;
            if (wfOn) wfDur = elapsed + left;
        }

        /// <summary>0..1 how much the wiping layer owns the arms.</summary>
        public float WipeWeight => wfW;

        void ApplyWipeFace(ref CharacterPose p, float dt, bool standing)
        {
            bool can = wfOn && standing && VisualLifeState == LifeState.Alive && !pathActive && action == ActionKind.None;
            float e = wfElapsed, left = wfLeft;
            // In: after the weapon went down; out: over the last WipeOut seconds.
            float target = can ? ExtTimeline.S(e, 0.08f, ExtTimeline.WipeIn) * (1f - ExtTimeline.S(-left, -ExtTimeline.WipeOut, -0.05f)) : 0f;
            wfW = Mathf.MoveTowards(wfW, target, dt / 0.12f);
            if (wfW <= 0.001f) return;
            float w = ActionTimeline.SmoothStep01(wfW);

            var q = p;
            float cough = ExtTimeline.Cough(e, wfDur, out float jerk);
            float shake = ExtTimeline.Shake(e, left);
            float recoil = ExtTimeline.Bump(e, 0f, 0.55f, 0.06f);
            // Hunched and unsteady: torso forward, swaying; head back on the hit, down while coughing, shaking.
            float sway = Mathf.Sin(time * 2.3f) * 4f;
            q.Torso = q.Torso * Quaternion.Euler(9f - 16f * recoil + 22f * cough + 8f * jerk, sway, Mathf.Sin(time * 1.7f) * 3f);
            q.Head = Quaternion.Euler(-24f * recoil + 18f * cough + 6f * jerk, shake + sway * 0.6f, Mathf.Sin(time * 3.1f) * 5f * (1f - cough));
            q.PelvisPos += new Vector3(0f, -0.04f * (cough + recoil * 0.5f), -0.03f * recoil);

            // Face frame (root space): centre a little in front of the face plane.
            Quaternion head = q.TorsoRoot * q.Head;
            Vector3 neck = q.TorsoPoint(BlockyCharacter.NeckRest);
            Vector3 F(float x, float y, float z) => neck + head * new Vector3(x, 4f * Px + y, 4f * Px + 0.1f + z);

            // Alternating strokes: the active hand sweeps from the nose across the eye and off to the side, the other covers the cheek.
            float st = Mathf.Max(0f, e - ExtTimeline.WipeIn) / ExtTimeline.WipeStroke;
            int stroke = Mathf.FloorToInt(st);
            float k = st - stroke;
            bool rightActive = (stroke & 1) == 0;
            float sweep = ActionTimeline.SmoothStep01(Mathf.Clamp01(k / 0.7f));
            float back = Mathf.Clamp01((k - 0.7f) / 0.3f);
            Vector3 Active(float sgn)
            {
                // inner corner of the eye -> outer edge of the head and a flick outwards, then back in an arc away from the face
                Vector3 a = F(sgn * 0.02f, 0.06f, -0.02f), b = F(sgn * 0.26f, -0.05f, 0.02f), c = F(sgn * 0.34f, -0.12f, 0.12f);
                Vector3 pos = sweep < 0.75f ? Vector3.Lerp(a, b, sweep / 0.75f) : Vector3.Lerp(b, c, (sweep - 0.75f) / 0.25f);
                return Vector3.Lerp(pos, F(sgn * 0.08f, 0.02f, 0.12f), ActionTimeline.SmoothStep01(back));
            }
            Vector3 Passive(float sgn) => F(sgn * 0.14f, -0.06f + 0.015f * Mathf.Sin(time * 5f), 0.04f);
            Vector3 r = rightActive ? Active(1f) : Passive(1f);
            Vector3 l = rightActive ? Passive(-1f) : Active(-1f);

            // Hit: both hands thrown up in front of the face.
            Vector3 shieldR = F(0.1f, 0.04f, 0.16f), shieldL = F(-0.1f, 0.0f, 0.18f);
            float shield = 1f - ExtTimeline.S(e, ExtTimeline.WipeIn - 0.05f, ExtTimeline.WipeIn + 0.25f);
            r = Vector3.Lerp(r, shieldR, shield);
            l = Vector3.Lerp(l, shieldL, shield);
            // Coughing: right fist to the mouth, left hand on the chest.
            r = Vector3.Lerp(r, F(0.02f, -0.18f, 0.06f) + head * Vector3.forward * 0.02f * jerk, cough);
            l = Vector3.Lerp(l, q.TorsoPoint(new Vector3(-0.05f, 0.42f, 0.2f)), cough * 0.8f);

            q.SolveArm(0, r, TorsoDir(q, 1f, -0.6f, -0.2f));
            q.SolveArm(1, l, TorsoDir(q, -1f, -0.6f, -0.2f));
            CharacterPose.LerpArm(ref p, q, 0, w);
            CharacterPose.LerpArm(ref p, q, 1, w);
            p.Torso = Quaternion.Slerp(p.Torso, q.Torso, w);
            p.Head = Quaternion.Slerp(p.Head, q.Head, w);
            p.PelvisPos = Vector3.Lerp(p.PelvisPos, q.PelvisPos, w);
        }

        /// <summary>Wipe stroke progress for the game (foam falling off the face): index of the stroke and 0..1 inside it.</summary>
        public static int WipeStrokeAt(float elapsed, out float k)
        {
            float st = Mathf.Max(0f, elapsed - ExtTimeline.WipeIn) / ExtTimeline.WipeStroke;
            int i = Mathf.FloorToInt(st);
            k = st - i;
            return elapsed < ExtTimeline.WipeIn ? -1 : i;
        }
    }
}
