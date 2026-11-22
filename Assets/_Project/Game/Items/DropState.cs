using System;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// An item lying on the floor after somebody dropped it (Q) or it did not fit the inventory. Server-written.
    /// Until <see cref="Settled"/> every peer simulates the fall itself from the same start (position, rotation,
    /// velocity, spin); then the server's resting pose is written here and every view eases onto it.
    /// </summary>
    public struct DropState : INetworkSerializable, IEquatable<DropState>
    {
        public int Id;
        public byte Item;      // HotbarItem
        public byte Extra;     // item payload (RC car: its slot)
        public bool Settled;
        public Vector3 Pos;    // start, then the resting position
        public Quaternion Rot; // start, then the resting rotation
        public Vector3 Vel, Spin;

        public HotbarItem Kind => (HotbarItem)Item;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Id);
            s.SerializeValue(ref Item);
            s.SerializeValue(ref Extra);
            s.SerializeValue(ref Settled);
            s.SerializeValue(ref Pos);
            s.SerializeValue(ref Rot);
            s.SerializeValue(ref Vel);
            s.SerializeValue(ref Spin);
        }

        public bool Equals(DropState o) => Id == o.Id && Item == o.Item && Extra == o.Extra && Settled == o.Settled &&
                                           Pos == o.Pos && Rot == o.Rot && Vel == o.Vel && Spin == o.Spin;
    }
}
