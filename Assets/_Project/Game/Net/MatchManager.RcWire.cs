using System;
using System.Collections;
using System.Collections.Generic;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HideAndSeek.Game.Net
{
    /// <summary>Tuning of the fence wire / RC antenna upgrade.</summary>
    public static class WireTuning
    {
        /// <summary>Range of an RC car with the wire antenna (x <see cref="Tuning.RcRange"/>).</summary>
        public const float RangeMul = 3f; // was 1.5 (doubled 2026-11-24)
        /// <summary>Pieces of wire one player can carry (one slot).</summary>
        public const int MaxCarried = 2;
        /// <summary>Server: how far (m, flat) the player may be from the stand point of the pull.</summary>
        public const float PullReach = 1.1f;
    }

    /// <summary>Which wire-mesh spots already gave their piece of wire this round (bit per <see cref="WireSpots"/> index).</summary>
    public struct WireBits : INetworkSerializable, IEquatable<WireBits>
    {
        public ulong A, B, C, D;

        public bool Get(int i) => i >= 0 && i < 256 && ((i < 64 ? A : i < 128 ? B : i < 192 ? C : D) >> (i & 63) & 1UL) != 0;

        public WireBits With(int i)
        {
            var w = this;
            if (i < 0 || i >= 256) return w;
            ulong bit = 1UL << (i & 63);
            if (i < 64) w.A |= bit; else if (i < 128) w.B |= bit; else if (i < 192) w.C |= bit; else w.D |= bit;
            return w;
        }

        public int CountSet()
        {
            int n = 0;
            foreach (ulong v in new[] { A, B, C, D }) for (ulong x = v; x != 0; x &= x - 1) n++;
            return n;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref A);
            s.SerializeValue(ref B);
            s.SerializeValue(ref C);
            s.SerializeValue(ref D);
        }

        public bool Equals(WireBits o) => A == o.A && B == o.B && C == o.C && D == o.D;
    }

    /// <summary>
    /// Fence wire as a longer RC antenna. Hiders tear a piece of wire off any wire-mesh fence / cage
    /// (<see cref="WireSpots"/>: hold E for <see cref="WirePullTimeline.Duration"/> s, one piece per panel per round)
    /// and, with the transmitter of their car in the hands and the wire anywhere in the inventory, press U to swap the
    /// transmitter's antenna for the wire (<see cref="AntennaSwapTimeline"/>): that car's range goes up
    /// <see cref="WireTuning.RangeMul"/> times for the rest of the round, whoever steers it later. One upgrade per car.
    /// Synced: <see cref="RcWired"/> (bit per car), <see cref="WireTaken"/>, the players' <see cref="NetPlayer.WireCount"/>,
    /// <see cref="NetPlayer.WirePull"/>, <see cref="NetPlayer.AntennaSwapAt"/> - late joiners get it all.
    /// </summary>
    public partial class MatchManager
    {
        /// <summary>Bit i = RC car i wears the wire antenna (longer range).</summary>
        public readonly NetworkVariable<byte> RcWired = new();
        public readonly NetworkVariable<WireBits> WireTaken = new();

        private readonly HashSet<ulong> _wirePullCancel = new();
        private readonly RcWireTracker[] _carLooks = new RcWireTracker[RcCar.Count];
        private readonly Dictionary<int, RcWireTracker> _dropLooks = new();
        private Action _stepRcWire;

        public bool RcIsWired(int slot) => slot >= 0 && slot < 8 && (RcWired.Value & (1 << slot)) != 0;

        /// <summary>Radio range of RC car <paramref name="slot"/> (race cars / no match: the stock range).</summary>
        public static float RcRangeOf(int slot)
        {
            var mm = Instance;
            return mm != null && mm.RcIsWired(slot) ? Tuning.RcRange * WireTuning.RangeMul : Tuning.RcRange;
        }

        private bool WirePhase => Phase.Value == MatchPhase.Hiding || Phase.Value == MatchPhase.Seeking;

        // ================================================================ every peer

        private void UpdateRcWire()
        {
            foreach (var pl in NetPlayer.All)
                if (pl != null && pl.GetComponent<RcWireDriver>() == null) pl.gameObject.AddComponent<RcWireDriver>();

            // Car looks (the shelf / floor cars) and dropped transmitters / cars.
            for (int i = 0; i < RcCar.Count; i++)
            {
                var car = RcCar.Peek(i);
                if (car == null) { _carLooks[i] = null; continue; }
                (_carLooks[i] ??= new RcWireTracker(car.transform)).Retarget(car.transform);
                _carLooks[i].Apply(RcAntennaLook.Wire, 0f, RcIsWired(i));
            }
            if (Drops != null && RcWired.Value != 0)
                for (int k = 0; k < Drops.Count; k++)
                {
                    var d = Drops[k];
                    if (d.Kind != HotbarItem.RcRemote && d.Kind != HotbarItem.RcCar) continue;
                    if (!_dropViews.TryGetValue(d.Id, out var v) || v == null) continue;
                    if (!_dropLooks.TryGetValue(d.Id, out var look)) _dropLooks[d.Id] = look = new RcWireTracker(v.transform);
                    look.Retarget(v.transform);
                    bool w = RcIsWired(d.Extra);
                    look.Apply(w ? RcAntennaLook.Wire : RcAntennaLook.Stock, 0f, w);
                }
            if (_dropLooks.Count > 32) _dropLooks.Clear();

            if (!IsServer) return;
            var ph = Phase.Value;
            if (ph == MatchPhase.Lobby || ph == MatchPhase.Loading)
            {
                if (RcWired.Value != 0) RcWired.Value = 0;
                if (!WireTaken.Value.Equals(default(WireBits))) WireTaken.Value = default;
                foreach (var pl in NetPlayer.All)
                {
                    if (pl == null) continue;
                    if (pl.WireCount.Value != 0) pl.WireCount.Value = 0;
                    if (pl.WirePull.Value.Spot >= 0) pl.WirePull.Value = WirePullState.None;
                    if (pl.AntennaSwapAt.Value >= 0d) pl.AntennaSwapAt.Value = -1d;
                }
            }
        }

        // ================================================================ server: tearing wire off a fence

        public void ServerStartWirePull(NetPlayer pl, short spot, float along, sbyte side)
        {
            if (pl == null || !WirePhase || RaceMode || !pl.IsAliveHider || pl.Busy.Value || pl.Seat.Value >= 0 || pl.RcAnimating) return;
            if (pl.WirePull.Value.Spot >= 0 || pl.AntennaSwapAt.Value >= 0d) return;
            var spots = WireSpots.All;
            if (spot < 0 || spot >= spots.Count) return;
            if (WireTaken.Value.Get(spot)) { pl.ToastRpc("Tu już ktoś urwał drut.", 2f); return; }
            foreach (var o in NetPlayer.All)
                if (o != null && o != pl && o.WirePull.Value.Spot == spot) { pl.ToastRpc("Ktoś już tu urywa drut.", 2f); return; }
            if (!Inventory.HasRoomFor(pl, HotbarItem.Wire))
            {
                pl.ToastRpc(pl.WireCount.Value > 0 ? "Więcej drutu nie uniesiesz." : Inventory.FullText, 2.5f);
                return;
            }
            var s = spots[spot];
            along = s.ClampAlong(along);
            int sd = side >= 0 ? 1 : -1;
            Vector3 stand = s.Stand(along, sd), at = pl.transform.position;
            if (Mathf.Abs(at.y - s.Floor) > 0.7f || Vector2.Distance(new Vector2(at.x, at.z), new Vector2(stand.x, stand.z)) > WireTuning.PullReach) return;
            pl.WirePull.Value = new WirePullState { Spot = spot, Along = along, Side = (sbyte)sd, Start = Now };
            _wirePullCancel.Remove(pl.NetworkObjectId);
            StartCoroutine(WirePullRoutine(pl, spot));
            Debug.Log($"[RcWire] {pl.Nick} tears wire off {s.Name} (spot {spot}, along {along:0.00}, side {sd})");
        }

        public void ServerCancelWirePull(NetPlayer pl)
        {
            if (pl != null) _wirePullCancel.Add(pl.NetworkObjectId);
        }

        private IEnumerator WirePullRoutine(NetPlayer pl, short spot)
        {
            ulong id = pl.NetworkObjectId;
            double start = Now;
            Vector3 pos0 = pl.transform.position;
            bool wounded0 = pl.Wounded.Value;
            byte severed0 = pl.Severed.Value;
            string why = null;
            while (Now - start < WirePullTimeline.Duration)
            {
                yield return null;
                if (pl == null || !pl.IsSpawned) { why = "player gone"; break; }
                if (pl.Life.Value != LifeState.Alive) why = "downed";
                else if (pl.Busy.Value || pl.Severed.Value != severed0 || (pl.Wounded.Value && !wounded0)) why = "attacked";
                else if (!WirePhase) why = "round over";
                else if (_wirePullCancel.Remove(id)) why = "let go / moved";
                else if (Now - start > 0.4 && Vector3.Distance(Flat(pl.transform.position), Flat(pos0)) > 0.7f) why = "walked away";
                else if (pl.WirePull.Value.Spot != spot) why = "state changed";
                if (why != null) break;
            }
            _wirePullCancel.Remove(id);
            if (pl == null || !pl.IsSpawned) yield break;
            if (pl.WirePull.Value.Spot == spot) pl.WirePull.Value = WirePullState.None;
            if (why != null)
            {
                Debug.Log($"[RcWire] {pl.Nick}: wire pull interrupted ({why}) after {Now - start:0.0} s");
                if (why != "let go / moved") pl.ToastRpc("Przerwane.", 1.5f);
                yield break;
            }
            if (WireTaken.Value.Get(spot)) yield break;
            if (!Inventory.HasRoomFor(pl, HotbarItem.Wire)) { pl.ToastRpc(Inventory.FullText, 2.5f); yield break; }
            WireTaken.Value = WireTaken.Value.With(spot);
            pl.WireCount.Value++;
            Debug.Log($"[RcWire] {pl.Nick} got a piece of wire from spot {spot} (carries {pl.WireCount.Value}, spots used {WireTaken.Value.CountSet()}/{WireSpots.Count})");
        }

        // ================================================================ server: antenna swap

        /// <summary>Why the swap cannot start now for <paramref name="pl"/> (null = it can). Also read by the client prompt.</summary>
        public string AntennaSwapBlock(NetPlayer pl)
        {
            if (pl == null || !WirePhase || RaceMode || !pl.IsAliveHider) return "phase";
            if (pl.WireCount.Value == 0) return "no wire";
            int i = RcRemoteSlotOf(pl);
            if (i < 0 || RcDestroyed(i)) return "no transmitter";
            if (RcIsWired(i)) return "wired";
            var s = RcSlot(i);
            if (s.Mode != RcMode.Parked) return "car not parked";
            if (!pl.RcRemoteOut.Value || pl.RcAnimating || pl.Busy.Value || pl.Seat.Value >= 0 || pl.WirePull.Value.Spot >= 0) return "hands";
            if (pl.AntennaSwapAt.Value >= 0d) return "running";
            return null;
        }

        public void ServerStartAntennaSwap(NetPlayer pl)
        {
            string block = AntennaSwapBlock(pl);
            if (block != null)
            {
                if (pl != null && block == "wired") pl.ToastRpc("Ten samochodzik ma już antenę z drutu.", 2f);
                return;
            }
            int i = RcRemoteSlotOf(pl);
            pl.AntennaSwapAt.Value = Now;
            StartCoroutine(AntennaSwapRoutine(pl, i));
            Debug.Log($"[RcWire] {pl.Nick} swaps the antenna of car {i}");
        }

        private IEnumerator AntennaSwapRoutine(NetPlayer pl, int i)
        {
            double start = Now;
            string why = null;
            bool installed = false;
            while (Now - start < AntennaSwapTimeline.End)
            {
                yield return null;
                if (pl == null || !pl.IsSpawned) yield break;
                float t = (float)(Now - start);
                if (!installed)
                {
                    if (pl.Life.Value != LifeState.Alive) why = "downed";
                    else if (!WirePhase) why = "round over";
                    else if (RcRemoteSlotOf(pl) != i || RcDestroyed(i)) why = "transmitter gone";
                    else if (RcSlot(i).Mode != RcMode.Parked) why = "car moved";
                    else if (!pl.RcRemoteOut.Value || pl.Busy.Value) why = "put away";
                    else if (pl.WireCount.Value == 0) why = "wire gone";
                    if (why != null) break;
                    if (t >= AntennaSwapTimeline.Install)
                    {
                        installed = true;
                        pl.WireCount.Value--;
                        RcWired.Value = (byte)(RcWired.Value | (1 << i));
                        Debug.Log($"[RcWire] {pl.Nick}: car {i} wears the wire antenna, range {Tuning.RcRange:0} -> {RcRangeOf(i):0} m");
                    }
                }
                else if (pl.Life.Value != LifeState.Alive) break;
            }
            if (pl == null || !pl.IsSpawned) yield break;
            pl.AntennaSwapAt.Value = -1d;
            if (why != null)
            {
                Debug.Log($"[RcWire] {pl.Nick}: antenna swap of car {i} interrupted ({why})");
                pl.ToastRpc("Przerwane - stara antena została.", 2f);
            }
        }
    }
}
