using HideAndSeek.Characters;
using HideAndSeek.Player;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Sitting in the lobby: every time the player sits down one of <see cref="BlockyCharacterActions.SitPoseCount"/>
    /// poses is picked (random, never the previous one). The body stays still facing away from the seat; the mouse only
    /// turns the head (yaw within +-<see cref="SeatHeadYawMax"/>, pitch clamped). Pose and head yaw are owner-written
    /// network variables, the head pitch comes with the regular motion pack.
    /// </summary>
    public partial class NetPlayer
    {
        public const float SeatHeadYawMax = 75f, SeatPitchUp = 40f, SeatPitchDown = 55f;

        public readonly NetworkVariable<byte> SitPoseVar = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public readonly NetworkVariable<sbyte> SitHeadYaw = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        /// <summary>Test hook (-testseats): forces the next pose (-1 = random).</summary>
        public static int TestSitPose = -1;

        private int _lastSeatSeen = -1;
        private float _seatYaw, _headYaw, _headYawShown, _nextHeadSend;
        private HeadBob _seatBob;

        /// <summary>Current head yaw relative to the body (owner: exact, others: smoothed).</summary>
        public float SeatHeadYawNow => _headYawShown;

        private void UpdateSeatLook(float dt)
        {
            int seat = Seat.Value;
            if (IsOwner && Motor != null)
            {
                if (_seatBob == null) _seatBob = GetComponentInChildren<HeadBob>();
                if (seat >= 0 && _lastSeatSeen < 0)
                {
                    // Sat down: the root already faces away from the seat.
                    int prev = SitPoseVar.Value;
                    int pose = TestSitPose >= 0 ? TestSitPose : (prev + Random.Range(1, BlockyCharacterActions.SitPoseCount)) % BlockyCharacterActions.SitPoseCount;
                    TestSitPose = -1;
                    SitPoseVar.Value = (byte)pose;
                    var s = LobbySeat.Get(seat);
                    _seatYaw = s != null ? s.transform.eulerAngles.y : transform.eulerAngles.y;
                    _headYaw = 0f;
                }
                if (seat >= 0)
                {
                    // The motor turned the root with the mouse: move that turn into the head and put the body back.
                    float turned = Mathf.DeltaAngle(_seatYaw, transform.eulerAngles.y);
                    _headYaw = Mathf.Clamp(_headYaw + turned, -SeatHeadYawMax, SeatHeadYawMax);
                    Motor.SetLook(_seatYaw, Mathf.Clamp(Motor.LookPitch, -SeatPitchDown, SeatPitchUp));
                    sbyte sy = (sbyte)Mathf.RoundToInt(_headYaw);
                    if (SitHeadYaw.Value != sy && Time.unscaledTime >= _nextHeadSend)
                    {
                        _nextHeadSend = Time.unscaledTime + 1f / 15f;
                        SitHeadYaw.Value = sy;
                    }
                }
                else if (_lastSeatSeen >= 0)
                {
                    // Stood up: keep looking where the head was looking.
                    Motor.SetLook(transform.eulerAngles.y + _headYaw, Motor.LookPitch);
                    _headYaw = 0f;
                    if (SitHeadYaw.Value != 0) SitHeadYaw.Value = 0;
                }
                if (_seatBob != null) _seatBob.YawOffset = seat >= 0 ? _headYaw : 0f;
                _headYawShown = seat >= 0 ? _headYaw : 0f;
            }
            else
            {
                float target = seat >= 0 ? SitHeadYaw.Value : 0f;
                _headYawShown = Mathf.Lerp(_headYawShown, target, 1f - Mathf.Exp(-14f * dt));
            }
            _lastSeatSeen = seat;
            (Actions as BlockyCharacterActions)?.SetSitLook(SitPoseVar.Value, _headYawShown);
        }
    }
}
