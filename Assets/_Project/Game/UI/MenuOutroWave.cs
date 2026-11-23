using HideAndSeek.Characters;
using UnityEngine;

namespace HideAndSeek.Game.UI
{
    /// <summary>
    /// Goodbye wave of the menu character (game outro): layered on top of the procedural animator (runs after it),
    /// blended in by <see cref="Weight"/>. Right arm: wide energetic wave from the shoulder with a loose forearm;
    /// left arm: relaxed, swinging with the body; body: bounce on the toes, torso sways with the arm, head tilted to the camera.
    /// </summary>
    [DefaultExecutionOrder(150)] // after BlockyCharacterAnimator (100)
    public class MenuOutroWave : MonoBehaviour
    {
        public BlockyCharacter Character;
        /// <summary>0..1 blend of the wave pose over the animated pose.</summary>
        public float Weight;
        /// <summary>Seconds since the wave started (drives the loop).</summary>
        public float Time;

        private const float Hz = 2.6f; // waves per second
        private Vector3 _rootRest;
        private bool _rootSet;

        private void LateUpdate()
        {
            if (Character == null || Weight <= 0.0001f) return;
            float w = Weight;
            float ph = Time * Hz * Mathf.PI * 2f;
            float s = Mathf.Sin(ph), c = Mathf.Cos(ph);

            // ---- right arm: raised up and out, swinging side to side from the shoulder, forearm lagging behind
            var up = Quaternion.Euler(-12f - 6f * c, 0f, 160f + 20f * s); // hand high above the head
            var el = Quaternion.Euler(-22f - 14f * Mathf.Sin(ph - 0.9f), 0f, 0f);
            Blend(Character.ShoulderRight, up, w);
            Blend(Character.ElbowRight, el, w);

            // ---- left arm: relaxed, a little out to the side, swinging gently with the body
            var upL = Quaternion.Euler(8f + 4f * s, 0f, -(20f + 6f * Mathf.Sin(ph + 2.2f)));
            var elL = Quaternion.Euler(-26f - 8f * Mathf.Sin(ph + 1.4f), 0f, 0f);
            Blend(Character.ShoulderLeft, upL, w);
            Blend(Character.ElbowLeft, elL, w);

            // ---- body: torso leans with the waving arm, a little twist; head tilted, nodding with the rhythm
            var torso = Character.Torso.localRotation * Quaternion.Euler(-4f, 3f * s, -5f - 4f * s);
            Blend(Character.Torso, torso, w);
            var head = Character.Head.localRotation * Quaternion.Euler(-6f + 5f * Mathf.Abs(c), 0f, 13f + 4f * s);
            Blend(Character.Head, head, w);

            // ---- bounce on the toes: once per wave cycle, small (feet stay close to the floor)
            if (!_rootSet) { _rootRest = Character.transform.localPosition; _rootSet = true; }
            float hop = 0.035f * Mathf.Abs(Mathf.Sin(ph * 0.5f)) * w; // one bounce per wave
            Character.transform.localPosition = _rootRest + new Vector3(0f, hop, 0f);
            // knees give a little on every landing
            float dip = (1f - Mathf.Abs(Mathf.Sin(ph * 0.5f))) * 10f;
            Blend(Character.KneeRight, Character.KneeRight.localRotation * Quaternion.Euler(dip, 0f, 0f), w);
            Blend(Character.KneeLeft, Character.KneeLeft.localRotation * Quaternion.Euler(dip, 0f, 0f), w);
        }

        private void OnDestroy()
        {
            if (_rootSet && Character != null) Character.transform.localPosition = _rootRest;
        }

        private static void Blend(Transform t, Quaternion target, float w)
        {
            if (t != null) t.localRotation = Quaternion.Slerp(t.localRotation, target, w);
        }
    }
}
