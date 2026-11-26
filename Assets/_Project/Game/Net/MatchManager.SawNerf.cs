using System.Collections.Generic;
using HideAndSeek.Audio;
using HideAndSeek.Core;
using HideAndSeek.Map;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Chainsaw nerf on the server (2026-11-25): a hider wrests the saw from a seeker who is knocked out or has
    /// something in the eyes (it goes into the hider's inventory; hiders never start it, they can only hide it), and
    /// the round's fuel canisters (<see cref="SawTuning.Cans"/>, dealt with the fuses) fill the saw up again.
    /// </summary>
    public partial class MatchManager
    {
        /// <summary>Tests: canisters dealt this round / saws wrested so far.</summary>
        public int FuelCansDealt { get; private set; }
        public int SawWrests { get; private set; }

        private readonly Dictionary<ulong, (bool inHands, bool stalled, bool dry)> _sawStartWatch = new();
        private readonly Dictionary<ulong, float> _sawDryPullAt = new();

        /// <summary>
        /// Server, every frame: a new pull-start every time the saw comes into the hands, the stall is over or the empty
        /// tank was filled up (2026-11-26: an empty saw is dead - no start until refuelled); the number of tries is rolled
        /// here (25% two, 10% four, else one) and synced, so every peer animates the same yanks.
        /// </summary>
        private void UpdateSawStarts()
        {
            foreach (var p in NetPlayer.All)
            {
                if (p == null || !p.IsSpawned) continue;
                bool inHands = p.SawInHands, dry = p.SawDry, stalled = p.SawStalled || dry;
                _sawStartWatch.TryGetValue(p.NetworkObjectId, out var was);
                _sawStartWatch[p.NetworkObjectId] = (inHands, stalled, dry);
                if (!inHands)
                {
                    if (p.SawStartAt.Value != SawTuning.NotStarted) p.SawStartAt.Value = SawTuning.NotStarted;
                    continue;
                }
                if (stalled)
                {
                    // Dead engine: no start under way (a refuel or the end of the stall starts a fresh one).
                    if (p.SawStartAt.Value != SawTuning.NotStarted) p.SawStartAt.Value = SawTuning.NotStarted;
                    continue;
                }
                if (was.inHands && !was.stalled) continue;
                byte tries = ForcedSawTries > 0 ? (byte)ForcedSawTries : RollSawTries(Random.value);
                p.SawAttempts.Value = tries;
                p.SawStartAt.Value = Now + (!was.inHands ? SawTuning.PullDelay : was.dry ? SawTuning.RefuelPullDelay : 0d);
                Debug.Log($"[Saw] {p.Nick}: pull-start, {tries} tries{(!was.inHands ? "" : was.dry ? " (after the refuel)" : " (after the stall)")}");
            }
        }

        /// <summary>
        /// F of a seeker holding the saw with an empty tank: a pull of the cord that does not catch, heard by everybody
        /// (no cutting; at most one every <see cref="SawTuning.DryPullGap"/> s).
        /// </summary>
        public void ServerSawDryPull(NetPlayer p)
        {
            if (!Seeking || p == null || !p.SawInHands || !p.SawDry || p.SawStalled || p.Busy.Value) return;
            _sawDryPullAt.TryGetValue(p.NetworkObjectId, out float last);
            if (last > 0f && Time.time - last < SawTuning.DryPullGap) return;
            _sawDryPullAt[p.NetworkObjectId] = Time.time;
            p.SawDryPullRpc();
            p.ToastRpc(p.FuelCans.Value > 0 ? "Piła: pusty bak - nie odpali. R - zatankuj z kanistra." : "Piła: pusty bak - nie odpali. Znajdź kanister z paliwem.", 2.5f);
            Debug.Log($"[Saw] {p.Nick}: dry pull (empty tank)");
        }

        /// <summary>Tries a pull-start needs for a random <paramref name="r"/> in 0..1: 10% four, 25% two, else one.</summary>
        public static byte RollSawTries(float r) => (byte)(r < SawTuning.FourTries ? 4 : r < SawTuning.FourTries + SawTuning.TwoTries ? 2 : 1);

        /// <summary>Tests: the next pull-starts need this many tries (0 = random).</summary>
        public static int ForcedSawTries;

        /// <summary>R of a seeker with the saw in the hands and a canister in the inventory (the saw cannot be put away).</summary>
        public void ServerRefuelFromHands(NetPlayer p)
        {
            if (p == null || !p.SawInHands) return;
            if (p.FuelCans.Value == 0) { p.ToastRpc("Nie masz kanistra.", 2f); return; }
            ServerRefuelSaw(p);
        }

        /// <summary>E of a hider next to a stunned / blinded seeker holding the chainsaw.</summary>
        public void ServerWrestSaw(NetPlayer hider, NetPlayer seeker)
        {
            if (!Seeking || hider == null || seeker == null || !hider.IsAliveHider || hider.Busy.Value || hider.Seat.Value >= 0) return;
            if (!seeker.SawWrestable) return;
            Vector3 d = seeker.transform.position - hider.transform.position;
            if (new Vector2(d.x, d.z).magnitude > SawTuning.WrestRange + 0.4f || Mathf.Abs(d.y) > SawTuning.WrestHeight) return;
            if (!Inventory.HasRoomFor(hider, HotbarItem.Chainsaw)) { hider.ToastRpc(Inventory.FullText, 2.5f); return; }
            var def = ItemDefs.Get(HotbarItem.Chainsaw);
            if (def?.Take == null || def.Give == null || !def.Take(seeker, out byte extra)) return;
            // The fire extinguisher (if any) falls out of the hands: the saw needs them.
            int ext = ExtHeldBy(hider);
            if (ext >= 0) ExtDrop(ext, hider, "took the saw");
            def.Give(hider, (byte)(extra & ~SawFuel.HiderFlag));
            SawWrests++;
            seeker.SawRev.Value = false; // owner-written, the owner resets it as well (no saw in the hands)
            hider.SfxRpc(GameSfx.ChainsawStart, 0.35f);
            hider.SfxRpc(GameSfx.BodyFall, 0.4f);
            hider.ToastRpc("Wyrwałeś piłę! Nie odpalisz jej - schowaj ją (Q).", 4f);
            seeker.ToastRpc($"{hider.Nick} wyrwał ci piłę!", 4f);
            Debug.Log($"[Saw] {hider.Nick} wrested the chainsaw from {seeker.Nick} (fuel {SawFuel.Unpack(extra):0.0}, stunned={seeker.Stunned}, foamed={seeker.Foamed})");
        }

        /// <summary>LMB of a seeker with the canister selected: the saw is full again, the canister is used up.</summary>
        public void ServerRefuelSaw(NetPlayer p)
        {
            if (!Seeking || p == null || !p.IsSeeker || p.Life.Value != LifeState.Alive || p.Busy.Value || p.FuelCans.Value == 0) return;
            if (!p.HasChainsaw.Value) { p.ToastRpc("Kanister: najpierw zdobądź piłę.", 2.5f); return; }
            if (p.ServerSawFuel >= SawFuel.Full - 0.05f) { p.ToastRpc("Piła ma pełny bak.", 2f); return; }
            float before = p.ServerSawFuel;
            p.FuelCans.Value--;
            SetSawFuel(p, SawFuel.Full);
            p.SfxRpc(GameSfx.MedkitPickup, 0.8f);
            // No start sound here: a saw in the hands gets a fresh synced pull-start after the refuel (UpdateSawStarts,
            // NetPlayer.SawStart.cs plays its sounds), one in the inventory does it when it is drawn.
            p.ToastRpc($"Zatankowane: paliwo {SawFuel.Full:0}/{SawFuel.Full:0}.", 2.5f);
            Debug.Log($"[Saw] {p.Nick} refuelled the chainsaw ({before:0.0} -> {SawFuel.Full:0})");
        }

        /// <summary>With the fuses (Hiding): the canisters lie on the lanes of different levels (none when the saw is off).</summary>
        private void DealFuelCans(System.Random rnd, List<Vector3> avoid, List<Vector3> boxes)
        {
            FuelCansDealt = 0;
            if (Settings.Value.NoSaw) return;
            var levels = new List<int>();
            for (int lv = 0; lv < MapInfo.Levels; lv++) levels.Add(lv);
            Shuffle(levels, rnd);
            for (int k = 0; k < SawTuning.Cans; k++)
            {
                int lv = levels[k % levels.Count];
                var p = FuseSpots.FuseSpot(lv, rnd, avoid, boxes);
                if (p == null) { Debug.LogWarning($"[Saw] no floor spot for a fuel canister on level {lv}"); continue; }
                avoid.Add(p.Value);
                float yaw = (float)rnd.NextDouble() * 360f;
                Drops.Add(new DropState
                {
                    Id = _nextDropId++, Item = (byte)HotbarItem.FuelCan, Settled = true,
                    Pos = p.Value + Vector3.up * (FuelCanModel.HalfHeight + 0.004f), Rot = Quaternion.Euler(0f, yaw, 0f),
                });
                FuelCansDealt++;
                Debug.Log($"[Saw] fuel canister on level {lv} at {p.Value}");
            }
        }
    }
}
