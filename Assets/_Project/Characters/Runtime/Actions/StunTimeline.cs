using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Seeker knocked out by a thrown leg (seconds from the hit; shared by the third-person body, the first-person view,
    /// the black screen, sounds and the server). The whole thing takes <see cref="Duration"/>: hit, a stagger, the knees
    /// go, onto the side and the back, lying out cold, then slowly up again and a shake of the head.
    /// </summary>
    public static class StunTimeline
    {
        public const float Snap = 0.16f;      // head snapped back by the hit
        public const float Reel = 0.62f;      // stumbles back a step
        public const float Sway = 1.0f;       // wobbles to the side, the eyes roll
        public const float Knees = 1.38f;     // down on the knees
        public const float Topple = 1.7f;     // onto the side
        public const float Thud = 1.62f;      // the body hits the floor (sound)
        public const float Flat = 2.05f;      // rolled onto the back, out cold
        public const float Stir = 3.55f;      // comes round: the head rolls, a groan
        public const float RollSide = 4.05f;  // onto the side, a hand on the floor
        public const float AllFours = 4.6f;
        public const float HalfKneel = 5.1f;
        public const float Bent = 5.5f;       // hands on the knees
        public const float ShakeStart = 5.42f;
        public const float Duration = 6.0f;   // back on the feet: may move and attack again

        // Screen of the stunned player.
        public const float DarkStart = 0.3f;  // the view starts to go
        public const float DarkFull = 1.25f;  // black
        public const float LightStart = 3.7f; // slowly back, blurred
        public const float LightFull = 5.35f;
        public const float BlurEnd = 6.4f;    // last of the blur (after the controls are back)

        /// <summary>Immune to the next knock-out this long after one ends (seconds).</summary>
        public const float Immunity = 22f;

        public static float Seg(float t, float a, float b) => ActionTimeline.SmoothStep01(ActionTimeline.Remap01(t, a, b));

        /// <summary>0..1 how black the stunned player's screen is at <paramref name="t"/>.</summary>
        public static float Darkness(float t)
        {
            if (t < 0f) return 0f;
            float down = ActionTimeline.EaseIn(ActionTimeline.Remap01(t, DarkStart, DarkFull));
            float up = Seg(t, LightStart, LightFull);
            return Mathf.Clamp01(down * (1f - up));
        }

        /// <summary>0..1 blur / double vision while coming round.</summary>
        public static float Blur(float t)
        {
            if (t < 0f) return 0f;
            return Seg(t, DarkStart, DarkFull) * (1f - Seg(t, LightFull - 0.3f, BlurEnd));
        }
    }

    /// <summary>
    /// Throwing a cut-off leg (shared by both views, the network and the server): hold the left mouse button to wind up
    /// (the arm goes back over the shoulder and the throw gets stronger), let go to throw overhand.
    /// </summary>
    public static class LegThrowTimeline
    {
        /// <summary>The arm goes back over the shoulder after the button goes down.</summary>
        public const float WindTime = 0.3f;
        /// <summary>Held this long = full power.</summary>
        public const float FullCharge = 1.1f;
        /// <summary>From letting go to the leg leaving the hand.</summary>
        public const float Release = 0.12f;
        public const float End = 0.72f;
        public const float MinPower = 0.25f;

        public static float Power(float held) => Mathf.Lerp(MinPower, 1f, Mathf.Clamp01(held / FullCharge));
        /// <summary>Launch speed (m/s) along the look direction.</summary>
        public static float Speed(float power) => Mathf.Lerp(7f, 15.5f, power);
        /// <summary>Extra upward speed (m/s) for the arc.</summary>
        public static float Lift(float power) => Mathf.Lerp(1.4f, 2.4f, power);
        /// <summary>Spin end over end (rad/s).</summary>
        public static float Spin(float power) => Mathf.Lerp(9f, 16f, power);
    }
}
