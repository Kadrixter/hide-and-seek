using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Floss dance (loop): the hips swing to one side while both straight arms swing to the other, one arm in front of
    /// the body and the other one behind it; every swing the arms swap front and back by going round the hip (the
    /// hand path keeps clear of the body), with a short hold at each end and a twist of the shoulders.
    /// </summary>
    public partial class BlockyCharacterActions
    {
        /// <summary>Right hand path in torso space: 0 = across the front (arms left), 1 = behind on the right (arms right).</summary>
        static Vector3 FlossPath(float u)
        {
            float x = Mathf.Lerp(-0.04f, 0.42f, ActionTimeline.SmoothStep01(Mathf.Clamp01(u / 0.62f)));
            float z = Mathf.Lerp(0.34f, -0.26f, ActionTimeline.SmoothStep01(Mathf.Clamp01((u - 0.42f) / 0.58f)));
            float y = Mathf.Lerp(0.12f, 0.06f, u) + 0.03f * Mathf.Sin(Mathf.PI * u);
            return new Vector3(x, y, z);
        }

        CharacterPose FlossPose(in CharacterPose b, float t)
        {
            var p = Stance(b);
            const float sw = EmoteTimeline.FlossSwing;
            int k = Mathf.FloorToInt(t / sw);
            float f = t / sw - k;
            float s = Seg(f, 0.08f, 0.88f);
            float u = (k & 1) == 0 ? s : 1f - s;     // 0 arms left / hips right, 1 arms right / hips left
            float side = 1f - 2f * u;                 // +1 hips right, -1 hips left
            float bounce = Mathf.Abs(Mathf.Sin(Mathf.PI * f));
            p.PelvisPos += new Vector3(0.075f * side, -0.03f - 0.015f * bounce, 0f);
            p.Pelvis = Quaternion.Euler(0f, 8f * side, -7f * side);
            p.Torso = Quaternion.Euler(3f, -26f * side, 9f * side);
            p.Head = Quaternion.Euler(-4f + 5f * bounce, 14f * side, -6f * side);
            Feet(ref p, new Vector3(0.15f, 0f, 0.02f), new Vector3(-0.15f, 0f, -0.01f));
            Alive(ref p, t, 0.2f);
            Vector3 r = FlossPath(u);
            Vector3 l = FlossPath(1f - u);
            l.x = -l.x;
            Arm(ref p, 0, r, new Vector3(1f, -0.2f, -0.6f));
            Arm(ref p, 1, l, new Vector3(-1f, -0.2f, -0.6f));
            return p;
        }
    }
}
