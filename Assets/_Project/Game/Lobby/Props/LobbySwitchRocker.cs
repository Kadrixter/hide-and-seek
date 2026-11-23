using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>Rocker of a wall switch follows its toggle (fan switch), with a click when it flips.</summary>
    public class LobbySwitchRocker : MonoBehaviour
    {
        public Transform rocker;
        public LobbyPropKind kind;
        private int _was = -1;
        private float _angle;

        private void Update()
        {
            if (rocker == null) return;
            bool on = LobbyPropDriver.On(kind);
            if (_was >= 0 && _was != (on ? 1 : 0)) LobbyPropSounds.Play(LobbyPropSounds.Id.Click, rocker.position, 0.5f);
            if (_was < 0) _angle = on ? -12f : 12f;
            _was = on ? 1 : 0;
            _angle = Mathf.MoveTowards(_angle, on ? -12f : 12f, Time.deltaTime * 300f);
            rocker.localRotation = Quaternion.Euler(_angle, 0f, 0f);
        }
    }
}
