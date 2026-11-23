using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Where the two SNAKE cabinets stand (from <see cref="LobbyLayout"/>) and the points on them in world space.
    /// Cabinet local space: origin on the floor at the cabinet centre, the front (screen, controls) faces local -Z.
    /// Offsets match the builder (MultiplayerBuilder.LobbyArcade.cs).
    /// </summary>
    public static class ArcadeCabs
    {
        public const int Count = 2;

        /// <summary>Player spot in front of the controls.</summary>
        public static readonly Vector3 StandLocal = new(0f, 0f, -0.9f);
        public static readonly Vector3 StickBallLocal = new(-0.17f, 1.124f, -0.446f);
        public static readonly Vector3 ButtonsLocal = new(0.075f, 1.049f, -0.442f);
        public static readonly Vector3 ScreenLocal = new(0f, 1.35f, -0.36f);

        public static Vector3 Origin(int cab) => LobbyLayout.Arcades[Mathf.Clamp(cab, 0, Count - 1)].pos;
        public static float Yaw(int cab) => LobbyLayout.Arcades[Mathf.Clamp(cab, 0, Count - 1)].yaw;
        public static Quaternion Rotation(int cab) => Quaternion.Euler(0f, Yaw(cab), 0f);
        public static Vector3 World(int cab, Vector3 local) => Origin(cab) + Rotation(cab) * local;

        public static Vector3 Stand(int cab) => World(cab, StandLocal);
        public static Vector3 StickBall(int cab) => World(cab, StickBallLocal);
        public static Vector3 Buttons(int cab) => World(cab, ButtonsLocal);
        public static Vector3 Screen(int cab) => World(cab, ScreenLocal);

        /// <summary>Joystick direction (screen up / right / down / left) as a world-space offset of the stick ball.</summary>
        public static Vector3 Tilt(int cab, Vector2 dir, float amount) => Rotation(cab) * new Vector3(dir.x, 0f, dir.y) * amount;
    }
}
