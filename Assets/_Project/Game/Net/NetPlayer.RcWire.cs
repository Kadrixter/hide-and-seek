using System;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>A piece of wire being torn off a fence: which spot (<see cref="WireSpots"/>), where along it, from which side, since when.</summary>
    public struct WirePullState : INetworkSerializable, IEquatable<WirePullState>
    {
        public short Spot;   // -1 = not pulling
        public float Along;  // m along the panel from its centre
        public sbyte Side;   // +1 / -1: the side of the panel the player stands on (along the spot's normal)
        public double Start; // server time

        public static WirePullState None => new WirePullState { Spot = -1 };

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Spot);
            s.SerializeValue(ref Along);
            s.SerializeValue(ref Side);
            s.SerializeValue(ref Start);
        }

        public bool Equals(WirePullState o) => Spot == o.Spot && Along == o.Along && Side == o.Side && Start == o.Start;
    }

    /// <summary>
    /// Fence wire on the player (RC antenna upgrade, MatchManager.RcWire): pieces carried (<see cref="WireCount"/>,
    /// the "Kawałek drutu" inventory item), tearing one off a wire-mesh fence (<see cref="WirePull"/>, played by
    /// every client from the server clock, late joiners too) and the antenna swap on the transmitter in the hands
    /// (<see cref="AntennaSwapAt"/>). All written by the server.
    /// </summary>
    public partial class NetPlayer
    {
        public readonly NetworkVariable<byte> WireCount = new();
        public readonly NetworkVariable<WirePullState> WirePull = new(WirePullState.None);
        /// <summary>Server time the antenna swap started (-1 = none running).</summary>
        public readonly NetworkVariable<double> AntennaSwapAt = new(-1d);

        /// <summary>Tearing wire off a fence right now (hands busy: no item in the hand, no transmitter).</summary>
        public bool WireHandsBusy => WirePull.Value.Spot >= 0;

        /// <summary>Seconds into the antenna swap (-1 = none).</summary>
        public float AntennaSwapTime
        {
            get
            {
                var mm = MatchManager.Instance;
                if (mm == null || AntennaSwapAt.Value < 0d) return -1f;
                return Mathf.Max(0f, (float)(mm.Now - AntennaSwapAt.Value));
            }
        }

        [Rpc(SendTo.Server)]
        public void RequestWirePullRpc(short spot, float along, sbyte side) => MatchManager.Instance?.ServerStartWirePull(this, spot, along, side);

        [Rpc(SendTo.Server)]
        public void RequestWirePullCancelRpc() => MatchManager.Instance?.ServerCancelWirePull(this);

        [Rpc(SendTo.Server)]
        public void RequestAntennaSwapRpc() => MatchManager.Instance?.ServerStartAntennaSwap(this);
    }
}
