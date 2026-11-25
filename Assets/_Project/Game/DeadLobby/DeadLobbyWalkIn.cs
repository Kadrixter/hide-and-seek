using HideAndSeek.Audio;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// First-person walk-in of a dead player: he stands in the white nothingness behind the lobby's exit door, walks up,
    /// pushes the door open (it swings into the room like for the exit easter egg), steps in, turns towards the TV and the
    /// door falls shut behind him. Pure timeline on the lobby copy's door hinge (lobby-local layout numbers from
    /// <see cref="LobbyLayout"/> / MultiplayerBuilder.LobbyDetails: door in the south wall at x = ExitDoorX, the void
    /// behind it down to z = -HalfD - 3.5).
    /// </summary>
    public sealed class DeadLobbyWalkIn
    {
        public const float Duration = 5.2f;
        private const float DoorOpenAt = 1.35f, DoorOpenEnd = 2.15f, DoorCloseAt = 4.2f, DoorCloseEnd = 4.9f;
        private const float OpenYaw = -82f;

        private static float DoorX => LobbyLayout.ExitDoorX;
        private static Vector3 Start => new(DoorX, 0f, -LobbyLayout.HalfD - 2.9f);
        private static Vector3 AtDoor => new(DoorX, 0f, -LobbyLayout.HalfD - 0.75f);
        private static Vector3 Inside => new(DoorX - 0.15f, 0f, -LobbyLayout.HalfD + 1.75f);

        private readonly Transform _hinge;
        private readonly Quaternion _closed;
        private readonly float _endYaw;
        private float _t;
        private bool _opened, _closedSnd;

        public bool Done => _t >= Duration;

        public DeadLobbyWalkIn(DeadLobbyWorld world, DeadLobbyBody body)
        {
            if (world.Door != null)
            {
                _hinge = world.Door.transform;
                _closed = _hinge.localRotation;
            }
            // End facing the TV over the fireplace (north wall), a little towards the middle of the room.
            Vector3 tv = LobbyLayout.Fireplace + new Vector3(1.5f, 0f, 0f);
            Vector3 d = tv - Inside;
            _endYaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        }

        /// <summary>Feet position (world), yaw and pitch at time <paramref name="t"/>.</summary>
        public void Pose(float t, out Vector3 feet, out float yaw, out float pitch)
        {
            float a = Smooth(Seg(t, 0.35f, DoorOpenAt));
            float b = Smooth(Seg(t, DoorOpenAt + 0.45f, 3.6f));
            Vector3 local = b > 0f ? Vector3.Lerp(AtDoor, Inside, b) : Vector3.Lerp(Start, AtDoor, a);
            // Steps: a slight bob while walking.
            float walking = (a > 0f && a < 1f) || (b > 0f && b < 1f) ? 1f : 0f;
            local.y += Mathf.Abs(Mathf.Sin(t * 6.2f)) * 0.03f * walking;
            feet = DeadLobbyWorld.ToWorld(local);
            float turn = Smooth(Seg(t, 3.0f, 4.4f));
            yaw = Mathf.LerpAngle(0f, _endYaw, turn);
            // Looks at the door handle while opening it, then up into the room.
            pitch = Mathf.Lerp(-4f, -12f, Smooth(Seg(t, 0.9f, 1.4f))) * (1f - Smooth(Seg(t, 1.9f, 2.6f)));
        }

        public void Tick(float dt, out Vector3 feet, out float yaw, out float pitch)
        {
            _t += dt;
            Pose(_t, out feet, out yaw, out pitch);
            if (_hinge == null) return;
            float open = _t < DoorCloseAt ? Smooth(Seg(_t, DoorOpenAt, DoorOpenEnd)) : 1f - Ease(Seg(_t, DoorCloseAt, DoorCloseEnd));
            _hinge.localRotation = _closed * Quaternion.Euler(0f, OpenYaw * open, 0f);
            Vector3 handle = DeadLobbyWorld.ToWorld(new Vector3(DoorX + 0.3f, 1f, -LobbyLayout.HalfD));
            if (!_opened && _t >= DoorOpenAt - 0.05f)
            {
                _opened = true;
                GameAudio.PlayAt(GameSfx.DoorOpen, handle, 0.9f);
            }
            if (!_closedSnd && _t >= DoorCloseEnd - 0.04f)
            {
                _closedSnd = true;
                GameAudio.PlayAt(GameSfx.DoorSlam, handle, 0.45f);
            }
            if (Done) _hinge.localRotation = _closed;
        }

        private static float Seg(float t, float a, float b) => Mathf.Clamp01((t - a) / (b - a));
        private static float Smooth(float x) => x * x * (3f - 2f * x);
        private static float Ease(float x) => x * x; // the door accelerates when it falls shut
    }
}
