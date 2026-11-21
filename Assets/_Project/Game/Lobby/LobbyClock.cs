using System;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>Wall clock showing the real local time (hour / minute / second hands rotate around local -Z).</summary>
    public class LobbyClock : MonoBehaviour
    {
        public Transform hour, minute, second;

        private void Update()
        {
            var t = DateTime.Now;
            float s = t.Second + t.Millisecond / 1000f;
            float m = t.Minute + s / 60f;
            float h = (t.Hour % 12) + m / 60f;
            if (hour != null) hour.localRotation = Quaternion.Euler(0f, 0f, -h * 30f);
            if (minute != null) minute.localRotation = Quaternion.Euler(0f, 0f, -m * 6f);
            // Second hand ticks (with a tiny overshoot), like a cheap quartz clock.
            float tick = Mathf.Floor(s) + Mathf.Clamp01((s - Mathf.Floor(s)) * 12f);
            if (second != null) second.localRotation = Quaternion.Euler(0f, 0f, -tick * 6f);
        }
    }
}
