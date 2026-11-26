using System;
using HideAndSeek.Core;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>Camera power of the three security rooms (one battery tray + switch per room; room = level 0, 1, 2).</summary>
    public struct CamPowerState : INetworkSerializable, IEquatable<CamPowerState>
    {
        /// <summary>Bit per room: a battery stands in the tray.</summary>
        public byte Battery;
        /// <summary>Bit per room: the switch is on.</summary>
        public byte On;
        /// <summary>Charge of the battery in the tray, per cent rounded up (0 = dead / none).</summary>
        public byte P0, P1, P2;

        public bool HasBattery(int room) => room >= 0 && room < 3 && (Battery & (1 << room)) != 0;
        public bool IsOn(int room) => room >= 0 && room < 3 && (On & (1 << room)) != 0;
        public int Percent(int room) => room == 0 ? P0 : room == 1 ? P1 : room == 2 ? P2 : 0;
        /// <summary>The monitors of this room work: a charged battery in the tray and the switch on.</summary>
        public bool Powered(int room) => HasBattery(room) && IsOn(room) && Percent(room) > 0;
        public bool AnyPowered => Powered(0) || Powered(1) || Powered(2);

        public void SetPercent(int room, int pct)
        {
            byte v = (byte)Mathf.Clamp(pct, 0, 100);
            if (room == 0) P0 = v; else if (room == 1) P1 = v; else if (room == 2) P2 = v;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Battery);
            s.SerializeValue(ref On);
            s.SerializeValue(ref P0);
            s.SerializeValue(ref P1);
            s.SerializeValue(ref P2);
        }

        public bool Equals(CamPowerState o) => Battery == o.Battery && On == o.On && P0 == o.P0 && P1 == o.P1 && P2 == o.P2;
    }

    /// <summary>
    /// Camera power (26.11): at the start of a round every security room is dark - no battery in its power tray, the
    /// switch off, the monitors show "BRAK ZASILANIA" and the garage cameras' LEDs are out. One battery per room powers
    /// that room's monitors (each level's hiders need their own, and a room can be cut off by taking its battery).
    /// A hider puts a car battery into the tray (E) - a dead one: "Akumulator jest rozładowany"; anybody alive flips the
    /// switch (E). While on, the battery drains (<see cref="BatteryTuning.FullLasts"/> s for a full one, i.e. 4-6 min
    /// from the usual 65-100 %); empty = the room goes dark ("AKUMULATOR ROZŁADOWANY"). A battery (also an empty one)
    /// can be taken out of the tray again (E). Server-authoritative, synced through <see cref="CamPower"/>.
    /// </summary>
    public partial class MatchManager
    {
        public readonly NetworkVariable<CamPowerState> CamPower = new();

        private readonly float[] _roomCharge = new float[3];   // server: exact charge (%) of the battery in each tray

        private void UpdateCamPower(float dt)
        {
            var ph = Phase.Value;
            if (ph == MatchPhase.Lobby || ph == MatchPhase.Loading)
            {
                if (!CamPower.Value.Equals(default(CamPowerState))) CamPower.Value = default;
                Array.Clear(_roomCharge, 0, _roomCharge.Length);
                return;
            }
            if (!HoodPhase) return;
            var s = CamPower.Value;
            bool changed = false;
            for (int r = 0; r < 3; r++)
            {
                if (!s.Powered(r)) continue;
                _roomCharge[r] = Mathf.Max(0f, _roomCharge[r] - dt * 100f / BatteryTuning.FullLasts);
                int pct = Mathf.CeilToInt(_roomCharge[r] - 0.0001f);
                if (pct == s.Percent(r)) continue;
                s.SetPercent(r, pct);
                changed = true;
                if (pct == 0)
                {
                    Debug.Log($"[Battery] room {SecurityRooms.LevelName(r)}: battery empty, cameras off");
                    ToastRoom(r, "AKUMULATOR ROZŁADOWANY - kamery zgasły.");
                    CarHoodFxRpc(2, SecurityRooms.PowerSwitchPoint(r));
                }
            }
            if (changed) CamPower.Value = s;
        }

        /// <summary>Tells everybody standing in security room <paramref name="room"/>.</summary>
        private static void ToastRoom(int room, string text)
        {
            foreach (var p in NetPlayer.All)
                if (p != null && p.Life.Value != LifeState.Dead && SecurityRooms.InRoom(p.transform.position, room)) p.ToastRpc(text, 3f);
        }

        /// <summary>E at the battery tray: put the battery carried in, or take the one in the tray out (hiders).</summary>
        public void ServerCamBattery(NetPlayer pl, byte room)
        {
            if (pl == null) return;
            if (room > 2 || !HoodPhase || !pl.IsAliveHider || pl.Busy.Value) { Refused(pl, "battery tray " + room, "phase / not an alive hider / busy"); return; }
            float d = Vector3.Distance(pl.transform.position + Vector3.up * 1.2f, SecurityRooms.PowerSlotPoint(room));
            if (d > 2.4f) { Refused(pl, "battery tray " + room, $"too far ({d:0.00} m)"); return; }
            var s = CamPower.Value;
            if (!s.HasBattery(room))
            {
                if (!pl.HasCarBattery) { pl.ToastRpc("Nie masz akumulatora. Znajdziesz go pod maską któregoś auta.", 2.5f); return; }
                int charge = pl.CarBatteryCharge;
                pl.CarBattery.Value = 0;
                _roomCharge[room] = charge;
                s.Battery = (byte)(s.Battery | (1 << room));
                s.SetPercent(room, charge);
                CamPower.Value = s;
                CarHoodFxRpc(5, SecurityRooms.PowerSlotPoint(room));
                Debug.Log($"[Battery] {pl.Nick} connected a battery in room {SecurityRooms.LevelName(room)} (charge {charge}%, switch {(s.IsOn(room) ? "on" : "off")})");
                if (charge <= 0) pl.ToastRpc("Akumulator jest rozładowany. Kamery nie ruszą - poszukaj innego.", 3.5f);
                else if (!s.IsOn(room)) pl.ToastRpc("Akumulator podłączony. Włącz kamery przełącznikiem nad nim.", 3f);
                return;
            }
            if (pl.HasCarBattery) { pl.ToastRpc("W zasilaniu już stoi akumulator - najpierw go wyjmij.", 2.5f); return; }
            if (!Inventory.HasRoomFor(pl, HotbarItem.CarBattery)) { pl.ToastRpc(Inventory.FullText, 2.5f); return; }
            int left = Mathf.Clamp(s.Percent(room), 0, 100);
            pl.CarBattery.Value = (byte)(1 + left);
            _roomCharge[room] = 0f;
            s.Battery = (byte)(s.Battery & ~(1 << room));
            s.SetPercent(room, 0);
            CamPower.Value = s;
            CarHoodFxRpc(4, SecurityRooms.PowerSlotPoint(room));
            Debug.Log($"[Battery] {pl.Nick} took the battery out of room {SecurityRooms.LevelName(room)} ({left}%)");
        }

        /// <summary>E at the power switch (anybody alive - the seeker can switch the cameras off too).</summary>
        public void ServerCamSwitch(NetPlayer pl, byte room)
        {
            if (pl == null) return;
            if (room > 2 || !HoodPhase || pl.Life.Value != LifeState.Alive || pl.Escaped.Value || pl.Busy.Value) { Refused(pl, "camera switch " + room, "phase / not alive / busy"); return; }
            float d = Vector3.Distance(pl.transform.position + Vector3.up * 1.2f, SecurityRooms.PowerSwitchPoint(room));
            if (d > 2.4f) { Refused(pl, "camera switch " + room, $"too far ({d:0.00} m)"); return; }
            var s = CamPower.Value;
            bool on = !s.IsOn(room);
            s.On = (byte)(on ? s.On | (1 << room) : s.On & ~(1 << room));
            CamPower.Value = s;
            CarHoodFxRpc(6, SecurityRooms.PowerSwitchPoint(room));
            Debug.Log($"[Battery] {pl.Nick} switched the cameras of room {SecurityRooms.LevelName(room)} {(on ? "ON" : "off")} (battery {(s.HasBattery(room) ? s.Percent(room) + "%" : "none")})");
            if (!on) return;
            if (!s.HasBattery(room)) pl.ToastRpc("Brak zasilania - podłącz akumulator (skrzynka pod przełącznikiem).", 3f);
            else if (s.Percent(room) <= 0) pl.ToastRpc("Akumulator jest rozładowany.", 2.5f);
        }
    }
}
