using System;
using HideAndSeek.Audio;
using HideAndSeek.Core;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>What each RC car carries on its roof (one item per car; None = empty). Written by the server.</summary>
    public struct RcCargoState : INetworkSerializable, IEquatable<RcCargoState>
    {
        public byte Item0, Extra0, Item1, Extra1;

        public HotbarItem Item(int i) => (HotbarItem)(i == 0 ? Item0 : Item1);
        public byte Extra(int i) => i == 0 ? Extra0 : Extra1;

        public void Set(int i, HotbarItem item, byte extra)
        {
            if (i == 0) { Item0 = (byte)item; Extra0 = extra; }
            else { Item1 = (byte)item; Extra1 = extra; }
        }

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Item0);
            s.SerializeValue(ref Extra0);
            s.SerializeValue(ref Item1);
            s.SerializeValue(ref Extra1);
        }

        public bool Equals(RcCargoState o) => Item0 == o.Item0 && Extra0 == o.Extra0 && Item1 == o.Item1 && Extra1 == o.Extra1;
    }

    /// <summary>Tuning of the RC car picking items up.</summary>
    public static class RcCargo
    {
        /// <summary>Car centre to the item (horizontal, server side on the driver's 20 Hz pose).</summary>
        public const float GrabRadius = 0.5f, GrabHeight = 0.6f;
        /// <summary>E on the car: how far from the player's eyes.</summary>
        public const float TakeRange = 2.3f;

        /// <summary>Small things a hider uses: medkit, adrenaline syringe, the 8 fuses.</summary>
        public static bool Grabbable(HotbarItem k) => k == HotbarItem.Medkit || k == HotbarItem.Syringe || FuseItems.Is(k);
    }

    /// <summary>
    /// The RC car picks items up (2026-11-24): a driven car that runs into a medkit, a syringe or a fuse lying on the floor
    /// (from the map or dropped) takes it onto its roof at once - one item per car. Anybody allowed to have the item takes it
    /// off with E next to the car (<see cref="ServerRcCargoTake"/>); a car picked up into the inventory hands its load to
    /// whoever took it (or drops it at his feet when there is no room); a crushed / shot car loses it onto the floor.
    /// </summary>
    public partial class MatchManager
    {
        public readonly NetworkVariable<RcCargoState> RcCargoVar = new();

        public HotbarItem RcCargoOf(int i) => i >= 0 && i < RcCar.Count ? RcCargoVar.Value.Item(i) : HotbarItem.None;

        /// <summary>Items grabbed by the cars this session (tests).</summary>
        public int RcCargoGrabs { get; private set; }

        private void UpdateRcCargo(bool inGame)
        {
            RcCargoViews.Sync(inGame);
            if (!IsServer) return;
            var ph = Phase.Value;
            if (!inGame || (ph != MatchPhase.Hiding && ph != MatchPhase.Seeking))
            {
                if (ph != MatchPhase.Ended && !RcCargoVar.Value.Equals(default)) RcCargoVar.Value = default;
                return;
            }
            for (int i = 0; i < RcCar.Count; i++)
            {
                var s = RcSlot(i);
                var have = RcCargoOf(i);
                if (have == HotbarItem.None)
                {
                    if (s.Mode == RcMode.Driving) TryGrab(i, s);
                    continue;
                }
                switch (s.Mode)
                {
                    case RcMode.Carried:
                    {
                        // Picked up into somebody's inventory: the load goes with it.
                        var pl = NetPlayer.Find(s.Holder);
                        byte extra = RcCargoVar.Value.Extra(i);
                        var def = ItemDefs.Get(have);
                        if (pl != null && Inventory.CanPickUp(pl, have, ph) && Inventory.HasRoomFor(pl, have) && def?.Give != null && def.Give(pl, extra))
                        {
                            pl.ToastRpc($"{def.Name} z samochodzika - masz go w ekwipunku.", 3f);
                            Debug.Log($"[RcCargo] car {i}: {have} went to {pl.Nick} with the car");
                        }
                        else if (pl != null) SpillCargo(i, pl.transform.position + pl.transform.forward * 0.4f);
                        else SpillCargo(i, s.Pos);
                        ClearCargo(i);
                        break;
                    }
                    case RcMode.Wreck:
                    case RcMode.Shot:
                    case RcMode.Shelf:
                        RcWhere(i, out var pos, out _);
                        SpillCargo(i, pos);
                        ClearCargo(i);
                        break;
                }
            }
        }

        private void ClearCargo(int i)
        {
            var c = RcCargoVar.Value;
            c.Set(i, HotbarItem.None, 0);
            RcCargoVar.Value = c;
        }

        private void SpillCargo(int i, Vector3 at)
        {
            var item = RcCargoOf(i);
            if (item == HotbarItem.None) return;
            Vector3 dir = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f) * Vector3.forward;
            SpawnDrop(item, RcCargoVar.Value.Extra(i), at + Vector3.up * 0.3f, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f),
                dir * 0.8f + Vector3.up * 1.2f, at);
            Debug.Log($"[RcCargo] car {i}: {item} fell off at {at}");
        }

        /// <summary>A driven car touching a grabbable item: onto the roof.</summary>
        private void TryGrab(int i, RcSlotState s)
        {
            RcWhere(i, out var car, out _);
            bool Near(Vector3 p)
            {
                float dx = p.x - car.x, dz = p.z - car.z;
                return dx * dx + dz * dz <= RcCargo.GrabRadius * RcCargo.GrabRadius && Mathf.Abs(p.y - car.y) <= RcCargo.GrabHeight;
            }
            for (int k = 0; k < Medkits.Count; k++)
            {
                var m = Medkits[k];
                if (m.Taken || !Near(m.Position)) continue;
                m.Taken = true;
                Medkits[k] = m;
                Grabbed(i, s, HotbarItem.Medkit, 0);
                return;
            }
            for (int k = 0; k < Syringes.Count; k++)
            {
                var m = Syringes[k];
                if (m.Taken || !Near(m.Position)) continue;
                m.Taken = true;
                Syringes[k] = m;
                Grabbed(i, s, HotbarItem.Syringe, 0);
                return;
            }
            for (int k = 0; k < Drops.Count; k++)
            {
                var d = Drops[k];
                if (!d.Settled || !RcCargo.Grabbable(d.Kind) || !Near(DropPosition(d))) continue;
                Drops.RemoveAt(k);
                _dropAnchor.Remove(d.Id);
                Grabbed(i, s, d.Kind, d.Extra);
                return;
            }
        }

        private void Grabbed(int i, RcSlotState s, HotbarItem item, byte extra)
        {
            var c = RcCargoVar.Value;
            c.Set(i, item, extra);
            RcCargoVar.Value = c;
            RcCargoGrabs++;
            var def = ItemDefs.Get(item);
            var driver = NetPlayer.Find(s.Holder);
            driver?.ToastRpc($"Samochodzik podniósł: {def?.Name ?? item.ToString()}. Podjedź do kogoś - E przy aucie zabiera.", 3.5f);
            Debug.Log($"[RcCargo] car {i} picked up {item} (driver {driver?.Nick ?? "-"})");
        }

        /// <summary>Tests (server): an item dropped from just above <paramref name="floor"/>.</summary>
        public void TestSpawnDrop(HotbarItem item, Vector3 floor) =>
            SpawnDrop(item, 0, floor + Vector3.up * 0.25f, Quaternion.Euler(0f, 30f, 0f), Vector3.zero, floor);

        /// <summary>E next to a car carrying something: the item goes into the player's inventory.</summary>
        public void ServerRcCargoTake(NetPlayer pl, int i)
        {
            if (pl == null || i < 0 || i >= RcCar.Count || !RcPhase || pl.Life.Value != LifeState.Alive || pl.Busy.Value) return;
            var item = RcCargoOf(i);
            if (item == HotbarItem.None) return;
            var def = ItemDefs.Get(item);
            if (def?.Give == null || !Inventory.CanPickUp(pl, item, Phase.Value)) return;
            RcWhere(i, out var car, out _);
            if (Vector3.Distance(pl.transform.position + Vector3.up * 0.9f, car) > RcCargo.TakeRange + 1.2f) return;
            if (!Inventory.HasRoomFor(pl, item)) { pl.ToastRpc(Inventory.Count(pl, item) > 0 ? $"{def.Name}: masz już max." : Inventory.FullText, 2.5f); return; }
            if (!def.Give(pl, RcCargoVar.Value.Extra(i))) return;
            ClearCargo(i);
            pl.SfxRpc(GameSfx.MedkitPickup, 0.8f);
            int slot = Inventory.PredictSlot(pl, item);
            pl.ToastRpc(slot >= 0 ? $"{def.Name} z samochodzika (slot {slot + 1})" : $"{def.Name} z samochodzika", 2.5f);
            Debug.Log($"[RcCargo] {pl.Nick} took {item} off car {i}");
        }
    }
}
