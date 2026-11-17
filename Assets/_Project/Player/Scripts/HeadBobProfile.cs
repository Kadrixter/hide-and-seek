using System;
using UnityEngine;

namespace HideAndSeek.Player
{
    /// <summary>Bob shape for one gait (walk / run / crouch).</summary>
    [Serializable]
    public struct BobGait
    {
        [Tooltip("Vertical amplitude in metres (one cycle per step).")]
        public float vertical;
        [Tooltip("Lateral amplitude in metres (one cycle per two steps).")]
        public float lateral;
        [Tooltip("Roll amplitude in degrees, follows the lateral sway.")]
        public float roll;
        [Tooltip("Pitch nod amplitude in degrees, follows the vertical bob.")]
        public float pitch;
        [Tooltip("Multiplier on the step frequency (1 = one vertical cycle per footstep, synced with footstep events).")]
        public float frequency;

        public static BobGait Lerp(BobGait a, BobGait b, float t) => new BobGait
        {
            vertical = Mathf.Lerp(a.vertical, b.vertical, t),
            lateral = Mathf.Lerp(a.lateral, b.lateral, t),
            roll = Mathf.Lerp(a.roll, b.roll, t),
            pitch = Mathf.Lerp(a.pitch, b.pitch, t),
            frequency = Mathf.Lerp(a.frequency, b.frequency, t),
        };
    }

    /// <summary>Tunable camera bob / feel settings.</summary>
    [CreateAssetMenu(menuName = "HideAndSeek/Player/Head Bob Profile", fileName = "HeadBobProfile")]
    public class HeadBobProfile : ScriptableObject
    {
        [Header("Gaits")]
        public BobGait walk = new BobGait { vertical = 0.034f, lateral = 0.024f, roll = 0.55f, pitch = 0.35f, frequency = 1f };
        public BobGait run = new BobGait { vertical = 0.062f, lateral = 0.042f, roll = 1.1f, pitch = 0.7f, frequency = 1f };
        public BobGait crouch = new BobGait { vertical = 0.02f, lateral = 0.032f, roll = 0.75f, pitch = 0.25f, frequency = 1f };

        [Header("Shape")]
        [Tooltip("0 = classic arc, 1 = full figure-8 (lateral phase shifted 45 degrees).")]
        [Range(0f, 1f)] public float figureEight = 0.85f;
        [Tooltip("Sharpens the low point of the vertical bob (footfall emphasis). 0 = pure sine.")]
        [Range(0f, 1f)] public float footfallSharpness = 0.35f;

        [Header("Blending")]
        [Tooltip("How quickly the bob fades in/out (1/s).")]
        public float blendSharpness = 7f;
        [Tooltip("Speed scaling exponent: amplitude *= (speed/gaitSpeed)^x.")]
        public float speedAmplitudeExponent = 0.8f;

        [Header("Landing")]
        [Tooltip("Downward velocity (m/s) added to the head spring per m/s of impact.")]
        public float landingImpulse = 0.11f;
        public float landingMaxImpact = 12f;
        [Tooltip("Spring natural frequency in Hz.")]
        public float landingFrequency = 3.2f;
        [Range(0.05f, 1.5f)] public float landingDamping = 0.48f;
        [Tooltip("Max dip in metres.")]
        public float landingMaxDip = 0.2f;
        [Tooltip("Pitch (degrees) per metre of dip.")]
        public float landingPitch = 18f;
        [Tooltip("Small dip when jumping off.")]
        public float jumpImpulse = 0.35f;

        [Header("Idle breathing")]
        public float breathVertical = 0.0045f;
        public float breathLateral = 0.0025f;
        public float breathRoll = 0.12f;
        public float breathPitch = 0.15f;
        [Tooltip("Breaths per second.")]
        public float breathRate = 0.24f;

        [Header("Field of view")]
        public float baseFov = 70f;
        public float runFov = 76f;
        public float fovSharpness = 5f;

        [Header("Strafe tilt")]
        public bool strafeTilt = true;
        [Tooltip("Roll in degrees at run speed sideways.")]
        public float strafeTiltDegrees = 1.6f;
        public float strafeTiltSharpness = 6f;
    }
}
