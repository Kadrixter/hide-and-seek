using HideAndSeek.Characters;
using HideAndSeek.Player;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Carrier turning (local player): with somebody in the arms the body does not snap round with the mouse any more.
    /// The mouse turns the view (up to <see cref="BodyCarryTimeline.LookAheadMax"/> to either side, through the head
    /// bob's yaw offset), the body follows it slowly (<see cref="BodyCarryTimeline.TurnRate"/>, a little faster while
    /// walking) with eased starts and stops - the animator takes small turning steps meanwhile - and the carried body
    /// stays exactly where it is in the arms. While somebody treats the carried player the carrier stands still.
    /// Update runs after the motor applied the mouse (-50) and before the animators; LateUpdate after the seat look
    /// (which resets the yaw offset) and before the head bob (50).
    /// </summary>
    [DefaultExecutionOrder(10)]
    public class CarryTurn : MonoBehaviour
    {
        private NetPlayer _p;
        private HeadBob _bob;
        private bool _active;
        private float _lookYaw, _bodyYaw, _turnVel, _offset;

        /// <summary>View yaw ahead of the body (tests).</summary>
        public float Offset => _offset;

        private void Awake() => _p = GetComponent<NetPlayer>();

        private void Update()
        {
            if (_p == null || !_p.IsSpawned || !_p.IsOwner || _p.Motor == null) return;
            bool carrying = _p.IsCarryingBody;
            float yaw = transform.eulerAngles.y; // includes this frame's mouse turn
            if (carrying && !_active)
            {
                _active = true;
                _lookYaw = _bodyYaw = yaw;
                _turnVel = 0f;
            }
            if (!_active) return;
            float dt = Time.deltaTime;
            _lookYaw += Mathf.DeltaAngle(_bodyYaw, yaw);

            float rate = !carrying ? 300f
                : _p.CarryTurnLocked ? 0f
                : _p.Motor.HorizontalSpeed > 0.3f ? BodyCarryTimeline.TurnRateWalking : BodyCarryTimeline.TurnRate;
            float diff = Mathf.DeltaAngle(_bodyYaw, _lookYaw);
            if (Mathf.Abs(diff) > BodyCarryTimeline.LookAheadMax)
            {
                _lookYaw = _bodyYaw + Mathf.Sign(diff) * BodyCarryTimeline.LookAheadMax;
                diff = Mathf.Sign(diff) * BodyCarryTimeline.LookAheadMax;
            }
            // Eased: speeds up and slows down smoothly, never faster than the rate; small offsets are left alone.
            float want = Mathf.Abs(diff) < 4f && carrying ? 0f : Mathf.Clamp(diff * 2.2f, -rate, rate);
            _turnVel = Mathf.MoveTowards(_turnVel, want, dt * (carrying ? 160f : 900f));
            float step = _turnVel * dt;
            if (Mathf.Abs(step) > Mathf.Abs(diff)) step = diff;
            _bodyYaw += step;
            _p.Motor.SetLook(_bodyYaw, _p.Motor.LookPitch);
            _offset = Mathf.DeltaAngle(_bodyYaw, _lookYaw);
            if (!carrying && Mathf.Abs(_offset) < 0.5f)
            {
                _p.Motor.SetLook(_lookYaw, _p.Motor.LookPitch);
                _offset = 0f;
                _active = false;
            }
        }

        private void LateUpdate()
        {
            if (!_active || _p == null || !_p.IsOwner) return;
            if (_bob == null) _bob = GetComponentInChildren<HeadBob>();
            if (_bob != null) _bob.YawOffset = _offset;
        }
    }
}
