using System;
using Unity.Netcode;

namespace HideAndSeek.Game.Net
{
    /// <summary>What a player does at a car bonnet (<see cref="HideAndSeek.Map.CarHood"/>).</summary>
    public enum HoodWorkMode : byte
    {
        None,
        /// <summary>Picking the bonnet latch with a piece of wire (the local lockpick mini-game is open).</summary>
        Lockpick,
        /// <summary>Lifting the unlatched bonnet (<see cref="HoodWorkState.Duration"/> s, the bonnet creaks up on every client).</summary>
        Opening,
    }

    /// <summary>A player working on a bonnet: which hood, what, since when (server time) and for how long.</summary>
    public struct HoodWorkState : INetworkSerializable, IEquatable<HoodWorkState>
    {
        public short Hood;       // CarHood.Index, -1 = none (short since 27.11: up to 256 bonnets)
        public HoodWorkMode Mode;
        public double Start;
        public float Duration;   // opening only

        public static HoodWorkState None => new HoodWorkState { Hood = -1 };
        public bool Active => Hood >= 0 && Mode != HoodWorkMode.None;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Hood);
            s.SerializeValue(ref Mode);
            s.SerializeValue(ref Start);
            s.SerializeValue(ref Duration);
        }

        public bool Equals(HoodWorkState o) => Hood == o.Hood && Mode == o.Mode && Start == o.Start && Duration == o.Duration;
    }

    /// <summary>
    /// Car batteries on the player (MatchManager.CarBatteries / CamPower): the battery carried (<see cref="CarBattery"/>,
    /// the "Akumulator" item: 0 = none, else 1 + its charge in per cent - a dead one is 1, it looks the same) and the work
    /// at a bonnet (<see cref="HoodWork"/>: lockpick, lifting it). All written by the server; late joiners get both.
    /// </summary>
    public partial class NetPlayer
    {
        public readonly NetworkVariable<byte> CarBattery = new();
        public readonly NetworkVariable<HoodWorkState> HoodWork = new(HoodWorkState.None);

        public bool HasCarBattery => CarBattery.Value > 0;

        /// <summary>Charge (0..100 %) of the battery carried (server use: the HUD never shows it).</summary>
        public int CarBatteryCharge => Math.Max(0, CarBattery.Value - 1);

        /// <summary>Walking with the heavy battery: a little slower (NetPlayer.CombatSpeed).</summary>
        public float BatterySpeedScale => CarBattery.Value > 0 ? BatteryTuning.CarrySpeedScale : 1f;

        /// <summary>E at a bonnet: start the lockpick (or lift an already unlatched bonnet).</summary>
        [Rpc(SendTo.Server)]
        public void RequestHoodRpc(byte hood) => MatchManager.Instance?.ServerHoodRequest(this, hood);

        /// <summary>End of one lockpick try: won (all pins set) or lost (the wire breaks).</summary>
        [Rpc(SendTo.Server)]
        public void LockpickResultRpc(bool won, byte pins) => MatchManager.Instance?.ServerLockpickResult(this, won, pins);

        /// <summary>Esc in the lockpick / E while lifting the bonnet: stop (no wire lost).</summary>
        [Rpc(SendTo.Server)]
        public void HoodStopRpc() => MatchManager.Instance?.ServerHoodStop(this);

        [Rpc(SendTo.Server)]
        public void RequestBatteryTakeRpc(byte hood) => MatchManager.Instance?.ServerBatteryTake(this, hood);

        /// <summary>E at the power tray of a security room: connect the battery carried / take the one in the tray out.</summary>
        [Rpc(SendTo.Server)]
        public void RequestCamBatteryRpc(byte room) => MatchManager.Instance?.ServerCamBattery(this, room);

        /// <summary>E at the camera power switch of a security room.</summary>
        [Rpc(SendTo.Server)]
        public void RequestCamSwitchRpc(byte room) => MatchManager.Instance?.ServerCamSwitch(this, room);
    }
}
