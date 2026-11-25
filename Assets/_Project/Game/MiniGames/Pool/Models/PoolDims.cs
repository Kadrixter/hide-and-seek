using UnityEngine;

namespace HideAndSeek.Game.MiniGames.Pool
{
    /// <summary>
    /// Shared sizes of the pool (billiards) set, 2026-11-25. Real 7 ft "bar box" table, WPA balls (57.15 mm).
    /// TABLE SPACE: origin on the floor under the centre of the playing surface, long axis X, short axis Z, +Y up.
    /// The head end (break, kitchen) is at -X, the foot end (rack, foot spot) at +X.
    /// CUE SPACE: the cue lies along local +Z, the butt end (bumper bottom) at z = 0, the tip at z = <see cref="CueLength"/>.
    /// </summary>
    public static class PoolDims
    {
        // ---- balls
        public const float BallD = 0.05715f, BallR = BallD * 0.5f;

        // ---- playing surface (cushion nose to cushion nose)
        public const float PlayL = 1.981f, PlayW = 0.991f;
        /// <summary>Height of the cloth (bed) above the floor.</summary>
        public const float ClothY = 0.74f;
        /// <summary>Height of the cushion nose (contact point) above the cloth: 63.5 % of the ball.</summary>
        public const float NoseH = 0.0363f;
        /// <summary>Height of the top of the rails above the floor.</summary>
        public const float RailTopY = ClothY + 0.045f;
        /// <summary>Pocket mouths (between the cushion noses).</summary>
        public const float CornerMouth = 0.117f, SideMouth = 0.13f;

        /// <summary>Foot spot (apex ball of the rack), head spot / head string (kitchen line), on the cloth.</summary>
        public static readonly Vector3 FootSpot = new Vector3(PlayL * 0.25f, ClothY, 0f);
        public static readonly Vector3 HeadSpot = new Vector3(-PlayL * 0.25f, ClothY, 0f);

        /// <summary>Centre of a ball resting on the cloth at (x, z).</summary>
        public static Vector3 BallOnCloth(float x, float z) => new Vector3(x, ClothY + BallR, z);

        // ---- cue
        public const float CueLength = 1.47f;
        public const float TipD = 0.013f, ButtD = 0.031f;
        /// <summary>Joint of a two-piece cue (from the butt end).</summary>
        public const float JointZ = 0.735f;
    }
}
