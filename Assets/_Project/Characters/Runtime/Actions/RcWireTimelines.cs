using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Shared clock (s) of tearing a piece of wire out of a wire-mesh fence (third person, first person, the wire, sounds,
    /// server): step up and grab one strand with both fists (0 .. <see cref="Reach"/>), bend it back and forth until it
    /// fatigues (<see cref="Bends"/>, the mesh rattles), it snaps at <see cref="Snap"/> - a jolt back, a look at the piece
    /// in the right hand - and it goes into the pocket at <see cref="Pocket"/>.
    /// </summary>
    public static class WirePullTimeline
    {
        public const float Duration = 4f;
        public const float Reach = 0.45f, Snap = 3.3f, Pocket = 3.85f;
        public static readonly float[] Bends = { 0.85f, 1.35f, 1.85f, 2.35f, 2.85f };
        /// <summary>Feet this far (m) from the mesh, grip this high above the floor.</summary>
        public const float StandOff = 0.55f, GripHeight = 1.1f;

        public static float S(float t, float a, float b) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, t));

        /// <summary>-1..1: the strand bent towards / away from the body (the bends peak at <see cref="Bends"/>).</summary>
        public static float Bend(float t)
        {
            if (t < Reach + 0.1f || t >= Snap) return 0f;
            float k = S(t, Reach + 0.1f, Reach + 0.4f);
            return k * Mathf.Sin((t - (Bends[0] - 0.125f)) * Mathf.PI * 2f / 0.5f);
        }

        /// <summary>Whole-body weight (in and out).</summary>
        public static float Body(float t) => S(t, 0f, 0.3f) * (1f - S(t, Duration - 0.3f, Duration));
    }

    /// <summary>
    /// Shared clock (s) of swapping the RC transmitter's antenna for a piece of wire (third person, first person, the
    /// transmitter, sounds, server). The transmitter is lifted a little; the left hand lets go of its handle, takes
    /// the chrome antenna (<see cref="Grab"/>), wiggles and yanks it out of the socket (<see cref="YankEnd"/>), throws
    /// it away (<see cref="Release"/>), takes the wire from the pocket (<see cref="Pocket"/>), brings it up
    /// (<see cref="BringEnd"/>), pushes it into the socket (<see cref="InsertEnd"/>), twists it round the socket
    /// (<see cref="Install"/> - the long wire antenna is on, the range is up) and goes back to the handle.
    /// </summary>
    public static class AntennaSwapTimeline
    {
        public const float Raise = 0.25f, Grab = 0.55f, YankEnd = 0.9f, Release = 1.1f, Pocket = 1.6f, BringEnd = 2.2f,
            InsertEnd = 2.55f, Install = 2.65f, TwistEnd = 3.0f, End = 3.35f;

        /// <summary>Antenna socket on the transmitter (prop space, see <see cref="RcRemoteModel"/>), its rotation and axis.</summary>
        public static readonly Vector3 AntBase = new Vector3(0.335f, 0.238f, -0.012f);
        public static readonly Quaternion AntRot = Quaternion.Euler(-8f, 0f, -9f);
        public static Vector3 AntAxis => AntRot * Vector3.up;
        /// <summary>How far the chrome antenna comes out before it is free (m).</summary>
        public const float LiftMax = 0.3f;
        /// <summary>Where the fist holds the old antenna (m up its mast) and the new wire (m up from its end).</summary>
        public const float StockGrip = 0.13f, WireGrip = 0.16f;

        public static float S(float t, float a, float b) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, t));

        public static bool Running(float t) => t >= 0f && t < End;

        /// <summary>Old antenna pulled out of the socket (m): a wiggle, then two tugs.</summary>
        public static float StockLift(float t)
        {
            if (t < Grab) return 0f;
            float wiggle = 0.008f * Mathf.Abs(Mathf.Sin((t - Grab) * 40f)) * (1f - S(t, Grab + 0.15f, Grab + 0.2f));
            float k = S(t, Grab + 0.12f, Grab + 0.2f) * 0.25f + S(t, Grab + 0.2f, YankEnd) * 0.75f;
            return Mathf.Min(LiftMax, wiggle + LiftMax * k);
        }

        /// <summary>Height (m) of the wire's end above the socket while it is pushed in.</summary>
        public static float WireLift(float t)
        {
            if (t < BringEnd) return 0.12f;
            float k = S(t, BringEnd, BringEnd + 0.15f) * 0.5f + S(t, BringEnd + 0.2f, InsertEnd) * 0.5f;
            return 0.12f * (1f - k);
        }

        /// <summary>What the transmitter shows at swap time <paramref name="t"/> (&lt; 0 = no swap running).</summary>
        public static RcAntennaLook Look(float t, bool wired)
        {
            if (!Running(t)) return wired ? RcAntennaLook.Wire : RcAntennaLook.Stock;
            if (t < YankEnd) return RcAntennaLook.Stock;
            return t < Install ? RcAntennaLook.Bare : RcAntennaLook.Wire;
        }

        /// <summary>Lift of the stock antenna to show (0 outside the yank).</summary>
        public static float LiftShown(float t) => Running(t) && t < YankEnd ? StockLift(t) : 0f;

        /// <summary>Transmitter lifted towards the eyes 0..1.</summary>
        public static float Lifted(float t) => S(t, 0f, Raise) * (1f - S(t, TwistEnd, End));

        /// <summary>A point on the antenna axis, prop space (<paramref name="up"/> m above the socket).</summary>
        public static Vector3 OnAxis(float up) => AntBase + AntAxis * up;

        /// <summary>
        /// Left fist target for swap time <paramref name="t"/>. <paramref name="hand"/> / <paramref name="rot"/> = the
        /// transmitter's pivot (right fist) and rotation; <paramref name="toss"/> = where the old antenna is thrown from,
        /// <paramref name="pocket"/> = the left pocket (same space as the hand).
        /// </summary>
        public static Vector3 LeftHand(float t, Vector3 hand, Quaternion rot, Vector3 toss, Vector3 pocket)
        {
            Vector3 P(Vector3 local) => hand + rot * local;
            Vector3 handle = P(RcRemoteModel.LeftGrip);
            Vector3 stock = P(OnAxis(StockGrip + StockLift(t)));
            Vector3 wire = P(OnAxis(WireGrip + WireLift(t)));
            if (t < Raise) return handle;
            if (t < Grab)
            {
                float k = S(t, Raise, Grab);
                return Vector3.Lerp(handle, stock, k) + rot * new Vector3(0.04f, 0.05f, 0.06f) * Mathf.Sin(k * Mathf.PI);
            }
            if (t < YankEnd) return stock;
            if (t < Release + 0.1f) return Vector3.Lerp(stock, toss, S(t, YankEnd, Release));
            if (t < Pocket) return Vector3.Lerp(toss, pocket, S(t, Release + 0.1f, Pocket));
            if (t < BringEnd)
            {
                float k = S(t, Pocket + 0.05f, BringEnd);
                return Vector3.Lerp(pocket, wire, k) + rot * new Vector3(0.08f, 0.02f, 0.1f) * Mathf.Sin(k * Mathf.PI);
            }
            if (t < InsertEnd) return wire;
            Vector3 seated = P(OnAxis(0.05f));
            if (t < TwistEnd)
            {
                // Twisting the wire round the socket: small circles at the base.
                float a = (t - InsertEnd) * 28f;
                return Vector3.Lerp(wire, seated, S(t, InsertEnd, InsertEnd + 0.1f)) + rot * new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.012f;
            }
            return Vector3.Lerp(seated, handle, S(t, TwistEnd, End - 0.05f));
        }

        /// <summary>The loose wire shown in (or going from) the left hand: Pocket .. Install.</summary>
        public static bool WireInHand(float t) => t >= Pocket + 0.05f && t < Install;

        /// <summary>The thrown-away chrome antenna exists as a loose object from <see cref="YankEnd"/>.</summary>
        public static bool OldInHand(float t) => t >= YankEnd && t < Release;
    }
}
