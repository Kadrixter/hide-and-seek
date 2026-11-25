using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Floor plan of the drift track behind the lobby - shared by the lobby builder (the arch in the west wall, the
    /// passage, the shutter: MultiplayerBuilder.LobbyArch.cs) and the runtime track (<see cref="DriftTrack"/>) and karts
    /// (<see cref="LobbyKart"/>). The arch is in the lobby's west wall at z = <see cref="ArchZ"/> (between the moon
    /// painting's sconce and the sofa); a 4 m passage runs west to the track's east wall at <see cref="PadEast"/>; the
    /// asphalt pad (84 x 64 m, night, floodlights) lies west of it, centred on the passage line. North = +Z.
    /// </summary>
    public static class DriftTrackLayout
    {
        // ---- arch in the lobby wall (x = -HalfW) and the passage
        public const float ArchZ = 2.25f, ArchWidth = 1.4f, ArchSpring = 2.05f;
        public static float ArchRadius => ArchWidth * 0.5f;
        public static float ArchTop => ArchSpring + ArchRadius;
        public const float WallThick = 0.2f;
        public const float PassageWidth = 2.2f, PassageHeight = 3.0f;
        public static float PassageStart => -LobbyLayout.HalfW - WallThick;   // outer face of the lobby wall
        public const float PadEast = -12.2f;                                  // the track's east wall (gate plane)
        public static float PassageMid => (PassageStart + PadEast) * 0.5f;

        // ---- the asphalt pad
        public const float PadLength = 84f, PadDepth = 64f;
        public static float PadWest => PadEast - PadLength;
        public static Vector3 PadCenter => new(PadEast - PadLength * 0.5f, 0f, ArchZ);
        public static float PadNorth => ArchZ + PadDepth * 0.5f;
        public static float PadSouth => ArchZ - PadDepth * 0.5f;
        /// <summary>Barriers stand this far inside the pad edge; the karts must stay inside them.</summary>
        public const float BarrierInset = 1.2f;

        // ---- karts: a row north of the gate, noses west
        public const int KartCount = 5;
        public const float KartSlotYaw = 270f;
        public static Vector3 KartSlot(int i) => new(PadEast - 4.2f, 0f, ArchZ + 3.8f + i * 2.4f);

        /// <summary>Inside the barriers (karts outside are put back on their slot).</summary>
        public static bool InsidePad(Vector3 p, float margin = 0.4f) =>
            p.x < PadEast - margin && p.x > PadWest + BarrierInset + margin && p.z < PadNorth - BarrierInset - margin && p.z > PadSouth + BarrierInset + margin
            && p.y > -0.5f && p.y < 1.5f && !float.IsNaN(p.x) && !float.IsNaN(p.z);

        /// <summary>On the track side of the gate or in the passage (the track is drawn from here).</summary>
        public static bool OnTrackSide(Vector3 p) => p.x < -LobbyLayout.HalfW + 0.05f;

        /// <summary>The arch opening seen from the lobby (for the visibility test).</summary>
        public static Bounds ArchBounds => new(new Vector3(-LobbyLayout.HalfW - WallThick * 0.5f, ArchTop * 0.5f, ArchZ), new Vector3(WallThick + 0.1f, ArchTop, ArchWidth));
    }
}
