using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>Ceiling fan: the rotor spins up / coasts down with the fan switch (<see cref="LobbyPropKind.FanSwitch"/>), a faint wobble.</summary>
    public class LobbyFanMotor : MonoBehaviour
    {
        public Transform rotor;
        public float maxSpeed = 150f;
        private float _speed = -1f, _angle;

        private void Update()
        {
            if (rotor == null) return;
            float want = LobbyPropDriver.On(LobbyPropKind.FanSwitch) ? maxSpeed : 0f;
            if (_speed < 0f) _speed = want;
            // spin-up ~3 s, coast-down ~7 s
            _speed = Mathf.MoveTowards(_speed, want, Time.deltaTime * (want > _speed ? 50f : 22f));
            _angle = Mathf.Repeat(_angle + _speed * Time.deltaTime, 360f);
            float wob = _speed / maxSpeed * 0.35f;
            rotor.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 2.3f) * wob, _angle, Mathf.Cos(Time.time * 2.3f) * wob);
        }
    }
}
