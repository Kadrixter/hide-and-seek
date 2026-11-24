using System.Collections.Generic;
using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// The dead drop what they carry (2026-11-24): the moment a player becomes Dead (not merely downed - a downed hider
    /// keeps everything, the medkit can still get him up) every item of his inventory is thrown out of the body in a
    /// ring round it - each in its own direction, from chest height, so they scatter on the floor instead of piling up -
    /// as normal dropped items anybody allowed can pick up (E). A spot inside a wall / car / outside the garage is
    /// corrected by the drop system (ItemPlacement.CheckRest -> a free spot next to the body).
    /// Items with their own death rule keep it: the RC car in the hand is put down next to the body and the transmitter
    /// of a parked car falls next to it (MatchManager.Rc), the transmitter of a destroyed car is lost with its holder
    /// (MatchManager.RcRemote), the toy phone falls where the holder was (MatchManager.Phones). The seeker's knife is
    /// never dropped (seekers do not die anyway). A resurrected hider starts with an empty inventory.
    /// </summary>
    public partial class MatchManager
    {
        private readonly Dictionary<ulong, LifeState> _lastLife = new();
        private System.Action _stepDeathDrops;

        /// <summary>Items dropped by the dead this round (tests).</summary>
        public int DeathDropCount { get; private set; }

        private void UpdateDeathDrops()
        {
            if (!IsServer) return;
            var ph = Phase.Value;
            if (ph != MatchPhase.Hiding && ph != MatchPhase.Seeking && ph != MatchPhase.Ended)
            {
                _lastLife.Clear();
                DeathDropCount = 0;
                return;
            }
            foreach (var pl in NetPlayer.All)
            {
                if (pl == null || !pl.IsSpawned) continue;
                var now = pl.Life.Value;
                bool known = _lastLife.TryGetValue(pl.NetworkObjectId, out var was);
                _lastLife[pl.NetworkObjectId] = now;
                if (known && was != LifeState.Dead && now == LifeState.Dead && !RaceMode) DropAllOnDeath(pl);
            }
        }

        private static bool OwnDeathRule(HotbarItem item) => item == HotbarItem.RcCar || item == HotbarItem.RcRemote || item == HotbarItem.Phone;

        private void DropAllOnDeath(NetPlayer pl)
        {
            var kinds = new List<(HotbarItem item, byte extra)>();
            foreach (var d in ItemDefs.All)
            {
                if (d.Count == null || d.Take == null || !d.Droppable || d.CustomDrop != null || OwnDeathRule(d.Item)) continue;
                int n = Mathf.Min(d.Count(pl), 8);
                for (int k = 0; k < n; k++)
                {
                    if (!d.Take(pl, out byte extra)) break;
                    kinds.Add((d.Item, extra));
                }
            }
            if (kinds.Count == 0) return;
            float a0 = Random.Range(0f, 360f);
            for (int i = 0; i < kinds.Count; i++)
            {
                // Evenly round the body with some jitter, thrown out from the chest.
                float a = (a0 + i * 360f / kinds.Count + Random.Range(-18f, 18f)) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                var rot = Quaternion.Euler(Random.Range(-20f, 20f), Random.Range(0f, 360f), Random.Range(-20f, 20f));
                ThrowDrop(pl, kinds[i].item, kinds[i].extra, dir, 0.45f, 0.9f, Random.Range(1.7f, 2.7f), Random.Range(1.1f, 1.8f), rot);
            }
            DeathDropCount += kinds.Count;
            Debug.Log($"[Drops] {pl.Nick} died - {kinds.Count} item(s) fell out: {string.Join(", ", kinds.ConvertAll(k => k.item.ToString()))}");
        }
    }
}
