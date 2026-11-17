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
    }
}
