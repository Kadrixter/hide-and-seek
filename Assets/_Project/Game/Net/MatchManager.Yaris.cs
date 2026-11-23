using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>Easter egg "Toyota Yaris Luna": the server rolls it at every round start (see <see cref="YarisEgg"/>).</summary>
    public partial class MatchManager
    {
        /// <summary>This round's RC cars are blue Yarises (server-rolled, synced to everybody incl. late joiners).</summary>
        public readonly NetworkVariable<bool> YarisRound = new(false);

        private void RollYarisEgg()
        {
            bool force = YarisEgg.ForceTest;
            bool on = force || Random.value < YarisEgg.Chance;
            YarisRound.Value = on;
            Debug.Log($"[Yaris] round roll: {(on ? "YARIS" : "rally cars")} (chance {YarisEgg.Chance:P0}{(force ? ", -testyaris" : "")})");
        }
    }
}
