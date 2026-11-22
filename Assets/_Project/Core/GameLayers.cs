namespace HideAndSeek.Core
{
    /// <summary>Shared names so modules agree without referencing each other.</summary>
    public static class GameLayers
    {
        /// <summary>Tag on spawn point GameObjects placed by map builders.</summary>
        public const string SpawnPointTag = "Respawn";

        /// <summary>Name of the child holding all hider/seeker spawn points in a map.</summary>
        public const string SpawnPointsRoot = "SpawnPoints";

        /// <summary>Player nick that gets the custom photo face.</summary>
        public const string SpecialFaceNick = "cendy";
        public const string SpecialFaceResource = "Faces/opszczymorwformie";

        /// <summary>
        /// Physics layer of the ragdoll colliders of dead bodies (named "Ragdoll" in the TagManager). It collides with the
        /// level and with other ragdolls; character controllers exclude it (they never bump into a body), and cameras,
        /// blood decals and line-of-sight checks leave it out.
        /// </summary>
        public const int Ragdoll = 13;

        public const int RagdollMask = 1 << Ragdoll;
    }
}
