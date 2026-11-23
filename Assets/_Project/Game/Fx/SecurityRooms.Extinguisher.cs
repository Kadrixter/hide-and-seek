using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>Where the fire extinguisher of each security room hangs (entrance corridor, east wall, under its sign).</summary>
    public partial class SecurityRooms
    {
        /// <summary>Point on the floor under the tank axis of the extinguisher of level <paramref name="lv"/> (0, 1, 2).</summary>
        public static Vector3 ExtinguisherSpot(int lv) => new Vector3(X1 - Wall * 0.5f - 0.13f, FloorY(lv), Z0 + 2.15f);

        /// <summary>Wall surface behind it (x) and the direction out of the wall (into the corridor).</summary>
        public static float ExtinguisherWallX => X1 - Wall * 0.5f;
        public static readonly Vector3 ExtinguisherFacing = Vector3.left;

        /// <summary>Height of the extinguisher's foot above the floor (it hangs on the bracket).</summary>
        public const float ExtinguisherHang = 0.29f;
    }
}
