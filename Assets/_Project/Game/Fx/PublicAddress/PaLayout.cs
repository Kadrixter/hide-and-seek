using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Where the parking public-address system sits (fixed, the same on the server and every client; nothing is baked
    /// into the map). One wall station with the hand microphone per level on the south face of the central elevator
    /// core (between the two elevator doors, x 0.3, z -1.4 - the map builder's <c>BuildLobbyCentral</c>), and horn
    /// speakers high on the columns along the four column rows next to the aisles (z -47.6 / -8.1 / 8.1 / 48.6, the
    /// builder's <c>ColZ</c>), six per row, facing the aisle. Column positions are copied from ParkingGarageBuilder:
    /// keep in sync when the map changes (the runtime checks each spot and skips one that is not there).
    /// </summary>
    public static class PaLayout
    {
        public const int Levels = 3;
        public static float FloorY(int lv) => -3.4f * lv;
        public const float Ceiling = 3.0f;           // clear height of a level (floor to slab underside)

        // ---- station: box on the wall, the mic hangs on a hook right of it (seen from the front)
        private const float WallZ = -1.4f, BoxX = 0.3f;
        public const float BoxY = 1.5f;
        /// <summary>Station frame: origin on the wall at the box centre, +Z out of the wall (world -Z), +X = world -X.</summary>
        public static readonly Quaternion StationRot = Quaternion.Euler(0f, 180f, 0f);
        /// <summary>Mic centre on the hook (station frame).</summary>
        public static readonly Vector3 MicHangLocal = new Vector3(-0.27f, -0.081f, 0.04f);
        /// <summary>Tip of the cord gland under the box (station frame).</summary>
        public static readonly Vector3 GlandLocal = new Vector3(-0.07f, -0.183f, 0.045f);

        /// <summary>Take range: flat distance from the player to the hanging mic.</summary>
        public const float TakeRange = 1.05f;
        /// <summary>The prompt shows from here (farther: "come closer").</summary>
        public const float PromptRange = 2.2f;
        /// <summary>Cord fully stretched: gland to the holder's chest; beyond it the mic is pulled out of the hand.</summary>
        public const float MaxReach = 2.75f;

        public static Vector3 StationOrigin(int lv) => new Vector3(BoxX, FloorY(lv) + BoxY, WallZ);
        public static Vector3 Station(int lv, Vector3 local) => StationOrigin(lv) + StationRot * local;
        public static Vector3 MicHangPos(int lv) => Station(lv, MicHangLocal);
        public static Quaternion MicHangRot => StationRot;
        public static Vector3 GlandPos(int lv) => Station(lv, GlandLocal);

        /// <summary>Level of the station nearest to <paramref name="p"/> (same floor), -1 = none near.</summary>
        public static int StationNear(Vector3 p, float range)
        {
            for (int lv = 0; lv < Levels; lv++)
            {
                Vector3 m = MicHangPos(lv);
                if (Mathf.Abs(p.y - FloorY(lv)) > 1.2f) continue;
                if (new Vector2(p.x - m.x, p.z - m.z).magnitude <= range) return lv;
            }
            return -1;
        }

        // ---- speakers
        /// <summary>Horn pivot height above the floor (right under the ceiling beams, which start at 2.55 m).</summary>
        public const float SpeakerY = 2.4f;
        private static readonly float[] SpeakerX = { -67.5f, -37.5f, -7.5f, 7.5f, 37.5f, 67.5f };
        private static readonly (float z, float n)[] SpeakerRows = { (-47.6f, 1f), (-8.1f, -1f), (8.1f, 1f), (48.6f, -1f) };
        public const float ColumnHalf = 0.3f;

        /// <summary>Candidate column spots of one level: column centre (at the horn height) and the face normal.</summary>
        public static List<(Vector3 centre, Vector3 normal)> SpeakerSpots(int lv)
        {
            var list = new List<(Vector3, Vector3)>();
            foreach (var (z, n) in SpeakerRows)
            foreach (float x in SpeakerX)
                list.Add((new Vector3(x, FloorY(lv) + SpeakerY, z), new Vector3(0f, 0f, n)));
            return list;
        }
    }
}
