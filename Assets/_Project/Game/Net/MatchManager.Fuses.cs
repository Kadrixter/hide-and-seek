using System.Collections.Generic;
using HideAndSeek.Core;
using HideAndSeek.Map;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Escape objective, stage <see cref="EscapeStage.Fuses"/>: three wall fuse boxes (one per level, all alike, 4
    /// sockets each with a painted mark) and 8 fuses lying on the floor round the garage (each with its own mark). Only
    /// one box is the right one (server's secret, new every round). Marks are dealt every round: the right box gets
    /// A B C D, wrong box 1 A B E F, wrong box 2 C D G H - so a full wrong box is never one swap away from the right one.
    /// A fuse fits only the socket with its mark (<see cref="ServerFuseInsert"/>, MatchManager.FuseWork.cs).
    /// The right box full: locked for good, green lamp, hum, sparks, stage -> <see cref="EscapeStage.DotLink"/>.
    /// A wrong box full: every light of its level comes on (FloorLights) until the box is empty again; fuses come out one
    /// at a time (E held), into the hider's inventory or on the floor; the seeker can pull them out too (to the floor).
    /// Synced: <see cref="FuseBoxes"/>, fuses as dropped items (<see cref="Drops"/>) or in <see cref="NetPlayer.FuseMask"/>,
    /// work in progress in <see cref="NetPlayer.FuseWork"/> - late joiners get it all. Reset every round.
    /// </summary>
    public partial class MatchManager
    {
        public readonly Unity.Netcode.NetworkVariable<FuseBoxSet> FuseBoxes = new();

        /// <summary>Server: index of the right box (-1 = none this round).</summary>
        private int _fuseGood = -1;
        private bool _fusesDealt;
        private int _fuseRound;
        private System.Action _stepFuses;
        /// <summary>Server: last known position and fuses of every player (fuses of somebody who left fall where they were).</summary>
        private readonly Dictionary<ulong, (byte mask, Vector3 pos)> _fuseCarriers = new();

        /// <summary>Server, tests: the right box.</summary>
        public int FuseGoodBox => _fuseGood;

        private bool FusePhase => Phase.Value == MatchPhase.Hiding || Phase.Value == MatchPhase.Seeking;

        public FuseBox FuseBoxAt(int i) => FuseBoxes.Value[i];

        // ================================================================ every peer

        private void UpdateFuses()
        {
            bool game = SceneManager.GetActiveScene().name == NetSession.GameScene && !RaceMode;
            var ph = Phase.Value;
            bool show = game && (ph == MatchPhase.Hiding || ph == MatchPhase.Seeking || ph == MatchPhase.Ended);
            FuseBoxViews.Sync(show ? FuseBoxes.Value : default, show);
            FloorLights.Sync(show ? FuseBoxes.Value : default, show);
            foreach (var pl in NetPlayer.All)
                if (pl != null && pl.GetComponent<FuseWorkDriver>() == null) pl.gameObject.AddComponent<FuseWorkDriver>();
            if (ph == MatchPhase.Hiding || ph == MatchPhase.Seeking) EscapeHud.Ensure();

            if (ph == MatchPhase.Lobby && FuseModel.WarmUp()) FuseBoxModel.WarmUp(); // looks ready before the round starts
            if (!IsServer) return;
            if (ph == MatchPhase.Lobby || ph == MatchPhase.Loading)
            {
                if (_fusesDealt || FuseBoxes.Value.Placed) ResetFuses();
                return;
            }
            if (!game || RaceMode) return;
            if (ph == MatchPhase.Hiding && !_fusesDealt) DealFuses();
            if (FusePhase) TrackFuseCarriers();
        }

        private void ResetFuses()
        {
            _fusesDealt = false;
            _fuseGood = -1;
            _fuseCarriers.Clear();
            FuseBoxes.Value = new FuseBoxSet { Round = _fuseRound };
            foreach (var pl in NetPlayer.All)
            {
                if (pl == null) continue;
                if (pl.FuseMask.Value != 0) pl.FuseMask.Value = 0;
                if (pl.FuseWork.Value.Active) pl.FuseWork.Value = FuseWorkState.None;
            }
        }

        // ================================================================ server: the round's boxes and fuses

        private void DealFuses()
        {
            _fusesDealt = true;
            _fuseRound++;
            var rnd = new System.Random(System.Environment.TickCount ^ (_fuseRound * 7919));
            var walls = FuseSpots.PickBoxes(rnd);
            var boxes = new List<int>();
            var set = new FuseBoxSet { Round = _fuseRound };
            for (int lv = 0; lv < walls.Length && lv < FuseTuning.Boxes; lv++)
            {
                if (walls[lv] == null) continue;
                var w = walls[lv].Value;
                set[lv] = new FuseBox { Pos = w.Pos, Yaw = w.Yaw, Flags = FuseBox.FlagPlaced };
                boxes.Add(lv);
            }
            if (boxes.Count == 0)
            {
                Debug.LogWarning("[Fuses] no wall spot for any fuse box - objective off this round");
                FuseBoxes.Value = set;
                return;
            }

            // Marks: A B C D good, E F / G H bad; the right box random; sockets in random order.
            var marks = new List<int> { 0, 1, 2, 3, 4, 5, 6, 7 };
            Shuffle(marks, rnd);
            _fuseGood = boxes[rnd.Next(boxes.Count)];
            var bad = boxes.FindAll(b => b != _fuseGood);
            Deal(ref set, _fuseGood, new[] { marks[0], marks[1], marks[2], marks[3] }, rnd);
            if (bad.Count > 0) Deal(ref set, bad[0], new[] { marks[0], marks[1], marks[4], marks[5] }, rnd);
            if (bad.Count > 1) Deal(ref set, bad[1], new[] { marks[2], marks[3], marks[6], marks[7] }, rnd);
            FuseBoxes.Value = set;
            for (int i = 0; i < FuseTuning.Boxes; i++)
                if (set[i].Placed)
                    Debug.Log($"[Fuses] box {i}{(i == _fuseGood ? " (RIGHT)" : "")} at {set[i].Pos} yaw {set[i].Yaw:0} marks " +
                              $"{FuseSymbols.Name(set[i].S0)}, {FuseSymbols.Name(set[i].S1)}, {FuseSymbols.Name(set[i].S2)}, {FuseSymbols.Name(set[i].S3)}");

            // The 8 fuses: 3 / 3 / 2 over the levels (which level gets 2 is random).
            var boxPos = new List<Vector3>();
            for (int i = 0; i < FuseTuning.Boxes; i++) if (set[i].Placed) boxPos.Add(set[i].Pos);
            int few = rnd.Next(MapInfo.Levels);
            var levels = new List<int>();
            for (int lv = 0; lv < MapInfo.Levels; lv++)
                for (int k = 0; k < (lv == few ? 2 : 3); k++) levels.Add(lv);
            Shuffle(levels, rnd);
            var placed = new List<Vector3>();
            for (int s = 0; s < FuseTuning.Fuses; s++)
            {
                int lv = levels[s % levels.Count];
                var p = FuseSpots.FuseSpot(lv, rnd, placed, boxPos);
                if (p == null) { Debug.LogWarning($"[Fuses] no floor spot for fuse {FuseSymbols.Name(s)}"); continue; }
                placed.Add(p.Value);
                SpawnRestingDrop(FuseItems.Of(s), p.Value, rnd);
                Debug.Log($"[Fuses] fuse {FuseSymbols.Name(s)} on level {lv} at {p.Value} (cover {FuseSpots.Cover(p.Value)})");
            }
            DealFuelCans(rnd, placed, boxPos); // chainsaw fuel canisters (MatchManager.SawNerf.cs)
        }

        private static void Deal(ref FuseBoxSet set, int box, int[] marks, System.Random rnd)
        {
            var b = set[box];
            var order = new List<int>(marks);
            Shuffle(order, rnd);
            for (int i = 0; i < FuseTuning.Sockets; i++) b.SetSymbol(i, order[i]);
            set[box] = b;
        }

        private static void Shuffle<T>(List<T> list, System.Random rnd)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rnd.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /// <summary>A fuse lying still on the floor from the start (a settled dropped item: E picks it up as usual).</summary>
        private void SpawnRestingDrop(HotbarItem item, Vector3 floor, System.Random rnd)
        {
            float yaw = (float)rnd.NextDouble() * 360f;
            var rot = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(0f, 0f, rnd.Next(4) * 90f);
            Drops.Add(new DropState
            {
                Id = _nextDropId++, Item = (byte)item, Settled = true, Pos = floor + Vector3.up * (FuseModel.HalfShown + 0.004f), Rot = rot,
            });
        }

        /// <summary>Fuses of a player who left the game fall where he was last seen (else the objective could not be finished).</summary>
        private void TrackFuseCarriers()
        {
            var seen = new HashSet<ulong>();
            foreach (var pl in NetPlayer.All)
            {
                if (pl == null || !pl.IsSpawned) continue;
                seen.Add(pl.NetworkObjectId);
                if (pl.FuseMask.Value != 0) _fuseCarriers[pl.NetworkObjectId] = (pl.FuseMask.Value, pl.transform.position);
                else _fuseCarriers.Remove(pl.NetworkObjectId);
            }
            List<ulong> gone = null;
            foreach (var kv in _fuseCarriers) if (!seen.Contains(kv.Key)) (gone ??= new List<ulong>()).Add(kv.Key);
            if (gone == null) return;
            foreach (ulong id in gone)
            {
                var (mask, pos) = _fuseCarriers[id];
                _fuseCarriers.Remove(id);
                var around = new List<Vector3>();
                for (int s = 0; s < FuseTuning.Fuses; s++)
                {
                    if ((mask & (1 << s)) == 0) continue;
                    Vector3 p = ItemPlacement.Scatter(pos, ItemPlacement.Small, around, "fuse of a player who left");
                    around.Add(p);
                    Drops.Add(new DropState { Id = _nextDropId++, Item = (byte)FuseItems.Of(s), Pos = p + Vector3.up * 0.3f, Rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) });
                    Debug.Log($"[Fuses] player {id} left with fuse {FuseSymbols.Name(s)} - it fell at {p}");
                }
            }
        }
    }
}
