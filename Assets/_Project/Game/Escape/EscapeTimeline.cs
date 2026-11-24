using HideAndSeek.Game.Cinematics;
using UnityEngine;

namespace HideAndSeek.Game.Escape
{
    /// <summary>
    /// Timing and geometry of the escape run (stage GateOpening -> GateOpen, MatchManager.Escape.cs): the exit gate
    /// shutter rises slowly (motor + chain), the whole parking hears the siren, and past the gate a painted line on the
    /// driveway is the parking boundary. Everything is a pure function of the server time since the gate started
    /// (<c>MatchManager.EscapeGateAt</c>), so every peer shows the same shutter height.
    /// Gate-local space as in <see cref="ExitGate"/>: +Z into the garage, -Z out to the street, origin on the floor
    /// at the inner wall face, x across the opening.
    /// </summary>
    public static class EscapeTimeline
    {
        /// <summary>The motor winds up before the curtain moves.</summary>
        public const float SpinUp = 1.2f;
        /// <summary>Lift speed of the curtain bottom (m/s): ~20 s to standing height, ~27 s fully up.</summary>
        public const float Speed = 0.105f;
        /// <summary>Chain link period (s): a clunk and a small hitch in the motion each time.</summary>
        public const float LinkPeriod = 1.1f;
        /// <summary>Gap under the curtain from which a crouching player gets through (the collider follows the curtain).</summary>
        public const float CrouchClear = 1.2f;
        /// <summary>Gap at which a standing player walks through: the stage becomes GateOpen.</summary>
        public const float StandClear = 1.95f;
        /// <summary>The siren keeps going this long after the curtain is fully up.</summary>
        public const float SirenAfterOpen = 10f;

        /// <summary>The painted boundary line on the driveway (gate-local z). A hider whose feet pass it has escaped.</summary>
        public const float LineZ = -6f;
        /// <summary>Seekers only: the invisible wall in front of the line (its garage-side face).</summary>
        public const float SeekerWallZ = -5.6f;
        /// <summary>Half width of the driveway between the retaining walls.</summary>
        public const float DriveHalfW = ExitGate.HalfW + 0.4f;
        /// <summary>End of the walkable driveway (a wall stops anybody going further).</summary>
        public const float DriveEndZ = -11.5f;

        /// <summary>Curtain gap (m) <paramref name="t"/> seconds after the gate started.</summary>
        public static float Clearance(double t)
        {
            float u = (float)t - SpinUp;
            if (u <= 0f) return 0f;
            // Monotonic, with a slight slow-down once per chain link (derivative Speed * (1 - 0.35 cos)).
            float w = 2f * Mathf.PI / LinkPeriod;
            float c = Speed * (u - 0.35f * Mathf.Sin(w * u) / w);
            return Mathf.Clamp(c, 0f, ExitGate.OpenH);
        }

        /// <summary>0 = shut, 1 = fully up.</summary>
        public static float Fraction(double t) => Clearance(t) / ExitGate.OpenH;

        /// <summary>Seconds from the start until the curtain bottom reaches <paramref name="clear"/> m.</summary>
        public static float TimeTo(float clear)
        {
            // Monotonic: bisection on the closed form.
            float a = 0f, b = 80f;
            for (int i = 0; i < 40; i++)
            {
                float m = (a + b) * 0.5f;
                if (Clearance(m) < clear) a = m; else b = m;
            }
            return b;
        }

        static float _standAt = -1f, _fullAt = -1f, _crouchAt = -1f;
        /// <summary>When a crouching player first fits under the curtain.</summary>
        public static float CrouchAt => _crouchAt >= 0f ? _crouchAt : _crouchAt = TimeTo(CrouchClear);
        /// <summary>When the stage becomes GateOpen (standing height).</summary>
        public static float StandAt => _standAt >= 0f ? _standAt : _standAt = TimeTo(StandClear);
        /// <summary>When the curtain is fully up.</summary>
        public static float FullAt => _fullAt >= 0f ? _fullAt : _fullAt = TimeTo(ExitGate.OpenH - 0.001f);
        /// <summary>When the siren stops.</summary>
        public static float SirenEnd => FullAt + SirenAfterOpen;

        /// <summary>Gate frame (world) even before the gate object exists (server without the scene object yet).</summary>
        public static void Frame(out Vector3 pos, out Quaternion rot)
        {
            var g = ExitGate.Instance;
            if (g != null) { pos = g.transform.position; rot = g.transform.rotation; return; }
            var anchor = GameObject.Find("ExitGateAnchor");
            pos = anchor != null ? anchor.transform.position : ExitGate.DefaultPos;
            rot = Quaternion.Euler(0f, -90f, 0f);
        }

        /// <summary>World point -> gate-local point.</summary>
        public static Vector3 ToLocal(Vector3 world)
        {
            Frame(out var pos, out var rot);
            return Quaternion.Inverse(rot) * (world - pos);
        }

        public static Vector3 ToWorld(Vector3 local)
        {
            Frame(out var pos, out var rot);
            return pos + rot * local;
        }

        /// <summary>Feet past the boundary line, inside the driveway (not some point elsewhere with the same z).</summary>
        public static bool PastLine(Vector3 world)
        {
            var l = ToLocal(world);
            return l.z < LineZ && l.z > DriveEndZ - 3f && Mathf.Abs(l.x) < DriveHalfW + 0.5f && l.y > -1.5f && l.y < 3f;
        }

        /// <summary>Where an escaped player waits out the round (under the street, out of every view, own floor).</summary>
        public static Vector3 Limbo(ulong clientId)
        {
            int slot = (int)(clientId % 36);
            return new Vector3(150f + (slot % 6) * 2.5f, LimboY + 0.05f, -6f + (slot / 6) * 2.5f);
        }

        public const float LimboY = -40f;
    }
}
