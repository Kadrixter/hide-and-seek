using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// First-person hands of the SNAKE player. The view-model arms are drawn on top of the world, so hands placed on
    /// the real joystick / buttons (just under the screen) covered its lower part; they are kept in the lower
    /// corners of the view instead (left on the stick, tilting with the direction, right on the buttons).
    /// </summary>
    public static class ArcadeHands
    {
        public static void FirstPerson(Transform cam, Vector2 dir, out Vector3 right, out Vector3 left)
        {
            Vector3 f = cam.forward, r = cam.right, u = cam.up, o = cam.position;
            right = o + f * 0.40f + r * 0.27f - u * 0.31f;
            left = o + f * (0.40f + dir.y * 0.03f) + r * (-0.27f + dir.x * 0.03f) - u * 0.31f;
        }
    }
}
