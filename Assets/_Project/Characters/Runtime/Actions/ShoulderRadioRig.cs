using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Geometry and timings of the boombox carried on the right shoulder (the radio found on the parking), shared by
    /// the third-person layer (<c>BlockyCharacterActions.ShoulderRadio.cs</c>), the first-person hands and the game.
    /// The model itself lives in the game (<c>Boombox</c>); its pivot is the bottom centre, +Z = the speaker face,
    /// +Y = up, the carry handle on top.
    /// </summary>
    public static class ShoulderRadioRig
    {
        /// <summary>Boombox body (m) - keep in sync with <c>Boombox.Model.cs</c>.</summary>
        public const float Width = 0.56f, Height = 0.27f, Depth = 0.15f;

        /// <summary>Middle of the rubber grip of the handle (radio space).</summary>
        public static readonly Vector3 GripLocal = new Vector3(0f, 0.345f, -0.01f);

        /// <summary>Lift: reach and grab, up to the chest, onto the shoulder (s).</summary>
        public const float LiftTime = 1.45f;
        public const float LiftGrab = 0.5f, LiftChest = 0.98f;
        /// <summary>Put down: off the shoulder, down to the spot, let go and stand up (s).</summary>
        public const float PutTime = 1.3f;
        public const float PutChest = 0.38f, PutRelease = 0.88f;
        /// <summary>Knocked down / killed: the radio drops off the shoulder (s).</summary>
        public const float FallTime = 0.7f;

        /// <summary>Where the radio rides (pivot in torso space: origin at the hips, +X right, +Y up, +Z forward).</summary>
        public static Vector3 RideTorso = new Vector3(0.36f, 0.672f, -0.05f);
        /// <summary>Its rotation in torso space: face outwards (+X), long side front-back, top leaning a little in (to the ear).</summary>
        public static Vector3 RideEuler = new Vector3(-8f, 90f, 0f);
        /// <summary>
        /// Fist centre relative to the grip (radio space): the blocky arm is too short to hold the handle from right
        /// above, so the fist takes it from the outside (over the top outer edge), elbow out to the side.
        /// </summary>
        public static Vector3 FistOnGrip = new Vector3(0f, -0.05f, 0.13f);
        /// <summary>Elbow pole of the carrying arm (torso space).</summary>
        public static Vector3 RidePole = new Vector3(1f, -0.6f, 0.15f);
        /// <summary>Head tilt away from the radio (deg, roll to the left) and a small turn.</summary>
        public static float HeadTilt = 11f, HeadYaw = -4f;

        public static float S(float t, float a, float b) => ActionTimeline.SmoothStep01(ActionTimeline.Remap01(t, a, b));

        public static Quaternion RideRot => Quaternion.Euler(RideEuler);
    }
}
