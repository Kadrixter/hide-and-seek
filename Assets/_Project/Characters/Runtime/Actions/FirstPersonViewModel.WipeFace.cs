using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// First-person side of the seeker wiping extinguisher foam off his face (same clock as the third-person body,
    /// <see cref="ExtTimeline"/>): the weapon goes down (the game sets no held item), both fists come up in front of the
    /// eyes, rub across the view in alternating strokes and flick the foam off to the sides; while coughing the right fist
    /// goes to the mouth and the view-model jerks; at the end the hands drop and the weapon comes back up.
    /// </summary>
    public partial class FirstPersonViewModel
    {
        bool wvOn;
        float wvElapsed, wvLeft, wvW, wvDur = 5f;

        /// <summary>Every frame from the game (local seeker): covered in foam, seconds since the hit, seconds left.</summary>
        public void SetFoamed(bool on, float elapsed, float left)
        {
            wvOn = on && left > 0f;
            wvElapsed = elapsed;
            wvLeft = left;
            if (wvOn) wvDur = elapsed + left;
        }

        /// <summary>0..1 how much the wiping hands are up (the game may use it to clear the screen foam).</summary>
        public float WipeHandsWeight => wvW;

        bool WipeHands(ref HeldItem show, out Vector3 rightT, out Quaternion propRot, ref Vector3 leftT, ref float wantLeft)
        {
            rightT = default;
            propRot = Quaternion.identity;
            float dt = Time.deltaTime;
            float e = wvElapsed, left = wvLeft;
            float target = wvOn ? ExtTimeline.S(e, 0.12f, ExtTimeline.WipeIn) * (1f - ExtTimeline.S(-left, -ExtTimeline.WipeOut, -0.05f)) : 0f;
            wvW = Mathf.MoveTowards(wvW, target, dt / 0.1f);
            if (wvW <= 0.001f || rig == null) return false;
            float w = ActionTimeline.SmoothStep01(wvW);

            float cough = ExtTimeline.Cough(e, wvDur, out float jerk);
            int stroke = BlockyCharacterActions.WipeStrokeAt(e, out float k);
            bool rightActive = stroke < 0 || (stroke & 1) == 0;
            float sweep = ActionTimeline.SmoothStep01(Mathf.Clamp01(k / 0.7f));
            float back = ActionTimeline.SmoothStep01(Mathf.Clamp01((k - 0.7f) / 0.3f));
            Vector3 Active(float sgn)
            {
                Vector3 a = new(sgn * 0.02f, -0.06f, 0.36f), b = new(sgn * 0.3f, -0.14f, 0.36f), c = new(sgn * 0.42f, -0.24f, 0.4f);
                Vector3 pos = sweep < 0.75f ? Vector3.Lerp(a, b, sweep / 0.75f) : Vector3.Lerp(b, c, (sweep - 0.75f) / 0.25f);
                return Vector3.Lerp(pos, new Vector3(sgn * 0.12f, -0.14f, 0.44f), back);
            }
            Vector3 Passive(float sgn) => new(sgn * 0.2f, -0.2f + 0.012f * Mathf.Sin(time * 5f), 0.4f);
            Vector3 r = rightActive ? Active(1f) : Passive(1f);
            Vector3 l = rightActive ? Passive(-1f) : Active(-1f);
            float shield = 1f - ExtTimeline.S(e, ExtTimeline.WipeIn - 0.05f, ExtTimeline.WipeIn + 0.25f);
            r = Vector3.Lerp(r, new Vector3(0.1f, -0.1f, 0.34f), shield);
            l = Vector3.Lerp(l, new Vector3(-0.12f, -0.14f, 0.36f), shield);
            r = Vector3.Lerp(r, new Vector3(0.04f, -0.3f, 0.3f + 0.03f * jerk), cough);
            l = Vector3.Lerp(l, new Vector3(-0.2f, -0.5f, 0.4f), cough);
            // From / to the rest pose below the view.
            r = Vector3.Lerp(new Vector3(0.28f, -0.85f, 0.4f), r, w);
            l = Vector3.Lerp(new Vector3(-0.28f, -0.85f, 0.4f), l, w);

            // Rig jerks with the coughs, wobbles with the head shakes.
            float shake = ExtTimeline.Shake(e, left);
            rig.localRotation = rig.localRotation * Quaternion.Euler(-6f * jerk * cough, shake * 0.25f, 0f);
            rig.localPosition += new Vector3(0f, -0.02f * jerk * cough, 0f);

            show = HeldItem.None;
            rightT = r;
            propRot = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            SolveArm(shR, elR, ShoulderR, r, new Vector3(1f, -1f, -0.6f));
            leftT = l;
            wantLeft = 1f;
            return true;
        }
    }
}
