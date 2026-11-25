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
            p.SfxRpc(GameSfx.ChainsawStart, 0.5f);
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
