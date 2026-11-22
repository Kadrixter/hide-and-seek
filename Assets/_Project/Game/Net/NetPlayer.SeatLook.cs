using HideAndSeek.Characters;
using HideAndSeek.Player;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Sitting in the lobby: every time the player sits down one of the first <see cref="SeatPoses.RandomCount"/>
    /// poses is picked (random, never the previous one); the pin-up poses come only from the pose wheel. The body
    /// stays still facing away from the seat; the mouse only turns the head within the pose's limits
    /// (<see cref="SeatPoses.Limits"/>, around the pose's neutral look: lying poses look along the sofa). Pose and head yaw are owner-written
    /// network variables, the head pitch comes with the regular motion pack. The own body is visible while seated
    /// (<see cref="SeatedView"/>: camera in the model's eyes, head hidden).
    /// </summary>
    public partial class NetPlayer
    {
        public readonly NetworkVariable<byte> SitPoseVar = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public readonly NetworkVariable<sbyte> SitHeadYaw = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        /// <summary>Test hook (-testseats): forces the next pose (-1 = random).</summary>
        public static int TestSitPose = -1;

        private int _lastSeatSeen = -1, _lastPoseSeen = -1;
        private float _seatYaw, _headYaw, _headYawShown, _nextHeadSend;
        private HeadBob _seatBob;
        private SeatedView _seatView;
        private Vector3 _sitFromPos, _sitCamPos;
        private Quaternion _sitFromRot = Quaternion.identity, _sitCamRot = Quaternion.identity;
        private int _sitFromFrame = -10;

        /// <summary>Current head yaw relative to the body (owner: exact, others: smoothed).</summary>
        public float SeatHeadYawNow => _headYawShown;

        /// <summary>Local player: the own body is drawn (seated in the lobby), see <see cref="SeatedView"/>.</summary>
        public bool SeatBodyShown { get; private set; }

        /// <summary>Outer look limits of the current seated pose (the pitch range also shrinks with the yaw, see <see cref="SeatPoses.ClampLook"/>).</summary>
        public SeatPoses.LookLimits SeatLookLimits
        {
            get
            {
                var s = LobbySeat.Get(Seat.Value);
                return SeatPoses.Limits(SitPoseVar.Value, s != null ? s.Shape : null);
            }
        }

        /// <summary>
        /// Owner, right before the root is moved onto a seat: remembers where the body stood and where the camera was
        /// looking, so the body walks into the seat and the view turns round smoothly instead of jumping.
        /// </summary>
        public void PrepareSit()
        {
            if (Character != null)
            {
                _sitFromPos = Character.transform.position;
                _sitFromRot = Character.transform.rotation;
                _sitFromFrame = Time.frameCount;
            }
            if (Camera != null)
            {
                _sitCamPos = Camera.transform.position;
                _sitCamRot = Camera.transform.rotation;
            }
        }

        internal void SetSeatBodyShown(bool on)
        {
            if (SeatBodyShown == on) return;
            SeatBodyShown = on;
            ApplyOwnBodyMode();
        }

        private void UpdateSeatLook(float dt)
        {
            int seat = Seat.Value;
            if (IsOwner && Motor != null)
            {
                if (_seatBob == null) _seatBob = GetComponentInChildren<HeadBob>();
                if (seat >= 0 && _lastSeatSeen < 0)
                {
                    // Sat down: the root already faces away from the seat.
                    int prev = SitPoseVar.Value % SeatPoses.RandomCount;
                    int pose = TestSitPose >= 0 ? TestSitPose : (prev + Random.Range(1, SeatPoses.RandomCount)) % SeatPoses.RandomCount;
                    TestSitPose = -1;
                    var s = LobbySeat.Get(seat);
                    // A pose that needs the whole sofa only when the other seat is free (tests may force any pose).
                    if (s != null && !SitPoseCatalog.Allowed(s.Shape, (byte)pose, SeatNeighbourTaken(s))) pose = 0;
                    SitPoseVar.Value = (byte)pose;
                    _seatYaw = s != null ? s.transform.eulerAngles.y : transform.eulerAngles.y;
                    // Lying poses look along the sofa: start at the pose's neutral look.
                    _headYaw = SeatPoses.Limits(pose, s != null ? s.Shape : null).Center;
                    _lastPoseSeen = pose;
                }
                else if (seat >= 0 && SitPoseVar.Value != _lastPoseSeen)
                {
                    // Pose changed while seated (pose wheel): keep looking the same way relative to the pose's neutral
                    // look, the view turns smoothly while the body moves.
                    var s = LobbySeat.Get(seat);
                    var shape = s != null ? s.Shape : null;
                    float from = _lastPoseSeen >= 0 ? SeatPoses.Limits(_lastPoseSeen, shape).Center : 0f;
                    _headYaw += SeatPoses.Limits(SitPoseVar.Value, shape).Center - from;
                    if (_seatBob != null && Camera != null)
                        _seatBob.BeginTurnFrom(Camera.transform.rotation, SeatPoses.SwitchTime(System.Math.Max(_lastPoseSeen, 0), SitPoseVar.Value));
                    _lastPoseSeen = SitPoseVar.Value;
                }
                if (seat >= 0) GiveUpLyingIfCrowded(seat);
                if (seat >= 0)
                {
                    // The motor turned the root with the mouse: move that turn into the head and put the body back.
                    // The look stays in the pose's range (the eyes never come near the own shoulders / chest).
                    var s = LobbySeat.Get(seat);
                    float yaw = _headYaw + Mathf.DeltaAngle(_seatYaw, transform.eulerAngles.y), pitch = Motor.LookPitch;
                    SeatPoses.ClampLook(SitPoseVar.Value, s != null ? s.Shape : null, ref yaw, ref pitch);
                    _headYaw = yaw;
                    Motor.SetLook(_seatYaw, pitch);
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
