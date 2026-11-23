using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>Floor globe: spins when used (speed and direction rolled by the server), slows down with friction, whirrs.</summary>
    public class LobbyGlobe : MonoBehaviour
    {
        public Transform sphere;
        private float _speed;

        private void Start() => LobbyPropDriver.Used += OnUsed;
        private void OnDestroy() => LobbyPropDriver.Used -= OnUsed;

        private void OnUsed(LobbyPropKind kind, ulong user, int arg)
        {
            if (kind != LobbyPropKind.Globe) return;
            _speed = -1000f; // wait for the hand to reach it
            _pending = arg;
            _at = Time.time + 0.45f;
        }

        private int _pending;
        private float _at;

        private void Update()
        {
            if (sphere == null) return;
            if (_speed <= -999f && Time.time >= _at)
            {
                _speed = _pending;
                LobbyPropSounds.Play(LobbyPropSounds.Id.Whirr, sphere.position, 0.5f);
            }
            if (_speed <= -999f) return;
            sphere.Rotate(0f, _speed * Time.deltaTime, 0f, Space.Self);
            // friction: quadratic at speed, linear when slow
            _speed = Mathf.MoveTowards(_speed, 0f, (40f + Mathf.Abs(_speed) * 0.35f) * Time.deltaTime);
        }
    }
}
