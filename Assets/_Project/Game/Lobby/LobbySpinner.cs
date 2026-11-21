using System;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>Spins around a local axis (ceiling fan).</summary>
    public class LobbySpinner : MonoBehaviour
    {
        public Vector3 axis = Vector3.up;
        public float degreesPerSecond = 60f;

        private void Update() => transform.Rotate(axis, degreesPerSecond * Time.deltaTime, Space.Self);
    }
}
