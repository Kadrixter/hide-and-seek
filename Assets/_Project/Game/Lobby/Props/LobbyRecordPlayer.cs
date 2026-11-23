using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Record player on the cabinet: toggled on, the tone arm swings over the record and lowers, the platter spins
    /// up to 33 rpm and the needle crackles; toggled off the arm lifts and returns, the platter coasts.
    /// </summary>
    public class LobbyRecordPlayer : MonoBehaviour
    {
        public Transform platter;
        /// <summary>Tone arm pivot: yaw swings it over the record, local X tilt lowers it.</summary>
        public Transform arm;
        public float armRestYaw = 0f, armPlayYaw = 24f;
        private float _rpm, _armU = -1f;
        private AudioSource _crackle;

        private void Start()
        {
            _crackle = LobbyPropSounds.Loop(transform, LobbyPropSounds.Id.Crackle, 6f);
            _crackle.Play();
        }

        private void Update()
        {
            bool on = LobbyPropDriver.On(LobbyPropKind.RecordPlayer);
            if (_armU < 0f) _armU = on ? 1f : 0f;
            // arm: 0 rest -> 0.7 over the groove -> 1 down; the needle drops last
            _armU = Mathf.MoveTowards(_armU, on ? 1f : 0f, Time.deltaTime * 0.9f);
            _rpm = Mathf.MoveTowards(_rpm, on ? 33.3f : 0f, Time.deltaTime * (on ? 25f : 9f));
            if (platter != null) platter.Rotate(0f, _rpm * 6f * Time.deltaTime, 0f, Space.Self);
            if (arm != null)
            {
                float swing = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_armU / 0.7f));
                float drop = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((_armU - 0.7f) / 0.3f));
                arm.localRotation = Quaternion.Euler(-4f * (1f - drop) + 1.5f * drop, Mathf.Lerp(armRestYaw, armPlayYaw, swing) + drop * Mathf.Sin(Time.time * 3.5f) * 0.15f, 0f);
            }
            if (_crackle != null) _crackle.volume = _armU > 0.98f ? 0.35f * Mathf.Clamp01(_rpm / 33.3f) : 0f;
        }
    }
}
