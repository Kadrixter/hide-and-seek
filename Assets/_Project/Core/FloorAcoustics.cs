using UnityEngine;

namespace HideAndSeek.Core
{
    /// <summary>
    /// How sound travels between the parking floors (slab tops at y 0 / -3.4 / -6.8). The concrete slabs stop it
    /// completely; the only way to another floor is through an opening that links them: the two ramp shafts and the
    /// four stairwells. A sound one floor away is heard only when both the listener and the source are close to the
    /// same opening, and then heavily muffled. Two floors away nothing gets through.
    /// Shared by every 3D sound: pooled sfx, ambient emitters, footsteps and voice chat.
    /// Geometry mirrors Map/Editor ParkingGarageBuilder.Ramps / Stairs (keep in sync when the map changes).
    /// </summary>
    public static class FloorAcoustics
    {
        private struct Opening
        {
            public Rect Area;      // footprint on the xz plane (x, z)
            public bool Stairwell; // enclosed stairwell: sound has to get through the doors
            public float DoorX0, DoorX, DoorZ; // stairwell door x on level 0 / lower levels, door z (south face)
        }

        // Ramp shafts (ParkingGarageBuilder.Ramps): x between XTop / XBot, z0..z1, stacked through all levels.
        // Stairwells (ParkingGarageBuilder.Stairs): x0..x0+4, Zf..Zf+9.2, door on the south face (x0+1 on level 0, x0+3 below).
        private static readonly Opening[] Openings =
        {
            new Opening { Area = Rect.MinMaxRect(-66f, -7.5f, -44f, -0.5f) },
            new Opening { Area = Rect.MinMaxRect(44f, 0.5f, 66f, 7.5f) },
            Stair(-62f, 50.8f), Stair(46f, 50.8f), Stair(-34f, -4.6f), Stair(30f, -4.6f),
        };

        private static Opening Stair(float x0, float zf) => new Opening
        {
            Area = Rect.MinMaxRect(x0, zf, x0 + 4f, zf + 9.2f),
            Stairwell = true,
            DoorX0 = x0 + 1f,
            DoorX = x0 + 3f,
            DoorZ = zf - 0.3f,
        };

        /// <summary>Path length (listener to opening + opening to source) at which nothing is heard any more.</summary>
        public const float RampReach = 16f;
        public const float StairReach = 14f;

        /// <summary>Floor index (0, -1, -2) of a point.</summary>
        public static int FloorOf(float y) => y >= -0.25f ? 0 : y >= -3.65f ? -1 : -2;

        /// <summary>Floor of a listener (camera at eye height; also a crouching / prone camera).</summary>
        public static int ListenerFloor(float y) => FloorOf(y - 0.4f);

        /// <summary>Floor of a sound source (feet level up to the ceiling).</summary>
        public static int SourceFloor(float y) => FloorOf(y - 0.1f);

        /// <summary>True when the listener and the source are on different floors.</summary>
        public static bool DifferentFloor(Vector3 listener, Vector3 source) => ListenerFloor(listener.y) != SourceFloor(source.y);

        /// <summary>
        /// Gain (0..1) of a sound at <paramref name="source"/> heard from <paramref name="listener"/>, and the low-pass
        /// cutoff to apply. Same floor: 1 / no filter. <paramref name="clearLine"/>: nothing solid between them (looking
        /// down a ramp shaft) - a bit less muffled; two people next to each other on a ramp hear each other normally.
        /// </summary>
        public static float Transfer(Vector3 listener, Vector3 source, bool clearLine, out float cutoff)
        {
            cutoff = 22000f;
            int fl = ListenerFloor(listener.y), fs = SourceFloor(source.y);
            if (fl == fs) return 1f;
            if (clearLine && Mathf.Abs(listener.y - source.y) < 2f) return 1f;
            cutoff = 350f;
            if (Mathf.Abs(fl - fs) > 1) return 0f;

            float best = 0f;
            bool bestStair = false;
            foreach (var o in Openings)
            {
                float f = OpeningFactor(o, listener, fl, source, fs);
                if (f > best) { best = f; bestStair = o.Stairwell; }
            }
            if (best <= 0f) return 0f;
            float k = best * best;
            float gain = (clearLine ? 0.45f : bestStair ? 0.16f : 0.26f) * k;
            cutoff = Mathf.Lerp(380f, clearLine ? 2400f : bestStair ? 700f : 950f, best);
            return gain;
        }

        /// <summary>0..1: how well the opening links listener and source (1 = both standing at it).</summary>
        private static float OpeningFactor(in Opening o, Vector3 listener, int fl, Vector3 source, int fs)
        {
            float path;
            if (o.Stairwell)
            {
                // In the stairwell itself the flights are open between the levels; outside, through the door.
                float dl = InArea(o.Area, listener) ? 0f : Flat(listener, DoorOf(o, fl));
                float ds = InArea(o.Area, source) ? 0f : Flat(source, DoorOf(o, fs));
                path = dl + ds + 4f; // one flight between the doors
                return Mathf.Clamp01(1f - path / StairReach);
            }
            path = DistToArea(o.Area, listener) + DistToArea(o.Area, source);
            return Mathf.Clamp01(1f - path / RampReach);
        }

        private static Vector2 DoorOf(in Opening o, int floor) => new Vector2(floor == 0 ? o.DoorX0 : o.DoorX, o.DoorZ);

        private static bool InArea(Rect r, Vector3 p) => r.Contains(new Vector2(p.x, p.z));

        private static float Flat(Vector3 p, Vector2 q) => Vector2.Distance(new Vector2(p.x, p.z), q);

        private static float DistToArea(Rect r, Vector3 p)
        {
            float dx = Mathf.Max(r.xMin - p.x, 0f, p.x - r.xMax);
            float dz = Mathf.Max(r.yMin - p.z, 0f, p.z - r.yMax);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
